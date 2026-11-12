using System;
using System.Buffers;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>The final drawn land triangles, indexed by their existing grid cells.
    /// Refined banks differ from the regular height grid; movement and placement
    /// must use those triangles instead of walking through the visible surface.</summary>
    public sealed class TerrainContactSurface
    {
        readonly float[] positions;
        readonly int[] triangles,offsets,entries;
        readonly int segments;
        readonly double size,step;
        float[]? currentHeights;
        public long Bytes=>(long)(positions.Length+(currentHeights?.Length??0))*4+(long)(triangles.Length+offsets.Length+entries.Length)*4;

        TerrainContactSurface(LocalTerrainMesh mesh)
        {
            segments=mesh.Segments;size=mesh.SizeM;step=size/segments;
            positions=(float[])mesh.Positions.Clone();triangles=(int[])mesh.Triangles.Clone();
            offsets=new int[segments*segments+1];
            // Both CSR passes use the exact same triangle-cell bounds. Keep
            // them only during construction instead of recalculating them.
            var bounds=ArrayPool<int>.Shared.Rent(checked(triangles.Length/3*4));
            try
            {
                for(int i=0,at=0;i<triangles.Length;i+=3,at+=4)
                {
                    Bounds(i,out int x0,out int z0,out int x1,out int z1);
                    bounds[at]=x0;bounds[at+1]=z0;bounds[at+2]=x1;bounds[at+3]=z1;
                    for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)offsets[z*segments+x+1]++;
                }
                for(int i=1;i<offsets.Length;i++)offsets[i]+=offsets[i-1];
                entries=new int[offsets[offsets.Length-1]];var cursor=(int[])offsets.Clone();
                for(int i=0,at=0;i<triangles.Length;i+=3,at+=4)
                {
                    int x0=bounds[at],z0=bounds[at+1],x1=bounds[at+2],z1=bounds[at+3];
                    for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)entries[cursor[z*segments+x]++]=i;
                }
            }
            finally{ArrayPool<int>.Shared.Return(bounds);}
        }

        /// <summary>Ordinary grids already have exact interpolation. Refined
        /// meshes need their final triangles at every detail level, including
        /// when a resident coarse edge is fitted to a finer neighbour.</summary>
        public static TerrainContactSurface? Build(LocalTerrainMesh mesh)
            =>mesh.Positions.Length/3>mesh.GroundVertices||mesh.Triangles.Length!=mesh.Segments*mesh.Segments*6
                ?new TerrainContactSurface(mesh):null;

        public float OriginalHeight(int vertex)=>positions[vertex*3+1];
        public void SetHeight(int vertex,float height)
        {
            float original=OriginalHeight(vertex);
            if(currentHeights==null)
            {
                if(height==original)return;
                currentHeights=new float[positions.Length/3];
                for(int i=0;i<currentHeights.Length;i++)currentHeights[i]=positions[i*3+1];
            }
            currentHeights[vertex]=height;
        }

        /// <summary>Conservative regular-grid cells of the actual triangles
        /// incident to edited vertices. Invalid indices return null so callers
        /// retain whole-tile correction rather than omit a changed surface.</summary>
        public TerrainHeightChange? ChangedTriangleCells(int gridSegments,IReadOnlyList<int> vertices)
        {
            if(gridSegments!=segments)return null;
            var change=new TerrainHeightChange(gridSegments);
            foreach(int vertex in vertices)
            {
                if(vertex<0||vertex>=positions.Length/3)return null;
                double x=positions[vertex*3],z=positions[vertex*3+2];
                int x0=Cell(x-1e-5),x1=Cell(x+1e-5),z0=Cell(z-1e-5),z1=Cell(z+1e-5);
                // Every incident triangle includes this vertex's point, and is
                // already indexed in one of its adjacent cells. No global scan
                // or persistent vertex-to-triangle index is necessary.
                for(int cz=z0;cz<=z1;cz++)for(int cx=x0;cx<=x1;cx++)
                {
                    int cell=cz*segments+cx;
                    for(int at=offsets[cell];at<offsets[cell+1];at++)
                    {
                        int triangle=entries[at];
                        if(triangles[triangle]!=vertex&&triangles[triangle+1]!=vertex&&triangles[triangle+2]!=vertex)continue;
                        Bounds(triangle,out int left,out int bottom,out int right,out int top);
                        change.MarkBounds(left,bottom,right,top);
                    }
                }
            }
            return change.Empty?null:change;
        }

        int Cell(double value)=>Math.Max(0,Math.Min(segments-1,(int)Math.Floor(value/step)));
        void Bounds(int triangle,out int x0,out int z0,out int x1,out int z1)
        {
            int a=triangles[triangle]*3,b=triangles[triangle+1]*3,c=triangles[triangle+2]*3;
            x0=Cell(Math.Min(positions[a],Math.Min(positions[b],positions[c]))-1e-5);
            x1=Cell(Math.Max(positions[a],Math.Max(positions[b],positions[c]))+1e-5);
            z0=Cell(Math.Min(positions[a+2],Math.Min(positions[b+2],positions[c+2]))-1e-5);
            z1=Cell(Math.Max(positions[a+2],Math.Max(positions[b+2],positions[c+2]))+1e-5);
        }

        public bool TryHeight(double x,double z,out double height)=>TryHeight(x,z,false,out height);
        public bool TryOriginalHeight(double x,double z,out double height)=>TryHeight(x,z,true,out height);
        /// <summary>Triangle-profile knots on a local straight cut. Resident
        /// LOD boundaries use these to retain real refined river-bank bends;
        /// the existing cell index limits the scan to the requested interval.
        /// Duplicates are intentional and sorted/deduplicated by the caller.</summary>
        public void AddProfileCuts(bool vertical,double fixedAt,double alongStart,double alongEnd,List<double> cuts)
        {
            double epsilon=Math.Max(.00001,size*1e-8);
            if(fixedAt< -epsilon||fixedAt>size+epsilon||alongEnd<0||alongStart>size)return;
            fixedAt=Math.Max(0,Math.Min(size,fixedAt));alongStart=Math.Max(0,alongStart);alongEnd=Math.Min(size,alongEnd);
            int first=Cell(alongStart-epsilon),last=Cell(alongEnd+epsilon),across0=Cell(fixedAt-epsilon),across1=Cell(fixedAt+epsilon);
            void Edge(int a,int b)
            {
                double da=positions[a+(vertical?0:2)]-fixedAt,db=positions[b+(vertical?0:2)]-fixedAt;
                double aa=positions[a+(vertical?2:0)],ab=positions[b+(vertical?2:0)];
                if(Math.Abs(da)<=epsilon&&aa>=alongStart-epsilon&&aa<=alongEnd+epsilon)cuts.Add(Math.Max(alongStart,Math.Min(alongEnd,aa)));
                if(da*db<0)
                {double along=aa+(ab-aa)*da/(da-db);if(along>=alongStart-epsilon&&along<=alongEnd+epsilon)cuts.Add(Math.Max(alongStart,Math.Min(alongEnd,along)));}
            }
            for(int along=first;along<=last;along++)for(int across=across0;across<=across1;across++)
            {
                int cell=vertical?along*segments+across:across*segments+along;
                for(int at=offsets[cell];at<offsets[cell+1];at++)
                {
                    int triangle=entries[at],a=triangles[triangle]*3,b=triangles[triangle+1]*3,c=triangles[triangle+2]*3;
                    Edge(a,b);Edge(b,c);Edge(c,a);
                }
            }
        }
        bool TryHeight(double x,double z,bool original,out double height)
        {
            height=double.NegativeInfinity;
            if(x<-.0001||z<-.0001||x>size+.0001||z>size+.0001||double.IsNaN(x)||double.IsNaN(z))return false;
            int cell=Cell(x)+Cell(z)*segments;var heights=original?null:currentHeights;
            for(int at=offsets[cell];at<offsets[cell+1];at++)
            {
                int i=entries[at],a=triangles[i]*3,b=triangles[i+1]*3,c=triangles[i+2]*3;
                double ax=positions[a],az=positions[a+2],bx=positions[b]-ax,bz=positions[b+2]-az,cx=positions[c]-ax,cz=positions[c+2]-az;
                double determinant=bx*cz-bz*cx;if(Math.Abs(determinant)<1e-12)continue;
                double px=x-ax,pz=z-az,u=(px*cz-pz*cx)/determinant,v=(bx*pz-bz*px)/determinant;
                if(u< -1e-7||v< -1e-7||u+v>1.0000001)continue;
                double ha=heights==null?positions[a+1]:heights[triangles[i]],hb=heights==null?positions[b+1]:heights[triangles[i+1]],hc=heights==null?positions[c+1]:heights[triangles[i+2]];
                double y=ha+u*(hb-ha)+v*(hc-ha);
                height=Math.Max(height,y);
            }
            return !double.IsNegativeInfinity(height);
        }
    }
}

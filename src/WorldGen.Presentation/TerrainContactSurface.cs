using System;

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
        public long Bytes=>(long)positions.Length*4+(long)(triangles.Length+offsets.Length+entries.Length)*4;

        TerrainContactSurface(LocalTerrainMesh mesh)
        {
            segments=mesh.Segments;size=mesh.SizeM;step=size/segments;
            positions=(float[])mesh.Positions.Clone();triangles=(int[])mesh.Triangles.Clone();
            offsets=new int[segments*segments+1];
            for(int i=0;i<triangles.Length;i+=3)
            {
                Bounds(i,out int x0,out int z0,out int x1,out int z1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)offsets[z*segments+x+1]++;
            }
            for(int i=1;i<offsets.Length;i++)offsets[i]+=offsets[i-1];
            entries=new int[offsets[offsets.Length-1]];var cursor=(int[])offsets.Clone();
            for(int i=0;i<triangles.Length;i+=3)
            {
                Bounds(i,out int x0,out int z0,out int x1,out int z1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)entries[cursor[z*segments+x]++]=i;
            }
        }

        /// <summary>Ordinary grids already have exact interpolation. Keep extra
        /// collision data only for refined nearby tiles, never distant backdrops.</summary>
        public static TerrainContactSurface? Build(LocalTerrainMesh mesh)
            =>mesh.SizeM/mesh.Segments<=64&&(mesh.Positions.Length/3>mesh.GroundVertices||mesh.Triangles.Length!=mesh.Segments*mesh.Segments*6)
                ?new TerrainContactSurface(mesh):null;

        int Cell(double value)=>Math.Max(0,Math.Min(segments-1,(int)Math.Floor(value/step)));
        void Bounds(int triangle,out int x0,out int z0,out int x1,out int z1)
        {
            int a=triangles[triangle]*3,b=triangles[triangle+1]*3,c=triangles[triangle+2]*3;
            x0=Cell(Math.Min(positions[a],Math.Min(positions[b],positions[c]))-1e-5);
            x1=Cell(Math.Max(positions[a],Math.Max(positions[b],positions[c]))+1e-5);
            z0=Cell(Math.Min(positions[a+2],Math.Min(positions[b+2],positions[c+2]))-1e-5);
            z1=Cell(Math.Max(positions[a+2],Math.Max(positions[b+2],positions[c+2]))+1e-5);
        }

        public bool TryHeight(double x,double z,out double height)
        {
            height=double.NegativeInfinity;
            if(x<-.0001||z<-.0001||x>size+.0001||z>size+.0001||double.IsNaN(x)||double.IsNaN(z))return false;
            int cell=Cell(x)+Cell(z)*segments;
            for(int at=offsets[cell];at<offsets[cell+1];at++)
            {
                int i=entries[at],a=triangles[i]*3,b=triangles[i+1]*3,c=triangles[i+2]*3;
                double ax=positions[a],az=positions[a+2],bx=positions[b]-ax,bz=positions[b+2]-az,cx=positions[c]-ax,cz=positions[c+2]-az;
                double determinant=bx*cz-bz*cx;if(Math.Abs(determinant)<1e-12)continue;
                double px=x-ax,pz=z-az,u=(px*cz-pz*cx)/determinant,v=(bx*pz-bz*px)/determinant;
                if(u< -1e-7||v< -1e-7||u+v>1.0000001)continue;
                double y=positions[a+1]+u*(positions[b+1]-positions[a+1])+v*(positions[c+1]-positions[a+1]);
                height=Math.Max(height,y);
            }
            return !double.IsNegativeInfinity(height);
        }
    }
}

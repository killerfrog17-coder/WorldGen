using System;

namespace WorldGen.Presentation
{
    /// <summary>Exact changed grid cells between two equal-resolution height
    /// surfaces. A changed vertex marks every incident triangle's cell.</summary>
    public sealed class TerrainHeightChange
    {
        public int Segments {get;}
        readonly ulong[] cells;
        int minX,minZ,maxX,maxZ;
        TerrainHeightChange(int n){Segments=n;cells=new ulong[(n*n+63)/64];minX=minZ=n;maxX=maxZ=-1;}
        void Mark(int x,int z)
        {
            int n=Segments;if(x<0||z<0||x>=n||z>=n)return;
            int at=z*n+x;cells[at>>6]|=1UL<<(at&63);
            minX=Math.Min(minX,x);minZ=Math.Min(minZ,z);maxX=Math.Max(maxX,x);maxZ=Math.Max(maxZ,z);
        }
        public static TerrainHeightChange? Compare(float[] before,float[] after,int segments)
        {
            if(segments<1||segments>128||before.Length!=(segments+1)*(segments+1)||after.Length!=before.Length)throw new ArgumentException("Incompatible terrain height grids");
            var change=new TerrainHeightChange(segments);int side=segments+1;
            for(int z=0;z<=segments;z++)for(int x=0;x<=segments;x++)if(before[z*side+x]!=after[z*side+x])
            {change.Mark(x-1,z-1);change.Mark(x,z-1);change.Mark(x-1,z);change.Mark(x,z);}
            return change.maxX<0?null:change;
        }
        public void Union(TerrainHeightChange other)
        {
            if(other.Segments!=Segments)throw new ArgumentException("Incompatible change grids");
            for(int i=0;i<cells.Length;i++)cells[i]|=other.cells[i];
            minX=Math.Min(minX,other.minX);minZ=Math.Min(minZ,other.minZ);maxX=Math.Max(maxX,other.maxX);maxZ=Math.Max(maxZ,other.maxZ);
        }
        public bool Affects(double x,double z,double margin=0)
        {
            int x0=Math.Max(0,(int)Math.Floor(x-margin)),x1=Math.Min(Segments-1,(int)Math.Floor(x+margin));
            int z0=Math.Max(0,(int)Math.Floor(z-margin)),z1=Math.Min(Segments-1,(int)Math.Floor(z+margin));
            // The outermost grid edge belongs to its final triangle too.
            if(x-margin==Segments)x0=Segments-1;if(z-margin==Segments)z0=Segments-1;
            if(x0>maxX||x1<minX||z0>maxZ||z1<minZ)return false;
            x0=Math.Max(x0,minX);x1=Math.Min(x1,maxX);z0=Math.Max(z0,minZ);z1=Math.Min(z1,maxZ);
            for(int row=z0;row<=z1;row++)
            {
                int first=row*Segments+x0,last=row*Segments+x1;
                for(int word=first>>6;word<=(last>>6);word++)
                {
                    int low=word==(first>>6)?first&63:0,high=word==(last>>6)?last&63:63;
                    ulong mask=(ulong.MaxValue<<low)&(ulong.MaxValue>>(63-high));
                    if((cells[word]&mask)!=0)return true;
                }
            }
            return false;
        }
    }
}

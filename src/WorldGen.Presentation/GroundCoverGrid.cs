using System;

namespace WorldGen.Presentation
{
    /// <summary>Conservative drawn-water exclusion, including streams narrower
    /// than one terrain sample. No plant roots are fitted to guessed water levels.</summary>
    public static class GroundCoverGrid
    {
        public static void ExcludeWater(LocalTerrainMesh mesh)
        {
            if(mesh.GroundCover.Length==0||mesh.WaterTriangles.Length==0)return;
            int side=mesh.Segments+1;double step=mesh.SizeM/mesh.Segments;
            for(int t=0;t<mesh.WaterTriangles.Length;t+=3)
            {
                int ia=mesh.WaterTriangles[t]*3,ib=mesh.WaterTriangles[t+1]*3,ic=mesh.WaterTriangles[t+2]*3;
                double ax=mesh.WaterPositions[ia],az=mesh.WaterPositions[ia+2],bx=mesh.WaterPositions[ib],bz=mesh.WaterPositions[ib+2],cx=mesh.WaterPositions[ic],cz=mesh.WaterPositions[ic+2];
                if(Math.Abs((bx-ax)*(cz-az)-(bz-az)*(cx-ax))<1e-8)continue;
                int x0=Math.Max(0,(int)Math.Floor(Math.Min(ax,Math.Min(bx,cx))/step)),x1=Math.Min(mesh.Segments-1,(int)Math.Floor(Math.Max(ax,Math.Max(bx,cx))/step));
                int z0=Math.Max(0,(int)Math.Floor(Math.Min(az,Math.Min(bz,cz))/step)),z1=Math.Min(mesh.Segments-1,(int)Math.Floor(Math.Max(az,Math.Max(bz,cz))/step));
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    double mx=(x+.5)*step,mz=(z+.5)*step;
                    bool Separate(double ux,double uz)
                    {
                        double a=ax*ux+az*uz,b=bx*ux+bz*uz,c=cx*ux+cz*uz;
                        double center=mx*ux+mz*uz,radius=(Math.Abs(ux)+Math.Abs(uz))*step*.5;
                        return Math.Min(a,Math.Min(b,c))>center+radius+1e-7||Math.Max(a,Math.Max(b,c))<center-radius-1e-7;
                    }
                    if(Separate(az-bz,bx-ax)||Separate(bz-cz,cx-bx)||Separate(cz-az,ax-cx))continue;
                    for(int dz=0;dz<2;dz++)for(int dx=0;dx<2;dx++)
                    {int i=(z+dz)*side+x+dx;mesh.GroundCover[i*16+3]=0;for(int f=0;f<9;f++)mesh.GroundCover[i*16+4+f]=0;}
                }
            }
        }
    }
}

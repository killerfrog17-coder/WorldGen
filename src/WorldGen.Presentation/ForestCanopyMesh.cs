using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Bounded statistical crown clusters, separate triangles ABOVE the
    /// existing ground, never altered terrain or collision heights. Same broad
    /// geographic cover field as trees; not an exact census of distant individuals.</summary>
    public static class ForestCanopyMesh
    {
        public static void Append(LocalTerrainMesh mesh,LocalTerrainFrame frame,int seed,CancellationToken token)
        {
            if(mesh.ForestCover.Length==0)return;
            bool any=false;for(int i=3;i<mesh.ForestCover.Length;i+=8)if(mesh.ForestCover[i]>.025f){any=true;break;}if(!any)return;
            int side=mesh.Segments+1;double step=mesh.SizeM/mesh.Segments;
            // No per-blade/world-wide census: bounded overlay work on existing tiles.
            int stride=1;while(step*stride<64)stride*=2;
            while(mesh.Segments/stride>8)stride*=2;
            if(stride>mesh.Segments)return;
            var positions=new List<float>(mesh.Positions);var normals=new List<float>(mesh.Normals);var colors=new List<float>(mesh.Colors);
            var indices=new List<int>(mesh.Triangles);int initial=positions.Count;
            double Value(double x,double z,int field)
            {
                double gx=D.Clamp(x/step,0,mesh.Segments-.000001),gz=D.Clamp(z/step,0,mesh.Segments-.000001);
                int a=(int)gx,b=(int)gz;double u=gx-a,v=gz-b;
                double At(int xx,int zz)=>field<0?mesh.Positions[(zz*side+xx)*3+1]:mesh.ForestCover[(zz*side+xx)*8+field];
                return At(a,b)*(1-u)*(1-v)+At(a+1,b)*u*(1-v)+At(a,b+1)*(1-u)*v+At(a+1,b+1)*u*v;
            }
            for(int z=0;z<mesh.Segments;z+=stride)for(int x=0;x<mesh.Segments;x+=stride)
            {
                token.ThrowIfCancellationRequested();double cell=step*stride;
                double cx=(x+stride*.5)*step,cz=(z+stride*.5)*step;
                V point=frame.Position(mesh.EastM+cx,mesh.NorthM+cz);
                // Stable geographic seed; no camera/frustum or completion-order RNG.
                int hash=unchecked((int)Rng.Mix((uint)(int)(point.X*10000000)^(uint)(int)(point.Y*10000000)*19349663u^(uint)(int)(point.Z*10000000)*83492791u));
                var r=new Rng(seed,"forest-crown-proxy-1",hash);
                double cover=Value(cx,cz,3);if(cover<.025||r.Unit()>Math.Min(1,cover*1.7))continue;
                double height=Value(cx,cz,4),width=Value(cx,cz,5);if(height<1)continue;
                int crowns=cell>256?2:3;
                for(int crown=0;crown<crowns;crown++)
                {
                    double px=cx+(r.Unit()-.5)*cell*.65,pz=cz+(r.Unit()-.5)*cell*.65;
                    double local=Value(px,pz,3);if(local<.02)continue;
                    double radius=Math.Max(width*.32,cell*Math.Sqrt(local)*.24),top=height*(.78+.24*r.Unit());
                    V colour=new V(Value(px,pz,0),Value(px,pz,1),Value(px,pz,2))*(.92+.13*r.Unit());
                    const int sides=7,rings=3;int first=positions.Count/3;
                    for(int ring=0;ring<=rings;ring++)for(int j=0;j<=sides;j++)
                    {
                        double theta=ring*Math.PI/rings,az=j*Math.PI*2/sides;
                        double rr=Math.Sin(theta)*radius*(1+.08*Math.Sin(az*3+crown));
                        double xx=px+Math.Cos(az)*rr,zz=pz+Math.Sin(az)*rr;
                        // Crown bases follow drawn ground without modifying its grid.
                        double yy=Value(xx,zz,-1)+top*(.62+.38*Math.Cos(theta));
                        positions.Add((float)xx);positions.Add((float)yy);positions.Add((float)zz);
                        V n=new V(Math.Sin(theta)*Math.Cos(az),Math.Cos(theta)*radius/Math.Max(1,top*.38),Math.Sin(theta)*Math.Sin(az)).Unit;
                        normals.Add((float)n.X);normals.Add((float)n.Y);normals.Add((float)n.Z);
                        colors.Add((float)colour.X);colors.Add((float)colour.Y);colors.Add((float)colour.Z);colors.Add(-3);
                    }
                    for(int ring=0;ring<rings;ring++)for(int j=0;j<sides;j++)
                    {int at=first+ring*(sides+1)+j;indices.Add(at);indices.Add(at+1);indices.Add(at+sides+1);indices.Add(at+1);indices.Add(at+sides+2);indices.Add(at+sides+1);}
                }
            }
            if(positions.Count==initial)return;
            mesh.Positions=positions.ToArray();mesh.Normals=normals.ToArray();mesh.Colors=colors.ToArray();mesh.Triangles=indices.ToArray();
            int vertices=mesh.Positions.Length/3;Array.Resize(ref mesh.SurfaceWeights,vertices*4);Array.Resize(ref mesh.GroundWeightsB,vertices*4);Array.Resize(ref mesh.Lithology,vertices*6);
        }
    }
}

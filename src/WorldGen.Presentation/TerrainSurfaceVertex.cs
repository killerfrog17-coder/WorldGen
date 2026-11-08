using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // A complete surface sample, not another height function. Fine boundaries
    // interpolate the same float attributes that the coarse triangles submit.
    internal struct TerrainSurfaceVertex
    {
        public V Normal,Color,CliffOffset;
        public GroundSurface Material;
        public GroundProfile Ground;
        public GroundCoverProfile Cover;
        public MidCoverProfile Mid;
        public ForestCoverProfile Forest;
        public double Coverage,MidAmount,Height;

        public static TerrainSurfaceVertex Evaluate(LocalTerrain terrain,LocalTerrainFrame frame,V point,LocalTerrainSample sample,double nx,double nz)
        {
            double length=Math.Sqrt(nx*nx+1+nz*nz),up=1/length;
            double snow=sample.Biome==Biome.Ice?D.Clamp((up-.55)/.4):0;
            var material=LocalGeology.Surface(sample.Ground,point,terrain.SourceSeed,sample.ElevationM,up,sample.OceanInfluence,snow,
                terrain.RadiusM*frame.HorizontalScale,sample.Province.ExposedRock*(1-D.Clamp(sample.RiverSand+sample.RiverGravel)));
            double canopy=sample.Underwater?0:TreeFlora.CanopyShade(sample.Forest,point,terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale)*TreeFlora.SlopeAdmission(up);
            var cover=GroundFlora.Surface(sample.Cover,point,terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale,sample.Ground,
                sample.ElevationM,sample.BaseElevationM,up,sample.Underwater,snow,sample.Biome,canopy);
            double coverage=D.Clamp(cover.Coverage),visible=D.Clamp(coverage/.45);
            var mid=sample.MidCover;
            double midAmount=sample.Underwater||sample.ElevationM<2?0:mid.Coverage*MidFlora.StandDensity(point,terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale)*D.Clamp((up-.65)/.25);
            var biome=TreeFlora.TidalCode(sample.Forest.MorphologyCode)?terrain.Profile(sample.Cell).Biome:sample.Biome;
            var forest=TreeFlora.Surface(sample.Forest,point,terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale,sample.ElevationM,up,
                sample.Underwater,sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,biome);
            V offset=default,normal=new V(nx/length,up,nz/length);
            double wall=OrogenMorphology.Smooth(.18,.48,1-up)*Math.Max(material.Rock,sample.Province.ExposedRock);
            if(!sample.Underwater&&sample.WaterKind!=LocalWaterKind.River&&sample.ElevationM>8&&wall>.001)
            {
                double clearance=double.IsNaN(sample.WaterSurfaceM)?1:OrogenMorphology.Smooth(8,20,sample.ElevationM-sample.WaterSurfaceM);
                double hardness=1-.65*sample.Ground.RockWeight((int)RockFamily.Mudstone);
                double amount=CliffSurfaceDetail.Outward(point*(terrain.RadiusM*frame.HorizontalScale+sample.ElevationM),terrain.SourceSeed,hardness,out V gradient)*wall*clearance;
                double horizontal=Math.Sqrt(nx*nx+nz*nz);
                if(horizontal>.001)
                {
                    var direction=new V(nx/horizontal,0,nz/horizontal);offset=direction*amount;
                    double gx=V.Dot(gradient,frame.East-frame.Origin*nx)*wall*clearance;
                    double gz=V.Dot(gradient,frame.North-frame.Origin*nz)*wall*clearance;
                    V tangentX=new V(1,-nx,0)+direction*gx,tangentZ=new V(0,-nz,1)+direction*gz;
                    normal=V.Cross(tangentZ,tangentX).Unit;
                }
            }
            return new TerrainSurfaceVertex{Normal=normal,CliffOffset=offset,Color=material.Color*(1-visible)+cover.Color*(.90*visible),
                Material=material,Ground=sample.Ground,Cover=cover,Mid=mid,Forest=forest,Coverage=coverage,MidAmount=midAmount,Height=frame.SurfaceY(point,sample.ElevationM)};
        }
        static float Mix(double a,double b,double t)=>(float)((float)a*(1-t)+(float)b*t);
        static void Vector(float[] target,int at,V a,V b,double t)
        {target[at]=Mix(a.X,b.X,t);target[at+1]=Mix(a.Y,b.Y,t);target[at+2]=Mix(a.Z,b.Z,t);}
        public static void Write(LocalTerrainMesh mesh,int i,TerrainSurfaceVertex a,TerrainSurfaceVertex b,double t)
        {
            int side=mesh.Segments+1;double step=mesh.SizeM/mesh.Segments;
            mesh.Positions[i*3]=(float)((i%side)*step+Mix(a.CliffOffset.X,b.CliffOffset.X,t));
            mesh.Positions[i*3+2]=(float)((i/side)*step+Mix(a.CliffOffset.Z,b.CliffOffset.Z,t));
            Vector(mesh.Normals,i*3,a.Normal,b.Normal,t);Vector(mesh.Colors,i*4,a.Color,b.Color,t);mesh.Colors[i*4+3]=Mix(a.Coverage,b.Coverage,t);
            for(int k=0;k<4;k++){mesh.SurfaceWeights[i*4+k]=Mix(a.Material.Weight(k),b.Material.Weight(k),t);mesh.GroundWeightsB[i*4+k]=Mix(a.Material.Weight(k+4),b.Material.Weight(k+4),t);}
            for(int k=0;k<6;k++)mesh.Lithology[i*6+k]=Mix(a.Ground.RockWeight(k),b.Ground.RockWeight(k),t);
            Vector(mesh.GroundCover,i*16,a.Cover.Color,b.Cover.Color,t);mesh.GroundCover[i*16+3]=Mix(a.Coverage,b.Coverage,t);
            for(int k=0;k<9;k++)mesh.GroundCover[i*16+4+k]=Mix(a.Cover.Weight(k),b.Cover.Weight(k),t);
            mesh.GroundCover[i*16+13]=Mix(a.Cover.Stature,b.Cover.Stature,t);mesh.GroundCover[i*16+14]=Mix(a.Cover.LeafWidth,b.Cover.LeafWidth,t);
            mesh.GroundCover[i*16+15]=mesh.Positions[i*3+1];
            Vector(mesh.MidCover,i*6,a.Mid.Color,b.Mid.Color,t);mesh.MidCover[i*6+3]=Mix(a.MidAmount,b.MidAmount,t);
            mesh.MidCover[i*6+4]=Mix(a.Mid.HeightM,b.Mid.HeightM,t);mesh.MidCover[i*6+5]=Mix(a.Mid.WidthM,b.Mid.WidthM,t);
            Vector(mesh.ForestCover,i*8,a.Forest.Color,b.Forest.Color,t);mesh.ForestCover[i*8+3]=Mix(a.Forest.Coverage,b.Forest.Coverage,t);
            mesh.ForestCover[i*8+4]=Mix(a.Forest.HeightM,b.Forest.HeightM,t);mesh.ForestCover[i*8+5]=Mix(a.Forest.WidthM,b.Forest.WidthM,t);
            mesh.ForestCover[i*8+6]=Mix(a.Forest.Openness,b.Forest.Openness,t);
            mesh.ForestCover[i*8+7]=BitConverter.Int32BitsToSingle((t<.5?a.Forest:b.Forest).MorphologyCode);
        }
    }
}

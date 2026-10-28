using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public enum ScatterKind { Broadleaf,Conifer,SavannaTree,Shrub,Boulder,Grass,Stone,Outcrop,Herb,Fungus,MidPlant,GeneratedTree,Branch }
    /// <summary>Global cube-sphere address, never an exploration chart/chunk-relative seed.</summary>
    public struct ScatterKey : IEquatable<ScatterKey>,IComparable<ScatterKey>
    {
        public int Face,X,Y;public bool Detail;
        public ScatterKey(int face,int x,int y,bool detail=false){Face=face;X=x;Y=y;Detail=detail;}
        public bool Equals(ScatterKey b)=>Face==b.Face&&X==b.X&&Y==b.Y&&Detail==b.Detail;
        public override bool Equals(object o)=>o is ScatterKey k&&Equals(k);
        public override int GetHashCode()=>unchecked(Face*83492791^X*73856093^Y*19349663^(Detail?int.MinValue:0));
        public int CompareTo(ScatterKey b){int c=Detail.CompareTo(b.Detail);if(c==0)c=Face.CompareTo(b.Face);if(c==0)c=X.CompareTo(b.X);return c==0?Y.CompareTo(b.Y):c;}
        public ulong Id(int channel,int slot)=>((ulong)Face<<61)|((Detail?1UL:0UL)<<60)|((ulong)X<<40)|((ulong)Y<<20)|((ulong)channel<<16)|(uint)slot;
    }
    public struct ScatterInstance
    {
        public ulong Id;public ScatterKind Kind;public int Variant;
        public V Position,Normal,Color;
        public double GroundM,HeightM,WidthM,YawDegrees;
    }
    public sealed class ScatterPatch
    {
        public ScatterKey Key;public V Center;public ScatterInstance[] Instances=Array.Empty<ScatterInstance>();
    }
    /// <summary>Replaceable static natural cover over accepted terrain, not a population or forestry simulation.</summary>
    public sealed partial class LocalScatter
    {
        public const string Version="local-scatter-toolstone-branches-11";
        readonly LocalTerrain terrain;readonly World world;readonly int seed;readonly double scale;
        static readonly V[] Normals={new V(1,0,0),new V(-1,0,0),new V(0,1,0),new V(0,-1,0),new V(0,0,1),new V(0,0,-1)};
        public LocalScatter(LocalTerrain terrain,double horizontalScale=1){this.terrain=terrain??throw new ArgumentNullException(nameof(terrain));if(horizontalScale<.01||horizontalScale>2||double.IsNaN(horizontalScale))throw new ArgumentOutOfRangeException(nameof(horizontalScale));scale=horizontalScale;world=terrain.SourceWorld;seed=world.Recipe.Seed;}
        public int Divisions(bool detail)=>(int)Math.Ceiling(2*terrain.RadiusM*scale/(detail?64:512));
        static void Basis(int face,out V n,out V u,out V v)
        {if(face<0||face>5)throw new ArgumentOutOfRangeException(nameof(face));n=Normals[face];u=face<4?V.Cross(new V(0,0,1),n).Unit:new V(1,0,0);v=V.Cross(n,u).Unit;}
        public V Position(ScatterKey key,double u=.5,double v=.5)
        {
            int count=Divisions(key.Detail);if(count>1048576||key.X<0||key.Y<0||key.X>=count||key.Y>=count)throw new ArgumentOutOfRangeException(nameof(key));
            Basis(key.Face,out V n,out V a,out V b);return (n+a*((key.X+u)*2/count-1)+b*((key.Y+v)*2/count-1)).Unit;
        }
        public ScatterKey Address(V position,bool detail=false)
        {
            int face=0;double best=double.MinValue;for(int i=0;i<6;i++){double d=V.Dot(position,Normals[i]);if(d>best){best=d;face=i;}}
            Basis(face,out V n,out V u,out V v);double d0=V.Dot(position,n);int count=Divisions(detail);
            return new ScatterKey(face,Math.Min(count-1,Math.Max(0,(int)Math.Floor((V.Dot(position,u)/d0+1)*.5*count))),Math.Min(count-1,Math.Max(0,(int)Math.Floor((V.Dot(position,v)/d0+1)*.5*count))),detail);
        }
        public ScatterKey[] Around(V position,double radiusM,bool detail=false)
        {
            if(radiusM<0||radiusM>5000||double.IsNaN(radiusM))throw new ArgumentOutOfRangeException(nameof(radiusM));
            var result=new List<ScatterKey>();double tile=detail?64:512,delta=(radiusM+tile*2)/(terrain.RadiusM*scale);int count=Divisions(detail);
            for(int face=0;face<6;face++)
            {
                Basis(face,out V n,out V u,out V v);double d=V.Dot(position,n);if(d+delta<.5773)continue;
                double a=V.Dot(position,u),b=V.Dot(position,v);
                void Bounds(double x,out int low,out int high)
                {double lo=double.MaxValue,hi=double.MinValue;foreach(double q in new[]{x-delta,x+delta})foreach(double t in new[]{d-delta,d+delta}){lo=Math.Min(lo,q/t);hi=Math.Max(hi,q/t);}low=Math.Max(0,(int)Math.Floor((lo+1)*.5*count));high=Math.Min(count-1,(int)Math.Floor((hi+1)*.5*count));}
                Bounds(a,out int x0,out int x1);Bounds(b,out int y0,out int y1);
                double limit=(radiusM+tile*1.5)/(terrain.RadiusM*scale);limit*=limit;
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var key=new ScatterKey(face,x,y,detail);V p=Position(key),diff=p-position;if(diff.X*diff.X+diff.Y*diff.Y+diff.Z*diff.Z<=limit)result.Add(key);}
            }
            result.Sort();return result.ToArray();
        }
        public ScatterPatch Build(ScatterKey key,CancellationToken token=default)
        {
            V center=Position(key);var patch=terrain.Patch(center,(key.Detail?110:840)/scale);var result=new List<ScatterInstance>();
            double nominal=2*terrain.RadiusM*scale/Divisions(key.Detail),area=nominal*nominal;
            int grid=key.Detail?32:64;
            for(int channel=0;channel<(key.Detail?3:4);channel++)
            {
                // Dense low vegetation now belongs to the shared GPU ground-cover
                // layer; preserve all tree/shrub/rock channels and their IDs.
                if(key.Detail&&channel==0)continue;
                if(!key.Detail&&channel==2)continue; // Replaced by regional generated mid-height flora.
                if(!key.Detail&&channel==0)continue; // Replaced by inherited procedural tree lineages.
                int side=channel==0?grid:key.Detail?(channel==2?4:8):channel==3?4:16;
                for(int y=0;y<side;y++)
                {
                    token.ThrowIfCancellationRequested();
                    for(int x=0;x<side;x++)
                    {
                        int slot=y*side+x;ulong id=key.Id(channel,slot);int hash=unchecked((int)Rng.Mix((uint)id^(uint)(id>>32)));
                        var random=new Rng(seed,"local-scatter-instance-1",hash);
                        double u=(x+.12+.76*random.Unit())/side,v=(y+.12+.76*random.Unit())/side;
                        V p=Position(key,u,v),km=p*(terrain.RadiusM/1000);var sample=patch.Sample(p);
                        if(sample.ElevationM<2||sample.Underwater)continue;
                        var env=world.Ecology!=null&&sample.Cell<world.Ecology.Habitat.Length?world.Ecology.Habitat[sample.Cell]:null;
                        Biome biome=sample.Biome;if(biome==Biome.Freshwater||biome==Biome.SalineLake||biome==Biome.Ocean)continue;
                        double wet=env==null?.7:D.Clamp(env.AridityMilli/1300.0,.03,1.4);
                        double warm=env==null?18:env.WarmTenthsC/10.0-.006*(sample.ElevationM-world.ElevationM[sample.Cell]);
                        double trees=0,cover=.6;
                        switch(biome)
                        {
                            case Biome.TemperateForest:case Biome.TemperateRainforest:trees=.95;cover=.45;break;
                            case Biome.TropicalRainforest:trees=1;cover=.5;break;
                            case Biome.TropicalSeasonalForest:trees=.75;cover=.65;break;
                            case Biome.BorealForest:trees=.8;cover=.5;break;
                            case Biome.MediterraneanWoodland:trees=.28;cover=.4;break;
                            case Biome.Savanna:trees=.09;cover=.8;break;
                            case Biome.Grassland:trees=.012;cover=1;break;
                            case Biome.Floodplain:trees=.12;cover=.9;break;
                            case Biome.Marsh:case Biome.Peatland:case Biome.SaltMarsh:trees=0;cover=.8;break;
                            case Biome.Mangrove:trees=.35;cover=.4;break;
                            case Biome.Desert:trees=0;cover=.025;break;
                            case Biome.Tundra:trees=0;cover=.3;break;
                            case Biome.Alpine:trees=0;cover=.18;break;
                            case Biome.Ice:trees=cover=0;break;
                        }
                        double cluster=D.Clamp(.55+.6*SphereNoise.Sample(km,seed^0x3921,3)+.18*SphereNoise.Sample(km,seed^0x7231,13));
                        trees*=cluster*D.Clamp((warm-6)/6)*D.Clamp(wet/.55);
                        trees*=.15+.85*sample.Ground.RootingSupport(biome==Biome.Mangrove,biome==Biome.Mangrove);
                        if(env!=null){trees*=1-.75*env.PermafrostPermille/1000.0;if(env.SalinityMilliPpt>5000&&biome!=Biome.Mangrove)trees=0;}
                        // Cube projection's area Jacobian keeps density comparable at face centres, edges and corners.
                        double cu=(key.X+u)*2/Divisions(key.Detail)-1,cv=(key.Y+v)*2/Divisions(key.Detail)-1;
                        double jacobian=1/Math.Pow(1+cu*cu+cv*cv,1.5);
                        double chance;ScatterKind kind;
                        var geology=default(LocalRockProfile);
                        bool rockChannel=key.Detail?channel==1:channel==1||channel==3;
                        if(rockChannel)geology=LocalGeology.Describe(sample.Ground,p,seed,terrain.RadiusM*scale);
                        if(key.Detail&&channel==2){kind=ScatterKind.Branch;chance=trees*.65*jacobian;}
                        else if(key.Detail){kind=channel==0?ScatterKind.Grass:ScatterKind.Stone;chance=channel==0?cover*(.35+.65*cluster)*D.Clamp(wet/.35)*area*jacobian/(side*side)*.2:Math.Max(geology.LooseRock*3,biome!=Biome.Ice&&sample.Ground.Deposit!=ParentDeposit.Windblown&&sample.Ground.Deposit!=ParentDeposit.Alluvium&&sample.Ground.Gravel>.02?.12:0)*jacobian;}
                        else if(channel==0){kind=biome==Biome.BorealForest||warm<17?ScatterKind.Conifer:biome==Biome.Savanna?ScatterKind.SavannaTree:ScatterKind.Broadleaf;chance=trees*area*jacobian/(side*side)*.009;}
                        else if(channel==1){kind=ScatterKind.Boulder;chance=geology.LooseRock*.9*jacobian;}
                        else if(channel==3){kind=ScatterKind.Outcrop;chance=biome==Biome.Ice?0:geology.Outcrop*.28*jacobian;}
                        else{kind=ScatterKind.Shrub;chance=cover*.8*jacobian*D.Clamp((warm-2)/10);}
                        if(random.Unit()>D.Clamp(chance))continue;
                        // Local slope, not absolute altitude, determines whether a plant can root here.
                        V east=V.Cross(Math.Abs(p.Z)<.99?new V(0,0,1):new V(0,1,0),p).Unit,north=V.Cross(p,east).Unit;
                        double e=1.5/(terrain.RadiusM*scale);
                        double sx=(patch.Sample((p+east*e).Unit).ElevationM-patch.Sample((p-east*e).Unit).ElevationM)/3;
                        double sz=(patch.Sample((p+north*e).Unit).ElevationM-patch.Sample((p-north*e).Unit).ElevationM)/3;
                        double slope=Math.Sqrt(sx*sx+sz*sz);
                        bool tree=kind==ScatterKind.Broadleaf||kind==ScatterKind.Conifer||kind==ScatterKind.SavannaTree;
                        if((tree&&slope>.65)||(kind==ScatterKind.Grass&&slope>.9)||(kind==ScatterKind.Shrub&&slope>.8)||(!tree&&kind!=ScatterKind.Grass&&kind!=ScatterKind.Shrub&&slope>1.6))continue;
                        if(kind==ScatterKind.Branch)
                        {
                            if(slope>.8)continue;
                            double length=.35+1.95*random.Unit(),diameter=.014+.035*random.Unit();int form=(int)(random.Unit()*8);
                            result.Add(new ScatterInstance{Id=id,Kind=kind,Variant=form,Position=p,Normal=(p-east*sx-north*sz).Unit,GroundM=sample.ElevationM,HeightM=D.Q(diameter,1000),WidthM=D.Q(length,1000),YawDegrees=random.Unit()*360,Color=new V(1,1,1)});continue;
                        }
                        double size=random.Unit(),width=random.Unit(),height;
                        if(tree){double adult=kind==ScatterKind.SavannaTree?13:kind==ScatterKind.Conifer?25:biome==Biome.TropicalRainforest?35:28;height=(.4+.6*size)*adult*D.Clamp((warm-5)/12,.3,1);}
                        else height=kind==ScatterKind.Shrub?.35+1.3*size:kind==ScatterKind.Outcrop?4+22*size*size*D.Clamp(geology.JointSpacingM/7,.6,1.4):kind==ScatterKind.Boulder?.35+3.8*size*size*D.Clamp(geology.JointSpacingM/6,.6,1.3):kind==ScatterKind.Stone?.035+.16*size:.22+.48*size;
                        bool stone=kind==ScatterKind.Boulder||kind==ScatterKind.Stone||kind==ScatterKind.Outcrop;
                        V color=stone?sample.Ground.RockColor:new V(.73+.2*width,.78+.15*size,.65+.16*width);
                        if(!stone&&(biome==Biome.Savanna||biome==Biome.Desert))color=new V(.98,.85,.54);
                        if(!stone&&(biome==Biome.Tundra||biome==Biome.Alpine))color=new V(.68,.67,.48);
                        double spread=kind==ScatterKind.Conifer?.32:kind==ScatterKind.Broadleaf?.6:kind==ScatterKind.SavannaTree?1.05:kind==ScatterKind.Shrub?1.5:kind==ScatterKind.Grass?.85:kind==ScatterKind.Outcrop?2+.8*geology.Roundness:1.1+.8*geology.Roundness;
                        int modelVariant=(stone?(int)sample.Ground.Bedrock*3:0)+(int)(random.Unit()*3);
                        if(kind==ScatterKind.Stone){modelVariant=ToolStone.Encode(ToolStone.Source(sample.Ground,p,seed,terrain.RadiusM*scale),modelVariant%3);color=ToolStone.Color(modelVariant,color);}
                        result.Add(new ScatterInstance{Id=id,Kind=kind,Variant=modelVariant,Position=p,Normal=(p-east*sx-north*sz).Unit,
                            GroundM=sample.ElevationM,HeightM=D.Q(height,1000),WidthM=D.Q(height*spread*(.8+.4*width),1000),YawDegrees=D.Q(random.Unit()*360,1000),Color=color});
                    }
                }
            }
            if(key.Detail)AddGroundSpecialists(key,patch,result,token);
            else {AddMidPlants(key,patch,result,token);AddGeneratedTrees(key,patch,result,token);}
            return new ScatterPatch{Key=key,Center=center,Instances=result.ToArray()};
        }
    }
}

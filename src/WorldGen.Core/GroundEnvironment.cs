using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    public enum RockFamily { Granite, Basalt, Sandstone, Limestone, Mudstone, Metamorphic }
    public enum ParentDeposit { Residual, Alluvium, Windblown, Glacial,Loess }
    public enum GroundMaterial { Soil, Sand, Gravel, Clay, Rock, Organic, Salt, Ice }

    /// <summary>Independent physical descriptors, not biome synonyms or measured soil survey data.
    /// Fractions are0..1, depth is metres, available water is millimetres per root zone.</summary>
    public struct GroundProfile
    {
        public double Sand,Silt,Clay,Gravel,DepthM,Organic,PH,Fertility,Drainage,WaterCapacityMm,Iron,Salinity,Weathering;
        public V SoilColor,RockColor,SandColor;
        public RockFamily Bedrock;public ParentDeposit Deposit;
        public double RootSupport=>RootingSupport();
        public double RootingSupport(bool saltTolerant=false,bool waterloggedTolerant=false)=>D.Clamp(DepthM/.6)*D.Clamp(WaterCapacityMm/80)*(.3+.7*Fertility)*(saltTolerant?1:1-Salinity)*(waterloggedTolerant?1:.4+.6*D.Clamp(Drainage/.45));
        public string TextureName=>Organic>.2?"Organic peat":DepthM<.12?"Thin rocky soil":Salinity>.4?"Saline soil":(Gravel>.35?"Gravelly ":"")+(Sand>.85?"sand":Sand>.7?"loamy sand":Clay>.4?"clay":Silt>.65?"silt loam":Clay>.27?"clay loam":Sand>.52?"sandy loam":"loam");
        public double FarmFit(int crop,bool managed=false)
        {
            double root=D.Clamp(DepthM/(crop==4?1:.45)),stones=1-.85*Gravel;
            double ph=D.Clamp(1-Math.Max(0,Math.Abs(PH-(crop==2?6:6.6))-1.1)/3);
            double drainage=crop==2?.7+.3*(1-Drainage):.25+.75*D.Clamp(Drainage/.45);
            double water=.25+.75*D.Clamp(WaterCapacityMm/(crop==4?110:75));
            double nutrients=.2+.8*Fertility;if(managed)nutrients=.35+.65*Fertility;
            return D.Q(D.Clamp(root*stones*ph*drainage*water*nutrients*(1-.95*Salinity)),1000000);
        }
        internal void Add(GroundProfile p,double w)
        {
            Sand+=p.Sand*w;Silt+=p.Silt*w;Clay+=p.Clay*w;Gravel+=p.Gravel*w;DepthM+=p.DepthM*w;Organic+=p.Organic*w;
            PH+=p.PH*w;Fertility+=p.Fertility*w;Drainage+=p.Drainage*w;WaterCapacityMm+=p.WaterCapacityMm*w;
            Iron+=p.Iron*w;Salinity+=p.Salinity*w;Weathering+=p.Weathering*w;
            SoilColor+=p.SoilColor*w;RockColor+=p.RockColor*w;SandColor+=p.SandColor*w;
        }
        internal void Divide(double w)
        {
            Sand/=w;Silt/=w;Clay/=w;Gravel/=w;DepthM/=w;Organic/=w;PH/=w;Fertility/=w;Drainage/=w;WaterCapacityMm/=w;
            Iron/=w;Salinity/=w;Weathering/=w;SoilColor=SoilColor/w;RockColor=RockColor/w;SandColor=SandColor/w;
        }
    }
    public struct GroundSurface
    {
        public GroundProfile Profile;
        public double Soil,Sand,Gravel,Clay,Rock,Organic,Salt,Ice,DunePotential;
        public V Color;
        public double Weight(int i)=>i==0?Soil:i==1?Sand:i==2?Gravel:i==3?Clay:i==4?Rock:i==5?Organic:i==6?Salt:Ice;
        public GroundMaterial Material
        {get{int best=0;for(int i=1;i<8;i++)if(Weight(i)>Weight(best))best=i;return (GroundMaterial)best;}}
    }

    /// <summary>Seeded ground model shared by local terrain and prospective historical agronomy.
    /// Pure per-cell descriptions have a bounded thread-safe memo: no recurring cell/member census.
    /// Earth relief supplies landforms, NOT measured lithology; an eventual geology input can replace
    /// Describe without changing the soil/surface/sound contracts.</summary>
    public sealed class GroundEnvironment
    {
        public const string Version="ground-environment-1";
        static readonly ConditionalWeakTable<World,GroundEnvironment> contexts=new ConditionalWeakTable<World,GroundEnvironment>();
        readonly World world;readonly ConcurrentDictionary<int,GroundProfile> profiles=new ConcurrentDictionary<int,GroundProfile>();
        readonly Func<int,GroundProfile> describe;
        GroundEnvironment(World world){this.world=world;describe=Describe;}
        public static GroundEnvironment For(World world)=>contexts.GetValue(world,w=>new GroundEnvironment(w));
        public GroundProfile Cell(int cell)
        {
            if(cell<0||cell>=world.Surface.Cells.Length)throw new ArgumentOutOfRangeException(nameof(cell));
            if(profiles.TryGetValue(cell,out var cached))return cached;
            if(profiles.Count>=65536)return Describe(cell);
            return profiles.GetOrAdd(cell,describe);
        }
        internal GroundProfile TerrainCell(int cell)=>Describe(cell);
        static double Field(int[] values,int cell)=>cell<values.Length?values[cell]/1000.0:0;
        GroundProfile Describe(int cell)
        {
            var p=world.Surface.Cells[cell].Position;int seed=world.Recipe.Seed;
            var h=world.Ecology!=null&&cell<world.Ecology.Habitat.Length?world.Ecology.Habitat[cell]:null;
            double geology=D.Clamp(.5+.85*SphereNoise.Sample(p,seed^0x47b2,7)),facies=.5+.5*SphereNoise.Sample(p,seed^0x7219,23);
            double craton=Field(world.CratonPermille,cell),fold=Field(world.FoldPermille,cell),basin=Field(world.BasinPermille,cell);
            RockFamily rock=geology<.25?RockFamily.Sandstone:geology<.43?RockFamily.Limestone:geology<.62?RockFamily.Mudstone:geology<.80?RockFamily.Granite:RockFamily.Metamorphic;
            if(Field(world.MantlePermille,cell)>.7&&facies>.52)rock=RockFamily.Basalt;
            else if(craton>.65&&geology>.45)rock=RockFamily.Granite;
            else if(fold>.6&&geology>.65)rock=RockFamily.Metamorphic;
            double warm=h==null?15:h.MeanTenthsC/10.0,moist=h==null?.7:h.AridityMilli/1000.0;
            double dry=D.Clamp((.55-moist)/.55),wet=h==null?0:D.Clamp((100-h.WaterTableCm)/100.0);
            double relief=h==null?0:h.ReliefM,flood=h==null?0:h.FloodFractionPermille/1000.0;
            double frozen=h==null?0:h.PermafrostPermille/1000.0;
            bool glacial=cell<world.Glaciated.Length&&world.Glaciated[cell];
            ParentDeposit deposit=flood>.12?ParentDeposit.Alluvium:glacial?ParentDeposit.Glacial:dry>.5&&basin>.3&&facies>.6?ParentDeposit.Windblown:ParentDeposit.Residual;
            if(deposit==ParentDeposit.Residual&&warm<18&&moist>.3&&moist<1.2&&relief<400&&facies<.4)
                foreach(int neighbor in world.Surface.Cells[cell].Neighbors)
                    if(neighbor<world.Glaciated.Length&&world.Glaciated[neighbor]||world.Ecology!=null&&neighbor<world.Ecology.Habitat.Length&&world.Ecology.Habitat[neighbor].AridityMilli<300){deposit=ParentDeposit.Loess;break;}
            double sand=rock==RockFamily.Sandstone?.72:rock==RockFamily.Granite?.58:rock==RockFamily.Mudstone?.20:.35;
            double clay=rock==RockFamily.Mudstone?.46:rock==RockFamily.Limestone?.36:.18;
            double weathering=D.Clamp((warm+5)/32)*D.Clamp((moist-.35)/1.1)*(.45+.55*facies)*(1-.65*flood)*(1-.5*fold);
            if(rock==RockFamily.Basalt&&Field(world.MantlePermille,cell)>.7)weathering*=.25; // Fresh volcanic parent, not a blanket tropical penalty.
            sand*=1-.4*weathering;clay=Math.Min(.65,clay+.22*weathering);
            double gravel=D.Clamp(.06+.22*facies+.22*D.Clamp(relief/1400)+.23*dry-.2*flood);
            if(deposit==ParentDeposit.Alluvium){sand=.22+.28*D.Clamp(relief/500);clay=.16;gravel=.04+.2*D.Clamp(relief/1000);}
            if(deposit==ParentDeposit.Glacial){gravel=Math.Max(.35,gravel);sand=.50;clay=.18;}
            if(deposit==ParentDeposit.Windblown){sand=.92;clay=.04;gravel=.025;}
            if(deposit==ParentDeposit.Loess){sand=.20;clay=.16;gravel=.04;}
            double depth=Math.Max(.04,(.35+1.2*facies+.8*basin+1.1*flood)*(1-.8*D.Clamp(relief/1800)));
            if(deposit==ParentDeposit.Loess)depth=Math.Max(1.3,depth);
            depth*=1-.7*frozen;
            double organic=D.Clamp((.025+.10*wet+.045*D.Clamp((12-warm)/20))*(1-.9*dry)*(1-.5*D.Clamp(warm/30)));
            if(h!=null&&(h.Biome==Biome.Grassland||h.Biome==Biome.Savanna))organic+=.035*(1-dry);
            if(h!=null&&h.Biome==Biome.Peatland){organic=.45;depth=Math.Max(1,depth);}
            double salt=h==null||h.Biome==Biome.Ocean?0:D.Clamp(h.SalinityMilliPpt/5000.0);
            double ph=Math.Max(4,Math.Min(9,6.5+(rock==RockFamily.Limestone?1.1:rock==RockFamily.Basalt?.4:0)+.8*dry-1.8*weathering-1.3*organic));
            double fertile=D.Clamp((rock==RockFamily.Basalt?.85:rock==RockFamily.Granite?.48:.65)*(1-.65*weathering)+.20*flood+organic);
            if(deposit==ParentDeposit.Loess)fertile=Math.Max(.72,fertile);
            fertile*=1-.65*D.Clamp((organic-.1)/.4); // Peat carbon is not automatically accessible mineral nutrition.
            double drainage=D.Clamp(.3+.65*sand-.35*clay-.6*wet+.12*D.Clamp(relief/500));
            double awc=Math.Max(.035,Math.Min(.28,.05+.24*(1-sand-clay)+.15*clay+.35*organic))*(1-gravel)*Math.Min(1.5,depth)*1000;
            double iron=D.Clamp((rock==RockFamily.Basalt?.7:rock==RockFamily.Sandstone?.5:.25)*(.4+.6*facies)+weathering*.45)*(1-.85*wet);
            V rockColor=rock==RockFamily.Basalt?new V(.21,.22,.22):rock==RockFamily.Granite?new V(.51,.50,.47):rock==RockFamily.Limestone?new V(.65,.62,.53):rock==RockFamily.Sandstone?new V(.62,.40,.26):rock==RockFamily.Mudstone?new V(.40,.37,.32):new V(.36,.39,.40);
            V soil=new V(.44+.16*iron,.35-.07*iron,.25-.08*iron)*(1-.85*Math.Min(.7,organic*4));
            V sandColor=rock==RockFamily.Basalt?new V(.29,.28,.25):new V(.65+.06*iron,.58-.19*iron,.43-.20*iron);
            return new GroundProfile{Bedrock=rock,Deposit=deposit,Sand=D.Q(sand,10000),Clay=D.Q(clay,10000),Silt=D.Q(1-sand-clay,10000),Gravel=D.Q(gravel,10000),DepthM=D.Q(depth,1000),Organic=D.Q(organic,10000),PH=D.Q(ph,100),Fertility=D.Q(fertile,10000),Drainage=D.Q(drainage,10000),WaterCapacityMm=D.Q(awc,100),Iron=D.Q(iron,10000),Salinity=D.Q(salt,10000),Weathering=D.Q(weathering,10000),RockColor=rockColor,SoilColor=soil,SandColor=sandColor};
        }
        public static GroundSurface Surface(GroundProfile p,V position,int seed,double elevation,double up,double ocean,double ice,double horizontalRadiusM)
        {
            // Physical metre-addressed variation; never reseeded by a tile or nearest-cell ID.
            var metres=position*horizontalRadiusM;
            double patch=.5+.5*SphereNoise.Sample(metres,seed^0x5117,.0016),small=.5+.5*SphereNoise.Sample(metres,seed^0x37a1,.028);
            double slope=D.Clamp((1-up-.04)/.45),bare=D.Clamp(slope+(1-D.Clamp(p.DepthM/.25))*.45);
            double beach=D.Clamp((7-Math.Abs(elevation))/6)*D.Clamp(ocean*10)*(1-slope);
            double sand=D.Clamp((p.Sand-.42)/.5)*(.3+.7*patch)*(1-p.Gravel);
            double gravel=D.Clamp(p.Gravel*1.6)*(.5+.5*small),clay=D.Clamp((p.Clay-.25)/.5)*(.4+.6*patch);
            if(beach>0){double stony=D.Clamp((p.Gravel-.15)*3);sand=sand*(1-beach)+beach*(1-stony);gravel=gravel*(1-beach)+beach*stony;clay*=1-beach;}
            double organics=D.Clamp((p.Organic-.04)*5)*(1-beach),salt=elevation>0?D.Clamp(p.Salinity*1.3)*(1-beach):0;
            double soil=Math.Max(.08,1-sand*.8-gravel*.6-clay*.7-organics*.8-salt);
            double total=soil+sand+gravel+clay+organics+salt,solid=(1-bare)*(1-ice);
            var s=new GroundSurface{Profile=p,Soil=soil/total*solid,Sand=sand/total*solid,Gravel=gravel/total*solid,Clay=clay/total*solid,Organic=organics/total*solid,Salt=salt/total*solid,Rock=bare*(1-ice),Ice=ice,DunePotential=D.Clamp(sand*(1-p.Gravel)*(1-p.Organic*3)*(1-slope))};
            s.Color=p.SoilColor*s.Soil+p.SandColor*s.Sand+(p.RockColor*.85)*s.Gravel+(p.SoilColor*.94)*s.Clay+p.RockColor*s.Rock+new V(.18,.14,.10)*s.Organic+new V(.78,.75,.65)*s.Salt+new V(.86,.89,.91)*s.Ice;
            return s;
        }
    }
}

using System;

namespace WorldGen.Core
{
    public enum LandscapeRegion
    {
        VegetatedPlain, Woodland, Forest, Rainforest, DryWoodland, Steppe,
        SandSea, SandSheet, GravelDesert, RockDesert, ClayPan, SaltFlat,
        Badlands, LoessHills, LimestoneUpland, RockyUpland, AlpineScree,
        GlacialLowland, Tundra, FreshwaterMarsh, WoodedSwamp, Peatland, TidalWetland
    }

    // Continuous controls, rather than a hard biome replacement or a collection
    // of nearest-cell switches. Shared refined vertices carry the same mixture.
    public struct LandscapeControls
    {
        public float Sand,Clay,Salt,Badlands,Loess,Karst,Rock,Glacial,Wet,Peat,Moisture,Canopy;
        public static LandscapeControls Blend(LandscapeControls a,LandscapeControls b,double t)
            => Mix(a,b,default,1-t,t,0);
        public static LandscapeControls Mix(LandscapeControls a,LandscapeControls b,LandscapeControls c,double x,double y,double z)
            =>new LandscapeControls{
                Sand=(float)(a.Sand*x+b.Sand*y+c.Sand*z),Clay=(float)(a.Clay*x+b.Clay*y+c.Clay*z),Salt=(float)(a.Salt*x+b.Salt*y+c.Salt*z),
                Badlands=(float)(a.Badlands*x+b.Badlands*y+c.Badlands*z),Loess=(float)(a.Loess*x+b.Loess*y+c.Loess*z),Karst=(float)(a.Karst*x+b.Karst*y+c.Karst*z),
                Rock=(float)(a.Rock*x+b.Rock*y+c.Rock*z),Glacial=(float)(a.Glacial*x+b.Glacial*y+c.Glacial*z),Wet=(float)(a.Wet*x+b.Wet*y+c.Wet*z),
                Peat=(float)(a.Peat*x+b.Peat*y+c.Peat*z),Moisture=(float)(a.Moisture*x+b.Moisture*y+c.Moisture*z),Canopy=(float)(a.Canopy*x+b.Canopy*y+c.Canopy*z)};
        public double Flatness=>Math.Max(Wet*.8,Math.Max(Clay,Salt));
    }

    public static class LandscapeProvince
    {
        public const string Version="landscape-provinces-1";
        static double Smooth(double a,double b,double value)=>OrogenMorphology.Smooth(a,b,value);
        public static LandscapeControls Describe(GroundProfile ground,Biome biome,double moisture,double canopy,double relief,double frozen,double flood)
        {
            bool desert=biome==Biome.Desert,peat=biome==Biome.Peatland;
            bool marsh=biome==Biome.Marsh||biome==Biome.Mangrove||biome==Biome.SaltMarsh||peat;
            double dry=desert?1:1-Smooth(.2,.65,moisture),flat=1-Smooth(150,700,relief);
            double wet=marsh?1:Smooth(.62,1.15,moisture)*(1-ground.Drainage)*Math.Max(flood,flat*.5);
            return new LandscapeControls{
                Sand=(float)(dry*Smooth(.47,.85,ground.Sand)*(1-ground.Gravel)*(1-ground.Organic)),
                Clay=(float)(dry*flat*Smooth(.23,.48,ground.Clay)*(1-flood)),
                Salt=(float)(dry*flat*Smooth(.15,.65,ground.Salinity)),
                Badlands=(float)(dry*ground.RockWeight(4)*Smooth(120,900,relief)*(1-canopy)),
                Loess=ground.Deposit==ParentDeposit.Loess?1:0,
                Karst=(float)(ground.RockWeight(3)*Smooth(130,800,relief)),
                Rock=(float)((ground.RockWeight(0)+ground.RockWeight(1)+ground.RockWeight(5))*Smooth(150,1100,relief)),
                Glacial=(float)D.Clamp(frozen),Wet=(float)D.Clamp(wet),Peat=peat?1:(float)Smooth(.12,.35,ground.Organic),
                Moisture=(float)D.Clamp(moisture/1.2),Canopy=(float)D.Clamp(canopy)};
        }
        public static double SandSea(V metres,int seed,LandscapeControls controls)
        {
            if(controls.Sand<=.001)return 0;
            // Sand availability is necessary; only some sediment provinces
            // accumulate a sand sea. The remainder remains sandy sheets/reg.
            double province=.5+.5*SphereNoise.Sample(metres,seed^0x612b7,1/4300d);
            return controls.Sand*Smooth(.42,.67,province);
        }
        public static double HeightOffset(V metres,int seed,LandscapeControls controls)
        {
            double dunes=SandSea(metres,seed,controls),result=0;
            if(dunes>.001)
            {
                double warp=SphereNoise.Sample(metres,seed^0x5c721,1/820d)*55;
                double Ridge(double phase){double t=phase-Math.Floor(phase),side=t<.78?t/.78:(1-t)/.22;return .5-.5*Math.Cos(Math.PI*side);}
                double first=Ridge((metres.X*.73+metres.Y*.41+metres.Z*.54+warp)/260);
                double second=Ridge((metres.X*.13+metres.Y*.85-metres.Z*.51+warp)/260);
                var direction=metres.Unit;double alignment=direction.X*.73+direction.Y*.41+direction.Z*.54;
                // A fixed 3D wave vector becomes almost radial at two planetary
                // locations, stretching local dunes towards infinity. Blend a
                // second nonparallel family there without blending huge phases.
                double blend=Smooth(.2,.6,1-alignment*alignment),ridge=first*blend+second*(1-blend);
                double secondary=.5+.5*SphereNoise.Sample(metres,seed^0x2b531,1/85d);
                result+=Smooth(.08,.4,dunes)*(22*ridge+2.2*secondary);
            }
            if(controls.Badlands>.01)
            {
                double gullies=1-Math.Abs(SphereNoise.Sample(metres,seed^0x39171,1/120d));
                result-=controls.Badlands*8*gullies*gullies*gullies;
            }
            if(controls.Karst>.01)
            {
                double ribs=Math.Abs(SphereNoise.Sample(metres,seed^0x1d731,1/145d));
                result+=controls.Karst*7*ribs*ribs;
            }
            if(controls.Loess>.01)result+=controls.Loess*2.5*SphereNoise.Sample(metres,seed^0x731b1,1/180d);
            if(controls.Rock>.01)result+=controls.Rock*.65*SphereNoise.Sample(metres,seed^0x17ba3,1/14d);
            if(controls.Wet>.01)result+=controls.Wet*.12*SphereNoise.Sample(metres,seed^0x73419,1/19d);
            return result;
        }
        public static LandscapeRegion Region(LandscapeControls p,V metres,int seed,Biome biome)
        {
            if(biome==Biome.SaltMarsh||biome==Biome.Mangrove)return LandscapeRegion.TidalWetland;
            if(p.Peat>.55)return LandscapeRegion.Peatland;
            if(p.Canopy>.5&&p.Wet>.4)return LandscapeRegion.WoodedSwamp;
            if(p.Wet>.5)return p.Canopy>.5?LandscapeRegion.WoodedSwamp:LandscapeRegion.FreshwaterMarsh;
            if(p.Salt>.38)return LandscapeRegion.SaltFlat;
            if(p.Clay>.45)return LandscapeRegion.ClayPan;
            if(p.Badlands>.30)return LandscapeRegion.Badlands;
            if(SandSea(metres,seed,p)>.35)return LandscapeRegion.SandSea;
            if(p.Sand>.35)return LandscapeRegion.SandSheet;
            if(biome==Biome.Desert)return p.Rock>.4?LandscapeRegion.RockDesert:LandscapeRegion.GravelDesert;
            if(p.Glacial>.45)return p.Rock>.3?LandscapeRegion.AlpineScree:LandscapeRegion.GlacialLowland;
            if(biome==Biome.Tundra||biome==Biome.Alpine)return LandscapeRegion.Tundra;
            if(p.Loess>.5)return LandscapeRegion.LoessHills;
            if(p.Karst>.45)return LandscapeRegion.LimestoneUpland;
            if(p.Rock>.45)return LandscapeRegion.RockyUpland;
            if(biome==Biome.MediterraneanWoodland)return LandscapeRegion.DryWoodland;
            if(biome==Biome.Grassland&&p.Moisture<.4)return LandscapeRegion.Steppe;
            if(biome==Biome.TropicalRainforest||biome==Biome.TemperateRainforest)return LandscapeRegion.Rainforest;
            return p.Canopy>.7?LandscapeRegion.Forest:p.Canopy>.2?LandscapeRegion.Woodland:LandscapeRegion.VegetatedPlain;
        }
        public static GroundProfile Ground(GroundProfile p,LandscapeControls controls,V metres,int seed)
        {
            double sand=Smooth(.12,.4,SandSea(metres,seed,controls)),clay=controls.Clay,salt=controls.Salt;
            p.Sand=p.Sand*(1-sand)+.94*sand;p.Clay*=1-sand;p.Gravel*=1-.92*sand;p.Organic*=1-.85*sand;
            p.Clay=p.Clay*(1-clay)+.58*clay;p.Sand=p.Sand*(1-clay)+.14*clay;p.Gravel*=1-.8*clay;
            double sum=p.Sand+p.Clay;if(sum>.98){p.Sand*=.98/sum;p.Clay*=.98/sum;}p.Silt=1-p.Sand-p.Clay;
            p.Salinity=Math.Max(p.Salinity,.85*salt);p.Drainage=Math.Min(p.Drainage,1-.9*Math.Max(controls.Wet,clay));
            if(controls.Peat>.1){p.Organic=Math.Max(p.Organic,.45*controls.Peat);p.SoilColor=p.SoilColor*(1-.55*controls.Peat);}
            return p;
        }
        public static void Cover(LandscapeControls controls,V metres,int seed,ref GroundCoverProfile cover,ref MidCoverProfile mid,ref ForestCoverProfile forest,ref Biome biome)
        {
            // The compact ecology/material kernel can include neighbouring
            // grasslands. Active mobile sediment still prevents a rooted lawn.
            double sand=Smooth(.12,.4,SandSea(metres,seed,controls));
            double basin=Smooth(.2,.5,Math.Max(controls.Clay,controls.Salt)),bare=Math.Max(sand,basin);
            if(bare<=0)return;
            cover.ScaleWeights(1-.995*bare);mid.Coverage*=1-.98*bare;forest.Coverage*=1-.995*bare;
            if(sand>.85||basin>.9)biome=Biome.Desert;
        }
    }
}

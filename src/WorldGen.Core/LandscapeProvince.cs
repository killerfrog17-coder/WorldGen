using System;

namespace WorldGen.Core
{
    public enum LandscapeRegion
    {
        VegetatedPlain, Woodland, Forest, Rainforest, DryWoodland, Steppe,
        SandSea, SandSheet, GravelDesert, RockDesert, ClayPan, SaltFlat,
        Badlands, LoessHills, LimestoneUpland, RockyUpland, AlpineScree,
        GlacialLowland, Tundra, FreshwaterMarsh, WoodedSwamp, Peatland, TidalWetland, Icefield
    }

    // Continuous controls, rather than a hard biome replacement or a collection
    // of nearest-cell switches. Shared refined vertices carry the same mixture.
    public struct LandscapeControls
    {
        public float Sand,Clay,Salt,Badlands,Loess,Karst,Rock,Glacial,Frozen,Arid,Wet,Peat,Moisture,Canopy,BareRock,Remnant,RemnantExposure,CanyonExposure;
        public static LandscapeControls Blend(LandscapeControls a,LandscapeControls b,double t)
            => Mix(a,b,default,1-t,t,0);
        public static LandscapeControls Mix(LandscapeControls a,LandscapeControls b,LandscapeControls c,double x,double y,double z)
            =>new LandscapeControls{
                Sand=(float)(a.Sand*x+b.Sand*y+c.Sand*z),Clay=(float)(a.Clay*x+b.Clay*y+c.Clay*z),Salt=(float)(a.Salt*x+b.Salt*y+c.Salt*z),
                Badlands=(float)(a.Badlands*x+b.Badlands*y+c.Badlands*z),Loess=(float)(a.Loess*x+b.Loess*y+c.Loess*z),Karst=(float)(a.Karst*x+b.Karst*y+c.Karst*z),
                Rock=(float)(a.Rock*x+b.Rock*y+c.Rock*z),Glacial=(float)(a.Glacial*x+b.Glacial*y+c.Glacial*z),Frozen=(float)(a.Frozen*x+b.Frozen*y+c.Frozen*z),Arid=(float)(a.Arid*x+b.Arid*y+c.Arid*z),Wet=(float)(a.Wet*x+b.Wet*y+c.Wet*z),CanyonExposure=(float)(a.CanyonExposure*x+b.CanyonExposure*y+c.CanyonExposure*z),
                Peat=(float)(a.Peat*x+b.Peat*y+c.Peat*z),Moisture=(float)(a.Moisture*x+b.Moisture*y+c.Moisture*z),Canopy=(float)(a.Canopy*x+b.Canopy*y+c.Canopy*z),BareRock=(float)(a.BareRock*x+b.BareRock*y+c.BareRock*z),Remnant=(float)(a.Remnant*x+b.Remnant*y+c.Remnant*z),RemnantExposure=(float)(a.RemnantExposure*x+b.RemnantExposure*y+c.RemnantExposure*z)};
        public double Flatness=>Math.Max(Wet*.8,Math.Max(Clay,Salt));
    }

    public static class LandscapeProvince
    {
        public const string Version="landscape-provinces-1";
        static double Smooth(double a,double b,double value)=>OrogenMorphology.Smooth(a,b,value);
        public static Biome WetlandTreeSetting(HabitatCell h,GroundProfile ground,V metres,int seed)
        {
            if(h==null)return Biome.Grassland;
            if(h.Biome!=Biome.Marsh||h.WarmTenthsC<120||h.PermafrostPermille>500||ground.DepthM<.3||ground.Salinity>.08||h.FloodFractionPermille>900)return h.Biome;
            // The coarse "marsh" class previously prohibited woody wetlands
            // everywhere. Seasonally saturated, forest-capable substrates have
            // coherent wooded provinces alongside open herbaceous marshes.
            if(.5+.5*SphereNoise.Sample(metres,seed^0x53f782,1/5200d)<.51)return h.Biome;
            return h.WarmTenthsC>250?Biome.TropicalRainforest:h.WarmTenthsC>160?Biome.TemperateForest:Biome.BorealForest;
        }
        public static LandscapeControls Describe(GroundProfile ground,Biome biome,double moisture,double canopy,double relief,double frozen,double flood,double glacial=0)
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
                Rock=(float)((ground.RockWeight(0)+ground.RockWeight(1)+ground.RockWeight(5))*Math.Max(Smooth(150,1100,relief),dry*(1-Smooth(.08,.3,ground.DepthM)))),
                Remnant=(float)(dry*(ground.RockWeight(0)+.65*ground.RockWeight(2)+.85*ground.RockWeight(5))*(1-Smooth(600,1800,relief))*(1-glacial)*(1-wet)),
                Glacial=(float)D.Clamp(glacial),Frozen=(float)D.Clamp(frozen),Arid=(float)dry,Wet=(float)D.Clamp(wet),Peat=peat?1:(float)Smooth(.12,.35,ground.Organic),
                Moisture=(float)D.Clamp(moisture/1.2),Canopy=(float)D.Clamp(canopy)};
        }
        public static double SandSea(V metres,int seed,LandscapeControls controls)
        {
            if(controls.Sand<=.001)return 0;
            // Sand availability is necessary; only some sediment provinces
            // accumulate a sand sea. The remainder remains sandy sheets/reg.
            double province=.5+.5*SphereNoise.Sample(metres,seed^0x612b7,1/4300d);
            return controls.Sand*(1-controls.Wet)*Smooth(.42,.67,province);
        }
        public static double SaltCrust(LandscapeControls controls)
            =>Smooth(.28,.45,controls.Salt)*(1-Smooth(.2,.5,controls.Wet));
        public static double RootedAdmission(LandscapeControls controls,V metres,int seed)
        {
            double wet=Smooth(.25,.6,controls.Wet);
            double sand=Smooth(.12,.4,SandSea(metres,seed,controls));
            double basin=Smooth(.2,.5,Math.Max(controls.Clay,controls.Salt))*(1-wet);
            // Mature salt crust and actively moving sand are bare, including
            // the individual shrub path that does not use the cover profile.
            double remnant=Math.Max(controls.RemnantExposure,controls.CanyonExposure);
            return 1-Smooth(.02,.98,Math.Max(remnant,Math.Max(controls.BareRock,Math.Max(sand,Math.Max(basin,SaltCrust(controls))))));
        }
        public static double HeightOffset(V metres,int seed,LandscapeControls controls)
        {
            double dunes=SandSea(metres,seed,controls),result=0;
            if(dunes>.001)
            {
                double maturity=Smooth(.25,.8,.5+.5*SphereNoise.Sample(metres,seed^0x3d171,1/7600d));
                double smallSpacing=35/Math.Tan(30*Math.PI/180)/.22,largeSpacing=105/Math.Tan(30*Math.PI/180)/.22;
                // Long stoss slopes meet a sharp crest and a near-repose lee.
                // A cosine bell rounded both sides into generic rolling hills.
                double Ridge(double phase){double t=phase-Math.Floor(phase);return t<.78?Math.Pow(t/.78,1.35):(1-t)/.22;}
                double Stoss(double phase){double t=phase-Math.Floor(phase);return Smooth(.02,.14,t)*(1-Smooth(.56,.77,t));}
                double Profile(double coordinate)
                {
                    double small=coordinate/smallSpacing,large=coordinate/largeSpacing;
                    // Smaller dunes climb the broad windward flank, with their
                    // own crests. Keep the primary slip face clear: adding an
                    // unrestricted second wave made intersecting over-steep walls.
                    double climbing=Ridge(coordinate/155+.13*Math.Sin(coordinate/690));
                    return (35*Ridge(small)+7*climbing*Stoss(small))*(1-maturity)
                        +(105*Ridge(large)+17*climbing*Stoss(large))*maturity;
                }
                double warp=SphereNoise.Sample(metres,seed^0x5c721,1/2400d)*80;
                // Orthogonal to BOTH fixed wave families. The previous cross
                // vector nearly followed the fallback wind, so it warped its
                // slope rather than curving the crest across the dune field.
                double cross=metres.X*-.6805+metres.Y*.4507+metres.Z*.5777;
                double curve=Math.Sin(cross/220)*65+Math.Sin(cross/510)*65;
                double crestHeight=.72+.28*(.5+.5*Math.Sin(cross/170+2*Math.Sin(cross/570)));
                double first=Profile(metres.X*.73+metres.Y*.41+metres.Z*.54+warp+curve);
                double second=Profile(metres.X*.13+metres.Y*.85-metres.Z*.51+warp-curve);
                var direction=metres.Unit;double alignment=direction.X*.73+direction.Y*.41+direction.Z*.54;
                // A fixed 3D wave vector becomes almost radial at two planetary
                // locations, stretching local dunes towards infinity. Blend a
                // second nonparallel family there without blending huge phases.
                double blend=Smooth(.2,.6,1-alignment*alignment),ridge=first*blend+second*(1-blend);
                double secondary=.5+.5*SphereNoise.Sample(metres,seed^0x2b531,1/170d);
                result+=Smooth(.08,.4,dunes)*(ridge*crestHeight+1.2*secondary);
            }
            result+=ErodedTerrain.HeightOffset(metres,seed,controls);
            if(controls.Rock>.01)result+=controls.Rock*.65*SphereNoise.Sample(metres,seed^0x17ba3,1/14d);
            if(controls.Wet>.01)result+=controls.Wet*.12*SphereNoise.Sample(metres,seed^0x73419,1/19d);
            return result;
        }
        public static LandscapeRegion Region(LandscapeControls p,V metres,int seed,Biome biome)
        {
            if(biome==Biome.Ice)return LandscapeRegion.Icefield;
            if(biome==Biome.SaltMarsh||biome==Biome.Mangrove)return LandscapeRegion.TidalWetland;
            if(p.Peat>.55)return LandscapeRegion.Peatland;
            // Moist, poorly drained upland forest alone is not a swamp.
            // Require stronger hydric evidence for the wooded wetland label.
            if(p.Canopy>.5&&p.Wet>.55)return LandscapeRegion.WoodedSwamp;
            if(p.Canopy<=.5&&p.Wet>.5)return LandscapeRegion.FreshwaterMarsh;
            if(p.Salt>.38)return LandscapeRegion.SaltFlat;
            if(p.Clay>.45)return LandscapeRegion.ClayPan;
            if(p.Badlands>.30)return LandscapeRegion.Badlands;
            if(SandSea(metres,seed,p)>.35)return LandscapeRegion.SandSea;
            if(p.Sand>.35)return LandscapeRegion.SandSheet;
            if(biome==Biome.Desert)return p.Rock>.4?LandscapeRegion.RockDesert:LandscapeRegion.GravelDesert;
            if(p.Glacial>.45)return p.Rock>.3?LandscapeRegion.AlpineScree:LandscapeRegion.GlacialLowland;
            if(biome==Biome.Alpine&&p.Rock>.25)return LandscapeRegion.AlpineScree;
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
            p.DepthM=Math.Max(p.DepthM,2*sand);
            if(sand>.85)p.Deposit=ParentDeposit.Windblown;
            p.Clay=p.Clay*(1-clay)+.58*clay;p.Sand=p.Sand*(1-clay)+.14*clay;p.Gravel*=1-.8*clay;
            double pavement=controls.Arid*Smooth(.15,.4,p.Gravel)*(1-sand)*(1-clay)*(1-salt);
            p.Gravel=Math.Max(p.Gravel,.76*pavement);p.DepthM=Math.Min(p.DepthM,Math.Max(.06,1-controls.Rock*controls.Arid)*p.DepthM);
            double sum=p.Sand+p.Clay;if(sum>.98){p.Sand*=.98/sum;p.Clay*=.98/sum;}p.Silt=1-p.Sand-p.Clay;
            p.Salinity=Math.Max(p.Salinity,.85*salt);p.Drainage=Math.Min(p.Drainage,1-.9*Math.Max(controls.Wet,clay));
            double crust=SaltCrust(controls);
            if(crust>0)
            {
                p.Salinity=p.Salinity*(1-crust)+crust;
                p.SoilColor=p.SoilColor*(1-crust)+new V(.93,.94,.93)*crust;
                p.SandColor=p.SandColor*(1-crust)+new V(.94,.94,.92)*crust;
                p.Gravel*=1-.94*crust;p.Organic*=1-.98*crust;
            }
            p.SoilColor*=1-.22*controls.Wet;
            double remnant=Math.Max(controls.RemnantExposure,controls.CanyonExposure);
            p.DepthM*=1-.995*remnant;p.Organic*=1-remnant;
            if(remnant>.5)p.Deposit=ParentDeposit.Residual;
            p.Organic=Math.Max(p.Organic,.12*controls.Wet);
            if(controls.Peat>.1){p.Organic=Math.Max(p.Organic,.45*controls.Peat);p.SoilColor=p.SoilColor*(1-.55*controls.Peat);}
            double volcanic=D.Clamp(controls.BareRock);
            if(volcanic>0)
            {
                p.DepthM*=1-.995*volcanic;p.Organic*=1-volcanic;p.Gravel=Math.Max(p.Gravel,.65*volcanic);
                p.RockMixA=p.RockMixA*(1-volcanic)+new V(0,1,0)*volcanic;p.RockMixB*=1-volcanic;
                p.RockColor=p.RockColor*(1-volcanic)+new V(.18,.17,.16)*volcanic;p.SoilColor=p.SoilColor*(1-volcanic)+new V(.25,.22,.19)*volcanic;
                if(volcanic>.5)p.Bedrock=RockFamily.Basalt;
            }
            return p;
        }
        public static void Cover(LandscapeControls controls,V metres,int seed,ref GroundCoverProfile cover,ref MidCoverProfile mid,ref ForestCoverProfile forest,ref Biome biome)
        {
            double wet=Smooth(.25,.6,controls.Wet);
            if(wet>0)
            {
                // A compact material kernel can inherit upland lawns and
                // trees. Saturated open marsh retains sedges/rushes instead.
                double open=1-Smooth(.2,.48,controls.Canopy);
                for(int f=0;f<3;f++)cover.SetWeight(f,cover.Weight(f)*(1-.92*wet));
                cover.SetWeight(3,Math.Max(cover.Weight(3),.36*wet*open));
                forest.Coverage*=1-.985*wet*open;
                if(controls.Peat>.55)
                {
                    cover.SetWeight(6,Math.Max(cover.Weight(6),.35*wet));
                    cover.Stature*=.65;biome=Biome.Peatland;
                }
                else if(wet>.8&&open>.75)biome=Biome.Marsh;
            }
            // The compact ecology/material kernel can include neighbouring
            // grasslands. Active mobile sediment still prevents a rooted lawn.
            double sand=Smooth(.12,.4,SandSea(metres,seed,controls));
            double basin=Smooth(.2,.5,Math.Max(controls.Clay,controls.Salt))*(1-wet),bare=Math.Max(controls.CanyonExposure,Math.Max(controls.RemnantExposure,Math.Max(controls.BareRock,Math.Max(sand,Math.Max(basin,SaltCrust(controls))))));
            if(controls.Arid>.85&&controls.Canopy<.2&&controls.Wet<.4)biome=Biome.Desert;
            if(bare<=0)return;
            double admission=1-Smooth(.02,.98,bare);
            cover.ScaleWeights(admission);mid.Coverage*=admission;forest.Coverage*=admission;
            if(sand>.85||basin>.9)biome=Biome.Desert;
        }
    }
}

using System;

namespace WorldGen.Core
{
    /// <summary>Continuous, metre-addressed shore profiles over the existing ocean
    /// connection. Sediment-rich shelves, pocket beaches and rocky headlands share
    /// the same terrain/water datum. No independent beach ribbons or water planes.</summary>
    public static class CoastalTerrain
    {
        static readonly V[] sandColours={new V(.77,.72,.61),new V(.24,.25,.24),new V(.76,.58,.37),
            new V(.86,.83,.72),new V(.57,.53,.44),new V(.49,.50,.48)};
        static double Smooth(double a,double b,double value)=>RiverscapeKernel.Smooth((value-a)/(b-a));
        public static double ShoreMud(GroundProfile ground)=>D.Clamp(
            Smooth(.18,.42,ground.Clay)*(1-Smooth(.30,.65,ground.Drainage))
            +.6*Smooth(.06,.15,ground.Organic)*(1-Smooth(.35,.70,ground.Drainage)));
        public static double Deposition(V metres,int seed,double relief,double sediment,double hardness)
        {
            // Relief here is the inherited hill/mountain amplitude, not the
            // observed beach-face slope. The old 25..200m range classified
            // almost every ordinary lowland shore as a resistant headland.
            double rocky=Smooth(60,650,relief)*(.25+.75*D.Clamp(hardness));
            double shelter=Smooth(.52,.78,.5+.5*SphereNoise.Sample(metres,seed^0x382b7,1/2100d));
            return Math.Max((1-rocky)*(.35+.65*D.Clamp(sediment)),.75*D.Clamp(sediment)*shelter);
        }
        public static double BeachBand(GroundProfile ground,double elevation,double ocean,double exposedBedrock=0)
            =>(1-Smooth(2,7,elevation))*Smooth(0,.04,ocean)*(1-ShoreMud(ground))*(1-Smooth(.20,.45,ground.Gravel))*(1-D.Clamp(exposedBedrock));
        public static GroundProfile ShoreGround(GroundProfile ground,double elevation,double ocean,double exposedBedrock=0)
        {
            double beach=BeachBand(ground,elevation,ocean,exposedBedrock);
            ground.Sand=ground.Sand*(1-beach)+.94*beach;ground.Clay*=1-beach;
            ground.Silt=Math.Max(0,1-ground.Sand-ground.Clay);ground.Organic*=1-.98*beach;
            ground.SandColor=ground.SandColor*(1-beach)+SandColour(ground)*beach;
            return ground;
        }
        // A monotone profile with a shallow submerged/emersed rock platform,
        // a steeper scarp, and an unchanged upper coast. No displaced water mesh.
        public static double PlatformProfile(double height)
            =>height*(1-.94*Smooth(-20,-2,height)*(1-Smooth(12,32,height)));
        public static double Shape(V metres,int seed,double height,double seaWeight,double relief,double sediment,double hardness)
            =>Shape(metres,seed,height,seaWeight,relief,sediment,hardness,out _);
        public static double Shape(V metres,int seed,double height,double seaWeight,double relief,double sediment,double hardness,out double exposure)
        {
            exposure=0;
            // All-land root faces have zero sea weight at their boundary too.
            // A hard "coastal triangle" gate would split neighbouring tiles.
            double admission=Smooth(0,.025,seaWeight)*(1-Smooth(55,180,Math.Abs(height)));
            if(admission<=0)return height;
            double rocky=Smooth(60,650,relief)*(.25+.75*D.Clamp(hardness));
            double deposits=Deposition(metres,seed,relief,sediment,hardness);
            double embayment=SphereNoise.Sample(metres,seed^0x71c49,1/1400d)*30
                +SphereNoise.Sample(metres,seed^0x4b917,1/280d)*7;
            double h=height+embayment*(1-Smooth(35,100,Math.Abs(height)));
            double beachBand=Math.Max(0,1-Math.Abs(h)/70);
            h/=1+12*deposits*beachBand*beachBand;
            double cliffBand=Math.Max(0,1-Math.Abs(h)/100);
            h+=h*rocky*2.4*cliffBand*cliffBand;
            // Intermittent bars on shallow depositional shelves, not a second
            // coastline around every island. Most bars remain submerged.
            double barProvince=Smooth(.58,.8,.5+.5*SphereNoise.Sample(metres,seed^0x762b1,1/3100d));
            double bar=Math.Exp(-Math.Pow((h+2.5)/1.8,2))*3.8*deposits*barProvince;
            double platform=0;
            if(rocky>.12&&deposits<.75)
            {
                // A broad proxy for long-term wave exposure, not the current
                // weather: continuous regional stretches, not a platform on
                // every coast or a fresh random decision at each query.
                double waves=Smooth(.38,.70,.5+.5*SphereNoise.Sample(metres,seed^0x6b175,1/3600d));
                platform=Smooth(.12,.55,rocky*(1-Smooth(.30,.75,deposits))*waves);
                h+=(PlatformProfile(h)-h)*platform;
                bar*=1-platform;
            }
            double result=height+(h+bar-height)*admission;
            exposure=admission*platform*Smooth(-8,-.2,result)*(1-Smooth(35,75,result));
            return result;
        }

        public static V SandColour(GroundProfile ground)
        {
            double sum=0;V colour=default;
            // Sand is a grain size: basaltic black, quartz/feldspar cream,
            // iron-stained sandstone and pale carbonate beaches differ.
            for(int i=0;i<6;i++){double w=ground.RockWeight(i);colour+=sandColours[i]*w;sum+=w;}
            return sum>1e-6?colour/sum:ground.SandColor;
        }
    }
}

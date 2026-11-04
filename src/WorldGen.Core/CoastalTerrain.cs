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
        public static double Shape(V metres,int seed,double height,double seaWeight,double relief,double sediment,double hardness)
        {
            // All-land root faces have zero sea weight at their boundary too.
            // A hard "coastal triangle" gate would split neighbouring tiles.
            double admission=Smooth(0,.025,seaWeight)*(1-Smooth(55,180,Math.Abs(height)));
            if(admission<=0)return height;
            double rocky=Smooth(25,200,relief)*(.35+.65*D.Clamp(hardness));
            double deposits=(1-rocky)*(.25+.75*D.Clamp(sediment));
            double embayment=SphereNoise.Sample(metres,seed^0x71c49,1/1400d)*30
                +SphereNoise.Sample(metres,seed^0x4b917,1/280d)*7;
            double h=height+embayment*(1-Smooth(35,100,Math.Abs(height)));
            double beachBand=Math.Max(0,1-Math.Abs(h)/70);
            h/=1+2.6*deposits*beachBand*beachBand;
            double cliffBand=Math.Max(0,1-Math.Abs(h)/100);
            h+=h*rocky*2.4*cliffBand*cliffBand;
            // Intermittent bars on shallow depositional shelves, not a second
            // coastline around every island. Most bars remain submerged.
            double barProvince=Smooth(.58,.8,.5+.5*SphereNoise.Sample(metres,seed^0x762b1,1/3100d));
            double bar=Math.Exp(-Math.Pow((h+2.5)/1.8,2))*3.8*deposits*barProvince;
            return height+(h+bar-height)*admission;
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

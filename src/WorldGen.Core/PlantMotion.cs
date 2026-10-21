using System;

namespace WorldGen.Core
{
    /// <summary>Dimensionless phenotype controls, not measured material constants.</summary>
    public readonly struct PlantMotionTraits
    {
        public readonly double Flexibility,Response,Damping;
        public PlantMotionTraits(double flexibility,double response,double damping)
        {Flexibility=flexibility;Response=response;Damping=damping;}
    }
    public static class PlantMotion
    {
        public const string Version="plant-motion-1";
        public const int Taps=32;
        public const double HistoryStep=.25,HistorySeconds=Taps*HistoryStep;
        // Four underdamped modal responses: slow/fast x light/heavy damping.
        // Blend the resulting responses, not time steps or frame-dependent state.
        public static double Frequency(int mode)=>(mode&1)==0?3.0:7.0;
        public static double DampingRatio(int mode)=>mode<2?.40:.72;
        static double Step(double seconds,int mode)
        {
            double w=Frequency(mode),z=DampingRatio(mode),d=w*Math.Sqrt(1-z*z);
            return 1-Math.Exp(-z*w*seconds)*(Math.Cos(d*seconds)+z*w/d*Math.Sin(d*seconds));
        }
        public static double Weight(int tap,int mode)
            =>(Step((tap+1)*HistoryStep,mode)-Step(tap*HistoryStep,mode))/Step(HistorySeconds,mode);
        public static PlantMotionTraits Herb(int code)
        {
            var r=new Rng(code&~15,"herb-mechanics-1");var form=(SpecialistForm)((code>>24)&7);
            if(form==SpecialistForm.Fungus)return new PlantMotionTraits(0,1,1);
            double branch=.95+(code&15)/15d*.10;
            return new PlantMotionTraits((form==SpecialistForm.Rosette?.45:form==SpecialistForm.Fern?.95:.65+.45*r.Unit())*branch,
                .15+.7*r.Unit(),.15+.7*r.Unit());
        }
        // Same expression is used by GPU grass. Blended regional colour/width
        // express the phenotype; actual local stature supplies leverage/response.
        public static PlantMotionTraits Grass(GroundGrowthForm form,double height,double width,V color)
        {
            double f=form==GroundGrowthForm.DryTuft?.72:form==GroundGrowthForm.SedgeRush?.78:form==GroundGrowthForm.Fern?1.05:.95;
            double gene=.5+.5*Math.Sin((color.X*2.3+color.Y*4.1+color.Z*1.7)*11);
            return new PlantMotionTraits(D.Clamp(f*(.22+.95*height/(height+.45))/(.8+.2*width),.15,1.2),
                D.Clamp(.12+.85/(1+height*2)+.10*(width-1)),.15+.7*gene);
        }
    }
}

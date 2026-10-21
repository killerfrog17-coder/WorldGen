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
        public const string Version="plant-motion-2";
        public const int Taps=40;
        public const double HistoryStep=.25,HistorySeconds=Taps*HistoryStep;
        // Wind controls an animation envelope, not the instantaneous vertex
        // position. Positive, broad history windows reject rapid wind chatter.
        public static double Window(int mode)=>mode==0?7:mode==1?5:mode==2?10:8;
        static double Integral(double seconds,int mode)
        {
            double window=Window(mode),t=D.Clamp(seconds,0,window);
            return t*.5-window/(4*Math.PI)*Math.Sin(2*Math.PI*t/window);
        }
        public static double Weight(int tap,int mode)
            =>(Integral((tap+1)*HistoryStep,mode)-Integral(tap*HistoryStep,mode))/(Window(mode)*.5);
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

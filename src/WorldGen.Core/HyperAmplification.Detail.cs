using System;

namespace WorldGen.Core
{
    public sealed partial class HyperAmplification
    {
        internal sealed partial class Patch
        {
            double RawDetailHeight(V point)
            {
                var f=Locate(point,out double a,out double b,out double c);
                return a*f.A.Height+b*f.B.Height+c*f.C.Height;
            }
            V DetailGradient(Vertex v)
            {
                if(v.DetailGradientReady)return v.DetailGradient;
                lock(owner.gate)
                {
                    if(v.DetailGradientReady)return v.DetailGradient;
                    double physical=owner.radius*owner.scale,epsilon=4/physical;
                    V p=v.Position,e=V.Cross(Math.Abs(p.Z)<.99?new V(0,0,1):new V(0,1,0),p).Unit,n=V.Cross(p,e).Unit;
                    double x=(RawDetailHeight((p+e*epsilon).Unit)-RawDetailHeight((p-e*epsilon).Unit))/(2*epsilon);
                    double y=(RawDetailHeight((p+n*epsilon).Unit)-RawDetailHeight((p-n*epsilon).Unit))/(2*epsilon);
                    var gradient=e*x+n*y;double slope=gradient.Length/physical;
                    if(slope>4)gradient=gradient*(4/slope);
                    v.DetailGradient=gradient;v.DetailGradientReady=true;return gradient;
                }
            }
            void ApplySurfaceDetail(V point,Face f,double a,double b,double c,ref Surface s)
            {
                // Datum/containment and the actual cut bed/banks remain literal.
                // In particular, never smooth the bare substrate used to solve rivers.
                if(s.Kind==LocalWaterKind.River)return;
                double protection=s.DetailProtection*(1-OrogenMorphology.Smooth(0,.10,Math.Abs(s.Height-s.UnshapedHeight)));
                if(!double.IsNaN(s.Water))protection*=OrogenMorphology.Smooth(10,30,s.Height-s.Water);
                if(protection<=0)return;
                V ga=DetailGradient(f.A),gb=DetailGradient(f.B),gc=DetailGradient(f.C);
                double ha=f.A.Height,hb=f.B.Height,hc=f.C.Height;
                double Control(double h,double other,V gradient,V from,V to)=>Math.Max(Math.Min(h,other),Math.Min(Math.Max(h,other),h+V.Dot(gradient,to-from)/3));
                double ab=Control(ha,hb,ga,f.A.Position,f.B.Position),ba=Control(hb,ha,gb,f.B.Position,f.A.Position);
                double ac=Control(ha,hc,ga,f.A.Position,f.C.Position),ca=Control(hc,ha,gc,f.C.Position,f.A.Position);
                double bc=Control(hb,hc,gb,f.B.Position,f.C.Position),cb=Control(hc,hb,gc,f.C.Position,f.B.Position);
                double center=(ab+ba+ac+ca+bc+cb)/6;
                double cubic=a*a*a*ha+b*b*b*hb+c*c*c*hc+3*(a*a*b*ab+a*b*b*ba+a*a*c*ac+a*c*c*ca+b*b*c*bc+b*c*c*cb)+6*a*b*c*center;
                // Bounded cubic Hermite/Bezier reconstruction, shared edge controls.
                // No tile-addressed noise, extra subdivision level or height morph.
                double delta=Math.Max(-1.5,Math.Min(1.5,(cubic-s.UnshapedHeight)*.85));
                double activity=OrogenMorphology.Smooth(8,120,a*(f.A.Hill+f.A.Mountain*.25)+b*(f.B.Hill+f.B.Mountain*.25)+c*(f.C.Hill+f.C.Mountain*.25));
                double sediment=a*f.A.Sediment+b*f.B.Sediment+c*f.C.Sediment;
                double amplitude=(.035+.20*activity)*(1-.7*D.Clamp(sediment));
                V metres=point*(owner.radius*owner.scale);
                double detail=amplitude*(SphereNoise.Sample(metres,owner.seed^0x62b47,1/27d)+.30*SphereNoise.Sample(metres,owner.seed^0x51a23,1/8d));
                s.Height+=(delta+detail)*protection;
                if(!double.IsNaN(s.Water))s.Mask=s.Water-s.Height;
            }
        }
    }
}

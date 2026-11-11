using System;

namespace WorldGen.Core
{
    /// <summary>Bounded channel bends inside the existing drainage valley.
    /// Cohesive low-gradient banks support sinuous channels; steep confined
    /// bedrock channels retain their drainage spine. No new drainage or ticks.</summary>
    public static class RiverPlanform
    {
        public static V[] Build(Func<double,V> spine,int stations,double lengthM,double halfWidthM,
            double grade,double sediment,double hardness,double physicalRadius,ulong key,V? startTangent=null,V? endTangent=null)
        {
            int knots=Math.Max(4,Math.Min(48,(int)Math.Ceiling(lengthM/Math.Max(180,halfWidthM*12))));
            var anchors=new V[knots+1];
            for(int i=0;i<=knots;i++)anchors[i]=spine(i/(double)knots);
            double Smooth(double a,double b,double x)=>RiverscapeKernel.Smooth((x-a)/(b-a));
            double freedom=(1-Smooth(.002,.025,grade))*(.65+.35*D.Clamp(sediment))*(1-.50*D.Clamp(hardness));
            if(freedom<.001&&!startTangent.HasValue&&!endTangent.HasValue)
            {
                var confined=new V[stations+1];
                for(int i=0;i<=stations;i++)confined[i]=spine(i/(double)stations);
                return confined;
            }
            double cycles=Math.Max(1,Math.Min(stations/14d,lengthM/Math.Max(36,halfWidthM*26)));
            double wavelength=lengthM/cycles;
            // The radius of a bend remains wider than the channel itself.
            // Endpoint collars share position AND tangent with the parent spine.
            // A short graph link only a few channel widths long is part of
            // a broad bend/confluence, not room for an extra complete meander.
            double reachFreedom=Smooth(6,12,lengthM/Math.Max(.6,halfWidthM));
            double amplitude=Math.Min(Math.Min(halfWidthM*5.5,lengthM*(.035+.045*reachFreedom)),wavelength*.205)*freedom;
            double phase=((key>>12)&65535)/65535d*Math.PI*2;
            var result=new V[stations+1];
            V Base(double t)
            {
                double q=D.Clamp(t)*knots;int i=Math.Min(knots-1,(int)q);double u=q-i;
                V a=anchors[Math.Max(0,i-1)],b=anchors[i],c=anchors[i+1],d=anchors[Math.Min(knots,i+2)];
                if(i==0&&startTangent.HasValue)a=c-startTangent.Value.Unit*((c-b).Length*2);
                if(i==knots-1&&endTangent.HasValue)d=b+endTangent.Value.Unit*((c-b).Length*2);
                return (b*2+(c-a)*u+(a*2-b*5+c*4-d)*(u*u)+(a*-1+b*3-c*3+d)*(u*u*u)).Unit;
            }
            for(int i=0;i<=stations;i++)
            {
                double t=i/(double)stations;V p=Base(t);
                V tangent=Base(Math.Min(1,t+.0005))-Base(Math.Max(0,t-.0005));
                V side=V.Cross(p,tangent).Unit;
                double envelope=Smooth(0,.13,t)*(1-Smooth(.87,1,t));
                double theta=t*cycles*Math.PI*2+phase+.34*Math.Sin(t*Math.PI*3+phase);
                double bend=(Math.Sin(theta)+.16*Math.Sin(theta*2+phase))*(.78+.22*Math.Sin(t*Math.PI*3.7+phase));
                result[i]=i==0?anchors[0]:i==stations?anchors[knots]:(p+side*(amplitude*envelope*bend/physicalRadius)).Unit;
            }
            return result;
        }
    }
}

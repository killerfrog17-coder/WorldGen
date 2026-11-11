using System;

namespace WorldGen.Core
{
    public sealed partial class HyperAmplification
    {
        // One prepared station field for node fitting, terrain and rendering.
        // Wavelengths use physical channel size, not the number of graph cells.
        void RiverDimensions(int source,V[] points,out double[] widths,out double[] depths)
        {
            int target=riverReceivers[source],n=points.Length-1;var a=rootNodes[source];var b=rootNodes[target];
            double physical=radius*scale,nominal=RiverscapeKernel.HalfWidth(a.Flow,scale),baseDepth=RiverHydraulics.Depth(a.Flow,scale,nominal);
            var distance=new double[n+1];for(int i=1;i<=n;i++)distance[i]=distance[i-1]+(points[i]-points[i-1]).Length*physical;
            double total=distance[n],grade=Math.Max(0,(a.Water-b.Water)/Math.Max(1,total)),transition=Math.Max(24,nominal*2);
            double phase=Random(a.Key,918)*Math.Max(140,nominal*100);
            widths=new double[n+1];depths=new double[n+1];
            for(int i=0;i<=n;i++)
            {
                V before=(points[i]-points[Math.Max(0,i-1)]).Unit,after=(points[Math.Min(n,i+1)]-points[i]).Unit;
                double run=i>0&&i<n?(points[i+1]-points[i-1]).Length*physical:1;
                double bend=i>0&&i<n?D.Clamp(Math.Abs(RiverDot(after-before,V.Cross(points[i],(before+after).Unit)))*nominal*5/Math.Max(1,run)):0;
                var variation=RiverLongitudinalProfile.At(distance[i]+phase,nominal,grade,a.Sediment,a.Hardness,bend,a.Key);
                double taper=Math.Min(RiverscapeKernel.Smooth(distance[i]/transition),RiverscapeKernel.Smooth((total-distance[i])/transition));
                int node=distance[i]<total*.5?source:target;
                widths[i]=nominal*(1+(variation.WidthScale-1)*.3)*taper+riverNodeWidths[node]*(1-taper);
                depths[i]=Math.Min(widths[i]*.75,baseDepth*variation.DepthScale*taper+riverNodeDepths[node]*(1-taper));
            }
        }

        static void RefreshRiverFlow(RiverscapeSpan[] spans,double physical,double discharge,double sediment)
        {
            for(int i=0;i<spans.Length;i++)
            {
                var s=spans[i];double run=(s.B-s.A).Length*physical,grade=Math.Max(0,(s.LevelA-s.LevelB)/Math.Max(.001,run));
                var section=new RiverSection(s.LevelA,(s.WidthA+s.WidthB)*.5,(s.DepthA+s.DepthB)*.5,discharge,grade,sediment);
                s.Speed=section.Speed;s.Turbulence=section.Turbulence;
                s.Primitive=grade>.08?RiverFlowPrimitive.Cascade:grade>.004?RiverFlowPrimitive.Turbulent:RiverFlowPrimitive.Calm;spans[i]=s;
            }
        }
    }
}

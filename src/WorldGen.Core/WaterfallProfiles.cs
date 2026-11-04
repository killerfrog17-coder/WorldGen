using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    /// <summary>Rare bedrock knickpoints on already-descending channels. The
    /// replacement only lowers the old profile: no raised water, independent
    /// floating sheets, new drainage links or changes to graph junction levels.</summary>
    public static class WaterfallProfiles
    {
        public static bool CanHost(int source,int seed)=>(Rng.Mix(unchecked((uint)source^(uint)seed^0x7a41d3u))&0xffffffu)<.045*16777216;
        static RiverscapeSpan Part(RiverscapeSpan span,double a,double b,double levelA,double levelB)
        {
            var result=span;result.A=(span.A*(1-a)+span.B*a).Unit;result.B=(span.A*(1-b)+span.B*b).Unit;
            result.SideA=(span.SideA*(1-a)+span.SideB*a).Unit;result.SideB=(span.SideA*(1-b)+span.SideB*b).Unit;
            result.WidthA=span.WidthA*(1-a)+span.WidthB*a;result.WidthB=span.WidthA*(1-b)+span.WidthB*b;
            result.DepthA=span.DepthA*(1-a)+span.DepthB*a;result.DepthB=span.DepthA*(1-b)+span.DepthB*b;
            result.Depth=(result.DepthA+result.DepthB)*.5;result.LevelA=levelA;result.LevelB=levelB;
            result.From=a==0?span.From:-1;result.To=b==1?span.To:-1;return result;
        }
        public static RiverscapeSpan[] Apply(RiverscapeSpan[] spans,double physicalRadius,double resistance,int source,int seed)
        {
            if(spans.Length<12||resistance<.45||!CanHost(source,seed))return spans;
            int chosen=-1,end=-1;double best=0;
            int first=Math.Max(2,spans.Length/10),last=spans.Length*9/10;
            for(int i=first;i<last-2;i++)
            {
                var a=spans[i];double length=(a.B-a.A).Length*physicalRadius;
                if(a.From>=0||length<1.2||a.WidthA<2||a.WidthA>600)continue;
                double extent=0;
                for(int j=i;j<Math.Min(last,i+64);j++)
                {
                    extent+=(spans[j].B-spans[j].A).Length*physicalRadius;if(extent>450||spans[j].To>=0)break;
                    double crest=a.LevelA+(a.LevelB-a.LevelA)*.18-.05,drop=crest-spans[j].LevelB;
                    if(drop<12||drop>1200||drop/Math.Max(1,extent)<.055)continue;
                    double score=drop/Math.Sqrt(Math.Max(8,extent));
                    if(score>best){best=score;chosen=i;end=j;}
                }
            }
            if(chosen<0)return spans;
            var original=spans[chosen];double spanLength=(original.B-original.A).Length*physicalRadius;
            double lip=.18,top=original.LevelA+(original.LevelB-original.LevelA)*lip-.05,bottom=spans[end].LevelB;
            double dropHeight=top-bottom,run=Math.Min(.47,Math.Max(1.5,Math.Min(10,dropHeight*.06))/spanLength),foot=lip+run;
            var result=new List<RiverscapeSpan>(spans.Length+2);
            for(int i=0;i<chosen;i++)result.Add(spans[i]);
            result.Add(Part(original,0,lip,original.LevelA,top));
            var fall=Part(original,lip,foot,top,bottom);fall.Primitive=RiverFlowPrimitive.Waterfall;
            fall.Speed=Math.Min(28,Math.Sqrt(original.Speed*original.Speed+2*9.81*dropHeight));fall.Turbulence=1;
            double poolDepth=Math.Min(15,dropHeight*.2);
            double poolLength=(original.B-fall.B).Length*physicalRadius;
            for(int i=chosen+1;i<=end;i++)poolLength+=(spans[i].B-spans[i].A).Length*physicalRadius;
            double Pool(double distance)=>poolDepth*Math.Exp(-distance/35)*(1-RiverscapeKernel.Smooth((distance/Math.Max(1,poolLength)-.70)/.30));
            fall.DepthB+=poolDepth;fall.Depth=(fall.DepthA+fall.DepthB)*.5;result.Add(fall);
            var landing=Part(original,foot,1,bottom,bottom);landing.Primitive=RiverFlowPrimitive.Turbulent;
            double downstream=(landing.B-landing.A).Length*physicalRadius;
            landing.DepthA+=poolDepth;landing.DepthB+=Pool(downstream);landing.Depth=(landing.DepthA+landing.DepthB)*.5;landing.Turbulence=1;result.Add(landing);
            for(int i=chosen+1;i<=end;i++)
            {
                var s=spans[i];s.LevelA=s.LevelB=bottom;s.Primitive=RiverFlowPrimitive.Turbulent;
                s.DepthA+=Pool(downstream);downstream+=(s.B-s.A).Length*physicalRadius;
                s.DepthB+=Pool(downstream);s.Depth=(s.DepthA+s.DepthB)*.5;s.Turbulence=Math.Max(s.Turbulence,.8*Math.Exp(-downstream/60));result.Add(s);
            }
            for(int i=end+1;i<spans.Length;i++)result.Add(spans[i]);
            return result.ToArray();
        }
    }
}

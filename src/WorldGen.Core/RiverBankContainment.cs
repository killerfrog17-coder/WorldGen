using System;

namespace WorldGen.Core
{
    // One-time fitting of a cached calm alluvial reach, never a frame query.
    // Shared downstream heads can stand above a small bank hollow. Let a
    // bounded flooded cross-section reach real higher ground instead of
    // lifting the terrain or lowering a graph junction's water level.
    public static class RiverBankContainment
    {
        public static int Fit(RiverscapeSpan[] spans,double radius,double floor,double alluvium,Func<V,double> ground,Action<string> trace=null)
        {
            trace?.Invoke("bank fit spans="+spans.Length+" floor="+floor+" alluvium="+alluvium);
            if(spans.Length<3||floor<=0||alluvium<.1)return 0;
            bool Eligible(int i)=>i>=0&&i<spans.Length&&spans[i].From<0&&spans[i].To<0&&
                spans[i].Primitive==RiverFlowPrimitive.Calm&&spans[i].WidthA>=8&&spans[i].WidthB>=8&&
                Math.Abs(spans[i].LevelA-floor)<1e-6&&Math.Abs(spans[i].LevelB-floor)<1e-6;
            bool Mutable(int station)=>station>0&&station<spans.Length&&spans[station-1].To<0&&spans[station].From<0&&
                spans[station-1].Primitive==RiverFlowPrimitive.Calm&&spans[station].Primitive==RiverFlowPrimitive.Calm;
            bool any=false;for(int i=1;i<spans.Length-1;i++)if(Eligible(i)){any=true;break;}
            if(!any){trace?.Invoke("No eligible calm floor spans");return 0;}
            var widths=new double[spans.Length+1];var original=new double[widths.Length];
            widths[0]=spans[0].WidthA;for(int i=0;i<spans.Length;i++)widths[i+1]=spans[i].WidthB;
            Array.Copy(widths,original,widths.Length);
            bool Clear(RiverscapeSpan s,double t,double wa,double wb)
            {
                double head=s.LevelA*(1-t)+s.LevelB*t;
                V al=(s.A+s.SideA*((wa+2)/radius)).Unit,bl=(s.B+s.SideB*((wb+2)/radius)).Unit;
                V ar=(s.A-s.SideA*((wa+2)/radius)).Unit,br=(s.B-s.SideB*((wb+2)/radius)).Unit;
                return ground((al*(1-t)+bl*t).Unit)>=head+.15&&ground((ar*(1-t)+br*t).Unit)>=head+.15;
            }
            for(int i=1;i<spans.Length-1;i++)
            {
                if(!Eligible(i))continue;var s=spans[i];
                for(int k=0;k<=4;k++)
                {
                    double t=k*.25,width=s.WidthA*(1-t)+s.WidthB*t;
                    if(Clear(s,t,s.WidthA,s.WidthB))continue;
                    double fitted=width;
                    for(int step=1;step<=4;step++)
                    {
                        double candidate=width+Math.Min(80,width)*step*.25;
                        double ratio=candidate/width;
                        if(!Clear(s,t,s.WidthA*ratio,s.WidthB*ratio))continue;fitted=candidate;break;
                    }
                    if(fitted==width)continue;double factor=fitted/width;
                    if(Mutable(i))widths[i]=Math.Max(widths[i],s.WidthA*factor);
                    if(Mutable(i+1))widths[i+1]=Math.Max(widths[i+1],s.WidthB*factor);
                }
            }
            // A shared station can affect its neighbour. Reject this whole
            // tentative fit if any changed ribbon still has an exposed edge.
            int changed=0;
            for(int i=0;i<spans.Length;i++)
            {
                if(widths[i]<=original[i]+.001&&widths[i+1]<=original[i+1]+.001)continue;
                changed++;var s=spans[i];
                for(int k=0;k<=4;k++)
                {
                    double t=k*.25;
                    if(!Clear(s,t,widths[i],widths[i+1])){trace?.Invoke("Rejected changed span="+i+" t="+t+" widths="+widths[i]+","+widths[i+1]);return 0;}
                }
            }
            if(changed==0){trace?.Invoke("No bounded bank width proposals");return 0;}
            for(int i=0;i<spans.Length;i++)
            {
                var s=spans[i];if(widths[i]<=original[i]+.001&&widths[i+1]<=original[i+1]+.001)continue;
                s.Speed*=(s.WidthA+s.WidthB)/(widths[i]+widths[i+1]);
                s.WidthA=widths[i];s.WidthB=widths[i+1];spans[i]=s;
            }
            return changed;
        }
    }
}

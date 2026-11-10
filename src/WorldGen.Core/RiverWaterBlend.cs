using System;
using System.Buffers;

namespace WorldGen.Core
{
    public readonly struct RiverWaterState
    {
        public readonly bool Present;
        public readonly double Level,Speed,Turbulence,Minimum,Maximum;
        public readonly V Flow;
        public readonly int Contributors;
        public RiverWaterState(double level,V flow,double speed,double turbulence,int contributors)
        {Present=true;Level=Minimum=Maximum=level;Flow=flow;Speed=speed;Turbulence=turbulence;Contributors=contributors;}
        public RiverWaterState(double level,V flow,double speed,double turbulence,int contributors,double minimum,double maximum)
        {Present=true;Level=level;Flow=flow;Speed=speed;Turbulence=turbulence;Contributors=contributors;Minimum=minimum;Maximum=maximum;}
    }

    /// <summary>Compact local surface composition. A reach contributes once,
    /// even where two adjacent sections share a boundary. Small ordinary joins
    /// use caller stack storage; unusual crowded junctions use a returned pool.</summary>
    public ref struct RiverWaterBlend
    {
        public struct Contribution
        {internal int Key;internal double Weight,Level,Speed,Turbulence;internal V Flow;}
        Span<Contribution> values;
        Contribution[] rented;
        int count;
        public RiverWaterBlend(Span<Contribution> storage){values=storage;rented=null;count=0;}
        public void Add(int key,double level,double width,double depth,double bankDistance,V flow,double speed,double turbulence)
        {
            if(bankDistance<=0||width<=0||!double.IsFinite(level))return;
            // Section speed is span-constant; using it for stage influence
            // creates a head step at every shared station. Width and depth
            // interpolate the actual common station, so this support is C0.
            double weight=RiverscapeKernel.Smooth(bankDistance/width)*Math.Sqrt(Math.Max(.000001,width*depth));
            var item=new Contribution{Key=key,Weight=weight,Level=level,Flow=flow,Speed=speed,Turbulence=turbulence};
            for(int i=0;i<count;i++)if(values[i].Key==key)
            {if(weight>values[i].Weight||weight==values[i].Weight&&level>values[i].Level)values[i]=item;return;}
            if(count==values.Length)
            {
                var next=ArrayPool<Contribution>.Shared.Rent(Math.Max(16,count*2));values.Slice(0,count).CopyTo(next);
                if(rented!=null)ArrayPool<Contribution>.Shared.Return(rented);rented=next;values=next;
            }
            values[count++]=item;
        }
        public RiverWaterState Sample()
        {
            if(count==1){var c=values[0];return new RiverWaterState(c.Level,c.Flow,c.Speed,c.Turbulence,1);}
            double total=0,head=0,turbulence=0,minimum=double.PositiveInfinity,maximum=double.NegativeInfinity;V velocity=default;
            for(int i=0;i<count;i++)
            {var c=values[i];total+=c.Weight;head+=c.Level*c.Weight;velocity+=c.Flow*(c.Speed*c.Weight);turbulence+=c.Turbulence*c.Weight;minimum=Math.Min(minimum,c.Level);maximum=Math.Max(maximum,c.Level);}
            if(total<=0)return default;velocity*=1/total;
            return new RiverWaterState(head/total,velocity.Unit,velocity.Length,turbulence/total,count,minimum,maximum);
        }
        public void Dispose(){if(rented!=null)ArrayPool<Contribution>.Shared.Return(rented);rented=null;values=default;count=0;}
    }
}

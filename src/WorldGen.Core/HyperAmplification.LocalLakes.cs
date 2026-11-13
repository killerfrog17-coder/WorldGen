namespace WorldGen.Core
{
    public sealed partial class HyperAmplification
    {
        internal sealed partial class Patch
        {
            public bool SmallWaterMayIntersect(V center,double radiusM)
                =>owner.smallWater.MayIntersect(center,radiusM,smallWaterGround)||owner.wetlandWater.MayIntersect(center,radiusM,smallWaterGround);
            // Resolve only an already encountered local water body. No survey
            // of the surrounding map is needed to render a dry triangle corner
            // against that body's finite, continuous footprint.
            public SmallLake LocalLakeAt(ulong identity,V point)
            {
                if(!SmallWaterBodies.IsLocal(identity))return null;
                var lake=owner.smallWater.At(point,smallWaterGround);
                if(lake!=null&&lake.Id==identity)return lake;
                lake=owner.wetlandWater.At(point,smallWaterGround);
                return lake!=null&&lake.Id==identity?lake:null;
            }
        }
    }
}

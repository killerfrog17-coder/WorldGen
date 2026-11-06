using System;
using System.Threading;

namespace WorldGen.Presentation
{
    /// <summary>Exclusive ownership of decoded cover grids. Transfer this lease
    /// from transport to upload to the resident/cache tile. Return it only when
    /// that tile is actually destroyed, never when its mesh upload completes.</summary>
    public sealed class TerrainCoverBuffers : IDisposable
    {
        TerrainUploadBufferPool? pool;
        public float[] Ground { get; private set; }
        public float[] Mid { get; private set; }
        public float[] Forest { get; private set; }
        internal TerrainCoverBuffers(TerrainUploadBufferPool owner,float[] ground,float[] mid,float[] forest)
        {pool=owner;Ground=ground;Mid=mid;Forest=forest;}
        public void Dispose()
        {
            var owner=Interlocked.Exchange(ref pool,null);if(owner==null)return;
            owner.Return(Ground);owner.Return(Mid);owner.Return(Forest);
            Ground=Mid=Forest=Array.Empty<float>();
        }
    }
}

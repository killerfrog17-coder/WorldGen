using System;
using System.Security.Cryptography;

namespace WorldGen.Core
{
    // Separate from the root CryptoStream's SHA. No tree/node/value is retained.
    // A nested canonical computation owns a separate lazy tree context and
    // restores its caller's context, including on cancellation/serialization errors.
    internal static class CanonicalTreeScratch
    {
        [ThreadStatic] static Context current;
        [ThreadStatic] static int depth;
        internal sealed class Context:IDisposable
        {
            internal readonly SHA256 Sha=CanonicalSha256.Create();
            internal readonly byte[][] Buffers=new byte[5][];
            internal readonly byte[] Root=new byte[36];
            public void Dispose()=>Sha.Dispose();
        }
        internal struct Scope:IDisposable
        {
            readonly Context previous;readonly int previousDepth;bool disposed;
            internal Scope(Context previous,int previousDepth){this.previous=previous;this.previousDepth=previousDepth;disposed=false;current=null;depth=previousDepth+1;}
            public void Dispose()
            {
                if(disposed)return;if(depth!=previousDepth+1)throw new InvalidOperationException("Tree hash scopes must close in reverse order.");
                disposed=true;var owned=current;current=previous;depth=previousDepth;owned?.Dispose();
            }
        }
        internal static Scope Begin()=>new Scope(current,depth);
        internal static Context Acquire(out bool owned)
        {owned=depth==0;if(owned)return new Context();return current??(current=new Context());}
    }
}

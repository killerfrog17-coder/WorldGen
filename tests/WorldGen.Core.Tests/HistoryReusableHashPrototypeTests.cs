using System.Diagnostics;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;

// Local prototype only: the production provider is not changed by this fixture.
public static class HistoryReusableHashPrototypeTests
{
    static readonly Type Node=typeof(HistoricalCellTree).GetNestedType("Node",BindingFlags.NonPublic)!;
    static readonly FieldInfo Root=typeof(HistoricalCellTree).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly FieldInfo Children=Node.GetField("Children",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly FieldInfo Values=Node.GetField("Values",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly FieldInfo Digest=Node.GetField("Digest",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly byte[] Absent=new byte[32];
    sealed class Prototype:SHA256
    {
        readonly IntPtr algorithm;readonly bool reusable;IntPtr hash;bool finished,disposed;
        public int Creates,Initializes,Calls;public long Bytes;
        public Prototype(IntPtr algorithm,bool reusable){this.algorithm=algorithm;this.reusable=reusable;HashSizeValue=256;Initialize();}
        public override void Initialize()
        {
            if(disposed)throw new ObjectDisposedException(nameof(Prototype));Initializes++;
            if(reusable&&hash!=IntPtr.Zero&&finished){finished=false;return;}
            if(hash!=IntPtr.Zero)Check(BCryptDestroyHash(hash));Check(BCryptCreateHash(algorithm,out hash,IntPtr.Zero,0,IntPtr.Zero,0,reusable?0x20:0));Creates++;finished=false;
        }
        protected override void HashCore(byte[] bytes,int offset,int count)
        {
            if(count==0)return;finished=false;Calls++;Bytes+=count;var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);
            try{Check(BCryptHashData(hash,IntPtr.Add(pinned.AddrOfPinnedObject(),offset),count,0));}finally{pinned.Free();}
        }
        protected override byte[] HashFinal(){var bytes=new byte[32];Check(BCryptFinishHash(hash,bytes,32,0));finished=true;return bytes;}
        protected override void Dispose(bool disposing){if(!disposed){disposed=true;if(hash!=IntPtr.Zero){BCryptDestroyHash(hash);hash=IntPtr.Zero;}}base.Dispose(disposing);}
    }
    static void Put(byte[] bytes,int at,int value){for(int k=0;k<4;k++)bytes[at+k]=(byte)(value>>(k*8));}
    static string Fingerprint(HistoricalCellTree tree,Prototype sha)
    {
        var scratch=new byte[5][];
        byte[] Visit(object node,int depth)
        {
            if(node==null)return Absent;var prior=(byte[])Digest.GetValue(node);if(prior!=null)return prior;
            var values=(int[])Values.GetValue(node);int count=values==null?1025:129;var bytes=scratch[depth]??(scratch[depth]=new byte[count]);bytes[0]=(byte)(values==null?0:1);
            if(values!=null){for(int k=0;k<32;k++)Put(bytes,1+k*4,values[k]);}
            else{var children=(Array)Children.GetValue(node);for(int k=0;k<32;k++)Buffer.BlockCopy(Visit(children.GetValue(k),depth+1),0,bytes,1+k*32,32);}
            var result=sha.ComputeHash(bytes,0,count);Digest.SetValue(node,result);return result;
        }
        var whole=new byte[36];Put(whole,0,tree.Length);Buffer.BlockCopy(Visit(Root.GetValue(tree),0),0,whole,4,32);return Convert.ToHexString(sha.ComputeHash(whole)).ToLowerInvariant();
    }
    public static void Run(Action<bool,string> check)
    {
        Check(BCryptOpenAlgorithmProvider(out var algorithm,"SHA256",null,0));
        try
        {
            foreach(bool reusable in new[]{false,true})
            {
                using var sha=new Prototype(algorithm,reusable);using var literal=SHA256.Create();var vector=Enumerable.Range(0,2049).Select(i=>(byte)(i*73)).ToArray();
                foreach(int length in new[]{0,1,3,55,56,63,64,65,129,1025,2049})check(sha.ComputeHash(vector,0,length).SequenceEqual(literal.ComputeHash(vector,0,length)),"Prototype repeat/reset SHA vectors preserve exact bytes.");
                sha.TransformBlock(vector,0,31,null,0);sha.Initialize();check(sha.ComputeHash(vector).SequenceEqual(literal.ComputeHash(vector)),"Explicit reinitialization discards an unfinished hash, rather than retaining old data.");
                sha.TransformBlock(vector,0,1024,null,0);sha.TransformFinalBlock(vector,1024,1025);check(sha.Hash!.SequenceEqual(literal.ComputeHash(vector)),"Chunked hash data remain independent across native reuse.");
            }
            for(int repeat=0;repeat<2;repeat++)foreach(bool reusable in new[]{false,true})
            {
                var tree=HistoricalCellTree.From(65536,i=>i%997);using var initial=new Prototype(algorithm,reusable);Fingerprint(tree,initial);
                int creates=0,initializes=0,calls=0;long bytes=0,allocated=GC.GetAllocatedBytesForCurrentThread();string last="";var watch=Stopwatch.StartNew();
                for(int iteration=0;iteration<60;iteration++)
                {
                    for(int change=0;change<64;change++){int index=(iteration*7919+change*977)%tree.Length;tree=tree.With(index,iteration*64+change+2000);}
                    using var sha=new Prototype(algorithm,reusable);last=Fingerprint(tree,sha);creates+=sha.Creates;initializes+=sha.Initializes;calls+=sha.Calls;bytes+=sha.Bytes;
                }
                double elapsed=watch.Elapsed.TotalMilliseconds;long memory=GC.GetAllocatedBytesForCurrentThread()-allocated;
                check(last==tree.Fingerprint(true),"Actual dirty-tree writes and prototype fingerprints equal the authoritative Merkle fingerprint.");
                Console.WriteLine(JsonSerializer.Serialize(new{Repeat=repeat,Reusable=reusable,Milliseconds=elapsed,Allocated=memory,HashCreates=creates,HashInitializes=initializes,HashCalls=calls,HashBytes=bytes}));
            }
        }
        finally{BCryptCloseAlgorithmProvider(algorithm,0);}
    }
    public static void ProductionBenchmark(string baselineCore)
    {
        var context=new System.Runtime.Loader.AssemblyLoadContext("preceding-cng-tree",true);var baseline=context.LoadFromAssemblyPath(Path.GetFullPath(baselineCore)).GetType("WorldGen.Core.HistoricalCellTree")!;
        var kind=new[]{("preceding",baseline),("reusable",typeof(HistoricalCellTree))};string previous="";
        foreach(var entry in kind)
        {
            var target=Expression.Parameter(typeof(object));var index=Expression.Parameter(typeof(int));var value=Expression.Parameter(typeof(int));
            var create=Expression.Lambda<Func<object>>(Expression.Convert(Expression.Call(entry.Item2.GetMethod("From")!,Expression.Constant(65536),Expression.Constant((Func<int,int>)(i=>i%997))),typeof(object))).Compile();
            var update=Expression.Lambda<Func<object,int,int,object>>(Expression.Convert(Expression.Call(Expression.Convert(target,entry.Item2),entry.Item2.GetMethod("With")!,index,value),typeof(object)),target,index,value).Compile();
            var fingerprint=Expression.Lambda<Func<object,string>>(Expression.Call(Expression.Convert(target,entry.Item2),entry.Item2.GetMethod("Fingerprint")!,Expression.Constant(false)),target).Compile();
            for(int repeat=0;repeat<3;repeat++)
            {
                var tree=create();fingerprint(tree);long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();string last="";
                for(int iteration=0;iteration<240;iteration++){for(int change=0;change<64;change++)tree=update(tree,(iteration*7919+change*977)%65536,iteration*64+change+2000);last=fingerprint(tree);}
                double elapsed=watch.Elapsed.TotalMilliseconds;long memory=GC.GetAllocatedBytesForCurrentThread()-allocated;
                if(previous.Length>0&&last!=previous)throw new Exception("Production reusable tree hash differs from preceding Core.");previous=last;
                Console.WriteLine(JsonSerializer.Serialize(new{Backend=entry.Item1,Repeat=repeat,Milliseconds=elapsed,Allocated=memory,Hash=last}));
            }
        }
        context.Unload();
    }
    static void Check(int status){if(status<0)throw new CryptographicException("CNG prototype 0x"+status.ToString("X8"));}
    [DllImport("bcrypt.dll",CharSet=CharSet.Unicode)] static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithm,string name,string implementation,int flags);
    [DllImport("bcrypt.dll")] static extern int BCryptCreateHash(IntPtr algorithm,out IntPtr hash,IntPtr storage,int storageLength,IntPtr secret,int secretLength,int flags);
    [DllImport("bcrypt.dll")] static extern int BCryptHashData(IntPtr hash,IntPtr bytes,int count,int flags);
    [DllImport("bcrypt.dll")] static extern int BCryptFinishHash(IntPtr hash,byte[] bytes,int count,int flags);
    [DllImport("bcrypt.dll")] static extern int BCryptDestroyHash(IntPtr hash);
    [DllImport("bcrypt.dll")] static extern int BCryptCloseAlgorithmProvider(IntPtr algorithm,int flags);
}

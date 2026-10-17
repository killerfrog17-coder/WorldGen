using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WorldGen.Core;

public static class HistoryCanonicalProviderTests
{
    static readonly Type Provider=typeof(StateHash).Assembly.GetType("WorldGen.Core.CanonicalSha256")!;
    static SHA256 Create()=>(SHA256)Provider.GetMethod("Create",BindingFlags.Static|BindingFlags.Public)!.Invoke(null,null)!;
    static string Hex(byte[] bytes)=>Convert.ToHexString(bytes).ToLowerInvariant();
    public static void Run(Action<bool,string> check)
    {
        const string empty="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",abcHash="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",longHash="248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1";
        byte[] abc=Encoding.ASCII.GetBytes("abc"),offset=Encoding.ASCII.GetBytes("prefixabcTAIL"),longInput=Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq");
        check(CanonicalHashProviderDiagnostics.Verify().StartsWith("PASS 13"),"The shared Core smoke API verifies the actual runtime provider, including independent parallel contexts.");
        using(var sha=Create())
        {
            check(sha.HashSize==256&&Hex(sha.ComputeHash(Array.Empty<byte>()))==empty,"The actual canonical provider has the standard empty SHA-256 digest and size.");
            check(Hex(sha.ComputeHash(offset,6,3))==abcHash&&Encoding.ASCII.GetString(offset)=="prefixabcTAIL","Offset hashing selects the exact range without modifying surrounding bytes.");
            for(int split=0;split<=longInput.Length;split++)
            {
                sha.Initialize();sha.TransformBlock(longInput,0,split,null,0);sha.TransformFinalBlock(longInput,split,longInput.Length-split);
                check(Hex(sha.Hash!)==longHash,"Every chunk boundary produces the independent known long-message digest.");
            }
            sha.Initialize();sha.TransformBlock(offset,0,6,null,0);sha.Initialize();
            check(Hex(sha.ComputeHash(abc))==abcHash,"Explicit reinitialization discards an unfinished prior message.");
            for(int i=0;i<16;i++)check(Hex(sha.ComputeHash(i%2==0?abc:Array.Empty<byte>()))==(i%2==0?abcHash:empty),"Repeated ComputeHash calls reset independent message state.");
            using(var fallback=SHA256.Create())check(fallback.ComputeHash(longInput).SequenceEqual(sha.ComputeHash(longInput)),"The unchanged platform fallback has the same independently known digest.");
        }
        using(var live=Create())
        {
            live.TransformBlock(abc,0,1,null,0);
            for(int i=0;i<32;i++){using var other=Create();check(Hex(other.ComputeHash(abc))==abcHash,"Disposing another independent context does not close the shared provider.");}
            live.TransformFinalBlock(abc,1,2);check(Hex(live.Hash!)==abcHash,"A still-open hash remains valid after other contexts and provider leases are disposed.");
        }
        int errors=0;Parallel.For(0,128,i=>{using var sha=Create();sha.TransformBlock(longInput,0,i%longInput.Length,null,0);sha.TransformFinalBlock(longInput,i%longInput.Length,longInput.Length-i%longInput.Length);if(Hex(sha.Hash!)!=longHash)Interlocked.Increment(ref errors);});
        check(errors==0,"128 concurrent variable-chunk hashes never share mutable hash state.");
        var closed=Create();closed.Dispose();bool rejected=false;try{closed.Initialize();}catch(ObjectDisposedException){rejected=true;}
        check(rejected,"A disposed hash cannot resurrect a native context through Initialize while the shared provider remains alive.");
        int attempts=(int)Provider.GetProperty("ProviderOpenAttempts",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        check(attempts<=1,"The process opens/probes its immutable Windows algorithm provider at most once.");
        var unavailable=Provider.GetField("reusableHashUnavailable",BindingFlags.Static|BindingFlags.NonPublic)!;int preceding=(int)unavailable.GetValue(null)!;
        try
        {
            unavailable.SetValue(null,1);using var legacy=Create();
            for(int i=0;i<32;i++)check(Hex(legacy.ComputeHash(i%2==0?abc:Array.Empty<byte>()))==(i%2==0?abcHash:empty),"A provider without native reusable-hash support retains the exact preceding fallback behavior.");
            legacy.TransformBlock(abc,0,1,null,0);legacy.Initialize();check(Hex(legacy.ComputeHash(abc))==abcHash,"Legacy native fallback still discards explicit unfinished-message resets.");
        }
        finally{unavailable.SetValue(null,preceding);}
        Console.WriteLine(CanonicalHashProviderDiagnostics.Verify());
    }
    public static void Benchmark(string baselineCore)
    {
        var context=new System.Runtime.Loader.AssemblyLoadContext("baseline-sha-provider",true);
        var baseline=context.LoadFromAssemblyPath(Path.GetFullPath(baselineCore)).GetType("WorldGen.Core.CanonicalSha256")!;
        var previous=(Func<SHA256>)baseline.GetMethod("Create",BindingFlags.Static|BindingFlags.Public)!.CreateDelegate(typeof(Func<SHA256>));
        var current=(Func<SHA256>)Provider.GetMethod("Create",BindingFlags.Static|BindingFlags.Public)!.CreateDelegate(typeof(Func<SHA256>));
        byte[] payload=Enumerable.Range(0,1024).Select(i=>(byte)i).ToArray();const int iterations=20000;
        int expected;using(var sha=SHA256.Create())expected=sha.ComputeHash(payload)[0]*iterations;
        void Measure(string name,Func<SHA256> create)
        {
            for(int i=0;i<128;i++){using var warm=create();warm.ComputeHash(payload);}
            long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();int sum=0;
            for(int i=0;i<iterations;i++){using var sha=create();sum+=sha.ComputeHash(payload)[0];}
            timer.Stop();if(sum!=expected)throw new Exception("Provider benchmark produced a nonstandard digest.");
            Console.WriteLine(FormattableString.Invariant($"{name}: {iterations} independent1024-byte hashes; {timer.Elapsed.TotalMilliseconds:F3}ms; {GC.GetAllocatedBytesForCurrentThread()-bytes}B"));
        }
        Measure("Frozen provider",previous);Measure("Cached provider",current);context.Unload();
    }
}

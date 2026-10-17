using System.Reflection;
using System.Security.Cryptography;
using WorldGen.Core;

public static class HistoryTreeCanonicalBufferTests
{
    // Independent copy of the preceding wire algorithm, intentionally retaining
    // its literal streams. It never calls the optimized digest implementation.
    static string Reference(HistoricalCellTree tree)
    {
        var root=typeof(HistoricalCellTree).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(tree);
        using var sha=SHA256.Create();
        byte[] Digest(object node)
        {
            if(node==null)return new byte[32];var type=node.GetType();var values=(int[])type.GetField("Values",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(node);var children=(Array)type.GetField("Children",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(node);
            using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);writer.Write(values!=null);
            if(values!=null){foreach(int value in values)writer.Write(value);}else{foreach(var child in children!)writer.Write(Digest(child));}
            return sha.ComputeHash(bytes.ToArray());
        }
        using var stream=new MemoryStream();using var body=new BinaryWriter(stream);body.Write(tree.Length);body.Write(Digest(root));return Convert.ToHexString(sha.ComputeHash(stream.ToArray())).ToLowerInvariant();
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int length in new[]{0,1,31,32,33,1024,1025,32768,1048577,33554432})
        {
            var tree=new HistoricalCellTree(length);check(tree.Fingerprint()==Reference(tree),"Empty radix roots preserve the preceding literal length-plus-zero digest format.");
            for(int k=0;k<Math.Min(length,97);k++)
            {
                int at=(int)((long)k*7919%length),value=k%3==0?int.MinValue:k%3==1?int.MaxValue:k*104729;tree=tree.With(at,value);
            }
            string reference=Reference(tree);check(tree.Fingerprint()==reference&&tree.Fingerprint(true)==reference,"Dirty signed-extreme leaves and all radix depth boundaries preserve exact literal SHA bytes.");
            if(length>0){tree=tree.With(length-1,12345);check(tree.Fingerprint()==Reference(tree),"A later persistent edit cannot reuse a parent's partially filled buffer or stale digest.");tree=tree.With(length-1,-1);check(tree.Fingerprint(true)==Reference(tree),"Removing a leaf value preserves canonical sparse/absent children.");}
        }
        var shared=HistoricalCellTree.From(4097,i=>i%7==0?int.MinValue+i:-1);string expected=Reference(shared);
        Parallel.For(0,32,k=>check(shared.Fingerprint(k%2==0)==expected,"Concurrent cached and explicit verification hashes have isolated depth scratch."));
        var tampered=new HistoricalCellTree(32).With(7,42);string old=tampered.Fingerprint();object node=typeof(HistoricalCellTree).GetField("root",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(tampered)!;
        for(int depth=0;depth<4;depth++)node=((Array)node.GetType().GetField("Children",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(node)!).GetValue(0)!;
        var leaf=(int[])node.GetType().GetField("Values",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(node)!;leaf[7]=43;
        check(tampered.Fingerprint(true)!=old&&tampered.Fingerprint(true)==Reference(tampered),"Explicit integrity verification still detects modified actual leaf data despite previously cached hashes.");
    }
}

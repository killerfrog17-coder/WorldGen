using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using WorldGen.Core;
public static class HistorySeedTreeDigestTests
{
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static string Hex(byte[] bytes)=>Convert.ToHexString(bytes).ToLowerInvariant();
    static byte[] Inventory(HistoricalTownSeeds record,SHA256 sha)
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(record.TownId);writer.Write(record.FieldCell);
        for(int group=0;group<6;group++)
        {
            var lot=record[group];writer.Write(lot!=null);if(lot==null)continue;var c=lot.Cultivar;
            writer.Write(lot.AcquiredYear);writer.Write(lot.SourceTown);writer.Write(lot.SourceCell);writer.Write(lot.PracticeYears);writer.Write(lot.LastPracticeYear);
            foreach(int value in new[]{c.CropId,c.OriginCulture,c.ParentCulture,c.SelectedYears,c.MaturityDays,c.DaylengthLatitudeTenths,c.WaterRequirementPermille,c.YieldPermille,c.MinimumWarmTenthsC})writer.Write(value);
        }
        return sha.ComputeHash(stream.ToArray());
    }
    static string Literal(HistoricalTownSeedTree tree)
    {
        using var sha=SHA256.Create();
        byte[] Visit(object node,int depth)
        {
            if(node==null)return new byte[32];var type=node.GetType();var bytes=new byte[1025];bytes[0]=(byte)(depth==6?1:0);
            var children=(Array)type.GetField(depth==6?"Values":"Children",Private)!.GetValue(node)!;
            for(int k=0;k<32;k++){object child=children.GetValue(k)!;byte[] digest=depth==6?child==null?new byte[32]:Inventory((HistoricalTownSeeds)child,sha):Visit(child,depth+1);Buffer.BlockCopy(digest,0,bytes,1+32*k,32);}
            return sha.ComputeHash(bytes);
        }
        return Hex(Visit(typeof(HistoricalTownSeedTree).GetField("root",Private)!.GetValue(tree)!,0));
    }
    static HistoricalTownSeeds Record(int town,int variation=0)
    {
        var lots=new HistoricalLocalSeedlot[6];
        for(int group=0;group<6;group++)if((town+group)%3!=0)
        {var crop=new HistoricalCultivar{CropId=group,OriginCulture=3,ParentCulture=-1,SelectedYears=17+variation,MaturityDays=120,DaylengthLatitudeTenths=420,WaterRequirementPermille=350,MinimumWarmTenthsC=110};lots[group]=new HistoricalLocalSeedlot(crop,30,2,91,17,50);}
        return new HistoricalTownSeeds(town,123,lots);
    }
    public static void Run(Action<bool,string> check)
    {
        var tree=new HistoricalTownSeedTree();
        foreach(int town in new[]{0,1,31,32,33,1023,1024,1048577,int.MaxValue})tree=tree.With(town,Record(town));
        string expected=Literal(tree);check(tree.Fingerprint()==expected,"Optimized seed radix hash matches the preceding literal byte format, including sparse high town IDs.");
        check(tree.Fingerprint(true)==expected,"Explicit verification reproduces every actual cultivar and canonical node byte.");
        var next=tree.With(31,Record(31,1));string nextExpected=Literal(next);
        check(nextExpected!=expected&&next.Fingerprint()==nextExpected&&tree.Fingerprint(true)==expected,"One changed immutable inventory invalidates only its new branch and cannot modify an earlier tree.");
        var publicCopy=next[31][0]?.Cultivar;if(publicCopy!=null)publicCopy.YieldPermille++;
        check(next.Fingerprint(true)==nextExpected,"A returned cultivar copy cannot mutate a cached seed inventory.");
        var hashes=new string[32];Parallel.For(0,32,k=>hashes[k]=next.With(2000+k,Record(2000+k)).Fingerprint());
        check(hashes.Distinct().Count()==32&&next.Fingerprint(true)==nextExpected,"Independent concurrent hashes cannot alias scratch buffers or shared immutable inventories.");
        var serializer=new DataContractSerializer(typeof(HistoricalTownSeedTree));using var stream=new MemoryStream();serializer.WriteObject(stream,next);stream.Position=0;
        var loaded=(HistoricalTownSeedTree)serializer.ReadObject(stream)!;
        check(loaded.Fingerprint(true)==nextExpected&&loaded.Fingerprint()==nextExpected,"Ignored digest caches reconstruct exactly after a real DataContract cold roundtrip.");
        // Deliberate integrity-test corruption through reflection, not a legal
        // caller mutation: explicit verification must not trust the warm memo.
        var lot=loaded[31][0]??loaded[31][1];var profile=(HistoricalCultivar)typeof(HistoricalLocalSeedlot).GetField("cultivar",Private)!.GetValue(lot)!;profile.YieldPermille++;
        string changed=Literal(loaded);check(changed!=nextExpected&&loaded.Fingerprint(true)==changed,"Explicit verification detects actual seed corruption rather than trusting immutable-record memoization.");
        check(loaded.Fingerprint()==changed,"Successful explicit verification refreshes the ordinary fingerprint consistently.");
        var empty=new HistoricalTownSeedTree();check(empty.Fingerprint()==Literal(empty),"Empty seed radix retains its preceding all-zero absent-root digest.");
    }
}

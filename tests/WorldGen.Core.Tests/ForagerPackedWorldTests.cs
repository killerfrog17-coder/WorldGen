using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerPackedWorldTests
{
    public static void Run(string file,string output)
    {
        int checks=ForagerRecordTransportTests.Run();void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var random=new Random(173);var values=Enumerable.Range(0,10000).Select(_=>D.Q(random.NextDouble())).ToArray();
        foreach(var vector in new[]{values,new[]{0d,1d,-1d,.25,.1},new[]{-0d,double.NaN,double.PositiveInfinity,double.NegativeInfinity,double.Epsilon,Math.PI},Array.Empty<double>(),new[]{1e20,-1e20,.12345678912345678}})
        {
            var packed=PackedDoubleVector.Pack(vector);var restored=packed.Unpack();
            Check(vector.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(restored.Select(BitConverter.DoubleToInt64Bits)),"Numeric transport preserves every original floating-point bit, including raw fallback and signed zero");
        }
        Check(PackedDoubleVector.Pack(values).Data.Length==values.Length*4,"Quantised probability vectors actually use half-width numeric storage");
        bool rejected=false;try{new PackedDoubleVector{Scale=77,Data=new byte[4]}.Unpack();}catch(System.Runtime.Serialization.SerializationException){rejected=true;}Check(rejected,"Unrecognised numeric encodings are rejected");
        rejected=false;try{new PackedDoubleVector{Scale=1000,Data=new byte[3]}.Unpack();}catch(System.Runtime.Serialization.SerializationException){rejected=true;}Check(rejected,"Truncated numeric vectors are rejected");
        var watch=Stopwatch.StartNew();var w=WorldFile.ReadFile(file);double readOld=watch.Elapsed.TotalSeconds;string before=w.Prehistory.Hash;watch.Restart();var bytes=WorldFile.Write(w);double writeSeconds=watch.Elapsed.TotalSeconds;
        Check(bytes[7]==51,"New forager saves identify the new lossless transport version");
        watch.Restart();var reload=WorldFile.Read(bytes);double readNew=watch.Elapsed.TotalSeconds;Check(reload.Prehistory.Hash==before&&reload.Prehistory.ComputeHash()==before,"An actual old snapshot rewrites and reloads with the exact same complete historical hash");
        Check(ForagerRecordTransportTests.Fingerprint(w.Prehistory.Foragers)==ForagerRecordTransportTests.Fingerprint(reload.Prehistory.Foragers),"Every field of the full real-world history preserves its raw representation, beyond the quantised canonical hash");
        var expected=new Prehistory(w).AdvanceYears(w.Prehistory,25);var actual=new Prehistory(reload).AdvanceYears(reload.Prehistory,25);
        Check(expected.Hash==actual.Hash,"Future simulation remains identical after a lossless format migration");
        Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"packed.world"),bytes);
        var library=new WorldLibrary(Path.Combine(output,"library"));var saved=library.SaveNamed(null,"Long history",w);
        string historyFile=Path.Combine(Path.GetDirectoryName(saved.SnapshotPath)!,"history.bin");
        Check(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(historyFile),0,8)=="WGHIST03","Actual named saves use the lossless history transport, not only test snapshots");
        Check(library.Load(saved.Id).Prehistory.Hash==before,"The player's shared-base save/load path preserves the full historical ledger");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,before,after=actual.Hash,originalBytes=new FileInfo(file).Length,packedBytes=bytes.Length,readOld,writeSeconds,readNew},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} lossless numeric/world compatibility checks; {new FileInfo(file).Length} -> {bytes.Length} bytes");
    }
}

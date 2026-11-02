using System.Reflection;
using System.Security.Cryptography;
using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerRecordTransportTests
{
    static readonly Dictionary<Type,FieldInfo[]> fields=new();
    public static string Fingerprint(object value)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream=new CryptoStream(Stream.Null,new HashAdapter(hash),CryptoStreamMode.Write);
        using(var writer=new BinaryWriter(stream,System.Text.Encoding.Unicode,true))Write(writer,value);
        stream.FlushFinalBlock();return Convert.ToHexString(hash.GetHashAndReset());
    }
    sealed class HashAdapter:ICryptoTransform
    {
        readonly IncrementalHash hash;public HashAdapter(IncrementalHash h){hash=h;}
        public bool CanReuseTransform=>false;public bool CanTransformMultipleBlocks=>true;public int InputBlockSize=>1;public int OutputBlockSize=>1;
        public int TransformBlock(byte[] input,int offset,int count,byte[] output,int outputOffset){hash.AppendData(input,offset,count);return 0;}
        public byte[] TransformFinalBlock(byte[] input,int offset,int count){hash.AppendData(input,offset,count);return Array.Empty<byte>();}
        public void Dispose(){}
    }
    static void Write(BinaryWriter writer,object value)
    {
        writer.Write(value!=null);if(value==null)return;
        var type=value.GetType();writer.Write(type.FullName!);
        if(value is double d){writer.Write(BitConverter.DoubleToInt64Bits(d));return;}
        if(value is int i){writer.Write(i);return;}if(value is long l){writer.Write(l);return;}
        if(value is bool b){writer.Write(b);return;}
        if(value is string s){writer.Write(s.Length);foreach(char c in s)writer.Write((ushort)c);return;}
        if(type.IsEnum){writer.Write(Convert.ToInt32(value));return;}
        if(value is Array a){writer.Write(a.Length);foreach(var item in a)Write(writer,item);return;}
        if(!fields.TryGetValue(type,out var members))fields[type]=members=type.GetFields(BindingFlags.Instance|BindingFlags.Public).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray();
        foreach(var field in members){writer.Write(field.Name);Write(writer,field.GetValue(value));}
    }
    public static int Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        object Value(Type type,int variant)
        {
            if(type==typeof(double))return BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));
            if(type==typeof(int))return int.MinValue+91;if(type==typeof(long))return long.MaxValue-9;
            if(type.IsEnum)return Enum.ToObject(type,3);
            if(type==typeof(string))return variant==0?null:"A\ud800\udfff\0Z";
            if(variant==0)return null;
            if(type==typeof(double[]))return new[]{-0d,double.NaN,double.PositiveInfinity,Math.PI,.123456789,1e20};
            if(type==typeof(int[]))return new[]{int.MinValue,0,int.MaxValue};
            if(type==typeof(ForagerAffiliation[]))return new ForagerAffiliation[]{null!,new(){Id=31,Share=-0d},new(){Id=92,Share=.123456789}};
            throw new Exception("Uncovered transport field: "+type);
        }
        for(int variant=0;variant<2;variant++)
        {
            var cohort=new ForagerCohort();var culture=new ForagerCulture();
            foreach(var item in new object[]{cohort,culture})foreach(var field in item.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))field.SetValue(item,Value(field.FieldType,variant));
            Check(Fingerprint(cohort)==Fingerprint(PackedForagerRecords.UnpackCohort(PackedForagerRecords.Pack(cohort))),"Every cohort field preserves raw bits and nullability");
            Check(Fingerprint(culture)==Fingerprint(PackedForagerRecords.UnpackCulture(PackedForagerRecords.Pack(culture))),"Every culture field preserves raw bits and UTF16 code units");
        }
        var valid=PackedForagerRecords.Pack(new ForagerCohort());
        foreach(int length in new[]{0,1,7,8,15,valid.Length-1})
        {
            bool rejected=false;try{PackedForagerRecords.UnpackCohort(valid.Take(length).ToArray());}catch(Exception e)when(e is IOException||e is System.Runtime.Serialization.SerializationException){rejected=true;}
            Check(rejected,"Truncated record rejected before unbounded allocation");
        }
        var corrupt=(byte[])valid.Clone();corrupt[0]^=1;bool invalid=false;try{PackedForagerRecords.UnpackCohort(corrupt);}catch(System.Runtime.Serialization.SerializationException){invalid=true;}
        Check(invalid,"Wrong binary record schema rejected");
        corrupt=(byte[])valid.Clone();BitConverter.GetBytes(int.MaxValue).CopyTo(corrupt,8);invalid=false;try{PackedForagerRecords.UnpackCohort(corrupt);}catch(System.Runtime.Serialization.SerializationException){invalid=true;}
        Check(invalid,"Unbounded numeric collection request rejected before allocation");
        invalid=false;try{PackedForagerRecords.Pack(new ForagerCohort(),new CancellationToken(true));}catch(OperationCanceledException){invalid=true;}Check(invalid,"Writing respects cancellation");
        invalid=false;try{PackedForagerRecords.UnpackCohort(valid,new CancellationToken(true));}catch(OperationCanceledException){invalid=true;}Check(invalid,"Reading respects cancellation");
        Console.WriteLine($"PASS {checks} binary forager record edge checks");return checks;
    }
}


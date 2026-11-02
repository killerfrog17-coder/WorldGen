using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    [DataContract] public sealed class PackedForagerCohort
    {
        [DataMember(Order=0)] public byte[] Data;
    }
    [DataContract] public sealed class PackedForagerCulture
    {
        [DataMember(Order=0)] public byte[] Data;
    }
    // Version3 replaces the very numerous XML field records with checked binary
    // records. This is transport only: every field and every floating-point bit
    // survives. Legacy versions1/2 keep their original surrogate layout.
    public static partial class PackedForagerRecords
    {
        const int MaximumRecordBytes=16*1024*1024;
        static void CheckSchema(Type type,params string[] expected)
        {
            var fields=type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal).Select(f=>f.Name+":"+f.FieldType.FullName);
            if(!fields.SequenceEqual(expected))throw new SerializationException("Forager save schema changed; version and regenerate the record codec before writing saves: "+type.Name);
        }
        static void CheckLength(long length)
        {
            if(length<8||length>MaximumRecordBytes)throw new SerializationException("Invalid forager record length.");
        }
        static int ReadCount(BinaryReader reader,int minimumWidth)
        {
            int count=reader.ReadInt32();
            if(count < -1||count>MaximumRecordBytes/minimumWidth||count>=0&&(long)count*minimumWidth>reader.BaseStream.Length-reader.BaseStream.Position)
                throw new SerializationException("Invalid forager collection length.");
            return count;
        }
        static void WriteDoubles(BinaryWriter writer,double[] values,CancellationToken token)
        {
            if(values==null){writer.Write(-1);return;}
            var packed=PackedDoubleVector.Pack(values,token);writer.Write(packed.Data.Length);writer.Write(packed.Scale);writer.Write(packed.Data);
        }
        static double[] ReadDoubles(BinaryReader reader,CancellationToken token)
        {
            int bytes=ReadCount(reader,1);if(bytes<0)return null;
            int scale=reader.ReadInt32();if(bytes>reader.BaseStream.Length-reader.BaseStream.Position)throw new SerializationException("Truncated numeric record.");
            return new PackedDoubleVector{Scale=scale,Data=reader.ReadBytes(bytes)}.Unpack(token);
        }
        static void WriteIntegers(BinaryWriter writer,int[] values,CancellationToken token)
        {
            writer.Write(values?.Length??-1);if(values==null)return;
            for(int i=0;i<values.Length;i++){if((i&8191)==0)token.ThrowIfCancellationRequested();writer.Write(values[i]);}
        }
        static int[] ReadIntegers(BinaryReader reader,CancellationToken token)
        {
            int count=ReadCount(reader,4);if(count<0)return null;var values=new int[count];
            for(int i=0;i<count;i++){if((i&8191)==0)token.ThrowIfCancellationRequested();values[i]=reader.ReadInt32();}return values;
        }
        static void WriteAffiliations(BinaryWriter writer,ForagerAffiliation[] values,CancellationToken token)
        {
            writer.Write(values?.Length??-1);if(values==null)return;
            for(int i=0;i<values.Length;i++)
            {
                if((i&8191)==0)token.ThrowIfCancellationRequested();var value=values[i];writer.Write(value!=null);
                if(value!=null){writer.Write(value.Id);writer.Write(value.Share);}
            }
        }
        static ForagerAffiliation[] ReadAffiliations(BinaryReader reader,CancellationToken token)
        {
            int count=ReadCount(reader,1);if(count<0)return null;var values=new ForagerAffiliation[count];
            for(int i=0;i<count;i++){if((i&8191)==0)token.ThrowIfCancellationRequested();if(reader.ReadBoolean())values[i]=new ForagerAffiliation{Id=reader.ReadInt32(),Share=reader.ReadDouble()};}return values;
        }
        static void WriteString(BinaryWriter writer,string value,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();if(value==null){writer.Write(-1);return;}
            // UTF16 code units preserve even isolated surrogate values exactly.
            writer.Write(value.Length);foreach(char c in value)writer.Write((ushort)c);
        }
        static string ReadString(BinaryReader reader,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();int count=ReadCount(reader,2);if(count<0)return null;
            var chars=new char[count];for(int i=0;i<count;i++)chars[i]=(char)reader.ReadUInt16();return new string(chars);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Buffers;
using System.Collections.Generic;
namespace WorldGen.Serialization
{
    // Same-machine ephemeral protocol, not a save format. Compile typed field
    // access once; no per-value reflection, JSON, compression or decimal conversion.
    public static class RegionalWire
    {
        public const int Version=2;
        [ThreadStatic] static MemoryStream packetBuffer;
        [ThreadStatic] static byte[] numericBuffer;
        public static void Write<T>(BinaryWriter writer,T value)=>Codec<T>.Put(writer,value);
        public static T Read<T>(BinaryReader reader)=>Codec<T>.Get(reader);
        public static void WritePacket<T>(BinaryWriter writer,T value)
        {
            var memory=packetBuffer??(packetBuffer=new MemoryStream());memory.SetLength(0);memory.Position=0;
            using(var payload=new BinaryWriter(memory,System.Text.Encoding.UTF8,true)){Write(payload,value);payload.Flush();writer.Write(checked((int)memory.Length));writer.Write(memory.GetBuffer(),0,(int)memory.Length);writer.Flush();}
        }
        public static T ReadPacket<T>(BinaryReader reader)=>ReadPacket<T>(reader,out _,out _,out _);
        public static T ReadPacket<T>(BinaryReader reader,out int bytesRead,out double transferMs,out double decodeMs,Action received=null)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            int length=reader.ReadInt32();if(length<0||length>256*1024*1024)throw new InvalidDataException("Invalid regional packet length.");var bytes=ArrayPool<byte>.Shared.Rent(Math.Max(1,length));
            try
            {
                int read=0;while(read<length){int count=reader.Read(bytes,read,length-read);if(count<=0)throw new EndOfStreamException();read+=count;}
                bytesRead=length;transferMs=timer.Elapsed.TotalMilliseconds;received?.Invoke();timer.Restart();
                using(var memory=new MemoryStream(bytes,0,length,false,true))using(var payload=new BinaryReader(memory)){var value=Read<T>(payload);decodeMs=timer.Elapsed.TotalMilliseconds;return value;}
            }
            finally{ArrayPool<byte>.Shared.Return(bytes);}
        }
        static readonly MethodInfo WriteGeneric=typeof(RegionalWire).GetMethod(nameof(Write)),ReadGeneric=typeof(RegionalWire).GetMethod(nameof(Read));
        static class Codec<T>
        {
            public static readonly Action<BinaryWriter,T> Put=Writer();
            public static readonly Func<BinaryReader,T> Get=Reader();
            static FieldInfo[] Fields=>typeof(T).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>!f.IsNotSerialized).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray();
            static Expression PutValue(Expression writer,Expression value,Type t)=>Expression.Call(WriteGeneric.MakeGenericMethod(t),writer,value);
            static Expression GetValue(Expression reader,Type t)=>Expression.Call(ReadGeneric.MakeGenericMethod(t),reader);
            static Action<BinaryWriter,T> Writer()
            {
                var t=typeof(T);var w=Expression.Parameter(typeof(BinaryWriter));var v=Expression.Parameter(t);Expression body;
                if(t.IsEnum)body=PutValue(w,Expression.Convert(v,Enum.GetUnderlyingType(t)),Enum.GetUnderlyingType(t));
                else if(t.IsArray)body=Expression.Call(typeof(RegionalWire).GetMethod(nameof(WriteArray),BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(t.GetElementType()),w,v);
                else if(t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(List<>))body=Expression.Call(typeof(RegionalWire).GetMethod(nameof(WriteList),BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(t.GenericTypeArguments[0]),w,v);
                else if(t==typeof(string))body=Expression.Call(typeof(RegionalWire).GetMethod(nameof(WriteString),BindingFlags.Static|BindingFlags.NonPublic),w,v);
                else if(t.IsPrimitive)body=Expression.Call(w,typeof(BinaryWriter).GetMethod("Write",new[]{t}),v);
                else
                {
                    body=Expression.Block(Fields.Select(f=>PutValue(w,Expression.Field(v,f),f.FieldType)));
                    if(!t.IsValueType)body=Expression.Block(Expression.Call(w,typeof(BinaryWriter).GetMethod("Write",new[]{typeof(bool)}),Expression.NotEqual(v,Expression.Constant(null,t))),Expression.IfThen(Expression.NotEqual(v,Expression.Constant(null,t)),body));
                }
                return Expression.Lambda<Action<BinaryWriter,T>>(body,w,v).Compile();
            }
            static Func<BinaryReader,T> Reader()
            {
                var t=typeof(T);var r=Expression.Parameter(typeof(BinaryReader));Expression body;
                if(t.IsEnum)body=Expression.Convert(GetValue(r,Enum.GetUnderlyingType(t)),t);
                else if(t.IsArray)body=Expression.Call(typeof(RegionalWire).GetMethod(nameof(ReadArray),BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(t.GetElementType()),r);
                else if(t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(List<>))body=Expression.Call(typeof(RegionalWire).GetMethod(nameof(ReadList),BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(t.GenericTypeArguments[0]),r);
                else if(t==typeof(string))body=Expression.Call(typeof(RegionalWire).GetMethod(nameof(ReadString),BindingFlags.Static|BindingFlags.NonPublic),r);
                else if(t.IsPrimitive)body=Expression.Call(r,typeof(BinaryReader).GetMethod("Read"+t.Name));
                else
                {
                    var v=Expression.Variable(t);body=Expression.Block(new[]{v},new Expression[]{Expression.Assign(v,Expression.New(t))}.Concat(Fields.Select(f=>(Expression)Expression.Assign(Expression.Field(v,f),GetValue(r,f.FieldType)))).Concat(new Expression[]{v}));
                    if(!t.IsValueType)body=Expression.Condition(Expression.Call(r,typeof(BinaryReader).GetMethod("ReadBoolean")),body,Expression.Constant(null,t));
                }
                return Expression.Lambda<Func<BinaryReader,T>>(body,r).Compile();
            }
        }
        static void WriteString(BinaryWriter w,string value){w.Write(value!=null);if(value!=null)w.Write(value);}
        static string ReadString(BinaryReader r)=>r.ReadBoolean()?r.ReadString():null;
        static void WriteList<T>(BinaryWriter w,List<T> values){w.Write(values?.Count??-1);if(values!=null)foreach(var value in values)Write(w,value);}
        static List<T> ReadList<T>(BinaryReader r){int count=r.ReadInt32();if(count==-1)return null;if(count<0||count>16000000)throw new InvalidDataException("Invalid wire list size.");var values=new List<T>(count);for(int i=0;i<count;i++)values.Add(Read<T>(r));return values;}
        static bool Block(Type t)=>t==typeof(double)||t==typeof(float)||t==typeof(int)||t==typeof(long)||t==typeof(byte)||t==typeof(bool);
        static void WriteArray<T>(BinaryWriter w,T[] values)
        {
            w.Write(values?.Length??-1);if(values==null)return;
            if(Block(typeof(T))){int length=Buffer.ByteLength(values);if(numericBuffer==null||numericBuffer.Length<length)numericBuffer=new byte[Math.Max(256,length)];Buffer.BlockCopy(values,0,numericBuffer,0,length);w.Write(numericBuffer,0,length);}
            else foreach(var value in values)Write(w,value);
        }
        static T[] ReadArray<T>(BinaryReader r)
        {
            int count=r.ReadInt32();if(count==-1)return null;if(count<0||count>16000000)throw new InvalidDataException("Invalid regional array size.");var values=new T[count];
            if(Block(typeof(T)))
            {
                int length=Buffer.ByteLength(values);
                if(r.BaseStream is MemoryStream memory&&memory.TryGetBuffer(out var segment)){long at=memory.Position;if(at+length>memory.Length)throw new EndOfStreamException();Buffer.BlockCopy(segment.Array,segment.Offset+(int)at,values,0,length);memory.Position=at+length;}
                else{var bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();Buffer.BlockCopy(bytes,0,values,0,length);}
            }
            else for(int i=0;i<count;i++)values[i]=Read<T>(r);return values;
        }
    }
}

using System;
using System.Buffers.Binary;
using System.Runtime.Serialization;
using System.Threading;
using WorldGen.Core;
namespace WorldGen.Serialization
{
    // Lossless transport only: simulation values, hashes and object fields are
    // unchanged. Quantised vectors use integers only when every IEEE64 value
    // reconstructs bit for bit; all other vectors retain their original bits.
    [DataContract]
    public sealed class PackedDoubleVector
    {
        [DataMember(Order=0)] public int Scale;
        [DataMember(Order=1)] public byte[] Data;
        static readonly int[] Scales={1000000000,1000000,1000,1};
        public static PackedDoubleVector Pack(double[] values,CancellationToken token=default)
        {
            if(values==null)return null;
            int selected=0;
            foreach(int scale in Scales)
            {
                bool exact=true;
                for(int i=0;i<values.Length;i++)
                {
                    if((i&8191)==0)token.ThrowIfCancellationRequested();
                    double n=Math.Round(values[i]*scale,MidpointRounding.AwayFromZero);
                    if(!double.IsFinite(n)||n<int.MinValue||n>int.MaxValue||BitConverter.DoubleToInt64Bits(values[i])!=BitConverter.DoubleToInt64Bits(n/scale)){exact=false;break;}
                }
                if(exact){selected=scale;break;}
            }
            int width=selected==0?8:4;var data=new byte[checked(values.Length*width)];
            for(int i=0;i<values.Length;i++)
            {
                if((i&8191)==0)token.ThrowIfCancellationRequested();
                if(selected==0)BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(data,i*8,8),BitConverter.DoubleToInt64Bits(values[i]));
                else BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(data,i*4,4),(int)Math.Round(values[i]*selected,MidpointRounding.AwayFromZero));
            }
            return new PackedDoubleVector{Scale=selected,Data=data};
        }
        public double[] Unpack(CancellationToken token=default)
        {
            if(Data==null||Scale!=0&&Array.IndexOf(Scales,Scale)<0)throw new SerializationException("Invalid numeric vector encoding.");
            int width=Scale==0?8:4;
            if(Data.Length%width!=0||Data.Length/width>80000000)throw new SerializationException("Invalid numeric vector size.");
            var result=new double[Data.Length/width];
            for(int i=0;i<result.Length;i++)
            {
                if((i&8191)==0)token.ThrowIfCancellationRequested();
                result[i]=Scale==0?BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(Data,i*8,8))):BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(Data,i*4,4))/(double)Scale;
            }
            return result;
        }
    }
    sealed class BinaryWorldTransport:ISerializationSurrogateProvider,IDisposable
    {
        readonly HistoricalCellTree.BinaryTransportScope trees;
        readonly bool packed;readonly CancellationToken cancellation;
        public BinaryWorldTransport(CancellationToken token,bool pack){cancellation=token;packed=pack;trees=HistoricalCellTree.CreateBinaryTransport(token);}
        public Type GetSurrogateType(Type type)=>packed&&type==typeof(double[])?typeof(PackedDoubleVector):trees.GetSurrogateType(type);
        public object GetObjectToSerialize(object value,Type targetType)=>packed&&value is double[] values?PackedDoubleVector.Pack(values,cancellation):trees.GetObjectToSerialize(value,targetType);
        public object GetDeserializedObject(object value,Type targetType)=>packed&&value is PackedDoubleVector vector&&targetType==typeof(double[])?vector.Unpack(cancellation):trees.GetDeserializedObject(value,targetType);
        public void Dispose()=>trees.Dispose();
    }
}

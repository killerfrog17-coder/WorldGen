using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Xml;
namespace WorldGen.Core
{
    // Explicit native/WASM runtime probe only; never called by simulation.
    public static class HistoricalTreeTransportDiagnostics
    {
        public static string Verify()
        {
            int checks=0;void Check(bool ok){if(!ok)throw new SerializationException("Immutable tree transport probe failed.");checks++;}
            var first=new HistoricalCellTree(4096);for(int i=0;i<48;i++)first=first.With(i*71,i);
            var trees=new[]{first,first.With(4095,79)};string a=trees[0].Fingerprint(true),b=trees[1].Fingerprint(true);byte[] bytes;
            using(var stream=new MemoryStream())
            {
                using(var transport=HistoricalCellTree.CreateBinaryTransport())
                using(var writer=XmlDictionaryWriter.CreateBinaryWriter(stream,null,null,false))
                {var serializer=new DataContractSerializer(typeof(HistoricalCellTree[]));serializer.SetSerializationSurrogateProvider(transport);serializer.WriteObject(writer,trees);}
                bytes=stream.ToArray();
            }
            HistoricalCellTree[] Read(bool compact)
            {
                using(var scope=HistoricalCellTree.BeginReadInterning())
                using(var transport=HistoricalCellTree.CreateBinaryTransport())
                using(var reader=XmlDictionaryReader.CreateBinaryReader(bytes,XmlDictionaryReaderQuotas.Max))
                {var serializer=new DataContractSerializer(typeof(HistoricalCellTree[]));if(compact)serializer.SetSerializationSurrogateProvider(transport);return (HistoricalCellTree[])serializer.ReadObject(reader);}
            }
            foreach(bool compact in new[]{false,true})
            {var copy=Read(compact);Check(copy.Length==2);Check(copy[0].Fingerprint(true)==a);Check(copy[1].Fingerprint(true)==b);Check(copy[0].With(0,99)[0]==99&&copy[0][0]==0);}
            using(var stream=new MemoryStream())
            {var serializer=new DataContractJsonSerializer(typeof(HistoricalCellTree));serializer.WriteObject(stream,first);stream.Position=0;var copy=(HistoricalCellTree)serializer.ReadObject(stream);Check(copy.Fingerprint(true)==a);}
            Check(first.Fingerprint(true)==a);Check(trees[1].Fingerprint(true)==b);
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();bool stopped=false;try{using(var scope=HistoricalCellTree.BeginReadInterning(cancel.Token)){} }catch(OperationCanceledException){stopped=true;}Check(stopped);}
            return "PASS "+checks+" immutable-tree binary/legacy/JSON/cancellation vectors";
        }
    }
}

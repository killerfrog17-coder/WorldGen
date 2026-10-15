using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;

namespace WorldGen.Core
{
    // Immutable, persistent spatial storage. Five fixed radix levels cover
    // 33,554,432 entries. A claim copies 5 x 32 slots, never a country's cells.
    // No writable page is exposed to publications or callers. Serialization
    // stores actual values; integrity validation rebuilds the Merkle hashes.
    [DataContract] public sealed class HistoricalCellTree
    {
        [DataContract] sealed class Node
        {
            [DataMember] internal Node[] Children;
            [DataMember] internal int[] Values;
            [IgnoreDataMember] internal byte[] Digest;
        }
        [DataMember] Node root;
        [DataMember] int length;
        [IgnoreDataMember] string fingerprint;
        [IgnoreDataMember] byte[] canonicalFingerprint;
        [IgnoreDataMember] string canonicalFingerprintHash;
        internal void WriteCanonicalFingerprint(BinaryWriter target)
        {
            string hash=Fingerprint();
            if(canonicalFingerprint==null||!ReferenceEquals(hash,canonicalFingerprintHash))
            {
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {writer.Write("WorldGen.Core.HistoricalCellTree");writer.Write(hash);canonicalFingerprint=stream.ToArray();canonicalFingerprintHash=hash;}
            }
            target.Write(canonicalFingerprint);
        }
        public int Length=>length;
        // Read-only bulk export for rendering/diagnostics, not actor turns.
        // Leaf copies avoid a five-level lookup for each displayed value.
        public void CopyTo(int[] destination,int offset=0)
        {
            if(destination==null||offset<0||offset>destination.Length-length)throw new ArgumentOutOfRangeException(nameof(destination));
            for(int i=0;i<length;i++)destination[offset+i]=-1;
            CopyValues(root,0,20,destination,offset);
        }
        void CopyValues(Node node,int first,int shift,int[] destination,int offset)
        {
            if(node==null||first>=length)return;
            if(shift==0){Array.Copy(node.Values,0,destination,offset+first,Math.Min(32,length-first));return;}
            for(int i=0;i<32;i++){int start=first+(i<<shift);if(start>=length)break;CopyValues(node.Children[i],start,shift-5,destination,offset);}
        }
        public int this[int index] { get { if((uint)index>=(uint)length)throw new ArgumentOutOfRangeException(nameof(index));var node=root;for(int shift=20;shift>0;shift-=5){node=node?.Children[(index>>shift)&31];if(node==null)return -1;}return node?.Values[index&31]??-1; } }
        public HistoricalCellTree(int length){if(length<0||length>33554432)throw new ArgumentOutOfRangeException(nameof(length));this.length=length;}
        HistoricalCellTree(int length,Node root){this.length=length;this.root=root;}
        internal HistoricalCellTree Grow(int capacity){if(capacity<length||capacity>33554432)throw new ArgumentOutOfRangeException(nameof(capacity));return new HistoricalCellTree(capacity,root);}
        public HistoricalCellTree With(int index,int value)
        {if((uint)index>=(uint)length)throw new ArgumentOutOfRangeException(nameof(index));if(this[index]==value)return this;return new HistoricalCellTree(length,Set(root,index,20,value));}
        static Node Set(Node old,int index,int shift,int value)
        {
            if(shift==0){var values=old?.Values==null?EmptyValues():(int[])old.Values.Clone();values[index&31]=value;for(int i=0;i<32;i++)if(values[i]!=-1)return new Node{Values=values};return null;}
            var children=old==null?new Node[32]:(Node[])old.Children.Clone();int at=(index>>shift)&31;children[at]=Set(children[at],index,shift-5,value);for(int i=0;i<32;i++)if(children[i]!=null)return new Node{Children=children};return null;
        }
        static int[] EmptyValues(){var values=new int[32];for(int i=0;i<32;i++)values[i]=-1;return values;}
        public static HistoricalCellTree From(int length,Func<int,int> read)
        {if(read==null)throw new ArgumentNullException(nameof(read));var result=new HistoricalCellTree(length);result.root=Build(0,20,length,read);return result;}
        static Node Build(int first,int shift,int length,Func<int,int> read)
        {
            if(first>=length)return null;
            if(shift==0){var values=EmptyValues();bool any=false;for(int i=0;i<32&&first+i<length;i++){values[i]=read(first+i);any|=values[i]!=-1;}return any?new Node{Values=values}:null;}
            var children=new Node[32];bool present=false;for(int i=0;i<32;i++){children[i]=Build(first+(i<<shift),shift-5,length,read);present|=children[i]!=null;}return present?new Node{Children=children}:null;
        }
        public string Fingerprint(bool verify=false)
        {
            if(!verify&&fingerprint!=null)return fingerprint;
            // The tree and its length are immutable. Trusted publication can
            // reuse their digest; explicit integrity verification still walks
            // every actual node and recomputes the result, including old saves.
            using(var sha=CanonicalSha256.Create())using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {writer.Write(length);writer.Write(Digest(root,sha,verify));return fingerprint=Hex(sha.ComputeHash(stream.ToArray()));}
        }
        static readonly byte[] Absent=new byte[32];
        static byte[] Digest(Node node,SHA256 sha,bool verify)
        {
            if(node==null)return Absent;if(!verify&&node.Digest!=null)return node.Digest;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(node.Values!=null);
                if(node.Values!=null){if(node.Values.Length!=32||node.Children!=null)throw new SerializationException("Invalid spatial leaf.");foreach(int value in node.Values)writer.Write(value);}
                else {if(node.Children?.Length!=32)throw new SerializationException("Invalid spatial branch.");foreach(var child in node.Children)writer.Write(Digest(child,sha,verify));}
                return node.Digest=sha.ComputeHash(stream.ToArray());
            }
        }
        static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
    }
    public static class HistoricalCells
    {
        public static int Count(PrehistoryState s)=>s?.History?.Aggregates?.Cells.Length/5??s?.History?.CellPolity.Length??s?.Society?.CellCulture.Length??0;
        public static int Culture(PrehistoryState s,int cell)=>Read(s,cell,0);
        public static int Polity(PrehistoryState s,int cell){int owner=Read(s,cell,1);return s?.History?.Aggregates is HistoricalAggregateState a?a.Resolve(owner,s.History):owner;}
        public static int Religion(PrehistoryState s,int cell)=>Read(s,cell,2);
        public static int Market(PrehistoryState s,int cell)=>Read(s,cell,3);
        public static int Stage(PrehistoryState s,int cell)=>Read(s,cell,4);
        static int Read(PrehistoryState s,int cell,int layer)
        {
            var a=s?.History?.Aggregates;if(a!=null)return cell>=0&&cell<a.Cells.Length/5?a.Cells[cell*5+layer]:-1;
            var values=layer==0?s?.Society?.CellCulture:layer==1?s?.History?.CellPolity:layer==2?s?.History?.CellReligion:layer==3?s?.History?.CellMarket:s?.History?.CellStage;
            return values!=null&&cell>=0&&cell<values.Length?values[cell]:-1;
        }
    }
}

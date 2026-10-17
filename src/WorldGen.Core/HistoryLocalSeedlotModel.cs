using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;

namespace WorldGen.Core
{
    // A saved cultivar snapshot, not a mutable reference to a culture's seed.
    [DataContract] public sealed class HistoricalLocalSeedlot
    {
        [DataMember] HistoricalCultivar cultivar;
        [DataMember] int acquiredYear,sourceTown,sourceCell,practiceYears,lastPracticeYear;
        public int AcquiredYear=>acquiredYear;
        public int SourceTown=>sourceTown;
        public int SourceCell=>sourceCell;
        public int PracticeYears=>practiceYears;
        public int LastPracticeYear=>lastPracticeYear;
        public HistoricalCultivar Cultivar=>cultivar?.Copy();
        internal HistoricalCultivar Profile=>cultivar;
        public HistoricalLocalSeedlot(HistoricalCultivar profile,int acquired,int town,int cell,int practice,int last)
        {cultivar=profile?.Copy()??throw new ArgumentNullException(nameof(profile));acquiredYear=acquired;sourceTown=town;sourceCell=cell;practiceYears=practice;lastPracticeYear=last;}
        internal void Write(BinaryWriter w)
        {
            w.Write(acquiredYear);w.Write(sourceTown);w.Write(sourceCell);w.Write(practiceYears);w.Write(lastPracticeYear);
            w.Write(cultivar.CropId);w.Write(cultivar.OriginCulture);w.Write(cultivar.ParentCulture);w.Write(cultivar.SelectedYears);w.Write(cultivar.MaturityDays);
            w.Write(cultivar.DaylengthLatitudeTenths);w.Write(cultivar.WaterRequirementPermille);w.Write(cultivar.YieldPermille);w.Write(cultivar.MinimumWarmTenthsC);
        }
    }
    [DataContract] public sealed class HistoricalTownSeeds
    {
        [DataMember] int townId,fieldCell;
        [DataMember] HistoricalLocalSeedlot[] lots;
        public int TownId=>townId;
        public int FieldCell=>fieldCell;
        public HistoricalLocalSeedlot this[int group]=>lots[group];
        public HistoricalTownSeeds(int town,int cell,HistoricalLocalSeedlot[] seeds)
        {if(seeds?.Length!=6)throw new ArgumentException("Six seed groups required.");townId=town;fieldCell=cell;lots=(HistoricalLocalSeedlot[])seeds.Clone();}
        internal HistoricalTownSeeds With(int group,HistoricalLocalSeedlot seed)
        {var next=(HistoricalLocalSeedlot[])lots.Clone();next[group]=seed;return new HistoricalTownSeeds(townId,fieldCell,next);}
        internal bool ValidShape=>lots?.Length==6;
        internal byte[] Digest(SHA256 sha)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {writer.Write(townId);writer.Write(fieldCell);for(int k=0;k<6;k++){writer.Write(lots[k]!=null);lots[k]?.Write(writer);}return sha.ComputeHash(stream.ToArray());}
        }
    }
    // Immutable record-valued radix storage. Seven levels cover every positive
    // int town identity, including sparse historical IDs beyond terrain size.
    [DataContract] public sealed class HistoricalTownSeedTree
    {
        [DataContract] sealed class Node
        {
            [DataMember] internal Node[] Children;
            [DataMember] internal HistoricalTownSeeds[] Values;
            [IgnoreDataMember] internal byte[] Digest;
        }
        [DataMember] Node root;
        [IgnoreDataMember] string fingerprint;
        public HistoricalTownSeedTree(){}
        HistoricalTownSeedTree(Node value){root=value;}
        public HistoricalTownSeeds this[int town]
        {
            get{if(town<0)return null;var node=root;for(int shift=30;shift>0;shift-=5){node=node?.Children[(town>>shift)&31];if(node==null)return null;}return node?.Values[town&31];}
        }
        public HistoricalTownSeedTree With(int town,HistoricalTownSeeds seeds)
        {if(town<0||seeds!=null&&seeds.TownId!=town)throw new ArgumentException("Invalid seed holder.");return new HistoricalTownSeedTree(Set(root,town,30,seeds));}
        static Node Set(Node old,int key,int shift,HistoricalTownSeeds value)
        {
            if(shift==0){var values=old==null?new HistoricalTownSeeds[32]:(HistoricalTownSeeds[])old.Values.Clone();values[key&31]=value;for(int k=0;k<32;k++)if(values[k]!=null)return new Node{Values=values};return null;}
            var children=old==null?new Node[32]:(Node[])old.Children.Clone();int at=(key>>shift)&31;children[at]=Set(children[at],key,shift-5,value);for(int k=0;k<32;k++)if(children[k]!=null)return new Node{Children=children};return null;
        }
        internal bool AllPresentValues(Func<int,HistoricalTownSeeds,bool> predicate)=>Present(root,0,30,predicate);
        static bool Present(Node node,long first,int shift,Func<int,HistoricalTownSeeds,bool> predicate)
        {
            if(node==null)return true;if(first>int.MaxValue)return false;
            if(shift==0){if(node.Values?.Length!=32||node.Children!=null)return false;for(int k=0;k<32;k++)if(node.Values[k]!=null&&(first+k>int.MaxValue||!predicate((int)(first+k),node.Values[k])))return false;return true;}
            if(node.Children?.Length!=32||node.Values!=null)return false;for(int k=0;k<32;k++)if(!Present(node.Children[k],first+((long)k<<shift),shift-5,predicate))return false;return true;
        }
        internal void WriteCanonicalFingerprint(BinaryWriter writer){writer.Write("WorldGen.Core.HistoricalTownSeedTree");writer.Write(Fingerprint());}
        public string Fingerprint(bool verify=false)
        {if(!verify&&fingerprint!=null)return fingerprint;using(var sha=CanonicalSha256.Create())return fingerprint=BitConverter.ToString(Digest(root,sha,verify,0)).Replace("-","").ToLowerInvariant();}
        static readonly byte[] Absent=new byte[32];
        static byte[] Digest(Node node,SHA256 sha,bool verify,int depth)
        {
            if(node==null)return Absent;if(!verify&&node.Digest!=null)return node.Digest;
            if(depth>6||depth==6&&(node.Values?.Length!=32||node.Children!=null)||depth<6&&(node.Children?.Length!=32||node.Values!=null))throw new SerializationException("Invalid town-seed radix shape.");
            var bytes=new byte[1025];bytes[0]=(byte)(depth==6?1:0);
            for(int k=0;k<32;k++){var hash=depth==6?(node.Values[k]?.Digest(sha)??Absent):Digest(node.Children[k],sha,verify,depth+1);Buffer.BlockCopy(hash,0,bytes,1+32*k,32);}
            return node.Digest=sha.ComputeHash(bytes);
        }
    }
    [Serializable] public sealed class HistoricalLocalSeedlots
    {
        public int Version=1,SinceYear;
        public HistoricalTownSeedTree Towns=new HistoricalTownSeedTree();
        public HistoricalLocalSeedlots Copy()=>new HistoricalLocalSeedlots{Version=Version,SinceYear=SinceYear,Towns=Towns};
    }
}

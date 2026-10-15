using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalColonialCoasts
    {
        public HistoricalLocalBag[] Governments=Array.Empty<HistoricalLocalBag>();
        public HistoricalColonialCoasts Copy()=>new HistoricalColonialCoasts{Governments=Governments.Select(x=>x.Copy()).ToArray()};
    }
    public sealed partial class Prehistory
    {
        public const string CoastalHistoryVersion="history-27";
        public PrehistoryState UpgradeToCoastalHistory(PrehistoryState source)
        {Validate(source);if(source.History?.Version==CoastalHistoryVersion||RegionalAmbitionHistory(source))return source;source=UpgradeToColonialHistory(source);var s=source.Copy();s.History.Version=CoastalHistoryVersion;PrepareColonialCoasts(s);s.Seal();return s;}
        void PrepareColonialCoasts(PrehistoryState s)
        {
            var a=s.History.Aggregates;var c=a.Empires.Connections;c.Coasts=new HistoricalColonialCoasts{Governments=s.History.Polities.Select(_=>new HistoricalLocalBag()).ToArray()};
            // Explicit initialization only. Ordinary claims update one bag entry.
            for(int cell=0;cell<CellPatch.Length;cell++){int p=HistoricalCells.Polity(s,cell);if(p>=0&&CellPatch[cell]>=0&&DiffusionShore(cell))c.Coasts.Governments[p].Add(cell);}
        }
        void AppendColonialCoast(PrehistoryState s,int predecessor)
        {var c=s.History.Aggregates.Empires.Connections.Coasts;if(c==null)return;c.Governments=c.Governments.Concat(new[]{predecessor<0?new HistoricalLocalBag():c.Governments[predecessor].Copy()}).ToArray();if(predecessor>=0)c.Governments[predecessor]=new HistoricalLocalBag();}
        void ChangeColonialCoast(PrehistoryState s,int cell,int old,int owner)
        {var c=s.History.Aggregates.Empires?.Connections?.Coasts;if(c==null||CellPatch[cell]<0||!DiffusionShore(cell))return;if(old>=0)c.Governments[old].Remove(cell);if(owner>=0)c.Governments[owner].Add(cell);}
        bool ColonialCoastalPractice(PrehistoryState s,int culture,int court)
        {var c=s.History.Aggregates.Empires?.Connections?.Coasts;if(c==null)return false;court=s.History.Aggregates.Resolve(court,s.History);return court>=0&&s.History.Polities[court].CultureId==culture&&c.Governments[court].Count>0;}
        int ColonialProvincialHarbor(PrehistoryState s,int polity)
        {
            var c=s.History.Aggregates.Empires.Connections.Coasts;if(c==null)return -1;var bag=c.Governments[polity];if(bag.Count==0)return -1;
            int offset=(int)(HistoryRoll("colonial-provincial-harbor",polity,s.Years)*bag.Count);
            for(int k=0;k<Math.Min(6,bag.Count);k++){int cell=bag.At((offset+k)%bag.Count),source=s.History.Aggregates.Empires.Territories.Sources[cell];if(HistoricalCells.Polity(s,cell)==polity&&TerritorialNodeLive(s,polity,source))return cell;}return -1;
        }
        void ValidateColonialCoasts(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires.Connections.Coasts;if(s.History.Version!=CoastalHistoryVersion&&!RegionalAmbitionHistory(s)){if(c!=null)throw new ArgumentException("Legacy coastal network extension.");return;}if(c==null)throw new ArgumentException("Missing coastal network.");
            if(c.Governments.Length!=s.History.Polities.Length)throw new ArgumentException("Coastal government alignment.");
            for(int p=0;p<c.Governments.Length;p++){var b=c.Governments[p];if(b==null||b.Count<0||b.Count>CellPatch.Length)throw new ArgumentException("Invalid coastal index.");for(int k=0;k<b.Count;k++){int cell=b.At(k);if(cell<0||cell>=CellPatch.Length||CellPatch[cell]<0||!DiffusionShore(cell)||b.Positions[cell]!=k||s.History.Polities[p].EndedYear<0&&HistoricalCells.Polity(s,cell)!=p)throw new ArgumentException("Invalid coastal control reference.");}}
            for(int cell=0;cell<CellPatch.Length;cell++){int p=HistoricalCells.Polity(s,cell);if(p>=0&&CellPatch[cell]>=0&&DiffusionShore(cell)&&c.Governments[p].Positions[cell]<0)throw new ArgumentException("Missing controlled coastline.");}
        }
    }
}

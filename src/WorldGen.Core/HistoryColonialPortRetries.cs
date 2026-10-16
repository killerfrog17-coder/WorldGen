using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalPendingPortGovernment
    {
        public HistoricalLocalBag Pending=new HistoricalLocalBag();
        public int Cursor;
        public HistoricalPendingPortGovernment Copy()=>new HistoricalPendingPortGovernment{Pending=Pending.Copy(),Cursor=Cursor};
        public int[] Take()
        {
            int count=Math.Min(HistoricalColonialPortRetryEffects.Budget,Pending.Count);var result=new int[count];
            for(int i=0;i<count;i++){result[i]=Pending.At(Cursor);Cursor=(Cursor+1)%Pending.Count;}
            return result;
        }
        public void Remove(int post){Pending.Remove(post);Cursor=Pending.Count==0?0:Cursor%Pending.Count;}
    }
    [Serializable] public sealed class HistoricalColonialPortSettlements
    {
        public HistoricalPendingPortGovernment[] Governments=Array.Empty<HistoricalPendingPortGovernment>();
        public HistoricalCellTree LastTestYear=new HistoricalCellTree(33554432);
        public HistoricalColonialPortSettlements Copy()=>new HistoricalColonialPortSettlements{Governments=Governments.Select(x=>x.Copy()).ToArray(),LastTestYear=LastTestYear};
    }
    public static class HistoricalColonialPortRetryEffects
    {
        public const int Budget=8;
        // Current resources are known only at the review, not throughout a
        // dormant office's history. Never bank centuries of presumed supply.
        public static int OpportunityYears(int now,int founded,int previous)=>Math.Max(0,Math.Min(40,now-Math.Max(founded,previous)));
    }
    public sealed partial class Prehistory
    {
        public PrehistoryState UpgradeColonialPortRetries(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Port retry upgrade requires history32.");
            if(source.History.Aggregates.Empires.Connections.PortSettlements!=null)return source;
            var s=source.Copy();InitializeColonialPortRetries(s);s.Seal();return s;
        }
        void InitializeColonialPortRetries(PrehistoryState s)
        {
            var a=s.History.Aggregates;var c=a.Empires.Connections;if(c.PortSettlements!=null)return;
            c.PortSettlements=new HistoricalColonialPortSettlements{Governments=c.Governments.Select(_=>new HistoricalPendingPortGovernment()).ToArray()};
            // Explicit setup/upgrade only. Runtime maintenance below addresses
            // one actual office, never a government's entire post portfolio.
            foreach(var post in a.Empires.Territories.Posts)TrackPendingColonialPort(s,post.Cell);
        }
        void AppendPendingPortGovernment(PrehistoryState s,int predecessor)
        {
            var retry=s.History.Aggregates.Empires.Connections.PortSettlements;if(retry==null)return;
            var next=predecessor<0?new HistoricalPendingPortGovernment():retry.Governments[predecessor].Copy();
            retry.Governments=retry.Governments.Concat(new[]{next}).ToArray();
            if(predecessor>=0)retry.Governments[predecessor]=new HistoricalPendingPortGovernment();
        }
        bool PendingColonialPort(PrehistoryState s,int polity,HistoricalAdministrativePost post)
            =>post.Maritime&&post.ClosedYear<0&&CellPatch[post.Cell]>=0&&s.History.Polities[polity].EndedYear<0&&
                s.History.Aggregates.Resolve(post.PolityId,s.History)==polity&&HistoricalCells.Polity(s,post.Cell)==polity&&s.History.Aggregates.TownAt[post.Cell]<0;
        void TrackPendingColonialPort(PrehistoryState s,int cell,bool resetOpportunity=false)
        {
            if(!LivingWorldHistory(s))return;var a=s.History.Aggregates;var retry=a.Empires.Connections.PortSettlements;if(retry==null)return;
            int id=a.Empires.Territories.PostAt[cell];if(id<0)return;var post=a.Empires.Territories.Posts[id];int owner=a.Resolve(post.PolityId,s.History);
            if(owner>=0&&PendingColonialPort(s,owner,post))
            {
                retry.Governments[owner].Pending.Add(id);
                if(resetOpportunity)retry.LastTestYear=retry.LastTestYear.With(id,s.Years);
            }
            else if(owner>=0)retry.Governments[owner].Remove(id);
        }
        void RemovePendingColonialPort(PrehistoryState s,int polity,int post)
        {
            if(!LivingWorldHistory(s))return;var retry=s.History.Aggregates.Empires.Connections.PortSettlements;
            if(retry!=null&&polity>=0)retry.Governments[polity].Remove(post);
        }
        void ValidateColonialPortRetries(PrehistoryState s)
        {
            var a=s.History.Aggregates;var retry=a.Empires.Connections.PortSettlements;if(retry==null)return;
            if(!LivingWorldHistory(s)||retry.Governments?.Length!=a.Polities.Length||retry.LastTestYear?.Length!=33554432||
                !retry.LastTestYear.AllPresentValues((post,year)=>post<a.Empires.Territories.Posts.Length&&year>=a.Empires.Territories.Posts[post].FoundedYear&&year<=s.Years))throw new ArgumentException("Invalid colonial settlement retry accounts.");
            var indexed=new bool[a.Empires.Territories.Posts.Length];
            for(int polity=0;polity<retry.Governments.Length;polity++)
            {
                var g=retry.Governments[polity];if(g?.Pending==null||g.Pending.Count<0||g.Pending.Count>indexed.Length||g.Cursor<0||g.Cursor>=Math.Max(1,g.Pending.Count))throw new ArgumentException("Invalid pending port cursor.");
                for(int k=0;k<g.Pending.Count;k++)
                {
                    int post=g.Pending.At(k);if(post<0||post>=indexed.Length||indexed[post]||g.Pending.Positions[post]!=k||!PendingColonialPort(s,polity,a.Empires.Territories.Posts[post]))throw new ArgumentException("Invalid pending port index.");
                    indexed[post]=true;
                }
            }
            foreach(var post in a.Empires.Territories.Posts)
            {int owner=a.Resolve(post.PolityId,s.History);if(owner>=0&&PendingColonialPort(s,owner,post)!=indexed[post.Id])throw new ArgumentException("Missing unsettled colonial office.");}
        }
    }
}

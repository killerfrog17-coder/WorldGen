using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalDependency
    {
        public int Id,PatronId,ClientId,StartedYear,EndedYear=-1,LastReachableYear,ArrearsYears;
        public long AnnualQuota,TotalPaid;
        public bool Imposed;
        public string Ending="";
        public HistoricalDependency Copy()=>(HistoricalDependency)MemberwiseClone();
    }
    public static class HistoricalClientEffects
    {
        public static bool AreLinked(HistoricalDependency[] records,int a,int b)=>records!=null&&records.Any(d=>d.EndedYear<0&&(d.PatronId==a&&d.ClientId==b||d.PatronId==b&&d.ClientId==a));
        public static bool WouldCycle(HistoricalDependency[] records,int patron,int client)
        {
            if(patron==client)return true;int current=patron;
            for(int i=0;i<=(records?.Length??0);i++)
            {
                var parent=records?.FirstOrDefault(d=>d.EndedYear<0&&d.ClientId==current);
                if(parent==null)return false;current=parent.PatronId;if(current==client)return true;
            }
            return true;
        }
        public static double BreakawayChance(int patronLegitimacy,int patronExhaustion,int clientAmbition,int arrearsYears,double militaryRatio,bool succession)
            =>D.Q(D.Clamp(.006+.04*D.Clamp((500-patronLegitimacy)/500.0)+.04*D.Clamp(patronExhaustion/700.0)+.025*D.Clamp(clientAmbition/1000.0)+.04*D.Clamp(arrearsYears/50.0)+.025*D.Clamp(militaryRatio)+(succession?.025:0)),1000000000);
        public static long Quota(long population)=>Math.Max(25,Math.Min(5000,Math.Max(0,population)/1500));
    }
    public sealed partial class Prehistory
    {
        bool TryHistoricalDependency(PrehistoryState s,HistoricalPolity patron,HistoricalPolity client,bool imposed)
        {
            var records=s.History.Dynamics.Dependencies;
            if(patron.Id==client.Id||patron.EndedYear>=0||client.EndedYear>=0||records.Any(d=>d.EndedYear<0&&d.ClientId==client.Id)||HistoricalClientEffects.WouldCycle(records,patron.Id,client.Id))return false;
            if(imposed&&(patron.Army<client.Army*1.4||HistoryRoll("tribute-dependency",client.Id,s.Years)>.20+.45*CivicLeader(s,patron.Id).Diplomacy/1000.0))return false;
            var relation=s.History.Relations.FirstOrDefault(r=>r.A==Math.Min(patron.Id,client.Id)&&r.B==Math.Max(patron.Id,client.Id));if(relation==null)return false;
            var dependency=new HistoricalDependency{Id=records.Length,PatronId=patron.Id,ClientId=client.Id,StartedYear=s.Years,LastReachableYear=s.Years,AnnualQuota=HistoricalClientEffects.Quota(client.Population),Imposed=imposed};
            s.History.Dynamics.Dependencies=records.Concat(new[]{dependency}).ToArray();
            // A battlefield agreement's active campaign is closed by its caller.
            if(relation.WarSince<0){relation.Trade=true;relation.Alliance=true;relation.Trust=Math.Max(35,relation.Trust);}
            HistoryEvent(s,"dependency-founded",client.Name+(imposed?" accepts tributary obligations to ":" seeks protection under ")+patron.Name+" while retaining its local ruler, communities and territory.",s.Society.Settlements[client.CapitalId].Cell,client.CultureId,client.Id,patron.Id,category:ChronicleCategory.Politics);
            return true;
        }
        bool DependencyContact(PrehistoryState s,HistoricalDependency dependency,CancellationToken token)
        {
            var towns=s.Society.Settlements;
            var patrons=towns.Where(t=>t.PolityId==dependency.PatronId&&t.Population>=100).OrderBy(t=>t.Id).ToArray();
            foreach(var client in towns.Where(t=>t.PolityId==dependency.ClientId&&t.Population>=100).OrderBy(t=>t.Id))
            foreach(var patron in patrons.OrderBy(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[client.Cell].Position)).ThenBy(t=>t.Id))
            {
                token.ThrowIfCancellationRequested();if(ReachableHistoricalContact(s,patron.Cell,client.Cell,patron.CultureId,token)||ReachableHistoricalContact(s,client.Cell,patron.Cell,client.CultureId,token))return true;
            }
            return false;
        }
        void EndDependency(PrehistoryState s,HistoricalDependency d,string reason,bool rebellion)
        {
            if(d.EndedYear>=0)return;d.EndedYear=s.Years;d.Ending=reason;
            var patron=s.History.Polities[d.PatronId];var client=s.History.Polities[d.ClientId];
            var relation=s.History.Relations.FirstOrDefault(r=>r.A==Math.Min(d.PatronId,d.ClientId)&&r.B==Math.Max(d.PatronId,d.ClientId));
            if(relation!=null){relation.Alliance=false;relation.Trade=!rebellion&&patron.EndedYear<0&&client.EndedYear<0;relation.Trust=rebellion?-65:0;}
            HistoryEvent(s,rebellion?"tributary-revolt":"dependency-ended",client.Name+" ends its obligations to "+patron.Name+". "+reason,s.Society.Settlements[client.CapitalId].Cell,client.CultureId,client.Id,patron.Id,category:ChronicleCategory.Politics);
            if(rebellion&&relation!=null&&patron.EndedYear<0&&client.EndedYear<0&&patron.Army>30&&client.Army>30)
            {relation.WarSince=s.Years;relation.Trade=false;BeginCivicCampaign(s,relation,client.Id,HistoricalWarAim.Liberation);}
        }
        void EndHistoricalDependencies(PrehistoryState s,int polity)
        {
            foreach(var d in s.History.Dynamics.Dependencies.Where(d=>d.EndedYear<0&&(d.PatronId==polity||d.ClientId==polity)))EndDependency(s,d,"One court's political institutions dissolve.",false);
        }
        void AdvanceHistoricalDependencies(PrehistoryState s,CancellationToken token)
        {
            foreach(var d in s.History.Dynamics.Dependencies.Where(d=>d.EndedYear<0).OrderBy(d=>d.Id))
            {
                token.ThrowIfCancellationRequested();var patron=s.History.Polities[d.PatronId];var client=s.History.Polities[d.ClientId];
                if(patron.EndedYear>=0||client.EndedYear>=0){EndDependency(s,d,"A court's institutions dissolve.",false);continue;}
                bool reachable=DependencyContact(s,d,token);
                if(reachable)d.LastReachableYear=s.Years;
                else if(s.Years-d.LastReachableYear>=30){EndDependency(s,d,"Sustained contact and practical protection are lost.",false);continue;}
                if(!reachable)continue;
                long due=d.AnnualQuota*10,available=client.Treasury;
                long paid=HistoricalLeadershipEffects.TransferableFunds(available,patron.Treasury,due);client.Treasury-=paid;patron.Treasury+=paid;d.TotalPaid+=paid;
                d.ArrearsYears=available<due?Math.Min(100,d.ArrearsYears+10):Math.Max(0,d.ArrearsYears-10);
                var clientLeader=CivicLeader(s,client.Id);bool succession=s.Years-patron.RulerSince<10;
                double risk=HistoricalClientEffects.BreakawayChance(patron.Legitimacy,patron.Exhaustion,clientLeader.Ambition,d.ArrearsYears,client.Army/(double)Math.Max(1,patron.Army),succession);
                if(s.Years-d.StartedYear>=40&&HistoryRoll("dependency-defiance",d.Id,s.Years)<risk)
                    EndDependency(s,d,d.ArrearsYears>=20?"Local shortages and unpaid demands undermine cooperation.":succession?"A succession opens an opportunity to contest the obligations.":"The local court judges that the balance of protection and autonomy has changed.",true);
            }
        }
    }
}

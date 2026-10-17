using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalMilitaryGovernment
    {
        public int MobilizationEpoch,Reserved;
        public long Recruits,Deaths,RecruitmentSpending,FieldPayroll,FieldProvisions;
        public HistoricalMilitaryGovernment Copy()=>(HistoricalMilitaryGovernment)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalMilitaryOperation
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        public int PartyA,PartyB,EpochA,EpochB,CommittedA,CommittedB,OriginalCapitalA,OriginalCapitalB;
        public int VictoriesA,VictoriesB,CorePressureA,CorePressureB,IdleYears;
        public int MarchDaysA,MarchDaysB,SiegeTarget=-1,SiegeDays,SiegeBesieger=-1;
        public bool Closed;
        public HistoricalMilitaryOperation Copy()=>(HistoricalMilitaryOperation)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalMilitaryOperations
    {
        public int Version=1,SinceYear;
        public HistoricalMilitaryGovernment[] Governments=Array.Empty<HistoricalMilitaryGovernment>();
        public HistoricalMilitaryOperationCollection Campaigns=new HistoricalMilitaryOperationCollection();
        public HistoricalMilitaryOperations Copy()=>new HistoricalMilitaryOperations{Version=Version,SinceYear=SinceYear,Governments=Governments.Select(x=>x.Copy()).ToArray(),Campaigns=Campaigns.Copy()};
    }
    public static class HistoricalMilitaryOperationEffects
    {
        public static int Reinforcements(int army,int ideal,int elapsed,long treasury,long food)
        {
            if(elapsed<=0||ideal<=army||treasury<=0||food<=0)return 0;
            long training=Math.Max(1,(long)Math.Ceiling(Math.Max(0,ideal)*Math.Min(.5,.08*elapsed)));
            return (int)Math.Max(0,Math.Min(ideal-(long)army,Math.Min(training,Math.Min(treasury/4,food*4))));
        }
        public static int Reserve(int army,int reserved,int requested)=>Math.Max(0,Math.Min(Math.Max(0,requested),Math.Max(0,army-reserved)));
        public static int Losses(int men,bool won,double ratio,int elapsed)
        {
            if(men<=0||elapsed<=0)return 0;
            double fraction=won?.015+.035/Math.Max(1,ratio):.06+.12*D.Clamp(1-1/Math.Max(1,ratio));
            return (int)Math.Min(men,Math.Max(1,Math.Ceiling(men*Math.Min(.45,fraction*elapsed))));
        }
        public static int MarchDays(double km,double riding,double terrain,double motorization,double rail,int military)
        {
            double speed=(12+18*D.Clamp(riding)*D.Clamp(terrain)+25*D.Clamp(motorization)+40*D.Clamp(rail))*(1+.35*D.Clamp((military-700)/250.0));
            return Math.Max(1,(int)Math.Ceiling(Math.Max(0,km)/speed));
        }
        public static int SiegeDays(double defenses,double engineering,double fieldRatio)
            =>Math.Max(14,(int)Math.Ceiling((35+500*D.Clamp(defenses))/(1+2.5*D.Clamp(engineering)+.35*Math.Sqrt(Math.Max(0,fieldRatio)))));
        public static bool Submission(bool coreHeld,int corePressure,int victories,double fieldPower,double enemyPower)
            =>fieldPower>=20&&fieldPower>enemyPower*1.4&&(coreHeld||corePressure>=350&&victories>=2);
    }
    public sealed partial class Prehistory
    {
        static bool RevisedMilitaryHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Military!=null;
        HistoricalWarAim RevisedMilitaryAim(PrehistoryState s,int attacker,int defender,int front)
        {
            var h=s.History;var p=h.Polities[attacker];var q=h.Polities[defender];var leader=h.Dynamics.Leaders[h.Dynamics.States[attacker].LeaderId];
            if(p.ParentId==defender||q.ParentId==attacker||p.ParentId>=0&&p.ParentId==q.ParentId)return HistoricalWarAim.Reunification;
            if(leader.Ambition>=800)return HistoricalWarAim.Conquest;
            if(leader.Ambition<550&&q.Treasury>Math.Max(100,p.Treasury))return HistoricalWarAim.Tribute;
            if(front>=0&&DiffusionShore(front)&&leader.Ambition<700&&HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.Navigation,s.Years)>=.2)return HistoricalWarAim.TradeRoute;
            return HistoricalWarAim.Frontier;
        }
        void InitializeMilitaryOperations(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires.Connections;if(c.Military!=null)return;
            c.Military=new HistoricalMilitaryOperations{SinceYear=s.Years,Governments=s.History.Polities.Select(_=>new HistoricalMilitaryGovernment()).ToArray(),
                Campaigns=s.History.Dynamics.Campaigns.ReadOnlyValues().Select(campaign=>NewMilitaryOperation(s,campaign)).ToArray()};
        }
        HistoricalMilitaryOperation NewMilitaryOperation(PrehistoryState s,HistoricalCampaign c)
            =>new HistoricalMilitaryOperation{PartyA=c.A,PartyB=c.B,OriginalCapitalA=s.Society.Settlements[s.History.Polities[c.A].CapitalId].Cell,OriginalCapitalB=s.Society.Settlements[s.History.Polities[c.B].CapitalId].Cell,Closed=c.EndedYear>=0};
        void AppendMilitaryGovernment(PrehistoryState s)
        {
            if(!RevisedMilitaryHistory(s))return;var m=s.History.Aggregates.Empires.Connections.Military;
            m.Governments=m.Governments.Concat(new[]{new HistoricalMilitaryGovernment()}).ToArray();
        }
        void RegisterMilitaryOperation(PrehistoryState s,int id)
        {
            if(!RevisedMilitaryHistory(s))return;var m=s.History.Aggregates.Empires.Connections.Military;var c=s.History.Dynamics.Campaigns[id];
            if(id<m.Campaigns.Length)return;
            if(id!=m.Campaigns.Length)throw new InvalidOperationException("Military operation append alignment.");
            m.Campaigns.Add(NewMilitaryOperation(s,c));
        }
        void ReleaseMilitaryGovernment(PrehistoryState s,int polity)
        {
            if(!RevisedMilitaryHistory(s))return;var g=s.History.Aggregates.Empires.Connections.Military.Governments[polity];
            g.MobilizationEpoch=checked(g.MobilizationEpoch+1);g.Reserved=0;
        }
        void ReleaseMilitaryOperation(PrehistoryState s,int campaign)
        {
            if(!RevisedMilitaryHistory(s))return;var m=s.History.Aggregates.Empires.Connections.Military;if(campaign>=m.Campaigns.Length)return;var o=m.Campaigns[campaign];if(o.Closed)return;
            var a=m.Governments[o.PartyA];var b=m.Governments[o.PartyB];
            if(o.EpochA==a.MobilizationEpoch)a.Reserved=Math.Max(0,a.Reserved-o.CommittedA);
            if(o.EpochB==b.MobilizationEpoch)b.Reserved=Math.Max(0,b.Reserved-o.CommittedB);
            o.CommittedA=o.CommittedB=0;o.Closed=true;
        }
        int ReserveMilitaryContingent(PrehistoryState s,int campaign,int polity,int requested)
        {
            var m=s.History.Aggregates.Empires.Connections.Military;var o=m.Campaigns[campaign];var g=m.Governments[polity];bool first=o.PartyA==polity;
            if(o.PartyA!=polity&&o.PartyB!=polity)throw new ArgumentException("A military contingent needs an actual campaign party.");
            ref int epoch=ref(first?ref o.EpochA:ref o.EpochB);ref int committed=ref(first?ref o.CommittedA:ref o.CommittedB);
            if(epoch!=g.MobilizationEpoch){epoch=g.MobilizationEpoch;committed=0;}
            if(committed>Math.Max(0,requested)){g.Reserved=Math.Max(0,g.Reserved-(committed-Math.Max(0,requested)));committed=Math.Max(0,requested);}
            int extra=HistoricalMilitaryOperationEffects.Reserve(s.History.Polities[polity].Army,g.Reserved,Math.Max(0,requested-committed));g.Reserved+=extra;committed+=extra;return committed;
        }
        void AdvanceRevisedMilitaryReadiness(PrehistoryState s,int polity,int elapsed,int ideal)
        {
            var h=s.History;var p=h.Polities[polity];var g=h.Aggregates.Empires.Connections.Military.Governments[polity];var capital=s.Society.Settlements[p.CapitalId];
            if(g.Reserved>p.Army)ReleaseMilitaryGovernment(s,polity);
            // Soldiers remain people within the existing population accounts.
            // Mobilization pays equipment/food and is bounded by eligible men;
            // only actual deaths remove people from those accounts.
            int raised=HistoricalMilitaryOperationEffects.Reinforcements(p.Army,ideal,elapsed,Math.Max(0,p.Treasury-p.Army),Math.Max(0,capital.FoodReserve-capital.Population/2));
            if(!h.Aggregates.Cities[p.CapitalId].Supported||h.Aggregates.Living.Cities[p.CapitalId].AbandonedYear>=0)raised=0;
            long cost=raised*4L;capital.FoodReserve-=raised/4;p.Treasury-=cost;p.Army+=raised;g.Recruits+=raised;g.RecruitmentSpending+=cost;
            if(p.Army>ideal){int demobilized=(int)Math.Min(Math.Max(0,p.Army-g.Reserved),Math.Ceiling((p.Army-(double)ideal)*Math.Min(.5,.10*elapsed)));p.Army-=demobilized;}
            ReportImperialGovernment(s,polity);
        }
        void MilitaryOperationLosses(PrehistoryState s,int campaign,int polity,int city,int losses)
        {
            var m=s.History.Aggregates.Empires.Connections.Military;var o=m.Campaigns[campaign];var g=m.Governments[polity];bool first=o.PartyA==polity;
            ref int committed=ref(first?ref o.CommittedA:ref o.CommittedB);
            int actual=Math.Min(Math.Max(0,losses),Math.Min(committed,s.History.Polities[polity].Army));
            if(city<0)return;var source=s.Society.Settlements[city];actual=(int)Math.Min(actual,Math.Min(int.MaxValue,source.Population+s.History.Aggregates.Cities[city].Rural));
            committed-=actual;g.Reserved=Math.Max(0,g.Reserved-actual);g.Deaths+=actual;s.History.Polities[polity].Army-=actual;
            ProvincialBattleCasualties(s,city,actual);ReportImperialGovernment(s,polity);
            var c=s.History.Dynamics.Campaigns[campaign];if(first)c.CasualtiesA+=actual;else c.CasualtiesB+=actual;
        }
        void MilitaryUncommittedLosses(PrehistoryState s,int polity,int city,int requested)
        {
            var h=s.History;var a=h.Aggregates;var g=a.Empires.Connections.Military.Governments[polity];
            if(city<0||city>=a.Cities.Length||a.Resolve(s.Society.Settlements[city].PolityId,h)!=polity)return;
            int actual=(int)Math.Min(Math.Max(0,requested),Math.Min(Math.Max(0,h.Polities[polity].Army-g.Reserved),s.Society.Settlements[city].Population+a.Cities[city].Rural));
            h.Polities[polity].Army-=actual;g.Deaths+=actual;ProvincialBattleCasualties(s,city,actual);ReportImperialGovernment(s,polity);
        }
        void ValidateMilitaryOperations(PrehistoryState s)
        {
            var m=s.History.Aggregates.Empires.Connections.Military;if(m==null)return;
            if(s.History.Aggregates.Empires.Connections.Realms==null||s.History.Aggregates.Empires.Connections.Realms.Version<3||m.Version!=1||m.SinceYear<0||m.SinceYear>s.Years||m.Governments?.Length!=s.History.Polities.Length||m.Campaigns?.Length!=s.History.Dynamics.Campaigns.Length||m.Governments.Any(g=>g==null))throw new ArgumentException("Military operation policy alignment.");
            var reserved=new long[m.Governments.Length];
            for(int i=0;i<m.Campaigns.Length;i++)
            {
                var o=m.Campaigns.Peek(i);if(o==null||o.PartyA<0||o.PartyB<0||o.PartyA>=reserved.Length||o.PartyB>=reserved.Length||o.PartyA==o.PartyB||o.EpochA<0||o.EpochB<0||o.CommittedA<0||o.CommittedB<0||o.MarchDaysA<0||o.MarchDaysB<0||o.SiegeDays<0||o.IdleYears<0||o.CorePressureA<0||o.CorePressureB<0||o.VictoriesA<0||o.VictoriesB<0||o.OriginalCapitalA<0||o.OriginalCapitalA>=CellPatch.Length||o.OriginalCapitalB<0||o.OriginalCapitalB>=CellPatch.Length||o.SiegeTarget< -1||o.SiegeTarget>=CellPatch.Length||o.SiegeBesieger< -1||o.SiegeBesieger>=reserved.Length)throw new ArgumentException("Invalid military operation.");
                if(o.Closed&&(o.CommittedA!=0||o.CommittedB!=0))throw new ArgumentException("Closed military commitment.");
                if(o.EpochA==m.Governments[o.PartyA].MobilizationEpoch)reserved[o.PartyA]+=o.CommittedA;
                if(o.EpochB==m.Governments[o.PartyB].MobilizationEpoch)reserved[o.PartyB]+=o.CommittedB;
            }
            for(int i=0;i<reserved.Length;i++){var g=m.Governments[i];if(g.MobilizationEpoch<0||g.Reserved<0||g.Reserved!=reserved[i]||g.Recruits<0||g.Deaths<0||g.RecruitmentSpending<0||g.FieldPayroll<0||g.FieldProvisions<0||g.Reserved>s.History.Polities[i].Army)throw new ArgumentException("Military manpower reservation ledger.");}
        }
    }
}

using System;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalLayAssociations
    {
        public const int Fields=8;
        public int Version=1,SinceYear;
        public HistoricalCellTree Records;
        public HistoricalLayAssociations Copy()=>(HistoricalLayAssociations)MemberwiseClone();
    }
    public enum ReligiousAssociationDispute { None, SuppressedWorship, CapturedPatronage, RitualAuthority }
    public sealed class ReligiousLayCommunity
    {
        public int Faith,Town,Support,SinceYear,ReviewedYear,LastGiftYear,Commitment;
        public ReligiousAssociationDispute Dispute;
        public bool Active;
    }
    public static class HistoricalLayAssociationEffects
    {
        // Authored local organizing priors, not counts of believers or staff.
        public static long Gift(long residents,int elapsed)=>Math.Max(1,(long)Math.Ceiling(Math.Max(0,residents)*Math.Max(0,elapsed)/8000.0));
        public static long Assembly(long residents)=>Math.Max(10,Math.Max(0,residents)/100);
        public static int Support(int previous,int elapsed,bool paid,bool enforced)
        {
            double target=paid?(enforced?300:750):0;
            return Math.Max(0,Math.Min(1000,(int)Math.Round(previous+(target-previous)*(1-Math.Exp(-Math.Max(0,elapsed)/(paid?60.0:45.0))))));
        }
        public static int Commitment(int previous,int elapsed,bool actualDispute,bool paid,double severity)
            =>actualDispute&&paid?Math.Min(1000,Math.Max(0,previous)+(int)Math.Round(Math.Max(0,elapsed)*(8+16*D.Clamp(severity)))):Math.Max(0,(int)Math.Round(Math.Max(0,previous)*Math.Exp(-Math.Max(0,elapsed)/60.0)));
        public static int OrganizingCapacity(int association,int publicInstitution,bool sameFaith)
            =>Math.Min(1000,Math.Max(0,association)+(sameFaith?Math.Min(400,Math.Max(0,publicInstitution)/2):0));
    }
    public sealed partial class Prehistory
    {
        const int LayFaith=0,LaySupport=1,LayReview=2,LaySince=3,LayTown=4,LayCommitment=5,LayDispute=6,LayGift=7;
        static bool LayReligiousHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=20&&s.History.Aggregates.Empires.Connections.Religion?.LayAssociations!=null;
        void InitializeLayReligiousAssociations(PrehistoryState s)
        {
            if(s.History.Aggregates.Empires.Connections.Realms.Version<20)return;
            var r=s.History.Aggregates.Empires.Connections.Religion;
            if(r.LayAssociations==null)r.LayAssociations=new HistoricalLayAssociations{SinceYear=s.Years,Records=new HistoricalCellTree(checked(CellPatch.Length*HistoricalLayAssociations.Fields))};
        }
        public static ReligiousLayCommunity LayAssociationAt(PrehistoryState s,int cell)
        {
            var lay=s?.History?.Aggregates?.Empires?.Connections?.Religion?.LayAssociations;
            if(lay==null||cell<0||cell>=HistoricalCells.Count(s))return null;
            int key=cell*HistoricalLayAssociations.Fields,faith=lay.Records[key];if(faith<0)return null;
            int town=lay.Records[key+LayTown];bool active=town>=0&&town<s.Society.Settlements.Length&&s.History.Aggregates.TownAt[cell]==town&&s.History.Aggregates.Living.Cities[town].AbandonedYear<0;
            return new ReligiousLayCommunity{Faith=faith,Town=town,Support=active?Math.Max(0,lay.Records[key+LaySupport]):0,SinceYear=lay.Records[key+LaySince],ReviewedYear=lay.Records[key+LayReview],LastGiftYear=lay.Records[key+LayGift],Commitment=Math.Max(0,lay.Records[key+LayCommitment]),Dispute=(ReligiousAssociationDispute)Math.Max(0,lay.Records[key+LayDispute]),Active=active};
        }
        void SeedLayReligiousCommunity(PrehistoryState s,int cell,int faith)
        {
            if(!LayReligiousHistory(s)||faith<0||cell<0||cell>=CellPatch.Length)return;
            var a=s.History.Aggregates;int town=a.TownAt[cell];
            if(town<0||a.Living.Cities[town].AbandonedYear>=0||s.Society.Settlements[town].Population<100)return;
            var p=a.Empires.Connections.Religion.LayAssociations;int key=cell*HistoricalLayAssociations.Fields;
            if(p.Records[key+LayTown]==town&&p.Records[key]>=0&&(p.Records[key]==faith||p.Records[key+LaySupport]>0||p.Records[key+LayGift]>=0))return;
            p.Records=p.Records.With(key,faith).With(key+LaySupport,0).With(key+LayReview,s.Years).With(key+LaySince,s.Years).With(key+LayTown,town).With(key+LayCommitment,0).With(key+LayDispute,0).With(key+LayGift,-1);
        }
        void SeedLayFromDeliveredContact(PrehistoryState s,int cell,int faith,int actualSupport)
        {
            if(!LayReligiousHistory(s)||actualSupport<400)return;
            var r=s.History.Aggregates.Empires.Connections.Religion;
            if(r.Read(cell,FaithHousehold)==faith||HistoricalCells.Religion(s,cell)==faith)SeedLayReligiousCommunity(s,cell,faith);
        }
        void AdvanceLayReligiousCommunity(PrehistoryState s,int cell,int town,int owner,int publicFaith,int institution,int policy,bool officialsPaid)
        {
            if(!LayReligiousHistory(s)||town<0)return;
            var h=s.History;var a=h.Aggregates;var r=a.Empires.Connections.Religion;var lay=r.LayAssociations;
            if(a.TownAt[cell]!=town||a.Living.Cities[town].AbandonedYear>=0)return;
            // A same-tradition patronage dispute can organize an existing local
            // public core; bare minority memory cannot manufacture an institution.
            if(CapturedReligiousEndowment(s,owner,publicFaith,s.Society.Settlements[town].Stability)&&institution>=400&&policy!=0)SeedLayReligiousCommunity(s,cell,publicFaith);
            int key=cell*HistoricalLayAssociations.Fields,faith=lay.Records[key];
            if(faith<0||lay.Records[key+LayTown]!=town)return;
            int elapsed=s.Years-lay.Records[key+LayReview];if(elapsed<10)return;elapsed=Math.Min(20,elapsed);
            lay.Records=lay.Records.With(key+LayReview,s.Years);
            var t=s.Society.Settlements[town];
            bool restriction=owner>=0&&policy==2&&h.Polities[owner].ReligionId!=faith;
            bool enforced=restriction&&(officialsPaid||ReligiousPatronagePaid(s,owner,cell,elapsed));
            bool captured=faith==publicFaith&&policy!=0&&institution>=400&&CapturedReligiousEndowment(s,owner,faith,t.Stability);
            if(captured&&!officialsPaid)captured=ReligiousPatronagePaid(s,owner,cell,elapsed);
            int visitor=r.Read(cell,FaithVisitor);
            bool related=visitor>=0&&visitor!=faith&&(h.Religions[visitor].ParentId==faith||h.Religions[faith].ParentId==visitor);
            bool ritual=related&&r.Read(cell,FaithContactYear)>=0&&s.Years-r.Read(cell,FaithContactYear)<=80&&r.Read(cell,FaithBlend)>=300&&h.Religions[visitor].Ritual!=h.Religions[faith].Ritual;
            bool disputed=captured||ritual;long gift=HistoricalLayAssociationEffects.Gift(t.Population,elapsed),assembly=disputed?HistoricalLayAssociationEffects.Assembly(t.Population):0;
            bool paid=t.Population>=100&&t.FoodReserve-t.Population/2>=gift+assembly;
            int former=Math.Max(0,lay.Records[key+LaySupport]),priorReason=Math.Max(0,lay.Records[key+LayDispute]);
            if(paid){t.FoodReserve-=gift+assembly;lay.Records=lay.Records.With(key+LayGift,s.Years);}
            int support=HistoricalLayAssociationEffects.Support(former,elapsed,paid,enforced);
            double severity=captured?D.Clamp((h.Dynamics.States[owner].Cycle.EliteCapture-600)/400.0+(500-t.Stability)/500.0):ritual?.6:0;
            int commitment=HistoricalLayAssociationEffects.Commitment(Math.Max(0,lay.Records[key+LayCommitment]),elapsed,disputed,paid,severity);
            var reason=enforced?ReligiousAssociationDispute.SuppressedWorship:captured?ReligiousAssociationDispute.CapturedPatronage:ritual?ReligiousAssociationDispute.RitualAuthority:ReligiousAssociationDispute.None;
            lay.Records=lay.Records.With(key+LaySupport,support).With(key+LayCommitment,commitment).With(key+LayDispute,(int)reason);
            if(former<100&&support>=100&&paid)
                HistoryEvent(s,"religious-association","Households at "+t.Name+" provision a local association of "+h.Religions[faith].Name+" for shared observances, ritual calendars and mutual aid. This is an organized local community, not a census of believers or a new public religion.",cell,t.CultureId,owner,town:town,religion:faith);
            if(enforced&&priorReason!=(int)ReligiousAssociationDispute.SuppressedWorship&&former>=100)
                HistoryEvent(s,"religious-suppression","Funded restrictions at "+t.Name+" limit the meetings of households maintaining "+h.Religions[faith].Name+". Their inherited identity continues; reduced public activity is not a new sect or proof of conversion.",cell,t.CultureId,owner,town:town,religion:faith);
            if(!enforced&&priorReason==(int)ReligiousAssociationDispute.SuppressedWorship&&support>=100)
                HistoryEvent(s,"religious-reconciliation","The household association of "+h.Religions[faith].Name+" at "+t.Name+" resumes permitted local meetings as funded restrictions ease. Its faith and the community's public predominance are unchanged by this settlement.",cell,t.CultureId,owner,town:town,religion:faith);
            if(paid&&support>=200&&faith!=publicFaith)RecordReligiousVisitorWithSupport(s,cell,cell,faith,support);
            TryLayReligiousRenewal(s,cell,town,owner,faith,institution,elapsed,disputed,paid,ritual,visitor);
        }
        void TryLayReligiousRenewal(PrehistoryState s,int cell,int town,int owner,int faith,int institution,int elapsed,bool disputed,bool paid,bool ritual,int visitor)
        {
            var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;var lay=r.LayAssociations;int key=cell*HistoricalLayAssociations.Fields;
            if(!disputed||!paid||lay.Records[key+LayCommitment]<600||HistoricalLayAssociationEffects.OrganizingCapacity(lay.Records[key+LaySupport],institution,HistoricalCells.Religion(s,cell)==faith)<600)return;
            int recent=Math.Max(h.Religions[faith].FoundedYear,r.Read(cell,FaithReformed));
            if(s.Years-lay.Records[key+LaySince]<30||s.Years-recent<250||HistoryRoll("religion-lay-autonomy-20",town,s.Years)>=1-Math.Exp(-elapsed/200.0))return;
            var t=s.Society.Settlements[town];long cost=Math.Max(10,t.Population/50);if(t.FoodReserve-t.Population/2<cost)return;
            t.FoodReserve-=cost;int id=h.Religions.Length;var parent=h.Religions[faith];
            var renewal=new HistoricalReligion{Id=id,ParentId=faith,CultureId=t.CultureId,HolyCell=cell,FoundedYear=s.Years,Name=LanguageName(s,t.CultureId,LanguageNameKind.Religion,id),Reach=parent.Reach==ReligiousReach.Local?ReligiousReach.Local:ReligiousReach.Portable,
                Ethos=ritual?parent.Ethos:"Inherited obligations with ethical restraint, mutual aid and independence from captured patronage",
                Ritual=ritual?parent.Ritual+"; local instruction reinterprets practices encountered from "+h.Religions[visitor].Name:"Inherited household observances and communal instruction sustained by local gifts",
                Tradition=ritual?"A locally organized renewal of "+parent.Name+" following recorded disputes over practices received from "+h.Religions[visitor].Name:"An autonomous lay renewal of "+parent.Name+" opposing recorded capture of court patronage at "+t.Name};
            h.Religions=h.Religions.Concat(new[]{renewal}).ToArray();
            lay.Records=lay.Records.With(key,id).With(key+LaySince,s.Years).With(key+LayCommitment,0).With(key+LayDispute,0);r.Write(cell,FaithReformed,s.Years);
            HistoryEvent(s,ritual?"religious-schism":"religious-renewal",renewal.Name+" forms a separately provisioned association at "+t.Name+(ritual?" after an actual dispute over inherited ritual authority.":" in opposition to captured patronage, emphasizing ethical restraint and mutual aid.")+" The parent lineage is retained; public predominance does not change automatically.",cell,t.CultureId,owner,town:town,religion:id);
        }
        int LayReligiousCarrier(PrehistoryState s,int cell,int target,out int support)
        {
            support=Math.Max(0,s.History.Aggregates.Empires.Connections.Religion.Read(cell,FaithInstitution));int ordinary=HistoricalCells.Religion(s,cell);
            if(!LayReligiousHistory(s))return ordinary;var p=LayAssociationAt(s,cell);
            if(p==null||!p.Active||p.Support<200||p.Faith==ordinary||p.LastGiftYear<0||s.Years-p.LastGiftYear>20||p.Dispute==ReligiousAssociationDispute.SuppressedWorship)return ordinary;
            var targetFaith=s.History.Aggregates.Empires.Connections.Religion.Read(target,FaithHousehold);
            if(targetFaith!=p.Faith&&HistoryRoll("religion-lay-carrier-20-"+target,cell,s.Years)>=.20)return ordinary;
            var town=s.Society.Settlements[p.Town];long cost=Math.Max(1,town.Population/4000);if(town.FoodReserve-town.Population/2<cost)return ordinary;
            town.FoodReserve-=cost;support=p.Support;return p.Faith;
        }
        void ValidateLayReligiousAssociations(PrehistoryState s)
        {
            var p=s.History.Aggregates.Empires.Connections.Religion?.LayAssociations;
            if(p==null){if(s.History.Aggregates.Empires.Connections.Realms.Version>=20)throw new ArgumentException("Revised household organization requires its saved ledger.");return;}
            bool Valid(int key,int value)
            {
                if(value<0||CellPatch[key/HistoricalLayAssociations.Fields]<0)return false;
                switch(key%HistoricalLayAssociations.Fields)
                {
                    case LayFaith:return value<s.History.Religions.Length;
                    case LaySupport:case LayCommitment:return value<=1000;
                    case LayTown:return value<s.Society.Settlements.Length&&s.Society.Settlements[value].Cell==key/HistoricalLayAssociations.Fields;
                    case LayDispute:return value<=(int)ReligiousAssociationDispute.RitualAuthority;
                    default:return value<=s.Years;
                }
            }
            if(p.Version!=1||p.SinceYear<0||p.SinceYear>s.Years||p.Records?.Length!=CellPatch.Length*HistoricalLayAssociations.Fields||!p.Records.AllPresentValues(Valid))throw new ArgumentException("Invalid bounded local lay associations.");
        }
    }
}

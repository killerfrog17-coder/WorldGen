using System;
namespace WorldGen.Core
{
    public sealed class HistoricalLocalMemory
    {
        public int Ruler=-1,SinceYear,ReviewedYear,PreviousCourt=-1,SelfRuleSince=-1,LocalRights,InclusiveYears,WrongingState=-1,Harm,RelatedTown=-1,Relationship,LastViolence=-1;
    }
    public sealed partial class Prehistory
    {
        public static HistoricalLocalMemory LocalHistoricalMemoryAt(PrehistoryState s,int city)
        {
            if(!IdentityHistory(s)||city<0||city>=s.Society.Settlements.Length)return null;
            var t=s.History.Identities.Memories;int k=city*12;if(t[k+1]<0)return null;
            return new HistoricalLocalMemory{Ruler=t[k],SinceYear=t[k+1],ReviewedYear=t[k+2],PreviousCourt=t[k+3],SelfRuleSince=t[k+4],LocalRights=Math.Max(0,t[k+5]),InclusiveYears=Math.Max(0,t[k+6]),WrongingState=t[k+7],Harm=Math.Max(0,t[k+8]),RelatedTown=t[k+9],Relationship=t[k+10]<0?0:t[k+10]-1000,LastViolence=t[k+11]};
        }
        void StoreLocalMemory(PrehistoryState s,int city,HistoricalLocalMemory p)
        {
            var r=s.History.Identities;r.Memories=r.Memories.WithBlock(city*12,new[]{p.Ruler,p.SinceYear,p.ReviewedYear,p.PreviousCourt,p.SelfRuleSince,p.LocalRights,p.InclusiveYears,p.WrongingState,p.Harm,p.RelatedTown,1000+p.Relationship,p.LastViolence});
        }
        int CurrentLocalRights(PrehistoryState s,int owner)=>owner<0?650:Math.Min(1000,s.History.Aggregates.Communities.Governments[owner].RepresentationMilli+(1000-s.History.Aggregates.Communities.Governments[owner].CentralizationMilli)/2);
        HistoricalLocalMemory TouchLocalMemory(PrehistoryState s,int city,int ruler)
        {
            var p=LocalHistoricalMemoryAt(s,city);if(p!=null)return p;
            p=new HistoricalLocalMemory{Ruler=ruler,SinceYear=s.Years,ReviewedYear=s.Years,LocalRights=CurrentLocalRights(s,ruler)};
            if(ruler>=0&&s.History.Polities[ruler].CapitalId==city)p.SelfRuleSince=s.Years;
            StoreLocalMemory(s,city,p);return p;
        }
        void RecordIdentityGovernmentTransfer(PrehistoryState s,int city,int old,int owner)
        {
            if(!IdentityHistory(s))return;var p=TouchLocalMemory(s,city,old);var a=s.History.Aggregates;
            if(old==owner||old>=0&&a.Resolve(old,s.History)==owner){p.Ruler=owner;StoreLocalMemory(s,city,p);return;}
            if(old>=0){p.PreviousCourt=old;if(s.History.Polities[old].CapitalId==city&&p.SelfRuleSince<0)p.SelfRuleSince=p.SinceYear;}
            p.Ruler=owner;p.SinceYear=s.Years;
            // Administrative transfer alone is not a massacre or an invented
            // conquest. Explicit campaign hooks record actual violent capture.
            if(owner>=0&&s.History.Polities[owner].CapitalId==city&&p.SelfRuleSince<0)p.SelfRuleSince=s.Years;
            StoreLocalMemory(s,city,p);
        }
        void RecordIdentityViolence(PrehistoryState s,int city,int attacker,int severity)
        {
            if(!IdentityHistory(s)||city<0||attacker<0)return;
            var p=TouchLocalMemory(s,city,HistoricalCells.Polity(s,s.Society.Settlements[city].Cell));
            if(p.WrongingState!=attacker)p.Harm=0;
            p.WrongingState=attacker;p.Harm=Math.Min(1000,p.Harm+Math.Max(0,severity));p.LastViolence=s.Years;
            int center=s.History.Polities[attacker].CapitalId;
            if(center!=city){p.RelatedTown=center;p.Relationship=Math.Max(-1000,p.Relationship-350);}
            StoreLocalMemory(s,city,p);
        }
        void RecordLocalContactMemory(PrehistoryState s,int from,int to)
        {
            int a=s.History.Aggregates.TownAt[from],b=s.History.Aggregates.TownAt[to];if(a<0||b<0||a==b)return;
            var p=TouchLocalMemory(s,b,HistoricalCells.Polity(s,to));
            if(p.RelatedTown!=a&&p.Relationship< -300)return;
            if(p.RelatedTown!=a){p.RelatedTown=a;p.Relationship=0;}
            p.Relationship=Math.Min(800,p.Relationship+20);StoreLocalMemory(s,b,p);
        }
        void AdvanceLocalHistoricalMemory(PrehistoryState s,int city)
        {
            if(!IdentityHistory(s))return;int owner=HistoricalCells.Polity(s,s.Society.Settlements[city].Cell);var p=TouchLocalMemory(s,city,owner);
            int years=s.Years-p.ReviewedYear;if(years<=0)return;
            var local=s.History.Aggregates.Communities.Cities[city];bool inclusive=owner<0||local.InclusionMilli>=600&&s.History.Aggregates.Cities[city].Supported;
            p.LocalRights=Math.Max(p.LocalRights,CurrentLocalRights(s,owner));
            p.InclusiveYears=inclusive?Math.Min(1000000,p.InclusiveYears+years):Math.Max(0,p.InclusiveYears-years);
            p.Harm=HistoricalIdentityEffects.Approach(p.Harm,0,years,inclusive?180.0:1200.0);
            if(p.Relationship!=0)p.Relationship=(int)Math.Round(p.Relationship*Math.Exp(-years/(p.Relationship<0?500.0:250.0)));
            p.ReviewedYear=s.Years;StoreLocalMemory(s,city,p);
        }
        double LocalIdentityPoliticalGrievance(PrehistoryState s,int city,int owner,double difference)
        {
            if(!IdentityHistory(s))return 0;var p=EthnicCommunityAt(s,s.Society.Settlements[city].Cell);if(p==null)return 0;
            var memory=LocalHistoricalMemoryAt(s,city);var local=s.History.Aggregates.Communities.Cities[city];var policy=s.History.Aggregates.Communities.Governments[owner];
            int harm=memory!=null&&(memory.WrongingState==owner||memory.WrongingState>=0&&s.History.Aggregates.Resolve(memory.WrongingState,s.History)==owner)?memory.Harm:0;
            return HistoricalIdentityEffects.PoliticalGrievance(difference,p.Cohesion,p.HomeAttachment,local.InclusionMilli,policy.RepresentationMilli,harm,Math.Max(0,(memory?.LocalRights??0)-CurrentLocalRights(s,owner)),memory?.SelfRuleSince>=0&&memory.PreviousCourt!=owner);
        }
        bool SharedLocalPoliticalIdentity(PrehistoryState s,int city,int candidate)
        {
            if(!IdentityHistory(s))return s.Society.Settlements[city].CultureId==s.History.Polities[candidate].CultureId;
            // Physical routes, succession provenance and viable support are
            // still tested by the caller. Shared heritage never grants a border.
            return LocalIdentityDifference(s,city,candidate)<.5;
        }
        void ValidateLocalHistoricalMemories(PrehistoryState s)
        {
            var tree=s.History.Identities.Memories;var r=s.History.Identities;
            if(!tree.AllPresentValues((k,v)=>
            {
                int city=k/12,f=k%12;if(city>=s.Society.Settlements.Length||tree[city*12+1]<r.SinceYear)return false;
                if(f==0||f==3||f==7)return v>=0&&v<s.History.Polities.Length;
                if(f==1||f==2||f==4||f==11)return v>=r.SinceYear&&v<=s.Years;
                if(f==9)return v>=0&&v<s.Society.Settlements.Length&&v!=city;
                return f==6?v>=0&&v<=1000000:f==10?v>=0&&v<=2000:v>=0&&v<=1000;
            }))throw new ArgumentException("Invalid local historical memory.");
        }
    }
}

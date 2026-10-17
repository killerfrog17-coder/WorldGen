using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum HistoricalOrganizationKind { TradeLeague, PeaceCongress }
    [Serializable] public sealed class HistoricalDiplomaticAccount
    {
        public int Id,TradeOrganization=-1,PeaceOrganization=-1,LastTradeDuesYear,LastPeaceDuesYear,TradeArrearsYears,PeaceArrearsYears,CoalitionEntries,LastWarYear=-1;
        [System.Runtime.Serialization.OptionalField] public int ContinuitySuccessor=-1;
        [System.Runtime.Serialization.OnDeserializing] void InitializeContinuity(System.Runtime.Serialization.StreamingContext context){ContinuitySuccessor=-1;}
        public HistoricalLocalBag Relations=new HistoricalLocalBag();
        public HistoricalDiplomaticAccount Copy(){var c=(HistoricalDiplomaticAccount)MemberwiseClone();c.Relations=Relations.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalInternationalOrganization
    {
        public int Id,FounderPolity,FoundedYear,EndedYear=-1,LastActionYear,LastMediationYear=-1,Authority=500,Mediations,FailedMediations;
        public HistoricalOrganizationKind Kind;
        public string Name="";
        public long Reserve,Contributions,ServiceSpending,MediationSpending;
        public HistoricalLocalBag Members=new HistoricalLocalBag();
        public HistoricalInternationalOrganization Copy(){var c=(HistoricalInternationalOrganization)MemberwiseClone();c.Members=Members.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalCoalitionEntry
    {
        public int CampaignId,OriginCampaignId,AllyId,PolityId,Year;
        public HistoricalCoalitionEntry Copy()=>(HistoricalCoalitionEntry)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalInternationalSystem
    {
        public string Version="international-1";
        public HistoricalDiplomaticAccount[] Accounts=Array.Empty<HistoricalDiplomaticAccount>();
        public HistoricalInternationalOrganization[] Organizations=Array.Empty<HistoricalInternationalOrganization>();
        public HistoricalCoalitionEntry[] CoalitionEntries=Array.Empty<HistoricalCoalitionEntry>();
        public HistoricalRelationCampaigns RelationCampaigns=new HistoricalRelationCampaigns();
        [System.Runtime.Serialization.OptionalField] public HistoricalInternationalFinances Finances;
        public HistoricalInternationalSystem Copy()=>new HistoricalInternationalSystem{Version=Version,Accounts=Accounts.Select(x=>x.Copy()).ToArray(),Organizations=Organizations.Select(x=>x.Copy()).ToArray(),CoalitionEntries=CoalitionEntries.Select(x=>x.Copy()).ToArray(),RelationCampaigns=RelationCampaigns.Fork(),Finances=Finances?.Copy()};
    }
    public sealed class HistoricalOrganizationView
    {
        public int Id,Members,FoundedYear,Authority,Mediations;
        public string Name="",Purpose="",Standing="";
        public long Reserve;
    }
    public sealed class HistoricalInternationalView
    {
        public bool Available;
        public int KnownRelations,CoalitionEntries;
        public HistoricalOrganizationView[] Organizations=Array.Empty<HistoricalOrganizationView>();
    }
    public static class HistoricalInternationalEffects
    {
        public static bool TradeCapacity(double writing,double coinage,int institution)=>writing>=.2&&coinage>=.2&&institution>=350;
        public static bool PeaceCapacity(double printing,int institution)=>printing>=.2&&institution>=450;
        public static double Acceptance(int trust,int diplomacy,int ambition,int exhaustion,bool peace)
            =>D.Clamp(.08+.40*D.Clamp((trust+30)/130.0)+.25*D.Clamp(diplomacy/1000.0)+(peace?.25*D.Clamp(exhaustion/500.0)-.15*D.Clamp(ambition/1000.0):.10));
        public static double AgreementAcceptance(double own,double peer,bool ownMember,bool peerMember)
            =>ownMember&&peerMember?0:ownMember?D.Clamp(peer):peerMember?D.Clamp(own):Math.Min(D.Clamp(own),D.Clamp(peer));
        public static long Dues(long receipts,int elapsed,long treasury,long reserve)
            =>(long)Math.Max(0,Math.Min((decimal)Math.Min(Math.Max(0,treasury)/20,Math.Max(0,1000000000000L-reserve)),(decimal)Math.Max(0,receipts)*.01m*Math.Max(0,elapsed)));
        public static double WarRestraint(int authority,long reserve,int members)
            =>1-.35*D.Clamp(authority/1000.0)*D.Clamp(reserve/(250.0+Math.Max(0,members)*20.0));
        public static double TradeEfficiency(int authority,long reserve,int members)
            =>1+.12*D.Clamp(authority/1000.0)*D.Clamp(reserve/(250.0+Math.Max(0,members)*20.0));
        public static double MediationChance(int trust,int exhaustionA,int exhaustionB,int diplomacyA,int diplomacyB)
            =>D.Clamp(.02+.30*D.Clamp((exhaustionA+exhaustionB)/1000.0)+.20*D.Clamp((trust+100)/200.0)+.20*Math.Min(diplomacyA,diplomacyB)/1000.0);
        public static double CoalitionChance(int elapsed,int trust,int exhaustion,bool allyAttacked)
            =>1-Math.Exp(-.20*Math.Max(0,elapsed)*(.5+.5*D.Clamp(trust/100.0))*D.Clamp(1-exhaustion/700.0)*(allyAttacked?1:.25));
    }
    public sealed partial class Prehistory
    {
        void InitializeInternational(PrehistoryState s)
        {
            if(!LivingWorldHistory(s)||s.History.International!=null)return;
            var h=s.History;var system=new HistoricalInternationalSystem();h.International=system;
            PrepareAggregateRelations(s);
            system.Accounts=Enumerable.Range(0,h.Polities.Length).Select(k=>new HistoricalDiplomaticAccount{Id=k,LastTradeDuesYear=s.Years,LastPeaceDuesYear=s.Years}).ToArray();
            system.RelationCampaigns=Enumerable.Repeat(-1,h.Relations.Length).ToArray();
            // One-time explicit upgrade reconstruction; never an ordinary turn.
            for(int k=0;k<h.Relations.Length;k++)RememberInternationalRelation(s,k);
            for(int k=0;k<h.Dynamics.Campaigns.Length;k++)
            {var c=h.Dynamics.Campaigns[k];system.Accounts[c.A].LastWarYear=Math.Max(system.Accounts[c.A].LastWarYear,c.StartedYear);system.Accounts[c.B].LastWarYear=Math.Max(system.Accounts[c.B].LastWarYear,c.StartedYear);if(c.EndedYear>=0)continue;if(aggregateRelations.TryGetValue((Math.Min(c.A,c.B),Math.Max(c.A,c.B)),out int relation))system.RelationCampaigns[relation]=k;}
            InitializeInternationalFinances(s);
        }
        void RememberInternationalRelation(PrehistoryState s,int relation)
        {
            var system=s.History.International;if(!LivingWorldHistory(s)||system==null)return;
            if(system.RelationCampaigns.Length<s.History.Relations.Length)system.RelationCampaigns.Grow(s.History.Relations.Length,-1);
            var r=s.History.Relations.Peek(relation);system.Accounts[r.A].Relations.Add(relation);system.Accounts[r.B].Relations.Add(relation);
        }
        void AppendInternationalState(PrehistoryState s,int parent=-1,bool successor=false)
        {
            var system=s.History.International;if(!LivingWorldHistory(s)||system==null)return;
            int id=s.History.Polities.Length-1;var account=successor&&parent>=0?system.Accounts[parent].Copy():new HistoricalDiplomaticAccount{LastTradeDuesYear=s.Years,LastPeaceDuesYear=s.Years};account.Id=id;
            system.Accounts=system.Accounts.Concat(new[]{account}).ToArray();
            if(system.Finances!=null){var f=system.Finances;f.Accounts=f.Accounts.Concat(new[]{successor&&parent>=0?f.Accounts[parent].Copy():new HistoricalInternationalFinanceAccount()}).ToArray();if(successor&&parent>=0)f.Accounts[parent]=new HistoricalInternationalFinanceAccount();}
            if(successor&&parent>=0)
            {
                account.ContinuitySuccessor=-1;system.Accounts[parent].ContinuitySuccessor=id;
                foreach(int organization in new[]{account.TradeOrganization,account.PeaceOrganization})if(organization>=0)
                {var body=system.Organizations[organization];body.Members.Remove(parent);if(body.EndedYear<0)body.Members.Add(id);}
                system.Accounts[parent].TradeOrganization=system.Accounts[parent].PeaceOrganization=-1;
            }
        }
        int InternationalContinuingParty(PrehistoryState s,int polity)
        {
            var system=s.History.International;int guard=0;
            while(polity>=0&&system.Accounts[polity].ContinuitySuccessor>=0)
            {int next=system.Accounts[polity].ContinuitySuccessor;if(next<=polity||next>=system.Accounts.Length||++guard>system.Accounts.Length)throw new ArgumentException("Invalid diplomatic continuity.");polity=next;}
            return polity;
        }
        bool ContinueInternationalCampaign(PrehistoryState s,int campaign)
        {
            var h=s.History;var system=h.International;if(!LivingWorldHistory(s)||system==null)return true;var war=h.Dynamics.Campaigns[campaign];if(war.EndedYear>=0)return false;
            int first=InternationalContinuingParty(s,war.A),second=InternationalContinuingParty(s,war.B),attacker=InternationalContinuingParty(s,war.AttackerId);
            if(first==war.A&&second==war.B)return true;
            // Only explicit constitutional/dynastic continuity is followed.
            // Conquest, annexation and uncontinued collapse do not inherit wars.
            if(first==second){war.EndedYear=s.Years;war.Outcome="Both sides enter the same continuing government.";return false;}
            int oldA=war.A,oldB=war.B;long lossesA=war.CasualtiesA,lossesB=war.CasualtiesB;
            war.A=Math.Min(first,second);war.B=Math.Max(first,second);war.AttackerId=attacker;
            if(first>second){war.CasualtiesA=lossesB;war.CasualtiesB=lossesA;}
            war.LeaderId=h.Dynamics.States[attacker].LeaderId;war.OwnedCanonicalRecord=null;
            bool existed=aggregateRelations.TryGetValue((war.A,war.B),out int index);var relation=AggregateRelation(s,war.A,war.B);index=aggregateRelations[(war.A,war.B)];
            if(!existed&&aggregateRelations.TryGetValue((Math.Min(oldA,oldB),Math.Max(oldA,oldB)),out int former))
            {var prior=h.Relations.Peek(former);relation.Trust=prior.Trust;relation.LastBattle=prior.LastBattle;}
            int other=system.RelationCampaigns[index];
            if(other>=0&&other!=campaign&&h.Dynamics.Campaigns[other].EndedYear<0)
            {war.EndedYear=s.Years;war.Outcome="The continuing government's front joins its already active conflict with the same opponent.";return false;}
            relation.WarSince=war.StartedYear;relation.Trade=relation.Alliance=false;system.RelationCampaigns[index]=campaign;
            return true;
        }
        void CloseInternationalState(PrehistoryState s,int polity)
        {
            var system=s.History.International;if(!LivingWorldHistory(s)||system==null)return;
            var account=system.Accounts[polity];
            foreach(int organization in new[]{account.TradeOrganization,account.PeaceOrganization})if(organization>=0)
            {var body=system.Organizations[organization];if(system.Finances!=null){SettleInternationalOperatingWork(s,organization);FundInternationalOperatingWork(s,organization);RemoveInternationalAssessment(s,polity,organization);}body.Members.Remove(polity);if(body.Members.Count<2&&body.EndedYear<0)body.EndedYear=s.Years;}
            account.TradeOrganization=account.PeaceOrganization=-1;
        }
        int InternationalPeer(PrehistoryState s,int polity,int relation)
        {
            var r=s.History.Relations.Peek(relation);var a=s.History.Aggregates;int first=a.Resolve(r.A,s.History),second=a.Resolve(r.B,s.History);
            return first==polity&&second!=polity?second:second==polity&&first!=polity?first:-1;
        }
        int CurrentInternationalRelation(PrehistoryState s,int polity,int peer,int previous)
        {
            var h=s.History;var prior=h.Relations.Peek(previous);int a=Math.Min(polity,peer),b=Math.Max(polity,peer);
            if(prior.A==a&&prior.B==b)return previous;
            // Historical relation records retain their original parties. A
            // successor inherits their diplomatic habits without editing them.
            if(aggregateRelations.TryGetValue((a,b),out int known)){s.History.International.Accounts[polity].Relations.Remove(previous);return known;}
            var relation=AggregateRelation(s,polity,peer);relation.Trust=prior.Trust;relation.Trade=prior.Trade;relation.Alliance=prior.Alliance;relation.TruceUntil=prior.TruceUntil;relation.LastBattle=prior.LastBattle;
            s.History.International.Accounts[polity].Relations.Remove(previous);return aggregateRelations[(a,b)];
        }
        int ActiveInternationalOrganization(PrehistoryState s,int polity,HistoricalOrganizationKind kind)
        {
            var system=s.History.International;if(!LivingWorldHistory(s)||system==null||polity<0||polity>=system.Accounts.Length)return -1;
            var account=system.Accounts[polity];int id=kind==HistoricalOrganizationKind.TradeLeague?account.TradeOrganization:account.PeaceOrganization;
            if(id<0||id>=system.Organizations.Length||system.Organizations[id].EndedYear>=0)return -1;
            var members=system.Organizations[id].Members;int position=members.Positions[polity];
            return position>=0&&position<members.Count&&members.At(position)==polity?id:-1;
        }
        public HistoricalInternationalView InternationalProfile(PrehistoryState s,int polity)
        {
            var result=new HistoricalInternationalView();if(!LivingWorldHistory(s)||s.History.International==null||polity<0||polity>=s.History.Polities.Length)return result;
            var system=s.History.International;var account=system.Accounts[polity];result.Available=true;result.KnownRelations=account.Relations.Count;result.CoalitionEntries=account.CoalitionEntries;
            foreach(var kind in new[]{HistoricalOrganizationKind.TradeLeague,HistoricalOrganizationKind.PeaceCongress})
            {
                int id=ActiveInternationalOrganization(s,polity,kind);if(id<0)continue;var body=system.Organizations[id];
                result.Organizations=result.Organizations.Concat(new[]{new HistoricalOrganizationView{Id=id,Name=body.Name,Members=body.Members.Count,FoundedYear=body.FoundedYear,Authority=body.Authority,Mediations=body.Mediations,Reserve=body.Reserve,
                    Purpose=kind==HistoricalOrganizationKind.TradeLeague?"Negotiated trade rules reduce friction on already supplied physical routes; membership creates neither deposits nor free technical knowledge.":"A funded diplomatic forum can mediate between members. Its decisions are not automatic peace, disarmament or a world government.",Standing=InternationalFunding(s,id)>=.5?"Funded secretariat and active member contributions":"Operating work is underfunded; institutional benefits are limited."}}).ToArray();
            }
            return result;
        }
        bool InternationalCapacity(PrehistoryState s,int polity,HistoricalOrganizationKind kind)
        {
            var c=s.History.Cultures[s.History.Polities[polity].CultureId];
            return kind==HistoricalOrganizationKind.TradeLeague?HistoricalInternationalEffects.TradeCapacity(HistoricalModernEffects.Adoption(c,HistoricalTechnology.Writing,s.Years),HistoricalModernEffects.Adoption(c,HistoricalTechnology.Coinage,s.Years),c.Institution)
                :HistoricalInternationalEffects.PeaceCapacity(HistoricalModernEffects.Adoption(c,HistoricalTechnology.Printing,s.Years),c.Institution);
        }
        void JoinInternationalOrganization(PrehistoryState s,int polity,int organization)
        {
            var system=s.History.International;var body=system.Organizations[organization];var account=system.Accounts[polity];
            if(system.Finances!=null){int previous=body.Kind==HistoricalOrganizationKind.TradeLeague?account.TradeOrganization:account.PeaceOrganization;if(previous>=0&&previous!=organization)RemoveInternationalAssessment(s,polity,previous);SettleInternationalOperatingWork(s,organization);FundInternationalOperatingWork(s,organization);}
            body.Members.Add(polity);if(system.Finances!=null){RefreshInternationalAssessment(s,polity,organization);if(body.Reserve>=HistoricalInternationalBudgetEffects.ReserveTarget(InternationalAnnualBudget(s,organization)))CreditInternationalReservePledge(s,polity,organization,long.MaxValue);}
            if(body.Kind==HistoricalOrganizationKind.TradeLeague){account.TradeOrganization=organization;account.LastTradeDuesYear=s.Years;account.TradeArrearsYears=0;}
            else{account.PeaceOrganization=organization;account.LastPeaceDuesYear=s.Years;account.PeaceArrearsYears=0;}
        }
        void LeaveInternationalOrganization(PrehistoryState s,int polity,int organization,string reason)
        {
            var system=s.History.International;var body=system.Organizations[organization];if(system.Finances!=null){SettleInternationalOperatingWork(s,organization);FundInternationalOperatingWork(s,organization);RemoveInternationalAssessment(s,polity,organization);}body.Members.Remove(polity);var account=system.Accounts[polity];
            if(body.Kind==HistoricalOrganizationKind.TradeLeague)account.TradeOrganization=-1;else account.PeaceOrganization=-1;
            if(body.Members.Count<2)body.EndedYear=s.Years;
            var p=s.History.Polities[polity];HistoryEvent(s,"treaty-withdrawal",p.Name+" leaves "+body.Name+". "+reason,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
        }
        void PayInternationalDues(PrehistoryState s,int polity,int organization)
        {
            if(s.History.International.Finances!=null){PayInternationalOperatingDues(s,polity,organization);return;}
            var h=s.History;var body=h.International.Organizations[organization];var account=h.International.Accounts[polity];var p=h.Polities[polity];
            bool trade=body.Kind==HistoricalOrganizationKind.TradeLeague;int last=trade?account.LastTradeDuesYear:account.LastPeaceDuesYear,elapsed=s.Years-last;if(elapsed<=0)return;
            long due=HistoricalInternationalEffects.Dues(h.Aggregates.Polities[polity].Receipts,elapsed,p.Treasury,body.Reserve);p.Treasury-=due;body.Reserve+=due;body.Contributions+=due;
            decimal expected=(decimal)Math.Max(0,h.Aggregates.Polities[polity].Receipts)*.01m*elapsed;bool arrears=due<expected/2&&body.Reserve<1000000000000L;
            if(trade){account.LastTradeDuesYear=s.Years;account.TradeArrearsYears=arrears?account.TradeArrearsYears+elapsed:Math.Max(0,account.TradeArrearsYears-elapsed);}
            else{account.LastPeaceDuesYear=s.Years;account.PeaceArrearsYears=arrears?account.PeaceArrearsYears+elapsed:Math.Max(0,account.PeaceArrearsYears-elapsed);}
            if((trade?account.TradeArrearsYears:account.PeaceArrearsYears)>25){LeaveInternationalOrganization(s,polity,organization,"Persistent unpaid dues suspend its treaty privileges.");return;}
            if(s.Years<=body.LastActionYear)return;
            int years=s.Years-body.LastActionYear;body.LastActionYear=s.Years;long service=Math.Min(body.Reserve,(long)years*(1+body.Members.Count/4));body.Reserve-=service;body.ServiceSpending+=service;
            body.Authority=Math.Max(0,Math.Min(1000,body.Authority+(service>0?Math.Min(5,years):-years)));
        }
        void ConsiderInternationalOrganization(PrehistoryState s,int polity,int peer,HistoricalRelation relation,int elapsed,HistoricalOrganizationKind kind)
        {
            if(relation.WarSince>=0||relation.Trust<15||!InternationalCapacity(s,polity,kind)||!InternationalCapacity(s,peer,kind))return;
            bool trade=kind==HistoricalOrganizationKind.TradeLeague;
            bool recentWars=s.History.International.Accounts[polity].LastWarYear>=s.Years-75&&s.History.International.Accounts[peer].LastWarYear>=s.Years-75;
            if(trade&&!relation.Trade||!trade&&relation.TruceUntil<=s.Years&&relation.LastBattle<=0&&!recentWars)return;
            int own=ActiveInternationalOrganization(s,polity,kind),other=ActiveInternationalOrganization(s,peer,kind);if(own>=0&&other>=0)return;
            var h=s.History;var p=h.Polities[polity];var q=h.Polities[peer];var ownLeader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];var peerLeader=h.Dynamics.Leaders[h.Dynamics.States[peer].LeaderId];
            double ownAcceptance=HistoricalInternationalEffects.Acceptance(relation.Trust,ownLeader.Diplomacy,ownLeader.Ambition,p.Exhaustion,!trade),peerAcceptance=HistoricalInternationalEffects.Acceptance(relation.Trust,peerLeader.Diplomacy,peerLeader.Ambition,q.Exhaustion,!trade);
            double accept=HistoricalInternationalEffects.AgreementAcceptance(ownAcceptance,peerAcceptance,own>=0,other>=0);
            if(HistoryRoll("international-agreement-"+(int)kind+"-"+peer,polity,s.Years)>=1-Math.Exp(-.01*accept*elapsed))return;
            int invited=own>=0?peer:polity;int body=own>=0?own:other;
            if(body>=0)
            {
                var target=h.Polities[invited];long deposit=h.International.Finances==null?Math.Min(Math.Max(10,h.Aggregates.Polities[invited].Receipts/2),Math.Max(0,1000000000000L-h.International.Organizations[body].Reserve)):InternationalAccessionDeposit(s,invited,body);if(target.Treasury<deposit*3)return;
                target.Treasury-=deposit;h.International.Organizations[body].Reserve+=deposit;h.International.Organizations[body].Contributions+=deposit;JoinInternationalOrganization(s,invited,body);CreditInternationalReservePledge(s,invited,body,deposit);
                HistoryEvent(s,"treaty-accession",target.Name+" joins "+h.International.Organizations[body].Name+" after negotiations with a known partner and a contribution to its common fund.",s.Society.Settlements[target.CapitalId].Cell,target.CultureId,invited,category:ChronicleCategory.Politics);return;
            }
            long founding=Math.Max(50,Math.Min(h.Aggregates.Polities[polity].Receipts,h.Aggregates.Polities[peer].Receipts)*2),otherFounding=founding;
            if(h.International.Finances!=null){founding=HistoricalInternationalBudgetEffects.ReserveTarget(HistoricalInternationalBudgetEffects.Assessment(h.Aggregates.Polities[polity].Receipts));otherFounding=HistoricalInternationalBudgetEffects.ReserveTarget(HistoricalInternationalBudgetEffects.Assessment(h.Aggregates.Polities[peer].Receipts));if(founding<=0||otherFounding<=0)return;}
            if(p.Treasury<founding*3||q.Treasury<otherFounding*3)return;
            p.Treasury-=founding;q.Treasury-=otherFounding;int id=h.International.Organizations.Length;
            var organization=new HistoricalInternationalOrganization{Id=id,FounderPolity=polity,FoundedYear=s.Years,LastActionYear=s.Years,Kind=kind,Name=(trade?"League of ":"Congress of ")+s.Society.Settlements[p.CapitalId].Name,Reserve=founding+otherFounding,Contributions=founding+otherFounding};
            h.International.Organizations=h.International.Organizations.Concat(new[]{organization}).ToArray();if(h.International.Finances!=null)h.International.Finances.Organizations=h.International.Finances.Organizations.Concat(new[]{new HistoricalInternationalOperatingBudget()}).ToArray();JoinInternationalOrganization(s,polity,id);JoinInternationalOrganization(s,peer,id);CreditInternationalReservePledge(s,polity,id,founding);CreditInternationalReservePledge(s,peer,id,otherFounding);
            HistoryEvent(s,"international-foundation",p.Name+" and "+q.Name+" establish the "+organization.Name+(trade?" to coordinate commerce along their trade routes.":" to negotiate settlements after costly conflict."),s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,peer,category:ChronicleCategory.Politics);
        }
        double InternationalWarRestraint(PrehistoryState s,int a,int b)
        {int body=ActiveInternationalOrganization(s,a,HistoricalOrganizationKind.PeaceCongress);if(body<0||body!=ActiveInternationalOrganization(s,b,HistoricalOrganizationKind.PeaceCongress))return 1;var org=s.History.International.Organizations[body];return s.History.International.Finances==null?HistoricalInternationalEffects.WarRestraint(org.Authority,org.Reserve,org.Members.Count):1-.35*D.Clamp(org.Authority/1000.0)*InternationalFunding(s,body);}
        double InternationalTradeEfficiency(PrehistoryState s,int a,int b)
        {int body=ActiveInternationalOrganization(s,a,HistoricalOrganizationKind.TradeLeague);if(body<0||body!=ActiveInternationalOrganization(s,b,HistoricalOrganizationKind.TradeLeague))return 1;var org=s.History.International.Organizations[body];return s.History.International.Finances==null?HistoricalInternationalEffects.TradeEfficiency(org.Authority,org.Reserve,org.Members.Count):1+.12*D.Clamp(org.Authority/1000.0)*InternationalFunding(s,body);}
        void RegisterInternationalCampaign(PrehistoryState s,int attacker,int defender,int campaign)
        {
            var system=s.History.International;if(!LivingWorldHistory(s)||system==null)return;
            system.Accounts[attacker].LastWarYear=system.Accounts[defender].LastWarYear=s.Years;
            if(!aggregateRelations.TryGetValue((Math.Min(attacker,defender),Math.Max(attacker,defender)),out int relation))return;
            system.RelationCampaigns[relation]=campaign;
            int body=ActiveInternationalOrganization(s,attacker,HistoricalOrganizationKind.PeaceCongress);if(body>=0&&body==ActiveInternationalOrganization(s,defender,HistoricalOrganizationKind.PeaceCongress))
            {var org=system.Organizations[body];org.Authority=Math.Max(0,org.Authority-80);s.History.Relations[relation].Trust=Math.Max(-100,s.History.Relations[relation].Trust-20);HistoryEvent(s,"treaty-breach",s.History.Polities[attacker].Name+" goes to war with a fellow member of "+org.Name+", undermining its charter and mutual trust.",s.Society.Settlements[s.History.Polities[attacker].CapitalId].Cell,s.History.Polities[attacker].CultureId,attacker,defender,category:ChronicleCategory.Politics);}
        }
        void TryInternationalMediation(PrehistoryState s,int polity,int peer,int relation,int elapsed)
        {
            int body=ActiveInternationalOrganization(s,polity,HistoricalOrganizationKind.PeaceCongress);if(body<0||body!=ActiveInternationalOrganization(s,peer,HistoricalOrganizationKind.PeaceCongress))return;
            var h=s.History;var org=h.International.Organizations[body];var r=h.Relations[relation];int campaign=h.International.RelationCampaigns[relation];
            if(r.WarSince<0||campaign<0||org.Reserve<50||org.LastMediationYear>=s.Years)return;
            org.LastMediationYear=s.Years;long cost=Math.Min(org.Reserve,50);org.Reserve-=cost;org.MediationSpending+=cost;
            var a=h.Polities[polity];var b=h.Polities[peer];var leaderA=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];var leaderB=h.Dynamics.Leaders[h.Dynamics.States[peer].LeaderId];
            double chance=HistoricalInternationalEffects.MediationChance(r.Trust,a.Exhaustion,b.Exhaustion,leaderA.Diplomacy,leaderB.Diplomacy);
            if(HistoryRoll("international-mediation-"+peer,polity,s.Years)>=chance){org.FailedMediations++;org.Authority=Math.Max(0,org.Authority-10);return;}
            var war=h.Dynamics.Campaigns[campaign];if(war.EndedYear>=0)return;war.EndedYear=s.Years;war.Outcome="Both governments accept mediation through "+org.Name+"; the settlement does not erase local peoples or sovereignty.";
            ReleaseMilitaryOperation(s,campaign);
            r.WarSince=-1;r.TruceUntil=s.Years+10;r.Trust=Math.Min(100,r.Trust+10);org.Mediations++;org.Authority=Math.Min(1000,org.Authority+25);
            HistoryEvent(s,"international-mediation",org.Name+" mediates a ceasefire between "+a.Name+" and "+b.Name+". Both governments accept the negotiated proposal.",s.Society.Settlements[a.CapitalId].Cell,a.CultureId,polity,peer,category:ChronicleCategory.Politics);
        }
        void TryInternationalCoalitionEntry(PrehistoryState s,int polity,int ally,int elapsed,int trust)
        {
            var h=s.History;var system=h.International;var account=system.Accounts[ally];if(account.Relations.Count==0)return;var p=h.Polities[polity];
            if(p.Exhaustion>500||p.Cohesion<500||p.Army<100)return;
            int offset=(int)(HistoryRoll("international-ally-theater",polity,s.Years)*account.Relations.Count);
            for(int k=0;k<Math.Min(3,account.Relations.Count);k++)
            {
                int relation=account.Relations.At((offset+k)%account.Relations.Count);var warRelation=h.Relations.Peek(relation);if(warRelation.WarSince<0)continue;
                int enemy=InternationalPeer(s,ally,relation),origin=system.RelationCampaigns[relation];if(enemy<0||enemy==polity||origin<0||h.Dynamics.Campaigns[origin].EndedYear>=0)continue;
                if(HistoricalImperialHierarchy.Sovereign(s,polity)==HistoricalImperialHierarchy.Sovereign(s,enemy))continue;
                var target=AggregateRelation(s,polity,enemy);if(target.WarSince>=0||target.Alliance||target.TruceUntil>=s.Years)continue;
                long cost=Math.Max(100,p.Army/2);if(p.Treasury<cost*2)continue;
                bool defensive=h.Dynamics.Campaigns[origin].AttackerId!=ally;
                if(HistoryRoll("international-allied-entry-"+enemy,polity,s.Years)>=HistoricalInternationalEffects.CoalitionChance(elapsed,trust,p.Exhaustion,defensive))continue;
                int front=ProvincialWarFront(s,polity,enemy);if(front<0)continue;
                int next=h.Dynamics.Campaigns.Length;BeginAggregateCampaign(s,polity,enemy,target,front);if(h.Dynamics.Campaigns.Length==next)continue;
                p.Treasury-=cost;p.Exhaustion=Math.Min(1000,p.Exhaustion+35);system.Accounts[polity].CoalitionEntries++;
                system.CoalitionEntries=system.CoalitionEntries.Concat(new[]{new HistoricalCoalitionEntry{CampaignId=next,OriginCampaignId=origin,AllyId=ally,PolityId=polity,Year=s.Years}}).ToArray();
                h.Dynamics.Campaigns[next].Context="A linked front supporting "+h.Polities[ally].Name+" in "+h.Dynamics.Campaigns[origin].Name+". Each participant still requires its own forces, provisions and reachable theater.";
                HistoryEvent(s,"coalition-war",p.Name+" enters the conflict alongside "+h.Polities[ally].Name+", opening a new front against "+h.Polities[enemy].Name+".",front,p.CultureId,polity,enemy,category:ChronicleCategory.Politics);return;
            }
        }
        void AdvanceInternational(PrehistoryState s,int polity,int elapsed)
        {
            var h=s.History;var system=h.International;if(!LivingWorldHistory(s)||system==null)return;var account=system.Accounts[polity];
            foreach(var kind in new[]{HistoricalOrganizationKind.TradeLeague,HistoricalOrganizationKind.PeaceCongress})
            {
                int body=ActiveInternationalOrganization(s,polity,kind);if(body<0)continue;PayInternationalDues(s,polity,body);
                var org=system.Organizations[body];var leader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];
                if(ActiveInternationalOrganization(s,polity,kind)>=0&&org.Authority<350&&leader.Ambition>700&&s.Years-org.FoundedYear>25&&HistoryRoll("international-charter-exit-"+(int)kind,polity,s.Years)<1-Math.Exp(-.001*elapsed*(1-org.Authority/1000.0)))
                    LeaveInternationalOrganization(s,polity,body,"Its government rejects the weakened forum's constraints and resumes independent diplomacy.");
            }
            int count=account.Relations.Count;if(count==0)return;int offset=(int)(HistoryRoll("international-contacts",polity,s.Years)*count),allies=0;var considered=new int[8];int used=0;
            // Capture stable IDs first: removing stale bag entries swaps its
            // final slot, so iterating live modulo/counts would be unsafe.
            int sampled=Math.Min(8,count);var contacts=new int[sampled];for(int k=0;k<sampled;k++)contacts[k]=account.Relations.At((offset+k)%count);
            for(int k=0;k<sampled;k++)
            {
                int index=contacts[k],peer=InternationalPeer(s,polity,index);if(peer<0){account.Relations.Remove(index);continue;}
                bool duplicate=false;for(int n=0;n<used;n++)if(considered[n]==peer)duplicate=true;if(duplicate)continue;considered[used++]=peer;
                index=CurrentInternationalRelation(s,polity,peer,index);var r=h.Relations.Peek(index);
                if(r.WarSince>=0){TryInternationalMediation(s,polity,peer,index,elapsed);continue;}
                ConsiderInternationalOrganization(s,polity,peer,r,elapsed,HistoricalOrganizationKind.TradeLeague);
                ConsiderInternationalOrganization(s,polity,peer,r,elapsed,HistoricalOrganizationKind.PeaceCongress);
                if(r.Alliance&&++allies<=3)TryInternationalCoalitionEntry(s,polity,peer,elapsed,r.Trust);
            }
        }
        void ValidateInternational(PrehistoryState s)
        {
            var h=s.History;var system=h.International;if(system==null)return;
            void Require(bool condition,string description){if(!condition)throw new ArgumentException("Invalid international history: "+description);}
            Require(LivingWorldHistory(s)&&system.Version=="international-1"&&system.Accounts!=null&&system.Accounts.Length==h.Polities.Length&&system.RelationCampaigns?.Length==h.Relations.Length,"version/alignment");
            for(int k=0;k<system.Accounts.Length;k++)
            {
                var account=system.Accounts[k];Require(account!=null&&account.Id==k&&account.LastTradeDuesYear>=0&&account.LastTradeDuesYear<=s.Years&&account.LastPeaceDuesYear>=0&&account.LastPeaceDuesYear<=s.Years&&account.TradeArrearsYears>=0&&account.PeaceArrearsYears>=0&&account.CoalitionEntries>=0&&account.LastWarYear>=-1&&account.LastWarYear<=s.Years,"state ledger");
                Require(account.ContinuitySuccessor==-1||account.ContinuitySuccessor>k&&account.ContinuitySuccessor<system.Accounts.Length&&h.Polities[k].EndedYear>=0,"continuity successor");
                Require(account.Relations!=null&&account.Relations.Count>=0&&account.Relations.Count<=h.Relations.Length,"contact count");
                account.Relations.Items.Fingerprint(true);account.Relations.Positions.Fingerprint(true);
                for(int n=0;n<account.Relations.Count;n++){int relation=account.Relations.At(n);Require(relation>=0&&relation<h.Relations.Length&&account.Relations.Positions[relation]==n,"contact index");if(h.Polities[k].EndedYear<0){var record=h.Relations.Peek(relation);Require(h.Aggregates.Resolve(record.A,h)==k||h.Aggregates.Resolve(record.B,h)==k,"contact party");}}
                foreach(int organization in new[]{account.TradeOrganization,account.PeaceOrganization})Require(organization>=-1&&organization<system.Organizations.Length,"organization reference");
                if(account.TradeOrganization>=0)Require(system.Organizations[account.TradeOrganization].Kind==HistoricalOrganizationKind.TradeLeague,"trade membership type");
                if(account.PeaceOrganization>=0)Require(system.Organizations[account.PeaceOrganization].Kind==HistoricalOrganizationKind.PeaceCongress,"congress membership type");
                foreach(int organization in new[]{account.TradeOrganization,account.PeaceOrganization})if(organization>=0&&system.Organizations[organization].EndedYear<0)
                {var members=system.Organizations[organization].Members;int position=members.Positions[k];Require(h.Polities[k].EndedYear<0&&position>=0&&position<members.Count&&members.At(position)==k,"reciprocal member index");}
            }
            for(int k=0;k<system.Organizations.Length;k++)
            {
                var body=system.Organizations[k];Require(body!=null&&body.Id==k&&body.FounderPolity>=0&&body.FounderPolity<h.Polities.Length&&body.FoundedYear>=0&&body.FoundedYear<=s.Years&&body.EndedYear>=-1&&(body.EndedYear<0||body.EndedYear>=body.FoundedYear&&body.EndedYear<=s.Years)&&body.LastActionYear>=body.FoundedYear&&body.LastActionYear<=s.Years&&body.LastMediationYear>=-1&&body.LastMediationYear<=s.Years,"organization dates");
                Require(body.Reserve>=0&&body.Contributions>=0&&body.ServiceSpending>=0&&body.MediationSpending>=0&&body.Contributions==body.Reserve+body.ServiceSpending+body.MediationSpending&&body.Authority>=0&&body.Authority<=1000&&body.Mediations>=0&&body.FailedMediations>=0&&body.Members!=null&&body.Members.Count>=0&&body.Members.Count<=h.Polities.Length,"conserved organization budget");
                Require(body.EndedYear>=0||body.Members.Count>=2,"live multilateral membership");body.Members.Items.Fingerprint(true);body.Members.Positions.Fingerprint(true);
                for(int n=0;n<body.Members.Count;n++)
                {int member=body.Members.At(n);Require(member>=0&&member<h.Polities.Length&&body.Members.Positions[member]==n,"member index");if(body.EndedYear<0){var account=system.Accounts[member];Require(h.Polities[member].EndedYear<0&&(body.Kind==HistoricalOrganizationKind.TradeLeague?account.TradeOrganization:account.PeaceOrganization)==k,"live member alignment");}}
            }
            for(int k=0;k<system.RelationCampaigns.Length;k++){int campaign=system.RelationCampaigns[k];Require(campaign>=-1&&campaign<h.Dynamics.Campaigns.Length,"campaign index");}
            foreach(var link in system.CoalitionEntries)Require(link!=null&&link.CampaignId>=0&&link.CampaignId<h.Dynamics.Campaigns.Length&&link.OriginCampaignId>=0&&link.OriginCampaignId<link.CampaignId&&link.AllyId>=0&&link.AllyId<h.Polities.Length&&link.PolityId>=0&&link.PolityId<h.Polities.Length&&link.Year>=0&&link.Year<=s.Years,"linked campaign");
            ValidateInternationalFinances(s);
        }
    }
}

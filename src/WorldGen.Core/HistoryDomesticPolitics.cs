using System;
namespace WorldGen.Core
{
    // Five durable organized interests per government, not five exhaustive
    // castes or a census of individual party membership. Sparse immutable
    // pages keep one actor update independent of territorial size.
    [Serializable] public sealed class HistoricalDomesticPolitics
    {
        public int Version=1,SinceYear;
        public HistoricalCellTree Governments=new HistoricalCellTree(33554432);
        public HistoricalCellTree Groups=new HistoricalCellTree(33554432);
        public HistoricalDomesticPolitics Copy()=>new HistoricalDomesticPolitics{Version=Version,SinceYear=SinceYear,Governments=Governments,Groups=Groups};
    }
    public sealed class HistoricalPowerGroup
    {
        public int Kind,Founded,Influence,Organization,Grievance,Leader=-1,Seat=-1,Representation,Centralization,Priority,FoundingEthnicity=-1,FoundingFaith=-1,FoundingLanguage=-1;
        public string Name="",Interest="";public bool Governing;
    }
    public sealed class HistoricalDomesticProfile
    {
        public int Founded,LastYear,NextBargain,Coalition,Opposition,LastDecision,DecisionYear,Contests,LastContest,NegotiationCost;
        public HistoricalPowerGroup[] Groups=Array.Empty<HistoricalPowerGroup>();
    }
    public static class HistoricalDomesticEffects
    {
        public static readonly string[] Bases={"Landholder council","Trading and craft association","Officer circle","Religious council","Community assembly"};
        public static readonly string[] Priorities={"Defense","Civil works","Relief","Learning"};
        public static int Approach(int previous,int target,int elapsed,double years)
            =>Math.Max(0,Math.Min(1000,(int)Math.Round(previous+(target-previous)*(1-Math.Exp(-Math.Max(0,elapsed)/years)))));
        public static int[] Normalize(int[] weights)
        {
            long total=0;foreach(int w in weights)total+=Math.Max(0,w);var result=new int[weights.Length];if(total==0)return result;int used=0;
            for(int k=0;k<weights.Length-1;k++){result[k]=(int)(1000L*Math.Max(0,weights[k])/total);used+=result[k];}result[result.Length-1]=1000-used;return result;
        }
        public static double Opposition(HistoricalPowerGroup[] groups)
        {double sum=0;foreach(var g in groups)if(!g.Governing)sum+=g.Influence/1000.0*g.Organization/1000.0*D.Clamp((g.Grievance-250)/650.0);return D.Q(D.Clamp(sum),1000000000);}
        public static double ContestHazard(double opposition,int legitimacy,int exhaustion,int representation,bool armed)
            =>!armed?0:D.Q(.035*D.Clamp((opposition-.2)/.5)*D.Clamp((450-legitimacy)/350.0)*(.4+.6*D.Clamp(exhaustion/800.0))*(1-.75*D.Clamp(representation/1000.0)),1000000000);
        public static int Delay(double roll)=>4+(int)(9*Math.Min(.999999,D.Clamp(roll)));
        public static string Decision(int kind)=>kind==1?"Negotiated settlement":kind==2?"Proposal defeated":kind==3?"Assembly deferred: insufficient funds":kind==4?"Contested transfer of office":"No settlement recorded yet";
    }
    public sealed partial class Prehistory
    {
        const int DomesticGovernmentFields=12,DomesticGroupFields=12;
        static bool DomesticHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=37;
        void InitializeDomesticPolitics(PrehistoryState s)
        {
            if(!DomesticHistory(s))return;var realm=s.History.Aggregates.Empires.Connections.Realms;
            if(realm.Domestic!=null)return;realm.Domestic=new HistoricalDomesticPolitics{SinceYear=s.Years};
            // Explicit migration only. No old factions, votes or coups invented.
            foreach(var state in s.History.Polities)if(state.EndedYear<0)EnsureDomesticGovernment(s,state.Id);
        }
        void EnsureDomesticGovernment(PrehistoryState s,int state)
        {
            if(!DomesticHistory(s))return;var d=s.History.Aggregates.Empires.Connections.Realms.Domestic;if(d==null)return;
            int at=state*DomesticGovernmentFields;if(d.Governments[at]>=0)return;var h=s.History;var p=h.Polities[state];var policy=h.Aggregates.Communities.Governments[state];
            // Use the old derived profile only as a prospective founding prior.
            var profile=PoliticalProfile(s,state);int mask=0,best=0;
            for(int k=1;k<5;k++)if(profile.InfluencePermille[k]>profile.InfluencePermille[best])best=k;
            mask|=1<<best;int second=best==2?0:2;mask|=1<<second;
            d.Governments=d.Governments.WithBlock(at,new[]{s.Years,s.Years,s.Years+HistoricalDomesticEffects.Delay(HistoryRoll("domestic-first-assembly-37",state,s.Years)),mask,0,0,s.Years,0,-1,0,0,0});
            for(int k=0;k<5;k++)
            {
                int rep=k==4?750:k==1?550:k==2?150:k==0?300:250;
                int central=k==0?250:k==1?450:k==2?800:k==3?500:350;
                // Ideals are inherited local practices with overlapping variation,
                // not a universal path from estates to modern parties.
                rep=(rep+policy.RepresentationMilli)/2+(int)(160*(HistoryRoll("domestic-voice-37",state*5+k,s.Years)-.5));
                central=(central+policy.CentralizationMilli)/2+(int)(160*(HistoryRoll("domestic-center-37",state*5+k,s.Years)-.5));
                int priority=k==2?0:k==4?2:k==3?3:1;
                var towns=h.Aggregates.Polities[state].Cities;int seat=k==0&&towns.Count>1?towns.At((int)(HistoryRoll("domestic-founding-seat-37",state,s.Years)*towns.Count)):p.CapitalId;
                int cell=s.Society.Settlements[seat].Cell;
                d.Groups=d.Groups.WithBlock((state*5+k)*DomesticGroupFields,new[]{s.Years,profile.InfluencePermille[k],150,100,-1,seat,rep,central,priority,PrimaryEthnicityAt(s,cell),HistoricalCells.Religion(s,cell),PrimarySpokenLanguageAt(s,cell)});
            }
        }
        public HistoricalDomesticProfile DomesticProfile(PrehistoryState s,int state)
        {
            var d=s?.History?.Aggregates?.Empires?.Connections?.Realms?.Domestic;if(!DomesticHistory(s)||d==null||state<0||state>=s.History.Polities.Length)return null;
            int at=state*DomesticGovernmentFields;if(d.Governments[at]<0)return null;var result=new HistoricalDomesticProfile{Founded=d.Governments[at],LastYear=d.Governments[at+1],NextBargain=d.Governments[at+2],Coalition=d.Governments[at+3],Opposition=d.Governments[at+4],LastDecision=d.Governments[at+5],DecisionYear=d.Governments[at+6],Contests=d.Governments[at+7],LastContest=d.Governments[at+8],NegotiationCost=d.Governments[at+9],Groups=new HistoricalPowerGroup[5]};
            for(int k=0;k<5;k++)
            {
                int b=(state*5+k)*DomesticGroupFields,seat=d.Groups[b+5];string place=seat>=0&&seat<s.Society.Settlements.Length?s.Society.Settlements[seat].Name:s.History.Polities[state].Name;
                result.Groups[k]=new HistoricalPowerGroup{Kind=k,Founded=d.Groups[b],Influence=d.Groups[b+1],Organization=d.Groups[b+2],Grievance=d.Groups[b+3],Leader=d.Groups[b+4],Seat=seat,Representation=d.Groups[b+6],Centralization=d.Groups[b+7],Priority=d.Groups[b+8],FoundingEthnicity=d.Groups[b+9],FoundingFaith=d.Groups[b+10],FoundingLanguage=d.Groups[b+11],Governing=(result.Coalition&(1<<k))!=0,Name=place+" "+HistoricalDomesticEffects.Bases[k].ToLowerInvariant(),Interest=k==0?"Local privileges, land revenues and provincial bargaining":k==1?"Safe exchange, craft livelihoods and predictable administration":k==2?"Military provisioning, officers' authority and security":k==3?"Institutional endowments, public worship and religious autonomy":"Subsistence security, local voice and relief"};
            }
            return result;
        }
        HistoricalPoliticalProfile DomesticPoliticalProfile(PrehistoryState s,int state,HistoricalPoliticalProfile fallback)
        {
            var d=DomesticProfile(s,state);if(d==null)return fallback;
            int[] priorities={100,100,100,100};
            for(int k=0;k<5;k++)
            {
                var g=d.Groups[k];fallback.Blocs[k]=g.Name;fallback.InfluencePermille[k]=g.Influence;
                priorities[g.Priority]+=g.Influence*(g.Governing?3:1);
            }
            var shares=HistoricalDomesticEffects.Normalize(priorities);fallback.DefensePermille=shares[0];fallback.WorksPermille=shares[1];fallback.ReliefPermille=shares[2];fallback.LearningPermille=shares[3];
            return fallback;
        }
        double DomesticOpposition(PrehistoryState s,int state)
        {var d=s?.History?.Aggregates?.Empires?.Connections?.Realms?.Domestic;return !DomesticHistory(s)||d==null?0:Math.Max(0,d.Governments[state*DomesticGovernmentFields+4])/1000.0;}
        void AdvanceDomesticPolitics(PrehistoryState s,int state,int elapsed)
        {
            EnsureDomesticGovernment(s,state);var view=DomesticProfile(s,state);if(view==null)return;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[state];var policy=a.Communities.Governments[state];var ds=h.Dynamics.States[state];var ledger=a.Polities[state];var d=a.Empires.Connections.Realms.Domestic;
            int at=state*DomesticGovernmentFields,years=s.Years-view.LastYear;if(years<=0)return;
            var culture=h.Cultures[p.CultureId];int cell=s.Society.Settlements[p.CapitalId].Cell;
            double urban=D.Clamp(ledger.Urban/Math.Max(1.0,p.Population)*4),commerce=D.Clamp(.5*HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Coinage,s.Years)+.5*HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Sailing,s.Years));
            double literacy=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Printing,s.Years),deficit=D.Clamp((ledger.ServiceCost-ledger.Receipts)/Math.Max(1.0,ledger.ServiceCost));
            bool faith=(ReligiousCommunity(s,cell)?.Institution??0)>=100;
            var capital=s.Society.Settlements[p.CapitalId];bool shortage=capital.FoodReserve<capital.Population/2;
            var program=PoliticalProfile(s,state);int[] programShares={program.DefensePermille,program.WorksPermille,program.ReliefPermille,program.LearningPermille};
            int[] strengths={(int)(350+350*(1-urban)+.3*(ds.Cycle?.EliteCapture??0)),(int)(80+500*commerce+200*urban),p.Army<=0?0:(int)(150+400*D.Clamp(p.Army/Math.Max(1.0,p.Population*.03))+p.Exhaustion*.3),faith?450:40,(int)(60+.6*policy.RepresentationMilli+250*literacy)};
            var normalized=HistoricalDomesticEffects.Normalize(strengths);int[] blended=new int[5];
            for(int k=0;k<5;k++)blended[k]=HistoricalDomesticEffects.Approach(view.Groups[k].Influence,normalized[k],years,80);
            blended=HistoricalDomesticEffects.Normalize(blended);
            for(int k=0;k<5;k++)
            {
                var g=view.Groups[k];int b=(state*5+k)*DomesticGroupFields;g.Influence=blended[k];
                double capacity=k==2?(p.Army>0?.6:0):k==3?(faith?.65:.05):k==1?commerce:k==4?policy.RepresentationMilli/1000.0*.5+literacy*.4:.45;
                g.Organization=HistoricalDomesticEffects.Approach(g.Organization,(int)(1000*D.Clamp(capacity)),years,40);
                double disagreement=(Math.Abs(policy.RepresentationMilli-g.Representation)+Math.Abs(policy.CentralizationMilli-g.Centralization))/2000.0;
                double hardship=k==2?deficit*.5+p.Exhaustion/2000.0:k==4?(shortage?.5:0)+deficit*.25: k==1?p.Exhaustion/2500.0+deficit*.3:k==0?policy.CentralizationMilli/4000.0:ReligiousLocalPoliticalGrievance(s,cell,state);
                if(k==0||k==4)hardship+=.20*(1-a.Communities.Cities[g.Seat].InclusionMilli/1000.0);
                double neglectedPriority=.25*D.Clamp((300-programShares[g.Priority])/300.0);
                g.Grievance=HistoricalDomesticEffects.Approach(g.Grievance,(int)(1000*D.Clamp(.1+.65*disagreement+hardship+neglectedPriority+(g.Governing?-.12:.12))),years,30);
                int desiredVoice=k==4?(int)(650+250*literacy):k==1?(int)(350+300*literacy):k==2?150:k==0?300:250;
                g.Representation=HistoricalDomesticEffects.Approach(g.Representation,desiredVoice,years,400);
                d.Groups=d.Groups.With(b+6,g.Representation);
                // Fixed seats: existing specialists and one rotating real town,
                // never a search through a nation's people or settlements.
                int seat=k==0&&ledger.Cities.Count>1?ledger.Cities.At((int)(HistoryRoll("domestic-local-seat-37",state,s.Years)*ledger.Cities.Count)):p.CapitalId;
                if(g.Seat<0||a.Resolve(s.Society.Settlements[g.Seat].PolityId,h)!=state||s.Society.Settlements[g.Seat].Population<=0)g.Seat=seat;
                int slot=k==2?1:k==3?5:k==1?0:4,candidate=h.Notables?.Towns[g.Seat*8+slot]??-1;
                if(!NotableAlive(s,g.Leader)||h.Notables.People.Peek(g.Leader).State!=state)g.Leader=NotableAlive(s,candidate)&&h.Notables.People.Peek(candidate).State==state?candidate:-1;
                d.Groups=d.Groups.WithBlock(b+1,new[]{g.Influence,g.Organization,g.Grievance,g.Leader,g.Seat});
            }
            int opposition=(int)Math.Round(1000*HistoricalDomesticEffects.Opposition(view.Groups));
            d.Governments=d.Governments.With(at+1,s.Years).With(at+4,opposition);
            if(s.Years>=view.NextBargain)BargainDomesticCoalition(s,state,view);
            d.Governments=d.Governments.With(at+4,(int)Math.Round(1000*HistoricalDomesticEffects.Opposition(DomesticProfile(s,state).Groups)));
            ConsiderDomesticOfficeContest(s,state,years);
        }
        void BargainDomesticCoalition(PrehistoryState s,int state,HistoricalDomesticProfile view)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[state];var ds=h.Dynamics.States[state];var leader=h.Dynamics.Leaders[ds.LeaderId];var policy=a.Communities.Governments[state];var d=a.Empires.Connections.Realms.Domestic;int at=state*DomesticGovernmentFields;
            int cost=(int)Math.Min(100000,25+a.Polities[state].Urban/1000);int next=s.Years+HistoricalDomesticEffects.Delay(HistoryRoll("domestic-next-assembly-37",state,s.Years));
            d.Governments=d.Governments.With(at+2,next).With(at+6,s.Years).With(at+9,0);
            if(p.Treasury<cost){d.Governments=d.Governments.With(at+5,3);return;}
            p.Treasury-=cost;d.Governments=d.Governments.With(at+9,cost);
            // A challenge must mobilize organized influence, not merely have a
            // large nominal population. Durable allies remain attractive.
            int best=0;double bestValue=-1;
            for(int k=0;k<5;k++){var g=view.Groups[k];double score=g.Influence*(.3+.7*g.Organization/1000.0)*(g.Governing?1.2:1)*(1+.4*g.Grievance/1000.0);if(score>bestValue){best=k;bestValue=score;}}
            var anchor=view.Groups[best];int mask=1<<best,weight=anchor.Influence;
            for(int turn=0;turn<4&&weight<550;turn++)
            {
                int chosen=-1;double value=-1;
                for(int k=0;k<5;k++)if((mask&(1<<k))==0){var g=view.Groups[k];double affinity=1-(Math.Abs(g.Representation-anchor.Representation)+Math.Abs(g.Centralization-anchor.Centralization))/2000.0;double score=g.Influence*(.2+.8*affinity)*(g.Governing?1.15:1);if(score>value){chosen=k;value=score;}}
                mask|=1<<chosen;weight+=view.Groups[chosen].Influence;
            }
            double desiredRep=0,desiredCentral=0,total=0,backing=0;
            for(int k=0;k<5;k++)if((mask&(1<<k))!=0){var g=view.Groups[k];total+=g.Influence;desiredRep+=g.Influence*g.Representation;desiredCentral+=g.Influence*g.Centralization;backing+=g.Influence*(.3+.7*g.Organization/1000.0);}
            double chance=D.Clamp(.25+.4*backing/1000.0+.25*leader.Diplomacy/1000.0+.15*p.Legitimacy/1000.0-.25*p.Exhaustion/1000.0);
            bool accepted=HistoryRoll("domestic-assembly-accept-37",state,s.Years)<chance;
            d.Governments=d.Governments.With(at+5,accepted?1:2);
            if(!accepted)return;
            int oldRep=policy.RepresentationMilli,oldCentral=policy.CentralizationMilli;
            policy.RepresentationMilli=Math.Max(50,Math.Min(950,oldRep+(int)Math.Round(Math.Max(-30,Math.Min(30,(desiredRep/Math.Max(1,total)-oldRep)*.2)))));
            policy.CentralizationMilli=Math.Max(150,Math.Min(950,oldCentral+(int)Math.Round(Math.Max(-30,Math.Min(30,(desiredCentral/Math.Max(1,total)-oldCentral)*.2)))));
            policy.LastReformYear=s.Years;d.Governments=d.Governments.With(at+3,mask);
            for(int k=0;k<5;k++)if((mask&(1<<k))!=0){int b=(state*5+k)*DomesticGroupFields;d.Groups=d.Groups.With(b+3,Math.Max(0,d.Groups[b+3]-50));}
            p.Legitimacy=Math.Min(950,p.Legitimacy+10);RefreshPoliticalDescription(s,state);
            if(mask!=view.Coalition||Math.Abs(policy.RepresentationMilli-oldRep)>=15)
            {
                int ev=h.Archive.Count;string text=p.Name+" reaches a financed settlement led by the "+anchor.Name+" over public spending, local privileges and access to government.";
                HistoryEvent(s,"domestic-settlement",text,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,state,category:ChronicleCategory.Politics);
                if(NotableAlive(s,anchor.Leader))NotableDeed(s,anchor.Leader,new HistoricalPersonDeed{Year=s.Years,Event=ev,Kind="Institutional bargaining",Text=text},10);
            }
        }
        void ConsiderDomesticOfficeContest(PrehistoryState s,int state,int elapsed)
        {
            var v=DomesticProfile(s,state);var h=s.History;var p=h.Polities[state];var a=h.Aggregates;var policy=a.Communities.Governments[state];var g=v.Groups[2];
            if(v.LastContest>=0&&s.Years-v.LastContest<25||g.Governing||g.Organization<450||g.Grievance<600||!NotableAlive(s,g.Leader))return;
            var candidate=h.Notables.People.Peek(g.Leader);if(candidate.CurrentCampaign>=0&&h.Dynamics.Campaigns[candidate.CurrentCampaign].EndedYear<0)return;
            if(g.Leader==RulerPersonId(s,h.Dynamics.States[state].LeaderId)||s.Years-candidate.BirthYear<18)return;
            double hazard=HistoricalDomesticEffects.ContestHazard(v.Opposition/1000.0,p.Legitimacy,p.Exhaustion,policy.RepresentationMilli,p.Army>=100);
            if(HistoryRoll("domestic-office-contest-37",state,s.Years)>=1-Math.Exp(-hazard*elapsed))return;
            // Actual command and public solvency are necessary. Spending pays
            // organization; no new army, treasury, state, people or annexation.
            long cost=100+p.Army/10;if(p.Treasury<cost)return;p.Treasury-=cost;
            var d=a.Empires.Connections.Realms.Domestic;int at=state*DomesticGovernmentFields;
            d.Governments=d.Governments.With(at+7,v.Contests+1).With(at+8,s.Years).With(at+5,4).With(at+6,s.Years).With(at+9,(int)Math.Min(int.MaxValue,cost));
            int old=h.Dynamics.States[state].LeaderId;var former=h.Dynamics.Leaders[old];former.EndedYear=s.Years;former.Legacy="Removed from office by an organized officer coalition; this is not the ruler's death.";SealAggregateLeader(s,old);
            var person=h.Notables.People[g.Leader];person.Roles|=HistoricalPersonRoles.Ruler;h.Notables.Save(person);
            var leader=new HistoricalLeader{Id=h.Dynamics.Leaders.Length,PolityId=state,PredecessorId=old,StartedYear=s.Years,ExpectedEndYear=s.Years+HistoricalRealmEffects.RulerTenure(HistoryRoll("domestic-contested-tenure-37",state,s.Years)),Name=person.Name,Military=person.Skills[0],Administration=person.Skills[4],Diplomacy=person.Skills[3],Ambition=650,Selection="Acclamation by an organized officer coalition after a funded contest for central office"};
            h.Dynamics.Leaders.Add(leader);h.Dynamics.States[state].LeaderId=leader.Id;h.Dynamics.States[state].LastSuccession=s.Years;p.Ruler=leader.Name;p.RulerSince=s.Years;a.Polities[state].SuccessionDue=leader.ExpectedEndYear;
            h.Notables.Leaders=h.Notables.Leaders.With(leader.Id,g.Leader);RegisterNotableRuler(s,state,leader,false);SealAggregateLeader(s,leader.Id);
            d.Governments=d.Governments.With(at+3,(1<<2)|(1<<0));
            d.Governments=d.Governments.With(at+4,(int)Math.Round(1000*HistoricalDomesticEffects.Opposition(DomesticProfile(s,state).Groups)));
            var realm=a.Empires.Connections.Realms.Governments[state];realm.RegimeSince=s.Years;realm.RegimeChanges++;realm.CrisisUntil=Math.Max(realm.CrisisUntil,s.Years+8);realm.CrisisSeverity=Math.Max(realm.CrisisSeverity,Math.Max(450,v.Opposition));h.Dynamics.States[state].DisputedUntil=realm.CrisisUntil;
            p.Legitimacy=Math.Max(100,p.Legitimacy-60);
            int ev=h.Archive.Count;string text=person.Name+"'s officer coalition removes "+former.Name+" from office in "+p.Name+". The country and its existing resources continue; provincial institutions can contest the new court.";
            HistoryEvent(s,"domestic-office-contest",text,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,state,category:ChronicleCategory.Politics);
            NotableDeed(s,person.Id,new HistoricalPersonDeed{Year=s.Years,Event=ev,Kind="Transfer of government",Text=text},30);
        }
        void PrepareDomesticSuccession(PrehistoryState s,int state,HistoricalLeader leader)
        {
            var view=DomesticProfile(s,state);if(view==null||s.History.Aggregates.Communities.Governments[state].RepresentationMilli<650)return;
            int candidate=-1,best=-1,former=RulerPersonId(s,s.History.Dynamics.States[state].LeaderId);
            foreach(var g in view.Groups)
            {
                if(!g.Governing||g.Leader==former||!NotableAlive(s,g.Leader))continue;var person=s.History.Notables.People.Peek(g.Leader);
                if(s.Years-person.BirthYear<18||person.CurrentCampaign>=0&&s.History.Dynamics.Campaigns[person.CurrentCampaign].EndedYear<0)continue;
                int score=g.Influence*g.Organization/1000;if(score>best){best=score;candidate=g.Leader;}
            }
            if(candidate<0)return;var p=s.History.Notables.People.Peek(candidate);leader.Name=p.Name;leader.Military=p.Skills[0];leader.Administration=p.Skills[4];leader.Diplomacy=p.Skills[3];
            leader.Selection="Selection of an existing public figure by the governing institutional coalition";s.History.Notables.Leaders=s.History.Notables.Leaders.With(leader.Id,candidate);
        }
        void ValidateDomesticPolitics(PrehistoryState s)
        {
            var d=s.History.Aggregates.Empires.Connections.Realms.Domestic;if(d==null){if(DomesticHistory(s))throw new ArgumentException("Missing domestic politics.");return;}
            if(!DomesticHistory(s)||d.Version!=1||d.SinceYear<0||d.SinceYear>s.Years||d.Governments?.Length!=33554432||d.Groups?.Length!=33554432)throw new ArgumentException("Invalid domestic political ledger.");
            if(!d.Governments.AllPresentValues((k,v)=>
            {
                int state=k/12,f=k%12;if(state>=s.History.Polities.Length)return false;int start=d.Governments[state*12];
                if(start<d.SinceYear||start>s.Years)return false;
                if(f==0||f==1||f==6)return v>=start&&v<=s.Years;if(f==2)return v>=start&&v<=s.Years+12;if(f==3)return v>0&&v<32;if(f==4)return v>=0&&v<=1000;if(f==5)return v>=0&&v<=4;if(f==8)return v>=start&&v<=s.Years;return v>=0;
            })||!d.Groups.AllPresentValues((k,v)=>
            {
                int state=k/(5*12),f=k%12;if(state>=s.History.Polities.Length||d.Governments[state*12]<0)return false;
                if(f==0)return v>=d.SinceYear&&v<=s.Years;if(f==4)return v>=0&&v<s.History.Notables.People.Count;if(f==5)return v>=0&&v<s.Society.Settlements.Length;if(f==8)return v>=0&&v<4;
                if(f==9)return v>=0&&v<s.History.Identities.Peoples.Length;if(f==10)return v>=0&&v<s.History.Religions.Length;if(f==11)return v>=0&&v<s.History.Languages.Spoken.Varieties.Length;return v>=0&&v<=1000;
            }))throw new ArgumentException("Invalid domestic political record.");
            foreach(var p in s.History.Polities)if(d.Governments[p.Id*12]>=0){int total=0;for(int k=0;k<5;k++)total+=d.Groups[(p.Id*5+k)*12+1];if(total!=1000)throw new ArgumentException("Political influence must reconcile.");}
        }
    }
}

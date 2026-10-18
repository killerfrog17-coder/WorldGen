using System;
namespace WorldGen.Core
{
    public enum HistoricalSovereigntyRequest { Protection, Independence, Withdrawal }
    public enum HistoricalSovereigntyOutcome { Pending, Accepted, Reconciled, Refused, Superseded, Expired }
    public enum HistoricalSovereigntyCause { AdministrativeReassignment, LocalFoundation, Conquest, NegotiatedProtection, Secession, AdministrativeWithdrawal, SettlementAbandonment, SponsoredSettlement, FrontierAdministration }
    [Serializable] public sealed class HistoricalSovereigntyHistory
    {
        public int Version=1,SinceYear;
        // Latest episode per participating town. Earlier episodes remain in the archive.
        public HistoricalCellTree Cases=new HistoricalCellTree(33554432),Transfers=new HistoricalCellTree(33554432);
        public HistoricalSovereigntyHistory Copy()=>new HistoricalSovereigntyHistory{Version=Version,SinceYear=SinceYear,Cases=Cases,Transfers=Transfers};
    }
    public sealed class HistoricalSovereigntyCase
    {
        public int Owner=-1,Candidate=-1,StartedYear,ReviewedYear,NextYear,Support,OfferedRights,ResolvedYear=-1,NegotiationPaid;
        public HistoricalSovereigntyRequest Request;
        public HistoricalSovereigntyOutcome Outcome;
    }
    public sealed class HistoricalSovereigntyTransfer
    {
        public int From=-1,To=-1,Year,Campaign=-1,Rights,DisputeSince=-1;
        public HistoricalSovereigntyCause Cause;
    }
    public static class HistoricalSovereigntyEffects
    {
        public static double Consent(int inclusion,int offeredRights,int rememberedRights,int attributedHarm,double affinity,double religiousGrievance)
            =>D.Q(D.Clamp(.2+.35*D.Clamp(inclusion/1000.0)+.25*D.Clamp(offeredRights/1000.0)+.2*D.Clamp(affinity)
                -.5*D.Clamp(attributedHarm/1000.0)-.45*D.Clamp((rememberedRights-offeredRights)/1000.0)-D.Clamp(religiousGrievance)),1000000000);
        public static bool CanSecede(bool disconnected,int support,double crisis,int pressure,int cohesion)
            =>disconnected||support>=600&&(crisis>=.25||pressure>=650&&cohesion<450);
        public static int ReviewDelay(double roll)=>2+(int)(7*Math.Min(.999999,D.Clamp(roll)));
    }
    public sealed partial class Prehistory
    {
        static bool SovereigntyHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=34;
        void InitializeSovereigntyHistory(PrehistoryState s)
        {
            if(!SovereigntyHistory(s))return;var r=s.History.Aggregates.Empires.Connections.Realms;
            if(r.Sovereignty==null)r.Sovereignty=new HistoricalSovereigntyHistory{SinceYear=s.Years};
        }
        public static HistoricalSovereigntyCase SovereigntyCaseAt(PrehistoryState s,int city)
        {
            if(!SovereigntyHistory(s)||city<0||city>=s.Society.Settlements.Length)return null;
            var t=s.History.Aggregates.Empires.Connections.Realms.Sovereignty?.Cases;int k=city*12;if(t==null||t[k+3]<0)return null;
            return new HistoricalSovereigntyCase{Owner=t[k],Candidate=t[k+1],Request=(HistoricalSovereigntyRequest)t[k+2],StartedYear=t[k+3],ReviewedYear=t[k+4],NextYear=t[k+5],Support=t[k+6],OfferedRights=t[k+7],ResolvedYear=t[k+8],Outcome=(HistoricalSovereigntyOutcome)t[k+9],NegotiationPaid=Math.Max(0,t[k+10])};
        }
        public static HistoricalSovereigntyTransfer SovereigntyTransferAt(PrehistoryState s,int city)
        {
            if(!SovereigntyHistory(s)||city<0||city>=s.Society.Settlements.Length)return null;
            var t=s.History.Aggregates.Empires.Connections.Realms.Sovereignty?.Transfers;int k=city*8;if(t==null||t[k+2]<0)return null;
            return new HistoricalSovereigntyTransfer{From=t[k],To=t[k+1],Year=t[k+2],Cause=(HistoricalSovereigntyCause)t[k+3],Campaign=t[k+4],Rights=Math.Max(0,t[k+5]),DisputeSince=t[k+6]};
        }
        void StoreSovereigntyCase(PrehistoryState s,int city,HistoricalSovereigntyCase p)
        {
            var r=s.History.Aggregates.Empires.Connections.Realms.Sovereignty;
            r.Cases=r.Cases.WithBlock(city*12,new[]{p.Owner,p.Candidate,(int)p.Request,p.StartedYear,p.ReviewedYear,p.NextYear,p.Support,p.OfferedRights,p.ResolvedYear,(int)p.Outcome,p.NegotiationPaid,-1});
        }
        void RecordSovereigntyTransfer(PrehistoryState s,int city,int from,int to,HistoricalSovereigntyCause cause,int campaign=-1,int rights=0,int dispute=-1)
        {
            if(!SovereigntyHistory(s)||from==to)return;var r=s.History.Aggregates.Empires.Connections.Realms.Sovereignty;
            r.Transfers=r.Transfers.WithBlock(city*8,new[]{from,to,s.Years,(int)cause,campaign,rights,dispute,-1});
        }
        void RecordSovereigntyGovernmentTransfer(PrehistoryState s,int city,int from,int to)
        {
            if(!SovereigntyHistory(s))return;
            var cause=to<0?(s.Society.Settlements[city].Population<=0?HistoricalSovereigntyCause.SettlementAbandonment:HistoricalSovereigntyCause.AdministrativeWithdrawal)
                :s.History.Polities[to].FoundedYear==s.Years&&s.History.Polities[to].CapitalId==city?HistoricalSovereigntyCause.LocalFoundation
                :from<0?s.Society.Settlements[city].FoundedYear==s.Years?HistoricalSovereigntyCause.SponsoredSettlement:HistoricalSovereigntyCause.FrontierAdministration:HistoricalSovereigntyCause.AdministrativeReassignment;
            RecordSovereigntyTransfer(s,city,from,to,cause);
        }
        void RecordSovereigntyConquest(PrehistoryState s,int city,int winner,int campaign)
        {
            if(!SovereigntyHistory(s)||campaign<0||campaign>=s.History.Dynamics.Campaigns.Length||HistoricalCells.Polity(s,s.Society.Settlements[city].Cell)!=winner)return;
            var war=s.History.Dynamics.Campaigns[campaign];if(winner!=war.A&&winner!=war.B)return;
            var previous=SovereigntyTransferAt(s,city);
            if(previous==null||previous.To!=winner||previous.Year!=s.Years)return;
            RecordSovereigntyTransfer(s,city,previous.From,winner,HistoricalSovereigntyCause.Conquest,campaign);
            var p=SovereigntyCaseAt(s,city);if(p!=null&&p.Outcome==HistoricalSovereigntyOutcome.Pending)ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Superseded,"is overtaken by an actual military capture");
        }
        bool BeginSovereigntyCase(PrehistoryState s,int city,int owner,int candidate,HistoricalSovereigntyRequest request)
        {
            if(!SovereigntyHistory(s))return false;var old=SovereigntyCaseAt(s,city);
            if(old!=null&&(old.Outcome==HistoricalSovereigntyOutcome.Pending||s.Years-old.ResolvedYear<20))return true;
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];
            if(town.Population<300||a.Living.Cities[city].AbandonedYear>=0||HistoricalCells.Polity(s,town.Cell)!=owner||candidate==owner)return false;
            var existingClaim=HistoricalRealmClaimantHistory.At(s,city);
            int support=HistoricalRealmClaimantHistory.Active(s,existingClaim)?existingClaim.SupportMilli:0;
            if(request==HistoricalSovereigntyRequest.Independence&&owner>=0&&support<180&&a.Cities[city].Supported&&town.Population>=1200&&town.Stability>=200
                &&(h.Towns[city].AutonomyPressure>=650||RealmCrisis(s,owner)>=.25))
            {
                var claims=a.Empires.Connections.Realms.Claimants;var coalition=HistoricalRealmClaimantHistory.At(s,city);
                if(coalition==null){coalition=new HistoricalRealmLocalClaimant{City=city};int index=claims.Records.Length;Array.Resize(ref claims.Records,index+1);claims.Records[index]=coalition;claims.At=claims.At.With(city,index);}
                coalition.Owner=owner;coalition.DeclaredYear=coalition.LastYear=s.Years;coalition.CrisisSinceYear=a.Empires.Connections.Realms.Governments[owner].RegimeSince;
                coalition.ResolvedYear=coalition.SuccessorPolity=-1;coalition.Outcome="";coalition.SupportMilli=Math.Min(850,400+h.Towns[city].AutonomyPressure/3);NameRealmClaimant(s,coalition);support=coalition.SupportMilli;
            }
            if(request==HistoricalSovereigntyRequest.Independence&&owner>=0)
            {
                double best=double.MaxValue;
                foreach(int peer in a.Cities[city].Neighbors)
                {
                    var other=s.Society.Settlements[peer];int state=a.Resolve(other.PolityId,h);
                    if(state<0||state==owner||h.Polities[state].ParentId!=owner||s.Years-h.Polities[state].FoundedYear>50||!a.Cities[peer].Supported||a.Living.Cities[peer].AbandonedYear>=0)continue;
                    double km=LivingTravelKm(s,other.Cell,town.Cell,town.CultureId);
                    if(km<best&&km<Math.Max(180,a.Polities[state].ReachKm*2.0)){candidate=state;best=km;}
                }
                if(candidate>=0)request=HistoricalSovereigntyRequest.Protection;
            }
            var p=new HistoricalSovereigntyCase{Owner=owner,Candidate=candidate,Request=request,StartedYear=s.Years,ReviewedYear=s.Years,
                NextYear=s.Years+HistoricalSovereigntyEffects.ReviewDelay(HistoryRoll("sovereignty-first-review-34",city,s.Years)),Support=support,
                OfferedRights=candidate>=0?CurrentLocalRights(s,candidate):650};
            StoreSovereigntyCase(s,city,p);
            string action=request==HistoricalSovereigntyRequest.Protection?"seeks a protection agreement with "+h.Polities[candidate].Name:request==HistoricalSovereigntyRequest.Independence?"disputes the existing court's authority and organizes a local governing claim":"questions obligations to its inaccessible court";
            HistoryEvent(s,"sovereignty-petition",town.Name+" "+action+". No allegiance or border has changed.",town.Cell,town.CultureId,owner,candidate,city,category:ChronicleCategory.Politics);
            return true;
        }
        void ResolveSovereigntyCase(PrehistoryState s,int city,HistoricalSovereigntyCase p,HistoricalSovereigntyOutcome outcome,string reason)
        {
            p.Outcome=outcome;p.ResolvedYear=p.ReviewedYear=s.Years;StoreSovereigntyCase(s,city,p);
            var t=s.Society.Settlements[city];HistoryEvent(s,"sovereignty-decision",t.Name+"'s local sovereignty dispute "+reason+".",t.Cell,t.CultureId,p.Owner,p.Candidate,city,category:ChronicleCategory.Politics);
        }
        bool SovereigntyDetached(PrehistoryState s,int owner,int city)
        {
            if(owner<0)return true;var p=s.History.Polities[owner];int capital=s.Society.Settlements[p.CapitalId].Cell,cell=s.Society.Settlements[city].Cell;
            if(CourtSeaEffort(s,owner,capital,cell)<double.MaxValue)return false;
            return CourtLandConnection(s,owner,capital,cell)==HistoricalCourtConnection.Blocked;
        }
        bool SovereigntyReceiverReachable(PrehistoryState s,int city,int candidate)
        {
            var h=s.History;var a=h.Aggregates;if(candidate<0||candidate>=h.Polities.Length||h.Polities[candidate].EndedYear>=0)return false;
            var p=h.Polities[candidate];int capital=s.Society.Settlements[p.CapitalId].Cell,cell=s.Society.Settlements[city].Cell;
            bool peer=false;foreach(int id in a.Cities[city].Neighbors)
                if(a.Resolve(s.Society.Settlements[id].PolityId,h)==candidate&&a.Cities[id].Supported&&a.Living.Cities[id].AbandonedYear<0){peer=true;break;}
            if(!peer||p.Legitimacy<400||p.Cohesion<400||a.Polities[candidate].Receipts<a.Polities[candidate].ServiceCost)return false;
            if(CourtSeaEffort(s,candidate,capital,cell)<double.MaxValue)return true;
            return CourtFrontierWithinCore(s,candidate,LivingTravelKm(s,capital,cell,p.CultureId))&&CourtLandConnection(s,candidate,capital,cell)==HistoricalCourtConnection.Connected;
        }
        bool AdvanceSovereigntyCase(PrehistoryState s,int city)
        {
            var p=SovereigntyCaseAt(s,city);if(p==null||p.Outcome!=HistoricalSovereigntyOutcome.Pending)return false;
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];int owner=HistoricalCells.Polity(s,town.Cell);
            if(owner!=p.Owner||town.Population<300||a.Living.Cities[city].AbandonedYear>=0)
            {ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Superseded,"ends because its community or governing circumstances have changed");return true;}
            if(s.Years<p.NextYear)return true;
            if(s.Years-p.StartedYear>60){ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Expired,"lapses without an enforceable agreement or successful separation");return true;}
            p.ReviewedYear=s.Years;p.NextYear=s.Years+HistoricalSovereigntyEffects.ReviewDelay(HistoryRoll("sovereignty-review-34",city,s.Years));
            bool detached=SovereigntyDetached(s,owner,city);
            if(owner>=0&&!detached&&a.Cities[city].Supported&&a.Communities.Cities[city].InclusionMilli>=700&&RealmCrisis(s,owner)<=0)
            {ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Reconciled,"is reconciled as reliable administration and local inclusion restore the existing court's authority");return true;}
            if(p.Request==HistoricalSovereigntyRequest.Protection)
            {
                var localClaimant=HistoricalRealmClaimantHistory.At(s,city);
                p.Support=HistoricalRealmClaimantHistory.Active(s,localClaimant)?localClaimant.SupportMilli:0;
                bool rebellion=owner>=0&&p.Candidate>=0&&h.Polities[p.Candidate].ParentId==owner
                    &&HistoricalSovereigntyEffects.CanSecede(false,p.Support,RealmCrisis(s,owner),h.Towns[city].AutonomyPressure,h.Polities[owner].Cohesion);
                if(!SovereigntyReceiverReachable(s,city,p.Candidate)||owner>=0&&!detached&&!rebellion)
                {ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Refused,"finds no reachable, solvent protection agreement that can supersede the existing administration");return true;}
                var receiver=h.Polities[p.Candidate];long cost=100+town.Population/50;
                if(receiver.Treasury<cost){ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Refused,"fails because the proposed protector cannot finance the agreement");return true;}
                if(p.NegotiationPaid==0){receiver.Treasury-=cost;p.NegotiationPaid=1;}
                p.OfferedRights=CurrentLocalRights(s,p.Candidate);var memory=LocalHistoricalMemoryAt(s,city);
                int harm=memory!=null&&a.Resolve(memory.WrongingState,h)==p.Candidate?memory.Harm:0;
                double consent=HistoricalSovereigntyEffects.Consent(a.Communities.Cities[city].InclusionMilli,p.OfferedRights,memory?.LocalRights??650,harm,1-LocalIdentityDifference(s,city,p.Candidate),ReligiousLocalPoliticalGrievance(s,town.Cell,p.Candidate));
                if(HistoryRoll("sovereignty-local-consent-34",city,s.Years)>=consent)
                {ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Refused,"rejects the proposed protector's terms; local identity alone does not transfer allegiance");return true;}
                ClaimAggregatePolity(s,town.Cell,p.Candidate);RecordSovereigntyTransfer(s,city,owner,p.Candidate,rebellion?HistoricalSovereigntyCause.Secession:HistoricalSovereigntyCause.NegotiatedProtection,rights:p.OfferedRights,dispute:p.StartedYear);
                if(rebellion)JoinRealmClaimantGovernment(s,owner,city,p.Candidate);
                ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Accepted,"accepts a financed protection agreement with "+receiver.Name+(rebellion?" through its organized local secession coalition":" after local negotiation, rather than military conquest"));
                if(rebellion&&!detached)ConsiderSovereigntyRestoration(s,owner,p.Candidate,town.Cell);return true;
            }
            var claimant=HistoricalRealmClaimantHistory.At(s,city);p.Support=claimant!=null&&HistoricalRealmClaimantHistory.Active(s,claimant)?claimant.SupportMilli:0;
            if(p.Request==HistoricalSovereigntyRequest.Withdrawal&&detached)
            {
                ClaimAggregatePolity(s,town.Cell,-1);QueueTerritorialServiceRecovery(s,city);RecordSovereigntyTransfer(s,city,owner,-1,HistoricalSovereigntyCause.AdministrativeWithdrawal,dispute:p.StartedYear);
                ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Accepted,"ends in loss of effective administration; the settlement is neither annexed by its neighbor nor automatically made a state");return true;
            }
            if(owner>=0&&a.Cities[city].Supported&&town.Population>=1200&&h.Cultures[town.CultureId].Institution>=350
                &&HistoricalSovereigntyEffects.CanSecede(detached,p.Support,RealmCrisis(s,owner),h.Towns[city].AutonomyPressure,h.Polities[owner].Cohesion))
            {
                // The existing foundation divides actual treasury/levies. It grants
                // neither a province sweep nor a free independence army.
                FoundAggregateState(s,city,owner);int successor=town.PolityId;
                RecordSovereigntyTransfer(s,city,owner,successor,HistoricalSovereigntyCause.Secession,dispute:p.StartedYear);
                ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Accepted,"establishes the local successor court of "+h.Polities[successor].Name+(detached?" after losing effective connection to its former center":" through an organized secession"));
                a.Empires.Connections.Realms.Governments[owner].RegionalSeparations++;
                if(!detached)ConsiderSovereigntyRestoration(s,owner,successor,town.Cell);
                return true;
            }
            StoreSovereigntyCase(s,city,p);return true;
        }
        void ConsiderSovereigntyRestoration(PrehistoryState s,int owner,int successor,int cell)
        {
            var h=s.History;var p=h.Polities[owner];
            if(p.EndedYear>=0||p.Army<=30||p.Treasury<100||p.Exhaustion>=700)return;
            var relation=AggregateRelation(s,owner,successor);
            if(relation.WarSince<0){p.Treasury-=100;BeginAggregateCampaign(s,owner,successor,relation,cell);}
        }
        void RecordSovereigntyAbandonment(PrehistoryState s,int city,int owner)
        {
            if(!SovereigntyHistory(s))return;
            RecordSovereigntyTransfer(s,city,owner,-1,HistoricalSovereigntyCause.SettlementAbandonment);
            var p=SovereigntyCaseAt(s,city);if(p!=null&&p.Outcome==HistoricalSovereigntyOutcome.Pending)
                ResolveSovereigntyCase(s,city,p,HistoricalSovereigntyOutcome.Superseded,"ends as its inhabitants leave the settlement, rather than accepting an unrelated protector");
        }
        void ValidateSovereigntyHistory(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.Realms.Sovereignty;
            if(r==null){if(SovereigntyHistory(s))throw new ArgumentException("Missing local sovereignty ledger.");return;}
            if(!SovereigntyHistory(s)||r.Version!=1||r.SinceYear<0||r.SinceYear>s.Years||r.Cases?.Length!=33554432||r.Transfers?.Length!=33554432)throw new ArgumentException("Invalid local sovereignty extension.");
            bool State(int v)=>v>=0&&v<s.History.Polities.Length;
            if(!r.Cases.AllPresentValues((k,v)=>
            {
                int f=k%12,city=k/12;if(city>=s.Society.Settlements.Length||r.Cases[city*12+3]<r.SinceYear)return false;
                int start=r.Cases[city*12+3];
                if(f==3)
                {
                    int b=city*12,request=r.Cases[b+2],outcome=r.Cases[b+9],resolved=r.Cases[b+8];
                    if(r.Cases[b]==r.Cases[b+1]||request<0||outcome<0||r.Cases[b+4]<start||r.Cases[b+5]<start||r.Cases[b+6]<0||r.Cases[b+7]<0||r.Cases[b+10]<0)return false;
                    if(request==0?r.Cases[b+1]<0:r.Cases[b]<0||r.Cases[b+1]>=0)return false;
                    if(outcome==0?resolved>=0:resolved<start)return false;
                }
                if(f<=1)return State(v);if(f==2)return v>=0&&v<=2;if(f==3)return v<=s.Years;if(f==4||f==8)return v>=start&&v<=s.Years;
                if(f==5)return v>=start&&v<=s.Years+8;if(f==6||f==7)return v>=0&&v<=1000;if(f==9)return v>=0&&v<=5;return f==10&&v>=0&&v<=1;
            })||!r.Transfers.AllPresentValues((k,v)=>
            {
                int f=k%8,city=k/8;if(city>=s.Society.Settlements.Length||r.Transfers[city*8+2]<r.SinceYear)return false;
                if(f==2)
                {
                    int b=city*8,cause=r.Transfers[b+3];
                    if(r.Transfers[b]==r.Transfers[b+1]||cause<0||r.Transfers[b+5]<0)return false;
                    if(cause==(int)HistoricalSovereigntyCause.Conquest&&(r.Transfers[b+4]<0||r.Transfers[b+1]<0))return false;
                }
                if(f<=1)return State(v);if(f==2||f==6)return v>=r.SinceYear&&v<=s.Years;if(f==3)return v>=0&&v<=8;
                if(f==4)return v>=0&&v<s.History.Dynamics.Campaigns.Length;return f==5&&v>=0&&v<=1000;
            }))throw new ArgumentException("Invalid local sovereignty episode.");
        }
    }
}

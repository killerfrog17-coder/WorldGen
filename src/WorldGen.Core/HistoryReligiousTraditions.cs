using System;
using System.Linq;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        const int BodyFaith=0,BodySeat=1,BodyForm=2,BodyAutonomy=3,BodySupport=4,BodySince=5,BodyReview=6,BodyTension=7,BodyDissent=8,BodyReform=9,BodyPatron=10,BodyContactSeat=11,BodyContactYear=12,BodySeparated=13,BodyLoyalty=14;
        static bool StructuredReligion(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Religion?.Traditions!=null;
        public static HistoricalReligiousDoctrine ReligiousDoctrineAt(PrehistoryState s,int faith)
        {
            var d=s?.History?.Aggregates?.Empires?.Connections?.Religion?.Traditions?.Doctrines;
            return d!=null&&faith>=0&&faith<d.Length?d[faith].Copy():null;
        }
        void InitializeStructuredReligions(PrehistoryState s)
        {
            if(s.History.Aggregates.Empires.Connections.Realms.Version<31)return;
            var r=s.History.Aggregates.Empires.Connections.Religion;
            if(r.Traditions!=null)return;
            r.Traditions=new HistoricalReligiousTraditions{SinceYear=s.Years};
            // Explicit upgrade/setup only: no ordinary world/territory walk.
            for(int id=0;id<s.History.Religions.Length;id++)AppendReligiousDoctrine(s,id,ReligiousIssue.None,-1);
        }
        void AppendReligiousDoctrine(PrehistoryState s,int id,ReligiousIssue issue,int donor)
        {
            if(!StructuredReligion(s))return;var h=s.History;var r=h.Aggregates.Empires.Connections.Religion.Traditions;
            if(id<r.Doctrines.Length)return;
            if(id!=r.Doctrines.Length)throw new InvalidOperationException("Religious doctrine IDs must be appended in order.");
            var faith=h.Religions[id];HistoricalReligiousDoctrine profile;
            if(faith.ParentId>=0)
            {
                if(faith.ParentId>=id)throw new ArgumentException("Invalid faith ancestry.");
                profile=ReligiousDoctrines.Descendant(r.Doctrines[faith.ParentId],id,issue,donor>=0?r.Doctrines[donor]:null);
            }
            else profile=ReligiousDoctrines.Root(id,k=>HistoryRoll("religion-doctrine-31-"+k,id,0));
            r.Doctrines=r.Doctrines.Concat(new[]{profile}).ToArray();
            AppendCelestialFaith(s,id);
        }
        public static ReligiousInstitutionView ReligiousInstitutionAt(PrehistoryState s,int cell,int slot=0)
        {
            if(!StructuredReligion(s)||cell<0||cell>=HistoricalCells.Count(s)||slot<0||slot>1)return null;
            var a=s.History.Aggregates;int town=a.TownAt[cell];
            if(town<0||a.Living.Cities[town].AbandonedYear>=0)return null;
            var tree=a.Empires.Connections.Religion.Traditions.Institutions;int key=(town*2+slot)*HistoricalReligiousTraditions.InstitutionFields;
            if(tree[key]<0)return null;
            return new ReligiousInstitutionView{Town=town,Slot=slot,Faith=tree[key],Seat=tree[key+BodySeat],Form=(ReligiousInstitutionForm)tree[key+BodyForm],Independence=tree[key+BodyAutonomy],Support=Math.Max(0,tree[key+BodySupport]),SinceYear=tree[key+BodySince],ReviewedYear=tree[key+BodyReview],Tension=Math.Max(0,tree[key+BodyTension]),Dissent=Math.Max(0,tree[key+BodyDissent]),Patron=tree[key+BodyPatron],SeparatedYear=tree[key+BodySeparated]};
        }
        void RecordStructuredReligiousContact(PrehistoryState s,int cell,int source,int faith)
        {
            if(!StructuredReligion(s))return;
            int town=s.History.Aggregates.TownAt[cell];if(town<0)return;
            var r=s.History.Aggregates.Empires.Connections.Religion.Traditions;
            for(int slot=0;slot<2;slot++)
            {
                var body=ReligiousInstitutionAt(s,source,slot);if(body==null||body.Faith!=faith||body.Support<400)continue;
                int key=(town*2+slot)*HistoricalReligiousTraditions.InstitutionFields;if(r.Institutions[key]<0)continue;
                r.Institutions=r.Institutions.WithBlock(key+BodyContactSeat,new[]{body.Seat,s.Years});
            }
        }
        void AdvanceStructuredReligiousInstitutions(PrehistoryState s,int cell,int city,int owner,int policy,bool officialsPaid)
        {
            if(!StructuredReligion(s)||city<0)return;var h=s.History;var a=h.Aggregates;
            if(a.Living.Cities[city].AbandonedYear>=0)return;
            var network=a.Empires.Connections.Religion;var r=network.Traditions;var town=s.Society.Settlements[city];
            for(int slot=0;slot<2;slot++)
            {
                var lay=slot==1?LayAssociationAt(s,cell):null;
                int faith=slot==0?HistoricalCells.Religion(s,cell):lay!=null&&lay.Active?lay.Faith:-1;
                int support=slot==0?Math.Max(0,network.Read(cell,FaithInstitution)):lay?.Support??0;
                int key=(city*2+slot)*HistoricalReligiousTraditions.InstitutionFields;
                if(faith<0||support<100&&r.Institutions[key]<0)continue;
                bool fresh=r.Institutions[key]<0||r.Institutions[key]!=faith;
                if(fresh)
                {
                    // Doctrine does not give a ready-made international church.
                    // This body's support comes from the existing paid ledger.
                    var profile=r.Doctrines[faith];
                    int formerFaith=r.Institutions[key];
                    if(formerFaith>=0&&formerFaith!=faith)HistoryEvent(s,"religious-institutional-change",town.Name+" reorganizes its "+(slot==0?"public ritual body":"lay association")+" from "+h.Religions[formerFaith].Name+" to "+h.Religions[faith].Name+". The preceding institution's authority is not inherited automatically.",cell,town.CultureId,owner,town:city,religion:faith);
                    r.Institutions=r.Institutions.WithBlock(key,new[]{faith,city,(int)(slot==1?ReligiousInstitutionForm.TeachingFellowship:profile.Organization),slot==1?850:500,support,s.Years,s.Years,0,0,s.Years,owner,-1,-1,-1,0});
                    continue;
                }
                int elapsed=s.Years-r.Institutions[key+BodyReview];if(elapsed<=0)continue;
                var doctrine=r.Doctrines[faith];int court=owner>=0?h.Polities[owner].ReligionId:-1;
                var difference=court>=0?ReligiousDoctrines.Compare(doctrine,r.Doctrines[court]):new ReligiousComparison();
                bool restricted=owner>=0&&court!=faith&&policy==2&&(officialsPaid||slot==1&&lay.Dispute==ReligiousAssociationDispute.SuppressedWorship);
                double grievance=ReligiousDoctrines.LocalGrievance(difference,policy,restricted,owner>=0?a.Communities.Cities[city].InclusionMilli:1000,support);
                int tension=(int)Math.Round(Math.Max(0,r.Institutions[key+BodyTension])+(grievance*1000-Math.Max(0,r.Institutions[key+BodyTension]))*(1-Math.Exp(-elapsed/40.0)));
                bool captured=owner>=0&&court==faith&&CapturedReligiousEndowment(s,owner,faith,town.Stability)&&policy!=0;
                if(captured)tension=Math.Max(tension,(int)Math.Round(250*doctrine.ClericalAuthority/1000.0*(1-doctrine.RulerSanctity/1000.0)));
                int dissent=Math.Max(0,Math.Min(1000,Math.Max(0,r.Institutions[key+BodyDissent])+(int)Math.Round(elapsed*(captured?3:restricted?1+tension/200.0:-2))));
                int seat=r.Institutions[key+BodySeat],autonomy=Math.Max(0,r.Institutions[key+BodyAutonomy]),loyalty=Math.Max(0,r.Institutions[key+BodyLoyalty]);
                int contact=r.Institutions[key+BodyContactSeat],contactYear=r.Institutions[key+BodyContactYear];
                bool linked=contact>=0&&contact!=city&&contact<s.Society.Settlements.Length&&contactYear>=0&&s.Years-contactYear<=80&&a.Living.Cities[contact].AbandonedYear<0;
                if(linked)
                {
                    var authority=ReligiousInstitutionAt(s,s.Society.Settlements[contact].Cell,slot);
                    bool communion=authority!=null&&authority.Support>=400&&authority.Faith==faith;
                    loyalty=communion?Math.Min(1000,loyalty+(int)(elapsed*(.5+doctrine.ClericalAuthority/400.0))):Math.Max(0,loyalty-elapsed*2);
                    // Two local bodies can share doctrine without surrendering
                    // authority. Centralization requires actual continuing contact.
                    if(communion&&doctrine.ClericalAuthority>=600&&loyalty>=600&&dissent<400&&contact<city)
                    {
                        if(seat!=authority.Seat)HistoryEvent(s,"religious-communion",town.Name+" recognizes the ritual authority at "+s.Society.Settlements[authority.Seat].Name+" through repeated maintained religious contact. Doctrine and state allegiance remain unchanged.",cell,town.CultureId,owner,town:city,religion:faith);
                        seat=authority.Seat;autonomy=Math.Max(100,autonomy-elapsed);
                        if(slot==0)r.Institutions=r.Institutions.With(authority.Seat*2*HistoricalReligiousTraditions.InstitutionFields+BodyForm,(int)ReligiousInstitutionForm.CentralSee);
                    }
                }
                else loyalty=Math.Max(0,loyalty-elapsed*2);
                bool seatLost=seat<0||seat>=s.Society.Settlements.Length||a.Living.Cities[seat].AbandonedYear>=0;
                if(seat!=city&&(seatLost||dissent>=600||loyalty==0))
                {
                    seat=city;autonomy=850;r.Institutions=r.Institutions.With(key+BodySeparated,s.Years);
                    HistoryEvent(s,"religious-autonomy",town.Name+" maintains "+h.Religions[faith].Name+" but its provisioned ritual body resumes local authority. This is institutional separation, not a new doctrine or an automatic territorial secession.",cell,town.CultureId,owner,town:city,religion:faith);
                }
                var fields=new int[HistoricalReligiousTraditions.InstitutionFields];for(int field=0;field<fields.Length;field++)fields[field]=r.Institutions[key+field];
                fields[BodySeat]=seat;fields[BodyAutonomy]=autonomy;fields[BodySupport]=support;fields[BodyReview]=s.Years;fields[BodyTension]=tension;fields[BodyDissent]=dissent;fields[BodyPatron]=owner;fields[BodyLoyalty]=loyalty;r.Institutions=r.Institutions.WithBlock(key,fields);
                if(slot==0&&owner>=0&&tension>0)
                {
                    // A bounded local consequence, not a faith-difference
                    // secession lottery or a bonus to martial effectiveness.
                    town.Stability=Math.Max(0,town.Stability-(int)Math.Round(Math.Min(12,elapsed*tension/3000.0)));
                }
            }
        }
        double ReligiousLocalPoliticalGrievance(PrehistoryState s,int cell,int owner)
        {
            if(!StructuredReligion(s)||owner<0)return 0;
            var body=ReligiousInstitutionAt(s,cell);var lay=ReligiousInstitutionAt(s,cell,1);
            return Math.Min(.20,Math.Max(body==null?0:body.Tension/5000.0,lay==null?0:lay.Tension/10000.0));
        }
        void TryStructuredReligiousBranch(PrehistoryState s,int cell,int town,int owner,int faith,int institution,int elapsed,bool disputed,bool paid,bool ritual,int visitor)
        {
            if(!disputed||!paid)return;var h=s.History;var network=h.Aggregates.Empires.Connections.Religion;var r=network.Traditions;var lay=network.LayAssociations;
            int key=cell*HistoricalLayAssociations.Fields;
            if(lay.Records[key+LayCommitment]<600||HistoricalLayAssociationEffects.OrganizingCapacity(lay.Records[key+LaySupport],institution,HistoricalCells.Religion(s,cell)==faith)<600)return;
            int since=Math.Max(h.Religions[faith].FoundedYear,network.Read(cell,FaithReformed));
            if(s.Years-since<400||s.Years-lay.Records[key+LaySince]<80)return;
            var profile=r.Doctrines[faith];ReligiousIssue issue;
            if(ritual&&visitor>=0)issue=ReligiousDoctrines.MainIssue(profile,r.Doctrines[visitor]);
            else issue=profile.RulerSanctity>=400?ReligiousIssue.CourtAuthority:profile.ClericalAuthority>=400?ReligiousIssue.ClericalAuthority:profile.Charity<700?ReligiousIssue.MutualAid:ReligiousIssue.None;
            if(issue==ReligiousIssue.None)return;
            // A long-running funded authority dispute can stay an institutional
            // disagreement indefinitely. A distinct teaching is a rarer outcome.
            if(HistoryRoll("religion-specific-teaching-31",town,s.Years)>=1-Math.Exp(-elapsed/1200.0))return;
            var t=s.Society.Settlements[town];long cost=Math.Max(10,t.Population/50);
            if(t.FoodReserve-t.Population/2<cost)return;t.FoodReserve-=cost;
            int id=h.Religions.Length;var parent=h.Religions[faith];
            var renewal=new HistoricalReligion{Id=id,ParentId=faith,CultureId=t.CultureId,HolyCell=cell,FoundedYear=s.Years,Name=LanguageName(s,t.CultureId,LanguageNameKind.Religion,id),Reach=parent.Reach==ReligiousReach.Local?ReligiousReach.Local:ReligiousReach.Portable,
                Ethos=parent.Ethos,Ritual=parent.Ritual,Tradition="A separately provisioned teaching descended from "+parent.Name+", revising "+ReligiousDoctrines.Name(issue).ToLowerInvariant()+" after a sustained local dispute"};
            h.Religions=h.Religions.Concat(new[]{renewal}).ToArray();AppendReligiousDoctrine(s,id,issue,ritual?visitor:-1);
            lay.Records=lay.Records.With(key,id).With(key+LaySince,s.Years).With(key+LayCommitment,0).With(key+LayDispute,0);network.Write(cell,FaithReformed,s.Years);
            HistoryEvent(s,"religious-schism",renewal.Name+" establishes a distinct teaching on "+ReligiousDoctrines.Name(issue).ToLowerInvariant()+" at "+t.Name+". It retains the inherited cosmology and other unrevised practices of "+parent.Name+". The local public tradition and political allegiance do not automatically change.",cell,t.CultureId,owner,town:town,religion:id);
            NotableReligiousFigure(s,town,id,h.Archive.Count-1);
        }
        void ApplyReligiousDiplomaticDifference(PrehistoryState s,int first,int second,int elapsed,ref double risk)
        {
            if(!StructuredReligion(s))return;var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;var profiles=r.Traditions.Doctrines;
            int a=h.Polities[first].ReligionId,b=h.Polities[second].ReligionId;if(a<0||b<0)return;
            var difference=ReligiousDoctrines.Compare(profiles[a],profiles[b]);
            // Strong disagreement becomes diplomatic antagonism principally
            // when courts enforce exclusion; difference alone does not start war.
            int pa=r.Policies[first*4+1],pb=r.Policies[second*4+1];
            double enforcement=(pa==2?1:0)+(pb==2?1:0);
            var relation=AggregateRelation(s,first,second);
            int penalty=(int)Math.Round(difference.Tension/1000.0*enforcement*Math.Min(4,elapsed/5.0));
            relation.Trust=Math.Max(-100,relation.Trust-penalty);risk*=(1+.4*difference.Tension/1000.0*enforcement)*(1-.30*profiles[a].Nonviolence/1000.0);
        }
        void ValidateStructuredReligions(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.Religion?.Traditions;
            if(r==null){if(s.History.Aggregates.Empires.Connections.Realms.Version>=31)throw new ArgumentException("Structured religion requires saved doctrines and bodies.");return;}
            var h=s.History;
            if(r.Version!=1||r.SinceYear<0||r.SinceYear>s.Years||r.Doctrines?.Length!=h.Religions.Length||r.Institutions?.Length!=33554432)throw new ArgumentException("Invalid structured religion extension.");
            for(int i=0;i<r.Doctrines.Length;i++)
            {
                var p=r.Doctrines[i];int parent=h.Religions[i].ParentId;
                if(p==null||p.Id!=i||p.RootId<0||p.RootId>i||p.SecondaryParent< -1||p.SecondaryParent>=i||p.Generation<0||p.Generation>i||!Enum.IsDefined(typeof(ReligiousBranch),p.Branch)||!Enum.IsDefined(typeof(ReligiousIssue),p.Issue)||!Enum.IsDefined(typeof(ReligiousCosmology),p.Cosmology)||!Enum.IsDefined(typeof(ReligiousAfterlife),p.Afterlife)||!Enum.IsDefined(typeof(ReligiousRevelation),p.Revelation)||!Enum.IsDefined(typeof(ReligiousInstitutionForm),p.Organization)||p.Axes().Any(v=>v<0||v>1000)||p.CalendarDays<350||p.CalendarDays>366||p.FestivalDay<1||p.FestivalDay>p.CalendarDays||p.RestDays<4||p.RestDays>10)throw new ArgumentException("Invalid doctrinal profile.");
                if(parent<0?(p.RootId!=i||p.Generation!=0):(p.RootId!=r.Doctrines[parent].RootId||p.Generation!=r.Doctrines[parent].Generation+1))throw new ArgumentException("Invalid compiled faith ancestry.");
            }
            if(!r.Institutions.AllPresentValues((key,value)=>
            {
                int body=key/HistoricalReligiousTraditions.InstitutionFields,town=body/2,field=key%HistoricalReligiousTraditions.InstitutionFields;
                if(town>=s.Society.Settlements.Length)return false;
                if(field==BodyFaith)return value>=0&&value<h.Religions.Length;
                if(field==BodySeat||field==BodyContactSeat)return value>=0&&value<s.Society.Settlements.Length;
                if(field==BodyPatron)return value>=0&&value<h.Polities.Length;
                if(field==BodyForm)return value>=0&&value<=5;
                if(field==BodySince||field==BodyReview||field==BodyReform||field==BodyContactYear||field==BodySeparated)return value>=r.SinceYear&&value<=s.Years;
                return value>=0&&value<=1000;
            }))throw new ArgumentException("Invalid religious institution ledger.");
        }
    }
}

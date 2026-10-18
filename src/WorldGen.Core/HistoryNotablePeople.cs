using System;
using System.Linq;
namespace WorldGen.Core
{
    [Flags] public enum HistoricalPersonRoles { None=0,Ruler=1,RoyalFamily=2,Commander=4,Scientist=8,Inventor=16,Artist=32,Philosopher=64,Theologian=128,ReligiousTeacher=256,PropheticTeacher=512,Administrator=1024 }
    [Serializable] public sealed class HistoricalPersonDeed
    {
        public int Year,Event=-1,Campaign=-1,Technology=-1,Level,Faith=-1;
        public string Kind="",Text="";
        public HistoricalPersonDeed Copy()=>(HistoricalPersonDeed)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalNotablePerson
    {
public int Id,BirthYear,RecordedYear,DeathYear=-1,ExpectedDeathYear,OriginCell,Cell,Culture,State=-1,Faith=-1,Ethnicity=-1,Language=-1,Leader=-1,House=-1,Parent1=-1,Parent2=-1,Spouse=-1,Mentor=-1,Patron=-1,Regent=-1,MinorityUntil,Sex,Renown,LastDeedYear=-1,Project=-1,CurrentCampaign=-1;
        public long ResearchWork,BirthSeconds,DeathSeconds;
        public bool BirthEstimated,LifeOffice;
        public string Name="",HouseName="";
        public HistoricalPersonRoles Roles,Interests;
        // Military, scholarship, craft/art, rhetoric, administration, spiritual inquiry.
        public int[] Skills=new int[6],Children=Array.Empty<int>();
        public HistoricalPersonDeed[] Deeds=Array.Empty<HistoricalPersonDeed>();
        public HistoricalNotablePerson Copy(){var p=(HistoricalNotablePerson)MemberwiseClone();p.Skills=(int[])Skills.Clone();p.Children=(int[])Children.Clone();p.Deeds=Deeds.Select(d=>d.Copy()).ToArray();return p;}
    }
    [Serializable] public sealed class HistoricalNotableHistory
    {
        public int Version=1,SinceYear;
        public HistoricalPersonCollection People=new HistoricalPersonCollection();
        public HistoricalCellTree Digests=new HistoricalCellTree(33554432),Leaders=new HistoricalCellTree(33554432),Courts=new HistoricalCellTree(33554432),Towns=new HistoricalCellTree(33554432),Review=new HistoricalCellTree(33554432),FaithFounders=new HistoricalCellTree(33554432),Commands=new HistoricalCellTree(33554432);
        public HistoricalNotableHistory Copy()=>new HistoricalNotableHistory{Version=Version,SinceYear=SinceYear,People=People.Copy(),Digests=Digests,Leaders=Leaders,Courts=Courts,Towns=Towns,Review=Review,FaithFounders=FaithFounders,Commands=Commands};
        // Year-1 is odd: the power-of-two stride is coprime and IDs below
        // the tree capacity have distinct offsets within a mortality year.
        public static long DeathOffset(int id,long yearSalt)=>1+((id*1048576L+yearSalt)%(SimulationTime.Year-1));
        internal void Save(HistoricalNotablePerson p,bool append=false)
        {
            if(append)People.Add(p);else People.Replace(p.Id,p);
            string digest=StateHash.Compute(p);var values=new int[8];for(int k=0;k<8;k++)values[k]=unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16));Digests=Digests.WithBlock(p.Id*8,values);
        }
        internal void Verify()
        {
            foreach(var t in new[]{Digests,Leaders,Courts,Towns,Review,FaithFounders,Commands})t.Fingerprint(true);
            for(int i=0;i<People.Count;i++){string digest=StateHash.Compute(People.Peek(i));for(int k=0;k<8;k++)if(Digests[i*8+k]!=unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16)))throw new ArgumentException("Modified notable biography.");}
        }
    }
    public sealed partial class Prehistory
    {
        const int PersonProcess=28;
        static bool NotableHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=35&&s.History.Notables!=null;
        public static HistoricalNotablePerson NotablePersonAt(PrehistoryState s,int id)=>NotableHistory(s)&&id>=0&&id<s.History.Notables.People.Count?s.History.Notables.People[id]:null;
        public static int RulerPersonId(PrehistoryState s,int leader)=>NotableHistory(s)&&leader>=0?s.History.Notables.Leaders[leader]:-1;
        bool NotableAlive(PrehistoryState s,int id)=>id>=0&&id<s.History.Notables.People.Count&&s.History.Notables.People.Peek(id).DeathYear<0&&s.History.Notables.People.Peek(id).DeathSeconds>SimulationTime.Seconds(s);
        bool NotableLifeOfficeStillActive(PrehistoryState s,int leader)
        {
            int id=RulerPersonId(s,leader);return id>=0&&NotableAlive(s,id)&&s.History.Notables.People.Peek(id).LifeOffice;
        }
        void InitializeNotablePeople(PrehistoryState s)
        {
            if(s.History.Aggregates.Empires.Connections.Realms.Version<35||s.History.Notables!=null)return;
            s.History.Notables=new HistoricalNotableHistory{SinceYear=s.Years};
            if(s.History.Polities.Length==0)return;
            // Prospective upgrade: current office-holders only, no invented old family tree.
            bool installed=aggregateQueue==null;var queue=installed?new SimulationEventQueue(s.Timeline.Events):aggregateQueue;
            if(installed)aggregateQueue=queue;
            try{foreach(var p in s.History.Polities)if(p.EndedYear<0&&s.History.Dynamics.States[p.Id].LeaderId>=0)RegisterNotableRuler(s,p.Id,s.History.Dynamics.Leaders[s.History.Dynamics.States[p.Id].LeaderId],false);}
            finally{if(installed){s.Timeline.Events=queue.Ordered();aggregateQueue=null;}}
        }
        int CreateNotable(PrehistoryState s,int culture,int cell,int state,HistoricalPersonRoles roles,int birth=-1,int parent1=-1,int parent2=-1,string name=null)
        {
            var r=s.History.Notables;int id=r.People.Count;double Roll(string tag)=>HistoryRoll("notable-35-"+tag,id,s.Years);
            bool estimated=birth<0;if(estimated)birth=Math.Max(0,s.Years-22-(int)(25*Roll("age")));
            int death=Math.Max(s.Years+1,birth+(estimated?55+(int)(35*Roll("lifespan")):Roll("child-survival")<.12?1+(int)(12*Roll("child-age")):45+(int)(45*Roll("lifespan"))));
            var p=new HistoricalNotablePerson{Id=id,RecordedYear=s.Years,BirthYear=birth,BirthEstimated=estimated,ExpectedDeathYear=death,OriginCell=cell,Cell=cell,Culture=culture,State=state,Roles=roles,Parent1=parent1,Parent2=parent2,Sex=Roll("sex")<.5?0:1,
                Name=name??LanguageName(s,culture,LanguageNameKind.Person,unchecked(id*7907+s.Years)),Faith=HistoricalCells.Religion(s,cell),Ethnicity=PrimaryEthnicityAt(s,cell),Language=PrimarySpokenLanguageAt(s,cell)};
            p.BirthSeconds=estimated?(long)birth*SimulationTime.Year:SimulationTime.Seconds(s);
            p.DeathSeconds=(long)death*SimulationTime.Year+HistoricalNotableHistory.DeathOffset(id,(long)(HistoryRoll("notable-death-season-35",death,0)*SimulationTime.Year));
            for(int k=0;k<6;k++)p.Skills[k]=200+(int)(650*Roll("skill-"+k));
            HistoricalPersonRoles[] pursuits={HistoricalPersonRoles.Commander,HistoricalPersonRoles.Scientist,HistoricalPersonRoles.Artist,HistoricalPersonRoles.Philosopher,HistoricalPersonRoles.Theologian,HistoricalPersonRoles.Administrator};
            p.Interests=pursuits[(int)(Roll("interest-a")*pursuits.Length)]|pursuits[(int)(Roll("interest-b")*pursuits.Length)];
            if(parent1>=0){var parent=r.People.Peek(parent1);p.House=parent.House;p.HouseName=parent.HouseName;p.Ethnicity=parent.Ethnicity;p.Language=parent.Language;p.Faith=parent.Faith;}
            r.Save(p,true);ScheduleAggregate(s,PersonProcess,id,p.DeathSeconds);return id;
        }
        bool DynasticNotableCourt(PrehistoryState s,int state)=>PoliticalProfile(s,state).Form.IndexOf("dynastic",StringComparison.OrdinalIgnoreCase)>=0;
        void PrepareNotableSuccession(PrehistoryState s,int state,HistoricalLeader leader)
        {
            if(!NotableHistory(s)||!DynasticNotableCourt(s,state))return;var r=s.History.Notables;int best=-1;
            for(int slot=2;slot<=3;slot++)
            {
                int id=r.Courts[state*8+slot];if(!NotableAlive(s,id))continue;var p=r.People.Peek(id);
                if(best<0||p.BirthYear<r.People.Peek(best).BirthYear)best=id;
            }
            if(best<0)return;var heir=r.People.Peek(best);leader.Name=heir.Name;leader.Military=heir.Skills[0];leader.Administration=heir.Skills[4];leader.Diplomacy=heir.Skills[3];leader.Selection="Accession of an existing member of the ruling household, negotiated with court and provincial elites";
            if(s.Years-heir.BirthYear<16)
            {
                var minor=r.People[best];int regent=NotableAlive(s,minor.Parent2)?minor.Parent2:NotableAlive(s,minor.Parent1)?minor.Parent1:-1;minor.Regent=regent;r.Save(minor);
                var guardian=regent>=0?r.People.Peek(regent):null;leader.Military=guardian?.Skills[0]??350;leader.Administration=guardian?.Skills[4]??350;leader.Diplomacy=guardian?.Skills[3]??350;leader.Selection="Minority accession with authority exercised by the court and a regency council";
            }
            r.Leaders=r.Leaders.With(leader.Id,best);
        }
        void RegisterNotableRuler(PrehistoryState s,int state,HistoricalLeader leader,bool families=true)
        {
            if(!NotableHistory(s))return;var r=s.History.Notables;int cell=s.Society.Settlements[s.History.Polities[state].CapitalId].Cell,id=r.Leaders[leader.Id];
            if(id<0)id=CreateNotable(s,s.History.Polities[state].CultureId,cell,state,HistoricalPersonRoles.Ruler,name:leader.Name);
            var p=r.People[id];p.Roles|=HistoricalPersonRoles.Ruler;p.Leader=leader.Id;p.State=state;p.Name=leader.Name;p.Renown=Math.Max(p.Renown,Math.Min(1000,leader.Renown));
            if(s.Years-p.BirthYear>=16){p.Skills[0]=leader.Military;p.Skills[3]=leader.Diplomacy;p.Skills[4]=leader.Administration;}else p.MinorityUntil=p.BirthYear+16;
            if(DynasticNotableCourt(s,state))
            {p.Roles|=HistoricalPersonRoles.RoyalFamily;if(p.House<0){p.House=id;p.HouseName=LanguageName(s,p.Culture,LanguageNameKind.House,unchecked(id*8171+s.Years));}}
            p.LifeOffice=families&&DynasticNotableCourt(s,state);
            r.Save(p);r.Leaders=r.Leaders.With(leader.Id,id);r.Courts=r.Courts.WithBlock(state*8,new[]{id,p.Spouse,p.Children.Length>0?p.Children[0]:-1,p.Children.Length>1?p.Children[1]:-1,-1,-1,-1,-1});
            if(p.LifeOffice){leader.ExpectedEndYear=p.ExpectedDeathYear;s.History.Aggregates.Polities[state].SuccessionDue=leader.ExpectedEndYear;}
            if(families)ReviewNotableCourt(s,state);
        }
        void SyncNotableRulerName(PrehistoryState s,int leader)
        {
            int id=RulerPersonId(s,leader);if(id<0)return;var p=s.History.Notables.People[id];string name=s.History.Dynamics.Leaders.Peek(leader).Name;
            int renown=s.History.Dynamics.Leaders.Peek(leader).Renown;
            if(p.Name!=name||p.Renown<Math.Min(1000,renown)){p.Name=name;p.Renown=Math.Max(p.Renown,Math.Min(1000,renown));s.History.Notables.Save(p);}
        }
        void AdvanceNotableDeath(PrehistoryState s,int id)
        {
            var r=s.History.Notables;var p=r.People[id];if(p.DeathYear>=0)return;p.DeathYear=s.Years;r.Save(p);
            HistoryEvent(s,"notable-death",p.Name+" dies; recorded offices and achievements remain in the historical record.",p.Cell,p.Culture,p.State,category:ChronicleCategory.Society);
            if(p.Leader>=0&&p.State>=0&&s.History.Polities[p.State].EndedYear<0&&s.History.Dynamics.States[p.State].LeaderId==p.Leader)
                AppointAggregateLeader(s,p.State);
        }
        int NotableSeat(PrehistoryState s,int state,int city,int slot,HistoricalPersonRoles role)
        {
            var r=s.History.Notables;int key=city*8+slot,id=r.Towns[key];
            if(NotableAlive(s,id))
            {
                int patron=state>=0?r.Courts[state*8]:-1;var present=r.People.Peek(id);
                if(present.State!=state||present.Patron!=patron||(present.Roles&role)!=role){var update=r.People[id];update.State=state;update.Patron=patron;update.Roles|=role;r.Save(update);}return id;
            }
            var town=s.Society.Settlements[city];int mentor=-1;
            for(int k=0;k<6;k++){int teacher=r.Towns[city*8+k];if(NotableAlive(s,teacher)&&r.People.Peek(teacher).Skills[1]>=600){mentor=teacher;break;}}
            id=CreateNotable(s,town.CultureId,town.Cell,state,role);var p=r.People[id];p.Mentor=mentor;
            p.Patron=state>=0?r.Courts[state*8]:-1;r.Save(p);r.Towns=r.Towns.With(key,id);return id;
        }
        void NotableDeed(PrehistoryState s,int id,HistoricalPersonDeed deed,int renown)
        {
            var r=s.History.Notables;var p=r.People[id];p.Renown=Math.Min(1000,p.Renown+renown);p.LastDeedYear=s.Years;
            p.Deeds=p.Deeds.Concat(new[]{deed}).Skip(Math.Max(0,p.Deeds.Length+1-8)).ToArray();r.Save(p);
        }
        void ReviewNotableCourt(PrehistoryState s,int state)
        {
            if(!NotableHistory(s))return;var r=s.History.Notables;int previous=r.Review[state*2];if(previous>=0&&s.Years-previous<5)return;r.Review=r.Review.With(state*2,s.Years);
            var h=s.History;var court=h.Polities[state];int ruler=r.Courts[state*8];if(!NotableAlive(s,ruler))return;var p=r.People[ruler];
            if(p.MinorityUntil>0&&s.Years>=p.MinorityUntil)
            {
                p.MinorityUntil=0;p.Regent=-1;r.Save(p);var leader=h.Dynamics.Leaders[h.Dynamics.States[state].LeaderId];leader.Military=p.Skills[0];leader.Diplomacy=p.Skills[3];leader.Administration=p.Skills[4];leader.Selection="Personal rule after the recorded minority and regency";SealAggregateLeader(s,leader.Id);
            }
            if(DynasticNotableCourt(s,state))
            {
                bool changed=false;
                if(!NotableAlive(s,p.Spouse)&&s.Years-p.BirthYear>=18&&s.Years-p.BirthYear<60)
                {
                    int partner=CreateNotable(s,p.Culture,p.Cell,state,HistoricalPersonRoles.RoyalFamily);var spouse=r.People[partner];spouse.Sex=1-p.Sex;spouse.Spouse=p.Id;spouse.House=p.House;spouse.HouseName=p.HouseName;r.Save(spouse);p.Spouse=partner;changed=true;r.Courts=r.Courts.With(state*8+1,partner);
                    HistoryEvent(s,"court-marriage",p.Name+" and "+spouse.Name+" form the recorded ruling household of "+court.Name+".",p.Cell,p.Culture,state,category:ChronicleCategory.Society);
                }
                if(NotableAlive(s,p.Spouse)&&p.Children.Length<4&&p.Children.Count(child=>NotableAlive(s,child))<2)
                {
                    var spouse=r.People.Peek(p.Spouse);int age=s.Years-(p.Sex==0?p.BirthYear:spouse.BirthYear);
                    if(age>=18&&age<=45&&HistoryRoll("notable-royal-birth-35",p.Id,s.Years)<1-Math.Exp(-.07*Math.Min(10,Math.Max(1,previous<0?1:s.Years-previous))))
                    {
                        int child=CreateNotable(s,p.Culture,p.Cell,state,HistoricalPersonRoles.RoyalFamily,s.Years,p.Id,p.Spouse);p.Children=p.Children.Concat(new[]{child}).ToArray();changed=true;
                        var mother=r.People[p.Spouse];mother.Children=mother.Children.Concat(new[]{child}).Take(4).ToArray();r.Save(mother);
                        HistoryEvent(s,"royal-birth",r.People.Peek(child).Name+" is born into the ruling household of "+court.Name+".",p.Cell,p.Culture,state,category:ChronicleCategory.Society);
                    }
                }
                if(changed)r.Save(p);
                var heirs=p.Children.Where(child=>NotableAlive(s,child)).Take(2).ToArray();
                for(int k=0;k<2;k++){int heir=k<heirs.Length?heirs[k]:-1;if(r.Courts[state*8+2+k]!=heir)r.Courts=r.Courts.With(state*8+2+k,heir);}
            }
            int next=r.Review[state*2+1];if(next>s.Years)return;r.Review=r.Review.With(state*2+1,s.Years+20+(int)(40*HistoryRoll("notable-patronage-35",state,s.Years)));
            int city=court.CapitalId;var town=s.Society.Settlements[city];var a=h.Aggregates;
            long cost=Math.Max(100,town.Population/20);if(town.Population<1200||!a.Cities[city].Supported||court.Treasury<cost*3||a.Polities[state].Receipts<=a.Polities[state].ServiceCost)return;
            int choice=(int)(3*HistoryRoll("notable-commission-35",state,s.Years));
            if(choice==2&&HistoricalCells.Religion(s,town.Cell)<0)return;
            court.Treasury-=cost;var role=choice==0?HistoricalPersonRoles.Artist:choice==1?HistoricalPersonRoles.Philosopher:HistoricalPersonRoles.Theologian;
            int author=NotableSeat(s,state,city,3+choice,role);var person=r.People.Peek(author);
            string work=choice==0?"a commissioned work commemorating local life and the court":choice==1?"an inquiry into civic duties and legitimate government":"a commissioned commentary on local religious teaching";
            int ev=h.Archive.Count;HistoryEvent(s,"cultural-work",person.Name+" completes "+work+" with support funded by "+court.Name+".",town.Cell,town.CultureId,state,town:city,category:ChronicleCategory.Society);
            NotableDeed(s,author,new HistoricalPersonDeed{Year=s.Years,Event=ev,Kind="Commissioned work",Text=work},40);
        }
        void NotableResearch(PrehistoryState s,int culture,int cell,int technology,long work,bool completed,int level)
        {
            if(!NotableHistory(s)||work<=0&&!completed)return;int city=s.History.Aggregates.TownAt[cell];if(city<0||!s.History.Aggregates.Cities[city].Supported)return;
            var practice=s.History.Cultures[culture].Technical;if(completed&&(practice.Levels[technology]!=level||practice.LevelYear[technology]!=s.Years))return;
            int state=HistoricalCells.Polity(s,cell);var role=technology>=(int)HistoricalTechnology.ScientificMethod?HistoricalPersonRoles.Scientist:HistoricalPersonRoles.Inventor;
            int id=NotableSeat(s,state,city,0,role);var r=s.History.Notables;var p=r.People[id];p.Roles|=role;p.Project=technology;p.ResearchWork=Math.Min(long.MaxValue-work,p.ResearchWork)+work;p.Skills[1]=Math.Min(1000,p.Skills[1]+1);r.Save(p);
            if(completed&&(level<=3||level%5==0))
            {
                string text=p.Name+" contributes to the locally researched "+HistoricalTechnologies.Names[technology]+" (level "+level+"). This credits a participant in collective work, not a sole inventor or a free breakthrough.";
                int ev=s.History.Archive.Count;HistoryEvent(s,"notable-research",text,cell,culture,state,town:city,category:ChronicleCategory.Discoveries);
                NotableDeed(s,id,new HistoricalPersonDeed{Year=s.Years,Event=ev,Kind="Research contribution",Text=text,Technology=technology,Level=level},40);
            }
        }
        void NotableBattle(PrehistoryState s,int state,int campaign,int cell,bool won)
        {
            if(!NotableHistory(s))return;var r=s.History.Notables;int id=r.Commands[campaign*2+(s.History.Dynamics.Campaigns[campaign].A==state?0:1)];if(!NotableAlive(s,id))return;var p=r.People[id];p.Cell=cell;p.Skills[0]=Math.Min(1000,p.Skills[0]+2);r.Save(p);
            if(!won||p.LastDeedYear>=0&&s.Years-p.LastDeedYear<5)return;string text=p.Name+" commands forces in a successful supplied engagement during "+s.History.Dynamics.Campaigns[campaign].Name+".";
            int ev=s.History.Archive.Count;HistoryEvent(s,"notable-command",text,cell,p.Culture,state,category:ChronicleCategory.Politics);
            NotableDeed(s,id,new HistoricalPersonDeed{Year=s.Years,Event=ev,Campaign=campaign,Kind="Military command",Text=text},30);
        }
        double NotableCommandPower(PrehistoryState s,int state,int campaign,double power)
        {
            if(!NotableHistory(s)||power<=0||s.History.Dynamics.Campaigns[campaign].EndedYear>=0)return power;var r=s.History.Notables;int side=s.History.Dynamics.Campaigns[campaign].A==state?0:1,key=campaign*2+side,id=r.Commands[key];
            if(!NotableAlive(s,id))
            {
                int city=s.History.Polities[state].CapitalId,seat=r.Towns[city*8+1];
                if(NotableAlive(s,seat)&&(r.People.Peek(seat).CurrentCampaign<0||s.History.Dynamics.Campaigns[r.People.Peek(seat).CurrentCampaign].EndedYear>=0))id=seat;
                else{id=CreateNotable(s,s.History.Polities[state].CultureId,s.Society.Settlements[city].Cell,state,HistoricalPersonRoles.Commander);r.Towns=r.Towns.With(city*8+1,id);}
                var p=r.People[id];p.CurrentCampaign=campaign;p.Patron=r.Courts[state*8];r.Save(p);r.Commands=r.Commands.With(key,id);
            }
            int original=s.History.Dynamics.Leaders[s.History.Dynamics.States[state].LeaderId].Military;
            // Replace the original command aptitude, never add a stacked bonus
            // or waive the already-paid manpower, supplies and technology gates.
            return D.Q(power*(.4+r.People.Peek(id).Skills[0]/1000.0)/(.4+original/1000.0),1000000);
        }
        void NotableReligiousFigure(PrehistoryState s,int city,int faith,int sourceEvent)
        {
            if(!NotableHistory(s)||city<0)return;int state=HistoricalCells.Polity(s,s.Society.Settlements[city].Cell);int id=NotableSeat(s,state,city,5,HistoricalPersonRoles.Theologian|HistoricalPersonRoles.ReligiousTeacher);var r=s.History.Notables;var p=r.People[id];p.Faith=faith;
            if(s.History.Aggregates.Empires.Connections.Religion.Traditions.Doctrines[faith].Revelation==ReligiousRevelation.InspiredTeachers)p.Roles|=HistoricalPersonRoles.PropheticTeacher;
            r.Save(p);r.FaithFounders=r.FaithFounders.With(faith,id);
            NotableDeed(s,id,new HistoricalPersonDeed{Year=s.Years,Event=sourceEvent,Faith=faith,Kind="Religious teaching",Text="Helped articulate the new local teaching of "+s.History.Religions[faith].Name},60);
        }
        void NotablePoliticalAct(PrehistoryState s,HistoricalEvent e)
        {
            if(!NotableHistory(s)||e.Kind!="political-reform"||e.PolityId<0)return;int id=s.History.Notables.Courts[e.PolityId*8];if(!NotableAlive(s,id))return;
            var p=s.History.Notables.People[id];p.Roles|=HistoricalPersonRoles.Administrator;s.History.Notables.Save(p);
            NotableDeed(s,id,new HistoricalPersonDeed{Year=s.Years,Event=e.Id,Kind="Political reform",Text=e.Text},30);
        }
        void ValidateNotablePeople(PrehistoryState s)
        {
            var r=s.History.Notables;if(r==null){if(s.History.Aggregates.Empires?.Connections?.Realms?.Version>=35)throw new ArgumentException("Missing notable people.");return;}
            if(!NotableHistory(s)||r.Version!=1||r.SinceYear<0||r.SinceYear>s.Years||r.People==null)throw new ArgumentException("Invalid notable-person extension.");r.Verify();
            for(int id=0;id<r.People.Count;id++)
            {
                var p=r.People.Peek(id);bool Person(int value)=>value>=-1&&value<r.People.Count;
                if(p.BirthSeconds/SimulationTime.Year!=p.BirthYear||p.DeathSeconds/SimulationTime.Year!=p.ExpectedDeathYear||p.DeathSeconds<=p.BirthSeconds)throw new ArgumentException("Invalid notable life dates.");
                if(p.OriginCell<0||p.OriginCell>=World.Surface.Cells.Length||p.CurrentCampaign< -1||p.CurrentCampaign>=s.History.Dynamics.Campaigns.Length||p.MinorityUntil<0||p.Faith< -1||p.Faith>=s.History.Religions.Length||p.Ethnicity< -1||p.Ethnicity>=s.History.Identities.Peoples.Length||p.Language< -1||p.Language>=s.History.Languages.Spoken.Varieties.Length||((int)p.Roles&~2047)!=0||((int)p.Interests&~2047)!=0||p.Roles==HistoricalPersonRoles.None)throw new ArgumentException("Invalid notable affiliations or career.");
                if(p.Id!=id||string.IsNullOrWhiteSpace(p.Name)||p.BirthYear<0||p.BirthYear>p.RecordedYear||p.RecordedYear<r.SinceYear||p.RecordedYear>s.Years||p.ExpectedDeathYear<=p.RecordedYear||p.ExpectedDeathYear>p.BirthYear+100||p.DeathYear>=0&&(p.DeathYear<p.RecordedYear||p.DeathYear>s.Years)||p.Cell<0||p.Cell>=World.Surface.Cells.Length||p.Culture<0||p.Culture>=s.History.Cultures.Length||p.State< -1||p.State>=s.History.Polities.Length||p.Leader< -1||p.Leader>=s.History.Dynamics.Leaders.Length||!Person(p.House)||!Person(p.Spouse)||!Person(p.Mentor)||!Person(p.Patron)||!Person(p.Regent)||p.Parent1>=id||p.Parent2>=id||p.Parent1< -1||p.Parent2< -1||p.Sex<0||p.Sex>1||p.Renown<0||p.Renown>1000||p.ResearchWork<0||p.Skills?.Length!=6||p.Skills.Any(x=>x<0||x>1000)||p.Children==null||p.Children.Length>4||p.Children.Any(x=>x<=id||x>=r.People.Count)||p.Deeds==null||p.Deeds.Length>8)throw new ArgumentException("Invalid saved notable biography "+id);
                foreach(var d in p.Deeds)if(d.Year<p.RecordedYear||d.Year>s.Years||d.Event< -1||d.Event>=s.History.Archive.Count||d.Campaign< -1||d.Campaign>=s.History.Dynamics.Campaigns.Length||d.Technology< -1||d.Technology>=HistoricalTechnologies.Names.Length||d.Level<0||d.Faith< -1||d.Faith>=s.History.Religions.Length||string.IsNullOrWhiteSpace(d.Text))throw new ArgumentException("Invalid notable achievement.");
            }
            bool Ref(int key,int value)=>value>=0&&value<r.People.Count;
            if(!r.Leaders.AllPresentValues((k,v)=>k<s.History.Dynamics.Leaders.Length&&Ref(k,v))||!r.Courts.AllPresentValues((k,v)=>k/8<s.History.Polities.Length&&Ref(k,v))||!r.Towns.AllPresentValues((k,v)=>k/8<s.Society.Settlements.Length&&Ref(k,v))||!r.FaithFounders.AllPresentValues((k,v)=>k<s.History.Religions.Length&&Ref(k,v)))throw new ArgumentException("Invalid notable affiliation index.");
            if(!r.Commands.AllPresentValues((k,v)=>k/2<s.History.Dynamics.Campaigns.Length&&Ref(k,v))||!r.Digests.AllPresentValues((k,v)=>k/8<r.People.Count))throw new ArgumentException("Invalid notable command or biography digest index.");
        }
    }
}

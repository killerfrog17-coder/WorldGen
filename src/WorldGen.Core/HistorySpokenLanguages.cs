using System;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalSpeechVariety
    {
        public int Id,Parent=-1,Culture,Cell,FoundedYear;
        public string Name="";
        public LanguageProfile Sounds;
        public HistoricalSpeechVariety Copy(){var c=(HistoricalSpeechVariety)MemberwiseClone();c.Sounds=Sounds.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalSpokenLanguages
    {
        public const int Fields=12;
        public int Version=1,SinceYear;
        public HistoricalSpeechVariety[] Varieties=Array.Empty<HistoricalSpeechVariety>();
        public HistoricalCellTree Places,Regions,CultureVarieties=new HistoricalCellTree(33554432);
        public HistoricalSpokenLanguages Copy()=>new HistoricalSpokenLanguages{Version=Version,SinceYear=SinceYear,Varieties=Varieties.Select(v=>v.Copy()).ToArray(),Places=Places,Regions=Regions,CultureVarieties=CultureVarieties};
    }
    // Home shares partition households; second-language competence overlaps them.
    // These are coarse estimates, never another population ledger.
    public sealed class SpokenCommunity
    {
        public int Primary=-1,Minority=-1,MinorityPermille,Second=-1,BilingualPermille,LastContact=-1,ReviewedYear,SinceYear,IsolationYears,Administrative=-1,Liturgical=-1,ContactSource=-1;
        public bool RegionalEstimate;
    }
    public static class SpokenLanguageEffects
    {
        public static int Intelligibility(LanguageProfile a,LanguageProfile b)
        {
            if(a==null||b==null)return 0;
            if(a.FamilySeed!=b.FamilySeed)return 30;
            int same=0;for(int i=0;i<LanguageGenerator.SoundCount;i++)if(a.SoundMap[i]==b.SoundMap[i])same++;
            return Math.Max(100,1000-(LanguageGenerator.SoundCount-same)*22-(a.WordOrder==b.WordOrder?0:70));
        }
        public static int Competence(int previous,int elapsed,int exposure,int affinity)
        {
            if(elapsed<=0)return previous;
            double target=Math.Min(900,Math.Max(0,exposure)*(.35+.65*affinity/1000.0));
            return Math.Max(0,Math.Min(1000,(int)Math.Round(previous+(target-previous)*(1-Math.Exp(-elapsed/(exposure>0?90.0:200.0))))));
        }
        public static int HomeShift(int bilingual,int elapsed,int opportunity,int protection)
            =>Math.Max(0,(int)Math.Round(Math.Min(30,elapsed/30.0)*bilingual/1000.0*opportunity/1000.0*(1-.95*protection/1000.0)));
        public static int MigrantShare(long residents,long migrants)=>residents<=0?1000:migrants<=0?0:(int)Math.Round(1000.0*migrants/(residents+(double)migrants));
    }
    public sealed partial class Prehistory
    {
        const int SpeechPrimary=0,SpeechMinority=1,SpeechShare=2,SpeechSecond=3,SpeechBilingual=4,SpeechContact=5,SpeechReview=6,SpeechSince=7,SpeechIsolation=8,SpeechAdmin=9,SpeechRitual=10,SpeechSource=11;
        static bool SpokenHistory(PrehistoryState s)=>s?.History?.Languages?.Spoken!=null;
        void InitializeSpokenLanguages(PrehistoryState s)
        {
            if(s.History.Aggregates.Empires.Connections.Realms.Version<32||SpokenHistory(s))return;
            var a=s.History.Aggregates;
            s.History.Languages.Spoken=new HistoricalSpokenLanguages{SinceYear=s.Years,Places=new HistoricalCellTree(HistoricalCells.Count(s)*HistoricalSpokenLanguages.Fields),Regions=new HistoricalCellTree(a.Regions.Length*HistoricalSpokenLanguages.Fields)};
            // Explicit setup/upgrade only. No retrospective invented minorities.
            for(int c=0;c<s.History.Cultures.Length;c++)RegisterSpokenCulture(s,c,false);
        }
        int AddSpeechVariety(PrehistoryState s,int culture,int cell,int parent)
        {
            var r=s.History.Languages.Spoken;int id=r.Varieties.Length;
            var sounds=parent>=0?LanguageGenerator.Create(World.Recipe.Seed,id,s.Years,r.Varieties[parent].Sounds):s.History.Languages.Profiles[culture].Copy();
            if(parent<0){sounds.Id=id;sounds.ParentId=-1;sounds.FamilyId=id;}
            string name=LanguageGenerator.Capitalize(LanguageGenerator.Compound(sounds,"name:"+cell,"speech").Text);
            var v=new HistoricalSpeechVariety{Id=id,Parent=parent,Culture=culture,Cell=cell,FoundedYear=s.Years,Name=name,Sounds=sounds};
            r.Varieties=r.Varieties.Concat(new[]{v}).ToArray();return id;
        }
        void RegisterSpokenCulture(PrehistoryState s,int culture,bool inherit=true)
        {
            if(!SpokenHistory(s))return;var r=s.History.Languages.Spoken;if(r.CultureVarieties[culture]>=0)return;
            int parent=s.History.Cultures[culture].ParentCultureId,voice=inherit&&parent>=0&&s.History.Languages.Profiles[parent].FamilySeed==s.History.Languages.Profiles[culture].FamilySeed?r.CultureVarieties[parent]:-1;
            int cell=s.Society.Cultures[culture].OriginCell;
            if(voice<0)voice=AddSpeechVariety(s,culture,cell,-1);
            r.CultureVarieties=r.CultureVarieties.With(culture,voice);
        }
        static SpokenCommunity ReadSpeech(HistoricalCellTree tree,int key,bool regional)
            =>new SpokenCommunity{Primary=tree[key],Minority=tree[key+1],MinorityPermille=Math.Max(0,tree[key+2]),Second=tree[key+3],BilingualPermille=Math.Max(0,tree[key+4]),LastContact=tree[key+5],ReviewedYear=tree[key+6],SinceYear=tree[key+7],IsolationYears=Math.Max(0,tree[key+8]),Administrative=tree[key+9],Liturgical=tree[key+10],ContactSource=tree[key+11],RegionalEstimate=regional};
        public static SpokenCommunity SpokenCommunityAt(PrehistoryState s,int cell)
        {
            if(!SpokenHistory(s)||cell<0||cell>=HistoricalCells.Count(s))return null;
            var r=s.History.Languages.Spoken;int k=cell*HistoricalSpokenLanguages.Fields;
            if(r.Places[k]>=0)return ReadSpeech(r.Places,k,false);
            int region=s.History.Aggregates.Empires.Connections.CellRegions[cell];
            if(region>=0&&r.Regions[region*HistoricalSpokenLanguages.Fields]>=0)return ReadSpeech(r.Regions,region*HistoricalSpokenLanguages.Fields,true);
            int c=HistoricalCells.Culture(s,cell),v=c>=0?r.CultureVarieties[c]:-1;
            return v<0?null:new SpokenCommunity{Primary=v,SinceYear=r.SinceYear,ReviewedYear=r.SinceYear,RegionalEstimate=true};
        }
        public static int PrimarySpokenLanguageAt(PrehistoryState s,int cell)
        {
            if(!SpokenHistory(s)||cell<0||cell>=HistoricalCells.Count(s))return -1;
            var r=s.History.Languages.Spoken;int local=r.Places[cell*HistoricalSpokenLanguages.Fields];if(local>=0)return local;
            int region=s.History.Aggregates.Empires.Connections.CellRegions[cell];
            if(region>=0){local=r.Regions[region*HistoricalSpokenLanguages.Fields];if(local>=0)return local;}
            int c=HistoricalCells.Culture(s,cell);return c>=0?r.CultureVarieties[c]:-1;
        }
        int[] SpeechFields(SpokenCommunity p)=>new[]{p.Primary,p.Minority,p.MinorityPermille,p.Second,p.BilingualPermille,p.LastContact,p.ReviewedYear,p.SinceYear,p.IsolationYears,p.Administrative,p.Liturgical,p.ContactSource};
        void StoreSpeech(PrehistoryState s,int cell,SpokenCommunity p,bool regional=false)
        {
            var r=s.History.Languages.Spoken;var fields=SpeechFields(p);
            if(regional){int region=CellPatch[cell];r.Regions=r.Regions.WithBlock(region*HistoricalSpokenLanguages.Fields,fields);}
            else r.Places=r.Places.WithBlock(cell*HistoricalSpokenLanguages.Fields,fields);
        }
        void RecordSpokenContact(PrehistoryState s,int from,int to)
        {
            if(!SpokenHistory(s)||from<0||to<0||from==to)return;
            var donor=SpokenCommunityAt(s,from);var p=SpokenCommunityAt(s,to);if(donor==null||p==null)return;
            AdvanceSpokenCommunity(s,to,false);p=SpokenCommunityAt(s,to);
            if(donor.Primary==p.Primary){p.LastContact=s.Years;p.ContactSource=from;StoreSpeech(s,to,p);return;}
            // A single foreign visitor neither changes home language nor creates
            // a resident minority. Retain the strongest ongoing second language.
            if(p.Second!=donor.Primary&&p.BilingualPermille>=250&&p.LastContact>=s.Years-80)return;
            if(p.Second!=donor.Primary){p.Second=donor.Primary;p.BilingualPermille=0;}
            p.LastContact=s.Years;p.ContactSource=from;StoreSpeech(s,to,p);
        }
        void AdvanceSpokenCommunity(PrehistoryState s,int cell,bool regional)
        {
            if(!SpokenHistory(s))return;var p=SpokenCommunityAt(s,cell);if(p==null)return;
            // A rural regional clock must not repeatedly reread an older local
            // visitor record and then write its result into a different account.
            var speech=s.History.Languages.Spoken;
            if(regional&&speech.Regions[CellPatch[cell]*HistoricalSpokenLanguages.Fields]>=0)p=ReadSpeech(speech.Regions,CellPatch[cell]*HistoricalSpokenLanguages.Fields,true);
            int elapsed=s.Years-p.ReviewedYear;if(elapsed<40)return;
            var r=s.History.Languages.Spoken;var a=s.History.Aggregates;int town=a.TownAt[cell],owner=HistoricalCells.Polity(s,cell);
            // Local households survive a flag change. A working local office can
            // encourage bilingualism; conquest by itself never replaces speech.
            int admin=-1,ritual=-1;
            bool office=town>=0&&owner>=0&&a.Living.Cities[town].AbandonedYear<0&&a.Cities[town].Supported&&s.Society.Settlements[town].FoodReserve>0;
            if(office&&HistoricalTechnologies.Has(s.History.Cultures[s.History.Polities[owner].CultureId],HistoricalTechnology.Writing))admin=r.CultureVarieties[s.History.Polities[owner].CultureId];
            var body=ReligiousInstitutionAt(s,cell);
            if(body!=null&&body.Support>=400)ritual=p.Liturgical>=0?p.Liturgical:SpokenCommunityAt(s,s.Society.Settlements[body.Seat].Cell)?.Primary??-1;
            p.Administrative=admin;p.Liturgical=ritual;
            int contactYears=p.LastContact<0?0:Math.Max(0,Math.Min(s.Years,p.LastContact+80)-Math.Max(p.ReviewedYear,p.LastContact));
            bool contact=contactYears>0;
            int exposure=contact&&p.ContactSource>=0&&PrimarySpokenLanguageAt(s,p.ContactSource)==p.Second?650*contactYears/elapsed:0;
            if(admin>=0&&admin!=p.Primary&&(p.Second<0||exposure==0)){if(p.Second!=admin)p.BilingualPermille=0;p.Second=admin;exposure=office?450:0;}
            else if(ritual>=0&&ritual!=p.Primary&&p.Second<0){p.Second=ritual;exposure=250;}
            if(p.Second>=0)
            {
                int affinity=SpokenLanguageEffects.Intelligibility(r.Varieties[p.Primary].Sounds,r.Varieties[p.Second].Sounds);
                p.BilingualPermille=SpokenLanguageEffects.Competence(p.BilingualPermille,elapsed,exposure,affinity);
                // Home-language shift requires an actually sustained domain;
                // bilingual trade by itself does not erase minority households.
                if(p.Second!=p.Primary&&p.Second==admin)
                {
                    int shift=SpokenLanguageEffects.HomeShift(p.BilingualPermille,elapsed,600,200);
                    if(p.Minority==p.Second)p.MinorityPermille=Math.Min(999,p.MinorityPermille+shift);
                    else if(p.Minority<0&&shift>0){p.Minority=p.Second;p.MinorityPermille=shift;}
                    if(p.MinorityPermille>500){int former=p.Primary;p.Primary=p.Minority;p.Minority=former;p.MinorityPermille=1000-p.MinorityPermille;p.Second=former;}
                }
                if(exposure>0&&p.LastContact>=s.Years-80&&p.BilingualPermille>=250&&HistoryRoll("spoken-contact-loan-32",cell,s.Years)<.04)
                {
                    var v=r.Varieties[p.Primary];var donor=r.Varieties[p.Second];
                    string[] concepts={"market","ship","temple","iron","house","king"};
                    int k=(int)(HistoryRoll("spoken-loan-domain-32",cell,s.Years)*concepts.Length);
                    // Loans change this named variety's bounded sound inventory,
                    // never the donor or the culture naming profile.
                    v.Sounds=LanguageGenerator.Borrow(v.Sounds,donor.Sounds,concepts[k],s.Years);
                }
            }
            p.IsolationYears=contact||admin>=0||ritual>=0?Math.Max(0,p.IsolationYears-elapsed*2):Math.Min(1000000,p.IsolationYears+elapsed);
            // Local divergence, not all speakers mutating when a culture is renamed.
            int age=s.Years-r.Varieties[p.Primary].FoundedYear;
            if(age>=800&&p.IsolationYears>=1000&&HistoryRoll("spoken-local-divergence-32",cell,s.Years)<1-Math.Exp(-elapsed/6000.0))
            {
                int old=p.Primary;p.Primary=AddSpeechVariety(s,HistoricalCells.Culture(s,cell)>=0?HistoricalCells.Culture(s,cell):r.Varieties[old].Culture,cell,old);p.IsolationYears=0;
                HistoryEvent(s,"language-divergence",r.Varieties[p.Primary].Name+" develops as a local descendant of "+r.Varieties[old].Name+" through prolonged separation. Political and religious allegiance are unchanged.",cell,HistoricalCells.Culture(s,cell),town:town,category:ChronicleCategory.Society);
            }
            p.ReviewedYear=s.Years;StoreSpeech(s,cell,p,regional&&town<0);
        }
        void InheritSpokenHouseholds(PrehistoryState s,int target,int source,long residents,long migrants)
        {
            if(!SpokenHistory(s))return;var carried=SpokenCommunityAt(s,source);if(carried==null)return;
            var local=SpokenCommunityAt(s,target);int share=SpokenLanguageEffects.MigrantShare(residents,migrants);
            if(local==null||residents<=0){local=carried;local.SinceYear=local.ReviewedYear=s.Years;local.Administrative=local.Liturgical=-1;local.LastContact=local.ContactSource=-1;local.IsolationYears=0;}
            else if(local.Primary!=carried.Primary)
            {
                // Two largest home communities retained; the smaller represented
                // minority is an approximation, not every household's genealogy.
                int incoming=share,oldMinor=(int)Math.Round(local.MinorityPermille*(1-share/1000.0));
                if(local.Minority==carried.Primary)incoming+=oldMinor;
                if(incoming>=oldMinor){local.Minority=carried.Primary;local.MinorityPermille=incoming;}
                else local.MinorityPermille=oldMinor;
                if(local.MinorityPermille>500){int old=local.Primary;local.Primary=local.Minority;local.Minority=old;local.MinorityPermille=1000-local.MinorityPermille;}
                local.Second=carried.Primary==local.Primary?local.Minority:carried.Primary;local.LastContact=s.Years;local.ContactSource=source;
            }
            StoreSpeech(s,target,local);
        }
        void ValidateSpokenLanguages(PrehistoryState s)
        {
            var r=s.History.Languages?.Spoken;if(r==null){if(s.History.Aggregates?.Empires?.Connections?.Realms?.Version>=32)throw new ArgumentException("Missing spoken communities.");return;}
            if(r.Version!=1||r.SinceYear<0||r.SinceYear>s.Years||r.Varieties==null||r.Places?.Length!=HistoricalCells.Count(s)*HistoricalSpokenLanguages.Fields||r.Regions?.Length!=s.History.Aggregates.Regions.Length*HistoricalSpokenLanguages.Fields||r.CultureVarieties?.Length!=33554432)throw new ArgumentException("Invalid spoken language extension.");
            for(int i=0;i<r.Varieties.Length;i++){var v=r.Varieties[i];if(v==null||v.Id!=i||v.Parent< -1||v.Parent>=i||v.Culture<0||v.Culture>=s.History.Cultures.Length||v.Cell<0||v.Cell>=HistoricalCells.Count(s)||v.FoundedYear<r.SinceYear||v.FoundedYear>s.Years||string.IsNullOrWhiteSpace(v.Name)||v.Name.Length>300||!LanguageGenerator.Valid(v.Sounds)||v.Sounds.Id!=i)throw new ArgumentException("Invalid speech variety.");}
            foreach(var tree in new[]{r.Places,r.Regions})if(!tree.AllPresentValues((k,v)=>{int f=k%HistoricalSpokenLanguages.Fields;return f==SpeechPrimary||f==SpeechMinority||f==SpeechSecond||f==SpeechAdmin||f==SpeechRitual?v>=0&&v<r.Varieties.Length:f==SpeechSource?v>=0&&v<HistoricalCells.Count(s):f==SpeechContact||f==SpeechReview||f==SpeechSince?v>=r.SinceYear&&v<=s.Years:f==SpeechIsolation?v>=0&&v<=1000000:v>=0&&v<=1000;}))throw new ArgumentException("Invalid local speech record.");
            foreach(var tree in new[]{r.Places,r.Regions})if(!tree.AllPresentValues((k,v)=>
            {
                int key=k-k%HistoricalSpokenLanguages.Fields;if(tree[key]<0)return false;
                if(k%HistoricalSpokenLanguages.Fields!=0)return true;
                int minority=tree[key+SpeechMinority],share=tree[key+SpeechShare],second=tree[key+SpeechSecond];
                return tree[key+SpeechReview]>=r.SinceYear&&tree[key+SpeechSince]>=r.SinceYear&&minority!=v&&share>=0&&share<=500&&(minority>=0||share==0)&&(second>=0||tree[key+SpeechBilingual]<=0);
            }))throw new ArgumentException("Incoherent local speech community.");
            if(!r.CultureVarieties.AllPresentValues((k,v)=>k<s.History.Cultures.Length&&v>=0&&v<r.Varieties.Length)||Enumerable.Range(0,s.History.Cultures.Length).Any(c=>r.CultureVarieties[c]<0))throw new ArgumentException("Invalid culture speech defaults.");
        }
        double SpokenCommunicationAt(PrehistoryState s,int from,int to)
        {
            if(!SpokenHistory(s))return 1;int first=PrimarySpokenLanguageAt(s,from),second=PrimarySpokenLanguageAt(s,to);if(first<0||second<0||first==second)return 1;
            var a=SpokenCommunityAt(s,from);var b=SpokenCommunityAt(s,to);
            var r=s.History.Languages.Spoken;int familiarity=SpokenLanguageEffects.Intelligibility(r.Varieties[a.Primary].Sounds,r.Varieties[b.Primary].Sounds);
            if(a.Second==b.Primary)familiarity=Math.Max(familiarity,a.BilingualPermille);
            if(b.Second==a.Primary)familiarity=Math.Max(familiarity,b.BilingualPermille);
            if(a.Minority==b.Primary)familiarity=Math.Max(familiarity,a.MinorityPermille);
            if(b.Minority==a.Primary)familiarity=Math.Max(familiarity,b.MinorityPermille);
            return .35+.65*familiarity/1000.0;
        }
    }
}

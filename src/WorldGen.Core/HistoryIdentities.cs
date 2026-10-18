using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum PeopleIdentityOrigin { RecordedCommunity, LocalBranch, MixedCommunity }
    [Serializable] public sealed class HistoricalPeopleIdentity
    {
        public int Id,Parent=-1,OtherParent=-1,Culture,HomeCell,HomeRegion,FoundedYear,Language=-1,ReligiousFamily=-1;
        public string Name="";
        public PeopleIdentityOrigin Origin;
        // Social inheritance, NOT genetic ancestry or biological populations.
        public int[] Lineages=Array.Empty<int>(),Weights=Array.Empty<int>();
        public HistoricalPeopleIdentity Copy(){var c=(HistoricalPeopleIdentity)MemberwiseClone();c.Lineages=(int[])Lineages.Clone();c.Weights=(int[])Weights.Clone();return c;}
    }
    [Serializable] public sealed class HistoricalIdentityHistory
    {
        public const int Fields=12;
        public int Version=1,SinceYear;
        public HistoricalPeopleIdentity[] Peoples=Array.Empty<HistoricalPeopleIdentity>();
        public HistoricalCellTree Places,Regions,CultureDefaults=new HistoricalCellTree(33554432),Memories=new HistoricalCellTree(33554432),RulingIdentities=new HistoricalCellTree(33554432);
        public HistoricalIdentityHistory Copy()=>new HistoricalIdentityHistory{Version=Version,SinceYear=SinceYear,Peoples=Peoples.Select(p=>p.Copy()).ToArray(),Places=Places,Regions=Regions,CultureDefaults=CultureDefaults,Memories=Memories,RulingIdentities=RulingIdentities};
    }
    public sealed class EthnicCommunity
    {
        public int Primary=-1,Minority=-1,MinorityPermille,Cohesion,HomeAttachment,ReviewedYear,SinceYear,ContactGroup=-1,ContactYear=-1,ContactStrength,MixedYears,SeparatedYears;
        public bool RegionalEstimate;
    }
    public static class HistoricalIdentityEffects
    {
        public static int SharedInheritance(HistoricalPeopleIdentity a,HistoricalPeopleIdentity b)
        {
            int value=0;for(int i=0;i<a.Lineages.Length;i++)for(int j=0;j<b.Lineages.Length;j++)if(a.Lineages[i]==b.Lineages[j])value+=Math.Min(a.Weights[i],b.Weights[j]);return Math.Min(1000,value);
        }
        public static int Approach(int old,int target,int years,double duration)=>Math.Max(0,Math.Min(1000,(int)Math.Round(old+(target-old)*(1-Math.Exp(-Math.Max(0,years)/duration)))));
        public static double PoliticalGrievance(double difference,int cohesion,int attachment,int inclusion,int representation,int harm,int lostRights,bool imposed)
        {
            double exclusion=1-D.Clamp(inclusion/1000.0),voice=1-D.Clamp(representation/1000.0);
            // Difference itself grants no hostility. Bad treatment can alienate
            // coethnics too; remembered rights are not a permanent national claim.
            return D.Q(Math.Min(.18,.06*D.Clamp(difference)*cohesion/1000.0*attachment/1000.0*exclusion*voice
                +.08*harm/1000.0*exclusion+.05*lostRights/1000.0*voice*(imposed?1:.35)),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        static bool IdentityHistory(PrehistoryState s)=>s?.History?.Identities!=null;
        void InitializeIdentityHistory(PrehistoryState s)
        {
            if(s.History.Aggregates.Empires.Connections.Realms.Version<33||IdentityHistory(s))return;
            var a=s.History.Aggregates;
            s.History.Identities=new HistoricalIdentityHistory{SinceYear=s.Years,Places=new HistoricalCellTree(HistoricalCells.Count(s)*12),Regions=new HistoricalCellTree(a.Regions.Length*12)};
            // Explicit actor migration only. Do not invent old conquests or a
            // hidden ethnic census for an unrecorded past.
            for(int c=0;c<s.History.Cultures.Length;c++)RegisterPeopleIdentity(s,c,-1);
            foreach(var p in s.History.Polities)s.History.Identities.RulingIdentities=s.History.Identities.RulingIdentities.With(p.Id,s.History.Identities.CultureDefaults[p.CultureId]);
        }
        int AddPeopleIdentity(PrehistoryState s,int culture,int cell,int parent=-1,int other=-1)
        {
            var r=s.History.Identities;int id=r.Peoples.Length;
            var language=SpokenCommunityAt(s,cell);var profile=language!=null?s.History.Languages.Spoken.Varieties[language.Primary].Sounds:culture>=0?s.History.Languages.Profiles[culture]:LanguageGenerator.Create(World.Recipe.Seed,unchecked(-1-CellPatch[cell]),s.Years);
            int faith=HistoricalCells.Religion(s,cell),family=faith>=0?ReligiousDoctrineAt(s,faith)?.RootId??faith:-1;
            var p=new HistoricalPeopleIdentity{Id=id,Culture=culture,HomeCell=cell,HomeRegion=CellPatch[cell],Parent=parent,OtherParent=other,FoundedYear=s.Years,Language=language?.Primary??-1,ReligiousFamily=family,
                Name=LanguageGenerator.Capitalize(LanguageGenerator.Name(profile,LanguageNameKind.People,unchecked(cell+id*7919+173)).Text),Origin=other>=0?PeopleIdentityOrigin.MixedCommunity:parent>=0?PeopleIdentityOrigin.LocalBranch:PeopleIdentityOrigin.RecordedCommunity};
            if(parent<0){p.Lineages=new[]{id};p.Weights=new[]{1000};}
            else
            {
                var first=r.Peoples[parent];var second=other>=0?r.Peoples[other]:first;
                var origins=first.Lineages.Concat(second.Lineages).Distinct().Select(x=>new{Id=x,Weight=first.Lineages.Select((v,k)=>v==x?first.Weights[k]:0).Sum()+second.Lineages.Select((v,k)=>v==x?second.Weights[k]:0).Sum()}).OrderByDescending(x=>x.Weight).ThenBy(x=>x.Id).Take(4).ToArray();
                int provisional=origins.Sum(x=>x.Weight);origins=origins.Where(x=>1000*x.Weight>=provisional).ToArray();
                int total=origins.Sum(x=>x.Weight);p.Lineages=origins.Select(x=>x.Id).ToArray();p.Weights=origins.Select(x=>1000*x.Weight/total).ToArray();p.Weights[0]+=1000-p.Weights.Sum();
            }
            r.Peoples=r.Peoples.Concat(new[]{p}).ToArray();return id;
        }
        void RegisterPeopleIdentity(PrehistoryState s,int culture,int inheritedCulture)
        {
            if(!IdentityHistory(s))return;var r=s.History.Identities;if(r.CultureDefaults[culture]>=0)return;
            int cell=s.Society.Cultures[culture].OriginCell,region=CellPatch[cell];int identity=region>=0?r.Regions[region*12]:-1;
            if(identity<0&&inheritedCulture>=0)identity=r.CultureDefaults[inheritedCulture];
            if(identity<0)identity=AddPeopleIdentity(s,culture,cell);
            r.CultureDefaults=r.CultureDefaults.With(culture,identity);
            if(region>=0&&r.Regions[region*12]<0)StoreEthnicCommunity(s,cell,new EthnicCommunity{Primary=identity,Cohesion=450,HomeAttachment=500,SinceYear=s.Years,ReviewedYear=s.Years},true);
        }
        static EthnicCommunity ReadEthnic(HistoricalCellTree tree,int k,bool regional)=>new EthnicCommunity{Primary=tree[k],Minority=tree[k+1],MinorityPermille=Math.Max(0,tree[k+2]),Cohesion=Math.Max(0,tree[k+3]),HomeAttachment=Math.Max(0,tree[k+4]),ReviewedYear=tree[k+5],SinceYear=tree[k+6],ContactGroup=tree[k+7],ContactYear=tree[k+8],ContactStrength=Math.Max(0,tree[k+9]),MixedYears=Math.Max(0,tree[k+10]),SeparatedYears=Math.Max(0,tree[k+11]),RegionalEstimate=regional};
        public static int PrimaryEthnicityAt(PrehistoryState s,int cell)
        {
            if(!IdentityHistory(s)||cell<0||cell>=HistoricalCells.Count(s))return -1;var r=s.History.Identities;
            int primary=r.Places[cell*12];if(primary>=0)return primary;
            int region=s.History.Aggregates.Empires.Connections.CellRegions[cell];
            if(region>=0&&r.Regions[region*12]>=0)return r.Regions[region*12];
            int culture=HistoricalCells.Culture(s,cell);return culture>=0?r.CultureDefaults[culture]:-1;
        }
        public static EthnicCommunity EthnicCommunityAt(PrehistoryState s,int cell)
        {
            int id=PrimaryEthnicityAt(s,cell);if(id<0)return null;var r=s.History.Identities;
            if(r.Places[cell*12]>=0)return ReadEthnic(r.Places,cell*12,false);
            int region=s.History.Aggregates.Empires.Connections.CellRegions[cell];
            if(region>=0&&r.Regions[region*12]>=0)return ReadEthnic(r.Regions,region*12,true);
            return new EthnicCommunity{Primary=id,Cohesion=450,HomeAttachment=500,SinceYear=r.SinceYear,ReviewedYear=r.SinceYear,RegionalEstimate=true};
        }
        void StoreEthnicCommunity(PrehistoryState s,int cell,EthnicCommunity p,bool regional=false)
        {
            var r=s.History.Identities;int[] fields={p.Primary,p.Minority,p.MinorityPermille,p.Cohesion,p.HomeAttachment,p.ReviewedYear,p.SinceYear,p.ContactGroup,p.ContactYear,p.ContactStrength,p.MixedYears,p.SeparatedYears};
            if(regional)r.Regions=r.Regions.WithBlock(CellPatch[cell]*12,fields);else r.Places=r.Places.WithBlock(cell*12,fields);
        }
        void InheritEthnicHouseholds(PrehistoryState s,int target,int source,long residents,long migrants)
        {
            if(!IdentityHistory(s)||migrants<=0)return;var carried=EthnicCommunityAt(s,source);if(carried==null)return;var local=EthnicCommunityAt(s,target);
            if(local==null||residents<=0){local=carried;local.ReviewedYear=local.SinceYear=s.Years;local.HomeAttachment=200;local.ContactGroup=-1;local.ContactYear=-1;local.MixedYears=local.SeparatedYears=0;local.ContactStrength=0;}
            else
            {
                // Four bounded incoming/resident components. Any third/fourth
                // smaller community is folded into the main coarse estimate.
                double share=SpokenLanguageEffects.MigrantShare(residents,migrants)/1000.0;
                var ids=new[]{local.Primary,local.Minority,carried.Primary,carried.Minority};
                var weights=new[]{(1000-local.MinorityPermille)*(1-share),local.MinorityPermille*(1-share),(1000-carried.MinorityPermille)*share,carried.MinorityPermille*share};
                var top=ids.Where(x=>x>=0).Distinct().Select(id=>new{Id=id,Weight=ids.Select((x,k)=>x==id?weights[k]:0).Sum()}).Where(x=>x.Weight>0).OrderByDescending(x=>x.Weight).ThenBy(x=>x.Id).Take(2).ToArray();
                local.Primary=top[0].Id;local.Minority=top.Length>1?top[1].Id:-1;local.MinorityPermille=top.Length>1?(int)Math.Round(top[1].Weight):0;
                local.ContactGroup=carried.Primary;local.ContactYear=s.Years;local.ContactStrength=Math.Max(local.ContactStrength,200);
            }
            StoreEthnicCommunity(s,target,local);
        }
        void RecordIdentityContact(PrehistoryState s,int from,int to)
        {
            if(!IdentityHistory(s)||from<0||to<0||from==to)return;var donor=EthnicCommunityAt(s,from);var p=EthnicCommunityAt(s,to);if(donor==null||p==null)return;
            AdvanceEthnicCommunity(s,to,false);p=EthnicCommunityAt(s,to);
            if(p.ContactGroup!=donor.Primary&&p.ContactStrength>=400&&s.Years-p.ContactYear<=80)return;
            if(p.ContactGroup!=donor.Primary){p.ContactGroup=donor.Primary;p.ContactStrength=0;}
            p.ContactYear=s.Years;p.ContactStrength=Math.Min(1000,p.ContactStrength+50);StoreEthnicCommunity(s,to,p);
            RecordLocalContactMemory(s,from,to);
        }
        void AdvanceEthnicCommunity(PrehistoryState s,int cell,bool regional)
        {
            if(!IdentityHistory(s))return;
            if(s.History.Identities.Version>=2&&regional)EnsureForagerHeritage(s,CellPatch[cell]);
            var p=EthnicCommunityAt(s,cell);if(p==null)return;var r=s.History.Identities;
            if(regional&&r.Regions[CellPatch[cell]*12]>=0)p=ReadEthnic(r.Regions,CellPatch[cell]*12,true);
            int elapsed=s.Years-p.ReviewedYear;if(elapsed<40)return;var a=s.History.Aggregates;int town=a.TownAt[cell],owner=HistoricalCells.Polity(s,cell);
            if(regional){town=-1;owner=-1;}
            int active=p.ContactYear<0?0:Math.Max(0,Math.Min(s.Years,p.ContactYear+80)-Math.Max(p.ReviewedYear,p.ContactYear));
            // Local traffic among a widely dispersed founding identity does
            // not indefinitely prevent peripheral communities differentiating.
            if(r.Version>=2&&regional&&p.ContactGroup==p.Primary)active=0;
            int inclusion=town>=0&&owner>=0?a.Communities.Cities[town].InclusionMilli:700;
            p.Cohesion=HistoricalIdentityEffects.Approach(p.Cohesion,600+(inclusion<300?150:0),elapsed,250);
            p.HomeAttachment=HistoricalIdentityEffects.Approach(p.HomeAttachment,800,elapsed,400);
            p.SeparatedYears=active>0?Math.Max(0,p.SeparatedYears-elapsed):Math.Min(1000000,p.SeparatedYears+elapsed);
            if(active==0)p.ContactStrength=Math.Max(0,p.ContactStrength-elapsed/3);
            var speech=regional?RegionalSpokenCommunity(s,CellPatch[cell]):SpokenCommunityAt(s,cell);bool bilingual=speech!=null&&speech.BilingualPermille>=350;
            bool coResident=town>=0&&a.Cities[town].Supported&&s.Society.Settlements[town].Population>=100;
            bool mixing=p.Minority>=0&&p.MinorityPermille>=200&&inclusion>=600&&bilingual&&(coResident||p.ContactGroup==p.Minority&&active>0&&p.ContactStrength>=400);
            int faith=HistoricalCells.Religion(s,cell);int openness=faith>=0?ReligiousDoctrineAt(s,faith)?.Intermarriage??600:600;
            p.MixedYears=mixing?Math.Min(1000000,p.MixedYears+(int)Math.Round((coResident?elapsed:active)*(.2+.8*openness/1000.0))):Math.Max(0,p.MixedYears-elapsed);
            int identity=-1;
            if(p.MixedYears>=300&&HistoryRoll("identity-local-synthesis-33",cell,s.Years)<1-Math.Exp(-elapsed/2000.0))identity=AddPeopleIdentity(s,r.Peoples[p.Primary].Culture,cell,p.Primary,p.Minority);
            else if(p.SeparatedYears>=1500&&p.HomeAttachment>=650&&speech!=null&&r.Peoples[p.Primary].Language>=0&&speech.Primary!=r.Peoples[p.Primary].Language&&s.Years-r.Peoples[p.Primary].FoundedYear>=1500
                &&HistoryRoll("identity-local-branch-33",cell,s.Years)<1-Math.Exp(-elapsed/12000.0))identity=AddPeopleIdentity(s,r.Peoples[p.Primary].Culture,cell,p.Primary);
            else if(r.Version>=2&&regional&&p.SeparatedYears>=6000&&p.HomeAttachment>=650&&s.Years-r.Peoples[p.Primary].FoundedYear>=6000
                &&D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[r.Peoples[p.Primary].HomeCell].Position)*World.Recipe.RadiusKm>350
                &&HistoryRoll("forager-heritage-branch-36",cell,s.Years)<1-Math.Exp(-elapsed/18000.0))identity=AddPeopleIdentity(s,r.Peoples[p.Primary].Culture,cell,p.Primary);
            if(identity>=0)
            {
                int former=p.Primary;p.Primary=identity;
                if(r.Peoples[identity].OtherParent>=0){p.Minority=-1;p.MinorityPermille=0;}
                p.MixedYears=p.SeparatedYears=0;
                HistoryEvent(s,"people-identity",r.Peoples[identity].Name+" becomes an identifiable local people, inheriting "+r.Peoples[former].Name+(r.Peoples[identity].OtherParent>=0?" and "+r.Peoples[r.Peoples[identity].OtherParent].Name:"")+" traditions after sustained shared experience. Language, religion and state allegiance do not automatically change.",cell,HistoricalCells.Culture(s,cell),town:town,category:ChronicleCategory.Society);
            }
            p.ReviewedYear=s.Years;StoreEthnicCommunity(s,cell,p,regional);
            if(town>=0)AdvanceLocalHistoricalMemory(s,town);
        }
        double LocalIdentityDifference(PrehistoryState s,int city,int owner)
        {
            if(!IdentityHistory(s))return s.Society.Settlements[city].CultureId==s.History.Polities[owner].CultureId?0:1;
            var local=EthnicCommunityAt(s,s.Society.Settlements[city].Cell);int court=s.History.Identities.RulingIdentities[owner];
            if(local==null||court<0)return .5;var r=s.History.Identities;
            double Share(int id)=>id<0?0:id==court?1:.3*HistoricalIdentityEffects.SharedInheritance(r.Peoples[id],r.Peoples[court])/1000.0;
            double share=local.MinorityPermille/1000.0;return D.Clamp(1-Share(local.Primary)*(1-share)-Share(local.Minority)*share);
        }
        void AppendRulingIdentity(PrehistoryState s,int government,int predecessor)
        {
            if(!IdentityHistory(s))return;var r=s.History.Identities;var p=s.History.Polities[government];
            int id=predecessor>=0&&s.History.Polities[predecessor].CultureId==p.CultureId?r.RulingIdentities[predecessor]:PrimaryEthnicityAt(s,s.Society.Settlements[p.CapitalId].Cell);
            if(id<0)id=r.CultureDefaults[p.CultureId];r.RulingIdentities=r.RulingIdentities.With(government,id);
        }
        void ValidateIdentityHistory(PrehistoryState s)
        {
            var r=s.History.Identities;if(r==null){if(s.History.Aggregates?.Empires?.Connections?.Realms?.Version>=33)throw new ArgumentException("Missing identity history.");return;}
            if((r.Version!=1&&r.Version!=2)||r.SinceYear<0||r.SinceYear>s.Years||r.Peoples==null||r.Places?.Length!=HistoricalCells.Count(s)*12||r.Regions?.Length!=s.History.Aggregates.Regions.Length*12||r.CultureDefaults?.Length!=33554432||r.Memories?.Length!=33554432||r.RulingIdentities?.Length!=33554432)throw new ArgumentException("Invalid identity history extension.");
            for(int i=0;i<r.Peoples.Length;i++)
            {
                var p=r.Peoples[i];if(p==null||p.Id!=i||p.Parent< -1||p.Parent>=i||p.OtherParent< -1||p.OtherParent>=i||p.OtherParent==p.Parent&&p.Parent>=0||p.Parent<0&&p.OtherParent>=0||p.Culture<(r.Version>=2?-1:0)||p.Culture>=s.History.Cultures.Length||p.HomeCell<0||p.HomeCell>=HistoricalCells.Count(s)||p.HomeRegion<0||p.HomeRegion>=s.Population.Length||p.FoundedYear<r.SinceYear||p.FoundedYear>s.Years||p.Language< -1||p.Language>=(s.History.Languages?.Spoken?.Varieties.Length??0)||p.ReligiousFamily< -1||p.ReligiousFamily>=s.History.Religions.Length||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>300||!Enum.IsDefined(typeof(PeopleIdentityOrigin),p.Origin)||p.Lineages==null||p.Weights?.Length!=p.Lineages.Length||p.Lineages.Length<1||p.Lineages.Length>4||p.Weights.Any(x=>x<1||x>1000)||p.Weights.Sum()!=1000||p.Lineages.Any(x=>x<0||x>i)||p.Lineages.Distinct().Count()!=p.Lineages.Length)throw new ArgumentException("Invalid people identity.");
            }
            foreach(var tree in new[]{r.Places,r.Regions})if(!tree.AllPresentValues((k,v)=>
            {
                int f=k%12,key=k-f;if(tree[key]<0)return false;
                if(f==0){int minority=tree[key+1],share=tree[key+2];return v<r.Peoples.Length&&minority!=v&&share>=0&&share<=500&&(minority>=0||share==0)&&tree[key+5]>=r.SinceYear&&tree[key+6]>=r.SinceYear;}
                return f==1||f==7?v>=0&&v<r.Peoples.Length:f==5||f==6||f==8?v>=r.SinceYear&&v<=s.Years:f==10||f==11?v>=0&&v<=1000000:v>=0&&v<=1000;
            }))throw new ArgumentException("Invalid local identity record.");
            if(!r.CultureDefaults.AllPresentValues((k,v)=>k<s.History.Cultures.Length&&v>=0&&v<r.Peoples.Length)||Enumerable.Range(0,s.History.Cultures.Length).Any(c=>r.CultureDefaults[c]<0))throw new ArgumentException("Invalid default heritage.");
            if(!r.RulingIdentities.AllPresentValues((k,v)=>k<s.History.Polities.Length&&v>=0&&v<r.Peoples.Length)||Enumerable.Range(0,s.History.Polities.Length).Any(p=>r.RulingIdentities[p]<0))throw new ArgumentException("Invalid ruling identity.");
            ValidateLocalHistoricalMemories(s);
        }
    }
}

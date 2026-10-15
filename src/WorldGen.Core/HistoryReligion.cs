using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum ReligiousReach { Local, Portable, Missionary }
    [Serializable] public sealed class HistoricalFaithState
    {
        // A single strongest competing tradition, NOT a census of religious minorities.
        public int[] Candidate=Array.Empty<int>(),Influence=Array.Empty<int>(),LastChanged=Array.Empty<int>();
        public HistoricalFaithState Copy()=>new HistoricalFaithState{Candidate=(int[])Candidate.Clone(),Influence=(int[])Influence.Clone(),LastChanged=(int[])LastChanged.Clone()};
    }
    public static class HistoricalFaithEffects
    {
        public static int Exposure(ReligiousReach reach,bool exchange,bool patronage,int supportedPeople)
        {
            int network=reach==ReligiousReach.Missionary?55:reach==ReligiousReach.Portable?25:8;
            return network+(exchange?reach==ReligiousReach.Missionary?150:60:0)+(patronage?70:0)+Math.Min(30,Math.Max(0,supportedPeople)/1000);
        }
        public static int Accumulate(int priorCandidate,int priorInfluence,int proposed,int exposure)
            =>proposed<0?Math.Max(0,priorInfluence-70):Math.Min(1000,priorCandidate==proposed?priorInfluence+Math.Max(0,exposure):Math.Max(0,exposure-Math.Min(150,priorInfluence/2)));
        public static bool CanBecomeDominant(int year,int lastChanged,int influence)=>year-lastChanged>=200&&influence>=650;
        public static int CompetitiveExposure(int incumbentSupport,int challengerSupport)
            =>challengerSupport>incumbentSupport*1.1?Math.Min(300,Math.Max(0,challengerSupport-incumbentSupport)):0;
    }
    public sealed partial class Prehistory
    {
        void InitializeFaithDynamics(PrehistoryState s)
        {
            if(s.History.Faith!=null)return;int n=World.Surface.Cells.Length;
            s.History.Faith=new HistoricalFaithState{Candidate=Enumerable.Repeat(-1,n).ToArray(),Influence=new int[n],LastChanged=new int[n]};
        }
        void HistoricalReligionNetworks(PrehistoryState s,System.Threading.CancellationToken token)
        {
            InitializeFaithDynamics(s);var h=s.History;var faith=h.Faith;var before=(int[])h.CellReligion.Clone();int n=before.Length;
            var proposed=Enumerable.Repeat(-1,n).ToArray();var exposure=new int[n];
            bool competitive=DelegatedHistory(s);var localSupport=competitive?new System.Collections.Generic.Dictionary<int,int>[n]:null;
            var townsByCell=s.Society.Settlements.Where(t=>t.Population>=100).ToDictionary(t=>t.Cell,t=>t);
            void Offer(int cell,int religion,bool trade,bool court,int support)
            {
                if(religion<0||!competitive&&religion==before[cell]||s.Society.CellCulture[cell]<0)return;
                var tradition=h.Religions[religion];int value=HistoricalFaithEffects.Exposure(tradition.Reach,trade,court,support);
                // Place-based practices travel most readily inside related linguistic communities.
                int a=h.Cultures[s.Society.CellCulture[cell]].LanguageId,b=h.Cultures[tradition.CultureId].LanguageId;
                if(a!=b&&tradition.Reach==ReligiousReach.Local)value=Math.Max(1,value/3);
                if(competitive){var scores=localSupport[cell]??(localSupport[cell]=new System.Collections.Generic.Dictionary<int,int>());scores[religion]=(scores.TryGetValue(religion,out int prior)?prior:0)+value;return;}
                if(value>exposure[cell]||value==exposure[cell]&&(proposed[cell]<0||religion<proposed[cell])){proposed[cell]=religion;exposure[cell]=value;}
            }
            for(int cell=0;cell<n;cell++)if(s.Society.CellCulture[cell]>=0)
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();
                foreach(int next in World.Surface.Cells[cell].Neighbors)
                {
                    // Never diffuse a faith directly across an ocean cell without a lived community.
                    if(s.Society.CellCulture[next]<0||before[next]<0)continue;
                    if((World.Hydrology.OceanConnected[cell]||World.Hydrology.OceanConnected[next])&&!HistoricalTechnologies.Has(h.Cultures[s.Society.CellCulture[next]],HistoricalTechnology.Sailing))continue;
                    int support=townsByCell.TryGetValue(next,out var neighbor)?(int)Math.Min(30000,neighbor.Population):0;
                    Offer(cell,before[next],false,false,support);
                }
                int p=h.CellPolity[cell];if(p>=0&&h.Polities[p].EndedYear<0)
                {
                    var ruler=CivicLeader(s,p);bool patronage=ruler.Diplomacy<650||h.Polities[p].Legitimacy<450;
                    if(patronage)Offer(cell,h.Polities[p].ReligionId,false,true,(int)Math.Min(30000,h.Polities[p].Population/10));
                }
            }
            // Exchange includes city pairs and existing cultural seaways, not capital teleportation.
            var exchange=new System.Collections.Generic.List<(Settlement Source,Settlement Destination)>();
            foreach(var relation in h.Relations.Where(r=>r.Trade&&r.WarSince<0).OrderBy(r=>r.A).ThenBy(r=>r.B))
            {
                token.ThrowIfCancellationRequested();
                var a=s.Society.Settlements.Where(t=>t.PolityId==relation.A&&t.Population>=100).OrderBy(t=>t.Id).ToArray();
                var b=s.Society.Settlements.Where(t=>t.PolityId==relation.B&&t.Population>=100).OrderBy(t=>t.Id).ToArray();
                // Bounded nearest-endpoint screening prevents all-pairs empire-wide conversion.
                foreach(var destination in a.Concat(b))
                {
                    var sources=destination.PolityId==relation.A?b:a;
                    var source=sources.OrderBy(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[destination.Cell].Position)).ThenBy(t=>t.Id).FirstOrDefault();
                    if(source!=null)exchange.Add((source,destination));
                }
            }
            // Offers reduce by sum or deterministic max/tie; grouping these
            // read-only paths by origin reuses one bounded geographic search.
            foreach(var pair in exchange.OrderBy(x=>x.Source.Id).ThenBy(x=>x.Destination.Id))
            {
                if(!ReachableHistoricalContact(s,pair.Source.Cell,pair.Destination.Cell,pair.Source.CultureId,token))continue;
                Offer(pair.Destination.Cell,before[pair.Source.Cell],true,false,(int)Math.Min(30000,pair.Source.Population));
            }
            foreach(var route in Routes.Where(r=>r.Sea&&r.CellA>=0&&r.CellB>=0))
            {
                token.ThrowIfCancellationRequested();
                int a=s.Society.CellCulture[route.CellA],b=s.Society.CellCulture[route.CellB];if(a<0||b<0||a==b)continue;
                bool Can(int source)=>HistoricalTechnologies.Has(h.Cultures[source],HistoricalTechnology.Sailing)&&route.DistanceM<=(HistoricalTechnologies.Has(h.Cultures[source],HistoricalTechnology.Navigation)?700000:220000);
                if(Can(a))Offer(route.CellB,before[route.CellA],true,false,(int)Math.Min(30000,s.Society.Cultures[a].Population/5));
                if(Can(b))Offer(route.CellA,before[route.CellB],true,false,(int)Math.Min(30000,s.Society.Cultures[b].Population/5));
            }
            for(int cell=0;cell<n;cell++)
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();
                if(competitive&&s.Society.CellCulture[cell]>=0)
                {
                    int incumbent=before[cell],culture=s.Society.CellCulture[cell];var scores=localSupport[cell];
                    // Count the incumbent's lived neighborhood too. Ignoring it
                    // makes two neighboring faiths swap cells indefinitely.
                    int retained=incumbent<0?0:36+(h.Cultures[culture].ReligionId==incumbent?20:0)+(scores!=null&&scores.TryGetValue(incumbent,out int support)?support:0);
                    int best=-1,strongest=0;if(scores!=null)foreach(var pair in scores.OrderBy(p=>p.Key))if(pair.Key!=incumbent&&pair.Value>strongest){best=pair.Key;strongest=pair.Value;}
                    int margin=HistoricalFaithEffects.CompetitiveExposure(retained,strongest);proposed[cell]=margin>0?best:-1;exposure[cell]=margin;
                }
                int candidate=proposed[cell];faith.Influence[cell]=HistoricalFaithEffects.Accumulate(faith.Candidate[cell],faith.Influence[cell],candidate,exposure[cell]);
                if(candidate<0){if(faith.Influence[cell]==0)faith.Candidate[cell]=-1;continue;}
                faith.Candidate[cell]=candidate;
                if(!HistoricalFaithEffects.CanBecomeDominant(s.Years,faith.LastChanged[cell],faith.Influence[cell]))continue;
                h.CellReligion[cell]=candidate;faith.LastChanged[cell]=s.Years;faith.Candidate[cell]=-1;faith.Influence[cell]=0;
                if(townsByCell.TryGetValue(cell,out var town))HistoryEvent(s,"religious-community",h.Religions[candidate].Name+" becomes the predominant tradition at "+town.Name+" through sustained contact and local adoption.",cell,town.CultureId,town.PolityId,town:town.Id,religion:candidate);
            }
            foreach(var town in townsByCell.Values.OrderBy(t=>t.Id))
            {
                var hc=h.Cultures[town.CultureId];int contacts=s.Society.Cultures[town.CultureId].Contacts.Length;
                if(town.Population<5000||town.FoodCapacity<town.Population*.9||hc.Surplus<250||hc.Institution<450||s.Years-town.FoundedYear<300||HistoryRoll("faith-teacher",town.Id,s.Years)>.002)continue;
                bool portable=HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)||contacts>=2;
                int parent=h.CellReligion[town.Cell];if(parent<0)continue;int id=h.Religions.Length;var r=h.Religions[parent].Copy();r.Id=id;r.ParentId=parent;r.CultureId=town.CultureId;r.FoundedYear=s.Years;r.HolyCell=town.Cell;
                r.Reach=portable?ReligiousReach.Missionary:ReligiousReach.Portable;r.Name="Teaching of "+HistoricalName("faith-teacher-name",id);
                r.Tradition=portable?"Traveling teachers, hospitality houses and shared teachings descended from "+h.Religions[parent].Name:"A regional renewal of "+h.Religions[parent].Name;
                r.Ritual=portable?"Shared instruction, pilgrimage, charitable meals and local festivals":r.Ritual;
                h.Religions=h.Religions.Concat(new[]{r}).ToArray();h.CellReligion[town.Cell]=id;faith.LastChanged[town.Cell]=s.Years;faith.Candidate[town.Cell]=-1;faith.Influence[town.Cell]=0;
                HistoryEvent(s,"religious-reform",r.Name+" emerges at "+town.Name+"; "+(portable?"traveling teachers carry its practices through existing exchange networks.":"local communities reinterpret their inherited traditions."),town.Cell,town.CultureId,town.PolityId,town:town.Id,religion:id);
                if(town.PolityId>=0&&s.Society.Settlements[h.Polities[town.PolityId].CapitalId].Id==town.Id)h.Polities[town.PolityId].ReligionId=id;
            }
            // A court can follow a long-established capital tradition; conquest need not dictate belief.
            foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
            {
                int cell=s.Society.Settlements[p.CapitalId].Cell,religion=h.CellReligion[cell];
                if(religion<0||religion==p.ReligionId||s.Years-faith.LastChanged[cell]<200||HistoryRoll("court-conversion",p.Id,s.Years)>.08)continue;
                p.ReligionId=religion;HistoryEvent(s,"court-conversion",p.Ruler+" adopts "+h.Religions[religion].Name+" as the favored tradition of the court of "+p.Name+".",cell,p.CultureId,p.Id,religion:religion,category:ChronicleCategory.Society);
            }
        }
    }
}

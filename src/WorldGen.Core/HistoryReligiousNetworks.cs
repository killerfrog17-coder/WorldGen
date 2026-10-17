using System;
using System.Linq;

namespace WorldGen.Core
{
    // Optional actor-local revision. A dominant public tradition is neither a
    // census of belief nor a claim that household practices have disappeared.
    [Serializable] public sealed class HistoricalReligiousNetworks
    {
        public const int Fields=13;
        public int Version=2,SinceYear;
        public HistoricalCellTree Communities,Policies=new HistoricalCellTree(33554432);
        [System.Runtime.Serialization.OptionalField] public HistoricalLayAssociations LayAssociations;
        public HistoricalReligiousNetworks Copy(){var c=(HistoricalReligiousNetworks)MemberwiseClone();c.LayAssociations=LayAssociations?.Copy();return c;}
        public int Read(int cell,int field)=>Communities[cell*Fields+field];
        public void Write(int cell,int field,int value)=>Communities=Communities.With(cell*Fields+field,value);
    }
    public sealed class HistoricalReligiousCommunity
    {
        public int Dominant=-1,Household=-1,Visitor=-1,Pressure,Institution,Blending,LastContact=-1;
        public string Policy="Local practice without a state religious policy";
    }
    public static class HistoricalReligiousNetworkEffects
    {
        // Coefficients are explicit scenario priors, not fitted universal rates.
        public static int ActiveYears(int reviewed,int now,int firstContact,int lastContact)
            =>firstContact<0||lastContact<0?0:Math.Max(0,Math.Min(now,lastContact+80)-Math.Max(reviewed,firstContact));
        public static int Pressure(int previous,int elapsed,int contactYears,ReligiousReach reach,int institution,int tolerance,bool favored)
        {
            double network=reach==ReligiousReach.Missionary?3.0:reach==ReligiousReach.Portable?1.8:.65;
            double access=.30+.70*D.Clamp(tolerance/1000.0);
            double gain=contactYears*network*(.5+.5*D.Clamp(institution/1000.0))*access;
            if(favored)gain+=elapsed*.65;
            return Math.Max(0,Math.Min(1000,(int)Math.Round(previous+gain-Math.Max(0,elapsed-contactYears)*(favored?0:.8))));
        }
        public static bool Adopt(int pressure,int age,int residence)=>pressure>=700&&age>=150&&residence>=100;
        public static long FoodCost(long population,int elapsed)=>Math.Max(1,(long)Math.Ceiling(Math.Max(0,population)*Math.Max(0,elapsed)/2000.0));
        public static int Institution(int previous,int elapsed,bool funded,bool supported)
        {
            double target=funded&&supported?900:supported?150:0;
            return Math.Max(0,Math.Min(1000,(int)Math.Round(previous+(target-previous)*(1-Math.Exp(-elapsed/(funded?180.0:100.0))))));
        }
        public static int CarrierStrength(ReligiousReach reach,int support)
            =>(reach==ReligiousReach.Missionary&&support>=650?300:reach!=ReligiousReach.Local?180:65)+Math.Max(0,support)/10;
    }
    public sealed partial class Prehistory
    {
        const int FaithCandidate=0,FaithPressure=1,FaithReviewed=2,FaithChanged=3,FaithInstitution=4,FaithHousehold=5,
            FaithVisitor=6,FaithContactYear=7,FaithBlend=8,FaithDissent=9,FaithContactSince=10,FaithReformed=11,FaithCarrier=12;
        static bool ReligiousNetworkHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Religion!=null;
        void InitializeReligiousNetworks(PrehistoryState s)
        {
            if(ReligiousNetworkHistory(s))return;
            s.History.Aggregates.Empires.Connections.Religion=new HistoricalReligiousNetworks{Version=s.History.Aggregates.Empires.Connections.Realms?.Version>=4?2:1,SinceYear=s.Years,Communities=new HistoricalCellTree(checked(HistoricalCells.Count(s)*HistoricalReligiousNetworks.Fields))};
        }
        void TouchReligiousCommunity(PrehistoryState s,int cell)
        {
            var r=s.History.Aggregates.Empires.Connections.Religion;if(r.Read(cell,FaithReviewed)>=0)return;
            int faith=HistoricalCells.Religion(s,cell);
            r.Write(cell,FaithReviewed,s.Years);r.Write(cell,FaithChanged,s.Years);r.Write(cell,FaithHousehold,faith);
            r.Write(cell,FaithPressure,0);r.Write(cell,FaithInstitution,0);r.Write(cell,FaithBlend,0);r.Write(cell,FaithDissent,0);
        }
        // Only a successfully delivered, explicitly located contact may write
        // this memory. Retained culture IDs and shared ancestry are insufficient.
        void RecordReligiousContact(PrehistoryState s,int first,int second,int firstCell,int secondCell)
        {
            if(!ReligiousNetworkHistory(s)||firstCell<0||secondCell<0||firstCell==secondCell)return;
            if(firstCell>=CellPatch.Length||secondCell>=CellPatch.Length||first<0||second<0||first>=s.History.Cultures.Length||second>=s.History.Cultures.Length||!InstitutionalRouteAllowed(s,firstCell,secondCell,first))return;
            int a=HistoricalCells.Religion(s,firstCell),b=HistoricalCells.Religion(s,secondCell);
            if(LayReligiousHistory(s))
            {
                int carriedA=LayReligiousCarrier(s,firstCell,secondCell,out int supportA),carriedB=LayReligiousCarrier(s,secondCell,firstCell,out int supportB);
                SeedLayFromDeliveredContact(s,secondCell,carriedA,supportA);SeedLayFromDeliveredContact(s,firstCell,carriedB,supportB);
                if(carriedB!=a)RecordReligiousVisitorWithSupport(s,firstCell,secondCell,carriedB,supportB);
                if(carriedA!=b)RecordReligiousVisitorWithSupport(s,secondCell,firstCell,carriedA,supportA);
                return;
            }
            if(a==b)return;
            RecordReligiousVisitor(s,firstCell,secondCell,b);RecordReligiousVisitor(s,secondCell,firstCell,a);
        }
        void RecordReligiousVisitor(PrehistoryState s,int cell,int source,int religion)
            =>RecordReligiousVisitorWithSupport(s,cell,source,religion,-1);
        void RecordReligiousVisitorWithSupport(PrehistoryState s,int cell,int source,int religion,int suppliedSupport)
        {
            if(religion<0||CellPatch[cell]<0||HistoricalCells.Culture(s,cell)<0)return;
            TouchReligiousCommunity(s,cell);var r=s.History.Aggregates.Empires.Connections.Religion;
            // Rural endpoints have no settlement clock. Revisit just this
            // contacted actor so sustained country exchange can reach them.
            AdvanceReligiousCommunity(s,HistoricalCells.Culture(s,cell),cell,s.History.Aggregates.TownAt[cell]);
            int supported=suppliedSupport<0?Math.Max(0,r.Read(source,FaithInstitution)):suppliedSupport;
            int former=r.Read(cell,FaithVisitor);
            if(former>=0&&former!=religion&&s.Years-r.Read(cell,FaithContactYear)<=80)
            {
                int oldStrength=HistoricalReligiousNetworkEffects.CarrierStrength(s.History.Religions[former].Reach,Math.Max(0,r.Read(cell,FaithCarrier)));
                int newStrength=HistoricalReligiousNetworkEffects.CarrierStrength(s.History.Religions[religion].Reach,supported);
                if(newStrength<oldStrength||newStrength==oldStrength&&religion>former)return;
            }
            if(former!=religion){r.Write(cell,FaithVisitor,religion);r.Write(cell,FaithContactSince,s.Years);}
            r.Write(cell,FaithContactYear,s.Years);
            if(s.History.Religions[religion].Reach==ReligiousReach.Missionary&&supported>=650)
            {
                int city=s.History.Aggregates.TownAt[source];
                if(city<0)supported=0;
                else
                {
                    var town=s.Society.Settlements[city];long provisions=Math.Max(1,town.Population/2000);
                    if(town.FoodReserve-town.Population/2<provisions)supported=0;
                    else town.FoodReserve-=provisions;
                }
            }
            r.Write(cell,FaithCarrier,supported);
        }
        public HistoricalReligiousCommunity ReligiousCommunity(PrehistoryState s,int cell)=>ReligiousCommunityAt(s,cell);
        public static HistoricalReligiousCommunity ReligiousCommunityAt(PrehistoryState s,int cell)
        {
            if(!ReligiousNetworkHistory(s)||cell<0||cell>=HistoricalCells.Count(s))return null;
            var r=s.History.Aggregates.Empires.Connections.Religion;int polity=HistoricalCells.Polity(s,cell);
            return new HistoricalReligiousCommunity{Dominant=HistoricalCells.Religion(s,cell),Household=r.Read(cell,FaithHousehold),Visitor=r.Read(cell,FaithVisitor),
                Pressure=Math.Max(0,r.Read(cell,FaithPressure)),Institution=Math.Max(0,r.Read(cell,FaithInstitution)),Blending=Math.Max(0,r.Read(cell,FaithBlend)),LastContact=r.Read(cell,FaithContactYear),
                Policy=polity<0?"Local practice without a state religious policy":ReligiousPolicyName(r.Policies[polity*4+1])};
        }
        void RecordDeliveredRegionalReligion(PrehistoryState s,int source,int target)
        {
            // Revision 2 is the frozen A experiment. Its later null/military
            // migration cannot silently add rural delivery to those old rules.
            if((s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version??0)<3||!ReligiousNetworkHistory(s)||source<0||target<0||source>=CellPatch.Length||target>=CellPatch.Length)return;
            int first=HistoricalCells.Culture(s,source),second=HistoricalCells.Culture(s,target);
            if(first<0||second<0)return;
            // Read religion from the actual supplied communities. The unrelated
            // culture credited with advanced technical lessons is not a church.
            RecordReligiousContact(s,first,second,source,target);
        }
        void InheritReligiousHouseholds(PrehistoryState s,int target,int source,int previousFaith,long previousResidents)
        {
            if(MigrantReligiousHistory(s)){InheritMigrantTownReligiousHouseholds(s,target,source,previousFaith,previousResidents);return;}
            var r=s.History.Aggregates.Empires.Connections.Religion;int carried=HistoricalCells.Religion(s,source);if(carried<0)return;
            if(ReligiousInstitutionHistory(s)&&s.History.Religions[carried].Reach==ReligiousReach.Local&&ReligiousInstitutionVocation(s,carried)!=ReligiousVocation.SacredPlaces)s.History.Religions[carried].Reach=ReligiousReach.Portable;
            bool indigenous=previousResidents>0&&previousFaith>=0;
            SetAggregateLayer(s,target,2,indigenous?previousFaith:carried);TouchReligiousCommunity(s,target);
            int inherited=r.Read(source,FaithHousehold);
            r.Write(target,FaithHousehold,indigenous?carried:inherited>=0?inherited:carried);
            r.Write(target,FaithInstitution,0);r.Write(target,FaithPressure,0);r.Write(target,FaithDissent,0);
            r.Write(target,FaithCandidate,-1);r.Write(target,FaithVisitor,-1);r.Write(target,FaithContactYear,-1);r.Write(target,FaithContactSince,-1);r.Write(target,FaithCarrier,0);r.Write(target,FaithBlend,0);r.Write(target,FaithReformed,-1);
            r.Write(target,FaithReviewed,s.Years);r.Write(target,FaithChanged,s.Years);
            SeedLayReligiousCommunity(s,target,carried);
            if(indigenous&&previousFaith!=carried)
            {
                r.Write(target,FaithVisitor,carried);r.Write(target,FaithContactYear,s.Years);r.Write(target,FaithContactSince,s.Years);r.Write(target,FaithCarrier,0);
                int city=s.History.Aggregates.TownAt[target];
                HistoryEvent(s,"religious-diaspora","Migrant households at "+ReligiousPlace(s,target,city)+" bring "+s.History.Religions[carried].Name+" from their actual departure community. Existing local public rites continue; no religious institution is transported ready-made.",target,HistoricalCells.Culture(s,target),town:city,religion:carried);
            }
        }
        void AdvanceReligiousNetworks(PrehistoryState s,int culture,int contact,int elapsed)
        {
            var a=s.History.Aggregates;var sites=a.Cultures[culture].Cities;int origin=s.Society.Cultures[culture].OriginCell;
            // One rural/home community and one actual settlement actor per
            // culture event. Neither culture extent nor state extent is walked.
            AdvanceReligiousCommunity(s,culture,origin,-1);
            if(sites.Count==0)return;
            int at=(int)(HistoryRoll("religion-local-community-1",culture,s.Years)*sites.Count),city=sites.At(at);
            if(a.Living.Cities[city].AbandonedYear>=0)return;
            var town=s.Society.Settlements[city];var peers=a.Cities[city].Neighbors;
            // Contact memory is also delivered here for actual established town
            // links, after testing the present physical route and permission.
            int offset=peers.Length==0?0:(int)(HistoryRoll("religion-carrier-1",city,s.Years)*peers.Length);
            for(int k=0;k<Math.Min(2,peers.Length);k++)
            {
                int peer=peers[(offset+k)%peers.Length];var donor=s.Society.Settlements[peer];
                if(donor.Population<100||a.Living.Cities[peer].AbandonedYear>=0||!InstitutionalRouteAllowed(s,donor.Cell,town.Cell,donor.CultureId))continue;
                if(ReligiousInstitutionHistory(s)&&!ReligiousTownCarrierAllowed(s,peer,city))continue;
                RecordReligiousContact(s,donor.CultureId,town.CultureId,donor.Cell,town.Cell);
            }
            // The origin can also be a town; its clock prevents double upkeep.
            AdvanceReligiousCommunity(s,culture,town.Cell,city);
        }
        void AdvanceReligiousCommunity(PrehistoryState s,int culture,int cell,int city)
        {
            if(CellPatch[cell]<0||HistoricalCells.Culture(s,cell)<0)return;
            culture=HistoricalCells.Culture(s,cell);
            TouchReligiousCommunity(s,cell);var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;
            if(city<0)city=h.Aggregates.TownAt[cell];
            int reviewed=r.Read(cell,FaithReviewed),elapsed=s.Years-reviewed;if(elapsed<=0||LocalReligiousCityHistory(s)&&city>=0&&elapsed<10)return;
            r.Write(cell,FaithReviewed,s.Years);
            int incumbent=HistoricalCells.Religion(s,cell);if(incumbent<0)return;
            int owner=HistoricalCells.Polity(s,cell),tolerance=700,policy=0;
            if(owner>=0){EnsureReligiousPolicy(s,owner);policy=r.Policies[owner*4+1];tolerance=policy==2?180:policy==1?650:950;}
            int institution=AdvanceReligiousInstitution(s,cell,city,owner,elapsed);
            int visitor=r.Read(cell,FaithVisitor),contactYear=r.Read(cell,FaithContactYear);
            int contactYears=HistoricalReligiousNetworkEffects.ActiveYears(reviewed,s.Years,r.Read(cell,FaithContactSince),contactYear);
            int candidate=visitor;
            bool court=owner>=0&&policy!=0&&h.Polities[owner].ReligionId!=incumbent&&ReligiousPatronagePaid(s,owner,cell,elapsed);
            if(policy==2&&!court)tolerance=850; // An unfunded prohibition has weak local enforcement.
            // Patronage may reinforce a court tradition in its administered
            // locality; it changes neither the court's subjects nor all cells.
            if(court&&(candidate<0||contactYears==0))candidate=h.Polities[owner].ReligionId;
            bool favored=court&&candidate==h.Polities[owner].ReligionId;
            int pressure=Math.Max(0,r.Read(cell,FaithPressure));
            if(candidate>=0&&candidate!=incumbent)
            {
                if(r.Read(cell,FaithCandidate)!=candidate){pressure=0;r.Write(cell,FaithCandidate,candidate);}
                int carrier=Math.Max(0,r.Read(cell,FaithCarrier));var reach=h.Religions[candidate].Reach;
                if(reach==ReligiousReach.Missionary&&carrier<650)reach=ReligiousReach.Portable;
                pressure=HistoricalReligiousNetworkEffects.Pressure(pressure,elapsed,contactYears,reach,carrier,tolerance,favored);
                int blend=Math.Max(0,r.Read(cell,FaithBlend));
                if(contactYears>0)blend=Math.Min(1000,blend+(int)Math.Round(contactYears*(policy==2?.25:.9)));
                else blend=Math.Max(0,blend-elapsed/2);
                int priorBlend=Math.Max(0,r.Read(cell,FaithBlend));r.Write(cell,FaithBlend,blend);
                if(priorBlend<300&&blend>=300)
                    HistoryEvent(s,"religious-exchange","Households at "+ReligiousPlace(s,cell,city)+" adapt practices encountered through "+h.Religions[candidate].Name+" while retaining "+h.Religions[incumbent].Name+".",cell,culture,owner,town:city,religion:incumbent);
                int residence=s.Years-Math.Max(r.SinceYear,h.Religions[candidate].FoundedYear),age=s.Years-r.Read(cell,FaithChanged);
                if(HistoricalReligiousNetworkEffects.Adopt(pressure,age,residence))
                {
                    SeedLayReligiousCommunity(s,cell,incumbent);
                    SetAggregateLayer(s,cell,2,candidate);r.Write(cell,FaithChanged,s.Years);pressure=150;
                    // The previous public tradition remains a household memory.
                    r.Write(cell,FaithHousehold,incumbent);
                    HistoryEvent(s,"religious-community",h.Religions[candidate].Name+" becomes the predominant public tradition at "+ReligiousPlace(s,cell,city)+" after sustained local adoption; inherited household rites continue.",cell,culture,owner,town:city,religion:candidate);
                    incumbent=candidate;
                }
            }
            else pressure=Math.Max(0,pressure-elapsed);
            r.Write(cell,FaithPressure,pressure);
            if(owner>=0&&city>=0)
            {
                if(policy==2&&court&&h.Polities[owner].ReligionId!=incumbent)
                {
                    var town=s.Society.Settlements[city];town.Stability=Math.Max(0,town.Stability-Math.Min(30,elapsed/3));
                }
                AdvanceReligiousCourt(s,owner,city,incumbent,elapsed);
            }
            AdvanceLayReligiousCommunity(s,cell,city,owner,incumbent,institution,policy,court);
            TryReligiousRenewal(s,culture,cell,city,incumbent,institution,elapsed,owner>=0&&policy==2&&court&&h.Polities[owner].ReligionId!=incumbent);
        }
        string ReligiousPlace(PrehistoryState s,int cell,int city)=>city>=0?s.Society.Settlements[city].Name:"the home communities of "+s.Society.Cultures[HistoricalCells.Culture(s,cell)].Name;
        void ValidateReligiousNetworks(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.Religion;if(r==null)return;
            if(!LivingWorldHistory(s)||(r.Version!=1&&r.Version!=2)||r.SinceYear<0||r.SinceYear>s.Years||r.Communities?.Length!=HistoricalCells.Count(s)*HistoricalReligiousNetworks.Fields||r.Policies?.Length!=33554432)throw new ArgumentException("Invalid religious-network extension.");
            if(!r.Communities.AllPresentValues((key,value)=>
            {
                int field=key%HistoricalReligiousNetworks.Fields;
                if(field==FaithCandidate||field==FaithHousehold||field==FaithVisitor)return value>=0&&value<s.History.Religions.Length;
                if(field==FaithReviewed||field==FaithChanged||field==FaithContactYear||field==FaithContactSince||field==FaithReformed)return value>=r.SinceYear&&value<=s.Years;
                return value>=0&&value<=1000;
            })||!r.Policies.AllPresentValues((key,value)=>key/4<s.History.Polities.Length&&(key%4==0?value>=-1&&value<s.History.Dynamics.Leaders.Length:key%4==1?value>=0&&value<=2:key%4==2?value>=r.SinceYear&&value<=s.Years:value>=0&&value<=1000)))throw new ArgumentException("Invalid local religious account.");
        }
    }
}

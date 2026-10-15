using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ValidateAggregateHistory(PrehistoryState s)
        {
            // Explicit load/setup work. No lazily initialized terrain arrays
            // are permitted to appear inside a subsequent actor event.
            PrepareAggregateEnvironment(s);
            var h=s.History;var a=h.Aggregates;
            void Require(bool condition,string what){if(!condition)throw new ArgumentException("Invalid aggregate history: "+what);}
            Require((h.Version==AggregateHistoryVersion&&a.Living==null||(h.Version==LivingHistoryVersion||ConnectedHistory(s))&&a.Living!=null)&&a.Version=="aggregate-history-1"&&s.Society!=null,"version");
            if(CommunityHistory(s))Require(a.Communities!=null&&a.Communities.Cities.Length==a.Cities.Length&&a.Communities.Governments.Length==a.Polities.Length&&a.Communities.Campaigns.Length==h.Dynamics.Campaigns.Length,"local community alignment");
            else Require(a.Communities==null,"legacy community extension");
            if(CommunityHistory(s))
            {
                foreach(var local in a.Communities.Cities)Require(local!=null&&local.RulerId>=-1&&local.RulerId<h.Polities.Length&&local.SinceYear>=0&&local.SinceYear<=s.Years&&local.InclusionMilli>=0&&local.InclusionMilli<=1000,"local inclusion account");
                foreach(var policy in a.Communities.Governments)Require(policy!=null&&policy.RepresentationMilli>=0&&policy.RepresentationMilli<=1000&&policy.CentralizationMilli>=0&&policy.CentralizationMilli<=1000&&policy.LastReformYear>=0&&policy.LastReformYear<=s.Years,"government policy account");
                foreach(var campaign in a.Communities.Campaigns)Require(campaign!=null&&campaign.AttackerAreaKm2>=0&&campaign.DefenderAreaKm2>=0,"campaign area account");
            }
            Require(a.Cells?.Length==CellPatch.Length*5&&a.TownAt?.Length==CellPatch.Length&&a.Successors!=null&&a.LeaderDigests!=null,"persistent geography");
            Require(h.CellPolity.Length==0&&h.CellReligion.Length==0&&h.CellMarket.Length==0&&h.CellStage.Length==0&&s.Society.CellCulture.Length==0&&h.Faith==null&&h.RegionalLearning==null,"retired dense working arrays");
            Require(a.Cities.Length==s.Society.Settlements.Length&&a.Cities.Length==h.Towns.Length&&a.Cultures.Length==h.Cultures.Length&&a.Cultures.Length==s.Society.Cultures.Length&&a.Polities.Length==h.Polities.Length&&h.Dynamics.States.Length==h.Polities.Length&&a.Regions.Length==Patches.Length,"actor alignment");
            Require(h.Modern!=null&&Enum.IsDefined(typeof(HistoricalTechnologyCeiling),h.Modern.TechnologyCeiling),"technology ceiling");
            if(ProvincialHistory(s))Require(a.Provinces?.Centers?.Length==CellPatch.Length&&a.Provinces.FarmPotential.Length==a.Polities.Length&&a.Provinces.FarmPotential.All(v=>v>=0)&&a.Provinces.ClaimedAreaKm2.Length==a.Regions.Length&&a.Provinces.ClaimedAreaKm2.All(v=>v>=0),"provincial support accounts");
            else Require(a.Provinces==null,"legacy provincial extension");
            if(ProvincialHistory(s))
            {
                Require(a.Provinces.Exchanges.Length==a.Cultures.Length&&a.Provinces.RegionalKnowledge.Length==a.Regions.Length,"exchange memory alignment");
                Require(a.Provinces.Theaters.Length==h.Dynamics.Campaigns.Length&&a.Provinces.Theaters.All(t=>t.Cell>=0&&t.Cell<CellPatch.Length&&t.SupplyA>=-1&&t.SupplyA<a.Cities.Length&&t.SupplyB>=-1&&t.SupplyB<a.Cities.Length),"campaign theater alignment");
                foreach(var e in a.Provinces.Exchanges)Require(e.Peers.Length<=16&&e.Peers.Length==e.LastYear.Length&&e.Peers.Length==e.LastDirectYear.Length&&e.Peers.Length==e.Capabilities.Length&&e.Peers.All(p=>p>=0&&p<a.Cultures.Length)&&e.LastYear.All(y=>y<=s.Years),"bounded dated exchange offers");
            }
            if(LivingHistory(s))
            {
                var living=a.Living;Require(living.Version=="living-history-1"&&living.Cities.Length==a.Cities.Length&&living.Regions.Length==a.Regions.Length,"living actor alignment");
                Require(living.TownBuckets?.Length==180*90*16&&living.BucketCounts?.Length==180*90,"settlement spatial index");
                Require(living.CivicOrigins?.Length==33554432,"civic lineage index");
                for(int i=0;i<a.Cultures.Length;i++){int origin=living.CivicOrigins[i];Require(origin>=-1&&origin<a.Polities.Length&&(origin<0||h.Polities[origin].CultureId==i),"civic lineage reference");}
                foreach(var r in living.Regions)Require(r.Teacher>=-1&&r.Teacher<a.Cultures.Length&&r.CivicTeacher>=-1&&r.CivicTeacher<a.Polities.Length&&r.Exposure>=0&&r.Exposure<=1000&&r.CivicExposure>=0&&r.CivicExposure<=1000&&r.CommittedAreaKm2>=0,"local exchange and supply commitments");
                for(int i=0;i<living.Cities.Length;i++){var c=living.Cities[i];Require(c.AbandonedYear>=-1&&c.AbandonedYear<=s.Years&&c.Productivity>=0&&c.Productivity<=1&&c.DeclineYears>=0,"settlement life cycle");if(c.AbandonedYear>=0)Require(a.Cultures[s.Society.Settlements[i].CultureId].Cities.Positions[i]<0,"retired live-city membership");}
                for(int bucket=0;bucket<180*90;bucket++){int count=Math.Max(0,living.BucketCounts[bucket]);Require(count<=16,"settlement bucket bound");for(int k=0;k<count;k++){int id=living.TownBuckets[bucket*16+k];Require(id>=0&&id<a.Cities.Length&&living.Cities[id].AbandonedYear<0&&LivingTownBucket(World.Surface.Cells[s.Society.Settlements[id].Cell].Position)==bucket,"settlement index reference");}}
            }
            for(int cell=0;cell<CellPatch.Length;cell++)
            {int culture=HistoricalCells.Culture(s,cell),raw=a.Cells[cell*5+1],faith=HistoricalCells.Religion(s,cell),market=HistoricalCells.Market(s,cell),stage=HistoricalCells.Stage(s,cell);Require(culture>=-1&&culture<h.Cultures.Length&&raw>=-1&&raw<h.Polities.Length&&faith>=-1&&faith<h.Religions.Length&&market>=-1&&market<h.Towns.Length&&stage>=0&&stage<=3,"cell references");}
            long expected=s.Population.Sum()+s.Society.Settlements.Sum(t=>t.Population)+a.Cities.Sum(c=>c.Rural+c.Suburban)+a.Polities.Sum(p=>p.Remote);
            Require(expected==a.TotalPopulation&&a.TotalUrban==s.Society.Settlements.Sum(t=>t.Population),"population conservation "+expected+" != "+a.TotalPopulation);
            var urban=new long[h.Cultures.Length];var supported=new long[h.Cultures.Length];var supportCount=new int[h.Cultures.Length];
            foreach(var culture in h.Cultures)ValidateTechnicalPractice(s,culture);
            ValidateImperialSystem(s);
            ValidateTerritorialAdministration(s);
            var stateUrban=new long[h.Polities.Length];var stateRural=new long[h.Polities.Length];var stateSuburban=new long[h.Polities.Length];var settledRegions=new long[a.Regions.Length];
            for(int i=0;i<a.Cities.Length;i++)
            {
                var city=a.Cities[i];var town=s.Society.Settlements[i];Require(city.Id==i&&town.Id==i&&h.Towns[i].Id==i&&city.Rural>=0&&city.Suburban>=0&&city.RuralCapacity>=0&&town.Population>=0&&town.CultureId>=0&&town.CultureId<h.Cultures.Length&&city.LastYear<=s.Years&&city.Neighbors.Length<=12,"city");
                bool abandoned=LivingHistory(s)&&a.Living.Cities[i].AbandonedYear>=0;
                Require(abandoned?town.Population==0&&city.Rural==0&&city.Suburban==0&&!city.Supported:a.TownAt[town.Cell]==i&&HistoricalCells.Culture(s,town.Cell)==town.CultureId&&a.Resolve(town.PolityId,h)==HistoricalCells.Polity(s,town.Cell),"city ownership "+i+" at "+town.Cell+" culture "+town.CultureId+"/"+HistoricalCells.Culture(s,town.Cell)+" state "+town.PolityId+" resolved "+a.Resolve(town.PolityId,h)+"/"+HistoricalCells.Polity(s,town.Cell));
                Require(city.Neighbors.All(n=>n>=0&&n<a.Cities.Length&&n!=i),"city contact");urban[town.CultureId]+=town.Population;if(city.Supported){supported[town.CultureId]+=town.Population;supportCount[town.CultureId]++;}
                if(LivingHistory(s))settledRegions[CellPatch[town.Cell]]+=town.Population+city.Rural+city.Suburban;
                int owner=a.Resolve(town.PolityId,h);if(owner>=0){stateUrban[owner]+=town.Population;stateRural[owner]+=city.Rural;stateSuburban[owner]+=city.Suburban;}
            }
            for(int i=0;i<a.Cultures.Length;i++)
            {
                var c=a.Cultures[i];var hc=h.Cultures[i];Require(c.Id==i&&s.Society.Cultures[i].Id==i&&hc.Id==i&&c.Urban==urban[i]&&c.SupportedUrban==supported[i]&&c.SupportedCities==supportCount[i]&&c.Cities.Count==c.CityCount,"culture aggregates");Require(hc.Technology.Length==HistoricalTechnologies.Names.Length&&hc.Research.Length==hc.Technology.Length&&hc.Technology.All(y=>y>=0&&y<=s.Years+1),"capability dates");
                // Under the leveled model, delivered knowledge may precede a
                // local manufacturing chain. Invention checks the graph before
                // spending effort; legacy capability dates keep their old rule.
                if(!TechnicalHistory(s))for(int k=0;k<hc.Technology.Length;k++)if(hc.Technology[k]>0)Require(HistoricalTechnologies.Requirements(ProvincialHistory(s)&&hc.Technology[k]-1<=a.Provinces.UpgradedYear?ConnectedHistoryVersion:h.Version,k).All(p=>hc.Technology[p]>0&&hc.Technology[p]<=hc.Technology[k]),"capability prerequisites");
            }
            for(int i=0;i<a.Polities.Length;i++)
            {
                var p=h.Polities[i];var account=a.Polities[i];Require(p.Id==i&&account.Id==i&&account.Remote>=0&&account.RemoteCapacity>=0&&p.CapitalId>=0&&p.CapitalId<a.Cities.Length,"state");
                if(p.EndedYear<0)Require(account.Urban==stateUrban[i]&&account.Rural==stateRural[i]&&account.Suburban==stateSuburban[i]&&p.Population==account.Urban+account.Rural+account.Suburban+account.Remote,"state population aggregates");
                int successor=a.Successors[i];Require(successor==-1||successor>i&&successor<h.Polities.Length,"succession graph");
            }
            for(int i=0;i<h.Archive.Count;i++)Require(h.Archive[i].Id==i&&h.Archive[i].Year<=s.Years,"chronicle");
            if(LivingHistory(s))for(int i=0;i<a.Regions.Length;i++)Require(a.Living.Regions[i].SettledPopulation==settledRegions[i],"settled regional population account");
            for(int i=0;i<a.Regions.Length;i++)Require(a.Regions[i].Id==i&&a.Regions[i].Capacity>=0&&a.Regions[i].Routes.All(r=>r>=0&&r<Routes.Length),"regional geography");
        }
    }
}

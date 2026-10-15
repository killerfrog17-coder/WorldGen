using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public PrehistoryState SetStepYears(PrehistoryState source,int years,bool automatic=false)
        {
            Validate(source);if(years<1||years>10000)throw new ArgumentOutOfRangeException(nameof(years));
            if(source.Society==null)throw new InvalidOperationException("Start a new world to use historical phases and variable year steps.");
            var s=source.Copy();s.Society.ManualStep=!automatic;s.Society.SelectedStepYears=automatic?AutomaticYears(s):years;s.Seal();return s;
        }
        public PrehistoryState AdvanceYears(PrehistoryState source,int years,CancellationToken token=default)
        {
            using(ProfileWork("checkpoint validation"))Validate(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            if(years<1||years>1000000||source.Years>10000000-years)throw new ArgumentOutOfRangeException(nameof(years));
            if(source.Timeline!=null||ModernHistory(source))return AdvanceCalendar(ModernHistory(source)?UpgradeScheduling(source):source,years*SimulationTime.Year,int.MaxValue,token,false);
            if(source.Version!="prehistory-4"&&source.Version!="prehistory-5"&&source.Version!="prehistory-6"){int unit=source.Version=="prehistory-1"?250:100;if(years%unit!=0)throw new ArgumentException("Legacy saves use their original step size.");return Advance(source,years/unit,token);}
            var s=source.Copy();int target=source.Years+years;
            while(s.Years<target)
            {
                token.ThrowIfCancellationRequested();int next=Math.Min(target,(s.Years/50+1)*50);
                if(s.Society.Settlements.Length>0)next=s.Years+1;
                s.Years=next;
                if(next%100==0)
                {
                    // Reuse the demographic solver, with this checkpoint's cultural capacity and knowledge.
                    AdvanceDemographicBoundary(s,token);
                }
                if(next%50==0){if(s.History!=null)AdvanceHistoricalCultures(s,token);else AdvanceCultures(s,token);}
                if(s.Society.Settlements.Length>0){if(s.History!=null)AdvanceHistoricalYear(s,token);else AdvanceSettlements(s);}
            }
            RefreshSocietyTotals(s);s.Seal();return s;
        }
        void AdvanceDemographicBoundary(PrehistoryState s,CancellationToken token)
        {
            var demographic=s.Copy(false);demographic.Timeline=null;demographic.Version="prehistory-3";demographic.Years=s.Years-100;demographic.Seal();
            demographic=Advance(demographic,1,token);s.Population=demographic.Population;s.ArrivalYear=demographic.ArrivalYear;s.Ancestry=demographic.Ancestry;
            if(EraHistory(s))s.Society.Knowledge=demographic.Society.Knowledge;AdvanceKnowledge(s);
        }
        long SocietyCapacity(PrehistoryState state,int patch)
        {
            var h=state.Society;long baseline=CapacityAt(patch,state.Years,!LocalHistory(state)&&UsesGlacialDispersal(state));
            if(h==null||h.CellCulture.Length==0)return baseline;
            double multiplier=0;
            foreach(int cell in Patches[patch].Cells)
            {
                int owner=h.CellCulture[cell];
                if(owner<0){multiplier+=1;continue;}
                if(RevisedHistory(state))
                {
                    double production=h.Cultures[owner].Farming/35.0*AgriculturalYield(state,cell)*(EraHistory(state)?CropYieldFactor(state,owner,cell,false):1);
                    if(RealismHistory(state))production*=3*AnimalFoodMultiplier(state,owner,cell);
                    if(ModernHistory(state))production*=HistoricalModernEffects.FoodMultiplier(state.History.Cultures[owner],state.Years)*ManagedLandMultiplier(state,owner,cell);
                    multiplier+=1+production;
                }
                else multiplier+=state.History==null?1+h.Cultures[owner].Farming/180.0:1+h.Cultures[owner].Farming/90.0*FarmingSuitability(cell);
            }
            return (long)Math.Round(baseline*multiplier/Patches[patch].Cells.Length);
        }
        void AdvanceKnowledge(PrehistoryState s)
        {
            if(LocalHistory(s))return; // Historical fields survive only for legacy compatibility.
            var h=s.Society;var before=(int[])h.Knowledge.Clone();
            foreach(var route in Routes)if(s.Population[route.A]>0&&s.Population[route.B]>0&&(!route.Sea||RouteChance(route,UsesGlacialDispersal(s)?s.Years:0)>.02))
            {h.Knowledge[route.A]=Math.Max(h.Knowledge[route.A],before[route.B]*98/100);h.Knowledge[route.B]=Math.Max(h.Knowledge[route.B],before[route.A]*98/100);}
            for(int p=0;p<Patches.Length;p++)if(s.Population[p]>0)
            {
                var habitat=World.Ecology.Habitat[Patches[p].Cell];
                int gain=2+(int)(3*D.Clamp(s.Population[p]/800.0))+Math.Max(0,habitat.GrowingDays)/120;
                h.Knowledge[p]=Math.Min(10000,h.Knowledge[p]+gain);
            }
        }
        double FarmingSuitability(int cell)
        {
            var h=World.Ecology.Habitat[cell];
            double water=Math.Max(D.Clamp(h.AridityMilli/500.0),h.Flooding==FloodRegime.RiverSeasonal||h.Flooding==FloodRegime.RiverPersistent?.9:0);
            return D.Q(water*D.Clamp(h.GrowingDays/220.0)*D.Clamp(h.SoilFertilityPermille/700.0)*D.Clamp(1-h.ReliefM/2000.0)*D.Clamp(1-h.SalinityMilliPpt/5000.0),1000000);
        }
        void Record(PrehistoryState s,CultureIdentity c,ChronicleCategory category,string message,int cell=-1)
        {
            if(s.History!=null)return; // Historical events use the linked lifetime archive and its short UI feed.
            var items=s.Society.Events.ToList();items.Add(new ChronicleEvent{Year=s.Years,CultureId=c.Id,Cell=cell<0?c.OriginCell:cell,Category=category,Text=message});
            if(items.Count>256)items.RemoveRange(0,items.Count-256);s.Society.Events=items.ToArray();
        }
        CultureIdentity FoundCulture(PrehistoryState s,int patch,int cell)
        {
            var h=s.Society;int id=h.Cultures.Length;var rng=new Rng(World.Recipe.Seed,"culture-name",cell);
            string[] starts={"Ara","Bel","Dara","Ena","Isha","Kera","Luma","Mara","Nara","Oru","Sela","Tavi","Una","Vala"};
            string[] ends={"ni","ra","si","tan","ven","li","mi","sha","ren","ka"};
            string name=starts[(int)(rng.Unit()*starts.Length)]+ends[(int)(rng.Unit()*ends.Length)]+" "+(id+1);
            var p=Patches[patch];var habitat=World.Ecology.Habitat[cell];
            var taxa=p.Region<0?Array.Empty<Taxon>():World.Ecology.Regions[p.Region].Species.Select(x=>World.Ecology.Taxa[x.TaxonId]).ToArray();
            if(RealismHistory(s))taxa=taxa.Where(t=>Biogeography.Present(World.Ecology,cell,t.Id)).ToArray();
            var prey=taxa.Where(t=>!t.Producer&&!t.BasalResource&&t.MassKg>=1&&t.MassKg<1000&&(t.Occupies&Stratum.Ground)!=0&&(t.Eats&Food.Vertebrate)==0).OrderBy(t=>t.Id).ToArray();
            var plants=taxa.Where(t=>t.Producer&&(t.Provides&(Food.Seed|Food.Fruit|Food.Grass))!=0).OrderBy(t=>t.Id).ToArray();
            int fishing=p.Lifestyle==ForagerLifestyle.Coastal||habitat.Flooding!=FloodRegime.None?450:50;
            if(RealismHistory(s))fishing=(int)Math.Round(50+400*Math.Max(p.Lifestyle==ForagerLifestyle.Coastal?1:0,Math.Min(1,HistoricalRiverAccess(cell)*5)));
            int hunting=prey.Length>0?300:100,herding=taxa.Any(t=>t.ScientificName=="Rangifer tarandus")?100:0;
            var c=new CultureIdentity{Id=id,Name=name,OriginCell=cell,FoundedYear=s.Years,StageYear=s.Years,Fishing=fishing,Hunting=hunting,Herding=herding,Gathering=1000-fishing-hunting-herding,
                Resources=string.Join(", ",plants.Take(3).Concat(prey.Take(3)).Select(t=>t.Name)),Subsistence=fishing>300?"Coastal and river foragers":herding>0?"Hunting and managed reindeer":habitat.Habitat.HasFlag(Habitat.Forest)?"Woodland gathering and hunting":"Seasonal hunting and seed gathering"};
            if(h.CellCulture.Length==0)h.CellCulture=Enumerable.Repeat(-1,World.Surface.Cells.Length).ToArray();
            SetCultureOwner(s,cell,id);h.Cultures=h.Cultures.Concat(new[]{c}).ToArray();Record(s,c,ChronicleCategory.Society,c.Name+" emerges as a distinct regional culture.");return c;
        }
        void AdvanceCultures(PrehistoryState s,CancellationToken token)
        {
            var h=s.Society;
            if(s.Years%100==0)
            {
                var established=new bool[Patches.Length];foreach(var c in h.Cultures)established[CellPatch[c.OriginCell]]=true;
                for(int p=0;p<Patches.Length;p++)if(!Patches[p].Island&&!established[p]&&s.Population[p]>=400&&h.Knowledge[p]>=4500)
                {
                    double chance=.08*D.Clamp((h.Knowledge[p]-4000)/6000.0);
                    if(Rng.Value(World.Recipe.Seed,"culture-emergence",unchecked(p*7919+s.Years))>chance)continue;
                    int cell=Patches[p].Cells.Where(i=>World.Hydrology.LakeCoverageFraction[i]<.5&&(h.CellCulture.Length==0||h.CellCulture[i]<0)).OrderByDescending(FarmingSuitability).ThenBy(i=>i).DefaultIfEmpty(-1).First();
                    if(cell>=0)FoundCulture(s,p,cell);
                }
            }
            if(h.Cultures.Length==0)return;
            // One graph frontier per 50-year event; no same-event flood fill and no whole-ecoregion claims.
            var claims=new SortedDictionary<int,int>();var contacts=new SortedSet<(int,int)>();
            for(int cell=0;cell<h.CellCulture.Length;cell++)
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=h.CellCulture[cell];if(owner<0)continue;
                foreach(int next in World.Surface.Cells[cell].Neighbors)
                {
                    int other=h.CellCulture[next],p=CellPatch[next];if(other>=0){if(other!=owner)contacts.Add((Math.Min(owner,other),Math.Max(owner,other)));continue;}
                    if(p<0||World.Hydrology.OceanConnected[next]||World.Hydrology.LakeCoverageFraction[next]>=.9||s.Population[p]<80)continue;
                    double similarity=HabitatSimilarity(CellPatch[cell],p);
                    double distance=D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[next].Position)*World.Recipe.RadiusKm;
                    // Cultural identification is harder to maintain far from its founding contact network.
                    // This is not an army or an empire projecting unlimited territorial control.
                    double homelandDistance=D.Angle(World.Surface.Cells[h.Cultures[owner].OriginCell].Position,World.Surface.Cells[next].Position)*World.Recipe.RadiusKm;
                    double probability=(.06+.4*similarity)*Math.Exp(-distance/180-homelandDistance/900)*(1+h.Cultures[owner].Farming/2000.0);
                    if(Rng.Value(World.Recipe.Seed,"culture-frontier",unchecked(cell*32771+next*31+s.Years))<probability&&(!claims.TryGetValue(next,out int prior)||owner<prior))claims[next]=owner;
                }
            }
            foreach(var claim in claims)SetCultureOwner(s,claim.Key,claim.Value);
            foreach(var pair in contacts)
            {
                var a=h.Cultures[pair.Item1];var b=h.Cultures[pair.Item2];
                if(!a.Contacts.Contains(b.Id)){a.Contacts=a.Contacts.Concat(new[]{b.Id}).OrderBy(i=>i).ToArray();b.Contacts=b.Contacts.Concat(new[]{a.Id}).OrderBy(i=>i).ToArray();Record(s,a,ChronicleCategory.Politics,a.Name+" establishes exchange with "+b.Name+".");}
            }
            RefreshSocietyTotals(s);
            foreach(var c in h.Cultures)
            {
                int p=CellPatch[c.OriginCell];double suitability=FarmingSuitability(c.OriginCell);
                double warmth=h.Phase>=SocietyStage.EmbryonicCivilization?1:1-.65*GlacialIntensity(s.Years);
                if(c.Farming==0&&h.Knowledge[p]>=6500&&s.Years-c.FoundedYear>=1500&&suitability>.15&&Rng.Value(World.Recipe.Seed,"plant-tending",unchecked(c.Id*7919+s.Years))<.015*suitability*warmth)
                {c.Farming=50;Record(s,c,ChronicleCategory.Discoveries,c.Name+" begins sustained plant cultivation.");}
                if(c.Farming>0&&c.Stage<SocietyStage.Civilization)c.Farming=Math.Min(800,c.Farming+Math.Max(1,(int)(12*suitability)));
                if(c.Farming>0)c.Subsistence=World.Ecology.Habitat[c.OriginCell].Flooding!=FloodRegime.None?"Floodplain cultivation with fishing and gathering":"Mixed cultivation, gathering and hunting";
                if(c.Stage<SocietyStage.Civilization)c.Organization=Math.Min(800,c.Organization+(c.Farming>300?3:1)+Math.Min(3,c.Contacts.Length));
                var towns=h.Settlements.Where(t=>t.CultureId==c.Id).ToArray();
                if(c.Farming>=350&&c.Population>=2500&&towns.Length==0)FoundSettlement(s,c,c.OriginCell);
                if(c.Stage==SocietyStage.AdvancedCulture&&c.Farming>=400&&c.Organization>=300&&towns.Any(t=>t.Population>=1000))
                {c.Stage=SocietyStage.EmbryonicCivilization;c.StageYear=s.Years;Record(s,c,ChronicleCategory.Society,c.Name+" develops permanent central settlements and regional institutions.");}
                if(c.Stage>=SocietyStage.EmbryonicCivilization&&s.Years%500==0&&towns.Length<12)
                {
                    int candidate=-1;double best=0;
                    for(int cell=0;cell<h.CellCulture.Length;cell++)if(h.CellCulture[cell]==c.Id&&!towns.Any(t=>t.Cell==cell))
                    {
                        double quality=FarmingSuitability(cell);if(quality<=best||s.Population[CellPatch[cell]]<400)continue;
                        if(towns.Any(t=>D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[t.Cell].Position)*World.Recipe.RadiusKm<150))continue;
                        best=quality;candidate=cell;
                    }
                    if(candidate>=0)FoundSettlement(s,c,candidate);
                }
                if(c.Stage==SocietyStage.EmbryonicCivilization&&c.Farming>=600&&c.Organization>=600&&c.Population>=15000&&towns.Length>=2&&towns.Sum(t=>t.Population)>=4000)
                {c.Stage=SocietyStage.Civilization;c.StageYear=s.Years;Record(s,c,ChronicleCategory.Society,c.Name+" forms a city-based civilization. Its city polities remain independent.");}
            }
            if(!h.ManualStep)h.SelectedStepYears=SocietyState.DefaultStep(h.Phase);
        }
        void FoundSettlement(PrehistoryState s,CultureIdentity c,int cell,long founderLimit=400,bool arrivingTogether=false)
        {
            int patch=CellPatch[cell];long founders=Math.Min(founderLimit,arrivingTogether?s.Population[patch]:s.Population[patch]/5);if(founders<100)return;s.Population[patch]-=founders;
            var h=s.Society;int id=h.Settlements.Length;var town=new Settlement{Id=id,CultureId=c.Id,PolityId=id,Cell=cell,FoundedYear=s.Years,Name=c.Name+" "+(h.Settlements.Count(t=>t.CultureId==c.Id)+1),Population=founders,FoodReserve=founders,FoodCapacity=founders*3,CultivatedPermille=c.Farming};
            h.Settlements=h.Settlements.Concat(new[]{town}).ToArray();Record(s,c,ChronicleCategory.Politics,town.Name+" establishes a permanent settlement and local council.",cell);
        }
        void AdvanceSettlements(PrehistoryState s)
        {
            var h=s.Society;var counts=new int[h.Cultures.Length];foreach(var t in h.Settlements)counts[t.CultureId]++;
            foreach(var t in h.Settlements)
            {
                var c=h.Cultures[t.CultureId];int patch=CellPatch[t.Cell];
                t.FoodCapacity=Math.Max(600,Math.Min(30000,(long)(c.AreaKm2*.12*(c.Farming/800.0)*(.3+.7*FarmingSuitability(t.Cell))/counts[c.Id])));
                double weather=.88+.24*Rng.Value(World.Recipe.Seed,"settlement-harvest",unchecked(t.Id*7919+s.Years/10));
                long harvest=(long)(t.FoodCapacity*weather);t.FoodReserve=Math.Max(0,Math.Min(t.FoodCapacity*3,t.FoodReserve+harvest-t.Population));
                double balance=(harvest+Math.Min(t.FoodReserve,t.Population*.3))/Math.Max(1.0,t.Population);
                double growth=t.Population*.008*(1-t.Population/(double)t.FoodCapacity);if(balance<1)growth-=t.Population*(1-balance)*.03;
                long delta=(long)Math.Floor(growth);if(Rng.Value(World.Recipe.Seed,"settlement-demography",unchecked(t.Id*104729+s.Years))<growth-delta)delta++;
                t.Population=Math.Max(25,t.Population+delta);t.Health=(int)(600+200*D.Clamp(balance));t.Stability=(int)(500+250*D.Clamp(balance));t.CultivatedPermille=c.Farming;
                if(s.Years%10==0&&t.Population<t.FoodCapacity*.8){long incoming=Math.Min(100,s.Population[patch]/200);s.Population[patch]-=incoming;t.Population+=incoming;}
            }
        }
        long[] ruralAllocationSource,ruralAllocationValue;
        public long RuralCellPopulation(PrehistoryState s,int cell)
        {
            if(AggregateHistory(s))return AggregateRuralCellPopulation(s,cell);
            int patch=cell>=0&&cell<CellPatch.Length?CellPatch[cell]:-1;if(patch<0)return 0;
            if(EraHistory(s)&&ruralPatchWeight[patch]>0)
            {
                long population=s.Population[patch];
                // The exact allocation depends only on this patch's current
                // population and immutable world weights, not year/ownership.
                // Memoize on demand inside private work; never freeze a region's
                // population or assume that a changed patch needs a world scan.
                if(UseHistoryIndexes&&historyWorkActive)
                {
                    if(ruralAllocationSource==null){ruralAllocationSource=Enumerable.Repeat(-1L,CellPatch.Length).ToArray();ruralAllocationValue=new long[CellPatch.Length];}
                    if(ruralAllocationSource[cell]==population)return ruralAllocationValue[cell];
                    long allocated=AllocateRuralShare(population,ruralPrefix[cell],ruralWeight[cell],ruralPatchWeight[patch]);
                    ruralAllocationSource[cell]=population;ruralAllocationValue[cell]=allocated;return allocated;
                }
                return AllocateRuralShare(population,ruralPrefix[cell],ruralWeight[cell],ruralPatchWeight[patch]);
            }
            int count=Patches[patch].Cells.Length,rank=Array.BinarySearch(Patches[patch].Cells,cell);return s.Population[patch]/count+(rank<s.Population[patch]%count?1:0);
        }
        public static long AllocateRuralShare(long population,long prefix,long weight,long total)
        {
            if(population<0||prefix<0||weight<0||total<=0||prefix>total-weight)throw new ArgumentOutOfRangeException(nameof(weight));
            long Fraction(long end)=>population==0?0:population<=long.MaxValue/Math.Max(1,end)?population*end/total:(long)decimal.Floor((decimal)population*end/total);
            return Fraction(prefix+weight)-Fraction(prefix);
        }
        public void RefreshSocietyTotals(PrehistoryState s)
        {
            var h=s.Society;if(h==null||h.CellCulture.Length==0)return;
            if(RefreshIndexedSocietyTotals(s))return;
            foreach(var c in h.Cultures){c.Population=0;c.AreaKm2=0;}
            for(int i=0;i<h.CellCulture.Length;i++)if(h.CellCulture[i]>=0){var c=h.Cultures[h.CellCulture[i]];c.Population+=RuralCellPopulation(s,i);c.AreaKm2+=(long)Math.Round(RevisedHistory(s)?HistoricalLandArea(i):World.Surface.Cells[i].AreaM2/1e6);}
            foreach(var town in h.Settlements)h.Cultures[town.CultureId].Population+=town.Population;
        }
        void ValidateSociety(PrehistoryState s)
        {
            var h=s.Society;if(h==null||h.Version!="early-societies-1"||h.Knowledge.Length!=Patches.Length||h.Knowledge.Any(k=>k<0||k>10000)||h.SelectedStepYears<1||h.SelectedStepYears>10000||h.CellCulture.Length!=0&&h.CellCulture.Length!=World.Surface.Cells.Length||h.CellCulture.Any(i=>i< -1||i>=h.Cultures.Length)||h.Cultures.Length>0&&h.CellCulture.Length==0)throw new ArgumentException("Invalid society checkpoint.");
            for(int i=0;i<h.Cultures.Length;i++){var c=h.Cultures[i];if(c.Id!=i||c.OriginCell<0||c.OriginCell>=CellPatch.Length||CellPatch[c.OriginCell]<0||c.Stage<SocietyStage.AdvancedCulture||c.Stage>SocietyStage.Civilization||c.Farming<0||c.Farming>1000||c.Organization<0||c.Organization>1000)throw new ArgumentException("Invalid culture.");}
            for(int i=0;i<h.Settlements.Length;i++){var t=h.Settlements[i];if(t.Id!=i||t.CultureId<0||t.CultureId>=h.Cultures.Length||t.Cell<0||t.Cell>=CellPatch.Length||h.CellCulture[t.Cell]!=t.CultureId||t.Population<0||t.FoodReserve<0)throw new ArgumentException("Invalid settlement.");}
        }
    }
}

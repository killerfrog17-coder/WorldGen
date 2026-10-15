using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    // Explicit historical opportunities, not intelligence, latitude bonuses or
    // a physical climate reconstruction. Numerical rates remain game priors.
    public static class HistoricalLocalEffects
    {
        public const int AgricultureOpeningYear=100000;
        public static bool AgricultureOpen(int year)=>year>=AgricultureOpeningYear;
        // A newly occupied habitat is not already a persistent, specialist
        // storage/exchange network. This is a soft local opportunity modifier,
        // not an intelligence score or a global calendar unlock.
        public static double RegionalTraditionOpportunity(int occupiedYears)
        {double established=1-Math.Exp(-Math.Max(0,occupiedYears)/12000.0);return D.Q(.12*established*established,1000000000);}
        public static double CommunityChance(double storedFood,double seasonalConcentration,double density,double crowding,long residents)
        {
            if(residents<600||density<.022||storedFood<.15)return 0;
            double groups=Math.Min(4,Math.Sqrt(residents/2000.0));
            return D.Q(.00035*Math.Pow(D.Clamp(storedFood/.5),2)*(.12+.88*D.Clamp(seasonalConcentration))*D.Clamp(density/.08)*(.3+.7*D.Clamp(crowding))*Math.Max(.5,groups),1000000000);
        }
        public static double FoundationReadiness(double storage,double concentration,double institutions,double coordination,double circumscription,double exchange)
            =>D.Q(D.Clamp(storage)*D.Clamp(concentration)*D.Clamp(institutions)*D.Clamp(.18+.55*coordination+.45*circumscription+.3*exchange),1000000);
        public static double FoundationChance(double readiness)=>readiness<.18?0:D.Q(.00005+.0015*readiness*readiness,1000000000);
        public static double AdministrativeReachKm(long urban,int administration,int works,bool writing,bool roads)
            =>D.Q((35+.06*administration+.025*works+(writing?35:0)+(roads?60:0))*(.6+.4*D.Clamp(Math.Sqrt(Math.Max(0,urban)/80000.0))),1000000);
    }
    public sealed partial class Prehistory
    {
        static bool LocalHistory(PrehistoryState s)=>s.History?.Version=="history-10"||DelegatedHistory(s);
        static bool DelegatedHistory(PrehistoryState s)=>s.History?.Version=="history-11"||InstitutionalHistory(s);
        public long AssessForagerCapacity(PrehistoryState s,int patch)=>CapacityAt(patch,s.Years,!LocalHistory(s)&&UsesGlacialDispersal(s));
        public int AssessAdministrativeOwner(PrehistoryState s,int cell,CancellationToken token=default)
        {Validate(s);return AggregateHistory(s)?HistoricalCells.Polity(s,cell):CivicInfluence(s,token)[cell];}
        public int[] AssessAdministrativeOwners(PrehistoryState s,CancellationToken token=default)
        {Validate(s);return (int[])CivicInfluence(s,token).Clone();}
        public int[] AssessAdministrativeCommunication(PrehistoryState s,CancellationToken token=default)
        {Validate(s);if(!DelegatedHistory(s))throw new InvalidOperationException("Separate capital communication requires history11.");return (int[])BuildAdministrativeNetwork(s,token).CommunicationOwner.Clone();}
        public HistoricalAdministrativeAccount[] AssessAdministrativeAccounts(PrehistoryState s,CancellationToken token=default)
        {Validate(s);if(!LocalHistory(s))throw new InvalidOperationException("Administrative accounts require the current historical model.");return (AggregateHistory(s)?s.History.Administration.Accounts:BuildAdministrativeNetwork(s,token).Accounts).Select(a=>a.Copy()).ToArray();}
        double CommunityConcentration(int cell)
        {
            PrepareHistoricalEnvironment();var a=World.Ecology.Habitat[cell];
            double season=D.Clamp(Math.Max(a.WarmSeasonDryPermille/700.0,(a.WarmTenthsC-a.ColdTenthsC)/220.0));
            bool coast=World.Surface.Cells[cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
            return D.Q(D.Clamp(Math.Max(season,Math.Max(riverSites[cell],coast?.65:0))),1000000);
        }
        double LocalCommunityChance(PrehistoryState s,int patch,int cell)
        {
            double chance=HistoricalLocalEffects.CommunityChance(CultureOpportunity(s,cell),CommunityConcentration(cell),HistoricalSiteDensity(s,cell),s.Population[patch]/(double)Math.Max(1,CapacityAt(patch,s.Years,false)),s.Population[patch]);
            if(RealismHistory(s)){PrepareLandscape();double localConcentration=Math.Max(landscapeSeason[cell],Math.Min(.7,HistoricalRiverAccess(cell)*3));bool coastal=World.Surface.Cells[cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);if(coastal)localConcentration=Math.Max(localConcentration,.65);chance=HistoricalLocalEffects.CommunityChance(CultureOpportunity(s,cell),localConcentration,HistoricalSiteDensity(s,cell),s.Population[patch]/(double)Math.Max(1,CapacityAt(patch,s.Years,false)),s.Population[patch]);}
            if(!ModernHistory(s))return chance;
            var a=World.Ecology.Habitat[cell];double stored=WildCropStorage(cell),concentration=CommunityConcentration(cell);
            // Local, storable abundance supports persistent exchange specialists.
            // Low-concentration forests retain a long stochastic tail, not a ban.
            double substrate=.08+.92*D.Clamp(stored/.4)*(.2+.8*concentration);
            double establishment=(s.History.Version=="history-14"||(s.History.Version=="history-15"||s.History.Version=="history-16"))?HistoricalLocalEffects.RegionalTraditionOpportunity(s.ArrivalYear[patch]<0?0:s.Years-s.ArrivalYear[patch]):1;
            if(EmergenceHistory(s))
            {
                bool coast=World.Surface.Cells[cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
                if(RealismHistory(s))stored*=LandscapeCropFactor(cell);
                substrate*=HistoricalEmergenceEffects.EcologicalConcentration(a,concentration,stored,Math.Max((RealismHistory(s)?HistoricalRiverAccess(cell)*2:riverSites[cell]*.65),coast?.55:0));
            }
            return D.Q(chance*substrate*HistoricalCropCatalog.LeachingPenalty(a,false)*establishment,1000000000);
        }
        double LocalExchangeConcentration(PrehistoryState s,Settlement town)
        {
            PrepareLocalExchangeNeighbors(s);int neighbors=0;foreach(int id in localExchangeNeighbors[town.Id])if(s.Society.Settlements[id].Population>=500)neighbors++;
            return Math.Min(1,neighbors/4.0);
        }
        PrehistoryState localExchangeState;Settlement[] localExchangeTowns;int[] localExchangeCells=Array.Empty<int>();
        System.Collections.Generic.List<int>[] localExchangeNeighbors=Array.Empty<System.Collections.Generic.List<int>>();
        void PrepareLocalExchangeNeighbors(PrehistoryState s)
        {
            var towns=s.Society.Settlements;if(ReferenceEquals(s,localExchangeState)&&ReferenceEquals(towns,localExchangeTowns))return;
            int retained=localExchangeCells.Length;
            if(towns.Length<retained||Enumerable.Range(0,Math.Min(towns.Length,retained)).Any(i=>towns[i].Cell!=localExchangeCells[i]))
            {retained=0;localExchangeCells=Array.Empty<int>();localExchangeNeighbors=Array.Empty<System.Collections.Generic.List<int>>();}
            if(towns.Length>retained)
            {
                Array.Resize(ref localExchangeCells,towns.Length);Array.Resize(ref localExchangeNeighbors,towns.Length);
                var locator=new CellLocator(towns.Select(t=>World.Surface.Cells[t.Cell]).ToArray());double chord=2*D.Sin(130.0/(2*World.Recipe.RadiusKm));
                for(int i=retained;i<towns.Length;i++)
                {
                    localExchangeCells[i]=towns[i].Cell;localExchangeNeighbors[i]=new System.Collections.Generic.List<int>();
                    foreach(int j in locator.Within(World.Surface.Cells[towns[i].Cell].Position,chord*chord).Where(j=>j<i).OrderBy(j=>j))if(D.Angle(World.Surface.Cells[towns[i].Cell].Position,World.Surface.Cells[towns[j].Cell].Position)*World.Recipe.RadiusKm<=120)
                    {localExchangeNeighbors[i].Add(j);localExchangeNeighbors[j].Add(i);}
                }
            }
            localExchangeState=s;localExchangeTowns=towns;
        }
        // Explicit opt-in for old saves; loading/replaying their version never
        // silently reinterprets history. Past events and founding dates survive.
        public PrehistoryState UpgradeHistoricalModel(PrehistoryState source)
        {
            Validate(source);if(LocalHistory(source))return source;
            if(source.History?.Version!="history-9")throw new InvalidOperationException("Start a new simulation to use the current rules with this older history.");
            var s=source.Copy();s.History.Version="history-10";Array.Clear(s.Society.Knowledge,0,s.Society.Knowledge.Length);s.History.Administration=new HistoricalAdministration();s.Seal();return UpgradeScheduling(s);
        }
        public PrehistoryState UpgradeToDelegatedHistory(PrehistoryState source)
        {
            Validate(source);if(DelegatedHistory(source))return source;
            if(source.History?.Version!="history-10")throw new InvalidOperationException("Update this history to the local formation model first.");
            var s=source.Copy();s.History.Version="history-11";s.Seal();return UpgradeScheduling(s);
        }
        public PrehistoryState UpgradeToCurrentHistory(PrehistoryState source)
        {
            Validate(source);if(source.History?.Version==CurrentHistoryVersion)return source;
            if(ModernHistory(source))return UpgradeToCoastalHistory(source);
            var delegated=DelegatedHistory(source)?source:UpgradeToDelegatedHistory(source.History?.Version=="history-9"?UpgradeHistoricalModel(source):source);
            return UpgradeToCurrentHistory(UpgradeToModernHistory(UpgradeToInstitutionalHistory(delegated)));
        }
    }
}

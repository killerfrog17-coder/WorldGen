using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed class HistoricalSitePotential
    {
        public int Cell,Patch,Knowledge,NativeCropMask;
        public long RuralPopulation,PatchPopulation,ForagerCapacity;
        public double Density,StorageOpportunity,AgriculturalYield,RiverFraction;
    }
    public sealed partial class Prehistory
    {
        static bool RevisedHistory(PrehistoryState s)=>s.History?.Version=="history-2"||EraHistory(s);
        static bool EraHistory(PrehistoryState s)=>s.History?.Version=="history-3"||FrontierHistory(s);
        static bool FrontierHistory(PrehistoryState s)=>s.History?.Version=="history-4"||CivicHistory(s);
        static bool CivicHistory(PrehistoryState s)=>s.History?.Version=="history-5"||ExperimentalHistory(s);
        static bool ExperimentalHistory(PrehistoryState s)=>s.History?.Version=="history-6"||TerritorialHistory(s);
        static bool TerritorialHistory(PrehistoryState s)=>s.History?.Version=="history-7"||DiffusionHistory(s);
        static bool DiffusionHistory(PrehistoryState s)=>s.History?.Version=="history-8"||RegionalHistory(s);
        static bool RegionalHistory(PrehistoryState s)=>s.History?.Version=="history-9"||LocalHistory(s);
        static bool UsesGlacialDispersal(PrehistoryState s)=>LocalHistory(s)?s.History.Polities.Length==0:s.Society==null||s.Society.Phase<SocietyStage.EmbryonicCivilization&&(!RevisedHistory(s)||s.History.Polities.Length==0);
        static int AutomaticYears(PrehistoryState s)=>RevisedHistory(s)&&s.History.Polities.Length>0?1:SocietyState.DefaultStep(s.Society.Phase);
        double[] agriculturalYield,storedYield,settlementOpportunity,riverAccess,riverSites,inhabitableArea;
        void PrepareHistoricalEnvironment()
        {
            if(agriculturalYield!=null)return;
            int n=World.Surface.Cells.Length;agriculturalYield=new double[n];storedYield=new double[n];settlementOpportunity=new double[n];riverAccess=new double[n];riverSites=new double[n];inhabitableArea=new double[n];
            foreach(var r in World.Hydrology.Rivers)if(r.From>=0&&r.From<n)
            {
                double widthKm=.12*r.Order*r.Order,cellWidthKm=Math.Sqrt(World.Surface.Cells[r.From].AreaM2/1e6);
                riverAccess[r.From]=Math.Max(riverAccess[r.From],Math.Min(.18,widthKm/Math.Max(1,cellWidthKm)));
                riverSites[r.From]=Math.Max(riverSites[r.From],D.Clamp(Math.Log(1+r.DischargePotentialM3PerS)/Math.Log(1001)));
            }
            for(int i=0;i<n;i++)
            {
                var a=World.Ecology.Habitat[i];if(World.Hydrology.OceanConnected[i])continue;
                inhabitableArea[i]=World.Surface.Cells[i].AreaM2/1e6*(1-World.Hydrology.LakeCoverageFraction[i]);
                bool fresh=a.Flooding==FloodRegime.RiverSeasonal||a.Flooding==FloodRegime.RiverPersistent;
                double flood=fresh?Math.Max(riverAccess[i],a.FloodFractionPermille/1000.0):riverAccess[i];riverAccess[i]=D.Q(Math.Min(.65,flood),1000000);
                double heat=D.Clamp((a.WarmTenthsC-80)/120.0)*D.Clamp(a.GrowingDays/180.0)*(1-a.PermafrostPermille/1000.0);
                double slope=D.Clamp(1-a.ReliefM/1800.0),salt=D.Clamp(1-a.SalinityMilliPpt/3000.0);
                double moisture=D.Clamp((a.AridityMilli-100)/550.0),soil=D.Clamp(a.SoilFertilityPermille/700.0);
                // Seasonal seeds, roots/tree crops and concentrated aquatic food are distinct opportunities.
                // These are deliberately broad crop-system proxies, not claims about a species' domestication.
                double season=D.Clamp(Math.Max(a.WarmSeasonDryPermille/700.0,(a.WarmTenthsC-a.ColdTenthsC)/220.0));
                double grain=.30+.70*season;
                double clearing=a.Biome==Biome.TropicalRainforest?.48:a.Habitat.HasFlag(Habitat.Forest)?.75:1;
                double rainfed=heat*slope*salt*moisture*soil*clearing;
                double irrigable=heat*slope*salt*.9;
                agriculturalYield[i]=D.Q(rainfed*(1-flood)+irrigable*flood,1000000);
                storedYield[i]=D.Q(rainfed*grain*(1-flood)+irrigable*flood*.9,1000000);
                bool coast=World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
                double aquatic=(coast?.32:0)+Math.Min(.35,flood*2);
                // A useful river site need not imply that its entire (potentially desert) cell is irrigated.
                riverSites[i]=D.Q(riverSites[i]*heat*slope*salt,1000000);
                settlementOpportunity[i]=D.Q(Math.Max(storedYield[i],Math.Max(aquatic,.8*riverSites[i])),1000000);
            }
            if(World.Subcells!=null)foreach(var group in World.Subcells.Cells)
            {
                int cell=group.Cell;if(!World.Hydrology.OceanConnected[cell])continue;var a=World.Ecology.Habitat[cell];
                // Only the largest island supports this demographic node; never count the ocean cell as land.
                inhabitableArea[cell]=group.LargestIslandAreaM2/1e6;
                double warmth=D.Clamp((a.WarmTenthsC-80)/120.0)*D.Clamp(a.GrowingDays/180.0),water=D.Clamp((a.AridityMilli-100)/600.0);
                agriculturalYield[cell]=D.Q(.55*warmth*water,1000000);storedYield[cell]=D.Q(agriculturalYield[cell]*.65,1000000);
                settlementOpportunity[cell]=D.Q(Math.Max(storedYield[cell],.35*warmth*water),1000000);
            }
        }
        double HistoricalLandArea(int cell){PrepareHistoricalEnvironment();return inhabitableArea[cell];}
        public double HistoricalAgriculturalYield(int cell){PrepareHistoricalEnvironment();return agriculturalYield[cell];}
        public double HistoricalStorageOpportunity(int cell){PrepareHistoricalEnvironment();return settlementOpportunity[cell];}
        double HistoricalStoredYield(int cell){PrepareHistoricalEnvironment();return storedYield[cell];}
        double HistoricalRiverAccess(int cell){PrepareHistoricalEnvironment();return riverAccess[cell];}
        double CultureOpportunity(PrehistoryState s,int cell){if(RealismHistory(s)){PrepareLandscape();return Math.Max(landscapeOpportunity[cell],WildCropStorage(cell)*LandscapeCropFactor(cell));}return EraHistory(s)?Math.Max(HistoricalStorageOpportunity(cell),WildCropStorage(cell)):RevisedHistory(s)?HistoricalStorageOpportunity(cell):StorageOpportunity(cell);}
        double CultureAgriculture(PrehistoryState s,int cell)=>RealismHistory(s)?AgriculturalSiteYield(s,cell):RevisedHistory(s)?Math.Max(HistoricalAgriculturalYield(cell),riverSites[cell]*.85):FarmingSuitability(cell);
        double CultureStoredOpportunity(PrehistoryState s,int cell)=>RealismHistory(s)?StoredYield(s,cell):RevisedHistory(s)?Math.Max(HistoricalStoredYield(cell),riverSites[cell]*.8):FarmingSuitability(cell);
        double HistoricalSiteDensity(PrehistoryState s,int cell)
        {
            if(!RevisedHistory(s))return LocalDensity(s,cell);
            PrepareHistoricalEnvironment();double inhabited=riverSites[cell]>.2?Math.Max(.08,agriculturalYield[cell]+riverAccess[cell]):1;
            return RuralCellPopulation(s,cell)/Math.Max(1,HistoricalLandArea(cell))/Math.Min(1,inhabited);
        }
        // Read-only diagnostic of the same quantities used by regional emergence.
        // No geography-specific scoring or alternative generation route.
        public HistoricalSitePotential AssessHistoricalSite(PrehistoryState s,int cell)
        {
            if(cell<0||cell>=CellPatch.Length)throw new ArgumentOutOfRangeException(nameof(cell));
            int patch=CellPatch[cell];var result=new HistoricalSitePotential{Cell=cell,Patch=patch};if(patch<0)return result;
            result.RuralPopulation=RuralCellPopulation(s,cell);result.PatchPopulation=s.Population[patch];result.ForagerCapacity=Patches[patch].Capacity;
            result.Knowledge=s.Society?.Knowledge[patch]??0;result.Density=HistoricalSiteDensity(s,cell);result.StorageOpportunity=CultureOpportunity(s,cell);
            result.AgriculturalYield=CultureAgriculture(s,cell);result.RiverFraction=HistoricalRiverAccess(cell);result.NativeCropMask=EraHistory(s)?NativeCropMask(cell):0;return result;
        }
        double CropCompatibility(int a,int b)
        {
            var x=World.Ecology.Habitat[a];var y=World.Ecology.Habitat[b];
            return D.Q(Math.Exp(-Math.Abs(x.ColdTenthsC-y.ColdTenthsC)/250.0-Math.Abs(x.AridityMilli-y.AridityMilli)/1200.0),1000000);
        }
        void HistoricalMaritimeCultures(PrehistoryState s,SortedDictionary<int,int> claims,SortedSet<(int,int)> contacts,CancellationToken token,double scale=1)
        {
            // Occupied coastal endpoints: seafaring does not project identity directly from an inland capital.
            var h=s.Society;
            foreach(var route in Routes)
            {
                token.ThrowIfCancellationRequested();if(!route.Sea||route.Shallow&&!EraHistory(s))continue;
                for(int direction=0;direction<2;direction++)
                {
                    int a=direction==0?route.A:route.B,b=direction==0?route.B:route.A;
                    int from=direction==0?route.CellA:route.CellB,to=direction==0?route.CellB:route.CellA;
                    if(from<0||to<0)continue;int owner=h.CellCulture[from];if(owner<0||s.Population[a]<200||s.Population[b]<100)continue;
                    var hc=s.History.Cultures[owner];if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing))continue;
                    bool navigation=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation);if(route.DistanceM>(navigation?700000:220000))continue;
                    int other=h.CellCulture[to];if(other>=0&&other!=owner){contacts.Add((Math.Min(owner,other),Math.Max(owner,other)));continue;}
                    int cell=FindCoastalCultureSite(s,b,to,false);
                    if(cell<0||HistoricalStorageOpportunity(cell)<.2)continue;
                    double chance=(navigation?.10:.04)*Math.Exp(-route.DistanceM/(navigation?400000.0:120000.0))*HabitatSimilarity(a,b);
                    if(HistoryRoll("maritime-network",unchecked(a*32771+b),s.Years)<ElapsedChance(chance,scale)&&!claims.ContainsKey(cell))
                    {claims[cell]=owner;HistoryEvent(s,"sea-contact",h.Cultures[owner].Name+" establishes an overseas coastal community.",cell,owner,category:ChronicleCategory.Politics);}
                }
            }
        }
        string HistoricalName(string stream,int id)
        {
            string[] onset={"Ar","Bel","Dar","En","Ish","Ker","Lum","Mar","Nar","Or","Sel","Tav","Un","Val","Kes","Ser","Tal","Mor","Zan","Eth"};
            string[] middle={"a","e","i","o","u","ai","an","en","el","ir","or","av"};
            string[] end={"ra","is","an","eth","or","um","esh","en","ia","os","ara","in","ath","el","on","ur"};
            return onset[(int)(HistoryRoll(stream+"-a",id,0)*onset.Length)]+middle[(int)(HistoryRoll(stream+"-b",id,0)*middle.Length)]+end[(int)(HistoryRoll(stream+"-c",id,0)*end.Length)];
        }
        void HistoricalCasualties(PrehistoryState s,HistoricalPolity polity,int losses)
        {
            if(CivicHistory(s)){CivicCasualties(s,polity,losses);return;}
            // Levies came from all the state's towns, not solely from its capital.
            var towns=s.Society.Settlements.Where(t=>t.PolityId==polity.Id&&t.Population>0).OrderBy(t=>t.Id).ToArray();
            long remainingPopulation=towns.Sum(t=>t.Population),remaining=Math.Min(losses,remainingPopulation);
            foreach(var t in towns){long population=t.Population,taken=remainingPopulation==0?0:remaining*population/remainingPopulation;t.Population-=taken;remaining-=taken;remainingPopulation-=population;}
        }
    }
}

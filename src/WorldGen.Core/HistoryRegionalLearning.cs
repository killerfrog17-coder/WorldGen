using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed class HistoricalFoundationPotential
    {
        public int Town,Cell,Preparation,InstitutionalExamples;
        public double StoredSupport,FiscalPotential,AdministrativeOpportunity,Readiness;
        public bool Supported;
    }
    [Serializable] public sealed class HistoricalRegionalLearning
    {
        public int[] FarmingSource=Array.Empty<int>(),FarmingExposure=Array.Empty<int>();
        public HistoricalRegionalLearning Copy()=>new HistoricalRegionalLearning{FarmingSource=(int[])FarmingSource.Clone(),FarmingExposure=(int[])FarmingExposure.Clone()};
    }
    public static class HistoricalRegionalEffects
    {
        // Central administration is one contingent outcome of a supported town,
        // not the inevitable next label for every prosperous local community.
        public static double AdministrativeOpportunity(double fiscalPotential,double concentration,double circumscription,double coordination,int examples)
        {
            double local=.025+.55*Math.Pow(D.Clamp(fiscalPotential),2)*(.20+.45*D.Clamp(coordination)+.35*D.Clamp(circumscription))*(.45+.55*D.Clamp(concentration));
            return D.Q(D.Clamp(local*(1+.75*D.Clamp(examples/1000.0))),1000000000);
        }
        // Knowledge can cross a forager relay. Adoption is a separate local event.
        // Patch radii charge within-region travel rather than teleporting across it.
        public static int RelayedExposure(int exposure,int distanceM,double radiusA,double radiusB,double reliefM)
        {
            double metres=(Math.Max(0,distanceM)+500*(Math.Max(0,radiusA)+Math.Max(0,radiusB)))*(1+Math.Max(0,reliefM)/1800.0);
            return Math.Max(0,exposure-60-(int)Math.Min(1000,Math.Round(metres/5000)));
        }
        public static double AdoptionChance(int exposure,long residents,double crop,double density)
            =>residents<300||exposure<450||crop<.3?0:D.Q(.06*D.Clamp(exposure/800.0)*D.Clamp(crop)*D.Clamp(density/.025),1000000000);
        public static int PackageMerit(int exposure,double compatibility)
            =>(int)Math.Round(Math.Max(0,exposure)*(.35+.65*D.Clamp(compatibility))*1000);
        public static bool CanStudyRegionalCrop(int exposure,int year,int adoptedYear,double suitability)
            =>exposure>=450&&adoptedYear>0&&adoptedYear<=year&&suitability>=.3;
    }
    public sealed partial class Prehistory
    {
        void AdvanceRegionalLearning(PrehistoryState s,CancellationToken token,double scale=1)
        {
            if(ModernHistory(s)){AdvanceLocalCropLearning(s,token);return;}
            var h=s.History;var society=s.Society;int count=Patches.Length;
            if(h.RegionalLearning==null)h.RegionalLearning=new HistoricalRegionalLearning{FarmingSource=Enumerable.Repeat(-1,count).ToArray(),FarmingExposure=new int[count]};
            var learning=h.RegionalLearning;var source=(int[])learning.FarmingSource.Clone();var exposure=(int[])learning.FarmingExposure.Clone();
            var compatibility=new System.Collections.Generic.Dictionary<(int,int),double>();
            double Fit(int culture,int patch)
            {
                if(culture<0)return 0;var key=(culture,patch);if(compatibility.TryGetValue(key,out double known))return known;
                PreparePatchCropPotential();double best=0;var crops=h.Agriculture[culture];
                for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0&&crops.AdoptedYear[k]<=s.Years)best=Math.Max(best,ModernHistory(s)?CropTransferFit(s,culture,Patches[patch].Cell,k):patchCropPotential[patch*6+k]/1000000.0);
                compatibility.Add(key,best);return best;
            }
            // One snapshot supplies this entire pass; fresh recipients cannot relay again.
            // Information decays gradually without maintained contacts, not overnight.
            for(int p=0;p<count;p++){learning.FarmingExposure[p]=Math.Max(0,exposure[p]-(int)Math.Round(scale));if(learning.FarmingExposure[p]==0)learning.FarmingSource[p]=-1;}
            void Offer(int patch,int candidate,int strength)
            {
                if(candidate<0||strength<=0)return;
                int old=learning.FarmingExposure[patch],prior=learning.FarmingSource[patch];
                int merit=HistoricalRegionalEffects.PackageMerit(strength,Fit(candidate,patch)),previous=HistoricalRegionalEffects.PackageMerit(old,Fit(prior,patch));
                if(merit>previous||merit==previous&&(strength>old||strength==old&&(prior<0||candidate<prior))){learning.FarmingSource[patch]=candidate;learning.FarmingExposure[patch]=strength;}
            }
            if(society.CellCulture.Length>0)for(int cell=0;cell<society.CellCulture.Length;cell++)
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=society.CellCulture[cell],patch=CellPatch[cell];
                if(owner<0||patch<0||s.Population[patch]<100)continue;var culture=society.Cultures[owner];
                if(culture.Farming<150||culture.Population<100||!h.Agriculture[owner].AdoptedYear.Any(y=>y>0&&y<=s.Years))continue;
                int strength=Math.Min(1000,650+culture.Farming/2);Offer(patch,owner,strength);
                // Source farmers existed before this pass; they may teach a first neighbor now.
                int merit=HistoricalRegionalEffects.PackageMerit(strength,Fit(owner,patch)),previous=HistoricalRegionalEffects.PackageMerit(exposure[patch],Fit(source[patch],patch));
                if(merit>previous||merit==previous&&(strength>exposure[patch]||strength==exposure[patch]&&(source[patch]<0||owner<source[patch]))){source[patch]=owner;exposure[patch]=strength;}
            }
            foreach(var route in Routes)
            {
                token.ThrowIfCancellationRequested();if(s.Population[route.A]<100||s.Population[route.B]<100)continue;
                for(int direction=0;direction<2;direction++)
                {
                    int from=direction==0?route.A:route.B,to=direction==0?route.B:route.A,origin=source[from];if(origin<0||exposure[from]<=60)continue;
                    var hc=h.Cultures[origin];
                    if(route.Sea)
                    {
                        bool sailing=HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing),navigation=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation);
                        if(!sailing||route.Shallow&&!route.Coastal||route.DistanceM>(navigation?700000:220000))continue;
                        // Food/practice shipping uses actual coast links, not arbitrary deep-ocean adjacency.
                    }
                    double relief=route.Sea?0:(World.Ecology.Habitat[Patches[from].Cell].ReliefM+World.Ecology.Habitat[Patches[to].Cell].ReliefM)*.5;
                    int incoming=HistoricalRegionalEffects.RelayedExposure(exposure[from],route.DistanceM,Patches[from].RadiusKm,Patches[to].RadiusKm,relief);
                    if(ModernHistory(s)&&!route.Sea)
                    {
                        var a=World.Ecology.Habitat[Patches[from].Cell];var b=World.Ecology.Habitat[Patches[to].Cell];
                        double contrast=Math.Abs(a.GrowingDays-b.GrowingDays)/180.0+Math.Abs(a.AridityMilli-b.AridityMilli)/1200.0;
                        bool dry=(a.AridityMilli<200&&HistoricalRiverAccess(Patches[from].Cell)<.01)||(b.AridityMilli<200&&HistoricalRiverAccess(Patches[to].Cell)<.01);
                        incoming=(int)Math.Round(incoming*Math.Exp(-.45*contrast)*(dry?.45:1));
                    }
                    Offer(to,origin,incoming);
                }
            }
        }
        int[] patchCropPotential;
        void PreparePatchCropPotential()
        {
            if(patchCropPotential!=null)return;PrepareWildCrops();var values=new int[Patches.Length*6];
            for(int p=0;p<Patches.Length;p++)foreach(int cell in Patches[p].Cells)if(HistoricalLandArea(cell)>0)for(int k=0;k<6;k++)values[p*6+k]=Math.Max(values[p*6+k],cropSuitability[cell*6+k]);
            patchCropPotential=values;
        }
        void AdoptRegionalCrops(PrehistoryState s,CultureIdentity child,int source)
        {
            if(LocalHistory(s)&&!HistoricalLocalEffects.AgricultureOpen(s.Years))return;
            var parent=s.History.Agriculture[source];var crops=s.History.Agriculture[child.Id];var hc=s.History.Cultures[child.Id];
            for(int k=0;k<6;k++)if(HistoricalMaritimeKnowledge.CanCarryCrop(s.Years,parent.AdoptedYear[k],ModernHistory(s)?CropTransferFit(s,source,child.OriginCell,k):CropSuitability(child.OriginCell,k)))
            {
                if(ModernHistory(s)&&LocalCultivarFit(s,source,child.OriginCell,k)<.25)continue;
                crops.AdoptedYear[k]=s.Years+1;crops.SourceCulture[k]=source;crops.Practice[k]=600;
                if(ModernHistory(s))crops.Cultivars[k]=MakeCultivar(s,child.Id,k,source);
            }
            if(crops.AdoptedYear.Any(y=>y>0)){child.Farming=50;hc.Technology[(int)HistoricalTechnology.Cultivation]=s.Years+1;}
            if(ModernHistory(s))InvalidateCultivarFits(child.Id);
            if(s.History.Cultures[source].Technology[(int)HistoricalTechnology.Pottery]>0&&s.History.Cultures[source].Technology[(int)HistoricalTechnology.Pottery]<=s.Years)hc.Technology[(int)HistoricalTechnology.Pottery]=s.Years+1;
            // Learning a food system does not imply ancestry, shared language,
            // a direct diplomatic tie to its remote inventor, or political ownership.
        }
        void RegionalFarmingCommunities(PrehistoryState s,CancellationToken token,double scale=1)
        {
            if(ModernHistory(s)){LocalFarmingCommunities(s,token,scale);return;}
            if(LocalHistory(s)&&!HistoricalLocalEffects.AgricultureOpen(s.Years))return;
            var learning=s.History.RegionalLearning;if(learning==null)return;var society=s.Society;
            var established=new bool[Patches.Length];foreach(var culture in society.Cultures)established[CellPatch[culture.OriginCell]]=true;
            for(int p=0;p<Patches.Length;p++)
            {
                token.ThrowIfCancellationRequested();int source=learning.FarmingSource[p];
                if(source<0||learning.FarmingExposure[p]<450||established[p]||s.Population[p]<300||!LocalHistory(s)&&society.Knowledge[p]<3500)continue;
                var crops=s.History.Agriculture[source];int best=-1;double cropScore=0,bestScore=0;
                foreach(int cell in Patches[p].Cells)
                {
                    if(society.CellCulture[cell]>=0||World.Hydrology.LakeCoverageFraction[cell]>=.5||CultureOpportunity(s,cell)<.25)continue;
                    double suitability=0;for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0&&crops.AdoptedYear[k]<=s.Years)suitability=Math.Max(suitability,ModernHistory(s)?CropTransferFit(s,source,cell,k):CropSuitability(cell,k));
                    double score=suitability*D.Clamp(HistoricalSiteDensity(s,cell)/.025);
                    if(suitability>=.3&&(score>bestScore||score==bestScore&&(best<0||cell<best))){best=cell;bestScore=score;cropScore=suitability;}
                }
                if(best<0||HistoryRoll("regional-farming-9",p,s.Years)>=ElapsedChance(HistoricalRegionalEffects.AdoptionChance(learning.FarmingExposure[p],s.Population[p],cropScore,HistoricalSiteDensity(s,best)),scale))continue;
                var child=FoundCulture(s,p,best);InitializeHistoricalCulture(s,child);AdoptRegionalCrops(s,child,source);
                HistoryEvent(s,"regional-farming-community",child.Name+" establishes a local farming tradition through regional exchange of food plants and methods, ultimately recorded from "+society.Cultures[source].Name+". The community retains its own identity and institutions.",best,child.Id,category:ChronicleCategory.Society);
            }
        }
        double UncachedLocalFiscalPotential(PrehistoryState s,int culture,int cell)
        {
            var c=s.Society.Cultures[culture];var crops=s.History.Agriculture[culture];double best=0;
            // Observability/collectibility are crop-system proxies, not a claim
            // that roots, aquatic food or unwritten societies cannot support states.
            int[] legibility={900,850,800,220,450,700};
            if(LocalHistory(s))legibility=new[]{900,850,850,420,550,750};
            for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0)
            {
                double adaptation=Math.Min(1.2,CropSuitability(s,cell,k)/Math.Max(.01,HistoricalCrops.AbioticPotential(CropHabitat(s,cell),riverSites[cell])));
                if(ModernHistory(s))adaptation*=LocalCultivarFit(s,culture,cell,k)*crops.Cultivars[k].YieldPermille/1000.0*CropSoilPenalty(s,culture,cell,k);
                best=Math.Max(best,adaptation*legibility[k]/1000.0);
            }
            return D.Q(D.Clamp(best*c.Farming/850.0+CultureOpportunity(s,cell)*c.Fishing/1100.0),1000000);
        }
        double LocalAdministrativeOpportunity(PrehistoryState s,Settlement town)
        {
            var context=CivicContext(s,town.Cell);
            return HistoricalRegionalEffects.AdministrativeOpportunity(LocalFiscalPotential(s,town.CultureId,town.Cell),town.Population/6000.0,context.Circumscription,context.Coordination,s.History.Towns[town.Id].CivicExampleExposure);
        }
        public HistoricalFoundationPotential AssessHistoricalFoundation(PrehistoryState s,int townId)
        {
            if(s?.History==null||townId<0||townId>=s.Society.Settlements.Length)throw new ArgumentOutOfRangeException(nameof(townId));
            var town=s.Society.Settlements[townId];var hc=s.History.Cultures[town.CultureId];var record=s.History.Towns[townId];
            double stored=LocalStoredSupport(s,town.CultureId,town.Cell);
            return new HistoricalFoundationPotential{Town=townId,Cell=town.Cell,Preparation=record.CivicPreparationYears,InstitutionalExamples=record.CivicExampleExposure,StoredSupport=stored,FiscalPotential=RegionalHistory(s)?LocalFiscalPotential(s,town.CultureId,town.Cell):stored,AdministrativeOpportunity=RegionalHistory(s)?LocalAdministrativeOpportunity(s,town):1,Readiness=StateFoundationReadiness(s,town),Supported=HistoricalFormationEffects.Preparation(0,town.Population,town.FoodCapacity,town.FoodReserve,town.Stability,hc.Institution,stored)>0};
        }
        void RefreshInstitutionalExamples(PrehistoryState s,System.Collections.Generic.SortedSet<(int,int)> contacts)
        {
            var h=s.History;var established=new bool[h.Cultures.Length];
            foreach(var p in h.Polities)if(p.EndedYear<0)established[p.CultureId]=true;
            var examples=new int[h.Cultures.Length];
            foreach(var pair in contacts){if(established[pair.Item1])examples[pair.Item2]=Math.Min(1000,examples[pair.Item2]+350);if(established[pair.Item2])examples[pair.Item1]=Math.Min(1000,examples[pair.Item1]+350);}
            for(int i=0;i<examples.Length;i++)h.Cultures[i].InstitutionalExamples=established[i]?1000:examples[i];
        }
    }
}

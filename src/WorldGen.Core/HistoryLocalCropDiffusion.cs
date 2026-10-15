using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed class HistoricalCropTransmission
    {
        public int Cell,SourceCulture=-1,TravelEffortM,Exposure;
    }
    public static class HistoricalCropDiffusionEffects
    {
        public static double EnvironmentalCost(HabitatCell from,HabitatCell to,bool adaptedToDry)
        {
            double contrast=Math.Abs(from.GrowingDays-to.GrowingDays)/180.0+Math.Abs(from.AridityMilli-to.AridityMilli)/1200.0;
            bool dry=(from.AridityMilli<200&&from.Flooding==FloodRegime.None)||(to.AridityMilli<200&&to.Flooding==FloodRegime.None);
            double cover=to.Biome==Biome.TropicalRainforest?1.6:to.Habitat.HasFlag(Habitat.Forest)?1.15:1;
            return D.Q((1+.5*contrast)*cover*(dry?(adaptedToDry?1.35:3):1),1000000);
        }
        public static int Exposure(int farming,int effort,bool roads)
            =>Math.Max(0,Math.Min(1000,650+farming/2)-(int)Math.Round(effort/(roads?750.0:500.0)));
        public static double AdoptionChance(double cropFit,double density,int exposure)
            =>exposure<450||cropFit<.3?0:D.Q(.12*D.Clamp(cropFit)*D.Clamp(density/.025)*D.Clamp(exposure/850.0),1000000000);
    }
    public sealed partial class Prehistory
    {
        int[] localCropSource,localCropEffort,localCropFarming;PrehistoryState localCropState;string localCropHash;int localCropYear;
        void BuildLocalCropDiffusion(PrehistoryState s,CancellationToken token,bool force=false)
        {
            if(!force&&ReferenceEquals(localCropState,s)&&localCropHash==s.Hash&&localCropYear==s.Years)return;
            localCropState=null;var h=s.History;int n=World.Surface.Cells.Length;
            if(!h.Agriculture.Any(a=>a.AdoptedYear.Any(y=>y>0&&y<=s.Years))) {localCropSource=null;localCropEffort=null;localCropState=s;localCropHash=s.Hash;localCropYear=s.Years;return;}
            PrepareHistoricalEnvironment();PrepareAdministrativeSeaRoutes();
            if(localCropSource==null||localCropSource.Length!=n){localCropSource=new int[n];localCropEffort=new int[n];}
            Array.Fill(localCropSource,-1);Array.Fill(localCropEffort,int.MaxValue);
            localCropFarming=s.Society.Cultures.Select(c=>c.Farming).ToArray();var heap=new Heap();
            double BestFit(int source,int cell)
            {double best=0;for(int k=0;k<6;k++)if(h.Agriculture[source].AdoptedYear[k]>0&&h.Agriculture[source].AdoptedYear[k]<=s.Years)best=Math.Max(best,CropTransferFit(s,source,cell,k));return best;}
            var populated=new HashSet<int>(s.Society.Settlements.Where(t=>t.Population>=100).Select(t=>t.Cell));
            bool Inhabited(int cell)=>CellPatch[cell]>=0&&World.Hydrology.LakeCoverageFraction[cell]<.9&&(populated.Contains(cell)||RuralCellPopulation(s,cell)>=10);
            void Offer(int cell,int source,long effort)
            {
                if(!Inhabited(cell))return;var culture=h.Cultures[source];int limit=HistoricalTechnologies.Has(culture,HistoricalTechnology.Roads)?450000:250000;
                if(effort>limit||effort>localCropEffort[cell]||effort==localCropEffort[cell]&&localCropSource[cell]>=0&&source>=localCropSource[cell])return;
                localCropEffort[cell]=(int)effort;localCropSource[cell]=source;heap.Push(effort,cell);
            }
            foreach(int cell in CulturalCells(s))
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int source=s.Society.CellCulture[cell];if(source<0||localCropFarming[source]<150||!Inhabited(cell))continue;
                if(BestFit(source,cell)>=(EmergenceHistory(s)?.35:.08))Offer(cell,source,0);
            }
            var ships=new Dictionary<int,List<(int Cell,int Distance)>>();
            foreach(var link in h.Modern.Shipping)
            {
                var a=s.Society.Settlements[link.PortA];var b=s.Society.Settlements[link.PortB];if(a.Population<100||b.Population<100)continue;
                void Add(int from,int to){if(!ships.TryGetValue(from,out var list)){list=new List<(int,int)>();ships.Add(from,list);}list.Add((to,link.DistanceM));}
                Add(a.Cell,b.Cell);Add(b.Cell,a.Cell);
            }
            while(heap.Count>0)
            {
                token.ThrowIfCancellationRequested();var item=heap.Pop();if(localCropEffort[item.Id]!=item.Key)continue;int source=localCropSource[item.Id];var hc=h.Cultures[source];var cell=World.Surface.Cells[item.Id];
                bool adapted=World.Ecology.Habitat[s.Society.Cultures[source].OriginCell].AridityMilli<400;
                for(int k=0;k<cell.Neighbors.Length;k++)
                {
                    int next=cell.Neighbors[k];if(World.Hydrology.OceanConnected[next]||!Inhabited(next))continue;
                    // Account for within-cell access at coarse detail, then
                    // charge real terrain/environmental travel between fields.
                    double radii=(Math.Sqrt(cell.AreaM2/Math.PI)+Math.Sqrt(World.Surface.Cells[next].AreaM2/Math.PI))*.5;
                    int metres=(int)Math.Round(Math.Max(10000,cell.DistanceM[k]-radii));
                    double river=(World.Hydrology.Receiver[item.Id]==next||World.Hydrology.Receiver[next]==item.Id)&&HistoricalRiverAccess(item.Id)>.01&&HistoricalRiverAccess(next)>.01?.6:1;
                    double fit=BestFit(source,next),cost=TravelCost(item.Id,next,metres,hc)*river*HistoricalCropDiffusionEffects.EnvironmentalCost(World.Ecology.Habitat[item.Id],World.Ecology.Habitat[next],adapted)/(.25+.75*D.Clamp(fit));
                    if(ModernHistory(s))cost/=HistoricalModernEffects.LandTransport(hc,s.Years);
                    Offer(next,source,item.Key+(long)Math.Round(cost));
                }
                if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)&&administrativeSeaRoutes.TryGetValue(item.Id,out var routes))foreach(var route in routes)
                {
                    bool navigation=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation);if(route.DistanceM>(navigation?700000:220000))continue;
                    Offer(route.CellA==item.Id?route.CellB:route.CellA,source,item.Key+(long)Math.Round(route.DistanceM*(navigation?.25:.5))+15000);
                }
                if(HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation)&&ships.TryGetValue(item.Id,out var shipping))foreach(var route in shipping)Offer(route.Cell,source,item.Key+(long)Math.Round(route.Distance*.02)+25000);
            }
            localCropState=s;localCropHash=s.Hash;localCropYear=s.Years;
        }
        int LocalCropExposure(PrehistoryState s,int cell)
        {
            int source=localCropSource==null?-1:localCropSource[cell];return source<0?0:HistoricalCropDiffusionEffects.Exposure(localCropFarming[source],localCropEffort[cell],HistoricalTechnologies.Has(s.History.Cultures[source],HistoricalTechnology.Roads));
        }
        void AdvanceLocalCropLearning(PrehistoryState s,CancellationToken token)
        {
            using(ProfileWork("local crop diffusion",World.Surface.Cells.Length))BuildLocalCropDiffusion(s,token,true);var h=s.History;int count=Patches.Length;
            if(h.RegionalLearning==null)h.RegionalLearning=new HistoricalRegionalLearning{FarmingSource=Enumerable.Repeat(-1,count).ToArray(),FarmingExposure=new int[count]};
            var learning=h.RegionalLearning;Array.Fill(learning.FarmingSource,-1);Array.Clear(learning.FarmingExposure,0,count);
            if(localCropSource==null)return;
            // Patch fields remain inspectable summaries, not permission to
            // instantly teach every cell of a very large ecological patch.
            for(int cell=0;cell<localCropSource.Length;cell++)
            {if((cell&2047)==0)token.ThrowIfCancellationRequested();int source=localCropSource[cell],patch=CellPatch[cell];if(source<0||patch<0)continue;int exposure=LocalCropExposure(s,cell);if(exposure>learning.FarmingExposure[patch]||exposure==learning.FarmingExposure[patch]&&source<learning.FarmingSource[patch]){learning.FarmingSource[patch]=source;learning.FarmingExposure[patch]=exposure;}}
        }
        void LocalFarmingCommunities(PrehistoryState s,CancellationToken token,double scale)
        {
            if(!HistoricalLocalEffects.AgricultureOpen(s.Years)||localCropSource==null)return;
            var society=s.Society;var cells=Enumerable.Repeat(-1,Patches.Length).ToArray();var sources=Enumerable.Repeat(-1,Patches.Length).ToArray();var scores=new double[Patches.Length];
            var homes=new CellLocator(society.Cultures.Select(c=>World.Surface.Cells[c.OriginCell]).ToArray());double chord=2*D.Sin(225.0/(2*World.Recipe.RadiusKm));
            for(int cell=0;cell<localCropSource.Length;cell++)
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int source=localCropSource[cell],patch=CellPatch[cell];
                if(source<0||patch<0||society.CellCulture[cell]>=0||s.Population[patch]<300||World.Hydrology.LakeCoverageFraction[cell]>=.5||LocalCropExposure(s,cell)<450||CultureOpportunity(s,cell)<.25)continue;
                double fit=0;for(int k=0;k<6;k++)if(s.History.Agriculture[source].AdoptedYear[k]>0&&s.History.Agriculture[source].AdoptedYear[k]<=s.Years)fit=Math.Max(fit,CropTransferFit(s,source,cell,k));
                double score=fit*D.Clamp(HistoricalSiteDensity(s,cell)/.025);if(fit<.3||score<=scores[patch])continue;
                if(homes.Within(World.Surface.Cells[cell].Position,chord*chord).Any(i=>D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[society.Cultures[i].OriginCell].Position)*World.Recipe.RadiusKm<220))continue;
                cells[patch]=cell;sources[patch]=source;scores[patch]=score;
            }
            var founded=new List<int>();
            for(int patch=0;patch<cells.Length;patch++)if(cells[patch]>=0)
            {
                token.ThrowIfCancellationRequested();int cell=cells[patch],source=sources[patch];
                if(founded.Any(other=>D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[other].Position)*World.Recipe.RadiusKm<220))continue;
                double chance=HistoricalCropDiffusionEffects.AdoptionChance(scores[patch],HistoricalSiteDensity(s,cell),LocalCropExposure(s,cell));
                if(HistoryRoll("local-farming-13",cell,s.Years)>=ElapsedChance(chance,scale))continue;
                var child=FoundCulture(s,patch,cell);InitializeHistoricalCulture(s,child);AdoptRegionalCrops(s,child,source);founded.Add(cell);
                HistoryEvent(s,"regional-farming-community",child.Name+" establishes a local farming tradition through inhabited exchange routes from "+society.Cultures[source].Name+". Suitable seeds are introduced; local identity and institutions develop independently.",cell,child.Id,category:ChronicleCategory.Society);
            }
        }
        public HistoricalCropTransmission AssessCropTransmission(PrehistoryState s,int cell,CancellationToken token=default)
        {
            Validate(s);if(!ModernHistory(s)||cell<0||cell>=World.Surface.Cells.Length)throw new ArgumentException("Select a location in current history.");BuildLocalCropDiffusion(s,token);
            int source=localCropSource==null?-1:localCropSource[cell];return new HistoricalCropTransmission{Cell=cell,SourceCulture=source,TravelEffortM=source<0?0:localCropEffort[cell],Exposure=LocalCropExposure(s,cell)};
        }
        void LocalCropTeachingContacts(PrehistoryState s,SortedSet<(int,int)> contacts)
        {
            if(localCropSource==null)return;
            foreach(var a in s.History.Agriculture)foreach(int cell in a.PracticeCell.Distinct().OrderBy(i=>i))
            {int teacher=localCropSource[cell];if(teacher>=0&&teacher!=a.CultureId&&LocalCropExposure(s,cell)>=450)contacts.Add((Math.Min(teacher,a.CultureId),Math.Max(teacher,a.CultureId)));}
        }
    }
}

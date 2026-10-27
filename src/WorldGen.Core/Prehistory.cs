using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public enum ForagerLifestyle { Woodland, Grassland, Desert, Cold, Highland, Wetland, Coastal }
    [Serializable] public sealed partial class PrehistoryState
    {
        public string Version="prehistory-6", Species="Homo sapiens", WorldHash="", Hash="";
        [System.Runtime.Serialization.OptionalField] public SocietyState Society;
        [System.Runtime.Serialization.OptionalField] public HistoryState History;
        [System.Runtime.Serialization.OptionalField] public SimulationTimeline Timeline;
        [System.Runtime.Serialization.OptionalField] public HomininHistory DeepPast;
        public int Origin, Years; public bool Started;
        public long[] Population=Array.Empty<long>();
        public int[] ArrivalYear=Array.Empty<int>(), Ancestry=Array.Empty<int>();
        // Non-authoritative publication identity. It permits adapters to reuse
        // map colors across a clock-only copy, including detached publications.
        // It is neither serialized, hashed, nor consulted by simulation rules.
        [NonSerialized] internal object DomainToken=new object();
        public static bool SameDomain(PrehistoryState a,PrehistoryState b)=>ReferenceEquals(a,b)||a!=null&&b!=null&&a.DomainToken!=null&&ReferenceEquals(a.DomainToken,b.DomainToken);
        public PrehistoryState Copy(bool copySociety=true){var s=(PrehistoryState)MemberwiseClone();s.DomainToken=new object();s.Timeline=Timeline?.Copy();s.DeepPast=DeepPast?.Copy();s.Population=(long[])Population.Clone();s.ArrivalYear=(int[])ArrivalYear.Clone();s.Ancestry=(int[])Ancestry.Clone();if(copySociety){s.Society=Society?.Copy();s.History=History?.Copy();}return s;}
        internal PrehistoryState CopyClockOnly(){var s=(PrehistoryState)MemberwiseClone();s.Timeline=Timeline?.Copy();return s;}
        internal PrehistoryState CopyAggregateOwned()
        {
            var copy=Copy(false);copy.Society=Society.CopyOwned(History.Aggregates.Living);copy.History=History.CopyOwned();return copy;
        }
        public string ComputeHash()=>DeepPast?.Active==true?StateHash.Compute(new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry,Timeline,Society.SelectedStepYears,Society.ManualStep,DeepPast.CanonicalHash}):DeepPast==null?BaseHash():StateHash.Compute(new object[]{BaseHash(),DeepPast});
        string BaseHash()=>History?.Aggregates!=null?AggregateHash(true):Timeline!=null?StateHash.Compute(new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry,Society,History,Timeline}):Version=="prehistory-6"?StateHash.Compute(new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry,Society,History}):Version=="prehistory-4"||Version=="prehistory-5"?StateHash.Compute(new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry,Society}):StateHash.Compute(new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry});
        public void Seal(){Hash=ComputeHash();}
    }
    /// <summary>Independent demographic state on connected ecoregion patches. Never alters generated geography.</summary>
    public sealed partial class Prehistory
    {
        public sealed class Patch
        {
            public int Region, Cell; public int[] Cells; public long Capacity; public double RadiusKm;
            public ForagerLifestyle Lifestyle;
            public bool Island;
        }
        public sealed class Route
        {
            public int A,B,DistanceM,SeaDropM,CellA=-1,CellB=-1;
            // Runtime geography only. Keep legacy land CellA/B=-1 unchanged;
            // revision6 explicitly uses the actual neighboring border cells.
            public int LandCellA=-1,LandCellB=-1;
            public bool Shallow,Sea,Coastal;
        }
        public readonly World World; public readonly int[] CellPatch; public readonly Patch[] Patches; public readonly Route[] Routes;
        readonly int[][] regionalCultureEdges;
        readonly int[][] realmLandRoutes;
        // Derived habitat weights, not a second population store. Prefix allocation conserves every person.
        readonly long[] ruralWeight,ruralPrefix;readonly long[] ruralPatchWeight;
        public const int StepYears=100;
        public static string CurrentHistoryVersion=>LivingWorldHistoryVersion;
        public static int YearsPerStep(PrehistoryState state)=>state.Version=="prehistory-1"?250:state.Society?.SelectedStepYears??StepYears;
        public static double CrossingChance(int distanceM,bool shallow)=>shallow?.22:.015+.30*Math.Exp(-Math.Max(0,distanceM)/55000.0);
        public static bool LowSeaLevel(int years)=>years%100000>=20000 && years%100000<80000;
        public Prehistory(World world,CancellationToken token=default)
        {
            World=world;var e=world.Ecology;var s=world.Surface;
            ruralWeight=new long[s.Cells.Length];ruralPrefix=new long[s.Cells.Length];
            bool historicalEcology=world.Prehistory==null||world.Prehistory.DeepPast!=null||RevisedHistory(world.Prehistory);
            if(e==null||e.Region.Length!=s.Cells.Length)throw new ArgumentException("Generate ecoregions before placing humans.");
            CellPatch=Enumerable.Repeat(-1,s.Cells.Length).ToArray();var patches=new List<Patch>();var queue=new Queue<int>();
            for(int i=0;i<CellPatch.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();
                if(CellPatch[i]>=0||e.Region[i]<0||world.Hydrology.OceanConnected[i])continue;
                int id=patches.Count,region=e.Region[i];var cells=new List<int>();CellPatch[i]=id;queue.Enqueue(i);
                while(queue.Count>0){int c=queue.Dequeue();cells.Add(c);foreach(int j in s.Cells[c].Neighbors)if(CellPatch[j]<0&&!world.Hydrology.OceanConnected[j]&&e.Region[j]==region){CellPatch[j]=id;queue.Enqueue(j);}}
                cells.Sort();double capacity=0,area=0,coast=0;
                foreach(int c in cells)
                {
                    var h=e.Habitat[c];double km2=s.Cells[c].AreaM2/1e6;area+=km2;
                    bool shore=s.Cells[c].Neighbors.Any(j=>world.Hydrology.OceanConnected[j]);if(shore)coast+=km2;
                    double density=.003+.09*D.Clamp(h.ProductivityGCM2Year/1000.0);
                    density*=D.Clamp((h.GrowingDays+40)/240.0,.08,1)*D.Clamp(1-h.ReliefM/5000.0,.15,1);
                    if(h.Biome==Biome.Desert)density*=.08;
                    if(h.Biome==Biome.Ice||h.Biome==Biome.Freshwater||h.Biome==Biome.SalineLake||world.ElevationM[c]>5000)density=0;
                    if(shore&&density>0)density+=.015;
                    if(historicalEcology&&density>0)density+=.20*HistoricalRiverAccess(c)*D.Clamp((h.GrowingDays+80)/200.0);
                    double localCapacity=km2*density*(1-world.Hydrology.LakeCoverageFraction[c]);capacity+=localCapacity;
                    ruralWeight[c]=Math.Max(0,(long)Math.Round(localCapacity*1000));
                }
                var biome=e.Regions[region].Biome;var lifestyle=coast>area*.4?ForagerLifestyle.Coastal:
                    biome==Biome.Tundra||biome==Biome.BorealForest?ForagerLifestyle.Cold:
                    biome==Biome.Alpine?ForagerLifestyle.Highland:biome==Biome.Desert?ForagerLifestyle.Desert:
                    biome==Biome.Marsh||biome==Biome.Floodplain||biome==Biome.Mangrove||biome==Biome.Peatland?ForagerLifestyle.Wetland:
                    biome==Biome.Grassland||biome==Biome.Savanna?ForagerLifestyle.Grassland:ForagerLifestyle.Woodland;
                patches.Add(new Patch{Region=region,Cell=cells[0],Cells=cells.ToArray(),Capacity=Math.Max(0,(long)Math.Round(capacity)),RadiusKm=Math.Sqrt(area/Math.PI),Lifestyle=lifestyle});
            }
            bool modern=world.Prehistory==null||world.Prehistory.DeepPast!=null||world.Prehistory.Version=="prehistory-3"||world.Prehistory.Version=="prehistory-4"||(world.Prehistory.Version=="prehistory-5"||world.Prehistory.Version=="prehistory-6");
            if(modern&&world.Subcells!=null)foreach(var group in world.Subcells.Cells)
            {
                // One conservative settlement node per island cell, supported by its largest island only.
                int c=group.Cell;var h=e.Habitat[c];double km2=group.LargestIslandAreaM2/1e6;
                double rainfall=(world.Climate.NorthernSpring.PrecipitationMm[c]+world.Climate.NorthernSummer.PrecipitationMm[c]+world.Climate.NorthernAutumn.PrecipitationMm[c]+world.Climate.NorthernWinter.PrecipitationMm[c]);
                long capacity=(long)Math.Round(km2*.045*D.Clamp((h.MeanTenthsC+100)/250.0,.05,1)*D.Clamp(rainfall/600.0,.05,1));
                if(capacity<25||CellPatch[c]>=0)continue;
                CellPatch[c]=patches.Count;patches.Add(new Patch{Region=-1,Cell=c,Cells=new[]{c},Capacity=capacity,RadiusKm=Math.Sqrt(km2/Math.PI),Lifestyle=ForagerLifestyle.Coastal,Island=true});
            }
            Patches=patches.ToArray();ruralPatchWeight=new long[Patches.Length];
            for(int p=0;p<Patches.Length;p++)foreach(int cell in Patches[p].Cells)
            {
                if(Patches[p].Island)ruralWeight[cell]=Patches[p].Capacity*1000;
                ruralPrefix[cell]=ruralPatchWeight[p];ruralPatchWeight[p]+=ruralWeight[cell];
            }
            var routes=new SortedDictionary<(int,int,int),Route>();
            void Link(int a,int b,int metres,bool sea,bool shallow,int drop=0,int cellA=-1,int cellB=-1,int landCellA=-1,int landCellB=-1)
            {
                if(a<0||b<0||a==b)return;if(a>b){int t=a;a=b;b=t;t=cellA;cellA=cellB;cellB=t;t=landCellA;landCellA=landCellB;landCellB=t;}
                var key=(a,b,modern?(sea?drop:-1):0);
                if(!routes.TryGetValue(key,out var old)||(!sea&&old.Sea)||(sea==old.Sea&&metres<old.DistanceM))routes[key]=new Route{A=a,B=b,DistanceM=metres,Sea=sea,Shallow=shallow,SeaDropM=drop,CellA=cellA,CellB=cellB,LandCellA=landCellA,LandCellB=landCellB};
            }
            var culturalEdges=new List<int>[Patches.Length];
            void CulturalEdge(int patch,int cell)
            {if(patch<0)return;var list=culturalEdges[patch]??(culturalEdges[patch]=new List<int>(8));if(list.Count<8&&!list.Contains(cell))list.Add(cell);}
            foreach(var edge in s.Edges)if(!modern||!world.Hydrology.OceanConnected[edge.A]&&!world.Hydrology.OceanConnected[edge.B])
            {
                int first=CellPatch[edge.A],second=CellPatch[edge.B];Link(first,second,edge.LengthM,false,false,landCellA:edge.A,landCellB:edge.B);
                if(first>=0&&second>=0&&first!=second){CulturalEdge(first,edge.A);CulturalEdge(second,edge.B);}
            }
            // Derived during existing geography setup, never lazily discovered
            // by scanning a region/culture when its first state forms.
            regionalCultureEdges=culturalEdges.Select(list=>list?.ToArray()??Array.Empty<int>()).ToArray();
            // Two bounded coast-to-coast searches: short sea trips and exposed shallow shelves.
            foreach(int depth in modern?new[]{0,30,70,130}:new[]{0,130})
            {
                bool shallow=depth>0;int limit=modern?(shallow?1800000:900000):(shallow?600000:140000);var dist=Enumerable.Repeat(int.MaxValue,s.Cells.Length).ToArray();var owner=Enumerable.Repeat(-1,s.Cells.Length).ToArray();var endpoint=Enumerable.Repeat(-1,s.Cells.Length).ToArray();var heap=new Heap();
                for(int i=0;i<s.Cells.Length;i++)if(CellPatch[i]>=0&&Patches[CellPatch[i]].Capacity>=25&&s.Cells[i].Neighbors.Any(j=>world.Hydrology.OceanConnected[j])){dist[i]=0;owner[i]=CellPatch[i];endpoint[i]=i;heap.Push(0,i);}
                while(heap.Count>0)
                {
                    var item=heap.Pop();int i=item.Id;if(item.Key!=dist[i])continue;if((i&1023)==0)token.ThrowIfCancellationRequested();
                    for(int k=0;k<s.Cells[i].Neighbors.Length;k++)
                    {
                        int j=s.Cells[i].Neighbors[k],length=s.Cells[i].DistanceM[k];long d=(long)dist[i]+length;
                        if(owner[j]>=0&&owner[j]!=owner[i]&&d+dist[j]<=limit)Link(owner[i],owner[j],(int)(d+dist[j]),true,shallow,depth,endpoint[i],endpoint[j]);
                        if(d>limit||!world.Hydrology.OceanConnected[j]||shallow&&world.ElevationM[j]<-depth)continue;
                        if(d<dist[j]||(d==dist[j]&&owner[i]<owner[j])){dist[j]=(int)d;owner[j]=owner[i];endpoint[j]=endpoint[i];heap.Push(d,j);}
                    }
                }
            }
            Routes=routes.Values.ToArray();
            if(world.Prehistory==null||world.Prehistory.DeepPast!=null||(world.Prehistory.Version=="prehistory-5"||world.Prehistory.Version=="prehistory-6"))Routes=Routes.Concat(CoastalDetours(token)).OrderBy(r=>r.A).ThenBy(r=>r.B).ThenBy(r=>r.Coastal).ThenBy(r=>r.SeaDropM).ToArray();
            // Geometry-only setup index. Ordinary treaty reviews address at
            // most eight neighboring land districts, not duplicate sea routes.
            var landLinks=new System.Collections.Generic.List<int>[Patches.Length];
            for(int i=0;i<Routes.Length;i++)if(!Routes[i].Sea){var r=Routes[i];(landLinks[r.A]??(landLinks[r.A]=new System.Collections.Generic.List<int>())).Add(i);(landLinks[r.B]??(landLinks[r.B]=new System.Collections.Generic.List<int>())).Add(i);}
            realmLandRoutes=landLinks.Select(list=>list?.ToArray()??Array.Empty<int>()).ToArray();
        }
        public int ChooseOrigin(int variant=0)
        {
            int best=-1;double score=double.MinValue;
            for(int i=0;i<Patches.Length;i++)if(Patches[i].Capacity>=200&&!Patches[i].Island)
            {
                var p=Patches[i];double v=Rng.Value(World.Recipe.Seed,"sapiens-origin",unchecked(i+variant*104729));
                var h=World.Ecology.Habitat[p.Cell];
                // Habitat tiers dominate the seeded tie break, including on Earth and on rerolls.
                var biome=World.Ecology.Regions[p.Region].Biome;
                v+=biome==Biome.Savanna?20:biome==Biome.Grassland?10:0;
                v+=h.MeanTenthsC>100&&h.MeanTenthsC<300?2:0;
                v+=Math.Min(1,Math.Log(Math.Max(200,p.Capacity)/200.0)/10);
                if(World.Recipe.TerrainSource=="Earth"&&variant==0){var point=World.Surface.Cells[p.Cell].Position;double lon=Math.Atan2(point.Y,point.X)*180/Math.PI,lat=Math.Asin(point.Z)*180/Math.PI;if(lon>10&&lon<45&&lat>-25&&lat<15)v+=3;}
                if(v>score){score=v;best=i;}
            }
            return best;
        }
        public PrehistoryState Prepare(int origin=-1)
            =>World.Prehistory?.DeepPast?.Active==true?PrepareHominins(origin,World.Prehistory.DeepPast.EarthLikeTrajectory):PrepareSapiensState(origin,false);
        public PrehistoryState PrepareSapiens(int origin=-1)=>PrepareSapiensState(origin,true);
        PrehistoryState PrepareSapiensState(int origin,bool currentRules)
        {
            if(origin<0)origin=ChooseOrigin();if(origin<0||origin>=Patches.Length||Patches[origin].Capacity<25)throw new ArgumentException("This location cannot support a founding band; choose another land region.");
            string version=currentRules?"prehistory-6":World.Prehistory?.Version??"prehistory-6";
            var s=new PrehistoryState{Version=version,Origin=origin,WorldHash=World.Stages.Last().Hash,Population=new long[Patches.Length],ArrivalYear=Enumerable.Repeat(-1,Patches.Length).ToArray(),Ancestry=Enumerable.Repeat(5000,Patches.Length*8).ToArray(),History=version=="prehistory-6"?new HistoryState{Version=currentRules?CurrentHistoryVersion:World.Prehistory?.History?.Version??CurrentHistoryVersion}:null,Society=version=="prehistory-4"||version=="prehistory-5"||version=="prehistory-6"?new SocietyState{Knowledge=new int[Patches.Length]}:null};
            // A new history retains the user's limit, not the previous world's
            // live ports/campaign memories referencing settlements it no longer has.
            if(ModernHistory(s))s.History.Modern=new HistoricalModernSettings{TechnologyCeiling=World.Prehistory?.History?.Modern?.TechnologyCeiling??HistoricalTechnologyCeiling.Modern};
            string requested=s.History?.Version;
            if(IsAggregateHistoryVersion(requested))s.History.Version="history-16";
            s.Seal();if(requested==LivingWorldHistoryVersion)return UpgradeToLivingWorldHistory(s);if(requested==EnvironmentalHistoryVersion)return UpgradeToEnvironmentalHistory(s);if(requested==LanguageHistoryVersion)return UpgradeToLanguageHistory(s);if(requested==AggregateHistoryVersion)return UpgradeToAggregateHistory(s);if(requested==LivingHistoryVersion)return UpgradeToLivingHistory(s);if(requested==ConnectedHistoryVersion)return UpgradeToConnectedHistory(s);if(requested==RegionalHistoryVersion)return UpgradeToRegionalHistory(s);if(requested==CommunityHistoryVersion)return UpgradeToCommunityHistory(s);if(requested==DiffusionHistoryVersion)return UpgradeToDiffusionHistory(s);if(requested==AgronomicHistoryVersion)return UpgradeToAgronomicHistory(s);if(requested==TechnicalHistoryVersion)return UpgradeToTechnicalHistory(s);if(requested==ProvincialFrontierHistoryVersion)return UpgradeToProvincialFrontierHistory(s);if(requested==ColonialHistoryVersion)return UpgradeToColonialHistory(s);if(requested==CoastalHistoryVersion)return UpgradeToCoastalHistory(s);if(requested==TerritorialHistoryVersion)return UpgradeToTerritorialHistory(s);if(requested==ResearchPacingHistoryVersion)return UpgradeToResearchPacingHistory(s);return s;
        }
        public PrehistoryState Start(PrehistoryState source){if(EarlyHominins(source))return StartHominins(source);Validate(source);if(source.Started)return source;var s=source.Copy();s.Started=true;s.Population[s.Origin]=Math.Min(200,Patches[s.Origin].Capacity);s.ArrivalYear[s.Origin]=0;if(s.History?.Aggregates!=null)s.History.Aggregates.TotalPopulation+=s.Population[s.Origin];if(s.History?.Identities?.Version>=2)EnsureForagerHeritage(s,s.Origin);s.Seal();return s;}
        public void Validate(PrehistoryState s)
        {
            if(EarlyHominins(s)){ValidateHominins(s);return;}
            if(s==null||(s.Version!="prehistory-1"&&s.Version!="prehistory-2"&&s.Version!="prehistory-3"&&s.Version!="prehistory-4"&&s.Version!="prehistory-5"&&s.Version!="prehistory-6")||s.Species!="Homo sapiens"||s.WorldHash!=World.Stages.Last().Hash||s.Origin<0||s.Origin>=Patches.Length||s.Years<0||s.Version!="prehistory-4"&&s.Version!="prehistory-5"&&s.Version!="prehistory-6"&&s.Years%(s.Version=="prehistory-1"?250:100)!=0||s.Population?.Length!=Patches.Length||s.ArrivalYear?.Length!=Patches.Length||s.Ancestry?.Length!=Patches.Length*8||s.Population.Any(p=>p<0)||s.Ancestry.Any(a=>a<0||a>10000)||s.Hash!=s.ComputeHash())throw new ArgumentException("Invalid or incompatible prehistory save.");
            if(s.History?.Aggregates!=null){ValidateAggregateHistory(s);ValidateAggregateTimeline(s);return;}
            if(s.Version=="prehistory-4"||s.Version=="prehistory-5"||s.Version=="prehistory-6")ValidateSociety(s);if(s.Version=="prehistory-6")ValidateHistory(s);
            ValidateTimeline(s);
        }
        public PrehistoryState Advance(PrehistoryState source,int steps,CancellationToken token=default)
        {
            if(EarlyHominins(source))return AdvanceHominins(source,checked((long)steps*YearsPerStep(source)*SimulationTime.Year),int.MaxValue,token,true);
            if(source.Version=="prehistory-4"||source.Version=="prehistory-5"||source.Version=="prehistory-6")return AdvanceYears(source,checked(steps*YearsPerStep(source)),token);
            Validate(source);if(!source.Started)throw new InvalidOperationException("Start the world before advancing time.");if(steps<1||steps>10000)throw new ArgumentOutOfRangeException(nameof(steps));var s=source.Copy(false);
            if(EraHistory(s))s.Society=s.Society.CopyDemographicKnowledge();
            bool legacy=s.Version=="prehistory-1";int stepYears=legacy?250:100;double scale=stepYears/250.0;
            for(int step=0;step<steps;step++)
            {
                token.ThrowIfCancellationRequested();if(s.Years>10000000-stepYears)throw new InvalidOperationException("Prototype timeline limit reached.");var before=(long[])s.Population.Clone();var incoming=new long[Patches.Length];var outgoing=new long[Patches.Length];var genes=new long[s.Ancestry.Length];
                bool revised=s.Version=="prehistory-3";
                // Founders carry learned practices across the same successful route as people.
                // Read the old checkpoint so a newly founded patch cannot relay knowledge
                // again within this demographic step. Historical technologies/crops are separate.
                var departingKnowledge=EraHistory(s)&&!LocalHistory(s)?(int[])s.Society.Knowledge.Clone():null;
                var foundingKnowledge=departingKnowledge==null?null:new int[Patches.Length];
                var capacities=revised?Patches.Select((p,i)=>SocietyCapacity(s,i)).ToArray():Patches.Select(p=>p.Capacity).ToArray();
                for(int i=0;i<before.Length;i++)if(before[i]>0){long cap=capacities[i];double density=before[i]/(double)Math.Max(1,cap);double rate=FrontierHistory(s)?FrontierGrowthPerCentury(before[i],cap)*stepYears/100.0:.12*scale;double growth=before[i]*rate*(1-density);before[i]=Math.Max(0,before[i]+(long)Math.Round(growth));}
                var navigation=new int[Patches.Length];if(s.History!=null&&s.Society.CellCulture.Length>0)for(int p=0;p<Patches.Length;p++)foreach(int cell in Patches[p].Cells){int owner=s.Society.CellCulture[cell];if(owner>=0){var hc=s.History.Cultures[owner];navigation[p]=Math.Max(navigation[p],HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?2:HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)?1:0);}}
                foreach(var route in revised?ActiveRoutes(UsesGlacialDispersal(s)?s.Years:0,EraHistory(s)?navigation:null):Routes)
                {
                    for(int direction=0;direction<2;direction++)
                    {
                        int a=direction==0?route.A:route.B,b=direction==0?route.B:route.A;
                        if(before[a]<80||capacities[b]<25||!revised&&route.Sea&&(route.Shallow?!LowSeaLevel(s.Years):legacy&&s.Years<15000))continue;
                        double similarity=revised?HabitatSimilarity(a,b):1;
                        int travel=(int)((Patches[a].RadiusKm+Patches[b].RadiusKm+route.DistanceM/1000.0)/(revised?.15+.65*similarity:.12));
                        if(s.History!=null&&navigation[a]>0)travel=Math.Max(100,travel/(1+navigation[a]*2));
                        if(s.ArrivalYear[a]<0||s.Years-s.ArrivalYear[a]<travel)continue;
                        double pressure=before[a]/(double)Math.Max(1,capacities[a])-before[b]/(double)capacities[b];
                        if(pressure<=0)continue;
                        double chance=revised?DispersalChance(route,a,b,before[a],capacities[a],UsesGlacialDispersal(s)?s.Years:0,s.Years-s.ArrivalYear[a]):route.Sea?(legacy?(route.Shallow?.2:.025):CrossingChance(route.DistanceM,route.Shallow)):1;
                        if(s.History!=null&&route.Sea&&navigation[a]>0&&!route.Shallow)chance=Math.Max(chance,D.Q((navigation[a]==2?.35:.12)*Math.Exp(-route.DistanceM/(navigation[a]==2?450000.0:180000.0)),1000000000));
                        if(EraHistory(s)&&route.Sea)chance=Math.Max(chance,MaritimeChance(route,navigation[a]));
                        if(s.Society!=null)chance=D.Clamp(chance*(LocalHistory(s)?1:.25+.75*s.Society.Knowledge[a]/10000.0)*(.55+.9*Rng.Value(World.Recipe.Seed,"dispersal-window",s.Years/2000)));
                        if(Rng.Value(World.Recipe.Seed,"sapiens-crossing",unchecked(s.Years*31+a*7381+b*193))>chance)continue;
                        long movers=Math.Min(Math.Max(0,before[a]/4-outgoing[a]),Math.Min(500,Math.Max(1,(long)(before[a]*(.035*scale)*pressure))));
                        if(before[b]==0&&incoming[b]==0){movers=Math.Min(revised&&route.Sea&&!route.Shallow?100:50,Math.Min(Math.Max(0,before[a]/4-outgoing[a]),capacities[b]));if(movers<(revised&&route.Sea&&!route.Shallow?50:25))continue;}
                        movers=Math.Min(movers,Math.Max(0,capacities[b]-before[b]-incoming[b]));if(movers<1)continue;
                        if(foundingKnowledge!=null&&before[b]==0&&movers>=25)foundingKnowledge[b]=Math.Max(foundingKnowledge[b],departingKnowledge[a]);
                        outgoing[a]+=movers;incoming[b]+=movers;for(int g=0;g<8;g++)genes[b*8+g]+=movers*s.Ancestry[a*8+g];
                    }
                }
                for(int i=0;i<before.Length;i++)
                {
                    long retained=before[i]-outgoing[i],population=retained+incoming[i];s.Population[i]=population;
                    if(population>0&&s.ArrivalYear[i]<0)s.ArrivalYear[i]=s.Years+stepYears;
                    if(foundingKnowledge!=null&&population>0)s.Society.Knowledge[i]=Math.Max(s.Society.Knowledge[i],foundingKnowledge[i]);
                    if(population==0)continue;
                    for(int g=0;g<8;g++)
                    {
                        double mean=(retained*s.Ancestry[i*8+g]+genes[i*8+g])/(double)population;
                        double frequency=mean/10000.0;
                        double drift=(Rng.Value(World.Recipe.Seed,"sapiens-neutral-drift",unchecked(s.Years*97+i*17+g))-0.5)*Math.Sqrt(12)*10000*Math.Sqrt((10*scale)*frequency*(1-frequency)/(2*Math.Max(50,population*.4)));
                        s.Ancestry[i*8+g]=Math.Max(0,Math.Min(10000,(int)Math.Round(mean+drift)));
                    }
                }
                s.Years+=stepYears;
            }
            s.Seal();return s;
        }
        public int[] Clusters(PrehistoryState state)
        {
            var parent=Enumerable.Range(0,Patches.Length).ToArray();int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            foreach(var r in Routes)if(!r.Sea&&state.Population[r.A]>0&&state.Population[r.B]>0&&Patches[r.A].Lifestyle==Patches[r.B].Lifestyle){int a=Root(r.A),b=Root(r.B);parent[Math.Max(a,b)]=Math.Min(a,b);}
            for(int i=0;i<parent.Length;i++)parent[i]=state.Population[i]>0?Root(i):-1;return parent;
        }
    }
}

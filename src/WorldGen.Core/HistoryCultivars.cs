using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalLocalHarvest
    {
        public int Year,CultureId;public int[] CropIds=Array.Empty<int>(),SharesPermille=Array.Empty<int>();
        public HistoricalLocalHarvest Copy()=>new HistoricalLocalHarvest{Year=Year,CultureId=CultureId,CropIds=(int[])CropIds.Clone(),SharesPermille=(int[])SharesPermille.Clone()};
    }
    [Serializable] public sealed class HistoricalCultivar
    {
        public int CropId=-1,OriginCulture=-1,ParentCulture=-1,SelectedYears;
        public int MaturityDays,DaylengthLatitudeTenths,WaterRequirementPermille,YieldPermille=650;
        [System.Runtime.Serialization.OptionalField] public int MinimumWarmTenthsC;
        public HistoricalCultivar Copy()=>(HistoricalCultivar)MemberwiseClone();
    }
    public sealed class HistoricalCropDefinition
    {
        public string Name;public int Group,MaturityDays,MinimumWarmC,MinimumWaterPermille;public bool DaylengthSensitive;
        public HistoricalCropDefinition(string name,int group,int maturity,int warmth,int water,bool sensitive=false)
        {Name=name;Group=group;MaturityDays=maturity;MinimumWarmC=warmth;MinimumWaterPermille=water;DaylengthSensitive=sensitive;}
    }
    public sealed class HistoricalCropUse
    {
        public int CropId,Group,SharePermille,SelectedYears,OriginCulture;public string Name;
        public double Suitability,StorableSupport;
    }
    public static class HistoricalCropCatalog
    {
        // Named Earth food plants in plausible new native provinces. Crop
        // requirements are broad, replaceable agronomic priors, not cultivars
        // reconstructed from a particular archaeological site.
        public static readonly HistoricalCropDefinition[] Crops={
            new HistoricalCropDefinition("Wheat",0,140,14,350),new HistoricalCropDefinition("Barley",0,110,12,260),new HistoricalCropDefinition("Rye",0,125,11,300),new HistoricalCropDefinition("Oats",0,120,11,450),
            new HistoricalCropDefinition("Maize",1,150,20,450,true),new HistoricalCropDefinition("Sorghum",1,130,23,250,true),new HistoricalCropDefinition("Millet",1,100,20,220,true),
            new HistoricalCropDefinition("Rice",2,160,23,700,true),
            new HistoricalCropDefinition("Cassava",3,270,24,650),new HistoricalCropDefinition("Yam",3,220,23,700),new HistoricalCropDefinition("Potato",3,120,13,450,true),
            new HistoricalCropDefinition("Olive",4,170,18,280),new HistoricalCropDefinition("Date palm",4,240,26,250),new HistoricalCropDefinition("Coconut",4,300,25,900),
            new HistoricalCropDefinition("Lentil",5,110,15,300),new HistoricalCropDefinition("Pea",5,100,12,400),new HistoricalCropDefinition("Bean",5,120,19,500,true),new HistoricalCropDefinition("Soybean",5,130,20,600,true)};
        public static double Fit(HabitatCell h,HistoricalCultivar cultivar,double latitude,double freshwater=0)
        {
            if(cultivar==null||cultivar.CropId<0)return 0;var crop=Crops[cultivar.CropId];
            double season=D.Clamp((h.GrowingDays-cultivar.MaturityDays+50)/70.0);
            double minimum=cultivar.MinimumWarmTenthsC==0?crop.MinimumWarmC:cultivar.MinimumWarmTenthsC/10.0;
            double warmth=D.Clamp((h.WarmTenthsC/10.0-minimum+3)/8.0);
            double water=h.AridityMilli;bool fresh=h.Flooding==FloodRegime.RiverSeasonal||h.Flooding==FloodRegime.RiverPersistent||freshwater>.2;if(fresh)water=Math.Max(water,900);
            double moisture=D.Clamp(water/Math.Max(1,cultivar.WaterRequirementPermille));
            double daylength=crop.DaylengthSensitive?Math.Exp(-Math.Abs(Math.Abs(latitude)*10-cultivar.DaylengthLatitudeTenths)/100.0):1;
            double frozen=D.Clamp(1-h.PermafrostPermille/700.0);
            return D.Q(D.Clamp(season*warmth*moisture*daylength*frozen),1000000);
        }
        public static double TransferFactor(HabitatCell h,int group,double freshwater=0,bool managed=false)
        {
            double physical=HistoricalCrops.AbioticPotential(h,freshwater);
            double system=Math.Min(1.2,HistoricalCrops.Suitability(h,group,freshwater)/Math.Max(.01,physical));
            double clearing=h.Biome==Biome.TropicalRainforest?(managed?.85:.55):(h.Habitat&Habitat.Forest)!=0?.85:1;
            return D.Q(system*D.Clamp(physical/.2)*LeachingPenalty(h,managed)*clearing,1000000);
        }
        public static double TransferFit(HabitatCell h,HistoricalCultivar cultivar,double latitude,double freshwater=0,bool managed=false)
            =>cultivar==null?0:D.Q(Fit(h,cultivar,latitude,freshwater)*TransferFactor(h,Crops[cultivar.CropId].Group,freshwater,managed),1000000);
        public static double LeachingPenalty(HabitatCell h,bool soilManagement)
        {
            if(h.Soil!=SoilKind.Leached)return 1;
            return soilManagement?.85:.5+.2*D.Clamp(h.SoilFertilityPermille/700.0);
        }
    }
    public sealed partial class Prehistory
    {
        sealed class CultivarFitCache { public int[] Signature=Array.Empty<int>();public readonly System.Collections.Generic.Dictionary<int,int[]> Cells=new System.Collections.Generic.Dictionary<int,int[]>(); }
        readonly System.Collections.Generic.Dictionary<int,CultivarFitCache> cultivarFitCaches=new System.Collections.Generic.Dictionary<int,CultivarFitCache>();
        PrehistoryState cultivarFitState;string cultivarFitHash;double[] cropLatitudes;
        double CropLatitude(int cell)
        {
            if(cropLatitudes==null)cropLatitudes=World.Surface.Cells.Select(c=>Math.Abs(Math.Asin(c.Position.Z)*180/D.Pi)).ToArray();return cropLatitudes[cell];
        }
        void InvalidateCultivarFits(int culture)=>cultivarFitCaches.Remove(culture);
        void PrepareCultivarFits(PrehistoryState s)
        {
            if(ReferenceEquals(s,cultivarFitState)&&s.Hash==cultivarFitHash)return;
            foreach(var a in s.History.Agriculture)
            {
                var signature=(a.Cultivars??Array.Empty<HistoricalCultivar>()).SelectMany(c=>c==null?new[]{-1}:new[]{c.CropId,c.MaturityDays,c.DaylengthLatitudeTenths,c.WaterRequirementPermille,c.MinimumWarmTenthsC}).ToArray();
                if(!cultivarFitCaches.TryGetValue(a.CultureId,out var cache)||!signature.SequenceEqual(cache.Signature))cultivarFitCaches[a.CultureId]=new CultivarFitCache{Signature=signature};
            }
            cultivarFitState=s;cultivarFitHash=s.Hash;
        }
        void InitializeCultivars(PrehistoryState s,HistoricalAgriculture a)
        {
            a.Cultivars=new HistoricalCultivar[6];
            for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0)
            {
                // Existing saves have only broad traditions. The upgrade adds
                // an explicit representative, without inventing past events.
                int source=a.SourceCulture[k];a.Cultivars[k]=MakeCultivar(s,a.CultureId,k,source);
                a.Cultivars[k].SelectedYears=Math.Min(4000,Math.Max(0,s.Years-a.AdoptedYear[k]+1));
            }
        }
        HistoricalCultivar MakeCultivar(PrehistoryState s,int culture,int group,int source)
        {
            if(source>=0&&s.History.Agriculture[source].Cultivars?[group]!=null)
            {var inherited=s.History.Agriculture[source].Cultivars[group].Copy();inherited.ParentCulture=source;return inherited;}
            var a=s.History.Agriculture[culture];int cell=a.PracticeCell[group];var h=CropHabitat(s,cell);
            var candidates=Enumerable.Range(0,HistoricalCropCatalog.Crops.Length).Where(k=>HistoricalCropCatalog.Crops[k].Group==group).ToArray();
            HistoricalCultivar New(int id){var crop=HistoricalCropCatalog.Crops[id];return new HistoricalCultivar{CropId=id,OriginCulture=culture,ParentCulture=source,MaturityDays=crop.MaturityDays,WaterRequirementPermille=crop.MinimumWaterPermille,MinimumWarmTenthsC=crop.MinimumWarmC*10,DaylengthLatitudeTenths=(int)Math.Round(CropLatitude(cell)*10)};}
            int choice=candidates.OrderByDescending(k=>HistoricalCropCatalog.Fit(h,New(k),CropLatitude(cell),riverSites[cell])*(.75+.25*Rng.Value(World.Recipe.Seed,"crop-species-province",unchecked(World.Ecology.Province[cell]*31+k)))).ThenBy(k=>k).First();
            return New(choice);
        }
        double LocalCultivarFit(PrehistoryState s,int culture,int cell,int crop)
        {
            PrepareCultivarFits(s);if(!cultivarFitCaches.TryGetValue(culture,out var cache)){cache=new CultivarFitCache();cultivarFitCaches[culture]=cache;}
            if(!cache.Cells.TryGetValue(cell,out var values))
            {
                var a=s.History.Agriculture[culture];values=new int[6];for(int k=0;k<6;k++)values[k]=(int)Math.Round(HistoricalCropCatalog.Fit(CropHabitat(s,cell),a.Cultivars?[k],CropLatitude(cell),riverSites[cell])*1000000);
                cache.Cells[cell]=values;
            }
            return values[crop]/1000000.0;
        }
        double CropTransferFit(PrehistoryState s,int culture,int cell,int crop)
        {
            int recipient=s.Society.CellCulture.Length>cell?s.Society.CellCulture[cell]:-1;
            bool managed=recipient>=0&&HistoricalTechnologies.Has(s.History.Cultures[recipient],HistoricalTechnology.CropRotation);
            return D.Q(LocalCultivarFit(s,culture,cell,crop)*HistoricalCropCatalog.TransferFactor(CropHabitat(s,cell),crop,riverSites[cell],managed),1000000);
        }
        void SelectLocalCrops(PrehistoryState s,CultureIdentity culture,double elapsedYears)
        {
            var a=s.History.Agriculture[culture.Id];if(a.Cultivars==null)InitializeCultivars(s,a);if(a.HusbandryYears==null)a.HusbandryYears=new int[6];
            for(int k=0;k<6;k++)
            {
                if(a.AdoptedYear[k]==0)continue;var cultivar=a.Cultivars[k]??(a.Cultivars[k]=MakeCultivar(s,culture.Id,k,a.SourceCulture[k]));
                int cell=a.PracticeCell[k];if(culture.Farming<50)continue;
                bool occupied=s.Society.CellCulture[cell]==culture.Id&&(RuralCellPopulation(s,cell)>=10||s.Society.Settlements.Any(t=>t.CultureId==culture.Id&&t.Cell==cell&&t.Population>=100));
                if(!occupied)
                {
                    int other=s.Society.Settlements.Where(t=>t.CultureId==culture.Id&&t.Population>=100&&LocalCultivarFit(s,culture.Id,t.Cell,k)>=.08).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).Select(t=>t.Cell).DefaultIfEmpty(-1).First();
                    if(other<0)continue;
                    a.HusbandryYears[k]=(int)Math.Round(a.HusbandryYears[k]*(CropHabitat(s,cell).Biome==CropHabitat(s,other).Biome?.25:0));a.PracticeCell[k]=cell=other;
                }
                double fit=LocalCultivarFit(s,culture.Id,cell,k);if(fit<.08)continue;
                double practiced=Math.Min(elapsedYears,Math.Max(0,s.Years-(a.AdoptedYear[k]-1)));
                int years=Math.Max(0,(int)Math.Round(practiced*Math.Min(1,culture.Farming/400.0)*fit));if(years==0)continue;
                cultivar.SelectedYears=Math.Min(20000,cultivar.SelectedYears+years);a.HusbandryYears[k]=Math.Min(20000,a.HusbandryYears[k]+years);
                var definition=HistoricalCropCatalog.Crops[cultivar.CropId];double selection=D.Clamp(cultivar.SelectedYears/6000.0);
                // Long cultivation can shorten maturity by at most 30%, not
                // grow wheat through polar darkness or remove water demand.
                cultivar.MaturityDays=(int)Math.Round(definition.MaturityDays*(1-.3*selection));
                cultivar.WaterRequirementPermille=(int)Math.Round(definition.MinimumWaterPermille*(1-.15*selection));
                cultivar.MinimumWarmTenthsC=definition.MinimumWarmC*10-(int)Math.Round(40*selection);
                cultivar.YieldPermille=650+(int)Math.Round(650*D.Clamp(cultivar.SelectedYears/8000.0));
                int target=(int)Math.Round(CropLatitude(cell)*10),move=(int)Math.Round(Math.Min(Math.Abs(target-cultivar.DaylengthLatitudeTenths),years*.02));
                cultivar.DaylengthLatitudeTenths+=Math.Sign(target-cultivar.DaylengthLatitudeTenths)*move;
            }
            InvalidateCultivarFits(culture.Id);
        }
        public HistoricalCropUse[] AssessLocalCrops(PrehistoryState s,int cell,int culture=-1)
        {
            if(!ModernHistory(s)||cell<0||cell>=CellPatch.Length||CellPatch[cell]<0)return Array.Empty<HistoricalCropUse>();
            if(culture<0)culture=HistoricalCells.Culture(s,cell);
            var town=s.Society.Settlements.FirstOrDefault(t=>t.Cell==cell&&t.Population>0);if(town!=null)culture=town.CultureId;
            if(culture<0||culture>=s.History.Agriculture.Length||s.Society.Cultures[culture].Farming==0)return Array.Empty<HistoricalCropUse>();
            var a=s.History.Agriculture[culture];var rows=Enumerable.Range(0,6).Where(k=>a.AdoptedYear[k]>0&&a.Cultivars[k]!=null).Select(k=>new HistoricalCropUse{CropId=a.Cultivars[k].CropId,Name=HistoricalCropCatalog.Crops[a.Cultivars[k].CropId].Name,Group=k,OriginCulture=a.Cultivars[k].OriginCulture,SelectedYears=a.Cultivars[k].SelectedYears,Suitability=AggregateHistory(s)?HistoricalCropCatalog.TransferFit(CropHabitat(s,cell),a.Cultivars[k],Math.Abs(Math.Asin(World.Surface.Cells[cell].Position.Z)*180/Math.PI),riverSites[cell]):D.Q(CropSuitability(s,cell,k)*LocalCultivarFit(s,culture,cell,k),1000000)}).Where(r=>r.Suitability>=.02).OrderByDescending(r=>r.Suitability).ThenBy(r=>r.CropId).ToArray();
            double sum=rows.Sum(r=>r.Suitability);int assigned=0;for(int i=0;i<rows.Length;i++){rows[i].SharePermille=i==rows.Length-1?1000-assigned:(int)Math.Floor(1000*rows[i].Suitability/sum);assigned+=rows[i].SharePermille;rows[i].StorableSupport=D.Q(rows[i].Suitability*CropStorage(s,culture,rows[i].Group)/1000.0,1000000);}return rows;
        }
        void SurveyTownCrops(PrehistoryState s,int culture)
        {
            foreach(var town in s.Society.Settlements.Where(t=>t.CultureId==culture&&t.Population>0))
            {
                var crops=AssessLocalCrops(s,town.Cell,culture);
                s.History.Towns[town.Id].Cropping=new HistoricalLocalHarvest{Year=s.Years,CultureId=culture,CropIds=crops.Select(c=>c.CropId).ToArray(),SharesPermille=crops.Select(c=>c.SharePermille).ToArray()};
            }
        }
    }
}

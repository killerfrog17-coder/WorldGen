using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryCropFieldAudit
{
    public static void Run(string input,string output)
    {
        if(File.Exists(output))throw new ArgumentException("Use a new field-factor audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(input));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string hash=source.ComputeHash();
        if(hash!="eacf2838970bf41cf3ff49bb2d7edb1ec7911222c0df74a9e59d5f0afb1af4d6")throw new ArgumentException("This bounded diagnostic requires the exact reviewed G Earth checkpoint.");
        var state=source.Copy();object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args);
        Call("PrepareAggregateRelations",state);var river=(double[])typeof(Prehistory).GetField("riverSites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;var access=(double[])typeof(Prehistory).GetField("riverAccess",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        HabitatCell Copy(HabitatCell h)=>(HabitatCell)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(h,null)!;
        var rows=new List<object>();var checks=new List<object>();void Check(bool pass,string message){checks.Add(new{Pass=pass,Message=message});if(!pass)throw new Exception(message);}
        foreach(int cell in new[]{176034,175424,163381})
        {
            const int culture=51,group=0;var h=(HabitatCell)Call("CommunityAgronomicHabitat",state,cell,group);var original=world.Ecology.Habitat[cell];var seed=state.History.Agriculture[culture].Cultivars[group];var def=HistoricalCropCatalog.Crops[seed.CropId];double lat=Math.Abs(Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI);
            bool managed=HistoricalTechnologies.Has(state.History.Cultures[culture],HistoricalTechnology.CropRotation);var c=world.Climate;
            var rain=new[]{c.NorthernSpring.PrecipitationMm[cell],c.NorthernSummer.PrecipitationMm[cell],c.NorthernAutumn.PrecipitationMm[cell],c.NorthernWinter.PrecipitationMm[cell]};double season=(rain.Max()-rain.Min())/Math.Max(1.0,rain.Max());
            double fraction=Math.Min(1,Math.Max(.08,D.Clamp((h.AridityMilli-100)/550.0)+access[cell]));
            double Area(HabitatCell habitat)=>HistoricalLandscapeEffects.CropAreaYield(habitat,access[cell],season)*HistoricalDevelopmentEffects.GrowingSeason(habitat);
            double Environment(HistoricalCultivar cultivar)=>def.DaylengthSensitive?1:HistoricalEnvironmentalEffects.LatitudeTransfer(cultivar.DaylengthLatitudeTenths/10.0,lat,cultivar.SelectedYears/12000.0,def.Group==4);
            double Fit(HabitatCell habitat,HistoricalCultivar cultivar)=>D.Q(HistoricalCropCatalog.TransferFit(habitat,cultivar,lat,river[cell],managed)*HistoricalCropEcology.ReproductiveFit(habitat,cultivar)*Environment(cultivar),1000000);
            double WheatLandscape(HabitatCell habitat,HistoricalCultivar cultivar)=>D.Q(D.Clamp(D.Clamp(Area(habitat)/fraction)*Fit(habitat,cultivar)),1000000);
            Check(!HistoricalLandscapeEffects.EverWet(h),"Reviewed French/Iberian field is not on the tropical ever-wet husbandry exception.");
            Check(Fit(h,seed)==(double)Call("CommunityTransferFit",state,cell,seed,managed),"Independent physiological/transfer factors reproduce the literal actual-cell wheat fit.");
            var retuned=seed.Copy();retuned.DaylengthLatitudeTenths=(int)Math.Round(lat*10);
            var noWaterlog=Copy(h);noWaterlog.WaterTableCm=2000;noWaterlog.FloodFractionPermille=0;noWaterlog.FloodDaysPerYear=0;noWaterlog.FloodPulsesPerYear=0;
            Check(Fit(noWaterlog,seed)==Fit(h,seed)&&Area(noWaterlog)==Area(h),"Changing only recorded water table and flood extent/duration leaves these active crop functions exactly unchanged.");
            var noPeat=Copy(h);if(noPeat.Biome==Biome.Peatland)noPeat.Biome=Biome.Grassland;if(noPeat.Soil==SoilKind.Organic)noPeat.Soil=SoilKind.Loam;
            Check(Fit(noPeat,seed)==Fit(h,seed)&&Area(noPeat)==Area(h),"Organic/peat labels alone carry no hidden crop penalty when continuous fertility and cover inputs are held fixed.");
            var cleared=Copy(h);cleared.Habitat=(cleared.Habitat&~Habitat.Forest)|Habitat.Open;
            var fertility=Copy(h);fertility.SoilFertilityPermille=700;
            double physical=HistoricalCrops.AbioticPotential(h,river[cell]);double suitability=HistoricalCrops.Suitability(h,group,river[cell]);
            rows.Add(new{Cell=cell,Latitude=lat,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,ActualBiome=original.Biome.ToString(),ActualSoil=original.Soil.ToString(),ActualCover=original.Habitat.ToString(),original.WaterTableCm,original.FloodFractionPermille,original.FloodDaysPerYear,original.SoilFertilityPermille,original.GrowingDays,original.ReliefM,
                SeasonalTemperatures=new[]{c.NorthernSpring.TemperatureTenthsC[cell],c.NorthernSummer.TemperatureTenthsC[cell],c.NorthernAutumn.TemperatureTenthsC[cell],c.NorthernWinter.TemperatureTenthsC[cell]},CerealWindowWarmthTenths=h.WarmTenthsC,PeakWarmthTenths=original.WarmTenthsC,original.MeanTenthsC,
                RiverCropSites=river[cell],RiverAreaAccess=access[cell],NormalizedCultivableFraction=fraction,
                AreaHeat=D.Clamp((h.WarmTenthsC-80)/120.0)*D.Clamp(h.GrowingDays/180.0)*(1-h.PermafrostPermille/1000.0),AreaSlope=D.Clamp(1-h.ReliefM/1800.0),AreaSoil=D.Clamp(h.SoilFertilityPermille/700.0),AreaClearing=(h.Habitat&Habitat.Forest)!=0?.70:1,AreaYield=Area(h),AreaQuality=D.Clamp(Area(h)/fraction),
                CultivarMaturation=D.Clamp((h.GrowingDays-seed.MaturityDays+50)/70.0),CultivarWarmth=D.Clamp((h.WarmTenthsC/10.0-seed.MinimumWarmTenthsC/10.0+3)/8.0),CultivarWater=D.Clamp(h.AridityMilli/(double)seed.WaterRequirementPermille),GrainSystemSuitability=suitability,AbioticPotential=physical,TransferSystem=Math.Min(1.2,suitability/Math.Max(.01,physical)),TransferPhysicalGate=D.Clamp(physical/.2),TransferClearing=(h.Habitat&Habitat.Forest)!=0?.85:1,LeachingPenalty=HistoricalCropCatalog.LeachingPenalty(h,managed),ReproductiveFactor=HistoricalCropEcology.ReproductiveFit(h,seed),EnvironmentalLatitude=Environment(seed),
                CurrentWheatFit=Fit(h,seed),RetunedWheatFit=Fit(h,retuned),CurrentWheatLandscape=WheatLandscape(h,seed),ActualPackageLandscape=(double)Call("LivingCropLandscape",state,culture,cell,false),RetunedWheatLandscape=WheatLandscape(h,retuned),
                NoWaterTableFloodFieldPenaltyWheatLandscape=WheatLandscape(noWaterlog,retuned),NoPeatLabelPenaltyWheatLandscape=WheatLandscape(noPeat,retuned),ClearedCoverCurrentWheatLandscape=WheatLandscape(cleared,seed),ClearedCoverRetunedWheatLandscape=WheatLandscape(cleared,retuned),Fertility700RetunedWheatLandscape=WheatLandscape(fertility,retuned)});
        }
        Check(source.ComputeHash()==hash,"Read-only field-factor counterfactuals do not alter the saved terrain, habitat, agriculture, population or history.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(input),Hash=hash,Year=source.Years,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Prehistory).Assembly.Location))),Checks=checks,Fields=rows,Notes=new[]{"Only three exact recorded sites and one actually carried wheat lineage are examined; no geographical/cultural census or new farming is performed.","Retuned latitude, removed forest cover and changed soil/water fields are diagnostic counterfactuals only, not legal historical seed/land-management changes.","The flood/groundwater control retains actual river access and supply; it isolates direct waterlogging effects, not water availability.","A small surface-flood fraction does not establish a well-aerated root zone. The actual69cm peatland water table can matter biologically even though this active model does not simulate root-zone oxygen.","The France target176034 and peatland departure175424 are different cells; peatland cannot explain the target's separate.09 ideal-retuned normalized yield."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Crop field audit:3 exact fields, "+checks.Count+" causal/integrity checks PASS, source unchanged "+hash);
    }
}

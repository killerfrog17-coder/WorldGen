using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline exact-field controls with the actual saved source seed inventory.
public static class HistoryTemperateFieldAudit
{
    public static void Run(string input,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new field audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(input));var original=world.Prehistory;string hash=original.ComputeHash();
        var s=original.Copy();var engine=new Prehistory(world);engine.Validate(original);var a=s.History.Aggregates;
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values)!;
        Call("PrepareAggregateRelations",s);
        double[] Field(string name)=>(double[])typeof(Prehistory).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        var rivers=Field("riverSites");var access=Field("riverAccess");
        HabitatCell Copy(HabitatCell h)=>(HabitatCell)typeof(object).GetMethod("MemberwiseClone",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(h,null)!;
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        var rows=new List<object>();var checks=new List<object>();
        void Check(bool pass,string message){checks.Add(new{Pass=pass,Message=message});if(!pass)throw new Exception(message);}
        foreach(var site in new[]{(Name:"France target",From:175424,Cell:176034),(Name:"French departure peatland",From:175424,Cell:175424),(Name:"Iberian comparison",From:163381,Cell:163381)})
        {
            int region=engine.CellPatch[site.From],owner=HistoricalCells.Polity(s,site.From),regional=a.Regions[region].Culture;
            int teacher=(int)Call("DevelopmentRegionalTeacher",s,region);if(teacher<0)teacher=a.Living.Regions[region].Teacher;
            var contexts=new List<(string Kind,int Culture)>();
            void Context(string kind,int culture){if(culture>=0&&!contexts.Any(c=>c.Culture==culture))contexts.Add((kind,culture));}
            if(owner>=0)Context("actual source controller",s.History.Polities[owner].CultureId);
            Context("actual source regional culture",regional);Context("teacher memory, not proof of a carrier",teacher);
            var h=world.Ecology.Habitat[site.Cell];var climate=world.Climate;
            double low=new[]{climate.NorthernSpring.PrecipitationMm[site.Cell],climate.NorthernSummer.PrecipitationMm[site.Cell],climate.NorthernAutumn.PrecipitationMm[site.Cell],climate.NorthernWinter.PrecipitationMm[site.Cell]}.Min();
            double high=new[]{climate.NorthernSpring.PrecipitationMm[site.Cell],climate.NorthernSummer.PrecipitationMm[site.Cell],climate.NorthernAutumn.PrecipitationMm[site.Cell],climate.NorthernWinter.PrecipitationMm[site.Cell]}.Max();
            double season=(high-low)/Math.Max(1,high),fraction=Math.Min(1,Math.Max(.08,D.Clamp((h.AridityMilli-100)/550.0)+access[site.Cell]));
            Check(!HistoricalLandscapeEffects.EverWet(h),site.Name+": temperate field does not use tropical ever-wet husbandry exception.");
            var inventories=new List<object>();
            foreach(var context in contexts)
            {
                int culture=context.Culture;var hc=s.History.Cultures[culture];
                var holder=(HistoricalTownSeeds)Call("LocalSeedHolder",s,culture,site.From,-1);
                bool managed=HistoricalTechnologies.Has(hc,HistoricalTechnology.CropRotation);var groups=new List<object>();double reconstructed=0;
                for(int k=0;k<6;k++)
                {
                    var seed=(HistoricalCultivar)Call("LocalSeedProfile",s,culture,k,holder);if(seed==null)continue;
                    int group=seed.CropId==10?6:seed.CropId==12?7:k;var agronomic=(HabitatCell)Call("CommunityAgronomicHabitat",s,site.Cell,group);
                    double environment=(double)Call("EnvironmentalCropTransfer",s,site.Cell,seed);
                    double Fit(HabitatCell habitat)=>D.Q(HistoricalCropCatalog.TransferFit(habitat,seed,Math.Abs(Lat(site.Cell)),rivers[site.Cell],managed)*HistoricalCropEcology.ReproductiveFit(habitat,seed)*environment,1000000);
                    double Area(HabitatCell habitat)=>HistoricalLandscapeEffects.CropAreaYield(habitat,access[site.Cell],season)*HistoricalDevelopmentEffects.GrowingSeason(habitat);
                    double Yield(HabitatCell habitat)=>D.Q(D.Clamp(Area(habitat)/fraction)*Fit(habitat),1000000);
                    double actualFit=(double)Call("CommunityTransferFit",s,site.Cell,seed,managed);
                    Check(Fit(agronomic)==actualFit,site.Name+": actual carried seed fit reproduced for culture"+culture+"/group"+k);
                    var drained=Copy(agronomic);drained.WaterTableCm=2000;drained.FloodFractionPermille=0;drained.FloodDaysPerYear=0;drained.FloodPulsesPerYear=0;
                    var relabeled=Copy(agronomic);if(relabeled.Biome==Biome.Peatland)relabeled.Biome=Biome.Grassland;if(relabeled.Soil==SoilKind.Organic)relabeled.Soil=SoilKind.Loam;
                    Check(Yield(drained)==Yield(agronomic),"Water-table/flood-record-only control leaves actual crop functions unchanged.");
                    Check(Yield(relabeled)==Yield(agronomic),"Peat/organic-label-only control leaves continuous field inputs and crop functions unchanged.");
                    var cleared=Copy(agronomic);cleared.Habitat=(cleared.Habitat&~Habitat.Forest)|Habitat.Open;
                    reconstructed=Math.Max(reconstructed,Yield(agronomic));
                    groups.Add(new{Group=k,Name=HistoricalCropCatalog.Crops[seed.CropId].Name,Seed=seed,HolderTown=holder?.TownId?? -1,
                        WindowWarmth=agronomic.WarmTenthsC,PeakWarmth=h.WarmTenthsC,Area=Area(agronomic),Quality=D.Clamp(Area(agronomic)/fraction),ActualFit=actualFit,
                        ActualGroupYield=Yield(agronomic),WaterRecordOnlyYield=Yield(drained),PeatLabelOnlyYield=Yield(relabeled),ClearedForestSensitivity=Yield(cleared)});
                }
                double actual=(double)Call("CarriedCropLandscape",s,culture,site.Cell,false,-1,site.From);
                Check(Math.Abs(reconstructed-actual)<=.0000011,site.Name+": existing source inventory reproduces actual carried package yield.");
                var technology=Enum.GetValues<HistoricalTechnology>().Where(t=>new[]{"Irrigation","Roads","CropRotation","SteamPower","Sanitation","MechanizedAgriculture"}.Contains(t.ToString()))
                    .Select(t=>new{Name=t.ToString(),Learned=HistoricalTechnologies.Has(hc,t),Level=hc.Technical?.Levels[(int)t]??0,Adoption=HistoricalModernEffects.Adoption(hc,t,s.Years)}).ToArray();
                inventories.Add(new{context.Kind,Culture=culture,Name=s.Society.Cultures[culture].Name,Farming=s.Society.Cultures[culture].Farming,
                    SeedHolderTown=holder?.TownId?? -1,SeedHolderField=holder?.FieldCell?? -1,GlobalFallback=holder==null,ActualCarriedYield=actual,Technology=technology,Groups=groups});
            }
            rows.Add(new{site.Name,DepartureCell=site.From,TargetCell=site.Cell,Latitude=Lat(site.Cell),Longitude=Lon(site.Cell),SourceOwner=owner,SourceRegionCulture=regional,SourceTeacher=teacher,
                ActualHabitat=h,ActualBiome=h.Biome.ToString(),ActualSoil=h.Soil.ToString(),ActualCover=h.Habitat.ToString(),CultivableFraction=fraction,RiverAreaAccess=access[site.Cell],RiverCropSites=rivers[site.Cell],Inventories=inventories});
        }
        Check(original.ComputeHash()==hash,"Field controls preserve the published original world/state hash.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(input),Year=s.Years,Hash=hash,Realm=a.Empires.Connections.Realms.Version,
            CoreAssembly=typeof(Prehistory).Assembly.Location,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Prehistory).Assembly.Location))),Checks=checks,Fields=rows,
            Notes=new[]{"Only exact accepted-terrain fields and current source controller/regional/teacher inventories are inspected. Teacher memory is not proof of physical seed carriers; no old G culture51 assumption is made.",
                "Holder-null culture-global fallback is explicitly reported: this is an existing source14 granularity limitation, not a newly invented local seed supply.",
                "Water-record-only control retains actual river supply/access and flood regime; it isolates direct waterlogging fields, not water availability. Small flood fraction does not prove a biologically aerated root zone.",
                "Removing forest cover or peat labels is diagnostic sensitivity only, not a paid historical clearance/drainage action or a proposal to waive physical thresholds.",
                "Technology inventories do not prove local drainage works. Current field functions use actual cover, fertility, thermal/crop factors and the managed CropRotation flag; no new engineering mechanism is applied."}
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Temperate field audit "+s.Years+": "+checks.Count+" source-inventory/field-control checks PASS; preserved "+hash);
    }
}

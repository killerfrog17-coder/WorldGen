using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit saved-state comparison. Hypothetical seed substitutions are made
// only on a detached diagnostic branch; no new adoption is applied to history.
public static class HistoryAgronomyLineageAudit
{
    sealed record Site(string Label,int Departure,int Cell,int Culture,int Owner,int Route);
    public static void Run(string input,string frontiers,string output)
    {
        if(File.Exists(output))throw new ArgumentException("Use a new agronomy audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(input));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string hash=source.ComputeHash();
        var s=source.Copy();var h=s.History;var a=h.Aggregates;
        object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args);
        Call("PrepareAggregateRelations",s);long reads=a.CellReads;
        double[] river=(double[])typeof(Prehistory).GetField("riverSites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        object Place(int cell)=>new{Cell=cell,Latitude=Lat(cell),Longitude=Lon(cell),Landmass=world.Landmass[cell],Ocean=world.Hydrology.OceanConnected[cell],Climate=world.Climate.Classification[cell].ToString(),Biome=world.Ecology.Habitat[cell].Biome.ToString(),Habitat=world.Ecology.Habitat[cell],River=river[cell],SeasonalWarmth=new[]{world.Climate.NorthernSpring.TemperatureTenthsC[cell],world.Climate.NorthernSummer.TemperatureTenthsC[cell],world.Climate.NorthernAutumn.TemperatureTenthsC[cell],world.Climate.NorthernWinter.TemperatureTenthsC[cell]}};
        double Distance(int cell,double lat,double lon){double dy=Lat(cell)-lat,dx=(Lon(cell)-lon)*Math.Cos(lat*Math.PI/180);return dx*dx+dy*dy;}
        using var document=JsonDocument.Parse(File.ReadAllText(frontiers));
        if(document.RootElement.GetProperty("Hash").GetString()!=hash)throw new ArgumentException("Frontier and world checkpoints must match exactly.");
        var edges=document.RootElement.GetProperty("AnonymousFrontiers").EnumerateArray().Select(e=>new Site("recorded anonymous farmer frontier",e.GetProperty("DepartureCell").GetInt32(),e.GetProperty("ArrivalCell").GetInt32(),e.GetProperty("SourceCulture").GetInt32(),e.GetProperty("SourceOwner").GetInt32(),e.GetProperty("Route").GetInt32())).ToArray();
        var sites=new List<Site>();
        void Nearby(string label,double lat,double lon,int count,double radius)
        {foreach(var site in edges.Where(e=>Distance(e.Cell,lat,lon)<=radius*radius).OrderBy(e=>Distance(e.Cell,lat,lon)).ThenBy(e=>e.Route).Take(count))if(!sites.Any(e=>e.Cell==site.Cell&&e.Culture==site.Culture))sites.Add(site with{Label=label});}
        var exact=edges.Single(e=>e.Route==12479&&e.Cell==176034);sites.Add(exact with{Label="Exact critic Europe49.461N1.811E case"});
        sites.Add(exact with{Label="Exact European departing border",Cell=exact.Departure,Route=-1});
        int capital=s.Society.Settlements[h.Polities[exact.Owner].CapitalId].Cell;
        sites.Add(exact with{Label="Exact European source court capital",Departure=capital,Cell=capital,Route=-1});
        Nearby("Europe southern comparison",43,12,2,20);Nearby("West Asia comparison",35,42,3,20);
        Nearby("India northwest",29,74,2,10);Nearby("India central",23,78,2,10);Nearby("India southern",11,78,2,8);
        foreach(var focus in new[]{("West Asia actual occupied farmer",35.0,42.0),("India actual occupied farmer",23.0,78.0)})
        {
            var town=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0&&s.Society.Cultures[t.CultureId].Farming>=250&&Distance(t.Cell,focus.Item2,focus.Item3)<225).OrderBy(t=>Distance(t.Cell,focus.Item2,focus.Item3)).ThenBy(t=>t.Id).FirstOrDefault();
            if(town!=null&&!sites.Any(e=>e.Cell==town.Cell&&e.Culture==town.CultureId))sites.Add(new Site(focus.Item1,town.Cell,town.Cell,town.CultureId,a.Resolve(town.PolityId,h),-1));
        }
        var rows=new List<object>();var checks=new List<object>();
        void Check(bool pass,string message){checks.Add(new{Pass=pass,Message=message});if(!pass)throw new Exception(message);}
        foreach(var site in sites)
        {
            var culture=s.Society.Cultures[site.Culture];var crop=h.Agriculture[site.Culture];int mask=(int)Call("CommunityNativeCropMask",s,site.Cell);bool managed=HistoricalTechnologies.Has(h.Cultures[site.Culture],HistoricalTechnology.CropRotation);
            double Fit(HistoricalCultivar cultivar)=>(double)Call("CommunityTransferFit",s,site.Cell,cultivar,managed);
            object Traits(HistoricalCultivar cultivar)=>cultivar==null?null:new{cultivar.CropId,Name=HistoricalCropCatalog.Crops[cultivar.CropId].Name,cultivar.OriginCulture,cultivar.ParentCulture,cultivar.SelectedYears,cultivar.MaturityDays,cultivar.MinimumWarmTenthsC,cultivar.DaylengthLatitudeTenths,cultivar.WaterRequirementPermille,cultivar.YieldPermille};
            double LandscapeWith(int group,HistoricalCultivar cultivar)
            {
                var old=crop.Cultivars[group];crop.Cultivars[group]=cultivar;
                try{return(double)Call("LivingCropLandscape",s,site.Culture,site.Cell,false);}finally{crop.Cultivars[group]=old;}
            }
            var groups=new List<object>();
            for(int group=0;group<6;group++)
            {
                var carried=crop.Cultivars[group];bool known=crop.AdoptedYear[group]>0;
                var local=(HistoricalCultivar)Call("AggregateCultivar",s,site.Culture,group,site.Cell);
                var latitude=carried?.Copy();if(latitude!=null)latitude.DaylengthLatitudeTenths=(int)Math.Round(Math.Abs(Lat(site.Cell))*10);
                var extended=carried?.Copy();if(extended!=null)extended.SelectedYears=20000;
                var combined=extended?.Copy();if(combined!=null)combined.DaylengthLatitudeTenths=(int)Math.Round(Math.Abs(Lat(site.Cell))*10);
                int donor=(int)Call("DevelopmentCropDonor",s,site.Culture,group,site.Cell,-1);
                var memory=a.Provinces.Exchanges[site.Culture];
                var offers=culture.Contacts.Take(16).Where(peer=>peer!=site.Culture&&h.Agriculture[peer].AdoptedYear[group]>0&&h.Agriculture[peer].Cultivars[group]!=null).Select(peer=>
                {
                    int at=Array.IndexOf(memory.Peers,peer),receipt=at>=0?memory.LastYear[at]:-1;int anchor=(int)Call("EnvironmentalContactAnchor",s,peer),receiver=(int)Call("EnvironmentalContactAnchor",s,site.Culture);
                    return new{Culture=peer,Name=s.Society.Cultures[peer].Name,Adopted=h.Agriculture[peer].AdoptedYear[group]-1,Receipt=receipt,Recent=receipt>=s.Years-250,AdoptionBeforeReceipt=h.Agriculture[peer].AdoptedYear[group]-1<=receipt,Fit=Fit(h.Agriculture[peer].Cultivars[group]),CurrentAnchor=anchor,CurrentReceiverAnchor=receiver,CurrentAnchorRouteAllowed=(bool)Call("InstitutionalRouteAllowed",s,anchor,receiver,peer)};
                }).ToArray();
                int urban=culture.OriginCell;var towns=a.Cultures[site.Culture].Cities;if(towns.Count>0)urban=s.Society.Settlements[towns.At((int)((double)Call("HistoryRoll","aggregate-culture-city",site.Culture,s.Years)*towns.Count))].Cell;
                int active=known&&carried!=null?(int)Call("ActiveCommunityCropYears",s,site.Culture,group,100,urban):0;
                var physical=carried==null?null:(HabitatCell)Call("CommunityAgronomicHabitat",s,site.Cell,carried.CropId==10?6:carried.CropId==12?7:group);
                double catalog=carried==null?0:HistoricalCropCatalog.TransferFit(physical,carried,Math.Abs(Lat(site.Cell)),river[site.Cell],managed);
                if(site.Route==12479&&group==0)
                {
                    double cached=Fit(carried);engine.UseAgronomicFitCache=false;double literal=Fit(carried);engine.UseAgronomicFitCache=true;
                    Check(cached==literal,"Exact European wheat fit agrees with the literal uncached production path.");
                    Check(Math.Abs(cached-.102325)<.0000001,"Exact route/culture/target reproduces the critic's frozen G carried-wheat observation.");
                    Check(known&&carried.SelectedYears==4000&&active>0&&Math.Min(active,Math.Max(0,4000-carried.SelectedYears))==0,"Actually viable current cultivation has positive selection effort but is stopped by the active4000-year cap.");
                    Check(donor<0&&offers.All(o=>!o.Recent||!o.AdoptionBeforeReceipt||o.Fit<=.12),"The actual donor selector rejects every insufficient-fit offer rather than suppressing an eligible native fallback.");
                    Check((mask&(1<<group))==0&&Fit(local)>cached,"The numerically better local representative is not an available native crop group at the exact frontier.");
                    Check(LandscapeWith(group,latitude)<.12&&LandscapeWith(group,combined)<.12,"Even idealized latitude retuning and extended selection fail the separate physical landscape-yield migration gate.");
                }
                groups.Add(new{Group=group,Name=HistoricalCrops.Names[group],Known=known,AdoptedYear=crop.AdoptedYear[group]-1,SourceCulture=crop.SourceCulture[group],Practice=crop.Practice[group],HusbandryYears=crop.HusbandryYears[group],PracticeSite=Place(crop.PracticeCell[group]),NativeObservedForCulture=(crop.NativeMask&(1<<group))!=0,NativeAtTarget=(mask&(1<<group))!=0,WildOrigin=crop.WildOriginCell[group],Carried=Traits(carried),CarriedFit=Fit(carried),CarriedCatalogTransferFit=catalog,ReproductiveFactor=physical==null?0:HistoricalCropEcology.ReproductiveFit(physical,carried),EnvironmentalLatitudeFactor=carried==null?0:(double)Call("EnvironmentalCropTransfer",s,site.Cell,carried),LocalRepresentative=Traits(local),LocalRepresentativeFit=Fit(local),LocalRepresentativeLandscape=known&&local!=null?LandscapeWith(group,local):0,LocalDaylengthOnlyFit=Fit(latitude),LocalDaylengthOnlyLandscape=known&&latitude!=null?LandscapeWith(group,latitude):0,Selection20000OnlyFit=Fit(extended),Selection20000OnlyLandscape=known&&extended!=null?LandscapeWith(group,extended):0,Selection20000AndLocalDaylengthFit=Fit(combined),Selection20000AndLocalDaylengthLandscape=known&&combined!=null?LandscapeWith(group,combined):0,CurrentActiveYearsPer100=active,ExpectedActualSelectedDeltaPer100=carried==null?0:Math.Min(active,Math.Max(0,4000-carried.SelectedYears)),ActualUnadoptedDonor=donor,DonorOffers=offers,ActualAlreadyAdoptedBranch=known?"Selection maintenance then continue: no donor/native variety comparison":"Eligible donor fit>.12, else native-mask/local representative; fit gate applies"});
            }
            var account=a.Polities.Length>site.Owner&&site.Owner>=0?a.Polities[site.Owner]:null;
            rows.Add(new{site.Label,site.Route,Departure=Place(site.Departure),Target=Place(site.Cell),site.Culture,CultureName=culture.Name,culture.Farming,culture.FoundedYear,CultureOrigin=Place(culture.OriginCell),site.Owner,CourtName=site.Owner>=0?h.Polities[site.Owner].Name:null,CourtCapital=site.Owner>=0?Place(s.Society.Settlements[h.Polities[site.Owner].CapitalId].Cell):null,RemoteResidents=account?.Remote??0,ActualPhysicalRoute=(bool)Call("InstitutionalRouteAllowed",s,site.Departure,site.Cell,site.Culture),CurrentLandscape=(double)Call("LivingCropLandscape",s,site.Culture,site.Cell,false),CurrentAggregateFit=(double)Call("AggregateCropSupport",s,site.Culture,site.Cell,false),NativeMaskAtTarget=mask,Managed=managed,Groups=groups});
        }
        a.CellReads=reads;Check(source.ComputeHash()==hash,"Cultivar sensitivity comparisons preserve the complete source historical hash.");
        string core=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(input),FrontierSource=Path.GetFullPath(frontiers),Year=s.Years,Hash=hash,Rules=a.Empires.Connections.Realms.Version,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core))),Checks=checks,Samples=rows,Notes=new[]{"Read-only saved checkpoint; cultivar alternatives exist only on a detached diagnostic branch and grant no historical adoption, people, food or production.","Current active path caps SelectedYears at4000 and never changes daylength latitude; legacy SelectLocalCrops has different20000-year/daylength behavior but is not dispatched for aggregate history.","NativeAtTarget is a procedural crop-GROUP opportunity, not a species inventory proving that a particular named wild plant exists there. A best local representative with no native group cannot be assumed available.","LocalDaylengthOnly and Selection20000 are sensitivity substitutions, not simulated breeding. They leave seasonal climate, soil, forest, salinity, cold, reproductive and water penalties enabled. Landscape substitutions retain current actual group-adoption flags.","Recent donor receipt and adoption-before-receipt are saved facts. Current physical anchors are not retained historical contact endpoints; a blocked current route does not invalidate a real historical delivery.","The exact Europe case is critic route12479/culture51/departure175424/arrival176034. Comparison cases are bounded nearest recorded frontiers and actual occupied farmer towns; absence of a sampled edge does not mean absence of inhabitants."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Agronomy lineage audit: {rows.Count} bounded actual sites; source unchanged {hash}");
    }
}

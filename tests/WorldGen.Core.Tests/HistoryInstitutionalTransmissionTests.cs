using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryInstitutionalTransmissionTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(0,0,0)==35,"Primitive dispersal is not a dependable long-range farm-package trade connection");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,0,0)==120,"Sailing alone cannot establish routine transoceanic institutional contact");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,1,0,1)==350,"Initial traditional navigation can link islands and short open-water gaps");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,1,0,8)==800,"Developed pre-ocean navigation remains distinct from ocean-going navigation");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,1,1)==14000,"Ocean-going craft can deliver knowledge across oceans without an arbitrary continent ban");
        double full=HistoricalInstitutionalTransmission.RelayExposure(1000,1,100),second=HistoricalInstitutionalTransmission.RelayExposure(full,1,100);
        check(full==200&&second==110,"An actually contacted novice teacher transmits less per visit, without imposing a permanent exponentially shrinking learning ceiling");
        check(HistoricalInstitutionalTransmission.RelayExposure(1000,.01,100)==2,"Forest, latitude and route resistance constrain civic transmission as well as ordinary contact");
        check(HistoricalInstitutionalTransmission.RelayExposure(1000,1,50)*2==full,"Exposure delivery scales with elapsed opportunities rather than UI publication frequency");
        check(HistoricalInstitutionalTransmission.RegularOceanDelivery(4000,4,0)<.01,"Oral ocean voyaging is not automatically frequent transoceanic institutional trade");
        check(HistoricalInstitutionalTransmission.RegularOceanDelivery(4000,4,2)>.7,"Developed ocean infrastructure can connect the same ocean without a permanent historical blockade");
        var corridor=new double[8];corridor[0]=1000;
        for(int century=0;century<30;century++)
        {
            var previous=(double[])corridor.Clone();for(int hop=1;hop<corridor.Length;hop++)corridor[hop]=Math.Min(1000,corridor[hop]+HistoricalInstitutionalTransmission.RelayExposure(previous[hop-1],.7,100));
        }
        check(corridor[7]>500,"Repeated actual deliveries propagate learned institutions across a multi-hop inhabited corridor rather than stopping two regions from the first court");
        var blocked=new double[8];blocked[0]=1000;
        for(int century=0;century<30;century++)
        {var previous=(double[])blocked.Clone();for(int hop=1;hop<blocked.Length;hop++)blocked[hop]=Math.Min(1000,blocked[hop]+HistoricalInstitutionalTransmission.RelayExposure(previous[hop-1],hop==3?0:.7,100));}
        check(blocked[7]==0,"No actual delivery through a closed jungle, desert or unsupported-ocean route means no civic transmission beyond it");
        check(HistoricalInstitutionalTransmission.RetainedExposure(500,100,true,true)==500&&HistoricalInstitutionalTransmission.RetainedExposure(500,100,false,false)<500,"Practiced inhabited communities retain local lessons while abandoned corridors forget");
        check(HistoricalInstitutionalTransmission.LocalLearning(22,.8,10)>1,"Modest local exposure can teach a town cumulatively instead of being discarded below an arbitrary150-point gate");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var before=world.Prehistory;string original=before.ComputeHash();
        var s=engine.UpgradeToLivingWorldHistory(before);engine.Validate(s);
        check(s.History.Version==Prehistory.LivingWorldHistoryVersion&&before.ComputeHash()==original,"Physical civic-delivery rules require explicit upgrade and preserve the old checkpoint");
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values)!;
        int[] shores=Enumerable.Range(0,world.Surface.Cells.Length).Where(c=>!world.Hydrology.OceanConnected[c]&&(bool)Call("DiffusionShore",c)).ToArray();
        int from=-1,to=-1;double best=0;
        // Explicit diagnostics only. Recurring model events never enumerate
        // these cells, shores or holdings.
        for(int i=0;i<Math.Min(64,shores.Length);i++)for(int j=0;j<Math.Min(64,shores.Length);j++)
        {
            int first=shores[i*shores.Length/Math.Min(64,shores.Length)],second=shores[j*shores.Length/Math.Min(64,shores.Length)];
            double km=D.Angle(world.Surface.Cells[first].Position,world.Surface.Cells[second].Position)*world.Recipe.RadiusKm;
            if(km<1800||km>9000||!(bool)Call("DiffusionSeaLane",first,second))continue;
            object[] values={first,second,false};double open=(double)Call("InstitutionalUnsupportedWaterKm",values);
            if(open>best){best=open;from=first;to=second;}
        }
        check(from>=0&&best>1500,"Control contains a genuine unsupported open-ocean passage rather than assuming continents imply isolation");
        int culture=0;var hc=s.History.Cultures[culture];
        foreach(var technology in new[]{HistoricalTechnology.Sailing,HistoricalTechnology.Navigation})
        {int k=(int)technology;hc.Technology[k]=1;hc.Technical.Levels[k]=1;hc.Technical.Deployment[k]=1000;hc.Technical.LevelYear[k]=0;}
        int ocean=(int)HistoricalTechnology.OceanNavigation;hc.Technology[ocean]=0;hc.Technical.Levels[ocean]=0;hc.Technical.Deployment[ocean]=0;
        check(!(bool)Call("InstitutionalRouteAllowed",s,from,to,culture),"An actually unsupported ocean passage cannot teach state institutions with coastal navigation alone");
        hc.Technology[ocean]=1;hc.Technical.Levels[ocean]=8;hc.Technical.Deployment[ocean]=1000;hc.Technical.LevelYear[ocean]=0;
        check((bool)Call("InstitutionalRouteAllowed",s,from,to,culture),"The same passage becomes eligible when ocean-going craft actually exist");
        object[] forward={from,to,false},reverse={to,from,false};double firstOpen=(double)Call("InstitutionalUnsupportedWaterKm",forward),reverseOpen=(double)Call("InstitutionalUnsupportedWaterKm",reverse);
        check(firstOpen==reverseOpen&&(bool)forward[2]==(bool)reverse[2],"Geometry-only route cache has canonical direction and cannot change reverse delivery");
        var cropProbe=engine.UpgradeToLivingWorldHistory(before).Copy();int receiver=-1,donor=-1,group=-1,site=-1;
        for(int r=0;r<Math.Min(12,cropProbe.Society.Cultures.Length)&&donor<0;r++)
        for(int d=0;d<Math.Min(12,cropProbe.Society.Cultures.Length)&&donor<0;d++)
        for(int g=0;g<6;g++)
        {
            if(r==d||cropProbe.History.Agriculture[d].Cultivars[g]==null)continue;int cell=cropProbe.History.Agriculture[r].PracticeCell[g];
            double fit=(double)Call("CommunityTransferFit",cropProbe,cell,cropProbe.History.Agriculture[d].Cultivars[g],false);
            if(fit<=.12)continue;receiver=r;donor=d;group=g;site=cell;break;
        }
        check(donor>=0,"Crop-delivery control has an agronomically suitable donor, not a habitat-induced false rejection");
        cropProbe.Society.Cultures[receiver].Contacts=new[]{donor};
        var memory=cropProbe.History.Aggregates.Provinces.Exchanges[receiver];memory.Peers=new[]{donor};memory.LastYear=new[]{cropProbe.Years-100};memory.LastDirectYear=new[]{cropProbe.Years-100};memory.Capabilities=new[]{long.MaxValue};
        cropProbe.History.Agriculture[donor].AdoptedYear[group]=cropProbe.Years-10;
        check((int)Call("DevelopmentCropDonor",cropProbe,receiver,group,site,donor)==-1,"A remembered donor cannot supply a crop domesticated after the actual delivery");
        memory.LastYear[0]=cropProbe.Years;
        check((int)Call("DevelopmentCropDonor",cropProbe,receiver,group,site,donor)==donor,"A fresh delivery can introduce the same suitable crop rather than blocking diffusion categorically");
        var lesson=engine.UpgradeToLivingWorldHistory(before).Copy();int candidate=-1,lessonTeacher=-1;var aggregates=lesson.History.Aggregates;
        for(int id=0;id<lesson.Society.Settlements.Length;id++)
        {
            var town=lesson.Society.Settlements[id];if(aggregates.Living.Cities[id].AbandonedYear>=0||town.Population<1200||town.FoodReserve<town.Population*.3||lesson.Society.Cultures[town.CultureId].Farming<180)continue;
            double fiscal=(double)Call("LivingCropLandscape",lesson,town.CultureId,town.Cell,true);
            int localCourt=(int)Call("ProvincialRegionalCourt",lesson,engine.CellPatch[town.Cell]);int teacher=localCourt>=0?localCourt:0;
            double physical=(double)Call("InstitutionalLocalExample",lesson,teacher,aggregates.Regions[engine.CellPatch[town.Cell]].BestCell,town.Cell);
            if(fiscal>=.10&&physical>0&&aggregates.Living.Cities[id].DeclineYears==0){candidate=id;lessonTeacher=teacher;break;}
        }
        check(candidate>=0,"Local civic-learning control has a supplied physically connected farming town, not a mock climate-free site");
        var learner=lesson.Society.Settlements[candidate];int district=engine.CellPatch[learner.Cell];
        learner.PolityId=-1;aggregates.Cities[candidate].Neighbors=Array.Empty<int>();aggregates.Cities[candidate].LastPoliticalYear=lesson.Years-10;
        aggregates.Cells=aggregates.Cells.With(learner.Cell*5+1,-1);aggregates.Living.CivicOrigins=aggregates.Living.CivicOrigins.With(learner.CultureId,-1);
        aggregates.Regions[district].Culture=learner.CultureId;var local=aggregates.Living.Regions[district];local.CivicTeacher=lessonTeacher;local.CivicExposure=22;local.LastExchangeYear=lesson.Years;
        lesson.History.Towns[candidate].CivicTeacherPolityId=-1;lesson.History.Towns[candidate].CivicExampleExposure=0;
        for(int opportunity=0;opportunity<20&&lesson.History.Towns[candidate].CivicExampleExposure==0;opportunity++)
        {aggregates.Cities[candidate].LastPoliticalYear=lesson.Years-10;Call("TryLivingInstitutions",lesson,candidate);lesson.Years+=10;}
        check(lesson.History.Towns[candidate].CivicExampleExposure>0,"Actual town handler learns from a modest22-point local delivered lesson instead of discarding it below150");
        var shipping=engine.UpgradeToLivingWorldHistory(before).Copy();var accounts=shipping.History.Aggregates;
        var provisioner=shipping.Society.Settlements.First(t=>accounts.Resolve(t.PolityId,shipping.History)>=0&&accounts.Cities[t.Id].Supported&&t.Population>=500&&(bool)Call("DiffusionShore",t.Cell));
        int state=accounts.Resolve(provisioner.PolityId,shipping.History),harbor=provisioner.Cell,providerCulture=shipping.History.Polities[state].CultureId;
        var shippingCulture=shipping.History.Cultures[providerCulture];
        for(int k=0;k<HistoricalTechnicalGraph.LegacyCount;k++){shippingCulture.Technology[k]=1;shippingCulture.Technical.Levels[k]=10;shippingCulture.Technical.LevelYear[k]=0;shippingCulture.Technical.Deployment[k]=1000;}
        shippingCulture.Institution=1000;
        int inland=Enumerable.Range(0,world.Surface.Cells.Length).Where(cell=>world.Landmass[cell]==world.Landmass[harbor]&&!(bool)Call("DiffusionShore",cell)&&engine.CellPatch[cell]>=0)
            .OrderBy(cell=>D.Angle(world.Surface.Cells[harbor].Position,world.Surface.Cells[cell].Position)).First();
        // Controlled coarse-cell fixture: an actual supported city provisions
        // its owned serviced coastal office rather than occupying the shore cell.
        provisioner.Cell=inland;accounts.Empires.Territories.Sources=accounts.Empires.Territories.Sources.With(harbor,provisioner.Id);
        var suppliedCities=accounts.Polities[state].Cities;for(int k=0;k<suppliedCities.Count;k++)shipping.Society.Settlements[suppliedCities.At(k)].FoodReserve=0;
        provisioner.FoodReserve=10000000;
        var cargoRoute=new Prehistory.Route{A=engine.CellPatch[harbor],B=engine.CellPatch[harbor],CellA=harbor,CellB=harbor,DistanceM=200000,Sea=true};
        object[] shippingArgs={shipping,state,cargoRoute,provisioner.Id,-1,0L};
        check((bool)Call("MaritimeCharterLogistics",shippingArgs)&&(int)shippingArgs[4]==provisioner.Id,"Actual owned coastal office has a finite inland supply leg from a supported city");
        long cargo=provisioner.FoodReserve,cost=(long)shippingArgs[5];
        check((bool)Call("InstitutionalVoyageDelivery",shipping,cargoRoute,providerCulture,state,-1)&&provisioner.FoodReserve==cargo-cost,"Institutional delivery reuses the real serviced harbor and charges the selected inland city's exact provisions");
        check(!(bool)Call("InstitutionalVoyageDelivery",shipping,cargoRoute,providerCulture,-1,provisioner.Id),"An unowned inland forager town cannot obtain the state's serviced-harbor exemption just by being passed as a preferred port");
        provisioner.FoodReserve=0;
        check(!(bool)Call("InstitutionalVoyageDelivery",shipping,cargoRoute,providerCulture,state,-1),"An owned coastal office without an actually provisioned city cannot deliver institutional cargo");
        var next=engine.AdvanceYears(engine.UpgradeToLivingWorldHistory(before),20);var split=engine.AdvanceYears(engine.AdvanceYears(engine.UpgradeToLivingWorldHistory(before),7),13);
        check(next.Hash==split.Hash,"Physical transmission and local civic exposure are invariant to publication partitioning");
        Console.WriteLine($"Civic ocean control: {best:F1} km unsupported passage, coastal navigation rejected, installed ocean craft accepted.");
    }
}

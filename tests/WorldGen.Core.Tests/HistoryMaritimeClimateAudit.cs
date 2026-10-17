using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static class HistoryMaritimeClimateAudit
{
    public static void Run(string path,string output,int replayYear=-1)
    {
        if(File.Exists(output))throw new ArgumentException("Refusing to overwrite a maritime climate audit.");
        var w=WorldFile.Read(File.ReadAllBytes(path));string input=w.Prehistory.Hash;var e=new Prehistory(w);var s=w.Prehistory;
        if(replayYear>=0){w.Prehistory=null;e=new Prehistory(w);var start=e.Start(e.Prepare());var session=new SimulationSession(e,start);session.AdvanceSeconds(replayYear*SimulationTime.Year,int.MaxValue);s=session.Snapshot();}
        e.Validate(s);object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);string before=s.ComputeHash();var h=s.History;var a=h.Aggregates;var ports=a.Empires.Connections.ProvisionedPorts;var events=h.Archive.ToArray();var locator=new CellLocator(w.Surface.Cells);
        var seasons=new[]{w.Climate.NorthernSpring,w.Climate.NorthernSummer,w.Climate.NorthernAutumn,w.Climate.NorthernWinter};
        double Lat(int cell)=>Math.Asin(w.Surface.Cells[cell].Position.Z)*180/Math.PI;
        object Endpoint(int cell)
        {
            var p=w.Surface.Cells[cell].Position;var habitat=w.Ecology.Habitat[cell];
            return new{Cell=cell,Latitude=Lat(cell),Longitude=Math.Atan2(p.Y,p.X)*180/Math.PI,ElevationM=w.ElevationM[cell],Ocean=w.Hydrology.OceanConnected[cell],Climate=w.Climate.Classification[cell].ToString(),Biome=habitat.Biome.ToString(),habitat.MeanTenthsC,habitat.WarmTenthsC,habitat.ColdTenthsC,habitat.GrowingDays,habitat.PermafrostPermille,habitat.ReliefM,habitat.AridityMilli,habitat.SoilFertilityPermille,SeasonTenthsC=seasons.Select(q=>q.TemperatureTenthsC[cell]).ToArray(),WindEastMilli=seasons.Select(q=>q.WindEastMilli[cell]).ToArray(),WindNorthMilli=seasons.Select(q=>q.WindNorthMilli[cell]).ToArray()};
        }
        object RouteClimate(int from,int to)
        {
            var start=w.Surface.Cells[from].Position;var end=w.Surface.Cells[to].Position;double angle=D.Angle(start,end),sin=Math.Sin(angle);var samples=new List<int>();var directions=new List<V>();
            for(int k=0;k<64;k++){double t=(k+.5)/64;var point=Math.Abs(sin)<.000001?(start*(1-t)+end*t).Unit:(start*(Math.Sin((1-t)*angle)/sin)+end*(Math.Sin(t*angle)/sin)).Unit;int cell=locator.Nearest(point);if(w.Hydrology.OceanConnected[cell]){samples.Add(cell);directions.Add(V.Tangent(point,end).Unit);}}
            var openArgs=new object[]{from,to,false};double open=(double)Call("InstitutionalUnsupportedWaterKm",openArgs);
            double WindProjection(int quarter,int sample)
            {
                int cell=samples[sample];var p=w.Surface.Cells[cell].Position;var east=new V(-p.Y,p.X,0).Unit;var north=V.Cross(p,east).Unit;
                var wind=east*seasons[quarter].WindEastMilli[cell]+north*seasons[quarter].WindNorthMilli[cell];return V.Dot(wind,directions[sample]);
            }
            return new{From=Endpoint(from),DistanceKm=angle*w.Recipe.RadiusKm,LongestUnsupportedWaterKm=open,CrossesWater=(bool)openArgs[2],OceanSamples=samples.Count,Seasons=Enumerable.Range(0,4).Select(k=>new{Season=k,MeanTenthsC=samples.Count>0?samples.Average(c=>seasons[k].TemperatureTenthsC[c]):0,MinTenthsC=samples.Count>0?samples.Min(c=>seasons[k].TemperatureTenthsC[c]):0,ColdFraction=samples.Count>0?samples.Count(c=>seasons[k].TemperatureTenthsC[c]<=0)/(double)samples.Count:0,FreezingAirFraction=samples.Count>0?samples.Count(c=>seasons[k].TemperatureTenthsC[c]<=-18)/(double)samples.Count:0,MeanRelativeWindMilli=samples.Count>0?samples.Average(c=>Math.Sqrt((double)seasons[k].WindEastMilli[c]*seasons[k].WindEastMilli[c]+(double)seasons[k].WindNorthMilli[c]*seasons[k].WindNorthMilli[c])):0,OutwardAdverseWindMilli=samples.Count>0?Enumerable.Range(0,samples.Count).Average(j=>Math.Max(0,-WindProjection(k,j))):0,ReturnAdverseWindMilli=samples.Count>0?Enumerable.Range(0,samples.Count).Average(j=>Math.Max(0,WindProjection(k,j))):0}).ToArray()};
        }
        object Fleet(int polity,int founded)
        {
            if(polity<0)return new{Unavailable=true};var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];
            return new{p.Id,p.Name,p.EndedYear,p.CultureId,p.Treasury,a.Polities[polity].Receipts,a.Polities[polity].ServiceCost,h.Dynamics.States[polity].PublicWorks,PendingFleetLimit=HistoricalSeaVentureEffects.Fleet(a.Polities[polity].Urban,a.Polities[polity].Receipts-a.Polities[polity].ServiceCost,p.Exhaustion),PendingNonReportMissions=a.Empires.Connections.Geography.Governments[polity].Missions.Count(m=>m.Kind!=HistoricalSeaMission.Report),Capital=Endpoint(s.Society.Settlements[p.CapitalId].Cell),ReturningFleetCapacity=Call("ReturningFleetCapacity",s,polity),LegKm=Call("StateMaritimeLegKm",s,polity),NetworkKm=Call("StateMaritimeNetworkKm",s,polity),Practice=Enumerable.Range(0,hc.Technology.Length).Where(k=>new[]{0,3,5,6,9,10,11,13,14,15,17,18,20,21,28}.Contains(k)).Select(k=>new{Technology=k,Name=HistoricalTechnologies.Names[k],FirstKnown=hc.Technology[k]-1,FirstInstalled=hc.Technical.InstalledYear[k]-1,Level=hc.Technical.Levels[k],UsefulLevel=HistoricalTechnicalGraph.UsefulLevel(k,h.Version),LastLevelYear=hc.Technical.LevelYear[k],Deployment=hc.Technical.Deployment[k],KnownAtFounding=hc.Technology[k]>0&&hc.Technology[k]-1<=founded}).ToArray()};
        }
        var towns=s.Society.Settlements.Where(t=>Lat(t.Cell)<-55).Select(t=>
        {
            int at=ports?.ByTown[t.Id]??-1;var port=at>=0?ports.Ports[at]:null;int originalSupplier=a.Cities[t.Id].ParentCity;
            var birth=events.FirstOrDefault(x=>x.SettlementId==t.Id&&(x.Kind=="provisioned-port"||x.Kind=="sponsored-port"||x.Kind=="coastal-farms"||x.Kind=="settlement"));
            int sponsor=birth?.PolityId??-1;if(sponsor<0){var charter=events.FirstOrDefault(x=>x.Cell==t.Cell&&x.Kind=="overseas-charter"&&Math.Abs(x.Year-t.FoundedYear)<3);sponsor=charter?.PolityId??-1;}
            int source=port?.DepartureCell??(originalSupplier>=0?s.Society.Settlements[originalSupplier].Cell:-1),post=port?.HostPostId??-1;
            var chain=new List<object>();int code=post>=0?-2-post:a.Empires.Territories.Sources[t.Cell];
            for(int depth=0;depth<8&&code!= -1;depth++)
            {
                if(code>=0){var root=s.Society.Settlements[code];chain.Add(new{Kind="Town",Source=code,root.Id,root.Name,root.FoundedYear,root.PolityId,root.Population,root.FoodCapacity,root.FoodReserve,a.Cities[code].Rural,a.Cities[code].RuralCapacity,a.Cities[code].Supported,Provisioned=ports!=null&&ports.ByTown[code]>=0,Location=Endpoint(root.Cell)});break;}
                var office=a.Empires.Territories.Posts[-code-2];chain.Add(new{Kind="Post",Source=code,office.Id,office.Name,office.PolityId,office.FoundedYear,office.ClosedYear,office.ParentSource,office.Maritime,office.LegDistanceM,office.MessageEffortKm,Location=Endpoint(office.Cell)});code=office.ParentSource;
            }
            return new{t.Id,t.Name,t.FoundedYear,t.PolityId,t.CultureId,t.Population,t.FoodReserve,Abandoned=a.Living.Cities[t.Id].AbandonedYear,a.Cities[t.Id].Supported,a.Cities[t.Id].Rural,a.Cities[t.Id].Suburban,Provisioned=port!=null,Port=port,OriginalSupplier=originalSupplier,OriginalSupplierLocation=originalSupplier>=0?Endpoint(s.Society.Settlements[originalSupplier].Cell):null,Location=Endpoint(t.Cell),Route=source>=0?RouteClimate(source,t.Cell):null,FoundingSponsor=sponsor,SponsorAtSavedYear=Fleet(sponsor,t.FoundedYear),CurrentServiceChainKm=sponsor>=0&&source>=0?Call("RevisedMaritimeChainKm",s,sponsor,a.Empires.Territories.Sources[source]):null,CurrentServiceChain=chain,Events=events.Where(x=>x.SettlementId==t.Id||x.Cell==t.Cell&&(x.Kind=="overseas-charter"||x.Kind=="administrative-withdrawal")).Select(x=>new{x.Year,x.Kind,x.PolityId,x.OtherPolityId,x.SettlementId,x.Text}).ToArray()};
        }).ToArray();
        int At(double latitude,double longitude){double lat=latitude*Math.PI/180,lon=longitude*Math.PI/180;return locator.Nearest(new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat)));}
        var comparisons=new[]{("Carthage-Ostia",36.85,10.3,41.73,12.27),("Malta-Alexandria",35.899,14.514,31.2,29.9),("Bergen-Reykjavik",60.39,5.32,64.15,-21.94),("Reykjavik-Julianehaab",64.15,-21.94,60.72,-46.04),("Praia-Recife",14.92,-23.51,-8.05,-34.9)}.Select(x=>new{Name=x.Item1,Warning="Modern coordinates only anchor physically comparable great-circle samples; this is not reconstruction of a historic navigable itinerary.",Route=RouteClimate(At(x.Item2,x.Item3),At(x.Item4,x.Item5))}).ToArray();
        if(s.ComputeHash()!=before)throw new Exception("Maritime climate audit changed the inspected checkpoint.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Input=path,InputHash=input,Replay=replayYear>=0,Year=s.Years,s.Hash,Realm=a.Empires.Connections.Realms.Version,EngineAssembly=typeof(Prehistory).Assembly.Location,Eras=WorldEraView.FirstYears(s),Warning="Fleet/deployment values are current at saved/replayed year, not retroactively reconstructed at founding. Ocean route climate is a64point great-circle diagnostic, not observed storms or a sea-ice simulation; winds are relative modelmilli, not calibratedm/s. Current post parents can have changed since founding; original town ParentCity is retained separately.",Towns=towns,ReferencePassages=comparisons},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine(output+" towns="+towns.Length+" year="+s.Years);
    }
}

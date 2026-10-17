using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Explicit offline inspection only. No authoritative physics, model versions,
// recurring censuses or generated geography are changed by this diagnostic.
public static class HistoryContinentalDiffusionAudit
{
    sealed class Edge
    {
        public int Route,From,To,Donor,BoundaryFrom,BoundaryTo;
        public bool SameLandmass,Inhabited,BoundaryAllowed,BestCellAllowed,FixedDonorBoundaryAllowed;
        public int RecipientRouteCount,TrialsPerReview;
        public double Km,BestCellKm,BoundaryTransmission,BestCellTransmission,PerAttemptAnnualTransit,AnnualTransit,Continuity;
    }
    public static void Run(string source,string output,int year)
    {
        if(File.Exists(Path.Combine(output,"continental-diffusion.json")))throw new IOException("Use a new continental diagnostic folder.");
        Directory.CreateDirectory(output);
        var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        while(session.LastSnapshot.Years<year)
        {
            int step=session.LastSnapshot.History.Polities.Length==0?1000:100;
            session.AdvanceSeconds(Math.Min(step,year-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            if(session.LastSnapshot.Years%20000==0)Console.WriteLine("Continental replay "+session.LastSnapshot.Years);
        }
        var checkpoint=session.Snapshot();engine.Validate(checkpoint);string checkpointHash=checkpoint.ComputeHash();
        // Routing getters can update read counters: inspect a detached branch.
        var s=checkpoint.Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] values)
        {
            var method=typeof(Prehistory).GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!;
            var parameters=method.GetParameters();
            if(values.Length<parameters.Length)values=values.Concat(Enumerable.Repeat<object>(Type.Missing,parameters.Length-values.Length)).ToArray();
            return method.Invoke(engine,values)!;
        }
        double[] Field(string name)=>(double[])typeof(Prehistory).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        var river=Field("riverAccess");var season=Field("landscapeSeason");
        double Km(int x,int y)=>D.Angle(world.Surface.Cells[x].Position,world.Surface.Cells[y].Position)*world.Recipe.RadiusKm;
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        var first=h.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();
        int firstCell=s.Society.Settlements[first.CapitalId].Cell,firstRegion=engine.CellPatch[firstCell],firstCulture=first.CultureId;
        int firstLandmass=world.Landmass[firstCell];
        var active=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0).ToArray();
        var townsByRegion=active.GroupBy(t=>engine.CellPatch[t.Cell]).ToDictionary(g=>g.Key,g=>g.Select(t=>t.Id).ToArray());
        var donors=new int[engine.Patches.Length];
        var residents=new long[donors.Length];
        for(int r=0;r<donors.Length;r++)
        {
            donors[r]=(int)Call("DevelopmentRegionalTeacher",s,r);
            residents[r]=(long)Call("ProvincialContactResidents",s,r,false);
            if(donors[r]<0&&s.Population[r]+a.Living.Regions[r].SettledPopulation>=100&&a.Living.Regions[r].Exposure>=100)donors[r]=a.Living.Regions[r].Teacher;
        }
        var regions=Enumerable.Range(0,engine.Patches.Length).Select(r=>
        {
            var region=a.Regions[r];var local=a.Living.Regions[r];int cell=region.BestCell,culture=region.Culture;
            return new{Id=r,Cell=cell,Latitude=Lat(cell),Longitude=Lon(cell),Landmass=world.Landmass[cell],RawResidents=s.Population[r],ContactResidents=residents[r],local.SettledPopulation,
                Culture=culture,Farming=culture<0?0:s.Society.Cultures[culture].Farming,Institution=culture<0?0:h.Cultures[culture].Institution,
                Donor=donors[r],local.Teacher,local.Exposure,local.LastExchangeYear,local.CivicTeacher,local.CivicExposure,region.ReliefM,region.AridityMilli,
                Towns=townsByRegion.TryGetValue(r,out var towns)?towns:Array.Empty<int>()};
        }).ToArray();
        var edges=new List<Edge>();
        for(int i=0;i<engine.Routes.Length;i++)
        {
            var route=engine.Routes[i];if(route.Sea)continue;
            for(int direction=0;direction<2;direction++)
            {
                int from=direction==0?route.A:route.B,to=direction==0?route.B:route.A;
                int fb=a.Regions[from].BestCell,tb=a.Regions[to].BestCell;
                int fc=direction==0?route.CellA:route.CellB,tc=direction==0?route.CellB:route.CellA;
                if(fc<0)fc=fb;if(tc<0)tc=tb;
                bool same=world.Landmass[fc]>=0&&world.Landmass[fc]==world.Landmass[tc];
                int donor=donors[from],probe=donor>=0?donor:firstCulture;
                bool boundary=(bool)Call("InstitutionalRouteAllowed",s,fc,tc,probe),best=(bool)Call("InstitutionalRouteAllowed",s,fb,tb,probe);
                double transmission=(double)Call("EnvironmentalTransmission",s,fb,tb,probe,false),boundaryTransmission=(double)Call("EnvironmentalTransmission",s,fc,tc,probe,false);
                double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(world.Ecology.Habitat[fb],world.Ecology.Habitat[fb],world.Ecology.Habitat[tb]);
                double distance=route.DistanceM/1000.0+Math.Min(450,engine.Patches[from].RadiusKm+engine.Patches[to].RadiusKm);
                var habitat=world.Ecology.Habitat[tb];double barrier=1+a.Regions[to].ReliefM/1200.0+(habitat.AridityMilli<180?3:0)+(habitat.PermafrostPermille>300?3:0);
                double transport=HistoricalModernEffects.LandTransport(h.Cultures[probe],s.Years)*Math.Sqrt(HistoricalModernEffects.Communication(h.Cultures[probe],s.Years));
                double annual=(.15+.85*continuity)*transport/Math.Max(80,60+distance*barrier*.3)*transmission;
                int choices=a.Regions[to].Routes.Length,trials=Math.Min(12,choices);
                edges.Add(new Edge{Route=i,From=from,To=to,Donor=donor,BoundaryFrom=fc,BoundaryTo=tc,SameLandmass=same,Inhabited=residents[from]>=100&&residents[to]>=100,
                    BoundaryAllowed=boundary,BestCellAllowed=best,FixedDonorBoundaryAllowed=(bool)Call("InstitutionalRouteAllowed",s,fc,tc,firstCulture),
                    Km=route.DistanceM/1000.0,BestCellKm=Km(fb,tb),BoundaryTransmission=boundaryTransmission,BestCellTransmission=transmission,
                    RecipientRouteCount=choices,TrialsPerReview=trials,PerAttemptAnnualTransit=annual,AnnualTransit=annual*trials/Math.Max(1,choices),Continuity=continuity});
            }
        }
        var adjacency=Enumerable.Range(0,donors.Length).Select(_=>new List<int>()).ToArray();
        for(int i=0;i<edges.Count;i++)adjacency[edges[i].From].Add(i);
        (double[] Dist,int[] Prev) Walk(Func<Edge,bool> allowed,Func<Edge,double> cost)
        {
            var dist=Enumerable.Repeat(double.PositiveInfinity,donors.Length).ToArray();var previous=Enumerable.Repeat(-1,donors.Length).ToArray();
            dist[firstRegion]=0;var queue=new PriorityQueue<int,double>();queue.Enqueue(firstRegion,0);
            while(queue.TryDequeue(out int at,out double priority))
            {
                if(priority!=dist[at])continue;
                foreach(int index in adjacency[at]){var e=edges[index];if(!allowed(e))continue;double next=priority+cost(e);if(next<dist[e.To]){dist[e.To]=next;previous[e.To]=index;queue.Enqueue(e.To,next);}}
            }
            return(dist,previous);
        }
        bool OnSourceLand(Edge e)=>e.SameLandmass&&world.Landmass[a.Regions[e.From].BestCell]==firstLandmass&&world.Landmass[a.Regions[e.To].BestCell]==firstLandmass;
        var raw=Walk(OnSourceLand,e=>e.Km);
        var physical=Walk(e=>OnSourceLand(e)&&e.FixedDonorBoundaryAllowed,e=>e.Km);
        var current=Walk(e=>OnSourceLand(e)&&e.Inhabited&&e.Donor>=0&&e.BoundaryAllowed,e=>1/Math.Max(1e-15,e.AnnualTransit));
        var targets=new[]{("Iberia",37.2,-6.3),("CentralEurope",48.0,15.0),("Anatolia",39.0,33.0),("Iran",35.7,51.4),("Indus",30.0,70.0),("Ganges",25.3,83.0),("Tarim",40.5,82.0),("Nile",30.0,31.0)};
        object PathTo(int region,(double[] Dist,int[] Prev) walk)
        {
            if(double.IsPositiveInfinity(walk.Dist[region]))return new{Reachable=false,Cost=(double?)null,Edges=Array.Empty<Edge>()};
            var path=new List<Edge>();int at=region;
            while(at!=firstRegion){int edge=walk.Prev[at];if(edge<0)break;path.Add(edges[edge]);at=edges[edge].From;}path.Reverse();
            return new{Reachable=true,Cost=(double?)walk.Dist[region],Edges=path.ToArray()};
        }
        var paths=targets.Select(target=>
        {
            double lat=target.Item2*Math.PI/180,lon=target.Item3*Math.PI/180;var point=new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat));
            int region=Enumerable.Range(0,donors.Length).OrderByDescending(r=>V.Dot(point,world.Surface.Cells[a.Regions[r].BestCell].Position)).ThenBy(r=>r).First();
            return new{Name=target.Item1,TargetLatitude=target.Item2,TargetLongitude=target.Item3,Region=region,RawRoute=PathTo(region,raw),FirstCulturePhysicalRoute=PathTo(region,physical),CurrentDonorRoute=PathTo(region,current)};
        }).ToArray();
        object Candidate(Settlement sourceTown,int trial)
        {
            int target=(int)Call("LivingLocalTownCandidate",s,sourceTown.Id,trial);double yield=(double)Call("LivingCropLandscape",s,sourceTown.CultureId,target,false);
            double rails=HistoricalModernEffects.Adoption(h.Cultures[sourceTown.CultureId],HistoricalTechnology.Railways,s.Years),spacing=(100-45*rails)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,yield))));
            return new{Trial=trial,Cell=target,Latitude=Lat(target),Longitude=Lon(target),Km=Km(sourceTown.Cell,target),Landmass=world.Landmass[target],Region=engine.CellPatch[target],
                RuralResidents=(long)Call("RuralCellPopulation",s,target),LocalYield=yield,SpacingKm=spacing,Nearby=(int[])Call("NearbyLivingTowns",s,target,spacing),ExistingTown=a.TownAt[target],
                RouteAllowed=(bool)Call("InstitutionalRouteAllowed",s,sourceTown.Cell,target,sourceTown.CultureId),Transmission=(double)Call("EnvironmentalTransmission",s,sourceTown.Cell,target,sourceTown.CultureId,false),
                CanFound=(bool)Call("CanFoundLivingTown",s,sourceTown.CultureId,target,sourceTown.Id),Habitat=world.Ecology.Habitat[target]};
        }
        var townDetails=active.Where(t=>Lat(t.Cell)>=10&&Lon(t.Cell)>=-15&&Lon(t.Cell)<90).Select(t=>
        {
            var city=a.Cities[t.Id];var local=a.Living.Cities[t.Id];var hc=h.Cultures[t.CultureId];int culture=t.CultureId;
            double fiscal=(double)Call("LivingCropLandscape",s,culture,t.Cell,true);
            return new{t.Id,t.Name,t.Cell,Latitude=Lat(t.Cell),Longitude=Lon(t.Cell),t.CultureId,t.PolityId,t.Population,t.FoodCapacity,t.FoodReserve,Age=s.Years-t.FoundedYear,
                city.Rural,city.RuralCapacity,city.Supported,local.DeclineYears,Farming=s.Society.Cultures[culture].Farming,hc.Institution,hc.Surplus,
                Fiscal=fiscal,Environment=(double)Call("EnvironmentalEstablishment",s,t.Cell,culture),Staples=(double)Call("EnvironmentalStaples",s,culture,t.Cell),
                Proto=HistoricalRegionalTraditions.Proto(s,t.Id),h.Towns[t.Id].CivicPreparationYears,h.Towns[t.Id].CivicExampleExposure,
                FounderAvailability=(long)Call("LivingFounderAvailability",s,t.Id),LocalTownRate=(double)Call("LivingLocalTownRate",s,t.Id),
                Nearest=active.Where(other=>other.Id!=t.Id).Select(other=>new{other.Id,other.Name,other.Population,Km=Km(t.Cell,other.Cell)}).OrderBy(x=>x.Km).ThenBy(x=>x.Id).Take(5).ToArray(),
                Candidates=Enumerable.Range(0,3).Select(trial=>Candidate(t,trial)).ToArray()};
        }).ToArray();
        var cultures=s.Society.Cultures.Select(c=>new{c.Id,c.Name,c.OriginCell,Latitude=Lat(c.OriginCell),Longitude=Lon(c.OriginCell),c.Population,c.Farming,c.Fishing,
            h.Cultures[c.Id].Institution,h.Cultures[c.Id].Surplus,KnowledgeEra=WorldEraView.KnowledgeEra(h.Cultures[c.Id]),
            Crops=h.Agriculture[c.Id],Levels=h.Cultures[c.Id].Technical?.Levels,Deployment=h.Cultures[c.Id].Technical?.Deployment}).ToArray();
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(Path.Combine(output,"continental-diffusion.json"),JsonSerializer.Serialize(new{
            Source=Path.GetFullPath(source),Year=s.Years,CheckpointHash=checkpointHash,CoreAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
            FirstCourt=new{first.Id,first.Name,first.FoundedYear,first.CapitalId,Cell=firstCell,Region=firstRegion,Culture=firstCulture,Landmass=firstLandmass},
            RawReach=raw.Dist.Count(double.IsFinite),PhysicalReach=physical.Dist.Count(double.IsFinite),CurrentDonorReach=current.Dist.Count(double.IsFinite),Paths=paths,Regions=regions,Edges=edges,Towns=townDetails,Cultures=cultures,
            Notes=new[]{"Offline census on a detached snapshot only; no authoritative simulation behavior is changed.","CurrentDonorRoute cost sums optimistic mean waits1/annual transit along CURRENT donor/inhabited routes; it is not a simulated historical arrival time.","Raw/physical paths use same-landmass graph routes; fixed first-culture capability is a diagnostic potential, not permission for a free transmission.","Boundary route gate and environmental transmission between distant BestCells are logged separately because the live regional exchange uses both.","Three local candidates are the exact current-year proposals, not all failed historical proposals. Geography target coordinates are inspection locations, not rules or homeland priors."}
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(checkpoint.ComputeHash()!=checkpointHash)throw new Exception("Diagnostic mutated its published source checkpoint.");
        Console.WriteLine($"Continental audit {s.Years}, {checkpointHash}, raw/physical/current reach {raw.Dist.Count(double.IsFinite)}/{physical.Dist.Count(double.IsFinite)}/{current.Dist.Count(double.IsFinite)}");
    }
}

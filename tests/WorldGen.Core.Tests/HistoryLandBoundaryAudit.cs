using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit offline alternative-crossing census. Never modifies model routes.
public static class HistoryLandBoundaryAudit
{
    public static void Run(string source,string census,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new boundary audit output.");
        using var reference=JsonDocument.Parse(File.ReadAllText(census));
        int year=reference.RootElement.GetProperty("Year").GetInt32();
        string expected=reference.RootElement.GetProperty("Hash").GetString();
        var selected=reference.RootElement.GetProperty("Edges").EnumerateArray().Where(e=>
            e.GetProperty("SourceKind").GetString()!="TeacherOnly"&&e.GetProperty("TargetOwner").GetInt32()<0
            &&e.GetProperty("Longitude").GetDouble()<105&&e.GetProperty("Latitude").GetDouble()>5).Select(e=>e.Clone()).ToArray();
        var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        while(session.LastSnapshot.Years<year)
        {
            int step=session.LastSnapshot.History.Polities.Length==0?1000:100;
            session.AdvanceSeconds(Math.Min(step,year-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            if(session.LastSnapshot.Years%20000==0)Console.WriteLine("Boundary replay "+session.LastSnapshot.Years);
        }
        var original=session.Snapshot();engine.Validate(original);string hash=original.ComputeHash();
        if(hash!=expected)throw new Exception("Boundary replay differs from its exact census checkpoint.");
        var s=original.Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        double Km(int first,int second)=>D.Angle(world.Surface.Cells[first].Position,world.Surface.Cells[second].Position)*world.Recipe.RadiusKm;
        var pairs=selected.Select(e=>(From:e.GetProperty("From").GetInt32(),To:e.GetProperty("To").GetInt32())).ToHashSet();
        var boundaries=new Dictionary<(int,int),List<(int From,int To,int Length)>>();
        foreach(var edge in world.Surface.Edges)
        {
            if(world.Hydrology.OceanConnected[edge.A]||world.Hydrology.OceanConnected[edge.B])continue;
            int first=engine.CellPatch[edge.A],second=engine.CellPatch[edge.B];
            if(first<0||second<0||first==second)continue;
            void Add(int from,int to,int x,int y)
            {
                if(!pairs.Contains((from,to)))return;
                if(!boundaries.TryGetValue((from,to),out var list))boundaries[(from,to)]=list=new List<(int,int,int)>();
                list.Add((x,y,edge.LengthM));
            }
            Add(first,second,edge.A,edge.B);Add(second,first,edge.B,edge.A);
        }
        var result=new List<object>();
        foreach(var item in selected)
        {
            int from=item.GetProperty("From").GetInt32(),to=item.GetProperty("To").GetInt32(),donor=item.GetProperty("ProbeCulture").GetInt32();
            int selectedFrom=item.GetProperty("DepartureCell").GetInt32(),selectedTo=item.GetProperty("ArrivalCell").GetInt32();
            var alternatives=new List<object>();int quality=0,free=0,unowned=0;
            foreach(var crossing in boundaries[(from,to)].OrderBy(x=>x.From).ThenBy(x=>x.To))
            {
                int x=crossing.From,y=crossing.To;
                double yield=(double)Call("LivingCropLandscape",s,donor,y,false),fit=(double)Call("AggregateCropSupport",s,donor,y,false);
                bool gate=(bool)Call("InstitutionalRouteAllowed",s,x,y,donor);
                long fields=(long)Call("ProvincialFarmPotential",s,y,donor);
                int raw=HistoricalCells.Polity(s,x),owner=raw>=0?a.Resolve(raw,h):-1;
                int targetRaw=HistoricalCells.Polity(s,y),targetOwner=targetRaw>=0?a.Resolve(targetRaw,h):-1;
                int sourceCulture=HistoricalCells.Culture(s,x),targetCulture=HistoricalCells.Culture(s,y);
                if(yield>=.12&&gate){quality++;if(fields>=400){free++;if(targetOwner<0)unowned++;}}
                int sourceBest=a.Regions[from].BestCell,targetBest=a.Regions[to].BestCell;
                double sourceTransmission=(double)Call("EnvironmentalTransmission",s,sourceBest,x,donor,false),targetTransmission=(double)Call("EnvironmentalTransmission",s,y,targetBest,donor,false);
                var habitat=world.Ecology.Habitat[y];
                alternatives.Add(new{FromCell=x,ToCell=y,Selected=x==selectedFrom&&y==selectedTo,SharedBoundaryM=crossing.Length,
                    Latitude=Lat(y),Longitude=Lon(y),SourceOwner=owner,SourceOwnerCulture=owner>=0?h.Polities[owner].CultureId:-1,SourceCellCulture=sourceCulture,
                    TargetOwner=targetOwner,TargetCellCulture=targetCulture,Yield=yield,CarriedFit=fit,InstitutionalGate=gate,FreeFields=fields,
                    CrossingKm=Km(x,y),SourceBestKm=Km(sourceBest,x),TargetBestKm=Km(y,targetBest),SourceTransmission=sourceTransmission,TargetTransmission=targetTransmission,
                    RelayEffortKm=Km(sourceBest,x)/Math.Max(.00001,sourceTransmission)+Km(x,y)+Km(y,targetBest)/Math.Max(.00001,targetTransmission),
                    Climate=world.Climate.Classification[y].ToString(),Biome=habitat.Biome.ToString(),habitat.ReliefM,habitat.GrowingDays,habitat.WarmTenthsC});
            }
            result.Add(new{Route=item.GetProperty("Route").GetInt32(),From=from,To=to,Donor=donor,SourceKind=item.GetProperty("SourceKind").GetString(),
                SelectedYield=item.GetProperty("CropYield").GetDouble(),SelectedFields=item.GetProperty("PotentialFreeFields").GetInt64(),SelectedGate=item.GetProperty("RouteAllowed").GetBoolean(),
                Crossings=alternatives.Count,QualityCrossings=quality,FreeFieldCrossings=free,UnownedFreeCrossings=unowned,Alternatives=alternatives});
        }
        if(original.ComputeHash()!=hash)throw new Exception("Boundary audit changed its source checkpoint.");
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=source,Census=census,Year=year,Hash=hash,CoreAssembly=assembly,
            CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),Pairs=result,
            Notes=new[]{"Explicit offline full shared-boundary scan for selected western frontier pairs; authoritative routes remain unchanged.",
                "The same recorded donor is evaluated at every crossing. Crop/gate/free-field success alone does not prove local donor households, service, food, residence time, receiving room or migration eligibility.",
                "Source/target owners and cell cultures are reported separately. Relay effort includes both region interiors, not just the shared border.",
                "Selection is fixed by the preceding exact census: unowned targets west of105E/north of5N with an actual regional-farmer or owner-Remote source proxy, not TeacherOnly."}
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Boundary audit {year}: {result.Count} directed region pairs; preserved {hash}");
    }
}

using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Explicit offline saved-world evidence. These censuses never run in simulation.
public static class HistoryReligionEvidenceAudit
{
    public static void Run(string path,string folder)
    {
        if(Directory.Exists(folder))throw new ArgumentException("Use a new religion evidence directory.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);
        string hash=s.ComputeHash();var h=s.History;var a=h.Aggregates;var r=a.Empires.Connections.Religion;
        if(r==null)throw new ArgumentException("This evidence audit requires saved local religious networks.");
        object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args);
        Call("PrepareAggregateRelations",s);
        var towns=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0).OrderBy(t=>t.Id).ToArray();
        var live=towns.Select(t=>t.Id).ToHashSet();int n=world.Surface.Cells.Length;
        var publicFaith=new int[n];var households=new int[n];var cellCount=new int[h.Religions.Length];var explicitCount=new int[h.Religions.Length];var houseCount=new int[h.Religions.Length];var components=new List<int>[h.Religions.Length];
        int recorded=0,distinct=0;for(int id=0;id<components.Length;id++)components[id]=new List<int>();
        for(int cell=0;cell<n;cell++)
        {
            publicFaith[cell]=HistoricalCells.Religion(s,cell);households[cell]=r.Read(cell,5);
            if(publicFaith[cell]>=0){cellCount[publicFaith[cell]]++;if(a.Cells[cell*5+2]>=0)explicitCount[publicFaith[cell]]++;}
            if(households[cell]>=0){houseCount[households[cell]]++;recorded++;if(households[cell]!=publicFaith[cell])distinct++;}
        }
        var seen=new bool[n];var queue=new Queue<int>();
        for(int cell=0;cell<n;cell++)
        {
            if(seen[cell]||publicFaith[cell]<0||world.Hydrology.OceanConnected[cell])continue;int faith=publicFaith[cell],count=0;seen[cell]=true;queue.Enqueue(cell);
            while(queue.Count>0){int from=queue.Dequeue();count++;foreach(int peer in world.Surface.Cells[from].Neighbors)if(!seen[peer]&&!world.Hydrology.OceanConnected[peer]&&publicFaith[peer]==faith){seen[peer]=true;queue.Enqueue(peer);}}
            components[faith].Add(count);
        }
        var links=new List<object>();var graph=towns.ToDictionary(t=>t.Id,t=>new HashSet<int>());int candidates=0,usable=0,crossFaith=0;
        foreach(var from in towns)foreach(int target in a.Cities[from.Id].Neighbors)
        {
            if(!live.Contains(target))continue;candidates++;var to=s.Society.Settlements[target];
            bool route=(bool)Call("InstitutionalRouteAllowed",s,from.Cell,to.Cell,from.CultureId);
            bool carrier=route&&(bool)Call("ReligiousTownCarrierAllowed",s,from.Id,to.Id);if(!carrier)continue;usable++;
            int first=publicFaith[from.Cell],second=publicFaith[to.Cell];if(first!=second)crossFaith++;
            if(first==second){graph[from.Id].Add(to.Id);graph[to.Id].Add(from.Id);}
            links.Add(new{Source=from.Id,Target=to.Id,SourceFaith=first,TargetFaith=second,SameFaith=first==second});
        }
        var townRows=towns.Select(t=>
        {
            int owner=a.Resolve(t.PolityId,h);var local=Prehistory.ReligiousCommunityAt(s,t.Cell);var p=owner>=0?h.Polities[owner]:null;
            long tenYearFood=HistoricalReligiousNetworkEffects.FoodCost(t.Population,10);
            bool institutionEligible=a.Cities[t.Id].Supported&&t.Population>=600&&t.FoodReserve>=t.Population/3;
            int parent=a.Cities[t.Id].ParentCity;double latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI,longitude=Math.Atan2(world.Surface.Cells[t.Cell].Position.Y,world.Surface.Cells[t.Cell].Position.X)*180/Math.PI;
            return new{t.Id,t.Name,t.Cell,t.CultureId,t.FoundedYear,t.Population,Latitude=latitude,Longitude=longitude,ParentCity=parent,ParentCurrentFaith=parent>=0?HistoricalCells.Religion(s,s.Society.Settlements[parent].Cell):-1,CultureDefaultFaith=h.Cultures[t.CultureId].ReligionId,Public=publicFaith[t.Cell],Household=local.Household,Visitor=local.Visitor,local.Institution,local.Pressure,local.Blending,local.LastContact,LastReviewedYear=r.Read(t.Cell,2),YearsSinceReview=r.Read(t.Cell,2)>=0?s.Years-r.Read(t.Cell,2):-1,RecentUnlikeContact=local.LastContact>=0&&s.Years-local.LastContact<=80,CarrierSupport=r.Read(t.Cell,12),ContactSince=r.Read(t.Cell,10),Dissent=r.Read(t.Cell,9),LastReform=r.Read(t.Cell,11),local.Policy,Owner=owner,CourtFaith=p?.ReligionId??-1,Treasury=p?.Treasury??0,ServiceCost=owner>=0?a.Polities[owner].ServiceCost:0,t.FoodReserve,t.FoodCapacity,t.Stability,DisruptedUntil=a.Living.Cities[t.Id].DisruptedUntil,a.Cities[t.Id].Supported,InstitutionEligible=institutionEligible,CanProvisionNextTenYearInstitution=institutionEligible&&t.FoodReserve-t.Population/2>=tenYearFood,TenYearInstitutionFoodCost=tenYearFood,ActualContactNeighbors=a.Cities[t.Id].Neighbors.Length,UsableSameFaithNeighbors=graph[t.Id].Count};
        }).ToArray();
        var events=h.Archive.Where(e=>e.Kind.StartsWith("religious-",StringComparison.Ordinal)||e.Kind=="court-conversion").ToArray();
        var faithRows=h.Religions.Select(f=>
        {
            var sites=townRows.Where(t=>t.Public==f.Id).ToArray();var remaining=sites.Select(t=>t.Id).ToHashSet();var clusters=new List<int>();
            while(remaining.Count>0){int start=remaining.Min(),count=0;remaining.Remove(start);queue.Enqueue(start);while(queue.Count>0){int from=queue.Dequeue();count++;foreach(int peer in graph[from])if(remaining.Remove(peer))queue.Enqueue(peer);}clusters.Add(count);}
            return new{f.Id,f.Name,f.ParentId,f.CultureId,f.FoundedYear,Age=s.Years-f.FoundedYear,Reach=f.Reach.ToString(),Vocation=Call("ReligiousInstitutionVocation",s,f.Id).ToString(),f.Ethos,f.Ritual,f.Tradition,PublicCells=cellCount[f.Id],ExplicitPublicCells=explicitCount[f.Id],CultureDefaultFallbackCells=cellCount[f.Id]-explicitCount[f.Id],RecordedHouseholdCells=houseCount[f.Id],PublicLandComponents=components[f.Id].OrderByDescending(x=>x).ToArray(),PublicTowns=sites.Length,UrbanResidentsUnderPublicPredominance=sites.Sum(t=>t.Population),PublicTownCultures=sites.Select(t=>t.CultureId).Distinct().Count(),PublicTownPolities=sites.Where(t=>t.Owner>=0).Select(t=>t.Owner).Distinct().Count(),PublicTownLandmasses=sites.Select(t=>world.Landmass[t.Cell]).Distinct().Count(),CurrentPermittedSameFaithTownComponents=clusters.OrderByDescending(x=>x).ToArray(),SupportedInstitutions=sites.Count(t=>t.Institution>=400),HighSupportProvisionableInstitutions=sites.Count(t=>t.Institution>=650&&t.CanProvisionNextTenYearInstitution),LocalHouseholdTowns=townRows.Count(t=>t.Household==f.Id),CourtPatrons=h.Polities.Count(p=>p.EndedYear<0&&p.ReligionId==f.Id),RecordedEvents=events.Count(e=>e.ReligionId==f.Id),RecentSignificantEvents=events.Where(e=>e.ReligionId==f.Id&&e.Kind!="religious-exchange"&&e.Kind!="religious-policy").TakeLast(15).Select(e=>new{e.Year,e.Kind,e.Cell,e.SettlementId,e.PolityId,e.Text})};
        }).OrderByDescending(f=>f.PublicTowns).ThenByDescending(f=>f.PublicCells).ThenBy(f=>f.Id).ToArray();
        Directory.CreateDirectory(folder);var options=new JsonSerializerOptions{WriteIndented=true};
        File.WriteAllText(Path.Combine(folder,"evidence.json"),JsonSerializer.Serialize(new{Source=Path.GetFullPath(path),s.Years,Hash=hash,RealmRules=a.Empires.Connections.Realms.Version,ReligionRules=r.Version,ActiveTowns=towns.Length,ReligiousActors=h.Religions.Length,PublicTraditionsInTowns=faithRows.Count(f=>f.PublicTowns>0),RecordedHouseholdCells=recorded,DistinctHouseholdCells=distinct,SupportedInstitutions=townRows.Count(t=>t.Institution>=400),DistinctHouseholdTowns=townRows.Count(t=>t.Household>=0&&t.Household!=t.Public),CandidateDirectedTownLinks=candidates,PresentlyPermittedDirectedCarriers=usable,CrossFaithPermittedDirectedCarriers=crossFaith,Events=events.GroupBy(e=>e.Kind).OrderBy(g=>g.Key).Select(g=>new{Kind=g.Key,Count=g.Count()}),Faiths=faithRows,Towns=townRows,PermittedLinks=links,Notes="Explicit offline geometry/actor census; not a recurring simulation pass. Public cell/town colors indicate predominance, not numbers of adherents. Household entries are saved local memories, not a complete census; grey means unrecorded, never inferred public uniformity. Public land components are polygon adjacency, not cultural isolation. Current same-faith town components use existing actual town links plus present physical/institutional/carrier permissions, not historical delivered-contact proof; no same-faith edge is required for historical diaspora or a route through unlike-faith communities. Recent unlike contacts are saved visitors, not all contacts (same-faith receipts are not stored). Food and treasury fields are observed balances and next-ten-year affordability proxies, not a saved ledger of historical religious payments. Public culture-default fallback is reported separately from explicit local map layers."},options));
        // A complete event export permits causal sequences without pretending
        // that current parent-town faith was necessarily its departure faith.
        File.WriteAllText(Path.Combine(folder,"religious-events.json"),JsonSerializer.Serialize(events.Select(e=>new{e.Id,e.Year,e.Kind,e.Cell,e.CultureId,e.PolityId,e.ReligionId,e.SettlementId,e.Text}),options));
        File.WriteAllText(Path.Combine(folder,"institution-review.json"),JsonSerializer.Serialize(new{Towns=townRows.Select(t=>new{t.Id,t.Name,t.CultureId,t.FoundedYear,t.ParentCity,t.ParentCurrentFaith,t.CultureDefaultFaith,t.Public,t.Household,t.Latitude,t.Longitude,t.LastReviewedYear,t.YearsSinceReview,t.Institution,t.CanProvisionNextTenYearInstitution}),Notes="Institution support is an observed saved index, not an annual religious-payment ledger. Current parent faith does not establish departure faith. A high-support provisionable site of a Local religion is not a missionary institution."},options));
        string[] legend=faithRows.Take(12).Select(f=>$"{f.Id}: {f.Name} / {f.Reach} / {f.PublicTowns} towns").ToArray();
        Draw(world,s,publicFaith,Path.Combine(folder,"public.svg"),"Public predominance (not adherent census)",legend);
        Draw(world,s,households,Path.Combine(folder,"household.svg"),"Recorded household traditions; grey = unobserved",legend);
        if(s.ComputeHash()!=hash)throw new Exception("Religion evidence audit modified the input state.");
        Console.WriteLine($"Religious evidence {s.Years}: {towns.Length} towns; {faithRows.Count(f=>f.PublicTowns>0)} public traditions; {townRows.Count(t=>t.Institution>=400)} supported institutions; {recorded} recorded household cells. Source unchanged.");
    }
    internal static void Draw(World world,PrehistoryState s,int[] faiths,string path,string label,string[] legend)
    {
        var corners=Enumerable.Range(0,faiths.Length).Select(_=>new List<V>()).ToArray();for(int t=0;t<world.Surface.Triangles.Length;t+=3)for(int k=0;k<3;k++)corners[world.Surface.Triangles[t+k]].Add(world.Surface.Corners[t/3]);
        var b=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1800' height='1120' viewBox='0 0 1800 1120'><rect width='1800' height='1120' fill='#10202b'/><rect y='60' width='1800' height='900' fill='#8cc7e6'/>");
        b.Append($"<text x='20' y='35' fill='white' font-family='sans-serif' font-size='22'>{SecurityElement.Escape(label)} / saved year {s.Years}</text>");
        for(int cell=0;cell<faiths.Length;cell++)
        {
            if(world.Hydrology.OceanConnected[cell])continue;var p=world.Surface.Cells[cell].Position;double lon=Math.Atan2(p.Y,p.X),lat=Math.Asin(p.Z);string hex=faiths[cell]>=0?SocietyView.ColorHex(faiths[cell]):"#bbbbbb";
            var points=corners[cell].Select(v=>{double x=Math.Atan2(v.Y,v.X);while(x-lon>Math.PI)x-=2*Math.PI;while(x-lon< -Math.PI)x+=2*Math.PI;return(x,y:Math.Asin(v.Z));}).OrderBy(v=>Math.Atan2(v.y-lat,(v.x-lon)*Math.Cos(lat))).ToArray();
            foreach(int wrap in new[]{-1,0,1}){if(points.All(v=>v.x+wrap*2*Math.PI< -Math.PI)||points.All(v=>v.x+wrap*2*Math.PI>Math.PI))continue;string polygon=string.Join(" ",points.Select(v=>FormattableString.Invariant($"{900+(v.x+wrap*2*Math.PI)*900/Math.PI:F2},{510-v.y*900/Math.PI:F2}")));b.Append($"<polygon points='{polygon}' fill='{hex}'/>");}
        }
        for(int i=0;i<legend.Length;i++){int id=int.Parse(legend[i].Split(':')[0],CultureInfo.InvariantCulture),x=20+(i%3)*590,y=985+(i/3)*32;b.Append($"<rect x='{x}' y='{y-14}' width='16' height='16' fill='{SocietyView.ColorHex(id)}'/><text x='{x+24}' y='{y}' fill='white' font-family='sans-serif' font-size='14'>{SecurityElement.Escape(legend[i])}</text>");}
        b.Append("</svg>");File.WriteAllText(path,b.ToString());
    }
}

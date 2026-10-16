using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

var json=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};
void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
Recipe RecipeFor(string name,int seed,int detail,string version)
{
    var r=new Recipe{Seed=seed,Detail=detail,AlgorithmVersion=version};
    if(name=="Earth")r.TerrainSource="Earth";
    if(name=="OneContinent"){r.ContinentCount=1;r.LandFraction=.5;}
    if(name=="SixContinents"){r.ContinentCount=6;r.LandFraction=.3;}
    if(name=="Donut"){r.LandShape="Donut-v1";r.LandFraction=.4;}
    if(name=="Archipelago"){r.ContinentCount=35;r.MicroplateCount=120;r.LandFraction=.25;}
    if(name=="Oceanless"){r.LandShape="AllLand-v1";r.LandFraction=1;r.LakeAmountPercent=0;}
    return r;
}
var pipeline=new WorldPipeline();
if(args[0]=="baseline")
{
    Directory.CreateDirectory(args[1]);
    foreach(string name in new[]{"Earth","OneContinent","SixContinents","Donut","Archipelago","Oceanless"})
    {
        if(File.Exists(Path.Combine(args[1],name+".world")))continue;
        var recipe=RecipeFor(name,73421,5000,"worldgen-3.32.0");var world=pipeline.Generate(recipe);
        File.WriteAllText(Path.Combine(args[1],name+".recipe.json"),WorldJson.Write(recipe));
        File.WriteAllText(Path.Combine(args[1],name+".hashes.json"),JsonSerializer.Serialize(world.Stages,json));
        File.WriteAllBytes(Path.Combine(args[1],name+".world"),WorldFile.Write(world));Console.WriteLine("BASELINE "+name+" "+world.Stages.Last().Hash);
    }
    return;
}
if(args[0]=="compat")
{
    foreach(string path in Directory.GetFiles(args[1],"*.world").OrderBy(p=>p))
    {
        var saved=WorldFile.Read(File.ReadAllBytes(path));var expected=saved.Stages.Select(s=>s.Hash).ToArray();
        foreach(var pair in new[]{("fresh",pipeline.Generate(saved.Recipe)),("reapply",pipeline.Reapply(saved.Recipe)),("recipe",pipeline.Generate(WorldJson.ReadRecipe(WorldJson.Write(saved.Recipe)))),("cached",pipeline.Reconstruct(saved))})
            Check(expected.SequenceEqual(pair.Item2.Stages.Select(s=>s.Hash)),Path.GetFileName(path)+" "+saved.Recipe.AlgorithmVersion+" "+pair.Item1+" all-stage identity");
    }
    return;
}
if(args[0]=="trial")
{
    string name=args[1],folder=args[7];Directory.CreateDirectory(folder);
    var r=RecipeFor(name,int.Parse(args[2]),int.Parse(args[3]),args[4]);
    if(r.AlgorithmVersion=="worldgen-3.33.0")
    {
        var field=typeof(Recipe).GetField("BoundaryInflow");var profile=Activator.CreateInstance(field.FieldType);
        field.FieldType.GetField("DragHours").SetValue(profile,double.Parse(args[5],CultureInfo.InvariantCulture));field.FieldType.GetField("Strength").SetValue(profile,double.Parse(args[6],CultureInfo.InvariantCulture));field.SetValue(r,profile);
    }
    var watch=System.Diagnostics.Stopwatch.StartNew();var world=pipeline.Generate(r,p=>{if(p.Percent>=80)Console.WriteLine(p.Stage+" "+watch.Elapsed.TotalSeconds.ToString("F1"));});
    File.WriteAllBytes(Path.Combine(folder,"final.world"),WorldFile.Write(world));
    Report(world,folder);Console.WriteLine("TRIAL "+folder+" "+watch.Elapsed.TotalSeconds.ToString("F1")+"s");return;
}
if(args[0]=="report"){Report(WorldFile.Read(File.ReadAllBytes(args[1])),args[2]);return;}
if(args[0]=="fields")
{
    var before=WorldFile.Read(File.ReadAllBytes(args[1]));var after=WorldFile.Read(File.ReadAllBytes(args[2]));
    Check(StateHash.Compute(before.CoarseSurface)==StateHash.Compute(after.CoarseSurface),"Atmospheric experiment retains exact coarse geometry");
    Check(StateHash.Compute(before.Surface)==StateHash.Compute(after.Surface),"Atmospheric experiment retains exact fine geometry");
    Check(StateHash.Compute(before.Microplates)==StateHash.Compute(after.Microplates),"Atmospheric experiment retains exact plate provenance");
    Check(StateHash.Compute(before.ElevationM)==StateHash.Compute(after.ElevationM),"Atmospheric experiment retains exact physical elevations");
    Check(StateHash.Compute(before.Hydrology.OceanConnected)==StateHash.Compute(after.Hydrology.OceanConnected),"Atmospheric experiment retains exact ocean mask");return;
}
if(args[0]=="mechanisms")
{
    var profileType=typeof(Recipe).GetField("BoundaryInflow").FieldType;var profile=Activator.CreateInstance(profileType);var asm=typeof(Recipe).Assembly;
    var boundaryType=asm.GetType("WorldGen.Core.AtmosphericBoundaryInflow");var response=boundaryType.GetMethod("Response",BindingFlags.NonPublic|BindingFlags.Static);
    V Respond(V gradient,double latitude)=>(V)response.Invoke(null,new object[]{gradient,latitude,profile});
    Check(V.Dot(Respond(new V(.0001,0,0),.4),new V(.0001,0,0))<0,"Frictional response moves down pressure, not uphill");
    Check(Respond(new V(),.4).Length==0,"No pressure gradient creates no additional wind");
    Check(Respond(new V(.0001,0,0),.4).Length==Respond(new V(.0001,0,0),-.4).Length,"Mirrored hemispheres have equal down-gradient mobility");
    Check((Respond(new V(.0001,0,0),.4)+Respond(new V(-.0001,0,0),.4)).Length<1e-12,"Seasonal thermal-gradient reversal reverses inflow");
    Check(double.IsFinite(Respond(new V(.0001,0,0),0).Length),"Equatorial response is finite");
    var r=new Recipe{Detail=5000,AlgorithmVersion="worldgen-3.33.0"};var surface=SphericalMesh.Build(r,default);var ocean=surface.Cells.Select(c=>D.Angle(c.Position,new V(.9063078,0,.4226183))>.55).ToArray();
    var thermal=new double[surface.Cells.Length];var circulation=asm.GetType("WorldGen.Core.AtmosphericCirculation").GetMethod("BasinWind",BindingFlags.NonPublic|BindingFlags.Static);
    V[] Field(double season)
    {
        circulation.Invoke(null,new object[]{surface,ocean.Select(x=>x?1d:0d).ToArray(),season,CancellationToken.None,null,null,thermal});
        return (V[])boundaryType.GetMethod("Solve",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{surface,thermal,profile,CancellationToken.None});
    }
    var summer=Field(1);var winter=Field(-1);
    Check(summer.Zip(winter,(a,b)=>(a+b).Length).Max()<1e-10,"Actual generic spherical coast thermal inflow reverses with season");
    Check(summer.Max(v=>v.Length)*5<=6.0000001,"Additional physical flow retains globally bounded speed");
    World Fixture(bool dry)=>new World{Recipe=r.Clone(),Surface=surface,ElevationM=ocean.Select(x=>!dry&&x?-100:100).ToArray(),Landmass=new int[surface.Cells.Length],Hydrology=new HydrologyResult{OceanConnected=dry?new bool[surface.Cells.Length]:ocean}};
    var wet=Fixture(false);Climate.Build(wet,default);
    Check(wet.Climate.AtmosphericMonths.All(m=>m.MoistureBudgetResidualPpm<10000),"Divergent inflow still closes every finite-column monthly moisture budget below1percent");
    Check(wet.Climate.AtmosphericMonths.All(m=>m.PrecipitationMm.All(p=>p>=0)),"Actual conservative solver never generates negative rainfall");
    var dry=Fixture(true);Climate.Build(dry,default);Check(dry.Climate.AtmosphericMonths.All(m=>m.PrecipitationMm.All(p=>p==0)),"Oceanless thermal inflow cannot invent a moisture source");
    using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{Climate.Build(Fixture(false),cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Climate forcing honors cancellation");
    var before=Fixture(false);before.Recipe.AlgorithmVersion="worldgen-3.32.0";Climate.Build(before,default);
    var zero=Fixture(false);var disabled=Activator.CreateInstance(profileType);profileType.GetField("Strength").SetValue(disabled,0d);typeof(Recipe).GetField("BoundaryInflow").SetValue(zero.Recipe,disabled);Climate.Build(zero,default);
    Check(StateHash.Compute(before.Climate)==StateHash.Compute(zero.Climate),"Zero experimental strength exactly preserves old climate arrays");
    Console.WriteLine("MECHANISMS COMPLETE");return;
}
if(args[0]=="flow")
{
    var world=WorldFile.Read(File.ReadAllBytes(args[1]));var surface=world.CoarseSurface;var locator=new CellLocator(surface.Cells);int n=surface.Cells.Length;var area=new double[n];var sea=new double[n];
    foreach(var cell in world.Surface.Cells){int j=locator.Nearest(cell.Position);area[j]+=cell.AreaM2;if(world.Hydrology.OceanConnected[cell.Id])sea[j]+=cell.AreaM2;}
    for(int i=0;i<n;i++)sea[i]/=Math.Max(1,area[i]);
    var pressure=new double[n];var call=new object[]{surface,sea,1d,CancellationToken.None,null,null,pressure};
    var asm=typeof(Recipe).Assembly;var basin=(V[])asm.GetType("WorldGen.Core.AtmosphericCirculation").GetMethod("BasinWind",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,call);
    var profile=typeof(Recipe).GetField("BoundaryInflow").GetValue(world.Recipe);
    var boundary=(V[])asm.GetType("WorldGen.Core.AtmosphericBoundaryInflow").GetMethod("Solve",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{surface,pressure,profile,CancellationToken.None});
    var response=asm.GetType("WorldGen.Core.AtmosphericBoundaryInflow").GetMethod("Response",BindingFlags.NonPublic|BindingFlags.Static);
    V Gradient(int i)
    {
        var cell=surface.Cells[i];var p=cell.Position;var east=new V(-p.Y,p.X,0).Unit;var north=V.Cross(p,east).Unit;double xx=.000001,xy=0,yy=.000001,gx=0,gy=0;
        for(int k=0;k<cell.Neighbors.Length;k++){int j=cell.Neighbors[k];var d=V.Tangent(p,surface.Cells[j].Position);double x=V.Dot(d,east),y=V.Dot(d,north),slope=(pressure[j]-pressure[i])/Math.Max(1,cell.DistanceM[k]);xx+=x*x;xy+=x*y;yy+=y*y;gx+=x*slope;gy+=y*slope;}
        double det=xx*yy-xy*xy;return east*((gx*yy-gy*xy)/det)+north*((gy*xx-gx*xy)/det);
    }
    double rawMax=0;for(int i=0;i<n;i++){double lat=Math.Asin(surface.Cells[i].Position.Z);rawMax=Math.Max(rawMax,((V)response.Invoke(null,new object[]{Gradient(i),lat,profile})).Length);}
    var places=new[]{("Delhi",28.61,77.21),("Kolkata",22.57,88.36),("Mumbai",19.08,72.88),("BayBengal",15d,87d),("ArabianSea",15d,66d),("Thar",27d,71d),("Tibet",32d,86d),("Sahara",24d,15d),("AustraliaInterior",-25d,134d),("NorthernAustralia",-15d,132d),("Congo",-1d,23d)};
    var result=places.Select(point=>{double lat=point.Item2*Math.PI/180,lon=point.Item3*Math.PI/180;int i=locator.Nearest(new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat)));var p=surface.Cells[i].Position;var east=new V(-p.Y,p.X,0).Unit;var north=V.Cross(p,east).Unit;var gradient=Gradient(i);var raw=(V)response.Invoke(null,new object[]{gradient,Math.Asin(p.Z),profile});return new{Name=point.Item1,AtmosphericCell=i,Latitude=Math.Asin(p.Z)*180/Math.PI,Longitude=Math.Atan2(p.Y,p.X)*180/Math.PI,ThermalPressurePa=pressure[i],GradientEastPaPerKm=V.Dot(gradient,east)*1000,GradientNorthPaPerKm=V.Dot(gradient,north)*1000,RawAgeEastMps=V.Dot(raw,east),RawAgeNorthMps=V.Dot(raw,north),ActualAgeEastMps=V.Dot(boundary[i],east)*5,ActualAgeNorthMps=V.Dot(boundary[i],north)*5,BasinEastMps=V.Dot(basin[i],east)*5,BasinNorthMps=V.Dot(basin[i],north)*5};});
    Directory.CreateDirectory(args[2]);
    object PressureCell(int i)=>new{Cell=i,Latitude=Math.Asin(surface.Cells[i].Position.Z)*180/Math.PI,Longitude=Math.Atan2(surface.Cells[i].Position.Y,surface.Cells[i].Position.X)*180/Math.PI,
        ThermalPressurePa=pressure[i],HeightM=world.Climate.AtmosphericElevationM[i],JuneTemperatureC=world.Climate.AtmosphericMonths[3].TemperatureTenthsC[i]/10.0,OceanFraction=sea[i]};
    File.WriteAllText(Path.Combine(args[2],"flow.json"),JsonSerializer.Serialize(new{Month="Peak northern summer circulation season=1",ExactFineOceanAggregation=true,RawGlobalMaximumMps=rawMax,AppliedGlobalScale=rawMax>6?6/rawMax:1,world.Recipe,Points=result,
        NorthernLandLows=Enumerable.Range(0,n).Where(i=>sea[i]<.5&&surface.Cells[i].Position.Z>0).OrderBy(i=>pressure[i]).Take(16).Select(PressureCell),
        AsiaLows=Enumerable.Range(0,n).Where(i=>sea[i]<.5&&Math.Asin(surface.Cells[i].Position.Z)*180/Math.PI>15&&Math.Atan2(surface.Cells[i].Position.Y,surface.Cells[i].Position.X)*180/Math.PI>45&&Math.Atan2(surface.Cells[i].Position.Y,surface.Cells[i].Position.X)*180/Math.PI<135).OrderBy(i=>pressure[i]).Take(16).Select(PressureCell)},json));
    foreach(string mode in new[]{"pressure","temperature"})
    {
        var svg=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1800' height='900' viewBox='0 0 1800 900'><rect width='1800' height='900' fill='#dddddd'/>");
        string F(double value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        for(int i=0;i<n;i++)
        {
            var p=surface.Cells[i].Position;double lon=Math.Atan2(p.Y,p.X)*180/Math.PI,lat=Math.Asin(p.Z)*180/Math.PI;
            double value=mode=="pressure"?Math.Clamp((pressure[i]+650)/1300,0,1):Math.Clamp((world.Climate.AtmosphericMonths[3].TemperatureTenthsC[i]/10.0+20)/60,0,1);
            int red=(int)Math.Round(255*value),blue=(int)Math.Round(255*(1-value));
            svg.Append("<circle cx='").Append(F((lon+180)*5)).Append("' cy='").Append(F((90-lat)*5)).Append("' r='7' fill='rgb(").Append(red).Append(",100,").Append(blue).Append(")'/>");
            if(mode=="pressure"&&i%33==0&&Math.Abs(lat)<70)
            {
                var east=new V(-p.Y,p.X,0).Unit;var north=V.Cross(p,east).Unit;double x=(lon+180)*5,y=(90-lat)*5,dx=V.Dot(boundary[i],east)*5*5,dy=-V.Dot(boundary[i],north)*5*5;
                svg.Append("<path d='M").Append(F(x)).Append(',').Append(F(y)).Append('L').Append(F(x+dx)).Append(',').Append(F(y+dy)).Append("' stroke='#111' stroke-width='1'/><circle cx='").Append(F(x+dx)).Append("' cy='").Append(F(y+dy)).Append("' r='1.7' fill='#111'/>");
            }
        }
        // Geography outlines from the saved atmospheric ocean mask.
        foreach(var edge in surface.Edges)if((sea[edge.A]>=.5)!=(sea[edge.B]>=.5)&&edge.TriangleA>=0&&edge.TriangleB>=0)
        {
            var a=surface.Corners[edge.TriangleA];var b=surface.Corners[edge.TriangleB];double x=(Math.Atan2(a.Y,a.X)*180/Math.PI+180)*5,y=(90-Math.Asin(a.Z)*180/Math.PI)*5,xx=(Math.Atan2(b.Y,b.X)*180/Math.PI+180)*5,yy=(90-Math.Asin(b.Z)*180/Math.PI)*5;
            if(Math.Abs(x-xx)<900)svg.Append("<path d='M").Append(F(x)).Append(',').Append(F(y)).Append('L').Append(F(xx)).Append(',').Append(F(yy)).Append("' stroke='#222' stroke-width='.7'/>");
        }
        svg.Append("</svg>");File.WriteAllText(Path.Combine(args[2],mode+".svg"),svg.ToString());
    }
    return;
}
throw new ArgumentException("Unknown experiment mode.");

void Report(World world,string folder)
{
    Directory.CreateDirectory(folder);var seasons=new[]{world.Climate.NorthernSpring,world.Climate.NorthernSummer,world.Climate.NorthernAutumn,world.Climate.NorthernWinter};
    var locations=new[]{("Delhi",28.61,77.21),("Kolkata",22.57,88.36),("Mumbai",19.08,72.88),("Chennai",13.08,80.27),("Sahara",24d,15d),("AustraliaInterior",-25d,134d),("Congo",-1d,23d),("Paris",48.85,2.35),("Berlin",52.52,13.4),("NorthChina",34.36,114.03),("Amazon",-4d,-60d)};
    double Lat(int i)=>Math.Asin(world.Surface.Cells[i].Position.Z)*180/Math.PI;
    double Lon(int i)=>Math.Atan2(world.Surface.Cells[i].Position.Y,world.Surface.Cells[i].Position.X)*180/Math.PI;
    var points=locations.Select(p=>{int i=Enumerable.Range(0,world.Surface.Cells.Length).MinBy(i=>Math.Pow(Lat(i)-p.Item2,2)+Math.Pow((Lon(i)-p.Item3)*Math.Cos(p.Item2*Math.PI/180),2));return new{Name=p.Item1,Cell=i,Latitude=Lat(i),Longitude=Lon(i),Height=world.ElevationM[i],Climate=world.Climate.Classification[i].ToString(),Rain=seasons.Sum(s=>s.PrecipitationMm[i]),Temperatures=seasons.Select(s=>s.TemperatureTenthsC[i]/10.0),QuarterRain=seasons.Select(s=>s.PrecipitationMm[i]),WindEast=seasons.Select(s=>s.WindEastMilli[i]/1000.0),WindNorth=seasons.Select(s=>s.WindNorthMilli[i]/1000.0),Humidity=seasons.Select(s=>s.MoisturePermille[i])};}).ToArray();
    var land=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]).ToArray();
    var report=new{world.Recipe,world.CoarseHash,world.Stages,Points=points,Residual=world.Climate.AtmosphericMonths.Select(m=>m.MoistureBudgetResidualPpm),MeanLandRain=land.Length==0?0:land.Average(i=>seasons.Sum(s=>s.PrecipitationMm[i])),Classes=land.GroupBy(i=>world.Climate.Classification[i]).OrderBy(g=>(int)g.Key).Select(g=>new{Climate=g.Key.ToString(),Cells=g.Count(),AreaKm2=g.Sum(i=>world.Surface.Cells[i].AreaM2)/1e6}),world.Metrics};
    File.WriteAllText(Path.Combine(folder,"report.json"),JsonSerializer.Serialize(report,json));
    // Diagnostic rendering of the actual barycentric dual, not new geography.
    var corners=Enumerable.Range(0,world.Surface.Cells.Length).Select(_=>new List<int>()).ToArray();
    for(int t=0;t<world.Surface.Triangles.Length;t+=3)for(int k=0;k<3;k++)corners[world.Surface.Triangles[t+k]].Add(t/3);
    var svg=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1800' height='900' viewBox='0 0 1800 900'><rect width='1800' height='900' fill='#102539'/>");
    string F(double v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
    string Color(Koppen k)=>k switch{Koppen.Ocean=>"#102539",Koppen.Af=>"#1040cc",Koppen.Am=>"#2080eb",Koppen.Aw=>"#59b8f2",Koppen.BWh=>"#ef331a",Koppen.BWk=>"#f79480",Koppen.BSh=>"#faa626",Koppen.BSk=>"#fad666",Koppen.ET=>"#adb3bd",Koppen.EF=>"#f0f7ff",_ when k<=Koppen.Cfc=>"#208c59",_=>"#397bb3"};
    for(int i=0;i<corners.Length;i++)
    {
        if(world.Hydrology.OceanConnected[i])continue;var p=world.Surface.Cells[i].Position;V east=new V(-p.Y,p.X,0).Unit,north=V.Cross(p,east).Unit;
        var ordered=corners[i].OrderBy(c=>Math.Atan2(V.Dot(world.Surface.Corners[c]-p,north),V.Dot(world.Surface.Corners[c]-p,east))).ToArray();
        foreach(int wrap in new[]{-1,0,1})
        {
            double center=(Lon(i)+180)*5;var xy=ordered.Select(c=>{var v=world.Surface.Corners[c];double x=(Math.Atan2(v.Y,v.X)*180/Math.PI+180)*5;while(x-center>900)x-=1800;while(x-center< -900)x+=1800;return(x+wrap*1800,(90-Math.Asin(v.Z)*180/Math.PI)*5);}).ToArray();
            if(xy.Length<3||xy.Max(v=>v.Item1)<0||xy.Min(v=>v.Item1)>1800)continue;
            string color=Color(world.Climate.Classification[i]);svg.Append("<path fill='").Append(color).Append("' stroke='").Append(color).Append("' stroke-width='.2' d='");
            for(int k=0;k<xy.Length;k++)svg.Append(k==0?'M':'L').Append(F(xy[k].Item1)).Append(',').Append(F(xy[k].Item2));svg.Append("Z'/>");
        }
    }
    svg.Append("</svg>");File.WriteAllText(Path.Combine(folder,"climate.svg"),svg.ToString());
}

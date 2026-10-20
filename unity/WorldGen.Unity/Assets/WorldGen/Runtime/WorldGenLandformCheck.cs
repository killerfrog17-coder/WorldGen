using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckLandforms(string output,string sourceHash)
    {
        Application.logMessageReceived+=(message,trace,kind)=>
        {if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        AudioListener.volume=0;play.Settings.HorizontalScale=.1;
        bool earth=world.Recipe.TerrainSource=="Earth";var sites=new List<Tuple<string,V>>();
        if(earth)
        {
            sites.Add(Tuple.Create("himalaya",TerrainMapViewport.FromDegrees(27.98,86.92)));
            sites.Add(Tuple.Create("tibetan-plateau",TerrainMapViewport.FromDegrees(32,88)));
            sites.Add(Tuple.Create("andes",TerrainMapViewport.FromDegrees(-32.65,-70.01)));
            sites.Add(Tuple.Create("appalachians",TerrainMapViewport.FromDegrees(38.5,-79.4)));
            sites.Add(Tuple.Create("texas-hills",TerrainMapViewport.FromDegrees(30.1,-98.4)));
            sites.Add(Tuple.Create("coast",TerrainMapViewport.FromDegrees(38.07,-122.96)));
        }
        else
        {
            foreach(var kind in new[]{FeatureKind.CollisionBelt,FeatureKind.Cordillera,FeatureKind.AncientOrogen})
            {
                var feature=world.Features.Where(f=>f.Kind==kind&&f.Cells.Length>0).OrderByDescending(f=>f.AmplitudeM).FirstOrDefault();
                if(feature!=null){int cell=feature.Cells.OrderByDescending(i=>world.ElevationM[i]).First();sites.Add(Tuple.Create(kind.ToString(),world.Surface.Cells[cell].Position));}
            }
            int flat=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>50&&world.ElevationM[i]<1200).OrderBy(i=>world.Ecology.Habitat[i].ReliefM).First();
            sites.Add(Tuple.Create("plain",world.Surface.Cells[flat].Position));
        }
        selectedCell=0;ExploreTerrain(sites[0].Item2);double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.Ready)throw new Exception("Landform sampler did not load.");
        var explorer=terrainExplorer;var terrain=localTerrainTask.Result;
        var river=terrain.Hydrology.Channels.Where(c=>c.WidthM>40&&c.WidthM<300&&c.StartM>30&&c.StartM<1500).OrderByDescending(c=>c.LengthM).First();
        sites.Add(Tuple.Create("river",river.Position(.5)));
        var lake=terrain.Hydrology.Shores.Where(f=>f.Kind==ShoreKind.Lake&&f.ScaleM*.1>500&&f.ScaleM*.1<10000).OrderByDescending(f=>f.AreaM2).FirstOrDefault();
        if(lake!=null)sites.Add(Tuple.Create("lake",lake.Boundary(0,terrain.RadiusM)));
        var report=new List<string>();var probes=new List<string>{"originX,originY,originZ,eastM,northM,elevationM"};
        foreach(var site in sites)
        {
            explorer.Teleport(site.Item2);explorer.TestView(80,site.Item1=="coast"?270:20,8);
            if(site.Item1=="coast")explorer.TestNearbyShore();
            PlayCommands.Execute(play,0,"/weather clear",weather:explorer.WeatherControl);
            TerrainMapViewport.Degrees(explorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            deadline=Time.realtimeSinceStartupAsDouble+180;
            while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!explorer.AllReady||explorer.Error!="")throw new Exception("Landform tiles failed: "+site.Item1+" "+explorer.Error);
            yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,site.Item1+".png"));
            report.Add(site.Item1+" "+lat.ToString("F6")+","+lon.ToString("F6")+": "+explorer.Diagnostics);probes.AddRange(explorer.Probes());
            explorer.TestView(site.Item1=="lake"?1500:700,20,65);
            deadline=Time.realtimeSinceStartupAsDouble+90;while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,site.Item1+"-above.png"));
            if(site.Item1=="river")
            {
                var sample=terrain.Patch(site.Item2).Sample(0,0);if(!sample.Underwater)throw new Exception("Native river has no water over its bed.");
                explorer.TestView(6,20,4);deadline=Time.realtimeSinceStartupAsDouble+90;while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,"river-near.png"));
                report.Add("River width "+river.WidthM.ToString("F2")+"m; level "+sample.WaterSurfaceM.ToString("F3")+"m; bed "+sample.ElevationM.ToString("F3")+"m");
            }
            File.WriteAllLines(Path.Combine(output,"landforms.txt"),report);
        }
        if(StateHash.Compute(world)!=sourceHash)throw new Exception("Local landforms changed the source world.");
        report.Add("PASS source unchanged "+sourceHash);File.WriteAllLines(Path.Combine(output,"landforms.txt"),report);
        File.WriteAllLines(Path.Combine(output,"probes.csv"),probes);explorer.Close();Application.Quit(0);
    }
}

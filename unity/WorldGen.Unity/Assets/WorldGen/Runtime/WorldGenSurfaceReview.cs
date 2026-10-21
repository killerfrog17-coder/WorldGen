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
    IEnumerator CheckSurfaceReview(string output,string hash)
    {
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var options=TerrainVideoOptions.Current;options.ViewDistanceM=30000;options.Quality=2;options.Effort=1;options.FullDetailTiles=1.5;TerrainVideoOptions.Revision++;
        play.Settings.HorizontalScale=.1;
        int mountain=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>500&&world.ElevationM[i]<3000).OrderByDescending(i=>world.Ecology.Habitat[i].ReliefM).First();
        int plain=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>50&&world.ElevationM[i]<800).OrderBy(i=>world.Ecology.Habitat[i].ReliefM).First();
        ExploreTerrain(world.Surface.Cells[mountain].Position);double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var e=terrainExplorer;if(e==null||!e.Ready)throw new Exception("Surface explorer unavailable");
        var report=new List<string>();var probes=new List<string>{"originX,originY,originZ,eastM,northM,height"};
        foreach(var site in new[]{("mountain",mountain),("plain",plain)})
        {
            e.Teleport(world.Surface.Cells[site.Item2].Position);e.TestView(3,20,8);
            PlayCommands.Execute(play,0,"/weather clear",weather:e.WeatherControl);
            TerrainMapViewport.Degrees(e.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            e.BeginLoadingProfile();double started=Time.realtimeSinceStartupAsDouble;deadline=started+60;
            yield return null;while(!e.NearReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!e.NearReady||e.Error!="")throw new Exception("Surface near readiness: "+e.Error);
            report.Add(site.Item1+" near "+(Time.realtimeSinceStartupAsDouble-started).ToString("F3")+"s; "+e.Diagnostics);
            yield return new WaitForSecondsRealtime(3.2f);
            e.Capture(Path.Combine(output,site.Item1+"-near.png"));
            report.Add(e.EndLoadingProfile());probes.AddRange(e.Probes());
            e.TestView(80,20,35);yield return null;e.Capture(Path.Combine(output,site.Item1+"-above.png"));
        }
        if(StateHash.Compute(world)!=hash)throw new Exception("Surface changed authoritative world/history");
        report.Add("PASS source "+hash);File.WriteAllLines(Path.Combine(output,"surface-review.txt"),report);File.WriteAllLines(Path.Combine(output,"probes.csv"),probes);
        e.Close();Application.Quit(0);
    }
}

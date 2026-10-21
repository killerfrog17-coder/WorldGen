using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckStreamingReuse(string output,string hash)
    {
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var session=WorldLibrary.ReadPlaySession(Argument("--worldgen-local-terrain-check",""));play.Settings.HorizontalScale=.1;
        ExploreTerrain(session.PlayerPosition);double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var e=terrainExplorer;if(e==null||!e.Ready)throw new Exception("Streaming explorer not ready");
        e.TestView(120,20,12);var report=new List<string>();
        foreach(double x in new[]{0d,500d,0d})
        {
            e.TestMove(x,0);double started=Time.realtimeSinceStartupAsDouble;deadline=started+30;yield return null;
            while(!e.NearReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!e.NearReady||e.Error!="")throw new Exception("Near loading: "+e.Error);
            report.Add("Near at "+x+"m: "+(Time.realtimeSinceStartupAsDouble-started).ToString("F3")+"s; cache hits "+e.TerrainCacheHits+", retained "+e.CachedTerrainTiles+" / "+e.CachedTerrainBytes+" bytes; requests "+e.TerrainBuildRequests);
            File.WriteAllLines(Path.Combine(output,"streaming-reuse.txt"),report);
            if(x==500){double settle=Time.realtimeSinceStartupAsDouble+1;while(Time.realtimeSinceStartupAsDouble<settle)yield return null;}
        }
        if(e.TerrainCacheHits==0)report.Add("Return still covered by resident products; no inactive-cache take needed in this route. Separate GPU cache check exercises take/restore.");
        if(e.CachedTerrainBytes>96L*1024*1024||e.CachedTerrainTiles>128)throw new Exception("Terrain retention exceeds budget");
        e.TestStreamingVelocity(-700,0);deadline=Time.realtimeSinceStartupAsDouble+3;
        while(e.TerrainPrefetchRequests==0&&Time.realtimeSinceStartupAsDouble<deadline){e.TestStreamingVelocity(-700,0);yield return null;}
        if(e.TerrainPrefetchRequests==0)throw new Exception("Predictive terrain did not enter spare worker lane");
        report.Add("Predictive requests "+e.TerrainPrefetchRequests+"; "+e.Diagnostics);
        e.Capture(Path.Combine(output,"returned-terrain.png"));
        File.WriteAllLines(Path.Combine(output,"probes.csv"),e.Probes());
        if(StateHash.Compute(world)!=hash)throw new Exception("Streaming changed source world/history");
        report.Add("PASS exact source "+hash);File.WriteAllLines(Path.Combine(output,"streaming-reuse.txt"),report);
        e.Close();Application.Quit(0);
    }
}

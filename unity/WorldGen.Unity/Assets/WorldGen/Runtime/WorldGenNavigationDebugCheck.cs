using System;
using System.Collections;
using System.IO;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckNavigationDebug(string output,string hash)
    {
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        ExploreTerrain();double deadline=Time.realtimeSinceStartupAsDouble+90;
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var e=terrainExplorer;if(e==null||!e.Ready)throw new Exception("Explorer not ready");
        var result=e.ExecuteExplorerCommand("/tp 10 20 120");
        if(!result.Success)throw new Exception(string.Join(";",result.Lines));
        TerrainMapViewport.Degrees(e.Position,out double lat,out double lon);
        if(Math.Abs(lat-10)>.00001||Math.Abs(play.OfficialLongitude(lon)-20)>.00001||e.HeightAboveGround<119.99)throw new Exception("Native coordinate teleport disagrees with official coordinates/height");
        if(!e.TestDebugText().Contains("Facing "))throw new Exception("F3 bearing absent");
        var position=e.Position;if(e.ExecuteExplorerCommand("/tp NaN 0").Success||(e.Position-position).Length>1e-10)throw new Exception("Invalid coordinate moved player");
        if(!e.ExecuteExplorerCommand("/tp random land").Success)throw new Exception("Random land command failed");
        var local=localTerrainTask.Result;if(local.Patch(e.Position,2000).Sample(e.Position).Underwater)throw new Exception("Random land destination is underwater");
        if(!e.ExecuteExplorerCommand("/tp random").Success)throw new Exception("Random globe command failed");
        e.TestRiverMap(true);deadline=Time.realtimeSinceStartupAsDouble+30;
        while((!e.RiverMapReady||!e.MapReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!e.RiverMapReady||!e.MapReady||e.Error!="")throw new Exception("River map: "+e.Error);
        e.CaptureRiverMap(Path.Combine(output,"waterways.png"));int count=e.MapBuildCount;e.TestRiverMap(false);yield return null;
        if(e.MapBuildCount!=count)throw new Exception("River toggle rebuilt base map");
        if(StateHash.Compute(world)!=hash)throw new Exception("Debug navigation changed world/history");
        File.WriteAllText(Path.Combine(output,"navigation-debug.txt"),"PASS actual explorer coordinate/invalid/random/random-land teleports, geographic F3 direction, asynchronous waterways map and independent toggle; unchanged "+hash);
        e.Close();Application.Quit(0);
    }
}

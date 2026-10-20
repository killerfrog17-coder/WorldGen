using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckTerrainHandoffs(string output,string sourceHash)
    {
        Application.logMessageReceived+=(message,trace,kind)=>
        {if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        play.Settings.HorizontalScale=.1;
        bool earth=world.Recipe.TerrainSource=="Earth";
        var location=earth?TerrainMapViewport.FromDegrees(50,10):
            world.Surface.Cells[Enumerable.Range(0,world.Surface.Cells.Length)
                .Where(i=>world.ElevationM[i]>20&&world.ElevationM[i]<1200)
                .OrderBy(i=>world.Ecology.Habitat[i].ReliefM).First()].Position;
        selectedCell=0;ExploreTerrain(location);
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while(terrainExplorer!=null&&(!terrainExplorer.Ready||!terrainExplorer.AllReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.AllReady)throw new Exception("Initial terrain did not become resident.");
        var explorer=terrainExplorer;var report=new List<string>();var frames=new List<double>();
        PlayCommands.Execute(play,0,"/weather clear",weather:explorer.WeatherControl);
        TerrainMapViewport.Degrees(explorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
        explorer.TestView(450,90,8);
        deadline=Time.realtimeSinceStartupAsDouble+120;
        while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        explorer.Capture(Path.Combine(output,"flight-start.png"));
        int seen=explorer.ReanchorCount,checks=0;double started=Time.realtimeSinceStartupAsDouble;
        deadline=started+100;double nextProgress=started;
        while(explorer.ReanchorCount<3&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            double before=Time.realtimeSinceStartupAsDouble;
            explorer.TestAdvanceEast(700*Math.Min(.05,Time.unscaledDeltaTime));
            yield return null;
            frames.Add(Time.realtimeSinceStartupAsDouble-before);
            if(!explorer.TerrainVisible||!explorer.CenterReady)throw new Exception("Visible terrain coverage disappeared during flight.");
            if(explorer.Error!="")throw new Exception(explorer.Error);
            if(Time.realtimeSinceStartupAsDouble>=nextProgress)
            {nextProgress=Time.realtimeSinceStartupAsDouble+2;File.WriteAllText(Path.Combine(output,"progress.txt"),"crossings "+explorer.ReanchorCount+"; distance "+explorer.ReanchorDistance+"; "+explorer.TestHandoffDiagnostics());}
            if(explorer.ReanchorCount!=seen)
            {
                seen=explorer.ReanchorCount;checks++;
                yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,"flight-handoff-"+seen+".png"));
                report.Add("Flight handoff "+seen+": "+explorer.Diagnostics+"; prepared "+explorer.PreparedTileCount+"; ground clearance "+explorer.HeightAboveGround.ToString("F3")+"m");
            }
        }
        if(checks<3)throw new Exception("Three flying reanchors did not complete; distance "+explorer.ReanchorDistance+"; pending "+explorer.PreparedTileCount);
        frames.Sort();report.Add("Flight frames "+frames.Count+"; median "+(frames[frames.Count/2]*1000).ToString("F2")+"ms; p95 "+(frames[(int)(frames.Count*.95)]*1000).ToString("F2")+"ms; worst "+(frames.Last()*1000).ToString("F2")+"ms");
        File.WriteAllLines(Path.Combine(output,"handoffs.txt"),report);
        explorer.Teleport(location);explorer.TestBoundaryGround();explorer.TestView(1.65,90,0);explorer.TestWalking();
        deadline=Time.realtimeSinceStartupAsDouble+120;
        while((!explorer.AllReady||explorer.PreparedTileCount<100)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        explorer.TestHoldHandoff=true;
        int walkedAt=explorer.ReanchorCount;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();explorer.TestKeyboard(keyboard);
        InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));
        deadline=Time.realtimeSinceStartupAsDouble+30;
        double worstClearance=0;
        while(!explorer.TestHandoffWaiting&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            yield return null;
            if(!explorer.TerrainVisible||!explorer.CenterReady)throw new Exception("Visible terrain disappeared during walking.");
            worstClearance=Math.Max(worstClearance,Math.Abs(explorer.RenderedFootClearance-1.65));
            if(Time.realtimeSinceStartupAsDouble>=nextProgress)
            {nextProgress=Time.realtimeSinceStartupAsDouble+2;File.WriteAllText(Path.Combine(output,"progress.txt"),"walking; distance "+explorer.ReanchorDistance+"/"+explorer.ReanchorLimit+"; "+explorer.TestHandoffDiagnostics());}
        }
        // Read the readiness result before yielding: the explorer refreshes
        // this diagnostic flag on its next Update, independently of this coroutine.
        if(!explorer.TestHandoffWaiting)throw new Exception("Walking replacement did not become ready: "+explorer.TestHandoffDiagnostics());
        InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
        InputSystem.RemoveDevice(keyboard);explorer.TestKeyboard(null);
        yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,"walk-before.png"));
        var beforePosition=explorer.Position;
        explorer.TestHoldHandoff=false;deadline=Time.realtimeSinceStartupAsDouble+5;
        while(explorer.ReanchorCount==walkedAt&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(explorer.ReanchorCount==walkedAt)throw new Exception("Actual walking did not cross the prepared boundary.");
        if(D.Angle(beforePosition,explorer.Position)*637100>.03)throw new Exception("Prepared handoff moved the player geographically.");
        yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,"walk-after.png"));
        double afterClearance=explorer.RenderedFootClearance;
        double scatterError=explorer.TestPreparedScatterError();report.Add("Prepared scatter matches ordinary placement: maximum matrix difference "+scatterError.ToString("G5"));
        if(scatterError>1e-5)throw new Exception("Prepared scatter changed ordinary placement: "+scatterError);
        report.Add("Walking across boundary: worst rendered eye-clearance error "+worstClearance.ToString("F4")+"m; after "+afterClearance.ToString("F4")+"m; analytic "+explorer.HeightAboveGround.ToString("F4")+"m");
        if(worstClearance>.12||Math.Abs(afterClearance-1.65)>.12)throw new Exception("Player/rendered-ground alignment changed across handoff: "+worstClearance+" / "+afterClearance);
        report.Add("Largest atomic handoff "+explorer.LargestHandoffMs.ToString("F3")+"ms; largest mesh upload "+explorer.LargestUploadMs.ToString("F3")+"ms; "+explorer.LargestHandoffProfile);
        File.WriteAllLines(Path.Combine(output,"handoffs.txt"),report);
        if(explorer.LargestHandoffMs>40)throw new Exception("Main-thread handoff exceeded40ms: "+explorer.LargestHandoffMs);
        if(StateHash.Compute(world)!=sourceHash)throw new Exception("Terrain handoff changed source world.");
        report.Add("PASS three700m/s flight crossings and actual walking crossing at0.1x; no empty terrain; unchanged source "+sourceHash);
        File.WriteAllLines(Path.Combine(output,"handoffs.txt"),report);
        explorer.Close();yield return null;Application.Quit(0);
    }
}

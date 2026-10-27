using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckDeathPrototype(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("DEATH CHECK "+text);report.Add("PASS "+text);}
        ExecuteExplorerCommand("/gamemode survival");Pause();TestNearbyFlatGround();TestWalking();Vitals.Restore();
        void Fall(double height)
        {
            motion.Y=Ground(motion.X,motion.Z)+TerrainMotion.EyeHeightM+height;motion.Grounded=false;motion.VerticalSpeed=0;
            double impact=0;
            for(int i=0;i<1200&&!motion.Grounded;i++){motion.Move(1d/60,0,0,0,heading,0,false,false,Ground);impact=Math.Max(impact,motion.LandingSpeedMps);}
            playerDeathVelocity=Vector3.down*(float)impact;
            PlayerDamageRules.Apply(Vitals,Mode,PlayerDamageRules.FallDamage(impact),PlayerDeathCause.Fall);UpdatePlayerDeath();UpdateCamera();
        }
        Fall(5);Check(Vitals.Health>84&&Vitals.Health<85&&!DeathVisible,"five-metre fall hurts but is survivable");
        Fall(12);Check(DeathVisible&&Vitals.DeathCause==PlayerDeathCause.Fall&&playerRagdoll!=null&&playerRagdoll.BodyCount==13,"fatal impact starts native-bone ragdoll and death screen");
        yield return new WaitForSecondsRealtime(2);yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"death-screen.png"),image.EncodeToPNG());Destroy(image);
        Check(!humanHideBody&&!WantsMouseLook&&!paused&&Cursor.lockState==CursorLockMode.None,"dead body visible, cursor released, pause cannot obscure death screen");
        var before=Position;RespawnPlayer();yield return null;
        Check(!DeathVisible&&playerRagdoll==null&&Vitals.Health==100&&Vitals.Hunger==0&&Vitals.Thirst==0&&!motion.Flying&&!motion.NoClip,"respawn removes physics and restores survival vitals");
        Check((Position-before).Length>.001&&!walkingPatch.Sample(Position).Underwater,"respawn teleports to different dry land");
        Check(humanPlayer.Animator.enabled&&humanPlayer.Graph.IsPlaying(),"respawn resumes normal native animation");
        Pause();
        foreach(var cause in new[]{PlayerDeathCause.Starvation,PlayerDeathCause.Dehydration})
        {
            Vitals.Restore();Vitals.Health=.1;Vitals.Hunger=cause==PlayerDeathCause.Starvation?100:0;Vitals.Thirst=cause==PlayerDeathCause.Dehydration?100:0;
            SurvivalRules.Advance(Vitals,30,60,new SurvivalEnvironment{AirTemperatureC=22,Humidity=.5},0,false);UpdatePlayerDeath();
            Check(DeathVisible&&Vitals.DeathCause==cause&&playerRagdoll!=null,"depleted need reaches ragdoll death: "+cause);
            RespawnPlayer();yield return null;Pause();
        }
        Vitals.CoreTemperatureC=29;SurvivalRules.Advance(Vitals,.01,60,new SurvivalEnvironment{AirTemperatureC=-20},0,false);UpdatePlayerDeath();
        Check(DeathVisible&&Vitals.DeathCause==PlayerDeathCause.Cold,"existing cold death uses same screen");
    }
    public void CheckDeathExit(string destination)=>ExitPlayerDeath(destination);
}

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckPlayerDeathFlow()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        var report=new List<string>();var errors=new List<string>();
        Application.LogCallback listener=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception)errors.Add(message);};Application.logMessageReceived+=listener;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("DEATH FLOW "+text);report.Add("PASS "+text);}
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((Busy||!worldReady||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(worldReady&&!Busy&&humans!=null,"fixture loaded through existing world pipeline");
        TerrainVideoOptions.Current.ViewDistanceM=1000;TerrainVideoOptions.Revision++;
        RequestTerrain();deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.CenterReady,"local terrain and player ready");
        yield return terrainExplorer.CheckDeathPrototype(output,report);
        terrainExplorer.CheckDeathExit("Simulation");yield return null;
        Check(terrainExplorer==null&&screen=="Simulation"&&play.Survival.Dead&&cameraView.enabled,"death exit returns to simulation, captures dead session and restores world camera");
        RequestTerrain();deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;yield return null;
        Check(terrainExplorer!=null&&terrainExplorer.DeathVisible,"re-entering a dead saved session restores death screen");
        terrainExplorer.CheckDeathExit("Menu");yield return null;yield return null;
        Check(terrainExplorer==null&&screen=="Menu"&&cameraView.enabled,"death exit returns to main menu and unloads explorer");
        Application.logMessageReceived-=listener;Check(errors.Count==0,"no runtime errors or exceptions: "+string.Join(" | ",errors));
        File.WriteAllLines(Path.Combine(output,"runtime-checks.txt"),report);Debug.Log("PLAYER DEATH FLOW PASS "+report.Count);Application.Quit(0);
    }
}

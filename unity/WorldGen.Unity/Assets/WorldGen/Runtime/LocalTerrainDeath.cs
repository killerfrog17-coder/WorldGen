using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    WorldGenHumanRagdoll playerRagdoll;
    bool deathPresented;Action<string> deathExit;
    bool respawnWaiting;string deathNotice="";
    Vector3 playerDeathVelocity;Vector3 deathCameraPosition;
    public bool DeathVisible=>SurvivalActive&&Vitals.Dead;
    void UpdatePlayerDeath()
    {
        if(!DeathVisible){if(deathPresented)ClearPlayerDeath();return;}
        if(deathPresented)return;deathPresented=true;
        paused=mapOpen=ecologyOpen=chatOpen=false;deathNotice="";
        if(humanPlayer!=null)
        {
            // Pose the existing skin at the final motor position before handing
            // its bones to physics. The original asset and weights stay intact.
            humanPlayer.Root.transform.localPosition=new Vector3((float)(motion.X-originX),(float)(motion.Y-TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
            humanPlayer.Graph.Stop();humanPlayer.Animator.enabled=false;
            playerRagdoll=gameObject.AddComponent<WorldGenHumanRagdoll>();
            playerRagdoll.Begin(humanPlayer.Root,(x,z)=>(float)Ground(x+originX,z+originZ),playerDeathVelocity);
            foreach(var renderer in humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.updateWhenOffscreen=true;
        }
        humanHideBody=false;humanReviewBodyMask=-1;
        var pivot=DeathCameraTarget();deathCameraPosition=pivot+Quaternion.Euler(0,heading,0)*new Vector3(0,1.5f,-3.4f);
        motion.VerticalSpeed=0;survivalSprinting=false;CaptureMouse();
    }
    Vector3 DeathCameraTarget()=>playerRagdoll!=null?playerRagdoll.Center:humanPlayer!=null?humanPlayer.Root.transform.localPosition+Vector3.up*.7f:new Vector3((float)(motion.X-originX),(float)motion.Y-.6f,(float)(motion.Z-originZ));
    void UpdateDeathCamera()
    {
        humanHideBody=false;var target=DeathCameraTarget();
        var cameraPosition=deathCameraPosition;cameraPosition.y=Mathf.Max(cameraPosition.y,(float)Ground(cameraPosition.x+originX,cameraPosition.z+originZ)+.35f);
        view.transform.localPosition=cameraPosition;view.transform.rotation=Quaternion.LookRotation(target-view.transform.position,Vector3.up);
    }
    void ClearPlayerDeath()
    {
        if(playerRagdoll!=null){playerRagdoll.enabled=false;Destroy(playerRagdoll);playerRagdoll=null;}
        if(deathPresented&&humanPlayer!=null)
        {
            int variant=humanPlayer.Definition.Variant[0]-'A';DestroyHuman(humanPlayer);humanPlayer=CreateHuman(variant,1);
            humanPlayer.Yaw=humanPlayer.TargetYaw=heading;
        }
        deathPresented=false;playerDeathVelocity=Vector3.zero;humanCameraProbeAt=0;
    }
    void RespawnPlayer()
    {
        if(!Ready)return;
        V target=Position;bool found=false;
        // Prefer random dry, moderately sloped land. Retry on coasts/lakes;
        // never interpret a failed random sample as permission to use ocean.
        for(int attempt=0;attempt<512;attempt++)
        {
            var point=ExplorerNavigation.RandomPoint(ref teleportRandom);
            if(sourceWorld!=null&&sourceWorld.Hydrology.OceanConnected[terrain.NearestCell(point)])continue;
            var patch=terrain.Patch(point,2000);var sample=patch.Sample(point);
            if(sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.1)continue;
            var local=new LocalTerrainFrame(point,terrain.RadiusM,sample.ElevationM,HorizontalScale);
            double a=patch.Elevation(local.Position(2,0)),b=patch.Elevation(local.Position(0,2));
            if(Math.Abs(a-sample.ElevationM)>1.2||Math.Abs(b-sample.ElevationM)>1.2)continue;
            target=point;found=true;break;
        }
        if(!found)
        {
            // A fully water-covered generated world has no valid land respawn.
            // Keep the death screen usable instead of reviving underwater.
            deathNotice="No dry land found. Try again or return to world simulation.";return;
        }
        ClearPlayerDeath();Vitals.Restore();motion.Flying=motion.NoClip=false;
        Teleport(target);ApplyPlayerMode();respawnWaiting=true;humanLastX=motion.X;humanLastZ=motion.Z;UpdateHumans();UpdateCamera();
        if(playState!=null)StorePlayer(playState);CaptureMouse();
    }
    void ExitPlayerDeath(string destination)
    {if(deathExit!=null)deathExit(destination);else Close();}
    void DrawPlayerDeath()
    {
        var old=GUI.color;GUI.color=new Color(0,0,0,.5f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
        float width=Mathf.Min(360,Screen.width-40),height=deathNotice.Length>0?310:270;var rect=new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height);
        GUILayout.BeginArea(rect);var title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=38,fontStyle=FontStyle.Bold};
        GUILayout.Label("You died",title,GUILayout.Height(70));GUILayout.Space(20);
        if(GUILayout.Button("Respawn",GUILayout.Height(42)))RespawnPlayer();GUILayout.Space(8);
        if(GUILayout.Button("Back to world simulation",GUILayout.Height(42)))ExitPlayerDeath("Simulation");GUILayout.Space(8);
        if(GUILayout.Button("Back to main menu",GUILayout.Height(42)))ExitPlayerDeath("Menu");
        if(deathNotice.Length>0)GUILayout.Label(deathNotice);
        GUILayout.EndArea();
    }
}

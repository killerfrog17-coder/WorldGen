using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckThrowLatency(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("THROW LATENCY "+text);report.Add("PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();TestHumanView(0);
        SetInventoryOpen(false);paused=true;CancelItemCharge();ClearInventoryPhysics();
        foreach(var root in inventoryVisuals.Values)DestroyInventoryVisual(root);
        inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();
        var timing=new List<string>();
        // Compare the former eager fallback with identical resident heights.
        var watch=Stopwatch.StartNew();var expected=new double[81];
        for(int z=0;z<9;z++)for(int x=0;x<9;x++)
        {double px=motion.X+(x-4)*.5,pz=motion.Z+(z-4)*.5;expected[z*9+x]=VisibleGround(px,pz,Ground(px,pz));}
        double eagerMs=watch.Elapsed.TotalMilliseconds;int before=surfaceGroundFallbacks;watch.Restart();
        for(int z=0;z<9;z++)for(int x=0;x<9;x++)
            Check(Math.Abs(expected[z*9+x]-SurfaceGround(motion.X+(x-4)*.5,motion.Z+(z-4)*.5))<1e-8,"Resident collision height matches previous sample "+(z*9+x));
        timing.Add("81 eager terrain samples ms="+eagerMs.ToString("F3")+"; resident ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
        Check(surfaceGroundFallbacks==before,"Resident collision samples perform no analytic terrain fallback");
        var ray=new Ray(transform.TransformPoint(new Vector3((float)(motion.X-originX),(float)motion.Y+1000,(float)(motion.Z-originZ))),transform.TransformDirection(Vector3.forward));
        before=surfaceGroundFallbacks;watch.Restart();var obstruction=TraceCombatObstacle(ray,120);
        timing.Add("Cold 120m aim trace ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
        Check(obstruction==null,"Long clear aim ray is not obstructed by ground below it");
        Check(surfaceGroundFallbacks==before,"Long resident aim trace performs no analytic terrain fallback");
        before=surfaceGroundFallbacks;watch.Restart();EnsureDropFloors(new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ)),2.5f);
        timing.Add("Cold projectile floor creation ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
        Check(inventoryFloors.Count>0&&surfaceGroundFallbacks==before,"Cold projectile collision floors use resident triangles without analytic regeneration");
        foreach(var floor in inventoryFloors.Values)
        {var p=floor.Mesh.vertices[0];Check(Math.Abs(p.y-SurfaceGround(p.x+originX,p.z+originZ))<.001,"Projectile floor matches rendered ground");}
        foreach(var kind in new[]{PossessionKind.WoodenSpear,PossessionKind.StoneTippedSpear,PossessionKind.BoneTippedSpear})
        {
            CancelItemCharge();ClearInventoryPhysics();combatGlobalReady=combatRightReady=combatLeftReady=0;
            var spear=PortableTechnology.Create(kind,Position);Inventory.Items.Add(spear);
            Check(Inventory.Move(spear.Id,PossessionPlace.RightHand,"",0,0,false,out _),"Equip "+kind);
            UpdateHumans();UpdateCamera();UpdateInventoryVisuals();
            BeginItemCharge(spear,false,1,true);Check(itemCharge!=null,"Begin actual wind-up "+kind);
            itemCharge.Started-=1;before=surfaceGroundFallbacks;watch.Restart();ReleaseItemCharge();ResolveRangedRelease(Time.unscaledTimeAsDouble+5);
            timing.Add(kind+" complete release ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
            Check(spear.InFlight&&itemProjectiles.ContainsKey(spear.Id)&&inventoryDrops.ContainsKey(spear.Id),"Release creates recoverable physical projectile "+kind);
            Check(surfaceGroundFallbacks==before,"Actual spear release has no analytic aim/floor sampling "+kind);
            var projectile=itemProjectiles[spear.Id];
            Check(projectile.Drop.Body.linearVelocity.magnitude>5&&projectile.Drop.Body.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic,"Spear retains flight speed and continuous collision "+kind);
            paused=false;yield return new WaitForFixedUpdate();yield return null;paused=true;
            Check(Inventory.Find(spear.Id)==spear,"Projectile ownership survives actual physics step "+kind);
            Inventory.Move(spear.Id,PossessionPlace.Ground,"",0,0,false,out _);
        }
        // Missing presentation data must still have a valid fallback.
        before=surfaceGroundFallbacks;double missing=SurfaceGround(motion.X+10000,motion.Z+10000);
        Check(!double.IsNaN(missing)&&!double.IsInfinity(missing)&&surfaceGroundFallbacks==before+1,"Unloaded terrain retains the analytic safety fallback");
        Ground(motion.X,motion.Z);ClearInventoryPhysics();CancelItemCharge();
        File.WriteAllLines(Path.Combine(output,"timing.txt"),timing);
    }
}

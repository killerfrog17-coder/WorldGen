using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

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
        File.WriteAllLines(Path.Combine(output,"timing.txt"),timing);
        Check(inventoryFloors.Count>0&&surfaceGroundFallbacks==before,"Cold projectile collision floors use resident triangles without analytic regeneration");
        foreach(var floor in inventoryFloors.Values)
        {var p=floor.Mesh.vertices[0];Check(Math.Abs(p.y-SurfaceGround(p.x+originX,p.z+originZ))<.001,"Projectile floor matches rendered ground");}
        foreach(var kind in new[]{PossessionKind.WoodenSpear,PossessionKind.StoneTippedSpear,PossessionKind.BoneTippedSpear})
        {
            CancelItemCharge();ClearInventoryPhysics();combatGlobalReady=combatRightReady=combatLeftReady=0;
            Possession spear;
            if(kind==PossessionKind.WoodenSpear){ExecuteGive(new[]{"/give","wooden","spear"});spear=Inventory.QuickHeld;}
            else {spear=PortableTechnology.Create(kind,Position);Inventory.Items.Add(spear);}
            Check(Inventory.Move(spear.Id,PossessionPlace.RightHand,"",0,0,false,out _),"Equip "+kind);
            UpdateHumans();UpdateCamera();UpdateInventoryVisuals();
            BeginItemCharge(spear,false,1,true);Check(itemCharge!=null,"Begin actual wind-up "+kind);
            itemCharge.Started-=1;before=surfaceGroundFallbacks;watch.Restart();ReleaseItemCharge();ResolveRangedRelease(Time.unscaledTimeAsDouble+5);
            timing.Add(kind+" complete release ms="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
            File.WriteAllLines(Path.Combine(output,"timing.txt"),timing);
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
        Vitals.Restore();PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();combatGlobalReady=combatRightReady=combatLeftReady=0;
        ExecuteGive(new[]{"/give","wooden","spear"});var held=Inventory.QuickHeld;double stamina=Vitals.Stamina;
        BeginItemCharge(held,false,1,true);
        Check(itemCharge!=null&&stamina-Vitals.Stamina<.5,"Actual survival spear wind-up has a small stamina cost");
        itemCharge.Started-=1;stamina=Vitals.Stamina;ReleaseItemCharge();
        Check(pendingRanged!=null&&stamina-Vitals.Stamina<1.5,"Actual full-power spear throw uses under1.5 stamina points");
        CancelItemCharge();Vitals.Restore();paused=false;UpdatePlayerSurvival(0,0);UpdateSurvivalHud();paused=true;
        var previousMode=survivalHudCanvas.renderMode;var previousCamera=survivalHudCanvas.worldCamera;int mask=view.cullingMask;bool arms=viewArmsVisible;viewArmsVisible=false;
        survivalHudCanvas.renderMode=RenderMode.ScreenSpaceCamera;survivalHudCanvas.worldCamera=view;survivalHudCanvas.planeDistance=.15f;survivalHudCanvas.enabled=true;view.cullingMask|=1<<5;
        Color32[] HudImage(string name)
        {
            survivalHud.SetValues(SurvivalHudValues.From(Vitals,22),true);survivalHud.SetAllDirty();Canvas.ForceUpdateCanvases();
            var active=RenderTexture.active;var prior=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            Destroy(image);RenderTexture.active=active;view.targetTexture=prior;target.Release();Destroy(target);return pixels;
        }
        try
        {
            var fresh=HudImage("hud-fed-hydrated");Vitals.FoodNeedDays=1;Vitals.WaterDeficitLitres=1;
            var half=HudImage("hud-hungry-thirsty");var values=survivalHud.Displayed;
            Check(Math.Abs(values.Fullness-.5)<.001&&Math.Abs(values.Hydration-.5)<.001,"Actual HUD binds half-full icons to one foodless day and noticeable thirst");
            Vitals.ReserveKcal=0;Vitals.Health=10;Vitals.WaterDeficitLitres=7.4;var critical=HudImage("hud-critical-needs");
            Check(survivalHud.Displayed.Fullness<.01&&survivalHud.Displayed.Hydration<.02,"Actual HUD reserves empty-looking icons for critical deprivation");
            int halfChanged=0,criticalChanged=0;for(int y=0;y<260;y++)for(int x=1000;x<1280;x++){int i=y*1280+x;if(Math.Abs(fresh[i].r-half[i].r)+Math.Abs(fresh[i].g-half[i].g)+Math.Abs(fresh[i].b-half[i].b)>20)halfChanged++;if(Math.Abs(half[i].r-critical[i].r)+Math.Abs(half[i].g-critical[i].g)+Math.Abs(half[i].b-critical[i].b)>20)criticalChanged++;}
            Check(halfChanged>100&&criticalChanged>100,"Native GPU visibly distinguishes fed, hungry/thirsty and critical HUD states");
        }
        finally{viewArmsVisible=arms;survivalHudCanvas.renderMode=previousMode;survivalHudCanvas.worldCamera=previousCamera;view.cullingMask=mask;}
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();
        var fire=new CampObject{Kind=CampKind.Fire,Position=frame.Position(motion.X+2,motion.Z),LastSimulatedSeconds=Camp.SimulatedSeconds};Camp.Objects.Add(fire);
        CampRules.Add(fire,PortableTechnology.Create(PossessionKind.TinderBundle,Position));CampRules.Add(fire,PortableTechnology.Create(PossessionKind.KindlingBundle,Position));
        var fireBranch=WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,Position=Position,WidthM=1.8,HeightM=.03});fireBranch.Id="test-fuel-"+Guid.NewGuid().ToString("N");CampRules.Add(fire,fireBranch);CampRules.Ignite(fire,true);
        double burnSeconds=0;while(fire.Burning&&burnSeconds<48*3600){CampRules.Advance(fire,60,0,.3,0,0);burnSeconds+=60;}
        Check(burnSeconds>3*3600&&burnSeconds<48*3600&&!fire.Burning&&fire.EmberSeconds>0,"A real fueled fire burns for extended hours then leaves usable coals");
        timing.Add("Tinder + kindling +1.8m/3cm branch flame hours="+(burnSeconds/3600).ToString("F2"));
        PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();
        var grass=PortableTechnology.Create(PossessionKind.DryGrass,Position);Inventory.Items.Add(grass);AddCampMaterial(fire,grass);
        Check(fire.Burning&&Inventory.Find(grass.Id)==null,"Actual survival feeding of dry grass revives spent coals without a drill or separate ignition");
        var kindling=PortableTechnology.Create(PossessionKind.KindlingBundle,Position);Inventory.Items.Add(kindling);AddCampMaterial(fire,kindling);
        var moreWood=fireBranch.Copy();moreWood.Id="test-fuel-"+Guid.NewGuid().ToString("N");Inventory.Items.Add(moreWood);AddCampMaterial(fire,moreWood);CampRules.Advance(fire,1800,0,.3,0,0);
        Check(fire.Burning&&fire.FuelKg>0&&Inventory.Find(kindling.Id)==null&&Inventory.Find(moreWood.Id)==null,"Actual grass-to-kindling-to-branch feeding keeps the relit fire going");
        fire.Burning=false;fire.EmberSeconds=300;fire.Materials.Clear();
        kindling=PortableTechnology.Create(PossessionKind.KindlingBundle,Position);Inventory.Items.Add(kindling);AddCampMaterial(fire,kindling);
        Check(fire.Burning,"Actual kindling-only feeding also revives a previously lit fire");
        foreach(var material in new[]{PossessionKind.DryGrass,PossessionKind.LeafLitter,PossessionKind.ReedBundle,PossessionKind.BarkStrips,PossessionKind.RawFibre,PossessionKind.PreparedFibre})
            Check(CampRules.Tinder(PortableTechnology.Create(material,Position)),"Fire-placement material is also eligible relighting tinder: "+material);
        fire.LastSimulatedSeconds=Camp.SimulatedSeconds;
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();
        File.WriteAllLines(Path.Combine(output,"timing.txt"),timing);
    }
}

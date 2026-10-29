using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Vector2 campUiScroll;
    void BeginCampSleep(double hours,bool rest=false,CampObject bed=null)
    {
        if(!Ready||Spectating||Vitals.Incapacitated||motion.Flying||motion.Swimming||!motion.Grounded){InventoryFeedback("Lie down on dry, supported ground first.");return;}
        var water=SampleMotionWater(motion.X,motion.Z);
        if(double.IsFinite(water.Surface)&&water.Surface>CampFloor(motion.X,motion.Z)+.02){InventoryFeedback("Choose dry ground to lie down.");return;}
        if(!rest&&Vitals.Tiredness<8&&SurvivalActive){InventoryFeedback("You are not sleepy enough. You can still rest.");return;}
        if(bed!=null)
        {
            if(!Camp.Objects.Contains(bed)||(Position-bed.Position).Length*frame.RadiusM>4){InventoryFeedback("The bedding is out of reach.");return;}
            TerrainHandoff.Coordinates(frame,bed.Position,out double x,out double z);
            var bedWater=SampleMotionWater(x,z);
            if(double.IsFinite(bedWater.Surface)&&bedWater.Surface>CampFloor(x,z)+.02){InventoryFeedback("This bedding is underwater. Choose dry ground.");return;}
            motion.Place(x,z,CampFloor(x,z));humanPlayer.TargetYaw=humanPlayer.Yaw=(float)bed.Heading;
        }
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);SetInventoryOpen(false);
        campMenu=null;campGroundMenu=campSleepMenu=false;campResting=rest;campSleeping=!rest;campSleepSeconds=Math.Max(1,hours)*3600;campSleepHealth=Vitals.Health;sleepingBed=bed;CaptureMouse();
    }
    void WakeCamp(string reason)
    {if(!campSleeping&&!campResting)return;campSleeping=campResting=false;sleepingBed=null;campSleepSeconds=0;InventoryFeedback(reason);CaptureMouse();}
    void UpdateCampSleep()
    {
        if(!campSleeping&&!campResting)return;
        if(DeathVisible||Spectating){WakeCamp("Rest interrupted.");return;}
        if(paused||mapOpen||chatOpen||inventoryOpen)return;
        double seconds=Math.Min(.25,Time.unscaledDeltaTime)*CampTimeRate*86400/(PlayerRules.Settings.RealMinutesPerDay*60);campSleepSeconds=Math.Max(0,campSleepSeconds-seconds);
        if(Vitals.Health<campSleepHealth-.1||Vitals.CoreTemperatureC<35||Vitals.CoreTemperatureC>39||motion.Swimming){WakeCamp("Danger interrupts your rest.");return;}
        campSleepHealth=Math.Max(campSleepHealth,Vitals.Health);
        if(campSleepSeconds<=0||campSleeping&&SurvivalActive&&Vitals.Tiredness<1)WakeCamp(campResting?"You finish resting.":"You wake feeling rested.");
    }
    double CampWarmth()
    {
        double heat=0;
        foreach(var c in Camp.Objects)if(c.Kind==CampKind.Fire&&(c.Burning||c.EmberSeconds>0))
        {double distance=(Position-c.Position).Length*frame.RadiusM;if(distance<12)heat+=CampRules.RadiantWarmth(c,distance);}
        return Math.Min(50,heat);
    }
    void DrawCampUi()
    {
        if(campPlaceMaterial!=null)
        {
            var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true};
            GUI.Label(new Rect(Screen.width*.25f,Screen.height-100,Screen.width*.5f,60),campPlaceValid?"Place "+campPlaceMaterial.Name.ToLowerInvariant()+"  [LMB]   Cancel [RMB]":campPlaceReason,style);return;
        }
        if(campSleeping||campResting)
        {
            InventoryFill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.02f,.026f,campSleeping?.98f:.65f));
            var r=new Rect(Screen.width*.5f-210,Screen.height*.5f-95,420,200);GUILayout.BeginArea(r);
            GUILayout.Label(campSleeping?"Sleeping":"Resting",new GUIStyle(GUI.skin.label){fontSize=28,alignment=TextAnchor.MiddleCenter});
            GUILayout.Label("Tiredness "+Vitals.Tiredness.ToString("0")+"%   ·   Energy "+Vitals.Energy.ToString("0")+"%",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});
            GUILayout.Label("Up to "+(campSleepSeconds/3600).ToString("0.0")+" hours remaining",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});
            if(GUILayout.Button("Wake up",GUILayout.Height(38)))WakeCamp("You wake up.");GUILayout.EndArea();return;
        }
        if(!CampUiOpen)return;
        var box=new Rect(Screen.width*.5f-200,Screen.height*.5f-220,400,440);GUI.Box(box,"");GUILayout.BeginArea(new Rect(box.x+16,box.y+14,box.width-32,box.height-28));
        campUiScroll=GUILayout.BeginScrollView(campUiScroll);
        if(campGroundMenu)
        {
            GUILayout.Label("Ground materials");if(GUILayout.Button("Gather a bundle of dry grass",GUILayout.Height(36)))GatherCampGrass();
        }
        else if(campSleepMenu)
        {
            GUILayout.Label("Rest on the ground");GUILayout.Label("Tiredness "+Vitals.Tiredness.ToString("0")+"%   ·   Energy "+Vitals.Energy.ToString("0")+"%");
            foreach(int hours in new[]{1,4,8})if(GUILayout.Button("Sleep up to "+hours+" hours",GUILayout.Height(36)))BeginCampSleep(hours);
            if(GUILayout.Button("Rest awake for one hour",GUILayout.Height(36)))BeginCampSleep(1,true);
        }
        else if(campMenu!=null)
        {
            var c=campMenu;if(!Camp.Objects.Contains(c)||(c.Position-Position).Length*frame.RadiusM>4){campMenu=null;CaptureMouse();GUILayout.EndScrollView();GUILayout.EndArea();return;}
            GUILayout.Label(c.Kind==CampKind.Fire?"Campfire":c.Kind==CampKind.Bedding?"Bedding":"Planted stick");
            if(c.Kind==CampKind.Stake)GUILayout.Label("Embedded "+c.EmbedM.ToString("0.00")+" m   ·   Stability "+(c.Stability*100).ToString("0")+"%");
            else GUILayout.Label(c.MassKg.ToString("0.00")+" kg   ·   Wetness "+(c.Wetness*100).ToString("0")+"%"+(c.Kind==CampKind.Bedding?"   ·   Depth "+(c.BeddingDepthM*100).ToString("0")+" cm":""));
            if(c.Kind==CampKind.Fire)
            {
                if(!c.Burning&&GUILayout.Button("Work tinder / light fire",GUILayout.Height(36)))LightCamp(c);
                if((c.Burning||c.EmberSeconds>0)&&GUILayout.Button("Smother with earth",GUILayout.Height(36))){c.Burning=false;c.EmberSeconds=0;InvalidateCampVisual(c);}
            }
            RefreshInventoryNearby();var candidates=CraftAccessible().Where(p=>c.Kind==CampKind.Fire?CampRules.Fuel(p):c.Kind==CampKind.Bedding&&CampRules.Bedding(p)).ToArray();
            foreach(var p in candidates)if(GUILayout.Button("Add "+p.Name.ToLowerInvariant()+" ("+p.MassKg.ToString("0.00")+" kg)",GUILayout.Height(32)))AddCampMaterial(c,p);
            if(c.Kind==CampKind.Bedding)foreach(int hours in new[]{1,4,8})if(GUILayout.Button("Sleep here, up to "+hours+" hours",GUILayout.Height(32)))BeginCampSleep(hours,false,c);
            if(GUILayout.Button(c.Kind==CampKind.Stake?"Pull stick out":"Gather remaining materials",GUILayout.Height(32)))RemoveCamp(c);
        }
        GUILayout.EndScrollView();GUILayout.FlexibleSpace();if(GUILayout.Button("Close",GUILayout.Height(32))){campMenu=null;campGroundMenu=campSleepMenu=false;CaptureMouse();}GUILayout.EndArea();
    }
}

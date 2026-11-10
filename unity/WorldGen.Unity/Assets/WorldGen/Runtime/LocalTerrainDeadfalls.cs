using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    double deadfallAt;int deadfallCursor;
    readonly Dictionary<string,Dictionary<ulong,Vector2>> deadfallTracks=new Dictionary<string,Dictionary<ulong,Vector2>>();
    string DeadfallPrompt(CampObject c)=>"Deadfall - "+(c.TrapArmed?"set":c.TrapSprung?"sprung":Deadfalls.Stone(c)==null?"add a broad stone":Deadfalls.Bait(c)==null?"add food bait":"set trigger")+"  [E]";
    bool InteractDeadfall(CampObject c)
    {
        if(!c.TrapArmed){RefreshInventoryNearby(true);var held=Inventory.QuickHeld;
            var material=held!=null&&Deadfalls.Accepts(c,held)?held:CraftAccessible().FirstOrDefault(p=>Deadfalls.Accepts(c,p));
            if(material!=null){AddDeadfallMaterial(c,material);return true;}
            if(Deadfalls.SetReason(c).Length==0){SetDeadfall(c);return true;}}
        campMenu=c;CaptureMouse();return true;
    }
    void AddDeadfallMaterial(CampObject c,Possession p)
    {
        if(!Deadfalls.Accepts(c,p))return;var material=TakeCampMaterial(p);if(material==null)return;
        if(Foraging.Food(material)&&material.MassKg>.025)
        {var bait=material.Copy();bait.Id="bait-"+Guid.NewGuid().ToString("N");Foraging.Resize(bait,.025);Foraging.Resize(material,material.MassKg-.025);Inventory.Items.Add(material);ReleasePossession(material,true);material=bait;}
        CampRules.Add(c,material);InvalidateCampVisual(c);inventoryNearbyAt=0;InventoryFeedback(Deadfalls.SetReason(c).Length==0?"Stone and bait ready. Set the trigger.":Deadfalls.SetReason(c));
    }
    void SetDeadfall(CampObject c)
    {
        string why=Deadfalls.SetReason(c);if(why.Length>0){InventoryFeedback(why);return;}
        var trigger=c.Materials.First(p=>p.Kind==PossessionKind.DeadfallTrigger);c.Materials.Remove(trigger);trigger.Position=Position;trigger.Place=PossessionPlace.Ground;Inventory.Items.Add(trigger);
        bool returned=false;void Return(){if(returned)return;returned=true;Inventory.Items.Remove(trigger);trigger.Place=PossessionPlace.Ground;trigger.ContainerId="";if(Camp.Objects.Contains(c))c.Materials.Add(trigger);else{trigger.Position=Position;Inventory.Items.Add(trigger);}RefreshChangedPossession(trigger);InvalidateCampVisual(c);}
        BeginWorldToolWork(trigger,"Balancing the stone and setting the trigger",120,()=>{Return();if(Camp.Objects.Contains(c)&&Deadfalls.Arm(c)){deadfallTracks.Remove(c.Id);InvalidateCampVisual(c);InventoryFeedback("Deadfall set. Its bait must be reached by a small ground animal.");}},()=>Return());
        if(worldToolWork==null)Return();campMenu=null;inventoryContext=null;CaptureMouse();
    }
    void DrawDeadfallActions(CampObject c)
    {
        GUILayout.Label(c.TrapArmed?"Set":c.TrapSprung?"Sprung":"Unarmed");
        if(c.TrapArmed){if(GUILayout.Button("Disarm",GUILayout.Height(32))){c.TrapArmed=false;deadfallTracks.Remove(c.Id);InvalidateCampVisual(c);}return;}
        RefreshInventoryNearby();foreach(var p in CraftAccessible().Where(p=>Deadfalls.Accepts(c,p)).ToArray())
            if(GUILayout.Button("Add "+p.Name.ToLowerInvariant()+(Foraging.Food(p)?" bait":""),GUILayout.Height(32))){AddDeadfallMaterial(c,p);break;}
        string why=Deadfalls.SetReason(c);if(why.Length==0&&GUILayout.Button("Set the trigger",GUILayout.Height(32)))SetDeadfall(c);
    }
    void CreateDeadfallVisual(CampObject c,GameObject root)
    {
        foreach(var p in c.Materials)
        {
            var part=CreatePossessionVisual(p,30);part.transform.SetParent(root.transform,false);
            if(Deadfalls.Weight(p)){part.transform.localPosition=new Vector3(0,c.TrapArmed?(float)Deadfalls.DropHeight(c):.025f,0);part.transform.localRotation=Quaternion.Euler(c.TrapArmed?-25:0,0,0);}
            else if(p.Kind==PossessionKind.DeadfallTrigger){part.transform.localPosition=new Vector3(0,.01f,-.1f);part.transform.localRotation=Quaternion.Euler(c.TrapArmed?0:82,0,0);}
            else {part.transform.localPosition=new Vector3(0,.025f,.06f);}
        }
    }
    bool TriggerDeadfall(CampObject c,Animal a,double x,double z,bool swept=false)
    {
        if(!c.TrapArmed||a==null||a.Dead||!Deadfalls.Eligible(a.MassKg,a.Spawn.Profile.medium))return false;
        double radius=Deadfalls.Radius(c);if(!swept&&Math.Pow(a.X-x,2)+Math.Pow(a.Z-z,2)>radius*radius)return false;
        double damage=Deadfalls.Spring(c);InvalidateCampVisual(c);deadfallTracks.Remove(c.Id);
        var point=transform.TransformPoint(new Vector3((float)(x-originX),(float)a.GroundY+.08f,(float)(z-originZ)));
        DamageAnimal(a,damage,point,Vector3.down,Deadfalls.Stone(c).MassKg*Math.Sqrt(2*9.81*Deadfalls.DropHeight(c)),false);PlayItemEffect("stone-contact",point,.45f);return true;
    }
    void UpdateDeadfalls()
    {
        if(!Ready||paused||Time.unscaledTimeAsDouble<deadfallAt)return;deadfallAt=Time.unscaledTimeAsDouble+.2;
        var traps=Camp.Objects.Where(c=>c.Kind==CampKind.Deadfall&&c.TrapArmed&&(c.Position-Position).Length*frame.RadiusM<250).ToArray();
        foreach(var key in deadfallTracks.Keys.Where(k=>!traps.Any(c=>c.Id==k)).ToArray())deadfallTracks.Remove(key);
        if(traps.Length==0)return;
        var prey=animals.Values.Where(a=>!a.Dead&&Deadfalls.Eligible(a.MassKg,a.Spawn.Profile.medium)).ToArray();
        for(int n=0;n<Math.Min(8,traps.Length);n++)
        {
            var c=traps[(deadfallCursor+n)%traps.Length];TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);
            if(!deadfallTracks.TryGetValue(c.Id,out var tracks)){tracks=new Dictionary<ulong,Vector2>();deadfallTracks[c.Id]=tracks;}
            foreach(var a in prey)
            {
                var current=new Vector2((float)(a.X-x),(float)(a.Z-z));bool was=tracks.TryGetValue(a.Spawn.Id,out var old);tracks[a.Spawn.Id]=current;
                if(Math.Abs(a.GroundY-CampFloor(x,z))>.5||!Deadfalls.Crosses(was?old.x:current.x,was?old.y:current.y,current.x,current.y,Deadfalls.Radius(c)))continue;
                // A bounded swept contact catches an actual crossing, including a
                // fast animal stepping across the small footprint between checks.
                if(TriggerDeadfall(c,a,x,z,true))break;
            }
            if(c.TrapArmed)foreach(var id in tracks.Keys.Where(id=>!animals.ContainsKey(id)).ToArray())tracks.Remove(id);
        }
        deadfallCursor=(deadfallCursor+8)%traps.Length;
    }
}

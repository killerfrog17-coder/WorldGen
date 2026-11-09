using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcBelongings(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC BELONGINGS PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)<2&&Time.unscaledTimeAsDouble<until)yield return null;
        var actors=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18&&p.Health>0).OrderBy(p=>HumanDistance(p.Planet,Position)).Take(2).ToArray();Check(actors.Length==2,"Two actual adult residents exist");
        var actor=actors[0];var hidden=actors[1];var group=livingGroups[actor.GroupId];paused=true;
        Check(actor.Belongings!=null&&NpcPersonRecord(actor).Inventory==actor.Belongings,"The physical actor uses its saved inventory rather than a prop-only loadout");
        var before=actor.Belongings.Items.Concat(Inventory.Items).Select(p=>p.Id).OrderBy(x=>x).ToArray();
        foreach(string role in new[]{"SpearReady1","WorkKnapping","ToolReady","Eat","FuelTend",null})UpdateNpcProps(actor,group,role);
        Check(before.SequenceEqual(actor.Belongings.Items.Concat(Inventory.Items).Select(p=>p.Id).OrderBy(x=>x)),"Repeated visual task changes create or delete no actual goods");
        Check(actor.NpcProps.All(p=>actor.Belongings.Items.Contains(p.Item)&&actor.Belongings.Carried(p.Item)),"Every displayed NPC possession is a real carried item");
        var copied=LivingState.Copy();copied.Validate();Check(copied.People.Any(p=>p.Person==actor.ResidentId&&p.Inventory!=null),"A healthy resident's complete belongings survive session copying");
        // Controlled property fixture: real item and physical witnesses, with
        // one resident behind a solid. This tests evidence, not random camp layout.
        motion.Place(actor.Motor.X-2,actor.Motor.Z,CampFloor(actor.Motor.X-2,actor.Motor.Z));motion.Flying=false;humanCamera.Restore(0,false);UpdateCamera();
        actor.Yaw=actor.TargetYaw=270;actor.Root.transform.localRotation=Quaternion.Euler(0,270,0);actor.CurrentActivity=HumanActivity.Watch;
        hidden.Motor.Place(motion.X-4,motion.Z,CampFloor(motion.X-4,motion.Z));hidden.Planet=frame.Position(hidden.Motor.X,hidden.Motor.Z);hidden.Root.transform.localPosition=new Vector3((float)(hidden.Motor.X-originX),(float)(hidden.Motor.Y-TerrainMotion.EyeHeightM),(float)(hidden.Motor.Z-originZ));hidden.CurrentActivity=HumanActivity.Watch;hidden.Root.transform.localRotation=Quaternion.Euler(0,90,0);
        var wall=new GameObject("Property hidden witness fixture");wall.layer=27;wall.transform.position=transform.TransformPoint(new Vector3((float)(motion.X-originX-2), (float)(motion.Y-TerrainMotion.EyeHeightM)+1.5f,(float)(motion.Z-originZ)));wall.transform.rotation=transform.rotation;wall.AddComponent<BoxCollider>().size=new Vector3(.3f,5,8);Physics.SyncTransforms();
        try
        {
            var item=PortableTechnology.Create(PossessionKind.StoneBlade,frame.Position(motion.X,motion.Z+1));item.Id="property-probe-"+actor.ResidentId;item.Place=PossessionPlace.Ground;NpcOwnership.Assign(item,actor.Resident,actor.GroupId);Inventory.Items.Add(item);
            foreach(var old in Inventory.Items.Where(p=>p!=item&&(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)).ToArray()){old.Place=PossessionPlace.Ground;old.Position=Position;}
            RefreshInventoryNearby(true);Check(!PlayerMayTake(item),"Personal belongings remain marked as another person's property");
            int seen=NpcMemoryFor(actor.ResidentId).Social.Incidents.Count,unseen=NpcMemoryFor(hidden.ResidentId).Social.Incidents.Count;
            Check(MovePossession(item.Id,PossessionPlace.RightHand),"An owned real ground item can actually be stolen");
            Check(NpcMemoryFor(actor.ResidentId).Social.Incidents.Skip(seen).Any(e=>e.Kind==NpcIncidentKind.Theft&&e.Actor==PlayerPropertyKey),"A seeing owner remembers who took the specific item");
            Check(NpcMemoryFor(hidden.ResidentId).Social.Incidents.Count==unseen,"A resident behind a real wall does not learn the theft magically");
            Check(item.OwnerPerson==actor.ResidentId&&Inventory.AtHand(PossessionPlace.RightHand)==item,"Possession changes physically while the original ownership remains recorded");
            Check(NpcMemoryFor(actor.ResidentId).Social.Anger>0&&NpcMemoryFor(actor.ResidentId).Social.Relationships.Any(t=>t.Other==PlayerPropertyKey&&t.Grievance>0),"Witnessed theft changes mood and the directed relationship");
            Inventory.Move(item.Id,PossessionPlace.Ground,"",0,0,false,out _);item.Position=frame.Position(motion.X,motion.Z+1);item.GroundRotation=new V(90,0,0);inventoryNearbyAt=0;
            RefreshInventoryNearby(true);var groundItem=inventoryNearby.First(n=>n.Item==item);
            for(int aim=0;aim<6;aim++){UpdateCamera();var direction=transform.InverseTransformDirection(transform.TransformPoint(groundItem.Point)-view.transform.position).normalized;heading=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(direction.y)*Mathf.Rad2Deg;}
            paused=false;npcThoughts=true;UpdateCamera();yield return NpcLifeImage(output,"owned-items-and-witness");paused=true;
            File.WriteAllText(Path.Combine(output,"aim-diagnostic.txt"),"item="+item.Id+"; actual="+AimedPossession()?.Item?.Id+"; bounds="+groundItem.Bounds+"; ray="+new Ray(view.transform.localPosition,view.transform.forward));
            Check(AimedPossession()?.Item==item&&pickupPromptTheft!=null&&pickupPromptTheft.normal.textColor.r>.8f&&pickupPromptTheft.normal.textColor.g<.5f,"The aimed owned item uses the actual red theft-prompt style");
            var privateBed=Camp.Objects.FirstOrDefault(c=>c.Kind==CampKind.Bedding&&c.NpcGroup==actor.GroupId&&c.NpcHousehold!=PlayerRules.Identity.Person.Household);
            if(privateBed!=null)Check(!PlayerMayUseCamp(privateBed),"An actual other household's bedding has private-use status");
            var all=Inventory.Items.Concat(livingPeople.Values.Where(p=>p.Belongings!=null).SelectMany(p=>p.Belongings.Items)).ToArray();
            Check(all.Select(p=>p.Id).Distinct().Count()==all.Length,"No possession ID exists in both the world and an NPC inventory");
            LivingState.Copy().Validate();Inventory.Validate();Check(true,"Owned goods and social consequences validate after the real transfer");
            File.WriteAllText(Path.Combine(output,"owner-memory.json"),JsonUtility.ToJson(NpcMemoryFor(actor.ResidentId),true));
        }
        finally{DestroyImmediate(wall);Physics.SyncTransforms();paused=false;}
        DisposeNpcMinds();
    }
}

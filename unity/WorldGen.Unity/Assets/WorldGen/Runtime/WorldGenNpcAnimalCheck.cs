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
    public IEnumerator CheckNpcAnimalContact(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC ANIMAL PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)<3&&Time.unscaledTimeAsDouble<until)yield return null;
        var adults=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18).Take(3).ToArray();Check(adults.Length==3,"Actual bandmates exist for the wildlife contact and witness checks");
        ExecuteAnimalSpawnCommand("/spawn wolf");until=Time.unscaledTimeAsDouble+90;while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="wolf")&&Time.unscaledTimeAsDouble<until)yield return null;
        var wolf=animals.Values.FirstOrDefault(a=>a.Manual&&a.Spawn.Profile.slug=="wolf");Check(wolf!=null,"An actual wolf loads through the normal animal asset path");paused=true;livingQueryAt=Time.unscaledTimeAsDouble+90;while(livingWork!=null)yield return null;
        var target=adults[0];var witness=adults[1];var hidden=adults[2];var group=livingGroups[target.GroupId];TerrainHandoff.Coordinates(frame,group.Anchor,out double cx,out double cz);double x=cx+28,z=cz+18;
        void Stand(HumanActor p,double px,double pz,float yaw){p.Motor.Place(px,pz,CampFloor(px,pz));p.Planet=frame.Position(px,pz);p.Root.transform.localPosition=new Vector3((float)(px-originX),(float)(p.Motor.Y-TerrainMotion.EyeHeightM),(float)(pz-originZ));p.Root.transform.localRotation=Quaternion.Euler(0,yaw,0);p.Yaw=p.TargetYaw=yaw;p.Threat=null;p.CurrentActivity=HumanActivity.Watch;p.Speed=p.TargetSpeed=0;}
        Stand(target,x,z,0);Stand(witness,x,z+3,180);Stand(hidden,x+4,z,270);motion.Place(x-5,z-4,CampFloor(x-5,z-4));UpdateHumans();
        void PlaceWolf(double pz){wolf.X=x;wolf.Z=pz;wolf.Planet=frame.Position(x,pz);wolf.GroundY=CampFloor(x,pz);wolf.Heading=wolf.TargetHeading=0;wolf.Speed=wolf.TargetSpeed=0;wolf.AttackAt=0;wolf.Life+=2;UpdateAnimal(wolf,0);}
        PlaceWolf(z-9);double health=target.Health;AnimalContactNpc(wolf,target);Check(target.Health==health,"A distant animal cannot damage a resident without physical contact");
        var wall=new GameObject("NPC animal hidden witness fixture");wall.layer=27;wall.transform.position=transform.TransformPoint(new Vector3((float)(x+2-originX),(float)(CampFloor(x+2,z)+1.6),(float)(z-originZ)));wall.transform.rotation=transform.rotation;wall.AddComponent<BoxCollider>().size=new Vector3(.3f,5,7);Physics.SyncTransforms();
        try
        {
            PlaceWolf(z-.7);npcAnimalContactFrame=-1;AnimalContactNpc(wolf,target);
            Check(target.Health<health&&target.Health>0,"Real animal contact reaches the resident's posed body and removes health");
            Check(target.Bleeding?.Rate>0,"The bite creates an actual coarse bleeding wound");
            Check(NpcMemoryFor(witness.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Assault&&e.Subject==target.ResidentId&&e.Animal==wolf.Spawn.Id&&e.Detail.IndexOf("wolf",StringComparison.OrdinalIgnoreCase)>=0),"A seeing bandmate remembers the particular wolf attacking the person");
            Check(!NpcMemoryFor(hidden.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Assault&&e.Subject==target.ResidentId),"A bandmate behind the real solid wall does not learn the unseen attack");
            nextWildlifeIndex=0;IndexWildlifeNeighbours();wolf.NpcAssailant=target.ResidentId;wolf.ThreatUntil=wolf.Life+12;wolf.DefendWarnUntil=0;wolf.BehaviourAt=0;
            Check(AnimalNpcResponse(wolf,null,true,double.MaxValue,.5)&&wolf.NpcHuntTarget==target.ResidentId,"A threatened animal can direct its defensive response at the actual NPC rather than the player");
            wolf.NpcAssailant=0;target.Health=1;wolf.Life+=2;wolf.AttackAt=0;npcAnimalContactFrame=-1;AnimalContactNpc(wolf,target);
            Check(target.Health==0&&target.Ragdoll!=null,"A fatal contact produces the real resident corpse and ragdoll");
            DiscoverNpcDeath(witness,target);Check(NpcMemoryFor(witness.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Death&&e.Subject==target.ResidentId),"The witness who discovers the body retains the death alongside the witnessed cause");
            Check(!NpcMemoryFor(hidden.ResidentId).Social.KnownPeople.Any(p=>p.Person==target.ResidentId&&p.SeenDead),"The hidden observer does not receive an omniscient death notification");
            CaptureLivingPerson(target);LivingState.Copy().Validate();Check(true,"Animal-caused wounds, real corpse goods and witness memories validate for saving");
            File.WriteAllText(Path.Combine(output,"wolf-witness-memory.json"),JsonUtility.ToJson(NpcMemoryFor(witness.ResidentId),true));
        }
        finally{DestroyImmediate(wall);Physics.SyncTransforms();}
        paused=false;DisposeNpcMinds();
    }
}

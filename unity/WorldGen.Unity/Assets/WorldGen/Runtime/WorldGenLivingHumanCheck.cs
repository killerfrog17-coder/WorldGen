using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckLivingHumans(string path,string output)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();
        Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("LIVING HUMANS "+message);checks.Add("PASS "+message);Debug.Log("LIVING HUMANS "+message);}
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        var load=Task.Run(()=>WorldFile.ReadFile(path));while(!load.IsCompleted)yield return null;var world=load.GetAwaiter().GetResult();
        var engine=new Prehistory(world);var state=world.Prehistory;
        if(!Prehistory.Foraging(state)||state.Foragers.Version!=ForagerState.RegionalVersion)
        {
            var history=Task.Run(()=>engine.AdvanceYears(engine.StartForagers(engine.PrepareForagers()),40000));while(!history.IsCompleted)yield return null;
            world.Prehistory=state=history.GetAwaiter().GetResult();
        }
        var play=new PlaySession();play.Settings.HorizontalScale=.1;play.Settings.RealMinutesPerDay=60;play.Settings.CalendarDaysPerDay=365.2425/24;play.Mode=GameMode.Creative;
        var planner=new HumanLocalLife(world,engine);HumanGroupSituation chosen=null;
        foreach(var cohort in state.Foragers.Cohorts.Where(c=>engine.ForagerBandCount(state,c)>0).OrderByDescending(c=>c.Population))
        {
            for(int ordinal=0;ordinal<engine.ForagerBandCount(state,cohort);ordinal++)
            {
                long id=((long)(cohort.Id+1)<<32)|(uint)(ordinal+1);if(!HumanLocalLife.SpatialAdmission(world.Recipe.Seed,id,.1))continue;
                var group=planner.Group(state,new HumanLifeClock(SimulationTime.Seconds(state),0,60,play.Settings.CalendarDaysPerDay,.1),cohort.Id,ordinal);
                if(group.Phase==HumanCampPhase.Occupied){chosen=group;break;}
            }
            if(chosen!=null)break;
        }
        Check(chosen!=null,"An admitted real simulated community exists");
        var setup=Task.Run(()=>new LocalTerrain(world,horizontalScale:.1));
        var owner=new GameObject("Living human actual-terrain fixture").AddComponent<LocalTerrainExplorer>();owner.viewDistance=1800;
        owner.Initialize(setup,chosen.Position,()=>{},world,play);
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.Ready||!owner.CenterReady||owner.livingPeople.Count==0)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(owner.Ready&&owner.CenterReady,"Actual local terrain and centre tiles are ready");
        Check(owner.livingPeople.Count>0,"Natural residents stream from world history, without a spawn command");
        var groupRuntime=owner.livingGroups[chosen.Band.Id];
        while((!groupRuntime.CampLoaded||owner.livingPeople.Count<Math.Min(12,groupRuntime.Residents.Members.Length))&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(groupRuntime.CampLoaded,"Natural camp passes local ground validation and is loaded");
        Check(owner.Camp.Objects.Any(c=>c.NpcGroup==chosen.Band.Id),"Camp consists of real player-compatible objects/materials");
        owner.Camp.Validate();Check(true,"Native camp validates with real terrain footing surveys");
        Check(owner.Camp.Objects.Where(c=>c.NpcGroup==chosen.Band.Id&&c.Blueprint!=null).All(c=>CampBlueprints.Complete(c.Blueprint)),"Occupied camp shelters retain every fitted roof/frame component");
        File.WriteAllText(Path.Combine(output,"camp.json"),WorldJson.Write(owner.Camp));
        Check(owner.livingPeople.Values.All(p=>p.ResidentId!=0&&p.Resident!=null&&p.Routine!=null),"Physical NPCs retain named family identities and actual scheduled tasks");
        Check(owner.livingGroups.Values.All(g=>g.Situation.DistanceToPlayerM<=51600),"Broad local group tracking is bounded at 50km plus foraging catchment");
        var target=new RenderTexture(1536,864,24);target.Create();var prior=RenderTexture.active;owner.view.targetTexture=target;
        void Image(string name,Vector3 offset)
        {
            TerrainHandoff.Coordinates(owner.frame,groupRuntime.Anchor,out double x,out double z);float floor=(float)owner.Ground(x,z);
            var focus=new Vector3((float)(x-owner.originX),floor+1,(float)(z-owner.originZ));owner.view.transform.localPosition=focus+offset;owner.view.transform.LookAt(owner.transform.TransformPoint(focus));
            owner.view.Render();RenderTexture.active=target;var image=new Texture2D(1536,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1536,864),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=prior;
        }
        Image("camp-first",new Vector3(18,8,24));Image("residents-first",new Vector3(0,2,15));
        File.WriteAllText(Path.Combine(output,"community.json"),WorldJson.Write(groupRuntime.Residents));
        var packet=new PlaySession();owner.StorePlayer(packet);packet.Validate();var restored=WorldJson.Read<PlaySession>(WorldJson.Write(packet));restored.Validate();
        Check(restored.Humans.Camps.Count==packet.Humans.Camps.Count&&restored.Camp.Objects.Select(c=>c.Id).SequenceEqual(packet.Camp.Objects.Select(c=>c.Id)),"Named camp objects and local memories survive actual player capture and serialization");
        var actor=owner.livingPeople.Values.First(p=>p.Resident.AgeYears>=18);long identity=actor.ResidentId;owner.DamageHuman(actor,12,null,Vector3.forward);owner.CaptureLivingHumans();
        Check(play.Humans.People.Single(p=>p.Person==identity).Health==actor.Health&&play.Humans.Groups.Single(g=>g.Group==actor.GroupId).Trust<0,"Aggression preserves wounds and community alarm/relationship consequences");
        owner.UnloadLivingCamp(groupRuntime);owner.LoadLivingCamp(groupRuntime);owner.Camp.Validate();
        Check(owner.Camp.Objects.Select(c=>c.Id).Distinct().Count()==owner.Camp.Objects.Count,"Unloading/revisiting a camp does not duplicate its objects");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("LIVING HUMANS PASS "+checks.Count);owner.view.targetTexture=null;target.Release();Destroy(target);Application.Quit(0);
    }
}

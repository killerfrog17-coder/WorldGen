using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckHuntingFlow(string output,List<string> report)
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("HUNTING "+why);report.Add("PASS "+why);Debug.Log("HUNTING PASS "+why);}
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();motion.Place(motion.X,motion.Z,CampFloor(motion.X,motion.Z));humanCamera.Restore(0,false);inventoryReviewMode=true;reviewWildlifeBehaviour=false;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);
        foreach(string command in new[]{"/spawn red-kangaroo 2","/spawn deer","/spawn wolf"})ExecuteAnimalSpawnCommand(command);
        double end=Time.unscaledTimeAsDouble+90;
        while((animals.Values.Count(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo")<2||!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="deer")||!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="wolf"))&&Time.unscaledTimeAsDouble<end)yield return null;
        Check(animals.Values.Count(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo")==2,"Two original kangaroo actors load through the ordinary spawn queue");
        var roos=animals.Values.Where(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo").ToArray();var roo=roos[0];var deer=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer");var wolf=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="wolf");paused=true;
        Check(deer.Model.Asset.Clips.Any(c=>c.role=="eating")&&deer.Model.Asset.Clips.Any(c=>c.role=="sleep"),"Additional original deer eating and sleep palettes load beside unchanged locomotion");
        Check(wolf.Model.Asset.Clips.Any(c=>c.role=="attack")&&wolf.Model.Asset.Clips.Any(c=>c.role=="alert"),"Original wolf attack and alert clips are available");
        double px=motion.X,pz=motion.Z;
        void Place(Animal a,double x,double z,float heading=180)
        {a.X=x;a.Z=z;a.Planet=frame.Position(x,z);a.GroundY=CampFloor(x,z);a.Heading=a.TargetHeading=heading;a.TargetSpeed=a.Speed=0;a.Awareness=new WildlifeAwarenessState();a.BehaviourAt=0;a.Fade=1;UpdateAnimal(a,0);}
        void Weather(double north)=>CurrentWeather=new WeatherFrame(22,.4,0,0,0,0,1500,500,8000,0,north,Math.Abs(north),0);
        void Observe(double speed,double cover,double light,int ticks)
        {huntingSpeed=speed;huntingCover=cover;huntingLight=light;huntingCoverAt=Time.unscaledTimeAsDouble+1000;for(int i=0;i<ticks;i++)PerceivePlayer(roo,.3,Math.Sqrt(Math.Pow(roo.X-motion.X,2)+Math.Pow(roo.Z-motion.Z,2)));}
        Place(roo,px,pz+20);Weather(0);Check(WildlifeSight(roo,px,pz),"Actual streamed ground permits an unobstructed player sight line");
        Observe(3,0,1,30);double exposed=roo.Awareness.Suspicion;Check(exposed>.78,"Sustained visible movement is recognized as a threat");
        roo.Awareness=new WildlifeAwarenessState();Observe(0,.85,.15,30);Check(roo.Awareness.Suspicion<exposed*.4,"Stillness, physical-cover input and low illumination sharply reduce identification");
        var obstruction=new GameObject("Hunting LOS check");
        try
        {
            var center=(view.transform.position+roo.Root.transform.position+Vector3.up)*.5f;obstruction.transform.position=center;obstruction.layer=27;obstruction.AddComponent<BoxCollider>().size=new Vector3(8,5,1);Physics.SyncTransforms();
            Check(!WildlifeSight(roo,px,pz),"A real solid collider blocks animal vision");roo.Awareness=new WildlifeAwarenessState();Observe(3,0,1,12);Check(roo.Awareness.Sense!=WildlifeSense.Sight,"Movement behind a solid obstacle can be heard without being seen");
        }
        finally{DestroyImmediate(obstruction);Physics.SyncTransforms();}
        Place(roo,px,pz+30,0);Weather(3);Observe(0,0,1,35);double scented=roo.Awareness.Suspicion;
        roo.Awareness=new WildlifeAwarenessState();Weather(-3);Observe(0,0,1,35);Check(scented>.6&&roo.Awareness.Suspicion<.1,"Native scent checks distinguish an upwind hunter from a downwind hunter while the animal faces away");
        WildlifePerception.Alarm(roo.Awareness,px,pz);Weather(0);Place(roos[1],px+8,pz+30,180);Check(ShareAnimalAlarm(roos[1],roo,8),"A nearby herd mate reacts to observed alarm without seeing the original threat");FleeKnownThreat(roos[1]);Check(roos[1].TargetSpeed>roos[1].Spawn.Profile.walkMps,"Social alarm produces a real escape movement request");
        roo.Exertion=new ExertionState{Burst=.12,Fatigue=.3,Breath=.8};RememberAnimal(roo,false);var id=roo.Spawn.Id;var spawn=roo.Spawn;var model=roo.Model;RemoveAnimal(id);Check(AddAnimal(spawn,model,true),"An effort-bearing animal can be streamed back in");roo=animals[id];Check(roo.Exertion.Burst<.2&&roo.Exertion.Fatigue>.25&&roo.Awareness.MemorySeconds>0,"Streaming preserves accumulated effort and threat memory");
        var saved=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));saved.Validate();var record=saved.Wildlife.Individuals.Single(a=>a.Id==id);Check(record.Exertion.Burst<.2&&record.Awareness.MemorySeconds>0,"The full save serializer preserves the active hunt state");
        Color32[] Capture(string name)
        {
            var old=view.targetTexture;var previous=RenderTexture.active;var rt=new RenderTexture(1280,800,24);rt.Create();view.targetTexture=rt;view.Render();RenderTexture.active=rt;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();var pixels=texture.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);view.targetTexture=old;RenderTexture.active=previous;rt.Release();Destroy(rt);return pixels;
        }
        void ActorImage(Animal a,string action,string name)
        {
            Place(a,px+4,pz+4);AnimalActivity(a,action,20);a.OldClip=null;a.BlendAge=1;a.Time=a.Clip.duration*.4;a.CollisionFrame=-1;UpdateAnimal(a,0);
            Check(PosedAnimal(a).All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Native "+name+" palette produces finite skin vertices");
            var bounds=a.Renderer.bounds;view.transform.position=bounds.center+new Vector3(2,.8f,-2).normalized*(float)Math.Max(1.6,a.Spawn.Profile.lengthM*1.8);view.transform.LookAt(bounds.center);Capture(name);
        }
        ActorImage(deer,"eat","01-deer-eating");ActorImage(deer,"sleep","02-deer-rest");ActorImage(wolf,"sleep","03-wolf-rest");
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();paused=false;UpdatePlayerSurvival(0,0);UpdateSurvivalHud();paused=true;
        Check(survivalHud!=null&&survivalHud.material.shader.isSupported,"The live survival HUD supports the wind shader");
        var mode=survivalHudCanvas.renderMode;var oldCamera=survivalHudCanvas.worldCamera;int mask=view.cullingMask;
        survivalHudCanvas.renderMode=RenderMode.ScreenSpaceCamera;survivalHudCanvas.worldCamera=view;survivalHudCanvas.planeDistance=.4f;view.cullingMask|=1<<5;survivalHudCanvas.enabled=true;
        try
        {
            var values=SurvivalHudValues.From(Vitals,22);values.Wind=WindHudValues.From(0,5,0);survivalHud.SetValues(values,true);Canvas.ForceUpdateCanvases();var wind=Capture("04-wind-from-behind");
            values.Wind=WindHudValues.From(0,-5,0);survivalHud.SetValues(values,true);Canvas.ForceUpdateCanvases();var front=Capture("05-wind-from-front");
            int changed=0;for(int i=0;i<wind.Length;i++)if(Math.Abs(wind[i].r-front[i].r)+Math.Abs(wind[i].g-front[i].g)+Math.Abs(wind[i].b-front[i].b)>12)changed++;
            Check(changed>10,"Actual GPU wind icons change between the front and rear HUD sectors");
        }
        finally{survivalHudCanvas.renderMode=mode;survivalHudCanvas.worldCamera=oldCamera;view.cullingMask=mask;}
        reviewWildlifeBehaviour=true;paused=false;
        var timer=System.Diagnostics.Stopwatch.StartNew();for(int frameCount=0;frameCount<60;frameCount++)foreach(var animal in animals.Values.Where(a=>!a.Spawn.Profile.tiny&&!a.Dead)){animal.BehaviourAt=0;animal.LastPerception=Math.Max(0,animal.Life-.3);UpdateAnimalBehaviour(animal);}timer.Stop();
        File.WriteAllText(Path.Combine(output,"behaviour-cost.txt"),animals.Count+" actors; 60 forced full decision sweeps "+timer.Elapsed.TotalMilliseconds.ToString("F2")+" ms (stress diagnostic, not frame-rate claim)");
        Check(animals.Values.All(a=>a.Exertion.Burst>=0&&a.Exertion.Burst<=1&&a.Awareness.Suspicion>=0&&a.Awareness.Suspicion<=1),"Mixed actor decisions retain bounded physical and awareness state");reviewWildlifeBehaviour=false;paused=true;
    }
}

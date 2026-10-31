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
        if(Environment.GetCommandLineArgs().Contains("--worldgen-hunting-revision-check")){yield return CheckHuntingRevisionFlow(output,report);yield break;}
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("HUNTING "+why);report.Add("PASS "+why);Debug.Log("HUNTING PASS "+why);}
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();motion.Place(motion.X,motion.Z,CampFloor(motion.X,motion.Z));humanCamera.Restore(0,false);reviewWildlifeBehaviour=false;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);
        foreach(string command in new[]{"/spawn red-kangaroo 2","/spawn deer","/spawn wolf","/spawn duck"})ExecuteAnimalSpawnCommand(command);
        double end=Time.unscaledTimeAsDouble+90;
        while((animals.Values.Count(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo")<2||!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="deer")||!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="wolf")||!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="duck"))&&Time.unscaledTimeAsDouble<end)yield return null;
        Check(animals.Values.Count(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo")==2,"Two original kangaroo actors load through the ordinary spawn queue");
        var roos=animals.Values.Where(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo").ToArray();var roo=roos[0];var deer=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer");var wolf=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="wolf");paused=true;
        WriteWildlifeProfileAudit(output,report);
        CheckSmallSchoolDecisions(roo.Model,report);
        var bird=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="duck");
        double oldAir=bird.AirHeight,oldTargetAir=bird.AirTargetHeight;ulong oldGroup=bird.Spawn.GroupId;
        try
        {
            // A supplied water plane isolates clearance from a real bird skin;
            // this is not a claim of a visual ocean acceptance test.
            bird.AirWaterM=walkingPatch.Sample(bird.Planet).ElevationM+12;bird.AirWaterAt=Time.unscaledTimeAsDouble+100;
            bird.AirHeight=bird.AirTargetHeight=4;UpdateAnimal(bird,0);
            Check(bird.Root.transform.localPosition.y>frame.SurfaceY(bird.Planet,bird.AirWaterM)+3,"An actual bird actor clears a supplied water surface rather than following its submerged bed");
            bird.AirWaterM=double.NaN;bird.AirWaterAt=0;bird.PerchSelected=true;bird.Perched=false;bird.PerchPosition=bird.Planet;bird.PerchHeight=.03;bird.AirHeight=bird.AirTargetHeight=.03;bird.PerchUntil=bird.Life+20;
            bird.Spawn.GroupId=99112;wildlifeGroups[99112]=new WildlifeGroup{Intent=WildlifeIntent.Rest};
            UpdateBirdBehaviour(bird,bird.Behaviour);
            Check(bird.Perched&&bird.BehaviourAction=="sleep"&&bird.TargetSpeed==0,"A resting bird enters its sleep behavior after an actual landing state");
        }
        finally
        {wildlifeGroups.Remove(99112);bird.Spawn.GroupId=oldGroup;bird.AirWaterM=double.NaN;bird.AirWaterAt=0;bird.PerchSelected=bird.Perched=false;bird.AirHeight=oldAir;bird.AirTargetHeight=oldTargetAir;bird.ActionUntil=0;bird.BehaviourAction="";AnimalRole(bird,"fly");UpdateAnimal(bird,0);}
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
        Place(wolf,px,pz+30,0);Place(deer,px,pz+15);deer.Speed=3;
        Check(AnimalVision(wolf,deer,15)==0,"Animal-to-animal vision respects the observer's actual blind direction");
        wolf.Heading=180;deer.CoverAt=Time.unscaledTimeAsDouble+10;deer.VisualCover=0;
        Check(AnimalVision(wolf,deer,15)>.1,"The same moving prey becomes visible when the observer turns toward it");
        wolf.BehaviourAction="sleep";wolf.ActionUntil=wolf.Life+20;
        Check(AnimalVision(wolf,deer,15)==0,"An actively sleeping observer has no animal-to-animal visual identification");
        wolf.BehaviourAction="";wolf.ActionUntil=0;
        wolf.Heading=0;wolf.Awareness=new WildlifeAwarenessState();huntingSpeed=0;
        ReportHuntingNoise(wolf.Root.transform.position+Vector3.forward*3,1.2,false);PerceivePlayer(wolf,.3,30);
        Check(wolf.Awareness.Sense==WildlifeSense.Sound&&!wolf.Awareness.ThreatIsPlayer,"An animal impact is heard at its real source without granting knowledge of the player");
        huntingNoiseUntil=0;huntingNoiseStrength=0;wolf.Heading=180;
        wolf.HuntTargetId=deer.Spawn.Id;wolf.HuntX=deer.X;wolf.HuntZ=deer.Z;wolf.HuntMemoryUntil=wolf.Life+15;RememberAnimal(wolf,false);
        var pursuit=WorldJson.Read<WildlifeIndividual>(WorldJson.Write(wildlifeRecords[wolf.Spawn.Id]));pursuit.Validate();
        Check(pursuit.HuntTargetId==deer.Spawn.Id&&pursuit.HuntMemorySeconds>14,"Native hunt memory serializes the actual last-seen prey position");
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();
        Check(!MayHuntPlayer(wolf,wolf.Behaviour,new WildlifeGroup{Hunger=.6,Members=8},10),"An eight-member ordinary wolf pack does not become an automatic human hunter");
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        Check(wolf.Spawn.Profile.runMps>12&&roo.Spawn.Profile.runMps>14&&deer.Spawn.Profile.runMps>11,"Loaded adult locomotion uses physical species priors rather than body-length multipliers");
        Place(wolf,px,pz+10,0);wolf.Speed=14;wolf.TargetSpeed=14;wolf.TargetHeading=0;wolf.ObstacleAt=0;
        var speedWall=GameObject.CreatePrimitive(PrimitiveType.Cube);speedWall.name="Hunting speed obstacle fixture";
        speedWall.transform.position=wolf.Root.transform.position+transform.TransformDirection(Vector3.forward*6+Vector3.up*1.5f);speedWall.transform.rotation=transform.rotation;speedWall.transform.localScale=new Vector3(4,4,.5f);Physics.SyncTransforms();
        reviewWildlifeBehaviour=true;
        try {AvoidAnimalSolids(wolf);Check(wolf.TargetSpeed<2&&Math.Abs(wolf.TargetHeading)>20,"A sprinting wolf detects a solid beyond the old 1.8 m ray and requests braking/turning");}
        finally {reviewWildlifeBehaviour=false;DestroyImmediate(speedWall);Physics.SyncTransforms();}
        Place(wolf,px,pz+12,0);Place(deer,px,pz+12,0);wolf.BehaviourAction="hunt";wolf.HuntTargetId=deer.Spawn.Id;wolf.AttackAt=0;
        reviewWildlifeBehaviour=true;
        double contactHealth=deer.Health;CheckMovingAnimalContact(wolf);
        Check(deer.Health<contactHealth&&wolf.AttackAt>Time.unscaledTimeAsDouble,"An active predator contacts prey between staggered decisions without passing through it");
        double afterContact=deer.Health;CheckMovingAnimalContact(wolf);Check(deer.Health==afterContact,"Frame-level proximity checks preserve the existing attack cooldown");reviewWildlifeBehaviour=false;
        Color32[] Capture(string name)
        {
            var old=view.targetTexture;var previous=RenderTexture.active;var rt=new RenderTexture(1280,800,24);rt.Create();view.targetTexture=rt;view.Render();RenderTexture.active=rt;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();var pixels=texture.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);view.targetTexture=old;RenderTexture.active=previous;rt.Release();Destroy(rt);return pixels;
        }
        void ActorImage(Animal a,string action,string name)
        {
            Place(a,px+4,pz+4);AnimalActivity(a,action,20);a.OldClip=null;a.BlendAge=1;a.Time=a.Clip.duration*.4;a.CollisionFrame=-1;UpdateAnimal(a,0);
            Check(PosedAnimal(a).All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Native "+name+" palette produces finite skin vertices");animalBoneBuffer.SetData(animalBoneData);
            var hidden=animals.Values.Where(other=>other!=a&&other.Renderer.enabled).ToArray();foreach(var other in hidden)other.Renderer.enabled=false;
            var bounds=a.Renderer.bounds;view.transform.position=bounds.center+new Vector3(2,.8f,-2).normalized*(float)Math.Max(1.6,a.Spawn.Profile.lengthM*1.8);view.transform.LookAt(bounds.center);Capture(name);foreach(var other in hidden)other.Renderer.enabled=true;
        }
        ActorImage(deer,"eat","01-deer-eating");ActorImage(deer,"sleep","02-deer-rest");ActorImage(wolf,"sleep","03-wolf-rest");ActorImage(roo,"eat","03b-kangaroo-grazing");
        foreach(var runner in new[]{deer,wolf,roo})
        {
            Place(runner,px+4,pz+4);AnimalRole(runner,"run");runner.OldClip=null;runner.BlendAge=1;runner.Speed=(float)runner.Spawn.Profile.runMps;
            var hidden=animals.Values.Where(other=>other!=runner&&other.Renderer.enabled).ToArray();foreach(var other in hidden)other.Renderer.enabled=false;
            var bounds=runner.Renderer.bounds;view.transform.position=bounds.center+Vector3.right*(float)(runner.Spawn.Profile.lengthM*2.2)+Vector3.up*.3f;view.transform.LookAt(bounds.center);
            for(int phase=0;phase<12;phase++)
            {
                runner.Time=phase/20d*Math.Min(3.2,runner.Speed/runner.NominalSpeed);runner.CollisionFrame=-1;UpdateAnimal(runner,0);animalBoneBuffer.SetData(animalBoneData);
                Capture("run-"+runner.Spawn.Profile.slug+"-"+phase.ToString("D2"));yield return null;
            }
            foreach(var other in hidden)other.Renderer.enabled=true;
        }
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
        var costs=new List<double>();int beforeDecisions=huntingDecisions,beforeDeferred=huntingDeferred;
        for(int sample=0;sample<120;sample++)
        {
            // Reset a dedicated forced sweep, then let the player advance to
            // another actual frame. The former one-frame loop measured mostly
            // already-deferred work and could not describe a typical decision.
            huntingDecisionFrame=-1;var timer=System.Diagnostics.Stopwatch.StartNew();
            foreach(var animal in animals.Values){if(animal.Dead)continue;animal.BehaviourAt=0;animal.LastPerception=Math.Max(0,animal.Life-.3);UpdateAnimalBehaviour(animal);}
            timer.Stop();if(sample>=10)costs.Add(timer.Elapsed.TotalMilliseconds);yield return null;
        }
        costs.Sort();File.WriteAllText(Path.Combine(output,"behaviour-cost.txt"),animals.Count+" actors; 120 frame-separated forced AI sweeps; warm p50 "+costs[costs.Count/2].ToString("F3")+" ms; p95 "+costs[(int)(costs.Count*.95)].ToString("F3")+" ms; maximum "+costs.Last().ToString("F3")+" ms; decisions "+(huntingDecisions-beforeDecisions)+" / deferred "+(huntingDeferred-beforeDeferred)+". This measures decision work with its 1 ms soft budget, not total game frame rate.");
        Check(animals.Values.All(a=>a.Exertion.Burst>=0&&a.Exertion.Burst<=1&&a.Awareness.Suspicion>=0&&a.Awareness.Suspicion<=1),"Mixed actor decisions retain bounded physical and awareness state");reviewWildlifeBehaviour=false;paused=true;
        if(Environment.GetCommandLineArgs().Contains("--worldgen-hunting-approach-check"))yield return CheckHuntingApproach(roo,output,report);
    }
    IEnumerator CheckHuntingRevisionFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("HUNT REVISION "+text);report.Add("PASS "+text);Debug.Log("HUNT REVISION PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();humanCamera.Restore(0,false);paused=false;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);
        foreach(string name in new[]{"red-kangaroo","deer","duck"})ExecuteAnimalSpawnCommand("/spawn "+name);
        double end=Time.unscaledTimeAsDouble+90;
        while(new[]{"red-kangaroo","deer","duck"}.Any(s=>!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug==s))&&Time.unscaledTimeAsDouble<end)yield return null;
        var roo=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="red-kangaroo");var deer=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer");var duck=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="duck");
        paused=true;Check(roo!=null&&deer!=null&&duck!=null,"Original kangaroo, deer and duck load through normal asset admission");
        double px=motion.X,pz=motion.Z;var oldTiles=tiles;var weather=CurrentWeather;double oldCoverAt=huntingCoverAt;
        // Controlled resident placement-grid fixture. It exercises the native
        // runtime sampler/perception, not an assertion about a natural biome.
        double floor=Ground(px,pz)+200;var fixture=new Dictionary<TerrainTileKey,Tile>();
        double size=TerrainLod.BaseSizeM*HorizontalScale;int cx=(int)Math.Floor(px/size),cz=(int)Math.Floor(pz/size);
        for(int x=cx-2;x<=cx+2;x++)for(int z=cz-2;z<=cz+12;z++)
        {var cover=new float[64];for(int k=0;k<4;k++){cover[k*16+3]=.8f;cover[k*16+5]=.8f;cover[k*16+13]=3;cover[k*16+15]=(float)floor;}fixture[new TerrainTileKey(x,z,0,HorizontalScale)]=new Tile{Segments=1,Heights=new[]{(float)floor,(float)floor,(float)floor,(float)floor},Cover=cover};}
        try
        {
            tiles=fixture;motion.Place(px,pz,floor);motion.Flying=false;
            CurrentWeather=new WeatherFrame(24,.4,0,0,0,0,1500,500,8000,0,0,0,0);
            void Place(double distance)
            {roo.X=px;roo.Z=pz+distance;roo.Planet=frame.Position(roo.X,roo.Z);roo.GroundY=floor;roo.Heading=roo.TargetHeading=180;roo.Speed=roo.TargetSpeed=0;roo.Awareness=new WildlifeAwarenessState();UpdateAnimal(roo,0);}
            void Observe(double speed,int count)
            {huntingSpeed=speed;huntingCover=0;huntingLight=1;huntingCoverAt=Time.unscaledTimeAsDouble+100;huntingNoiseUntil=0;for(int i=0;i<count;i++)PerceivePlayer(roo,.3,Math.Abs(roo.Z-pz));}
            Place(50);Check(HuntingColumn(px,pz,out var field,out var ground,out var slope)&&field.Stature==3&&slope>.9&&Math.Abs(ground-floor)<.001,"Native perception reads the exact resident grass placement channels and surface");
            Check(WildlifeVisibility(roo,px,pz,motion.Y+humanEyeOffset)==0,"A 50m eye-level sight line through tall dense grass is actually blocked");
            Observe(0,200);Check(roo.Awareness.Mode==WildlifeAwareness.Relaxed&&roo.Awareness.Sense!=WildlifeSense.Sight,"A motionless silent concealed person remains unseen after a full minute");
            roo.Awareness=new WildlifeAwarenessState();Observe(3,100);Check(roo.Awareness.Mode!=WildlifeAwareness.Fleeing&&roo.Awareness.Sense!=WildlifeSense.Sight,"Audible movement in occluding grass does not become visual knowledge or automatic distant flight");
            foreach(var tile in fixture.Values)for(int k=0;k<4;k++){tile.Cover[k*16+3]=0;tile.Cover[k*16+5]=0;}
            Place(50);Check(WildlifeVisibility(roo,px,pz,motion.Y+humanEyeOffset)>.99,"Removing the grass obstruction restores the same geometric sight line");
            Observe(3,100);Check(roo.Awareness.Mode!=WildlifeAwareness.Fleeing&&roo.Awareness.Suspicion>.45,"A clearly visible distant moving person is watched without an immediate full-speed escape");
            Place(20);Observe(3,1);Check(roo.Awareness.Mode!=WildlifeAwareness.Fleeing,"A nearby first sighting allows a reaction interval");
            Observe(3,40);Check(roo.Awareness.Mode==WildlifeAwareness.Fleeing,"Sustained credible nearby approach still triggers escape");
        }
        finally{tiles=oldTiles;CurrentWeather=weather;huntingCoverAt=oldCoverAt;motion.Place(px,pz,Ground(px,pz));roo.X=px;roo.Z=pz+20;roo.Planet=frame.Position(roo.X,roo.Z);roo.GroundY=Ground(roo.X,roo.Z);roo.Awareness=new WildlifeAwarenessState();UpdateAnimal(roo,0);UpdateHumans();UpdateCamera();}
        ulong gid=991990;wildlifeGroups[gid]=new WildlifeGroup{Position=frame.Position(px,pz+150),Heading=0,Members=1};
        try{var moved=new FaunaPrototypeSpawn{Profile=deer.Spawn.Profile,GroupId=gid,Position=frame.Position(px,pz+2300)};Check(SpawnViewDistance(moved)<250,"Model admission uses a tracked group's current position rather than its distant original home");}
        finally{wildlifeGroups.Remove(gid);}
        int oldMask=view.cullingMask;var oldRotation=view.transform.localRotation;var oldPosition=view.transform.localPosition;var oldClear=view.clearFlags;var oldColour=view.backgroundColor;
        Color32[] Pixels(string name)
        {
            var prior=RenderTexture.active;var old=view.targetTexture;var rt=new RenderTexture(1280,800,24);rt.Create();view.targetTexture=rt;view.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=prior;view.targetTexture=old;rt.Release();Destroy(rt);Destroy(image);return pixels;
        }
        try
        {
            view.cullingMask=1<<26;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;
            view.transform.localPosition=oldPosition+Vector3.up*200;view.transform.localRotation=Quaternion.identity;
            foreach(var pair in new[]{(deer,550f),(duck,200f)})
            {
                var a=pair.Item1;int layer=a.Root.layer;var oldPos=a.Root.transform.localPosition;var oldRot=a.Root.transform.localRotation;
                try
                {
                    a.Fade=1;AnimalRole(a,a==duck?"fly":"idle");UpdateAnimal(a,0);animalBoneBuffer.SetData(animalBoneData);
                    a.Root.layer=26;a.Root.transform.localPosition=view.transform.localPosition+Vector3.forward*pair.Item2;a.Root.transform.localRotation=Quaternion.Euler(0,a==duck?0:90,0);
                    float fade=Mathf.Clamp01(((float)a.Spawn.Profile.VisibleRadiusM-pair.Item2)/Math.Max(4,(float)a.Spawn.Profile.VisibleRadiusM*.12f));a.Props.SetFloat("_Fade",fade);a.Renderer.SetPropertyBlock(a.Props);
                    a.Renderer.enabled=false;var empty=Pixels(a.Spawn.Profile.slug+"-empty");a.Renderer.enabled=true;var present=Pixels(a.Spawn.Profile.slug+"-far");
                    int changed=present.Where((p,i)=>Math.Abs(p.r-empty[i].r)+Math.Abs(p.g-empty[i].g)+Math.Abs(p.b-empty[i].b)>2).Count();
                    Check(fade>.99&&changed>0,"Actual GPU "+a.Spawn.Profile.slug+" skin remains rendered at "+pair.Item2+"m; changed pixels="+changed);
                }
                finally{a.Renderer.enabled=true;a.Root.layer=layer;a.Root.transform.localPosition=oldPos;a.Root.transform.localRotation=oldRot;}
            }
        }
        finally{view.cullingMask=oldMask;view.transform.localPosition=oldPosition;view.transform.localRotation=oldRotation;view.clearFlags=oldClear;view.backgroundColor=oldColour;}
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();motion.Place(px,pz,Ground(px,pz));UpdateHumans();UpdateCamera();
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();var oldKeyboard=testKeyboard;testKeyboard=keyboard;
        var oldBackground=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            paused=false;double start=motion.Y;float lowest=100;bool airborne=false;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Space));yield return null;yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            end=Time.unscaledTimeAsDouble+3;
            while(Time.unscaledTimeAsDouble<end){airborne|=motion.Y>start+.1;lowest=Math.Min(lowest,(float)(Vitals.Stamina/Math.Max(1,Vitals.MaximumStamina)*100));yield return null;}
            Check(airborne&&lowest>95&&Vitals.Exertion.Burst>.99,"A real Space-key jump has a small cost and recovers within three seconds");
        }
        finally{paused=true;testKeyboard=oldKeyboard;UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=oldBackground;}
        Vitals.Restore();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
    IEnumerator CheckHuntingApproach(Animal subject,string output,List<string> report)
    {
        // Real frame/input trials complement isolated perception tests. Exact
        // flight distances are observations, not assertions on procedural cover.
        var oldKeyboard=UnityEngine.InputSystem.Keyboard.current;var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        var oldBackground=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;var weather=TestWeatherOverride;
        keyboard.MakeCurrent();TestKeyboard(keyboard);UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        var rows=new List<string>{"trial,seconds,player_x,player_z,animal_x,animal_z,distance,awareness,sense,suspicion,animal_speed,burst,breath,wind_north"};
        var summaries=new List<string>();double px=motion.X,pz=motion.Z;
        try
        {
            foreach(var other in animals.Values.Where(a=>a!=subject).ToArray())RemoveAnimal(other.Spawn.Id);
            animalWanted.Clear();animalManualWanted.Clear();nextAnimalPlan=Time.realtimeSinceStartupAsDouble+1000;animalPlanAt=Position;
            PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();humanCamera.Restore(0,false);humanCameraDistance=0;
            foreach(string trial in new[]{"walk-downwind","crouch-downwind","still-downwind","crouch-upwind"})
            {
                paused=true;Vitals.Restore();motion.Flying=false;motion.Place(px,pz,CampFloor(px,pz));heading=0;pitch=0;UpdateCamera();
                subject.X=px;subject.Z=pz+24;subject.Planet=frame.Position(subject.X,subject.Z);subject.GroundY=CampFloor(subject.X,subject.Z);
                subject.Heading=subject.TargetHeading=trial.StartsWith("still")?180:0;subject.Health=subject.MaximumHealth;subject.TargetSpeed=subject.Speed=0;
                subject.Awareness=new WildlifeAwarenessState();subject.Exertion=new ExertionState();subject.ThreatUntil=0;subject.LastPerception=0;subject.BehaviourAt=0;
                subject.OldClip=null;subject.BlendAge=1;AnimalActivity(subject,"eat",120);UpdateAnimal(subject,0);
                double wind=trial.EndsWith("upwind")?3:-3;TestWeatherOverride=new WeatherFrame(22,.4,0,0,0,0,1500,500,8000,0,wind,3,0);
                huntingCoverAt=0;huntingNoiseUntil=0;huntingNoiseStrength=0;nextWildlifeIndex=0;IndexWildlifeNeighbours();
                var keys=trial.StartsWith("still")?new UnityEngine.InputSystem.LowLevel.KeyboardState():trial.StartsWith("crouch")?new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftCtrl):new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.C);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,keys);reviewWildlifeBehaviour=true;paused=false;
                double started=Time.unscaledTimeAsDouble,due=started,end=started+24,firstFlee=-1,minGap=double.MaxValue;
                while(Time.unscaledTimeAsDouble<end)
                {
                    double elapsed=Time.unscaledTimeAsDouble-started,gap=Math.Sqrt(Math.Pow(subject.X-motion.X,2)+Math.Pow(subject.Z-motion.Z,2));minGap=Math.Min(minGap,gap);
                    if(subject.Awareness.Mode==WildlifeAwareness.Fleeing&&firstFlee<0)firstFlee=gap;
                    if(Time.unscaledTimeAsDouble>=due)
                    {
                        due+=.25;rows.Add(FormattableString.Invariant($"{trial},{elapsed:F3},{motion.X:F3},{motion.Z:F3},{subject.X:F3},{subject.Z:F3},{gap:F3},{subject.Awareness.Mode},{subject.Awareness.Sense},{subject.Awareness.Suspicion:F4},{subject.Speed:F3},{subject.Exertion.Burst:F4},{subject.Exertion.Breath:F4},{CurrentWeather.WindNorthMps:F2}"));
                    }
                    yield return null;
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;paused=true;
                double moved=Math.Sqrt(Math.Pow(motion.X-px,2)+Math.Pow(motion.Z-pz,2));
                summaries.Add(FormattableString.Invariant($"{trial}: player travel {moved:F2}m, closest {minGap:F2}m, first flee distance {firstFlee:F2}m (-1 = no flee)."));
                if(!trial.StartsWith("still")&&moved<1)throw new InvalidOperationException("Approach trial did not receive actual movement input: "+trial);
            }
            File.WriteAllLines(Path.Combine(output,"approach-frames.csv"),rows);File.WriteAllLines(Path.Combine(output,"approach-summary.txt"),summaries);
            report.Add("PASS Four 24-second actual movement/perception trials completed with recorded cover-dependent outcomes");
        }
        finally
        {
            TestWeatherOverride=weather;reviewWildlifeBehaviour=false;paused=true;TestKeyboard(null);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=oldBackground;
        }
    }
    void WriteWildlifeProfileAudit(string output,List<string> report)
    {
        string Csv(object value)=>"\""+Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";
        var bindings=FaunaPrototypePlanner.BindProfiles(sourceWorld.Ecology.Taxa,animalProfiles);
        var rows=new List<string>{"model,native_taxon,medium,diel,social,min_group,max_group,hunt_style,live_prey,carrion,defend_m,seasonal_rut,hibernates,migrates,sight_m,hearing_m,smell_m,cheap_decisions,walk_mps,burst_mps,source_run_stride_mps"};int families=0;
        foreach(var model in animalProfiles.OrderBy(p=>p.slug))
        {
            families++;var native=bindings.Where(p=>p.Value.slug==model.slug).Select(p=>p.Key).ToArray();if(native.Length==0)native=new[]{-1};
            foreach(int id in native)
            {
                var data=id<0?null:sourceWorld.Ecology.Taxa[id];var b=AnimalBehaviour(model,id);var n=b.NaturalHistory;
                var senses=WildlifeSenses.Describe(model.slug,data?.MassKg??1,b.Predator,model.medium=="air",model.tiny,data?.Name??"");
                rows.Add(string.Join(",",new object[]{model.slug,data?.Name??"unbound model",model.medium,n.Diel,n.Social,b.GroupMin,b.GroupMax,n.Hunt,b.CanHunt,b.CanScavenge,b.DefendM,b.SeasonalRut,b.Hibernates,b.Migratory,senses.VisionM,senses.HearingM,senses.SmellM,b.SmallBudget||model.tiny,model.walkMps,model.runMps,model.ClipSpeed("run")}.Select(Csv)));
            }
        }
        File.WriteAllLines(Path.Combine(output,"animal-profile-audit.csv"),rows);
        if(families!=177)throw new InvalidOperationException("Wildlife profile audit omitted model families.");
        report.Add("PASS Exported the active natural-history and sense configuration for all 177 model families");
    }
    void CheckSmallSchoolDecisions(AnimalModel model,List<string> report)
    {
        // Synthetic decision actors isolate schooling math, not visual fish
        // acceptance. They reuse a loaded clip container and never render.
        var saved=wildlifeNeighbours.ToArray();var roots=new List<GameObject>();
        Animal Fish(double x,double z,bool predator=false)
        {
            var root=new GameObject("School decision probe");roots.Add(root);root.transform.localPosition=new Vector3((float)x,0,(float)z);
            var p=animalProfiles.First(a=>a.slug=="small-native-freshwater-fish-guild");
            return new Animal{Root=root,Model=model,Spawn=new FaunaPrototypeSpawn{Profile=p},X=x,Z=z,Health=1,MaximumHealth=1,MassKg=predator?2:.02,Heading=90,Speed=predator?1:0,Behaviour=new WildlifeBehaviourProfile{CanHunt=predator}};
        }
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("SCHOOL "+why);report.Add("PASS "+why);}
        try
        {
            var fish=Fish(5000,5000);var mate=Fish(5002,5001);var predator=Fish(5000,5003,true);
            var neighbours=new List<Animal>{fish,mate,predator};wildlifeNeighbours.Clear();wildlifeNeighbours[(125,125)]=neighbours;
            Check(UpdateSmallSchool(fish)&&Math.Abs(fish.TargetSpeed-fish.Spawn.Profile.runMps)<1e-6&&Math.Cos(fish.TargetHeading*Math.PI/180)<-.9,"Cheap schooling reacts away from a nearby moving aquatic predator");
            predator.Root.transform.localPosition+=Vector3.up*10;
            Check(UpdateSmallSchool(fish)&&Math.Abs(fish.TargetSpeed-fish.Spawn.Profile.walkMps)<1e-6&&Math.Sin(fish.TargetHeading*Math.PI/180)>.5,"School cohesion resumes and does not sense a vertically distant predator as adjacent");
            neighbours.Remove(mate);neighbours.Remove(predator);Check(!UpdateSmallSchool(fish),"An isolated small fish falls back to its ordinary bounded wandering decision");
        }
        finally{wildlifeNeighbours.Clear();foreach(var pair in saved)wildlifeNeighbours.Add(pair.Key,pair.Value);foreach(var root in roots)DestroyImmediate(root);}
    }
}

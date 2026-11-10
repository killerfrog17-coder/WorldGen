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
    public sealed class NpcResidentCheckRow
    {
        public long Id,Group;public string Name,Activity,Action,Role;public double Age;public float Speed,TargetSpeed,X,Y,Z;public int AgeBody;public string[] Props;
    }
    public sealed class NpcSceneCheckInfo
    {
        public string Context,HistoryHash,Residence,Design,Cover;public double Year,MeanC,Rain,Canopy,Aridity,Coast,River,LocalHour;public int People,Houses;
    }
    public static IEnumerator CheckLivingHumans(string path,string output)
    {
        Directory.CreateDirectory(output);RecordNpcCheckPackage(output);var checks=new List<string>();
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
        string Option(string key,string fallback){var arguments=Environment.GetCommandLineArgs();int index=Array.IndexOf(arguments,key);return index>=0&&index+1<arguments.Length?arguments[index+1]:fallback;}
        string context=Option("--worldgen-npc-context","temperate"),residence=Option("--worldgen-npc-residence","any");double hour=double.Parse(Option("--worldgen-npc-hour","9"),System.Globalization.CultureInfo.InvariantCulture);
        double scale=double.Parse(Option("--worldgen-npc-scale","0.1"),System.Globalization.CultureInfo.InvariantCulture);
        var play=new PlaySession();play.Settings.HorizontalScale=scale;play.Settings.RealMinutesPerDay=60;play.Settings.CalendarDaysPerDay=365.2425/24;play.Mode=GameMode.Creative;
        play.SolarDays=((hour-9+24)%24)/24;play.PendingHistorySeconds=play.SolarDays*SimulationTime.Day*play.Settings.CalendarDaysPerDay;
        double Suitability(ForagerCohort c)
        {
            var d=state.Foragers.Districts[c.District];return context=="cold"?-d.MeanC:context=="wet"?d.AnnualRainMm:context=="arid"?-d.Aridity:context=="woodland"?d.Canopy:context=="coast"?d.Coast:1-Math.Abs(d.MeanC-16)/50;
        }
        var planner=new HumanLocalLife(world,engine);HumanGroupSituation chosen=null;
        foreach(var cohort in state.Foragers.Cohorts.Where(c=>engine.ForagerBandCount(state,c)>0&&(residence=="any"||state.Foragers.Cultures[c.Culture].Residence.ToString()==residence)).OrderByDescending(Suitability).ThenByDescending(c=>c.Population))
        {
            var region=state.Foragers.Districts[cohort.District];
            if(context=="cold"&&region.MeanC>=8||context=="coast"&&region.Coast<.15||context=="wet"&&region.AnnualRainMm<1400)continue;
            for(int ordinal=0;ordinal<engine.ForagerBandCount(state,cohort);ordinal++)
            {
                long id=((long)(cohort.Id+1)<<32)|(uint)(ordinal+1);if(!HumanLocalLife.SpatialAdmission(world.Recipe.Seed,id,scale))continue;
                var group=planner.Group(state,new HumanLifeClock(SimulationTime.Seconds(state)+play.PendingHistorySeconds,play.SolarDays,60,play.Settings.CalendarDaysPerDay,scale),cohort.Id,ordinal);
                double prime=Math.Atan2(group.Position.Y,group.Position.X)*180/Math.PI;
                group=planner.Group(state,new HumanLifeClock(SimulationTime.Seconds(state)+play.PendingHistorySeconds,play.SolarDays,60,play.Settings.CalendarDaysPerDay,scale,prime),cohort.Id,ordinal);
                if(group.Camp.ShelterAvailable&&group.Phase==HumanCampPhase.Occupied&&(!Environment.GetCommandLineArgs().Contains("--worldgen-npc-permanent")||group.PermanentBase)){chosen=group;break;}
            }
            if(chosen!=null)break;
        }
        Check(chosen!=null,"An admitted real simulated community exists");
        play.PrimeMeridianLongitude=Math.Atan2(chosen.Position.Y,chosen.Position.X)*180/Math.PI;
        string workerPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../TerrainWorker/WorldGen.Cli.exe"));
        var producer=new TerrainMeshProcess(workerPath,world,scale,Path.GetFullPath(path));
        var setup=Task.Run(()=>{producer.Prepare(default);return LocalTerrainDerivationCache.Create(world,horizontalScale:scale);});
        bool performance=Environment.GetCommandLineArgs().Contains("--worldgen-npc-perf");
        TerrainVideoOptions.Current.ViewDistanceM=performance?6000:1800;TerrainVideoOptions.Revision++;
        if(performance){TerrainVideoOptions.ApplyAllPlantQuality(2);TerrainVideoOptions.Current.Quality=2;TerrainVideoOptions.SurfaceQuality=2;TerrainVideoOptions.SurfaceTextures=true;}
        var owner=new GameObject("Living human actual-terrain fixture").AddComponent<LocalTerrainExplorer>();owner.viewDistance=performance?6000:1800;
        owner.Initialize(setup,chosen.Position,()=>{},world,play,worker:producer);
        owner.npcModel??=new NpcModelClient();owner.npcModel.Config.Enabled=false;
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        double firstStatusAt=0;
        while((!owner.Ready||!owner.CenterReady||owner.livingPeople.Count==0)&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(Time.realtimeSinceStartupAsDouble>=firstStatusAt)
            {
                firstStatusAt=Time.realtimeSinceStartupAsDouble+5;
                File.WriteAllText(Path.Combine(output,"terrain-start-status.txt"),"Ready "+owner.Ready+"; centre "+owner.CenterReady+"; tiles "+owner.TileCount+" / "+owner.WantedCount+"; residents "+owner.livingPeople.Count+"; error "+owner.Error+"; position "+owner.motion.X+","+owner.motion.Y+","+owner.motion.Z+"; target "+chosen.Position.X+","+chosen.Position.Y+","+chosen.Position.Z);
            }
            yield return null;
        }
        Check(owner.Ready&&owner.CenterReady,"Actual local terrain and centre tiles are ready");
        Check(owner.livingPeople.Count>0,"Natural residents stream from world history, without a spawn command");
        var groupRuntime=owner.livingGroups[chosen.Band.Id];
        var expectedHome=groupRuntime.Activities.Where(p=>p.Activity!=HumanActivity.Travel&&p.Activity!=HumanActivity.GatherPlants&&p.Activity!=HumanActivity.GatherFuel&&p.Activity!=HumanActivity.FetchWater&&p.Activity!=HumanActivity.Carried)
            .Where(p=>owner.HumanDistance(owner.LivingPosition(groupRuntime,p.Position),owner.Position)<450).Select(p=>p.Person).ToArray();
        int expectedNear=expectedHome.Length;
        double statusAt=0;bool followedValidatedCamp=false;
        while((!groupRuntime.CampLoaded||expectedHome.Any(id=>!owner.livingPeople.ContainsKey(id)))&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            // The broad camp estimate is not its terrain-validated clearing.
            // Follow the real accepted site before asking its near field to load;
            // otherwise a valid camp beyond 700m can never satisfy this fixture.
            if(groupRuntime.Grounded&&!followedValidatedCamp)
            {
                followedValidatedCamp=true;double distance=owner.HumanDistance(groupRuntime.Anchor,owner.Position);
                File.WriteAllText(Path.Combine(output,"validated-site-follow.txt"),"Actual accepted camp distance from initial review position: "+distance+"m; attempts="+groupRuntime.PlacementAttempt);
                if(distance>450)
                {
                    TerrainHandoff.Coordinates(owner.frame,groupRuntime.Anchor,out double siteX,out double siteZ);owner.motion.Place(siteX,siteZ,owner.VisibleGround(siteX,siteZ,0));
                    deadline=Math.Max(deadline,Time.realtimeSinceStartupAsDouble+120);
                }
            }
            if(Time.realtimeSinceStartupAsDouble>=statusAt){statusAt=Time.realtimeSinceStartupAsDouble+5;File.WriteAllText(Path.Combine(output,"streaming-status.txt"),"People "+owner.livingPeople.Count+" / expected near "+expectedNear+"; ground "+groupRuntime.Grounded+"; loaded "+groupRuntime.CampLoaded+"; attempt "+groupRuntime.PlacementAttempt+"; survey "+groupRuntime.SiteSurveyRadius+"m / "+groupRuntime.SiteSurvey?.Milliseconds+"ms; objects "+owner.Camp.Objects.Count);}
            yield return null;
        }
        Check(groupRuntime.CampLoaded,"Natural camp passes local ground validation and is loaded");
        Check(expectedHome.All(id=>owner.livingPeople.ContainsKey(id)),"Scheduled home residents are actually present; distant foragers are not required to crowd the camp");
        Check(owner.Camp.Objects.Any(c=>c.NpcGroup==chosen.Band.Id),"Camp consists of real player-compatible objects/materials");
        owner.Camp.Validate();Check(true,"Native camp validates with real terrain footing surveys");
        Check(owner.Camp.Objects.Any(c=>c.NpcGroup==chosen.Band.Id&&c.Blueprint!=null)&&owner.Camp.Objects.Where(c=>c.NpcGroup==chosen.Band.Id&&c.Blueprint!=null).All(c=>CampBlueprints.Complete(c.Blueprint)),"Occupied camp shelters retain every fitted roof/frame component");
        TerrainHandoff.Coordinates(owner.frame,groupRuntime.Anchor,out double anchorX,out double anchorZ);
        var plantKeys=new[]{owner.frame.Position(anchorX,anchorZ),owner.frame.Position(anchorX-45,anchorZ-45),owner.frame.Position(anchorX+45,anchorZ+45)}.Select(p=>owner.scatter.Address(p)).Distinct().ToArray();
        double plantsUntil=Time.realtimeSinceStartupAsDouble+120;
        while(plantKeys.Any(k=>!owner.scatterChunks.TryGetValue(k,out var chunk)||chunk.Groups==null)&&Time.realtimeSinceStartupAsDouble<plantsUntil)yield return null;
        Check(plantKeys.All(k=>owner.scatterChunks.TryGetValue(k,out var chunk)&&chunk.Groups!=null),"Actual surrounding scatter pages are resident before accepting camp environment captures");
        File.WriteAllText(Path.Combine(output,"camp.json"),WorldJson.Write(owner.Camp));
        Check(owner.livingPeople.Values.All(p=>p.ResidentId!=0&&p.Resident!=null&&p.Routine!=null),"Physical NPCs retain named family identities and actual scheduled tasks");
        Check(owner.livingGroups.Values.All(g=>g.Situation.DistanceToPlayerM<=51600),"Broad local group tracking is bounded at 50km plus foraging catchment");
        var target=new RenderTexture(1536,864,24);target.Create();var prior=RenderTexture.active;owner.view.targetTexture=target;
        void Image(string name,Vector3 offset)
        {
            TerrainHandoff.Coordinates(owner.frame,groupRuntime.Anchor,out double x,out double z);float floor=(float)owner.VisibleGround(x,z,0);
            var focus=new Vector3((float)(x-owner.originX),floor+1,(float)(z-owner.originZ));owner.view.transform.localPosition=focus+offset;owner.view.transform.LookAt(owner.transform.TransformPoint(focus));
            owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;var image=new Texture2D(1536,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1536,864),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=prior;
        }
        owner.SetDaylight(hour,SimulationTime.Seconds(state)/(double)SimulationTime.Year);
        for(int i=0;i<180;i++)yield return null;
        CheckLivingCampGround(owner,groupRuntime,output,checks);
        Image("camp-first",new Vector3(18,8,24));Image("residents-first",new Vector3(0,2,15));
        foreach(var house in owner.Camp.Objects.Where(c=>c.NpcGroup==chosen.Band.Id&&c.Blueprint!=null).Take(3))
        {
            TerrainHandoff.Coordinates(owner.frame,house.Position,out double hx,out double hz);float floor=(float)owner.VisibleGround(hx,hz,0);
            float side=house.Blueprint.Design==CampDesign.LeanTo?-1:1;var focus=new Vector3((float)(hx-owner.originX),floor+.7f,(float)(hz-owner.originZ));
            owner.view.transform.localPosition=focus+Quaternion.Euler(0,(float)house.Heading,0)*new Vector3(.6f,.45f,(float)(house.Blueprint.DepthM*.5+3)*side);owner.view.transform.LookAt(owner.transform.TransformPoint(focus));
            owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;var entrance=new Texture2D(1536,864,TextureFormat.RGB24,false);entrance.ReadPixels(new Rect(0,0,1536,864),0,0);entrance.Apply();File.WriteAllBytes(Path.Combine(output,"entrance-"+house.NpcHousehold+".png"),entrance.EncodeToPNG());Destroy(entrance);RenderTexture.active=prior;
        }
        foreach(var resident in owner.livingPeople.Values.GroupBy(p=>p.CurrentActivity).Select(g=>g.First()).Take(12))
        {
            var focus=resident.Root.transform.localPosition+Vector3.up*(resident.Resident.AgeYears<7?.45f:.8f);
            owner.view.transform.localPosition=focus+Quaternion.Euler(0,resident.Yaw,0)*new Vector3(2,.25f,3);owner.view.transform.LookAt(owner.transform.TransformPoint(focus));
            owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;var closeImage=new Texture2D(1536,864,TextureFormat.RGB24,false);closeImage.ReadPixels(new Rect(0,0,1536,864),0,0);closeImage.Apply();File.WriteAllBytes(Path.Combine(output,"task-"+resident.CurrentActivity+".png"),closeImage.EncodeToPNG());Destroy(closeImage);RenderTexture.active=prior;
        }
        owner.view.targetTexture=null;
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-walk"))yield return CheckLivingWalking(owner,groupRuntime,output,checks);
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-crowd"))yield return PrepareLivingCrowd(owner,output);
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-perf"))yield return MeasureLivingHumans(owner,output);
        File.WriteAllText(Path.Combine(output,"physical-residents.json"),WorldJson.Write(owner.livingPeople.Values.Select(a=>new NpcResidentCheckRow{Id=a.ResidentId,Group=a.GroupId,Name=a.Resident.Name,Age=a.Resident.AgeYears,Activity=a.CurrentActivity.ToString(),Role=a.Routine.Role,Speed=a.Speed,TargetSpeed=a.TargetSpeed,Action=a.Shared.ActionRole,AgeBody=a.AgeModel?.Years??99,X=a.Root.transform.localPosition.x,Y=a.Root.transform.localPosition.y,Z=a.Root.transform.localPosition.z,Props=a.NpcProps.Select(p=>p.Item.Name).ToArray()}).ToArray()));
        var district=state.Foragers.Districts[chosen.Band.District];
        File.WriteAllText(Path.Combine(output,"scene-info.json"),WorldJson.Write(new NpcSceneCheckInfo{Context=context,HistoryHash=state.Hash,Year=SimulationTime.Seconds(state)/(double)SimulationTime.Year,Residence=chosen.Band.Residence.ToString(),Design=chosen.Camp.Design.ToString(),Cover=chosen.Camp.Cover.ToString(),MeanC=district.MeanC,Rain=district.AnnualRainMm,Canopy=district.Canopy,Aridity=district.Aridity,Coast=district.Coast,River=district.River,LocalHour=groupRuntime.Situation.LocalHour,People=groupRuntime.Residents.Members.Length,Houses=owner.Camp.Objects.Count(c=>c.NpcGroup==chosen.Band.Id&&c.Blueprint!=null)}));
        File.WriteAllText(Path.Combine(output,"community.json"),WorldJson.Write(groupRuntime.Residents));
        var packet=new PlaySession();owner.StorePlayer(packet);packet.Validate();var restored=WorldJson.Read<PlaySession>(WorldJson.Write(packet));restored.Validate();
        Check(restored.Humans.Camps.Count==packet.Humans.Camps.Count&&restored.Camp.Objects.Select(c=>c.Id).SequenceEqual(packet.Camp.Objects.Select(c=>c.Id)),"Named camp objects and local memories survive actual player capture and serialization");
        var save=Task.Run(()=>new WorldLibrary(Path.Combine(output,"save-library")).SaveNew("NPC camp",world,play:packet));while(!save.IsCompleted)yield return null;
        var entry=save.GetAwaiter().GetResult();var binary=Task.Run(()=>WorldLibrary.ReadPlaySession(entry.SnapshotPath));while(!binary.IsCompleted)yield return null;
        var fromDisk=binary.GetAwaiter().GetResult();fromDisk.Validate();
        Check(fromDisk.Humans.Camps.Count==packet.Humans.Camps.Count&&fromDisk.Camp.Objects.Select(c=>c.Id).SequenceEqual(packet.Camp.Objects.Select(c=>c.Id)),"Actual named binary save cold-loads resident camp provenance and exact material-backed objects");
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-cycle"))yield return CheckLivingCampCycle(owner,groupRuntime,output,checks);
        var actor=owner.livingPeople.Values.First(p=>p.Resident.AgeYears>=18);long identity=actor.ResidentId;owner.DamageHuman(actor,12,null,Vector3.forward);owner.CaptureLivingHumans();
        Check(play.Humans.People.Single(p=>p.Person==identity).Health==actor.Health&&owner.NpcMemoryFor(identity).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Assault),"Aggression preserves wounds and the actual victim's local memory");
        owner.UnloadLivingCamp(groupRuntime);owner.LoadLivingCamp(groupRuntime);owner.Camp.Validate();
        Check(owner.Camp.Objects.Select(c=>c.Id).Distinct().Count()==owner.Camp.Objects.Count,"Unloading/revisiting a camp does not duplicate its objects");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("LIVING HUMANS PASS "+checks.Count);owner.view.targetTexture=null;target.Release();Destroy(target);Application.Quit(0);
    }
}

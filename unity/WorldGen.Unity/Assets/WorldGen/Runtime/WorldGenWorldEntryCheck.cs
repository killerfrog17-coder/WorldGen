using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckWorldEntry()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        Application.targetFrameRate=30;
        var report=new List<string>();
        void Check(bool ok,string text){if(!ok){Debug.LogError("ENTRY CHECK "+text);Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("ENTRY PASS "+text);}
        double deadline=Time.realtimeSinceStartupAsDouble+240;
        while((Busy||!worldReady||humans==null||humanStep!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(worldReady&&!Busy&&humanStep==null,"existing saved world is ready");humanPlaying=false;
        ChooseWorldEntry();Check(worldEntryOpen,"entry choices open");yield return new WaitForSecondsRealtime(2);yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"entry-options.png"),image.EncodeToPNG());Destroy(image);
        StartWorldEntry(false);Check(worldEntryWork!=null,"creative selection starts its background task");deadline=Time.realtimeSinceStartupAsDouble+240;
        bool observedCoverageLoading=false;Camera entryCamera=null;
        while((worldEntryOpen||terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady||!terrainExplorer.InitialViewReady)&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(terrainExplorer!=null&&!terrainExplorer.InitialViewReady&&!observedCoverageLoading)
            {
                entryCamera=terrainExplorer.GetComponentInChildren<Camera>();
                Check(terrainExplorer.Paused&&entryCamera!=null&&entryCamera.cullingMask==0,"loading hides incomplete terrain and pauses gameplay");observedCoverageLoading=true;
            }
            if(worldEntryError.Length>0)Check(false,"entry task failed: "+worldEntryError);yield return null;
        }
        Check(terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.CenterReady,"random-land entry loads actual ground");
        Check(observedCoverageLoading&&terrainExplorer.InitialViewReady&&!terrainExplorer.Paused&&terrainExplorer.HasInitialViewCoverage(),"first playable view has actual surrounding coverage and releases loading pause");
        Check(entryCamera!=null&&entryCamera.cullingMask!=0,"first playable view restores the actual camera");
        Check(play.Identity==null&&play.Mode==GameMode.Creative&&terrainExplorer.IsWalking==false,"random-land entry uses Creative flight and no NPC identity");
        Check(LocalTreeTextures.IsPrepared,"shared tree bank and all leaf hulls are prepared before gameplay");
        var warm=LocalTreeTextures.Prepare();Check(!warm.MoveNext(),"repeated tree preparation completes without waiting");
        yield return new WaitForEndOfFrame();
        terrainExplorer.Capture(Path.Combine(output,"creative-land.png"));
        if(Environment.GetCommandLineArgs().Contains("--worldgen-entry-creative-only"))
        {File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Application.Quit(0);yield break;}
        ReturnFromPlay("Simulation");while(terrainUnloading!=null&&!terrainUnloading.isDone)yield return null;
        ChooseWorldEntry();StartWorldEntry(true);Check(worldEntryWork!=null,"resident selection starts its background task");deadline=Time.realtimeSinceStartupAsDouble+240;
        while((worldEntryOpen||terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline){if(worldEntryError.Length>0)Check(false,"entry task failed: "+worldEntryError);yield return null;}
        Check(terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.CenterReady,"resident entry loads actual ground");
        Check(play.Identity!=null&&play.Mode==GameMode.Survival&&terrainExplorer.IsWalking,"resident starts in Survival");
        long identity=play.Identity.Person.Id,group=play.Identity.Group;
        yield return terrainExplorer.CheckControlledResident(output,report);
        CapturePlayer();var copy=play.Copy();copy.Validate();
        Check(copy.Identity.Person.Id==identity&&copy.Identity.Group==group,"capturing the playable session preserves person and community");
        ReturnFromPlay("Simulation");while(terrainUnloading!=null&&!terrainUnloading.isDone)yield return null;
        play=copy;RequestTerrain();deadline=Time.realtimeSinceStartupAsDouble+180;
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer!=null&&terrainExplorer.Ready&&play.Identity.Person.Id==identity,"re-entering resumes the same life");
        yield return terrainExplorer.CheckControlledResident(output,report,false);
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Application.Quit(0);
    }
}
public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckControlledResident(string output,List<string> report,bool capture=true)
    {
        void Check(bool ok,string text){if(!ok){Debug.LogError("RESIDENT CHECK "+text);Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("ENTRY PASS "+text);}
        var identity=PlayerRules.Identity;
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((humanPlayer==null||!livingGroups.ContainsKey(identity.Group)||humanNpcs.All(p=>p.GroupId!=identity.Group))&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(humanPlayer!=null&&humanPlayer.ResidentId==identity.Person.Id&&humanPlayer.GroupId==identity.Group,"controlled native human retains resident and tribe IDs");
        Check(humanPlayer.Definition.Variant[0]-'A'==PlayerWorldEntry.Variant(identity.Person),"player uses the same generated model as that NPC");
        Check(!livingPeople.ContainsKey(identity.Person.Id)&&humanNpcs.All(p=>p.ResidentId!=identity.Person.Id),"controlled resident is not duplicated as an NPC");
        Check(livingGroups.TryGetValue(identity.Group,out var group)&&group.Residents!=null,"home community is projected around the player");
        var person=group.Residents.Members.First(p=>p.Id==identity.Person.Id);
        Check(person.Mother==identity.Person.Mother&&person.Father==identity.Person.Father&&person.Partner==identity.Person.Partner,"same family links as the procedural community");
        Check(humanNpcs.Any(p=>p.GroupId==identity.Group),"other actual members of the home tribe are present");
        wildlifeRadarOpen=true;wildlifeRadarRefreshAt=0;RefreshWildlifeRadar();
        Check(radarHumanCommunities>0&&radarHumanActors>0&&wildlifeRadarDots.Any(p=>p.HumanKind==3),"radar contains coarse communities and physical people");
        Check(group.Situation.Phase==HumanCampPhase.Travelling||wildlifeRadarDots.Any(p=>p.HumanKind==2),"occupied/projected camp sites appear on radar");
        UpdateSurvivalHud();var radarBounds=WildlifeRadarLayout(out _,out _,out _);
        if(survivalHudCanvas!=null&&survivalHudCanvas.enabled)
        {survivalHud.rectTransform.GetWorldCorners(radarHudCorners);Check(radarBounds.yMax<=Screen.height-radarHudCorners[1].y-10,"radar stays above the actual survival HUD bounds");}
        if(capture)
        {
            humanCamera.Restore(4,false);humanCameraDistance=4;UpdateHumans();UpdateCamera();
            yield return new WaitForSecondsRealtime(2);yield return new WaitForEndOfFrame();
            Capture(Path.Combine(output,"resident-third-person.png"));
            var screen=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"resident-radar.png"),screen.EncodeToPNG());Destroy(screen);
            File.WriteAllText(Path.Combine(output,"identity.json"),JsonUtility.ToJson(identity,true));
        }
    }
}

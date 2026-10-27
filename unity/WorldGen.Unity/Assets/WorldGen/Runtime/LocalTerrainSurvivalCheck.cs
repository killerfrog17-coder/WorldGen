using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CaptureSurvivalUi(string path)
    {
        if(Application.isBatchMode){PerformanceCapture(path);yield break;}
        var prior=weatherCheckTarget;weatherCheckTarget=null;view.targetTexture=null;view.enabled=true;
        yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
        view.enabled=false;weatherCheckTarget=prior;view.targetTexture=prior;
    }
    public IEnumerator PerformanceSurvivalReview(string output)
    {
        var report=new List<string>();void Check(bool yes,string message){if(!yes)throw new InvalidOperationException("SURVIVAL REVIEW: "+message);report.Add("PASS "+message);}
        Check(survival!=null&&humanPlayer!=null,"survival and human initialize");survival.Validate();
        Check(humanPlayer.Definition.Roles.Contains("Dig")&&humanPlayer.Definition.Roles.Contains("Sword attack")&&humanPlayer.Definition.Roles.Contains("Sitting"),"authored action clips imported");
        survival.Mode=SurvivalMode.Creative;motion.Flying=false;motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));
        TerrainHandoff.Coordinates(frame,Position,out double x,out double z);
        var hearth=SurvivalCrafting.Shelters.First(b=>b.Id=="hearth");Check(SurvivalCrafting.Build(survival,hearth,frame.Position(x+2,z+1),0,out _),"build hearth");
        Check(SurvivalCrafting.Build(survival,SurvivalCrafting.Shelters.First(b=>b.Id=="lean-to"),frame.Position(x-3,z+3),30,out _),"build shelter");
        RefreshSurvivalScene();Check(survivalSiteObjects.Count>=2,"camp meshes present");
        survival.Mode=SurvivalMode.Survival;Check(survival.Inventory.TryAdd("raw-meat",1,out _),"meat fits real carrying equipment");
        Check(SurvivalCrafting.Craft(survival,SurvivalCrafting.Recipes.First(r=>r.Id=="cooked-meat"),HasSurvivalFire,out _),"cook at placed hearth");
        OpenSurvival();yield return null;yield return CaptureSurvivalUi(Path.Combine(output,"survival-inventory-list.png"));
        survival.Inventory.View=InventoryView.Grid;yield return null;yield return CaptureSurvivalUi(Path.Combine(output,"survival-inventory-grid.png"));
        survivalTab=1;yield return null;yield return CaptureSurvivalUi(Path.Combine(output,"survival-crafting.png"));
        survivalTab=2;yield return null;yield return CaptureSurvivalUi(Path.Combine(output,"survival-shelters.png"));survivalOpen=false;
        Check(survivalNodes.Count>0,"stable habitat-dependent gatherable resources present");
        var node=survivalNodes.Values.First();int count=survival.Inventory.Count(node.Item);GatherSurvivalNode(node);survivalActionRemaining=0;
        yield return null;Check(survival.Inventory.Count(node.Item)==count+1,"timed gathering completes");
        ExecuteExplorerCommand("/spawn deer 1");double deadline=Time.realtimeSinceStartupAsDouble+45;Animal deer=null;
        while(Time.realtimeSinceStartupAsDouble<deadline){deer=animals.Values.FirstOrDefault(a=>a.Manual&&a.Spawn.Profile.slug=="deer");if(deer!=null)break;yield return null;}
        Check(deer!=null,"deer streams for hunting");
        var before=animalBoneData.Skip(deer.Slot*AnimalBones).Take(deer.Model.Asset.BoneCount).ToArray();DamageSurvivalAnimal(deer,10000,Vector3.right*.2f);
        Check(deer.Dead&&animalRagdolls.ContainsKey(deer.Spawn.Id),"lethal hit starts articulated original-skeleton physics");
        var rag=animalRagdolls[deer.Spawn.Id];Check(rag.Bodies.Count(b=>b!=null)>5,"deer has articulated body and limb rigidbodies");
        UpdateDeadAnimal(deer);float maxError=0;for(int i=0;i<before.Length;i++)for(int k=0;k<16;k++)maxError=Math.Max(maxError,Math.Abs(before[i][k]-animalBoneData[deer.Slot*AnimalBones+i][k]));
        File.WriteAllText(Path.Combine(output,"ragdoll-initial-pose-error.txt"),maxError.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        Check(maxError<.025,"ragdoll begins close to the currently displayed native pose");
        var corpse=survival.Corpses.Last();double meat=corpse.MeatKg;Check(SurvivalPhysiology.Harvest(survival,corpse,"meat",out _),"carcass yields meat");Check(corpse.MeatKg<meat,"finite yield decreases");
        double end=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<end)yield return null;
        Check(rag.Bones.All(b=>float.IsFinite(b.position.x)&&float.IsFinite(b.position.y)&&float.IsFinite(b.position.z)),"ragdoll remains finite after settling");
        var p=deer.Renderer.bounds.center;view.transform.position=p+new Vector3(2.5f,1.5f,3);view.transform.LookAt(p);
        Capture(Path.Combine(output,"survival-deer-ragdoll.png"));
        var session=new PlaySession();StorePlayer(session);var saved=WorldJson.Read<PlaySession>(WorldJson.Write(session));saved.Validate();
        Check(saved.Survival.Corpses.Length==survival.Corpses.Length&&saved.Survival.Sites.Length==survival.Sites.Length,"camps and harvested carcasses save and reload");
        Check(saved.Survival.Corpses.Last().SettledPose?.Length==deer.Model.Asset.BoneCount*16,"current corpse pose is serialized");
        var spawn=deer.Spawn;var model=deer.Model;var pose=saved.Survival.Corpses.Last().SettledPose;RemoveAnimal(spawn.Id);
        survival=saved.Survival;AppendSavedCarcasses();Check(animalWanted.Any(a=>a.Id==spawn.Id),"saved manual carcass enters the independent local corpse stream");
        Check(AddAnimal(spawn,model),"saved carcass streams back into the renderer");deer=animals[spawn.Id];
        float restoredError=0;for(int i=0;i<model.Asset.BoneCount;i++)for(int k=0;k<16;k++)restoredError=Math.Max(restoredError,Math.Abs(pose[i*16+k]-animalBoneData[deer.Slot*AnimalBones+i][k]));
        Check(deer.Dead&&restoredError<.025,"saved carcass restores its articulated skin pose without resurrecting");
        foreach(var body in animalRagdolls[spawn.Id].Bodies)if(body!=null)body.Sleep();
        var rootBefore=deer.Root.transform.localPosition;var boneBefore=animalRagdolls[spawn.Id].Bones.First().position;double priorOrigin=originX;originX+=2048;UpdateDeadAnimal(deer);
        Check(Math.Abs(deer.Root.transform.localPosition.x-rootBefore.x+2048)<.01&&Math.Abs(animalRagdolls[spawn.Id].Bones.First().position.x-boneBefore.x+2048)<.03,"corpse bodies rebase with the floating terrain origin");originX=priorOrigin;UpdateDeadAnimal(deer);
        survivalOpen=false;File.WriteAllLines(Path.Combine(output,"survival-review.txt"),report);Debug.Log("SURVIVAL REVIEW PASS "+report.Count);
    }
}

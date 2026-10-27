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
        OpenSurvival();yield return null;PerformanceCapture(Path.Combine(output,"survival-inventory-list.png"));
        survival.Inventory.View=InventoryView.Grid;yield return null;PerformanceCapture(Path.Combine(output,"survival-inventory-grid.png"));
        survivalTab=1;yield return null;PerformanceCapture(Path.Combine(output,"survival-crafting.png"));
        survivalTab=2;yield return null;PerformanceCapture(Path.Combine(output,"survival-shelters.png"));survivalOpen=false;
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
        var p=deer.Root.transform.localPosition;view.transform.localPosition=p+new Vector3(3,2,4);view.transform.LookAt(transform.TransformPoint(p+Vector3.up*.4f));
        yield return null;PerformanceCapture(Path.Combine(output,"survival-deer-ragdoll.png"));
        var session=new PlaySession{Survival=survival.Copy()};var saved=WorldJson.Read<PlaySession>(WorldJson.Write(session));saved.Validate();
        Check(saved.Survival.Corpses.Length==survival.Corpses.Length&&saved.Survival.Sites.Length==survival.Sites.Length,"camps and harvested carcasses save and reload");
        survivalOpen=false;File.WriteAllLines(Path.Combine(output,"survival-review.txt"),report);Debug.Log("SURVIVAL REVIEW PASS "+report.Count);
    }
}

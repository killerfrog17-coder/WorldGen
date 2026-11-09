using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    struct NpcCoverProbe{public Vector4 Position,Color,Shape,Normal,Grasses,Herbs;}
    sealed class NpcScatterSlot{public ulong Id;public Matrix4x4 Matrix;public Vector4 Color,Fade;public Matrix4x4[] Parts;public float Wind;}
    public sealed class NpcBatchLookupProof{public int Keys,Iterations;public long DefaultBytes,ExplicitBytes;public double DefaultMs,ExplicitMs;public int Checksum;}
    static void CheckNpcBatchLookups(LocalTerrainExplorer owner,string output,List<string> checks)
    {
        var keys=owner.activeScatterBatches.Keys.ToArray();if(keys.Length==0)throw new InvalidOperationException("No actual scatter batch keys to test.");
        var baseline=new Dictionary<(long,int),int>();var candidate=new Dictionary<(long,int),int>(new ScatterBatchComparer());
        for(int i=0;i<keys.Length;i++){baseline.Add(keys[i],i);candidate.Add(keys[i],i);}
        int Run(Dictionary<(long,int),int> map,out long allocated,out double milliseconds)
        {
            for(int i=0;i<3000;i++)map.TryGetValue(keys[i%keys.Length],out _);
            long before=GC.GetAllocatedBytesForCurrentThread(),started=System.Diagnostics.Stopwatch.GetTimestamp();int sum=0;
            for(int i=0;i<500000;i++){if(!map.TryGetValue(keys[i%keys.Length],out int value))throw new InvalidOperationException("Lost actual batch key");sum+=value;}
            allocated=GC.GetAllocatedBytesForCurrentThread()-before;milliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;return sum;
        }
        var proof=new NpcBatchLookupProof{Keys=keys.Length,Iterations=500000};int first=Run(baseline,out proof.DefaultBytes,out proof.DefaultMs),second=Run(candidate,out proof.ExplicitBytes,out proof.ExplicitMs);proof.Checksum=first;
        if(first!=second)throw new InvalidOperationException("Explicit scatter comparer changed actual key lookup results.");
        File.WriteAllText(Path.Combine(output,"npc-batch-lookup-proof.json"),WorldJson.Write(proof));checks.Add("PASS Explicit and default batch key comparers return the same actual keys; allocation/timing recorded as Mono component evidence only");
    }
    static void CheckNpcNaturalRemoval(LocalTerrainExplorer owner,LivingGroup group,string output,List<string> checks)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NPC SCATTER REMOVAL "+text);checks.Add("PASS "+text);}
        owner.ApplyNaturalScatterRemovals();var batches=owner.drawBatches.ToArray();
        ScatterInstance source=default;Possession item=null;
        foreach(var batch in batches)
        {
            var kind=(ScatterKind)((batch.Key>>2)&63);if(kind!=ScatterKind.Stone&&kind!=ScatterKind.Branch)continue;
            for(int i=0;i<batch.Count&&item==null;i++)foreach(var chunk in owner.scatterChunks.Values)
            {
                foreach(var plant in chunk.Source.Instances)if(plant.Id==batch.SourceIds[i]&&!owner.CollectedRock(plant))
                {var candidate=RockPossession(plant);if(candidate.MassKg<=3){source=plant;item=candidate;}break;}
                if(item!=null)break;
            }
            if(item!=null)break;
        }
        Check(item!=null,"A real admitted stone or branch is available for the incremental removal fixture");
        var before=batches.ToDictionary(b=>b,b=>Enumerable.Range(0,b.Count).Select(i=>new NpcScatterSlot{Id=b.SourceIds[i],Matrix=b.Matrices[i],Color=b.Colors[i],Fade=b.Fades[i],Wind=b.SharedWind?b.WindSlots[i]:0,Parts=b.Model.Parts.Select((p,j)=>p.Local==Matrix4x4.identity?b.Matrices[i]:b.PartMatrices[j][i]).ToArray()}).ToArray());
        int expected=before.Values.Sum(a=>a.Count(s=>s.Id!=source.Id));var actor=owner.livingPeople.Values.First(p=>p.GroupId==group.Situation.Band.Id&&p.Resident.AgeYears>=18);
        bool wasDirty=owner.scatterDirty;owner.scatterDirty=false;owner.NpcCarryProduced(actor,item);
        Check(!owner.scatterDirty&&owner.inventoryCollected.Contains(source.Id)&&actor.Belongings.Find(item.Id)==item,"Actual NPC collection records depletion and transfers the real item without invalidating the whole vegetation scene");
        owner.ApplyNaturalScatterRemovals();Check(batches.SequenceEqual(owner.drawBatches)&&owner.drawBatches.Sum(b=>b.Count)==expected&&owner.drawBatches.All(b=>!Enumerable.Range(0,b.Count).Any(i=>b.SourceIds[i]==source.Id)),"Every admitted LOD/shadow copy is removed from the existing batches without rebuilding them");
        bool exact=true;
        foreach(var batch in batches)
        {
            var old=before[batch].ToDictionary(s=>s.Id);
            for(int i=0;i<batch.Count;i++)
            {
                if(!old.TryGetValue(batch.SourceIds[i],out var prior)||prior.Id==source.Id||!prior.Matrix.Equals(batch.Matrices[i])||!prior.Color.Equals(batch.Colors[i])||!prior.Fade.Equals(batch.Fades[i])||batch.SharedWind&&prior.Wind!=batch.WindSlots[i]){exact=false;break;}
                for(int j=0;j<batch.Model.Parts.Length;j++)if(!prior.Parts[j].Equals(batch.Model.Parts[j].Local==Matrix4x4.identity?batch.Matrices[i]:batch.PartMatrices[j][i]))exact=false;
            }
        }
        Check(exact,"All surviving root/child transforms, tints, fades and wind slots remain bit-exact after compaction");
        owner.scatterDirty=wasDirty;owner.BuildScatterBatches();
        Check(owner.drawBatches.All(b=>!Enumerable.Range(0,b.Count).Any(i=>b.SourceIds[i]==source.Id)),"A subsequent ordinary full rebuild does not resurrect the collected natural item");
        actor.Belongings.Validate();owner.Inventory.Validate();File.WriteAllText(Path.Combine(output,"incremental-removal-proof.txt"),"Source="+source.Id+"; remaining admissions="+expected+"; existing batches="+batches.Length+"; unaffected root/child attributes EXACT.");
    }
    static void CheckLivingCampGround(LocalTerrainExplorer owner,LivingGroup group,string output,List<string> checks)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NPC CAMP GROUND "+text);checks.Add("PASS "+text);Debug.Log("NPC CAMP GROUND "+text);}
        TerrainHandoff.Coordinates(owner.frame,group.Anchor,out double x,out double z);
        Check(group.SiteSurvey!=null&&owner.LivingSiteClear(group,x,z),"Actual occupied shelter/hearth footprints do not intersect canonical tree trunks or boulders");
        var survey=group.SiteSurvey;
        bool Literal(double cx,double cz)
        {
            for(int i=0;i<survey.Solids.Length;i++)
            {
                if(!SolidTree(survey.Solids[i].Kind)&&owner.CollectedRock(survey.Solids[i]))continue;
                double sx=survey.SolidX[i],sz=survey.SolidZ[i],r=survey.SolidRadius[i];
                if((sx-cx)*(sx-cx)+(sz-cz)*(sz-cz)>Math.Pow(survey.FootprintRadius+r,2))continue;
                foreach(var shape in survey.Shapes)
                {
                    double dx=sx-(cx+shape.X),dz=sz-(cz+shape.Z),lx=dx*shape.Cos-dz*shape.Sin,lz=dx*shape.Sin+dz*shape.Cos;
                    if(Math.Abs(lx)<shape.Width*.5+r+.35&&Math.Abs(lz)<shape.Depth*.5+r+.35)return false;
                }
            }
            return true;
        }
        int clear=0,blocked=0;
        for(int i=0;i<513;i++)
        {
            double bearing=i*2.399963229728653,distance=i==0?0:Math.Sqrt(i/512d)*(survey.Radius-survey.FootprintRadius-10);
            double sampleX=x+Math.Sin(bearing)*distance,sampleZ=z+Math.Cos(bearing)*distance;bool literal=Literal(sampleX,sampleZ),indexed=owner.LivingSiteClear(group,sampleX,sampleZ);
            if(literal!=indexed)throw new InvalidOperationException("Camp spatial index disagrees with full literal obstacle scan at "+i);
            if(indexed)clear++;else blocked++;
        }
        Check(clear>0,"513 real camp candidates have identical indexed and full-scan clearance; sparse open sites need not invent blocked candidates");
        File.WriteAllText(Path.Combine(output,"camp-site-index-proof.txt"),"513 literal/index pairs exact; clear="+clear+"; blocked="+blocked+"; solids="+survey.Solids.Length);
        owner.campMaskCheckAt=0;owner.UpdateCampClearing();
        var house=owner.Camp.Objects.FirstOrDefault(c=>c.NpcGroup==group.Situation.Band.Id&&c.Blueprint!=null);
        if(house==null)return;
        TerrainHandoff.Coordinates(owner.frame,house.Position,out double hx,out double hz);
        int px=(int)Math.Floor(hx-owner.campMaskX),pz=(int)Math.Floor(hz-owner.campMaskZ);
        Check(owner.campClearingPixels[pz*CampMaskSide+px]==255,"Occupied shelter floor has a full-strength ground clearing mask");
        var probes=new[]{new NpcCoverProbe{Position=new Vector4((float)(hx-owner.originX),0,(float)(hz-owner.originZ),1),Color=Vector4.one},new NpcCoverProbe{Position=new Vector4((float)(owner.campMaskX+2-owner.originX),0,(float)(owner.campMaskZ+2-owner.originZ),1),Color=Vector4.one}};
        var shader=Instantiate(owner.groundCoverPlacement);int kernel=shader.FindKernel("Cull");
        using(var roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,2,96))
        using(var count=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4))
        using(var read=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4))
        {
            var visible=new GraphicsBuffer[6];
            try
            {
                roots.SetData(probes);count.SetData(new uint[]{2});shader.SetBuffer(kernel,"_PlacedRoots",roots);shader.SetBuffer(kernel,"_RootCount",count);
                string[] names={"_NearRoots","_MiddleRoots","_FarRoots","_DistantRoots","_HorizonRoots","_FootRoots"};
                for(int i=0;i<6;i++){visible[i]=new GraphicsBuffer(GraphicsBuffer.Target.Append,2,4);shader.SetBuffer(kernel,names[i],visible[i]);}
                shader.SetVector("_CullOrigin",Vector4.zero);shader.SetVector("_CullCamera",Vector4.zero);shader.SetVectorArray("_CullPlanes",Enumerable.Repeat(new Vector4(0,0,0,1000000),6).ToArray());
                shader.SetFloat("_GrassRange",1000000);shader.SetFloat("_GrassNear",1000000);shader.SetFloat("_GrassNearDensity",1);shader.SetInt("_SpritesOnly",0);shader.SetInt("_CanopyEnabled",0);shader.SetInt("_CoverageEnabled",0);shader.SetFloat("_CoverageSize",48);
                shader.SetTexture(kernel,"_TerrainCoverage",Texture2D.blackTexture);shader.SetTexture(kernel,"_CampClearing",owner.campClearingTexture);shader.SetVector("_CampClearingRect",new Vector4((float)(owner.campMaskX-owner.originX),(float)(owner.campMaskZ-owner.originZ),CampMaskSide,0));
                uint DrawCount(bool mask)
                {
                    foreach(var buffer in visible)buffer.SetCounterValue(0);shader.SetInt("_CampClearingEnabled",mask?1:0);shader.Dispatch(kernel,1,1,1);
                    GraphicsBuffer.CopyCount(visible[0],read,0);var result=new uint[1];read.GetData(result);return result[0];
                }
                Check(DrawCount(false)==2&&DrawCount(true)==1,"Packed compute shader removes grass inside the real camp floor and retains grass outside");
            }
            finally{foreach(var buffer in visible)buffer?.Dispose();Destroy(shader);}
        }
        File.WriteAllText(Path.Combine(output,"camp-site-survey.txt"),group.SiteSurvey.Solids.Length+" natural solids; "+group.SiteSurvey.Milliseconds.ToString("F2")+"ms background; "+group.PlacementAttempt+" bounded candidate tests");
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-removal-proof")){CheckNpcNaturalRemoval(owner,group,output,checks);CheckNpcBatchLookups(owner,output,checks);}
    }
}

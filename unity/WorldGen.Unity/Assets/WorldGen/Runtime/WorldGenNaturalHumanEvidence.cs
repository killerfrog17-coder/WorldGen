using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void WriteNaturalPoses(string path,IEnumerable<NaturalPoseEvidence> records)
    {
        // Preserve every float/triangle while avoiding hundreds of MB of repeated
        // JSON keys in each review. This is diagnostic output only.
        using(var file=new FileStream(path+".gz",FileMode.CreateNew,FileAccess.Write))
        using(var compressed=new GZipStream(file,System.IO.Compression.CompressionLevel.Fastest))
        using(var writer=new StreamWriter(compressed))
        {
            writer.Write('[');bool first=true;
            foreach(var row in records){if(!first)writer.Write(',');writer.Write(JsonUtility.ToJson(row));first=false;}
            writer.Write(']');
        }
    }
    Possession CreateNaturalReviewHeld(PossessionKind kind,PossessionPlace hand=PossessionPlace.RightHand)
    {
        var item=kind==PossessionKind.SmallRock||kind==PossessionKind.LargeRock?new Possession{Id="natural-"+Guid.NewGuid().ToString("N"),Kind=kind,Place=PossessionPlace.Ground,Position=Position,
            WidthM=kind==PossessionKind.LargeRock?.42:.07,HeightM=kind==PossessionKind.LargeRock?.32:.06,DepthM=kind==PossessionKind.LargeRock?.35:.06,
            MassKg=kind==PossessionKind.LargeRock?12:.15,CellsWide=kind==PossessionKind.LargeRock?2:1,CellsHigh=kind==PossessionKind.LargeRock?2:1,Tint=new V(1,1,1),Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)}:PortableTechnology.Create(kind,Position);
        Inventory.Items.Add(item);if(!Inventory.Move(item.Id,hand,"",0,0,false,out string reason))throw new InvalidOperationException("Natural fixture: "+reason);return item;
    }
    void ExportNaturalRigid(GameObject obj,Transform body,out Vector3[] vertices,out int[] triangles)
    {
        var v=new List<Vector3>();var t=new List<int>();
        foreach(var filter in obj.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;int offset=v.Count;
            v.AddRange(mesh.vertices.Select(p=>body.InverseTransformPoint(filter.transform.TransformPoint(p))));t.AddRange(mesh.triangles.Select(i=>offset+i));
        }
        vertices=v.ToArray();triangles=t.ToArray();
    }
    void CaptureNaturalImage(string output,string name,int angle)
    {
        var p=view.transform.position;var r=view.transform.rotation;int mask=humanReviewBodyMask;
        if(angle>=0)
        {
            humanReviewBodyMask=0;var body=humanPlayer.Root.transform;
            Vector3 offset=angle==0?new Vector3(1.9f,1.5f,3.3f):new Vector3(3.5f,1.5f,.2f);
            view.transform.position=body.TransformPoint(offset);view.transform.LookAt(body.position+Vector3.up*.85f*NaturalScale);
            if(angle==2)
            {
                Vector3 eye=body.TransformPoint(humanPlayer.Shared.LocalEye),forward=NaturalLookAxis,right=Vector3.Cross(Vector3.up,forward).normalized;
                view.transform.position=eye+forward*.75f+right*.35f;view.transform.LookAt(eye-Vector3.up*.04f*NaturalScale);
            }
                if(angle==3)
                {
                    Vector3 eye=body.TransformPoint(humanPlayer.Shared.LocalEye);
                    view.transform.position=eye+body.forward*2.8f+body.right*1.8f+Vector3.up*.7f;view.transform.LookAt(eye-Vector3.up*.3f);
                }
        }
        var previous=view.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(768,768,24);target.Create();view.targetTexture=target;
        view.Render();RenderTexture.active=target;var image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());RenderTexture.active=active;view.targetTexture=previous;target.Release();Destroy(target);Destroy(image);
        view.transform.SetPositionAndRotation(p,r);humanReviewBodyMask=mask;
    }
    NaturalPoseEvidence ExportNaturalPose(Possession item,string role,float time,bool surface)
    {
        var body=humanPlayer.Root.transform;var names=inventoryBones.Keys.Where(k=>k=="pelvis"||k=="head"||k.StartsWith("spine_",StringComparison.Ordinal)||k.StartsWith("upperarm_",StringComparison.Ordinal)||k.StartsWith("lowerarm_",StringComparison.Ordinal)||k.StartsWith("hand_",StringComparison.Ordinal)||k.StartsWith("thigh_",StringComparison.Ordinal)||k.StartsWith("calf_",StringComparison.Ordinal)||k.StartsWith("foot_",StringComparison.Ordinal)).OrderBy(k=>k).ToArray();
        var result=new NaturalPoseEvidence{Variant=humanPlayer.Definition.Variant,Role=role,Time=time,Pitch=pitch,BoneNames=names,
            Bones=names.Select(k=>body.InverseTransformPoint(inventoryBones[k].position)).ToArray(),Rotations=names.Select(k=>Quaternion.Inverse(body.rotation)*inventoryBones[k].rotation).ToArray(),
            LeftGrip=body.InverseTransformPoint(SharedGripPosition("l")),RightGrip=body.InverseTransformPoint(SharedGripPosition("r")),
            StringGrip=body.InverseTransformPoint(NaturalBowStringGrip),Eye=humanPlayer.Shared.LocalEye,Item=item?.Kind.ToString()??"none"};
        if(item!=null&&inventoryVisuals.TryGetValue(item.Id,out var prop))
        {
            Vector3 start=prop.transform.position,end=start+prop.transform.up*(float)item.HeightM;
            result.PropStart=body.InverseTransformPoint(start);result.PropEnd=body.InverseTransformPoint(end);
            if(item.Kind==PossessionKind.Bow)
            {
                result.PropPoints=Enumerable.Range(0,17).Select(i=>{float t=i/16f;return body.InverseTransformPoint(prop.transform.TransformPoint(new Vector3(.38f*Mathf.Pow(t*2-1,2),t,0)));}).ToArray();
            }
            else result.PropPoints=new[]{result.PropStart,result.PropEnd};
            result.Clearance=NaturalBodyClearance(start,end,Mathf.Max(.009f*NaturalScale,(float)item.WidthM*.1f));
            string side=NaturalSide(item);float fraction=StoneCrafting.Rock(item)||item.Kind==PossessionKind.Bow?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f;
            result.GripError=Vector3.Distance(SharedGripPosition(side),Vector3.Lerp(start,end,fraction));
            if(SupportSpear(item))
            {
                Vector3 shaft=(end-start).normalized,delta=SharedGripPosition(side=="r"?"l":"r")-start;
                result.SupportError=Vector3.ProjectOnPlane(delta,shaft).magnitude;
            }
            if(sharedLoadedAmmo!=null)
            {
                var ammo=itemCharge?.Ammo??pendingRanged?.Charge.Ammo;
                if(ammo!=null)result.AmmoPoints=new[]{body.InverseTransformPoint(sharedLoadedAmmo.transform.position),body.InverseTransformPoint(sharedLoadedAmmo.transform.position+sharedLoadedAmmo.transform.up*(float)ammo.HeightM)};
            }
        }
        if(surface)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var mesh=new Mesh();
            foreach(var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var source=renderer.sharedMesh;if(source==null||source.vertexCount<10000)continue;
                renderer.BakeMesh(mesh,true);var weights=source.boneWeights;var bind=renderer.bones;var v=mesh.vertices;int offset=vertices.Count;
                vertices.AddRange(v.Select(p=>body.InverseTransformPoint(renderer.transform.TransformPoint(p))));
                bool Core(int index)
                {
                    var w=weights[index];float sum=0;
                    void Add(int b,float a){string name=bind[b].name;if(name=="pelvis"||name=="head"||name.StartsWith("spine_",StringComparison.Ordinal)||name.StartsWith("thigh_",StringComparison.Ordinal))sum+=a;}
                    Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);return sum>.65f;
                }
                var t=mesh.triangles;for(int i=0;i<t.Length;i+=3)if(Core(t[i])&&Core(t[i+1])&&Core(t[i+2])){triangles.Add(offset+t[i]);triangles.Add(offset+t[i+1]);triangles.Add(offset+t[i+2]);}
            }
            Destroy(mesh);var used=triangles.Distinct().OrderBy(i=>i).ToArray();var remap=new Dictionary<int,int>();for(int i=0;i<used.Length;i++)remap[used[i]]=i;
            result.Vertices=used.Select(i=>vertices[i]).ToArray();result.CoreTriangles=triangles.Select(i=>remap[i]).ToArray();
                if(result.Vertices.Any(p=>Mathf.Abs(p.x)>5||Mathf.Abs(p.y)>5||Mathf.Abs(p.z)>5))throw new InvalidOperationException("Native skin evidence is not in metre coordinates");
                if(item!=null&&inventoryVisuals.TryGetValue(item.Id,out var held))ExportNaturalRigid(held,body,out result.PropVertices,out result.PropTriangles);
                if(sharedLoadedAmmo!=null)ExportNaturalRigid(sharedLoadedAmmo,body,out result.AmmoVertices,out result.AmmoTriangles);
        }
        return result;
    }
}

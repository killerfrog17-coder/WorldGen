using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    AudioClip[] woodCraftClips;double craftSoundAt;
    AudioClip[] stoneCraftClips;AudioSource stoneCraftVoice;int stoneCraftSoundIndex,stoneCraftSoundCount;
    readonly Dictionary<GameObject,List<Mesh>> inventoryCraftMeshes=new Dictionary<GameObject,List<Mesh>>();
    ItemCraftJob ActiveItemCraft=>inventoryInitialized?Inventory.Crafts.FirstOrDefault(j=>j.Running):null;
    bool CraftWorking=>ActiveItemCraft!=null||worldToolWork!=null;
    ItemCraftJob CraftFor(string id)=>Inventory.Crafts.OrderByDescending(j=>j.Running).ThenByDescending(j=>j.WorkpieceId==id).FirstOrDefault(j=>PortableCrafting.Affects(j,id));
    bool CraftInputs(string id,out NearbyPossession core,out NearbyPossession tool,out string reason,ItemCraftAction action=ItemCraftAction.Flake)
    {
        RefreshInventoryNearby();core=InventoryCandidate(id);tool=null;
        reason=action==ItemCraftAction.Flake?StoneCrafting.WorkpieceReason(core?.Item):WoodCrafting.Reason(core?.Item,action);if(reason.Length>0)return false;
        if(!core.Natural&&!AccessiblePossession(core.Item)){reason="That stone is out of reach.";return false;}
        if(core.Item.Hands==2&&HeightAboveGround>3){reason="Land before working a two-handed stone on the ground.";return false;}
        var candidates=Inventory.Items.Where(AccessiblePossession).Select(p=>new NearbyPossession{Item=p}).Concat(inventoryNearby.Where(n=>n.Natural));
        tool=candidates.Where(n=>!Inventory.Crafts.Any(j=>PortableCrafting.IsJob(j)&&(j.IngredientIds?.Contains(n.Item.Id)??false))&&n.Item.Id!=id&&(action==ItemCraftAction.Flake?StoneCrafting.StrikingStone(n.Item):WoodCrafting.Cutter(n.Item)))
            .OrderByDescending(n=>n.Item.Place==PossessionPlace.RightHand||n.Item.Place==PossessionPlace.LeftHand).FirstOrDefault();
        if(tool==null){reason=action==ItemCraftAction.Flake?"Requires another suitably sized, compact stone for striking.":"Requires a usable cutting flake (at least 20% edge remaining).";return false;}
        string toolId=tool.Item.Id;
        var unfinished=Inventory.Crafts.FirstOrDefault(j=>j.WorkpieceId==id);
        if(unfinished!=null&&unfinished.Action!=action){reason="Finish the existing work on this piece first.";return false;}
        reason="";return true;
    }
    bool StartInventoryKnapping(string id,ItemCraftAction action=ItemCraftAction.Flake)
    {
        PauseItemCraft(ActiveItemCraft);RefreshInventoryNearby(true);
        if(!CraftInputs(id,out var core,out var tool,out var reason,action)){InventoryFeedback(reason);return false;}
        // Validate choices before claiming natural instances. Roll back a failed
        // preparation atomically, before touching scatter ownership or visuals.
        var backup=Inventory.Copy();
        if(core.Natural)Inventory.Items.Add(core.Item);
        if(tool.Natural)Inventory.Items.Add(tool.Item);
        var equipment=PrepareWorkHands(new[]{core.Item.Id,tool.Item.Id});
        foreach(var p in new[]{core.Item,tool.Item}){p.Place=PossessionPlace.Ground;p.ContainerId="";}
        bool prepared=Inventory.Move(tool.Item.Id,PossessionPlace.RightHand,"",0,0,false,out reason)&&
            Inventory.Move(core.Item.Id,core.Item.Hands==1?PossessionPlace.LeftHand:PossessionPlace.Ground,"",0,0,false,out reason);
        if(!prepared){PlayerRules.Inventory=backup;InventoryFeedback(reason);return false;}
        foreach(var n in new[]{core,tool})
        {
            RemoveDroppedBody(n.Item.Id);n.Item.GroundMoving=false;
            if(n.Natural){Inventory.CollectedNatural.Add(n.Item.NaturalId);inventoryCollected.Add(n.Item.NaturalId);scatterDirty=true;}
        }
        if(core.Item.Place==PossessionPlace.Ground)
        {
            var point=view.transform.localPosition+new Vector3(view.transform.forward.x,0,view.transform.forward.z).normalized*.65f;
            core.Item.Position=frame.Position(point.x+originX,point.z+originZ);core.Item.GroundOffsetM=0;core.Item.GroundRotation=new V();
        }
        var job=action==ItemCraftAction.Flake?StoneCrafting.Start(Inventory,id,tool.Item.Id,out reason):WoodCrafting.Start(Inventory,id,tool.Item.Id,action,out reason);
        if(job==null)throw new InvalidOperationException("Validated stone craft failed: "+reason);
        job.Equipment=equipment;inventorySelected=id;inventoryNotice="";inventoryNoticeUntil=0;inventoryNearbyAt=0;
        PlayCraftSound(action);if(inventoryOpen)RenderInventoryPreview();return true;
    }
    void PauseItemCraft(ItemCraftJob job)
    {if(job!=null){job.Running=false;RestoreWorkHands(job);}if(stoneCraftVoice!=null)stoneCraftVoice.Stop();}
    void UpdateStoneCrafting()
    {
        var job=ActiveItemCraft;if(job==null)return;
        bool suspended=paused||mapOpen||ecologyOpen||chatOpen||!Ready||!Application.isFocused&&!inventoryReviewMode;
        if(suspended){if(stoneCraftVoice!=null)stoneCraftVoice.Stop();return;}
        var key=ActiveKeyboard;
        if(DeathVisible||Spectating||SurvivalActive&&Vitals.Incapacitated||!inventoryOpen&&key!=null&&
            (key.wKey.isPressed||key.aKey.isPressed||key.sKey.isPressed||key.dKey.isPressed||key.spaceKey.isPressed))
        {PauseItemCraft(job);return;}
        if(PortableCrafting.IsJob(job)){UpdatePortableCrafting(job);return;}
        var core=Inventory.Find(job.WorkpieceId);var tool=Inventory.Find(job.ToolId);
        if(core==null||tool==null||!AccessiblePossession(core)||(tool.Place!=PossessionPlace.RightHand&&tool.Place!=PossessionPlace.LeftHand))
        {PauseItemCraft(job);return;}
        int previous=(int)(job.Fraction*4);
        var flake=StoneCrafting.Advance(Inventory,job,Math.Min(.25,Time.unscaledDeltaTime),PlayerRules.Settings);
        if(job.Action==ItemCraftAction.Flake){if((int)(job.Fraction*4)>previous)PlayStoneCraftImpact();}
        else if(Time.unscaledTimeAsDouble>=craftSoundAt)PlayCraftSound(job.Action);
        if(flake==null)return;
        RestoreWorkHands(job);
        if(job.Action!=ItemCraftAction.Flake)
        {
            if(inventoryVisuals.TryGetValue(core.Id,out var oldWood)){DestroyInventoryVisual(oldWood);inventoryVisuals.Remove(core.Id);inventoryVisualPlaces.Remove(core.Id);}
            inventoryNotice=WoodCrafting.Label(job.Action)+" finished.";inventoryNoticeUntil=Time.unscaledTime+3;inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();return;
        }
        // The mass came from this particular stone; its source ID remains depleted.
        if(inventoryVisuals.TryGetValue(core.Id,out var old)){DestroyInventoryVisual(old);inventoryVisuals.Remove(core.Id);inventoryVisualPlaces.Remove(core.Id);}
        flake.Position=frame.Position(motion.X+Math.Sin(heading*Math.PI/180)*.65,motion.Z+Math.Cos(heading*Math.PI/180)*.65);
        var root=CreatePossessionVisual(flake,30);root.transform.SetParent(transform,false);inventoryVisuals[flake.Id]=root;
        root.transform.position=view.transform.TransformPoint(new Vector3(.08f,-.35f,.65f));root.transform.rotation=view.transform.rotation;
        ReleasePossession(flake,false);inventoryNearbyAt=0;
        inventoryNotice="Cutting flake finished.";inventoryNoticeUntil=Time.unscaledTime+3;
        if(inventoryOpen)RenderInventoryPreview();
    }
    void PlayCraftSound(ItemCraftAction action)
    {
        if(action==ItemCraftAction.Flake){PlayStoneCraftImpact();return;}
        if(woodCraftClips==null)woodCraftClips=Resources.LoadAll<AudioClip>("Crafting/Wood");
        if(woodCraftClips.Length==0)return;
        if(stoneCraftVoice==null){var obj=new GameObject("Player handcrafting");obj.transform.SetParent(transform,false);stoneCraftVoice=obj.AddComponent<AudioSource>();stoneCraftVoice.playOnAwake=false;stoneCraftVoice.spatialBlend=0;stoneCraftVoice.dopplerLevel=0;}
        stoneCraftVoice.pitch=.97f+(stoneCraftSoundIndex%5)*.015f;stoneCraftVoice.PlayOneShot(woodCraftClips[stoneCraftSoundIndex++%woodCraftClips.Length],.25f);craftSoundAt=Time.unscaledTimeAsDouble+1.2;
    }
    void PlayStoneCraftImpact()
    {
        if(stoneCraftClips==null)stoneCraftClips=Resources.LoadAll<AudioClip>("Crafting/Stone");
        if(stoneCraftClips.Length==0)return;
        if(stoneCraftVoice==null)
        {
            var obj=new GameObject("Player stone working");obj.transform.SetParent(transform,false);
            stoneCraftVoice=obj.AddComponent<AudioSource>();stoneCraftVoice.playOnAwake=false;stoneCraftVoice.spatialBlend=0;stoneCraftVoice.dopplerLevel=0;
        }
        var clip=stoneCraftClips[stoneCraftSoundIndex++%stoneCraftClips.Length];
        stoneCraftVoice.pitch=.97f+(stoneCraftSoundIndex%5)*.015f;stoneCraftVoice.PlayOneShot(clip,.45f);stoneCraftSoundCount++;
    }
    void DestroyInventoryVisual(GameObject root)
    {
        if(root==null)return;
        if(inventoryCraftMeshes.TryGetValue(root,out var meshes)){foreach(var mesh in meshes)Destroy(mesh);inventoryCraftMeshes.Remove(root);}
        portableRoots.Remove(root);root.SetActive(false);Destroy(root);
    }
    void AddCraftMesh(GameObject root,Mesh mesh,Material material,Possession item,int layer)
    {
        var child=new GameObject("Worked stone",typeof(MeshFilter),typeof(MeshRenderer));child.layer=layer;child.transform.SetParent(root.transform,false);
        child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        var props=new MaterialPropertyBlock();props.SetVector("_Tint",new Vector4((float)item.Tint.X,(float)item.Tint.Y,(float)item.Tint.Z,1));props.SetVector("_LodTransition",new Vector4(0,1,0,0));renderer.SetPropertyBlock(props);
        if(!inventoryCraftMeshes.TryGetValue(root,out var meshes))inventoryCraftMeshes.Add(root,meshes=new List<Mesh>());meshes.Add(mesh);
    }
    static Mesh CuttingFlakeMesh()
    {
        var ring=new[]{new Vector3(-.42f,.2f,-.46f),new Vector3(.2f,.05f,-.5f),new Vector3(.5f,.06f,-.1f),new Vector3(.23f,.07f,.46f),new Vector3(-.31f,.12f,.5f),new Vector3(-.5f,.25f,.05f)};
        var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();var uvs=new List<Vector2>();
        void Tri(Vector3 a,Vector3 b,Vector3 c,float shade){int i=vertices.Count;vertices.AddRange(new[]{a,b,c});indices.AddRange(new[]{i,i+1,i+2});for(int j=0;j<3;j++)colors.Add(new Color(shade,shade,shade));foreach(var p in new[]{a,b,c})uvs.Add(new Vector2(p.x+.5f,p.z+.5f));}
        var bulb=new Vector3(-.1f,1,-.14f);var underside=new Vector3(0,0,0);
        for(int i=0;i<ring.Length;i++){var a=ring[i];var b=ring[(i+1)%ring.Length];Tri(bulb,b,a,.94f-i*.025f);Tri(underside,a,b,.82f);}
        var mesh=new Mesh{name="Fresh cutting flake"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    struct StoneVertex
    {
        public Vector3 P;public Vector2 Uv;public Color Color;
        public static StoneVertex Lerp(StoneVertex a,StoneVertex b,float t)=>new StoneVertex{P=Vector3.Lerp(a.P,b.P,t),Uv=Vector2.Lerp(a.Uv,b.Uv,t),Color=Color.Lerp(a.Color,b.Color,t)};
    }
    static Mesh WorkedStoneMesh(Mesh source,int cuts)
    {
        var points=source.vertices;var uv=source.uv;var colors=source.colors;var tris=source.triangles;var polygons=new List<List<StoneVertex>>();
        for(int i=0;i<tris.Length;i+=3)polygons.Add(new[]{tris[i],tris[i+1],tris[i+2]}.Select(k=>new StoneVertex{P=points[k],Uv=k<uv.Length?uv[k]:Vector2.zero,Color=k<colors.Length?colors[k]:Color.white}).ToList());
        for(int cut=0;cut<Math.Min(3,cuts);cut++)
        {
            var n=Quaternion.Euler(0,cut*127,0)*new Vector3(1,.12f,0).normalized;float distance=Vector3.Dot(n,new Vector3(0,.5f,0))+.33f;
            var next=new List<List<StoneVertex>>();var rim=new List<Vector3>();
            foreach(var polygon in polygons)
            {
                var result=new List<StoneVertex>();
                for(int i=0;i<polygon.Count;i++)
                {
                    var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=Vector3.Dot(n,a.P)-distance,db=Vector3.Dot(n,b.P)-distance;
                    if(da<=0)result.Add(a);
                    if((da<0)!=(db<0)){var v=StoneVertex.Lerp(a,b,da/(da-db));result.Add(v);if(!rim.Any(p=>(p-v.P).sqrMagnitude<1e-9f))rim.Add(v.P);}
                }
                if(result.Count>=3)next.Add(result);
            }
            if(rim.Count>=3)
            {
                var center=rim.Aggregate(Vector3.zero,(a,b)=>a+b)/rim.Count;var u=Vector3.Cross(n,Vector3.up).normalized;var v=Vector3.Cross(n,u);
                rim=rim.OrderBy(p=>Mathf.Atan2(Vector3.Dot(p-center,v),Vector3.Dot(p-center,u))).ToList();
                next.Add(rim.Select(p=>new StoneVertex{P=p,Uv=new Vector2(p.x+.5f,p.y),Color=new Color(.96f,.96f,.96f)}).ToList());
            }
            polygons=next;
        }
        var vertices=new List<Vector3>();var indices=new List<int>();var tex=new List<Vector2>();var tint=new List<Color>();
        foreach(var polygon in polygons)for(int j=1;j<polygon.Count-1;j++)foreach(var p in new[]{polygon[0],polygon[j],polygon[j+1]}){indices.Add(vertices.Count);vertices.Add(p.P);tex.Add(p.Uv);tint.Add(p.Color);}
        var mesh=new Mesh{name="Stone with flake scars"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.SetColors(tint);mesh.SetUVs(0,tex);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    const int ScatterSolidLayer=27,PlayerSolidLayer=26;
    sealed class ScatterSolid
    {public GameObject Root;public ScatterInstance Source;public bool Tree;public int Stamp;}
    readonly Dictionary<ulong,ScatterSolid> scatterSolids=new Dictionary<ulong,ScatterSolid>();
    readonly Dictionary<Collider,ScatterSolid> scatterSolidSources=new Dictionary<Collider,ScatterSolid>();
    readonly Dictionary<Mesh,Mesh> scatterSolidMeshes=new Dictionary<Mesh,Mesh>();
    readonly List<ulong> retiredScatterSolids=new List<ulong>();
    CharacterController playerSolid;
    Vector3 solidCacheCenter;
    int solidStamp,solidRevision=-1,solidCollected=-1;
    double solidOriginX=double.NaN,solidOriginZ,solidRefreshAt;

    void PreparePlayerSolids()
    {
        if(playerSolid==null)
        {
            var root=new GameObject("Player movement collision");root.layer=PlayerSolidLayer;
            root.transform.SetParent(transform,false);playerSolid=root.AddComponent<CharacterController>();
            playerSolid.height=1.75f;playerSolid.radius=.25f;playerSolid.center=Vector3.up*.875f;
            playerSolid.skinWidth=.015f;playerSolid.stepOffset=.22f;playerSolid.slopeLimit=50;playerSolid.minMoveDistance=0;
            playerSolid.enabled=false;
            // This motor only solves instanced solids. The existing analytic
            // terrain motor continues to own slopes, gravity and fall damage.
            for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(PlayerSolidLayer,layer,layer!=ScatterSolidLayer);
            motion.SolidContact=ResolvePlayerSolidContact;
        }
        if(!motion.NoClip)RefreshPlayerSolids(PlayerFeet(),PlayerFeet());
    }
    Vector3 PlayerFeet()=>new Vector3((float)(motion.X-originX),(float)(motion.Y-TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
    static bool SolidTree(ScatterKind kind)=>kind==ScatterKind.GeneratedTree||kind==ScatterKind.Broadleaf||kind==ScatterKind.Conifer||kind==ScatterKind.SavannaTree;

    void RefreshPlayerSolids(Vector3 from,Vector3 to)
    {
        Vector3 center=(from+to)*.5f;float swept=(to-from).magnitude*.5f;
        bool sameOrigin=solidOriginX==originX&&solidOriginZ==originZ;
        if(sameOrigin&&solidRevision==canopySourceRevision&&solidCollected==inventoryCollected.Count&&Time.unscaledTimeAsDouble<solidRefreshAt&&
            (center-solidCacheCenter).sqrMagnitude<16&&swept<4)return;
        solidOriginX=originX;solidOriginZ=originZ;solidRevision=canopySourceRevision;solidCollected=inventoryCollected.Count;
        solidRefreshAt=Time.unscaledTimeAsDouble+.25;solidCacheCenter=center;int stamp=++solidStamp;
        float radius=16+swept;var region=new Bounds(center,Vector3.one*radius*2);
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.Groups==null||!chunk.AllBounds.Intersects(region))continue;
            foreach(var group in chunk.Groups)
            {
                if(!group.Bounds.Intersects(region))continue;
                foreach(int i in group.Indices)
                {
                    var p=chunk.Source.Instances[i];bool tree=SolidTree(p.Kind);
                    bool rock=p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Outcrop||p.Kind==ScatterKind.Stone;
                    if(!tree&&!rock||rock&&(CollectedRock(p)||p.HeightM<.12))continue;
                    // Bounds include the entire rock, rather than only its root.
                    if(!chunk.Bounds[i].Intersects(region))continue;
                    if(!scatterSolids.TryGetValue(p.Id,out var solid))
                    {
                        solid=new ScatterSolid{Root=new GameObject(tree?"Solid tree trunk":"Solid rock"),Source=p,Tree=tree};
                        solid.Root.layer=ScatterSolidLayer;solid.Root.transform.SetParent(transform,false);
                        scatterSolids.Add(p.Id,solid);
                        if(tree)
                        {
                            float r=Mathf.Max(.015f,(float)p.WidthM*(p.Kind==ScatterKind.GeneratedTree?(float)new TreeMorphology(p.Variant).TrunkRadius:.035f));
                            float h=Mathf.Max(r*2,(float)p.HeightM*.88f);
                            var capsule=solid.Root.AddComponent<CapsuleCollider>();capsule.radius=r;capsule.height=h;capsule.center=Vector3.up*h*.5f;
                            scatterSolidSources.Add(capsule,solid);
                        }
                        else
                        {
                            var model=Art(p.Kind,p.Variant,0);
                            foreach(var part in model.Parts)
                            {
                                if(!scatterSolidMeshes.TryGetValue(part.Mesh,out var mesh))
                                {mesh=Instantiate(part.Mesh);mesh.name=part.Mesh.name+" collision";scatterSolidMeshes.Add(part.Mesh,mesh);}
                                var child=new GameObject("Rock surface");child.layer=ScatterSolidLayer;child.transform.SetParent(solid.Root.transform,false);
                                SetSolidTransform(child.transform,model.Scale*part.Local);
                                var collider=child.AddComponent<MeshCollider>();collider.sharedMesh=mesh;scatterSolidSources.Add(collider,solid);
                            }
                        }
                    }
                    solid.Stamp=stamp;
                    if(tree){solid.Root.transform.localPosition=chunk.Roots[i];solid.Root.transform.localRotation=Quaternion.identity;solid.Root.transform.localScale=Vector3.one;}
                    else SetSolidTransform(solid.Root.transform,chunk.Matrices[i]);
                }
            }
        }
        retiredScatterSolids.Clear();foreach(var pair in scatterSolids)if(pair.Value.Stamp!=stamp)retiredScatterSolids.Add(pair.Key);
        foreach(ulong id in retiredScatterSolids)
        {
            var solid=scatterSolids[id];foreach(var collider in solid.Root.GetComponentsInChildren<Collider>())scatterSolidSources.Remove(collider);
            solid.Root.SetActive(false);Destroy(solid.Root);scatterSolids.Remove(id);
        }
        if(scatterSolidMeshes.Count>128)
        {
            var used=new HashSet<Mesh>();foreach(var solid in scatterSolids.Values)foreach(var collider in solid.Root.GetComponentsInChildren<MeshCollider>())used.Add(collider.sharedMesh);
            var unused=new List<Mesh>();foreach(var pair in scatterSolidMeshes)if(!used.Contains(pair.Value))unused.Add(pair.Key);
            foreach(var key in unused){Destroy(scatterSolidMeshes[key]);scatterSolidMeshes.Remove(key);}
        }
        Physics.SyncTransforms();
    }
    static void SetSolidTransform(Transform target,Matrix4x4 matrix)
    {target.localPosition=matrix.GetColumn(3);target.localRotation=matrix.rotation;target.localScale=matrix.lossyScale;}

    double PlayerSolidGround(double x,double z)
    {
        double ground=Ground(x,z);if(motion.NoClip)return ground;
        float feet=(float)(motion.Y-TerrainMotion.EyeHeightM);
        var origin=transform.TransformPoint(new Vector3((float)(x-originX),feet+.23f,(float)(z-originZ)));
        if(Physics.Raycast(origin,transform.up*-1,out var hit,Mathf.Max(.24f,feet-(float)ground+.24f),1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore)&&hit.normal.y>.6f)
            ground=Math.Max(ground,transform.InverseTransformPoint(hit.point).y);
        return ground;
    }
    void ResolvePlayerSolidContact(TerrainMotion motor,double oldX,double oldY,double oldZ)
    {
        if(playerSolid==null||motor.NoClip)return;
        var from=new Vector3((float)(oldX-originX),(float)(oldY-TerrainMotion.EyeHeightM),(float)(oldZ-originZ));var target=PlayerFeet();
        RefreshPlayerSolids(from,target);
        playerSolid.transform.localPosition=from;playerSolid.enabled=true;
        float falling=(float)Math.Max(0,-motor.VerticalSpeed);
        var flags=playerSolid.Move(transform.TransformVector(target-from));
        var result=playerSolid.transform.localPosition;playerSolid.enabled=false;
        // Apply only the solver's correction. Copying its float position back
        // wholesale would quantize ordinary movement at high FPS/far origins.
        motor.X+=result.x-target.x;motor.Z+=result.z-target.z;motor.Y+=result.y-target.y;
        if((flags&CollisionFlags.Below)!=0&&!motor.Flying)motor.LandOnSolid(falling);
        if((flags&CollisionFlags.Above)!=0&&motor.VerticalSpeed>0)motor.VerticalSpeed=0;
    }
    void DisposePlayerSolids()
    {
        motion.SolidContact=null;if(playerSolid!=null){playerSolid.enabled=false;Destroy(playerSolid.gameObject);}
        foreach(var solid in scatterSolids.Values){solid.Root.SetActive(false);Destroy(solid.Root);}scatterSolids.Clear();scatterSolidSources.Clear();
        foreach(var mesh in scatterSolidMeshes.Values)Destroy(mesh);scatterSolidMeshes.Clear();
    }
}

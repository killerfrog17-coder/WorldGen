using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class DroppedPossession
    {
        public Possession Item;public Rigidbody Body;public BoxCollider Collider;
        public float Quiet;public bool Paused;public Vector3 Velocity,Spin;
    }
    sealed class DropFloor {public GameObject Root;public Mesh Mesh;}
    readonly Dictionary<string,DroppedPossession> inventoryDrops=new Dictionary<string,DroppedPossession>();
    readonly Dictionary<(int,int),DropFloor> inventoryFloors=new Dictionary<(int,int),DropFloor>();
    PhysicsMaterial inventoryDropMaterial;

    NearbyPossession AimedPossession()
    {
        var ray=new Ray(view.transform.localPosition,view.transform.forward);float closest=float.MaxValue;NearbyPossession target=null;
        foreach(var candidate in inventoryNearby)
            if(candidate.Bounds.IntersectRay(ray,out float distance)&&distance<closest){closest=distance;target=candidate;}
        return target;
    }
    void DrawPickupPrompt()
    {
        if(!Ready||DeathVisible||Spectating||inventoryOpen||paused||mapOpen||chatOpen||ecologyOpen||!Application.isFocused&&!inventoryReviewMode)return;
        RefreshInventoryNearby();PrepareInventoryIcons();
        float scale=Mathf.Clamp(Screen.height/1080f,.6f,1.8f);
        var feet=new Vector3((float)(motion.X-originX),(float)(motion.Y-TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
        var old=GUI.color;
        foreach(var candidate in inventoryMarkers)
        {
            float distance=Vector3.Distance(feet,candidate.Point),alpha=Mathf.SmoothStep(0,1,4-distance);
            if(alpha<=0)continue;var screen=view.WorldToScreenPoint(transform.TransformPoint(candidate.Point));
            if(screen.z<=0)continue;
            float size=34*scale;
            GUI.color=new Color(0,0,0,alpha*.8f);DrawPickupCategory(new Rect(screen.x-size/2-1,Screen.height-screen.y-size-1,size+2,size+2),candidate.Item);
            GUI.color=new Color(1,1,1,alpha);DrawPickupCategory(new Rect(screen.x-size/2,Screen.height-screen.y-size,size,size),candidate.Item);
        }
        GUI.color=old;
        var target=AimedPossession();if(target==null)return;
        var style=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(17*scale),alignment=TextAnchor.MiddleCenter,padding=new RectOffset(),wordWrap=false};
        var rect=new Rect(Screen.width*.5f-180*scale,Screen.height*.5f+31*scale,360*scale,30*scale);
        style.normal.textColor=Color.black;GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),InventoryDisplayName(target.Item)+"  [E]",style);
        style.normal.textColor=new Color(.95f,.95f,.89f);GUI.Label(rect,InventoryDisplayName(target.Item)+"  [E]",style);
    }
    void ReleasePossession(Possession item,bool fromContainer)
    {
        if(!inventoryVisuals.TryGetValue(item.Id,out var root))
        {root=CreatePossessionVisual(item,30);root.transform.SetParent(transform,false);inventoryVisuals[item.Id]=root;fromContainer=true;}
        if(fromContainer){root.transform.position=view.transform.TransformPoint(new Vector3(0,-.32f,.62f));root.transform.rotation=view.transform.rotation;}
        // The source remains at its held/worn visual pose until this release.
        // A view item is already in front of the camera; a worn bag falls behind.
        var position=root.transform.localPosition;
        item.Position=frame.Position(position.x+originX,position.z+originZ);
        item.GroundOffsetM=position.y+.01-Ground(position.x+originX,position.z+originZ);
        item.GroundRotation=new V(root.transform.localEulerAngles.x,root.transform.localEulerAngles.y,root.transform.localEulerAngles.z);
        item.GroundMoving=true;
        BeginDroppedBody(item,root,view.transform.forward*.35f);
    }
    void BeginDroppedBody(Possession item,GameObject root,Vector3 velocity)
    {
        if(inventoryDrops.TryGetValue(item.Id,out var existing))
        {if(existing.Body!=null&&existing.Collider!=null)return;RemoveDroppedBody(item.Id);}
        if(inventoryDropMaterial==null)inventoryDropMaterial=new PhysicsMaterial("Loose item friction"){dynamicFriction=.65f,staticFriction=.75f,bounciness=.12f};
        var collider=root.GetComponent<BoxCollider>();if(collider==null)collider=root.AddComponent<BoxCollider>();
        var bounds=new Bounds();bool first=true;
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            if(mesh.sharedMesh==null)continue;
            var b=mesh.sharedMesh.bounds;var matrix=root.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
            for(int corner=0;corner<8;corner++)
            {
                var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        collider.center=bounds.center;collider.size=bounds.size*.94f;collider.sharedMaterial=inventoryDropMaterial;collider.enabled=true;
        var body=root.GetComponent<Rigidbody>();if(body==null)body=root.AddComponent<Rigidbody>();body.isKinematic=false;body.detectCollisions=true;body.mass=(float)item.MassKg;
        body.linearDamping=.1f;body.angularDamping=.55f;body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.maxDepenetrationVelocity=2;
        body.linearVelocity=velocity;body.angularVelocity=new Vector3(1.1f,.4f,.7f);
        inventoryDrops.Add(item.Id,new DroppedPossession{Item=item,Body=body,Collider=collider});
        var contact=root.GetComponent<WorldGenProjectileContact>();if(contact==null)contact=root.AddComponent<WorldGenProjectileContact>();contact.Owner=this;contact.ItemId=item.Id;
        EnsureDropFloors(root.transform.localPosition,Mathf.Max((float)item.WidthM,(float)item.HeightM));Physics.SyncTransforms();
    }
    void CaptureInventoryPhysics()
    {
        if(frame==null)return;
        foreach(var drop in inventoryDrops.Values.ToArray())
        {
            if(drop.Body==null){RemoveDroppedBody(drop.Item.Id);continue;}
            CaptureDroppedPose(drop);
        }
    }
    void CaptureDroppedPose(DroppedPossession drop)
    {
        if(frame==null||drop.Body==null)return;
        var position=transform.InverseTransformPoint(drop.Body.position);
        drop.Item.Position=frame.Position(position.x+originX,position.z+originZ);
        drop.Item.GroundOffsetM=position.y+.01-Ground(position.x+originX,position.z+originZ);
        var rotation=(Quaternion.Inverse(transform.rotation)*drop.Body.rotation).eulerAngles;
        drop.Item.GroundRotation=new V(rotation.x,rotation.y,rotation.z);
        var velocity=drop.Paused?drop.Velocity:drop.Body.linearVelocity;var spin=drop.Paused?drop.Spin:drop.Body.angularVelocity;
        drop.Item.GroundVelocity=new V(velocity.x,velocity.y,velocity.z);drop.Item.GroundSpin=new V(spin.x,spin.y,spin.z);
    }
    void RemoveDroppedBody(string id,bool keepCollider=false)
    {
        itemProjectiles.Remove(id);
        if(inventoryDrops.TryGetValue(id,out var drop))
        {inventoryDrops.Remove(id);if(drop.Body!=null){drop.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;drop.Body.isKinematic=true;drop.Body.detectCollisions=false;}}
        if(!keepCollider&&inventoryVisuals.TryGetValue(id,out var root)&&root!=null)
        {var collider=root.GetComponent<BoxCollider>();if(collider!=null){collider.enabled=false;}}
    }
    void EnsureDropFloors(Vector3 position,float radius,HashSet<(int,int)> used=null)
    {
        radius=Mathf.Max(.4f,radius);
        int x0=(int)Math.Floor((position.x+originX-radius)/4),x1=(int)Math.Floor((position.x+originX+radius)/4);
        int z0=(int)Math.Floor((position.z+originZ-radius)/4),z1=(int)Math.Floor((position.z+originZ+radius)/4);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
        {
            var key=(x,z);used?.Add(key);if(inventoryFloors.ContainsKey(key))continue;
            const int segments=8;var vertices=new Vector3[81];var indices=new int[segments*segments*6];int p=0;
            for(int iz=0;iz<=segments;iz++)for(int ix=0;ix<=segments;ix++)
            {double px=x*4+ix*.5,pz=z*4+iz*.5;vertices[iz*9+ix]=new Vector3((float)(px-originX),(float)VisibleGround(px,pz,Ground(px,pz)),(float)(pz-originZ));}
            for(int iz=0;iz<segments;iz++)for(int ix=0;ix<segments;ix++)
            {int a=iz*9+ix,b=a+1,c=a+9,d=c+1;indices[p++]=a;indices[p++]=c;indices[p++]=b;indices[p++]=b;indices[p++]=c;indices[p++]=d;}
            var mesh=new Mesh{name="Loose item terrain collision"};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateBounds();
            var floor=new GameObject("Loose item terrain");floor.transform.SetParent(transform,false);floor.AddComponent<MeshCollider>().sharedMesh=mesh;
            inventoryFloors.Add(key,new DropFloor{Root=floor,Mesh=mesh});
        }
    }
    void UpdateDroppedBodies()
    {
        bool pause=inventoryOpen&&!CraftWorking||paused||mapOpen||ecologyOpen||chatOpen||!Application.isFocused&&!inventoryReviewMode;
        var used=new HashSet<(int,int)>();
        foreach(var drop in inventoryDrops.Values.ToArray())
        {
            if(drop.Body==null||drop.Collider==null){RemoveDroppedBody(drop.Item.Id);continue;}
            var body=drop.Body;
            if(drop.Paused!=pause)
            {
                drop.Paused=pause;
                if(pause){drop.Velocity=body.linearVelocity;drop.Spin=body.angularVelocity;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.isKinematic=true;}
                else {body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearVelocity=drop.Velocity;body.angularVelocity=drop.Spin;}
            }
            EnsureDropFloors(transform.InverseTransformPoint(body.position),Mathf.Max((float)drop.Item.WidthM,(float)drop.Item.HeightM),used);
            if(pause)continue;
            bool quiet=body.IsSleeping()||body.linearVelocity.sqrMagnitude<.0025f&&body.angularVelocity.sqrMagnitude<.04f;
            drop.Quiet=quiet?drop.Quiet+Time.unscaledDeltaTime:0;
            if(drop.Quiet>.8f){CaptureInventoryPhysics();drop.Item.GroundMoving=false;drop.Item.InFlight=false;RemoveDroppedBody(drop.Item.Id,true);}
        }
        CaptureInventoryPhysics();
        foreach(var key in inventoryFloors.Keys.Where(k=>!used.Contains(k)).ToArray())
        {var floor=inventoryFloors[key];floor.Root.SetActive(false);Destroy(floor.Root);Destroy(floor.Mesh);inventoryFloors.Remove(key);}
    }
    void ClearInventoryPhysics()
    {
        CaptureInventoryPhysics();
        foreach(var id in inventoryDrops.Keys.ToArray())
        {
            RemoveDroppedBody(id);
            if(inventoryVisuals.TryGetValue(id,out var root)){DestroyInventoryVisual(root);inventoryVisuals.Remove(id);inventoryVisualPlaces.Remove(id);}
        }
        foreach(var floor in inventoryFloors.Values){floor.Root.SetActive(false);Destroy(floor.Root);Destroy(floor.Mesh);}inventoryFloors.Clear();
    }
}

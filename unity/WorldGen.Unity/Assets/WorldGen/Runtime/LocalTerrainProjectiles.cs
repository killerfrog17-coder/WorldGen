using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed class WorldGenProjectileContact : MonoBehaviour
{
    public LocalTerrainExplorer Owner;public string ItemId;
    void OnCollisionEnter(Collision collision){if(Owner!=null)Owner.ProjectileCollision(ItemId,collision);}
}
public sealed partial class LocalTerrainExplorer
{
    sealed class ItemProjectile
    {public Possession Item;public DroppedPossession Drop;public Vector3 Previous,Velocity;}
    readonly Dictionary<string,ItemProjectile> itemProjectiles=new Dictionary<string,ItemProjectile>();
    static Vector3 UnityVector(V v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
    static V CoreVector(Vector3 v)=>new V(v.x,v.y,v.z);
    bool PointedProjectile(Possession p)=>p.Kind==PossessionKind.Arrow||p.Kind==PossessionKind.UnfletchedArrow||p.Kind==PossessionKind.SpearDart||p.Kind==PossessionKind.WoodenSpear||p.Kind==PossessionKind.StoneTippedSpear||p.Kind==PossessionKind.BoneTippedSpear||p.Kind==PossessionKind.Harpoon||p.Kind==PossessionKind.LooseStoneSpear;
    Vector3 ProjectileTip(ItemProjectile p)=>PointedProjectile(p.Item)?p.Drop.Body.position+p.Drop.Body.rotation*Vector3.up*(float)p.Item.HeightM:p.Drop.Body.worldCenterOfMass;
    void LaunchItemProjectile(Possession item,Vector3 origin,Vector3 direction,float speed)
    {
        RemoveDroppedBody(item.Id);Inventory.Move(item.Id,PossessionPlace.Ground,"",0,0,false,out _);item.AttachedAnimal=0;item.AttachedHuman=0;item.InFlight=item.GroundMoving=true;
        if(!inventoryVisuals.TryGetValue(item.Id,out var root)){root=CreatePossessionVisual(item,30);root.transform.SetParent(transform,false);inventoryVisuals[item.Id]=root;}
        root.transform.rotation=PointedProjectile(item)?Quaternion.FromToRotation(Vector3.up,direction):UnityEngine.Random.rotation;
        root.transform.position=origin-(PointedProjectile(item)?direction*(float)item.HeightM*.5f:Vector3.zero);
        item.GroundVelocity=CoreVector(direction*speed);var local=root.transform.localPosition;item.Position=frame.Position(local.x+originX,local.z+originZ);item.GroundOffsetM=local.y-Ground(local.x+originX,local.z+originZ);
        item.GroundRotation=CoreVector(root.transform.localEulerAngles);BeginDroppedBody(item,root,direction*speed);RegisterProjectile(item);inventoryNearbyAt=0;
        if(itemProjectiles.TryGetValue(item.Id,out var launch))launch.Previous=origin-direction*.4f;
    }
    void RegisterProjectile(Possession item)
    {
        if(!item.InFlight||itemProjectiles.ContainsKey(item.Id)||!inventoryDrops.TryGetValue(item.Id,out var drop))return;
        var p=new ItemProjectile{Item=item,Drop=drop,Velocity=drop.Body.linearVelocity};p.Previous=ProjectileTip(p);itemProjectiles.Add(item.Id,p);drop.Body.linearDamping=0;
        drop.Body.angularDamping=PointedProjectile(item)?2:.1f;
        var contact=drop.Body.GetComponent<WorldGenProjectileContact>();if(contact==null)contact=drop.Body.gameObject.AddComponent<WorldGenProjectileContact>();contact.Owner=this;contact.ItemId=item.Id;
    }
    void FixedUpdate()
    {
        if(closed||!Ready)return;
        bool stopped=paused||mapOpen||ecologyOpen||chatOpen||inventoryOpen&&!CraftWorking;
        foreach(var p in itemProjectiles.Values.ToArray())
        {
            if(!p.Item.InFlight||p.Item.Place!=PossessionPlace.Ground||p.Drop.Body==null||!inventoryDrops.ContainsKey(p.Item.Id)){itemProjectiles.Remove(p.Item.Id);continue;}
            if(stopped||p.Drop.Paused){p.Previous=ProjectileTip(p);continue;}
            Vector3 at=ProjectileTip(p),delta=at-p.Previous;float length=delta.magnitude;
            if(length>.0001f)
            {
                var ray=new Ray(p.Previous,delta/length);var wall=TraceCombatObstacle(ray,length,p.Drop.Collider);var target=TraceProjectileTarget(p,ray,wall?.Distance??length);
                if(target!=null){HitProjectile(p,target);continue;}
                if(wall!=null){StopProjectileAt(p,wall.Point,wall.Normal,true);continue;}
            }
            var velocity=p.Drop.Body.linearVelocity;float speed=velocity.magnitude;
            double area=Math.Max(.000001,Math.PI*p.Item.WidthM*p.Item.DepthM*.25),coefficient=PointedProjectile(p.Item)?.12:.47;
            if(speed>.01f)p.Drop.Body.linearVelocity=velocity.normalized*Mathf.Max(0,speed-(float)ItemUseRules.DragAcceleration(p.Item.MassKg,area,speed,coefficient)*Time.fixedDeltaTime);
            if(PointedProjectile(p.Item)&&speed>2)p.Drop.Body.MoveRotation(Quaternion.Slerp(p.Drop.Body.rotation,Quaternion.FromToRotation(Vector3.up,velocity.normalized),Mathf.Min(1,Time.fixedDeltaTime*10)));
            p.Velocity=velocity;p.Previous=at;EnsureDropFloors(transform.InverseTransformPoint(p.Drop.Body.position+velocity*.15f),Mathf.Max(.5f,(float)p.Item.HeightM));
        }
    }
    CombatHit TraceProjectileTarget(ItemProjectile projectile,Ray ray,float reach)
    {
        float radius=(float)Math.Min(.1,Math.Max(.002,projectile.Item.WidthM*.35));Vector3 side=Vector3.Cross(ray.direction,Vector3.up).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;Vector3 up=Vector3.Cross(side,ray.direction).normalized;
        CombatHit best=null;foreach(var offset in new[]{Vector3.zero,side*radius,-side*radius,up*radius,-up*radius}){var hit=TraceLiving(new Ray(ray.origin+offset,ray.direction),best?.Distance??reach);if(hit!=null)best=hit;}return best;
    }
    void HitProjectile(ItemProjectile p,CombatHit hit)
    {
        var velocity=p.Velocity.sqrMagnitude>.01f?p.Velocity:p.Drop.Body.linearVelocity;float speed=velocity.magnitude;double penetration=ItemUseRules.Profile(p.Item).Penetration;
        double damage=ItemUseRules.ImpactDamage(p.Item.MassKg,speed,penetration,ItemUseRules.Condition(p.Item));
        ApplyCombatHit(hit,damage,velocity.normalized,p.Item.MassKg*speed);ApplyItemImpactWear(p.Item,ItemUseRules.KineticEnergy(p.Item.MassKg,speed),false);
        bool stick=PointedProjectile(p.Item)&&penetration>.25&&speed>4&&p.Item.Kind!=PossessionKind.LooseStoneSpear;
        if(stick)
        {
            p.Drop.Body.position=hit.Point-velocity.normalized*(float)p.Item.HeightM*.82f;p.Drop.Body.rotation=Quaternion.FromToRotation(Vector3.up,velocity.normalized);
            CaptureInventoryPhysics();p.Item.InFlight=false;p.Item.GroundMoving=false;itemProjectiles.Remove(p.Item.Id);RemoveDroppedBody(p.Item.Id,true);
            if(hit.Animal!=null)AttachToAnimal(p.Item,hit,velocity.normalized);
            else if(hit.Human!=null)AttachToHuman(p.Item,hit,velocity.normalized);
        }
        else
        {
            p.Drop.Body.position=hit.Point-velocity.normalized*.04f;p.Drop.Body.linearVelocity=Vector3.Reflect(velocity,hit.Normal)*.22f;p.Item.InFlight=false;itemProjectiles.Remove(p.Item.Id);SplitLooseSpear(p.Item);
        }
    }
    void StopProjectileAt(ItemProjectile p,Vector3 point,Vector3 normal,bool hard)
    {
        if(!p.Item.InFlight)return;Vector3 velocity=p.Velocity.sqrMagnitude>.01f?p.Velocity:p.Drop.Body.linearVelocity;
        ApplyItemImpactWear(p.Item,ItemUseRules.KineticEnergy(p.Item.MassKg,velocity.magnitude),hard);p.Item.InFlight=false;itemProjectiles.Remove(p.Item.Id);
        if(PointedProjectile(p.Item)&&velocity.magnitude>5&&Vector3.Dot(velocity.normalized,normal)<-.22f&&ItemUseRules.Condition(p.Item)>0)
        {
            p.Drop.Body.position=point-velocity.normalized*(float)p.Item.HeightM*.9f;p.Drop.Body.rotation=Quaternion.FromToRotation(Vector3.up,velocity.normalized);
            CaptureInventoryPhysics();p.Item.GroundMoving=false;RemoveDroppedBody(p.Item.Id,true);
        }
        else p.Drop.Body.linearVelocity=Vector3.Reflect(velocity,normal)*.25f;
        SplitLooseSpear(p.Item);
    }
    internal void ProjectileCollision(string id,Collision collision)
    {
        if(inventoryInitialized&&inventoryDrops.TryGetValue(id,out var drop))PlayDroppedImpact(drop.Item,collision);
        if(!itemProjectiles.TryGetValue(id,out var p)||!p.Item.InFlight||collision.contactCount==0)return;
        var contact=collision.GetContact(0);StopProjectileAt(p,contact.point,contact.normal,true);
    }
    void AttachToAnimal(Possession item,CombatHit hit,Vector3 direction)
    {
        var animal=hit.Animal;InitializeWildlifeHealth();if(!wildlifeSkeletons.TryGetValue(animal.Spawn.Profile.slug,out var skeleton))return;
        var surface=AnimalSurface(animal);int vertex=surface.Triangles[hit.Face*3],bone=0;float best=0;
        for(int i=0;i<4;i++){if(surface.Weights[vertex][i]>best){best=surface.Weights[vertex][i];bone=(int)surface.Joints[vertex][i];}if(surface.ExtraWeights[vertex][i]>best){best=surface.ExtraWeights[vertex][i];bone=(int)surface.ExtraJoints[vertex][i];}}
        var frame=AnimalBoneWorld(animal,bone,skeleton);var root=inventoryVisuals[item.Id];item.AttachedAnimal=animal.Spawn.Id;item.AttachedBone=bone;
        item.AttachedLocalPoint=CoreVector(frame.inverse.MultiplyPoint3x4(root.transform.position));item.AttachedDirection=CoreVector(frame.inverse.MultiplyVector(direction).normalized);
    }
    Matrix4x4 AnimalBoneWorld(Animal animal,int bone,FaunaPhysicsSpecies skeleton)
    {
        var rest=new Matrix4x4();var values=skeleton.bones[bone].rest;for(int r=0;r<4;r++)for(int c=0;c<4;c++)rest[r,c]=values[r*4+c];
        return animal.Root.transform.localToWorldMatrix*animalBoneData[animal.Slot*AnimalBones+bone]*rest;
    }
    void AttachToHuman(Possession item,CombatHit hit,Vector3 direction)
    {
        var bones=hit.Human.Root.GetComponentsInChildren<Transform>();var bone=bones.Where(b=>b!=hit.Human.Root.transform).OrderBy(b=>(b.position-hit.Point).sqrMagnitude).FirstOrDefault();if(bone==null)return;
        item.AttachedHuman=hit.Human.Id;item.AttachedBoneName=bone.name;var root=inventoryVisuals[item.Id];item.AttachedLocalPoint=CoreVector(bone.InverseTransformPoint(root.transform.position));item.AttachedDirection=CoreVector(bone.InverseTransformDirection(direction));
    }
    void ReleaseProjectileAttachment(Possession item)
    {
        item.AttachedAnimal=0;item.AttachedHuman=0;item.AttachedBone=0;item.AttachedBoneName=null;
        item.AttachedLocalPoint=item.AttachedDirection=new V();item.InFlight=false;
        // Keep the last recorded world pose; ordinary drop physics resumes
        // when the item is visible. Do not invent another projectile or hit.
        if(item.Place==PossessionPlace.Ground){item.GroundMoving=true;item.GroundVelocity=item.GroundSpin=new V();}
    }
    void UpdateAttachedProjectiles()
    {
        if(!inventoryInitialized||!Ready)return;
        foreach(var item in Inventory.Items.Where(p=>p.Place==PossessionPlace.Ground&&(p.AttachedAnimal!=0||p.AttachedHuman!=0)))
        {
            Matrix4x4 matrix=Matrix4x4.identity;bool found=false;
            if(item.AttachedAnimal!=0&&animals.TryGetValue(item.AttachedAnimal,out var animal)&&!animal.Retired&&animal.Root!=null&&wildlifeSkeletons!=null&&wildlifeSkeletons.TryGetValue(animal.Spawn.Profile.slug,out var skeleton)&&item.AttachedBone>=0&&item.AttachedBone<skeleton.bones.Length)
            {matrix=AnimalBoneWorld(animal,item.AttachedBone,skeleton);found=true;}
            else if(item.AttachedHuman!=0)
            {
                var human=humanNpcs.FirstOrDefault(h=>h.Id==item.AttachedHuman);var bone=human?.Root!=null?human.Root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==item.AttachedBoneName):null;
                if(bone!=null){matrix=bone.localToWorldMatrix;found=true;}
            }
            if(!found)
            {
                // Saved animals can simply be streamed out. Temporary humans
                // and explicitly cleared animals have no persistent owner.
                bool savedAnimal=item.AttachedAnimal!=0&&(wildlifeRecords?.ContainsKey(item.AttachedAnimal)??Wildlife.Individuals.Any(a=>a.Id==item.AttachedAnimal));
                bool liveAnimal=item.AttachedAnimal!=0&&animals.ContainsKey(item.AttachedAnimal);
                bool liveHuman=item.AttachedHuman!=0&&humanNpcs.Any(h=>h.Id==item.AttachedHuman&&h.Root!=null);
                if(!savedAnimal&&!liveAnimal&&!liveHuman)ReleaseProjectileAttachment(item);
                continue;
            }
            var point=transform.InverseTransformPoint(matrix.MultiplyPoint3x4(UnityVector(item.AttachedLocalPoint)));var direction=transform.InverseTransformDirection(matrix.MultiplyVector(UnityVector(item.AttachedDirection))).normalized;
            item.Position=frame.Position(point.x+originX,point.z+originZ);item.GroundOffsetM=point.y-Ground(point.x+originX,point.z+originZ);item.GroundRotation=CoreVector(Quaternion.FromToRotation(Vector3.up,direction).eulerAngles);
        }
    }
}

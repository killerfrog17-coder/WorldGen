using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    [Serializable] sealed class AnimalRigFile{public int version;public string slug,sourceSha256;public AnimalRigBone[] bones;}
    [Serializable] sealed class AnimalRigBone{public string name;public int parent;public float[] bind,head,tail;public float radius,length;public int vertices;public bool physical;}
    sealed class AnimalRagdoll
    {
        public Animal Animal;public SurvivalCorpse Corpse;public Transform[] Bones;public Rigidbody[] Bodies;public Matrix4x4[] InverseBind,Attachments;
        public int[] PhysicalParent;public GameObject Ground;public int QuietFrames;public bool Paused;public Vector3[] Velocities,Spins;
    }
    sealed class SurvivalProjectile{public string Item;public GameObject Root;public Vector3 Velocity;public double Life,Damage;public bool Recoverable=true;}
    readonly Dictionary<ulong,AnimalRagdoll> animalRagdolls=new Dictionary<ulong,AnimalRagdoll>();
    readonly Dictionary<string,AnimalRigFile> animalRigs=new Dictionary<string,AnimalRigFile>();
    readonly List<SurvivalProjectile> survivalProjectiles=new List<SurvivalProjectile>();
    void InitializeRagdolls(){}
    static Matrix4x4 RigMatrix(float[] values){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=values[r*4+c];return m;}
    void StartAnimalRagdoll(Animal animal,SurvivalCorpse corpse,Vector3 impulse)
    {
        string slug=animal.Spawn.Profile.slug;
        if(!animalRigs.TryGetValue(slug,out var rig))
        {
            string path=Path.Combine(Application.streamingAssetsPath,"fauna-rigs",slug+".json");
            if(File.Exists(path))rig=JsonUtility.FromJson<AnimalRigFile>(File.ReadAllText(path));animalRigs[slug]=rig;
        }
        if(rig==null||rig.version!=1||rig.sourceSha256!=animal.Model.Asset.SourceSha256||rig.bones.Length!=animal.Model.Asset.BoneCount)
        {Debug.LogError("Original ragdoll bind hierarchy unavailable for "+slug);return;}
        int count=rig.bones.Length;
        var rag=new AnimalRagdoll{Animal=animal,Corpse=corpse,Bones=new Transform[count],Bodies=new Rigidbody[count],InverseBind=new Matrix4x4[count],Attachments=new Matrix4x4[count],PhysicalParent=new int[count],Velocities=new Vector3[count],Spins=new Vector3[count]};
        var initial=new Matrix4x4[count];int firstPhysical=Array.FindIndex(rig.bones,b=>b.physical);
        if(firstPhysical<0)firstPhysical=0;
        double mass=Math.Max(.1,corpse.MassKg);double total=rig.bones.Where(b=>b.physical).Sum(b=>Math.Max(1,b.vertices));
        for(int i=0;i<count;i++)
        {
            var bone=rig.bones[i];var bind=RigMatrix(bone.bind);rag.InverseBind[i]=bind.inverse;
            var posed=animalBoneData[animal.Slot*AnimalBones+i]*bind;initial[i]=posed;
            var root=new GameObject("Ragdoll "+bone.name);root.layer=30;root.transform.SetParent(animal.Root.transform,false);
            root.transform.localPosition=posed.GetColumn(3);root.transform.localRotation=posed.rotation;rag.Bones[i]=root.transform;
            if(!bone.physical&&i!=firstPhysical)continue;
            var collider=root.AddComponent<CapsuleCollider>();collider.direction=1;collider.center=new Vector3(0,bone.length*.5f,0);
            collider.radius=Math.Max(.006f,bone.radius);collider.height=Math.Max(collider.radius*2,bone.length);
            var body=root.AddComponent<Rigidbody>();body.mass=(float)Math.Max(.03,mass*Math.Max(1,bone.vertices)/Math.Max(1,total));
            body.linearDamping=.25f;body.angularDamping=1.2f;body.solverIterations=8;body.solverVelocityIterations=3;
            body.maxDepenetrationVelocity=2;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity=Quaternion.Euler(0,animal.Heading,0)*Vector3.forward*animal.Speed;
            body.AddForce(impulse.normalized*Math.Min(1.5f,(float)(8/Math.Sqrt(mass))),ForceMode.VelocityChange);
            rag.Bodies[i]=body;
        }
        for(int i=0;i<count;i++)
        {
            int parent=rig.bones[i].parent;while(parent>=0&&rag.Bodies[parent]==null)parent=rig.bones[parent].parent;
            if(parent<0&&i!=firstPhysical)parent=firstPhysical;rag.PhysicalParent[i]=parent;
            if(rag.Bodies[i]==null){rag.Attachments[i]=parent>=0?initial[parent].inverse*initial[i]:initial[i];continue;}
            if(parent<0||parent==i)continue;
            var joint=rag.Bones[i].gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=rag.Bodies[parent];
            joint.autoConfigureConnectedAnchor=true;joint.anchor=Vector3.zero;joint.axis=Vector3.right;joint.secondaryAxis=Vector3.up;
            joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
            string name=rig.bones[i].name.ToLowerInvariant();bool spine=name.Contains("spine")||name.Contains("body")||name.Contains("chest");
            float bend=spine?16:55;
            joint.lowAngularXLimit=new SoftJointLimit{limit=-bend};joint.highAngularXLimit=new SoftJointLimit{limit=bend};
            joint.angularYLimit=new SoftJointLimit{limit=spine?12:35};joint.angularZLimit=new SoftJointLimit{limit=spine?12:45};
            joint.enableCollision=false;joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.08f;joint.projectionAngle=15;
        }
        // Self collision between overlapping fitted skin capsules is unstable;
        // ground/objects remain collidable. Articulated joints retain anatomy.
        var colliders=rag.Bones.Select(b=>b.GetComponent<Collider>()).Where(c=>c!=null).ToArray();
        for(int i=0;i<colliders.Length;i++)for(int j=i+1;j<colliders.Length;j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
        rag.Ground=CreateRagdollGround(animal);animalRagdolls[corpse.SpawnId]=rag;
        animal.TargetSpeed=animal.Speed=0;animal.Props.SetFloat("_Glow",0);animal.Props.SetFloat("_Fade",1);animal.Renderer.SetPropertyBlock(animal.Props);
    }
    GameObject CreateRagdollGround(Animal animal)
    {
        var root=new GameObject("Local corpse ground");root.layer=30;root.transform.SetParent(transform,false);
        root.transform.localPosition=new Vector3((float)(animal.X-originX),0,(float)(animal.Z-originZ));
        const int grid=12;float radius=Math.Max(5,(float)animal.Spawn.Profile.lengthM*2.5f);var points=new Vector3[(grid+1)*(grid+1)];var indices=new int[grid*grid*6];
        for(int z=0;z<=grid;z++)for(int x=0;x<=grid;x++){float xx=-radius+2*radius*x/grid,zz=-radius+2*radius*z/grid;points[z*(grid+1)+x]=new Vector3(xx,(float)VisibleGround(animal.X+xx,animal.Z+zz,animal.GroundY),zz);}
        int at=0;for(int z=0;z<grid;z++)for(int x=0;x<grid;x++){int a=z*(grid+1)+x,b=a+1,c=a+grid+1,d=c+1;indices[at++]=a;indices[at++]=c;indices[at++]=b;indices[at++]=b;indices[at++]=c;indices[at++]=d;}
        var mesh=new Mesh{name="Corpse contact ground"};mesh.vertices=points;mesh.triangles=indices;mesh.RecalculateBounds();root.AddComponent<MeshCollider>().sharedMesh=mesh;return root;
    }
    void UpdateDeadAnimal(Animal animal)
    {
        if(!animalRagdolls.TryGetValue(animal.Spawn.Id,out var rag))return;
        bool awake=rag.Bodies.Any(b=>b!=null&&!b.IsSleeping()&&!b.isKinematic);if(!awake&&rag.QuietFrames++>3)return;rag.QuietFrames=0;
        for(int i=0;i<rag.Bones.Length;i++)
        {
            if(rag.Bodies[i]!=null)continue;int parent=rag.PhysicalParent[i];var pose=parent<0?animal.Root.transform.localToWorldMatrix*rag.Attachments[i]:rag.Bones[parent].localToWorldMatrix*rag.Attachments[i];
            rag.Bones[i].SetPositionAndRotation(pose.GetColumn(3),pose.rotation);
        }
        for(int i=0;i<rag.Bones.Length;i++)animalBoneData[animal.Slot*AnimalBones+i]=animal.Root.transform.worldToLocalMatrix*rag.Bones[i].localToWorldMatrix*rag.InverseBind[i];
    }
    void UpdateRagdolls(float dt)
    {
        bool pausedPhysics=dt==0;
        foreach(var rag in animalRagdolls.Values)
        {
            if(rag.Paused==pausedPhysics)continue;rag.Paused=pausedPhysics;
            for(int i=0;i<rag.Bodies.Length;i++){var b=rag.Bodies[i];if(b==null)continue;if(pausedPhysics){rag.Velocities[i]=b.linearVelocity;rag.Spins[i]=b.angularVelocity;b.isKinematic=true;}else{b.isKinematic=false;b.linearVelocity=rag.Velocities[i];b.angularVelocity=rag.Spins[i];}}
        }
        for(int i=survivalProjectiles.Count-1;i>=0;i--)
        {
            var p=survivalProjectiles[i];if(dt<=0)continue;p.Life+=dt;Vector3 old=p.Root.transform.localPosition,next=old+p.Velocity*dt+Vector3.down*(4.905f*dt*dt);p.Velocity+=Vector3.down*(9.81f*dt);
            Vector3 diff=next-old;float length=diff.magnitude;Animal hit=null;float nearest=length;
            foreach(var a in animals.Values){if(a.Dead)continue;var b=a.Renderer.bounds;if(b.IntersectRay(new Ray(transform.TransformPoint(old),transform.TransformDirection(diff.normalized)),out float t)&&t<=nearest){nearest=t;hit=a;}}
            bool ground=next.y<=VisibleGround(next.x+originX,next.z+originZ,next.y)+.05;
            if(hit!=null||ground||p.Life>8)
            {
                if(hit!=null)DamageSurvivalAnimal(hit,p.Damage,p.Velocity.normalized);
                V position=hit!=null?hit.Planet:frame.Position(next.x+originX,next.z+originZ);
                survival.Sites=survival.Sites.Concat(new[]{new SurvivalSite{Id=survival.NextSite++,Kind="drop",Position=position,Contents=new[]{new SurvivalStack{Id=1,Item=p.Item}}}}).ToArray();
                Destroy(p.Root);survivalProjectiles.RemoveAt(i);continue;
            }
            p.Root.transform.localPosition=next;p.Root.transform.rotation=Quaternion.LookRotation(p.Velocity.normalized)*Quaternion.Euler(90,0,0);
        }
    }
    void SpawnSurvivalProjectile(string item,Vector3 origin,Vector3 direction,Animal candidate,float distance,double damage)
    {
        var root=CreateSurvivalTool(item);root.transform.SetParent(transform,false);root.transform.position=origin+direction*.4f;
        survivalProjectiles.Add(new SurvivalProjectile{Item=item,Root=root,Velocity=transform.InverseTransformDirection(direction)*22,Damage=damage});
    }
    void RemoveAnimalRagdoll(ulong id)
    {
        if(!animalRagdolls.TryGetValue(id,out var rag))return;
        if(rag.Ground!=null){var mesh=rag.Ground.GetComponent<MeshCollider>()?.sharedMesh;if(mesh!=null)Destroy(mesh);Destroy(rag.Ground);}
        animalRagdolls.Remove(id);
    }
    void DisposeRagdolls(){foreach(var id in animalRagdolls.Keys.ToArray())RemoveAnimalRagdoll(id);foreach(var p in survivalProjectiles)if(p.Root!=null)Destroy(p.Root);survivalProjectiles.Clear();}
}

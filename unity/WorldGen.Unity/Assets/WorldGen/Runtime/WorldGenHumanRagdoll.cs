using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Unit-scale physics proxies drive the original, potentially scaled FBX bones.</summary>
public sealed class WorldGenHumanRagdoll : MonoBehaviour
{
    sealed class Part
    {public Transform Bone;public Rigidbody Body;public Quaternion Rotation;public Vector3 Start,Velocity,Spin;}
    readonly List<Part> parts=new List<Part>();
    GameObject physicsRoot;Mesh groundMesh;float age,quietTime;bool settled,paused;
    public int BodyCount=>parts.Count;
    public bool Settled=>settled;
    public Vector3 Center=>parts.Count>0?parts[0].Body.position:transform.position;
    public void Begin(GameObject model,Func<float,float,float> ground,Vector3 velocity)
    {
        physicsRoot=new GameObject("Death physics");physicsRoot.transform.SetParent(transform,false);
        var bones=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        var connected=new Dictionary<string,Part>();
        float scale=Mathf.Clamp((bones["head"].position-bones["pelvis"].position).magnitude/.7f,.65f,1.4f);
        Part Add(string name,Vector3 end,string parent,float radius,float mass,float swing=35)
        {
            var bone=bones[name];var start=bone.position;var direction=end-start;
            if(direction.magnitude<.025f)direction=Vector3.up*.05f;
            var obj=new GameObject(name+" physics");obj.transform.SetParent(physicsRoot.transform,false);
            obj.transform.position=(start+end)*.5f;obj.transform.rotation=Quaternion.FromToRotation(Vector3.up,direction.normalized);
            var collider=obj.AddComponent<CapsuleCollider>();collider.direction=1;collider.radius=radius*scale;collider.height=Mathf.Max(direction.magnitude,collider.radius*2);
            var body=obj.AddComponent<Rigidbody>();body.mass=mass;body.linearDamping=.35f;body.angularDamping=1;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations=12;body.solverVelocityIterations=4;body.maxDepenetrationVelocity=2;
            var part=new Part{Bone=bone,Body=body,Rotation=Quaternion.Inverse(body.rotation)*bone.rotation,Start=obj.transform.InverseTransformPoint(start)};
            if(parent!=null)
            {
                var joint=obj.AddComponent<CharacterJoint>();joint.connectedBody=connected[parent].Body;
                joint.autoConfigureConnectedAnchor=false;joint.anchor=obj.transform.InverseTransformPoint(start);
                joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(start);
                joint.axis=Vector3.up;joint.swingAxis=Vector3.right;
                joint.lowTwistLimit=new SoftJointLimit{limit=-20};joint.highTwistLimit=new SoftJointLimit{limit=20};
                joint.swing1Limit=new SoftJointLimit{limit=swing};joint.swing2Limit=new SoftJointLimit{limit=swing};
                joint.enableProjection=true;joint.projectionDistance=.025f;joint.projectionAngle=10;
            }
            parts.Add(part);connected.Add(name,part);body.linearVelocity=Vector3.ClampMagnitude(velocity,15);return part;
        }
        Add("pelvis",bones["spine_02"].position,null,.12f,12);
        Add("spine_02",bones["neck_01"].position,"pelvis",.14f,18,25);
        var head=bones["head"];Add("head",head.position+(head.position-bones["neck_01"].position).normalized*.15f*scale,"spine_02",.09f,5,35);
        foreach(string side in new[]{"l","r"})
        {
            string upper="upperarm_"+side,lower="lowerarm_"+side,hand="hand_"+side;
            Add(upper,bones[lower].position,"spine_02",.047f,3,75);
            Add(lower,bones[hand].position,upper,.037f,2,70);
            string thigh="thigh_"+side,calf="calf_"+side,foot="foot_"+side;
            Add(thigh,bones[calf].position,"pelvis",.065f,7,65);
            Add(calf,bones[foot].position,thigh,.045f,3.5f,65);
            var toe=bones.TryGetValue("ball_"+side,out var ball)?ball.position:bones[foot].position+model.transform.forward*.16f;
            Add(foot,toe,calf,.045f,1,30);
        }
        // Proxies overlap slightly at shoulders/hips by design. They interact
        // with terrain, not one another; joint limits prevent solver explosions.
        var colliders=physicsRoot.GetComponentsInChildren<Collider>();
        for(int i=0;i<colliders.Length;i++)for(int j=0;j<i;j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
        BuildGround(ground,model.transform.position);
        // A small off-centre impulse lets an upright starvation/cold death
        // collapse naturally rather than remaining balanced on straight legs.
        parts[1].Body.AddForceAtPosition((model.transform.forward*.9f+model.transform.right*.3f)*2,
            parts[1].Body.worldCenterOfMass+Vector3.up*.2f,ForceMode.Impulse);
        Physics.SyncTransforms();
    }
    void BuildGround(Func<float,float,float> height,Vector3 center)
    {
        const int segments=40;const float span=24;
        var vertices=new Vector3[(segments+1)*(segments+1)];var indices=new int[segments*segments*6];int p=0;
        for(int z=0;z<=segments;z++)for(int x=0;x<=segments;x++)
        {float px=center.x-span/2+x*span/segments,pz=center.z-span/2+z*span/segments;vertices[z*(segments+1)+x]=new Vector3(px,height(px,pz),pz);}
        for(int z=0;z<segments;z++)for(int x=0;x<segments;x++)
        {int a=z*(segments+1)+x,b=a+1,c=a+segments+1,d=c+1;indices[p++]=a;indices[p++]=c;indices[p++]=b;indices[p++]=b;indices[p++]=c;indices[p++]=d;}
        groundMesh=new Mesh{name="Local death terrain collision"};groundMesh.vertices=vertices;groundMesh.triangles=indices;groundMesh.RecalculateBounds();
        var floor=new GameObject("Local death terrain");floor.transform.SetParent(physicsRoot.transform,false);floor.AddComponent<MeshCollider>().sharedMesh=groundMesh;
    }
    void LateUpdate(){Synchronize();}
    public void Synchronize()
    {
        foreach(var p in parts)if(p.Bone!=null&&p.Body!=null)
            p.Bone.SetPositionAndRotation(p.Body.transform.TransformPoint(p.Start),p.Body.rotation*p.Rotation);
    }
    public void SetPaused(bool value)
    {if(settled||paused==value)return;paused=value;foreach(var p in parts){if(value){p.Velocity=p.Body.linearVelocity;p.Spin=p.Body.angularVelocity;p.Body.isKinematic=true;}else{p.Body.isKinematic=false;p.Body.linearVelocity=p.Velocity;p.Body.angularVelocity=p.Spin;}}}
    void FixedUpdate()
    {
        if(paused)return;age+=Time.fixedDeltaTime;
        // Retain a stable corpse with no continuing physics cost after settling.
        bool quiet=parts.All(p=>p.Body.IsSleeping()||p.Body.linearVelocity.sqrMagnitude<.01f&&p.Body.angularVelocity.sqrMagnitude<.04f);
        quietTime=quiet?quietTime+Time.fixedDeltaTime:0;
        if(!settled&&age>3&&quietTime>1)
        {settled=true;foreach(var p in parts){p.Body.linearVelocity=Vector3.zero;p.Body.angularVelocity=Vector3.zero;p.Body.isKinematic=true;}}
    }
    void OnDestroy()
    {
        if(physicsRoot!=null){physicsRoot.SetActive(false);if(Application.isPlaying)Destroy(physicsRoot);else DestroyImmediate(physicsRoot);}
        if(groundMesh!=null){if(Application.isPlaying)Destroy(groundMesh);else DestroyImmediate(groundMesh);}
    }
}

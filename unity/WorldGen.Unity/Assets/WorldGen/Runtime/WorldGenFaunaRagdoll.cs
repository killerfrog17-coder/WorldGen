using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class FaunaPhysicsBone
{public string name;public int parent;public float[] head,tail,rest;}
[Serializable] public sealed class FaunaPhysicsSpecies
{public string slug,source;public FaunaPhysicsBone[] bones;}
[Serializable] public sealed class FaunaPhysicsCatalogue
{public FaunaPhysicsSpecies[] species;}

/// <summary>Articulates the original exported skin palette without changing its
/// rig, weights, surface or clips. Unit-scale physics bodies drive palette deltas.</summary>
public sealed class WorldGenFaunaRagdoll : MonoBehaviour
{
    sealed class Part
    {public int Bone;public Rigidbody Body;public Matrix4x4 InverseStart;public Vector3 Velocity,Spin;}
    readonly List<Part> parts=new List<Part>();int[] owners;Matrix4x4[] initial;
    Transform model;GameObject physicsRoot,floor;Mesh groundMesh;Vector3 lastModelPosition;float age,quiet;bool settled,paused,lightPhysics;
    Func<float,float,float> groundQuery;Vector3 floorCenter;float floorSpan;
    public int BodyCount=>parts.Count;public bool Settled=>settled;
    public bool Connected=>parts.Skip(1).All(p=>p.Body.GetComponent<CharacterJoint>()?.connectedBody!=null);
    static Vector3 Point(float[] p)=>new Vector3(p[0],p[1],p[2]);
    public void Begin(Transform renderedModel,FaunaPhysicsSpecies skeleton,Matrix4x4[] palette,double mass,Vector3 velocity,Func<float,float,float> ground,bool freeze=false,FaunaCollisionSurface skin=null,Vector3[] posed=null,Vector4 visibleParts=default,bool coarse=false)
    {
        model=renderedModel;lightPhysics=coarse||mass<5;lastModelPosition=model.position;initial=(Matrix4x4[])palette.Clone();owners=new int[palette.Length];
        physicsRoot=new GameObject("Wildlife corpse physics");physicsRoot.transform.SetParent(transform,false);
        var map=new Dictionary<int,int>();float scale=model.lossyScale.x;
        var descriptions=skeleton?.bones;int count=descriptions?.Length??0;
        var clouds=new List<Vector3>[count];
        if(skin!=null&&posed?.Length==skin.Vertices.Length)
        {
            for(int vertex=0;vertex<posed.Length;vertex++)
            {
                if(!skin.Visible(vertex,visibleParts))continue;int owner=0;float strongest=0;
                for(int j=0;j<4;j++){if(skin.Weights[vertex][j]>strongest){strongest=skin.Weights[vertex][j];owner=(int)skin.Joints[vertex][j];}if(skin.ExtraWeights[vertex][j]>strongest){strongest=skin.ExtraWeights[vertex][j];owner=(int)skin.ExtraJoints[vertex][j];}}
                if(strongest<.25f||owner<0||owner>=count)continue;if(clouds[owner]==null)clouds[owner]=new List<Vector3>();clouds[owner].Add(posed[vertex]);
            }
        }
        var headBones=new HashSet<int>();for(int i=0;i<count;i++)if(descriptions[i].parent>=0&&descriptions[i].name.IndexOf("head",StringComparison.OrdinalIgnoreCase)>=0)headBones.Add(i);
        var headChain=new HashSet<int>(headBones);foreach(int head in headBones){int parent=descriptions[head].parent,guard=0;while(parent>=0&&parent<count&&guard++<count){headChain.Add(parent);parent=descriptions[parent].parent;}}
        for(int i=0;i<count;i++)
        {
            var b=descriptions[i];float span=Vector3.Distance(Point(b.head),Point(b.tail))*scale;string name=b.name.ToLowerInvariant();
            bool detail=name.Contains("eye")||name.Contains("ear")||name.Contains("jaw")||name.Contains("finger")||name.Contains("toe")||name.Contains("antler")||name.Contains("horn");
            int ancestor=b.parent,guard=0;while(ancestor>=0&&ancestor<count&&guard++<count){if(headBones.Contains(ancestor)){detail=true;break;}ancestor=descriptions[ancestor].parent;}
            // A short common root still connects independent limb branches.
            // Dropping it creates free rigidbodies that tear the skin apart.
            bool root=b.parent<0;
            if(!root&&(span<Math.Max(.008f,scale*.025f)||detail))continue;
            if(lightPhysics&&!root&&descriptions[b.parent].parent>=0&&!headChain.Contains(i))
            {
                // Small corpses use physical branches, not a separate rigidbody
                // for every rear spine segment, toe and feather. The structural
                // chain to the head stays articulated for long-necked birds.
                // Fine original bones
                // follow the nearest retained body through the owner palette.
                float midline=Point(descriptions[0].head).x;
                bool Central(FaunaPhysicsBone bone)=>Mathf.Abs(Point(bone.head).x-midline)<.025f&&Mathf.Abs(Point(bone.tail).x-midline)<.025f;
                if(Central(b)||!Central(descriptions[b.parent]))continue;
            }
            Vector3 start=model.TransformPoint(palette[i].MultiplyPoint3x4(Point(b.head))),end=model.TransformPoint(palette[i].MultiplyPoint3x4(Point(b.tail)));
            if(Vector3.Distance(start,end)<.006f){if(!root)continue;end=start+model.up*Mathf.Max(.001f,scale*.01f);}
            bool limb=name.Contains("leg")||name.Contains("arm")||name.Contains("foot")||name.Contains("paw")||name.Contains("shin")||name.Contains("fore")||name.Contains("wing")||name.Contains("tail");
            float radius=Mathf.Clamp(span*(limb?.17f:.35f),scale*(limb?.012f:.025f),scale*(limb?.045f:.09f));
            if(clouds[i]?.Count>=6)
            {
                var axis=end-start;float length2=axis.sqrMagnitude;var radii=new List<float>();
                foreach(var point in clouds[i]){float t=Vector3.Dot(point-start,axis)/length2;if(t<.1f||t>.9f)continue;radii.Add(Vector3.Distance(point,start+axis*t));}
                if(radii.Count>=4){radii.Sort();radius=Mathf.Clamp(radii[(int)((radii.Count-1)*.65f)]*.8f,Mathf.Max(.0015f,scale*.003f),scale*.18f);}
            }
            // Geometric classification is essential for rigs named Joint_01,
            // Joint_02... Names alone cannot distinguish their torso and legs.
            float centerX=Point(descriptions[0].head).x;
            bool central=Mathf.Abs(Point(b.head).x-centerX)<.025f&&Mathf.Abs(Point(b.tail).x-centerX)<.025f;
            bool limbBearing=false;for(int child=0;child<count;child++)if(descriptions[child].parent==i&&(Mathf.Abs(Point(descriptions[child].head).x-centerX)>.025f||Mathf.Abs(Point(descriptions[child].tail).x-centerX)>.025f)){limbBearing=true;break;}
            bool spine=central&&(limbBearing||Mathf.Abs(Point(b.tail).y-Point(b.head).y)<Mathf.Abs(Point(b.tail).z-Point(b.head).z)*.4f);
            var restAxis=Point(b.tail)-Point(b.head);bool leg=!central&&Mathf.Abs(restAxis.y)>Mathf.Abs(restAxis.x)*.6f&&(Point(b.head).y+Point(b.tail).y)*.5f<Point(descriptions[0].head).y+.04f;
            bool tail=central&&headBones.Count>0&&!headChain.Contains(i);
            Add(i,start,end,radius,Math.Max(.00001,mass),map,descriptions,velocity,spine&&!tail,leg,tail);
        }
        if(parts.Count==0)
        {
            Vector3 center=model.position+Vector3.up*scale*.18f;
            Add(0,center-model.forward*scale*.2f,center+model.forward*scale*.2f,Mathf.Max(.004f,scale*.12f),mass,map,null,velocity);
        }
        for(int i=0;i<owners.Length;i++)
        {
            int bone=i,guard=0;while(!map.ContainsKey(bone)&&descriptions!=null&&bone>=0&&bone<descriptions.Length&&guard++<descriptions.Length)bone=descriptions[bone].parent;
            owners[i]=map.TryGetValue(bone,out int part)?part:0;
        }
        double total=parts.Sum(p=>{var c=p.Body.GetComponent<CapsuleCollider>();return c.radius*c.radius*c.height;});
        // Tiny terminal capsules otherwise have thousands of times less mass
        // than the torso. That makes the constraint solver pull their skin into
        // spikes even though every matrix remains finite. Condition the solver
        // masses without changing the accepted skin or the total animal mass.
        foreach(var p in parts){var c=p.Body.GetComponent<CapsuleCollider>();p.Body.mass=(float)Math.Max(lightPhysics?mass*.002:.00001,mass*c.radius*c.radius*c.height/Math.Max(1e-12,total));}
        if(lightPhysics){float bodyMass=parts.Sum(p=>p.Body.mass);foreach(var p in parts)p.Body.mass*=(float)mass/bodyMass;}
        if(lightPhysics)foreach(var p in parts){var joint=p.Body.GetComponent<CharacterJoint>();if(joint!=null)joint.connectedMassScale=Mathf.Clamp(joint.connectedBody.mass/p.Body.mass,.05f,20);}
        var colliders=physicsRoot.GetComponentsInChildren<Collider>();
        for(int i=0;i<colliders.Length;i++)for(int j=0;j<i;j++)
        {
            var a=colliders[i];var b=colliders[j];var joint=a.GetComponent<CharacterJoint>();var other=b.GetComponent<CharacterJoint>();
            bool connected=joint!=null&&joint.connectedBody==b.attachedRigidbody||other!=null&&other.connectedBody==a.attachedRigidbody;
            if(connected||Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out _))Physics.IgnoreCollision(a,b);
        }
        BuildGround(ground,model.position,Mathf.Max(8,scale*3));Physics.SyncTransforms();
        if(freeze)Freeze();else if(parts.Count>0)parts[0].Body.AddForceAtPosition(model.right*(float)Math.Min(2,mass*.05),parts[0].Body.worldCenterOfMass+Vector3.up*.08f,ForceMode.Impulse);
    }
    void Add(int bone,Vector3 start,Vector3 end,float radius,double mass,Dictionary<int,int> map,FaunaPhysicsBone[] descriptions,Vector3 velocity,bool spine=false,bool leg=false,bool tail=false)
    {
        var obj=new GameObject("Corpse bone "+bone);obj.transform.SetParent(physicsRoot.transform,false);obj.transform.position=(start+end)*.5f;
        var axis=(end-start).normalized;var forward=Vector3.ProjectOnPlane(model.forward,axis);if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(model.up,axis);
        obj.transform.rotation=Quaternion.LookRotation(forward.normalized,axis);
        var capsule=obj.AddComponent<CapsuleCollider>();capsule.direction=1;capsule.radius=radius;capsule.height=Mathf.Max(radius*2,Vector3.Distance(start,end));
        var body=obj.AddComponent<Rigidbody>();body.mass=(float)Math.Max(.00001,mass/10);body.linearDamping=.4f;body.angularDamping=1.3f;body.solverIterations=10;body.solverVelocityIterations=4;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.maxDepenetrationVelocity=2;body.interpolation=RigidbodyInterpolation.Interpolate;body.linearVelocity=Vector3.ClampMagnitude(velocity,15);
        int parent=descriptions==null?-1:descriptions[bone].parent,guard=0;
        while(parent>=0&&!map.ContainsKey(parent)&&guard++<descriptions.Length)parent=descriptions[parent].parent;
        if(map.TryGetValue(parent,out int connected))
        {
            float centerX=descriptions==null?0:Point(descriptions[0].head).x;
            bool proximal=leg&&descriptions!=null&&Mathf.Abs(Point(descriptions[parent].head).x-centerX)<.025f&&Mathf.Abs(Point(descriptions[parent].tail).x-centerX)<.025f;
            var joint=obj.AddComponent<CharacterJoint>();joint.connectedBody=parts[connected].Body;joint.autoConfigureConnectedAnchor=false;
            joint.anchor=obj.transform.InverseTransformPoint(start);joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(start);joint.axis=Vector3.up;joint.swingAxis=Vector3.right;
            joint.lowTwistLimit=new SoftJointLimit{limit=spine||leg||tail?-6:-12};joint.highTwistLimit=new SoftJointLimit{limit=spine||leg||tail?6:12};joint.swing1Limit=new SoftJointLimit{limit=spine?10:tail?25:leg?(proximal?45:85):35};joint.swing2Limit=new SoftJointLimit{limit=spine?8:tail?15:leg?10:25};
            joint.enableProjection=true;joint.projectionDistance=lightPhysics?Mathf.Clamp(Vector3.Distance(start,end)*.02f,.00025f,.005f):.025f;joint.projectionAngle=lightPhysics?5:10;
        }
        map[bone]=parts.Count;parts.Add(new Part{Bone=bone,Body=body,InverseStart=(model.worldToLocalMatrix*body.transform.localToWorldMatrix).inverse});
    }
    void BuildGround(Func<float,float,float> height,Vector3 center,float span)
    {
        groundQuery=height;floorCenter=center;floorSpan=span;
        if(floor!=null){floor.GetComponent<MeshCollider>().sharedMesh=null;floor.transform.position=Vector3.zero;}
        const int segments=20;var vertices=new Vector3[441];var triangles=new int[2400];int cursor=0;
        for(int z=0;z<=segments;z++)for(int x=0;x<=segments;x++){float px=center.x-span*.5f+x*span/segments,pz=center.z-span*.5f+z*span/segments;vertices[z*21+x]=new Vector3(px,height(px,pz),pz);}
        for(int z=0;z<segments;z++)for(int x=0;x<segments;x++){int a=z*21+x,b=a+1,c=a+21,d=c+1;triangles[cursor++]=a;triangles[cursor++]=c;triangles[cursor++]=b;triangles[cursor++]=b;triangles[cursor++]=c;triangles[cursor++]=d;}
        if(groundMesh==null)groundMesh=new Mesh{name="Wildlife corpse ground"};groundMesh.Clear();groundMesh.vertices=vertices;groundMesh.triangles=triangles;groundMesh.RecalculateBounds();
        if(floor==null){floor=new GameObject("Wildlife corpse ground");floor.transform.SetParent(physicsRoot.transform,false);floor.AddComponent<MeshCollider>();}floor.GetComponent<MeshCollider>().sharedMesh=groundMesh;
    }
    public void Synchronize(Matrix4x4[] target,int offset)
    {
        if(model==null||initial==null)return;Vector3 delta=model.position-lastModelPosition;
        if(delta.sqrMagnitude>.000001f){foreach(var p in parts)p.Body.position+=delta;if(floor!=null)floor.transform.position+=delta;floorCenter+=delta;lastModelPosition=model.position;}
        for(int i=0;i<initial.Length;i++){var part=parts[owners[i]];target[offset+i]=model.worldToLocalMatrix*part.Body.transform.localToWorldMatrix*part.InverseStart*initial[i];}
    }
    public Bounds LocalBounds
    {
        get {var bounds=new Bounds(model.InverseTransformPoint(parts[0].Body.position),Vector3.zero);foreach(var p in parts){var box=p.Body.GetComponent<Collider>().bounds;for(int i=0;i<8;i++)bounds.Encapsulate(model.InverseTransformPoint(box.center+Vector3.Scale(box.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));}return bounds;}
    }
    public void SetPaused(bool paused)
    {if(settled||this.paused==paused)return;this.paused=paused;foreach(var p in parts){if(paused){p.Velocity=p.Body.linearVelocity;p.Spin=p.Body.angularVelocity;p.Body.isKinematic=true;}else{p.Body.isKinematic=false;p.Body.linearVelocity=p.Velocity;p.Body.angularVelocity=p.Spin;}}}
    void Freeze(){settled=true;foreach(var p in parts){p.Body.linearVelocity=Vector3.zero;p.Body.angularVelocity=Vector3.zero;p.Body.isKinematic=true;}}
    void FixedUpdate()
    {
        if(settled||paused)return;age+=Time.fixedDeltaTime;bool still=parts.All(p=>p.Body.IsSleeping()||p.Body.linearVelocity.sqrMagnitude<.01f&&p.Body.angularVelocity.sqrMagnitude<.05f);quiet=still?quiet+Time.fixedDeltaTime:0;
        if(parts.Count>0&&groundQuery!=null){var offset=parts[0].Body.position-floorCenter;offset.y=0;if(offset.sqrMagnitude>floorSpan*floorSpan*.0625f)BuildGround(groundQuery,parts[0].Body.position,floorSpan);}
        if(age>2&&quiet>1)Freeze();
    }
    void OnDestroy(){if(physicsRoot!=null){physicsRoot.SetActive(false);Destroy(physicsRoot);}if(groundMesh!=null)Destroy(groundMesh);}
}

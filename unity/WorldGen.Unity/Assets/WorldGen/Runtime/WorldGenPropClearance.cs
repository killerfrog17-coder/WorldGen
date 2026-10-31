using UnityEngine;

/// <summary>Cheap geometric clearance for held rigid props. No physics impulses,
/// colliders, mesh edits or per-frame allocations are required.</summary>
static class WorldGenPropClearance
{
    public sealed class BodyShape
    {
        public float Torso,Hips,Head,HeadHalfWidth;
        public Vector3 BowAnchorInHead;public bool HasBowAnchor;
        public Vector3 EyeInHead,MouthInHead;public bool HasNativeEye;
    }
    public static BodyShape Measure(Transform root,float eyeHeight,float eyeForward)
    {
        float scale=eyeHeight/1.65f;var result=new BodyShape{Torso=.20f*scale,Hips=.18f*scale,Head=.105f*scale,HeadHalfWidth=.08f*scale};
        // Called once in the evaluated native idle pose, before camera aiming.
        // Weight labels select the actual torso/head surface on this body.
        var bones=root.GetComponentsInChildren<Transform>();Transform pelvis=null,lower=null,upper=null,head=null,neck=null;
        foreach(var bone in bones){if(bone.name=="pelvis")pelvis=bone;else if(bone.name=="spine_01")lower=bone;else if(bone.name=="spine_03")upper=bone;else if(bone.name=="head")head=bone;else if(bone.name=="neck_01")neck=bone;}
        if(pelvis==null||lower==null||upper==null||head==null)return result;
        var mesh=new Mesh();var up=root.up;var headSurface=new System.Collections.Generic.List<Vector3>();
        Vector3 eyeSum=Vector3.zero;int eyeCount=0;
        try
        {
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var original=renderer.sharedMesh;if(original==null)continue;
                var weights=original.boneWeights;var bind=renderer.bones;if(weights.Length==0)continue;
                var flags=new byte[bind.Length];
                for(int i=0;i<bind.Length;i++)
                {
                    string name=bind[i].name;
                    flags[i]=(byte)(name.StartsWith("spine_",System.StringComparison.Ordinal)?1:name=="pelvis"||name.StartsWith("thigh_",System.StringComparison.Ordinal)?2:name=="head"?4:0);
                }
                renderer.BakeMesh(mesh,true);var vertices=mesh.vertices;
                bool eyeMesh=renderer.name.IndexOf("high-poly",System.StringComparison.OrdinalIgnoreCase)>=0||original.name.IndexOf("high-poly",System.StringComparison.OrdinalIgnoreCase)>=0;
                for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    var w=weights[i];float torso=0,hips=0,skull=0;
                    void Add(int index,float amount){if(index<0||index>=flags.Length)return;byte flag=flags[index];if(flag==1)torso+=amount;else if(flag==2)hips+=amount;else if(flag==4)skull+=amount;}
                    Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                    var point=renderer.transform.TransformPoint(vertices[i]);
                    if(eyeMesh){eyeSum+=point;eyeCount++;}
                    if(skull>.55f&&original.vertexCount>10000)headSurface.Add(root.InverseTransformPoint(point));
                    if(torso>.55f&&original.vertexCount>10000)result.Torso=Mathf.Max(result.Torso,Vector3.ProjectOnPlane(point-lower.position,((neck??upper).position-lower.position).normalized).magnitude);
                    if(hips>.55f&&Mathf.Abs(Vector3.Dot(point-pelvis.position,up))<.13f*scale)result.Hips=Mathf.Max(result.Hips,Vector3.ProjectOnPlane(point-pelvis.position,up).magnitude);
                    if(skull>.55f){result.Head=Mathf.Max(result.Head,Vector3.ProjectOnPlane(point-head.position,up).magnitude);result.HeadHalfWidth=Mathf.Max(result.HeadHalfWidth,Mathf.Abs(Vector3.Dot(point-head.position,root.right)));}
                }
            }
        }
        finally{if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
        result.Torso=Mathf.Min(result.Torso,.32f*scale);result.Hips=Mathf.Min(result.Hips,.28f*scale);result.Head=Mathf.Min(result.Head,Mathf.Max(.18f,.18f*scale));result.HeadHalfWidth=Mathf.Min(result.HeadHalfWidth,Mathf.Max(.13f,.13f*scale));
        Vector3 eye=eyeCount>0?root.InverseTransformPoint(eyeSum/eyeCount):new Vector3(0,eyeHeight,eyeForward-.025f);
        if(eyeCount>0){result.EyeInHead=head.InverseTransformPoint(root.TransformPoint(eye+Vector3.forward*.025f));result.HasNativeEye=true;}
        float mouthY=eye.y-.07f*scale,cornerX=eye.x+.045f*scale,mouthFront=float.NegativeInfinity;
        foreach(var local in headSurface)
            if(Mathf.Abs(local.y-mouthY)<.02f*scale&&Mathf.Abs(local.x-cornerX)<.018f*scale)mouthFront=Mathf.Max(mouthFront,local.z);
        if(!float.IsNegativeInfinity(mouthFront))
        {
            // A real surface sample at the mouth corner, not the outside of a
            // generic skull sphere. Fingers contact just in front of this skin.
            result.BowAnchorInHead=head.InverseTransformPoint(root.TransformPoint(new Vector3(cornerX,mouthY,mouthFront+.008f*scale)));result.HasBowAnchor=true;
        }
        float lipFront=float.NegativeInfinity;
        foreach(var local in headSurface)
            if(Mathf.Abs(local.y-mouthY)<.018f*scale&&Mathf.Abs(local.x-eye.x)<.018f*scale)lipFront=Mathf.Max(lipFront,local.z);
        result.MouthInHead=head.InverseTransformPoint(root.TransformPoint(new Vector3(eye.x,mouthY,float.IsNegativeInfinity(lipFront)?eye.z:lipFront+.008f*scale)));
        return result;
    }
    public static float Segments(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        var u=b-a;var v=d-c;var w=a-c;
        float aa=Vector3.Dot(u,u),bb=Vector3.Dot(u,v),cc=Vector3.Dot(v,v),dd=Vector3.Dot(u,w),ee=Vector3.Dot(v,w);
        float s,t;
        if(aa<1e-10f&&cc<1e-10f)return w.magnitude;
        if(aa<1e-10f){s=0;t=Mathf.Clamp01(ee/cc);}
        else if(cc<1e-10f){t=0;s=Mathf.Clamp01(-dd/aa);}
        else
        {
            float denominator=aa*cc-bb*bb;
            s=denominator>1e-10f?Mathf.Clamp01((bb*ee-cc*dd)/denominator):0;
            t=(bb*s+ee)/cc;
            if(t<0){t=0;s=Mathf.Clamp01(-dd/aa);}
            else if(t>1){t=1;s=Mathf.Clamp01((bb-dd)/aa);}
        }
        return (w+s*u-t*v).magnitude;
    }
    public static float Capsule(Vector3 a,Vector3 b,float radius,Vector3 c,Vector3 d,float bodyRadius)
        =>Segments(a,b,c,d)-radius-bodyRadius;
    public static float FurthestReach(Vector3 origin,Vector3 direction,Vector3 shoulder,float reach)
    {
        var offset=origin-shoulder;float along=Vector3.Dot(offset,direction);
        float perpendicular=Mathf.Max(0,offset.sqrMagnitude-along*along);
        return -along+Mathf.Sqrt(Mathf.Max(0,reach*reach-perpendicular));
    }
}

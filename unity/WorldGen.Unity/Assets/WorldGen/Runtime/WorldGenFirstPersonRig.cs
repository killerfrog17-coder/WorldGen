using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

/// <summary>One private, native first-person rig. Clips were authored and baked
/// on this exact skeleton; runtime only samples and blends them. No arm IK.</summary>
sealed class WorldGenFirstPersonRig : IDisposable
{
    [Serializable] public sealed class ClipInfo {public string name;public float duration,contact,release;public bool loop;}
    [Serializable] sealed class Manifest {public ClipInfo[] clips;}
    public readonly GameObject Root;
    public readonly Animator Animator;
    public readonly Dictionary<string,Transform> Bones;
    public HumanRuntimeDefinition Definition;
    public readonly SkinnedMeshRenderer[] Renderers;
    public readonly Dictionary<string,ClipInfo> Clips;
    readonly Material skin;
    readonly PlayableGraph graph;
    readonly AnimationLayerMixerPlayable layers;
    readonly Layer[] tracks;
    readonly AvatarMask leftMask,rightMask;
    bool maskLeft;

    sealed class Layer
    {
        public AnimationMixerPlayable Mixer;
        public AnimationClipPlayable[] Nodes;
        public AnimationClip[] Clips;
        public float[] Weights;
        public Dictionary<string,int> Index;
        public string Current;
        public double Key;
    }

    public WorldGenFirstPersonRig(Transform camera,HumanRuntimeDefinition person)
    {
        var source=Resources.Load<GameObject>("FirstPerson/Arms");
        var manifest=Resources.Load<TextAsset>("FirstPerson/clips");
        if(source==null||manifest==null)throw new InvalidOperationException("Authored first-person assets are missing.");
        Clips=JsonUtility.FromJson<Manifest>(manifest.text).clips.ToDictionary(c=>c.name,StringComparer.Ordinal);
        Root=UnityEngine.Object.Instantiate(source,camera,false);Root.name="Private authored first-person rig";
        Root.transform.localPosition=Vector3.zero;Root.transform.localRotation=Quaternion.identity;
        Bones=Root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var bone in Bones.Values)bone.gameObject.layer=30;
        Animator=Root.GetComponent<Animator>()??Root.AddComponent<Animator>();Animator.applyRootMotion=false;Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        skin=new Material(Resources.Load<Shader>("WorldGenHuman"));skin.name="Private first-person skin";skin.SetFloat("_HumanViewId",-100);skin.SetFloat("_Roughness",.76f);
        Renderers=Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach(var renderer in Renderers)
        {
            renderer.sharedMaterials=new[]{skin};renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=true;renderer.updateWhenOffscreen=true;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.quality=SkinQuality.Bone4;
            renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*4);
        }
        var clips=Resources.LoadAll<AnimationClip>("FirstPerson/Arms").Where(c=>!c.name.StartsWith("__",StringComparison.Ordinal)).ToArray();
        if(Clips.Keys.Except(clips.Select(c=>Role(c.name))).Any())throw new InvalidOperationException("Native first-person clip import is incomplete.");
        graph=PlayableGraph.Create("Private authored first-person motion");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        tracks=new[]{MakeLayer(clips),MakeLayer(clips)};layers=AnimationLayerMixerPlayable.Create(graph,2);
        for(int i=0;i<2;i++)graph.Connect(tracks[i].Mixer,0,layers,i);
        layers.SetInputWeight(0,1);layers.SetInputWeight(1,0);
        leftMask=MakeMask("Shoulder.L");rightMask=MakeMask("Shoulder.R");
        layers.SetLayerMaskFromAvatarMask(1,rightMask);
        var output=AnimationPlayableOutput.Create(graph,"Native close-up arms",Animator);output.SetSourcePlayable(layers);
        graph.Play();SetPerson(person);
    }

    static string Role(string name)=>name.Substring(name.LastIndexOf('|')+1);
    Layer MakeLayer(AnimationClip[] clips)
    {
        var t=new Layer{Clips=clips,Nodes=new AnimationClipPlayable[clips.Length],Weights=new float[clips.Length],Index=new Dictionary<string,int>(StringComparer.Ordinal)};
        t.Mixer=AnimationMixerPlayable.Create(graph,clips.Length);
        for(int i=0;i<clips.Length;i++)
        {
            t.Index.Add(Role(clips[i].name),i);t.Nodes[i]=AnimationClipPlayable.Create(graph,clips[i]);
            t.Nodes[i].SetApplyFootIK(false);t.Nodes[i].SetApplyPlayableIK(false);t.Nodes[i].SetSpeed(0);
            graph.Connect(t.Nodes[i],0,t.Mixer,i);t.Mixer.SetInputWeight(i,0);
        }
        return t;
    }
    AvatarMask MakeMask(string shoulder)
    {
        var all=Root.GetComponentsInChildren<Transform>(true);var mask=new AvatarMask();mask.transformCount=all.Length;
        var pivot=Bones[shoulder];
        for(int i=0;i<all.Length;i++)
        {
            string path="";for(var current=all[i];current!=Root.transform&&current!=null;current=current.parent)path=current.name+(path.Length==0?"":"/"+path);
            mask.SetTransformPath(i,path);mask.SetTransformActive(i,all[i]==pivot||all[i].IsChildOf(pivot));
        }
        return mask;
    }
    public void SetPerson(HumanRuntimeDefinition person)
    {
        if(Definition==person)return;Definition=person;
        var texture=Resources.Load<Texture2D>("FirstPerson/"+person.Variant.ToUpperInvariant());
        if(texture==null)throw new InvalidOperationException("Missing first-person pigment "+person.Variant);
        skin.SetTexture("_BaseMap",texture);
    }
    public double Duration(string name)=>Clips.TryGetValue(name,out var clip)?clip.duration:1;
    public void Pose(int layer,string name,double seconds,double key,float dt,bool immediate=false)
    {
        var t=tracks[layer];if(!t.Index.TryGetValue(name,out int selected)){name="Empty";selected=t.Index[name];}
        bool initial=t.Current==null;t.Current=name;t.Key=key;
        float total=0;
        for(int i=0;i<t.Nodes.Length;i++)
        {
            float goal=i==selected?1:0;
            // Weights retain the complete blended pose when another action
            // interrupts a transition. No single-clip snap on equip/cancel.
            t.Weights[i]=initial||immediate?goal:Mathf.MoveTowards(t.Weights[i],goal,Mathf.Max(0,dt)/.10f);total+=t.Weights[i];
            double time=i==selected?seconds:t.Nodes[i].GetTime()+dt;
            string role=Role(t.Clips[i].name);bool loop=Clips.TryGetValue(role,out var info)&&info.loop;
            double duration=Math.Max(.01,t.Clips[i].length);
            time=loop?Math.Max(0,time)%duration:Math.Max(0,Math.Min(duration-.00001,time));
            t.Nodes[i].SetTime(time);
        }
        if(total<.0001f){t.Weights[selected]=1;total=1;}
        for(int i=0;i<t.Nodes.Length;i++)t.Mixer.SetInputWeight(i,t.Weights[i]/total);
    }
    public void Offhand(bool enabled,bool left)
    {
        layers.SetInputWeight(1,enabled?1:0);
        if(maskLeft!=left){maskLeft=left;layers.SetLayerMaskFromAvatarMask(1,left?leftMask:rightMask);}
    }
    public void Evaluate()=>graph.Evaluate(0);
    public void Dispose()
    {
        if(graph.IsValid())graph.Destroy();UnityEngine.Object.Destroy(leftMask);UnityEngine.Object.Destroy(rightMask);
        UnityEngine.Object.Destroy(skin);UnityEngine.Object.Destroy(Root);
    }
}

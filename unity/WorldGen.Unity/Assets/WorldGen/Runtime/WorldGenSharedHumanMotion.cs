using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>One pose drives the complete person for both cameras. Actions are
/// layered over locomotion; charge time is supplied by gameplay, never autoplayed.</summary>
sealed class WorldGenSharedHumanMotion : IDisposable
{
    public readonly Dictionary<string,KimodoHumanClip> Clips;
    public readonly AnimationLayerMixerPlayable Output;
    readonly PlayableGraph graph;
    readonly Track locomotion,body,upper,offhand;
    readonly List<AvatarMask> masks=new List<AvatarMask>();
    readonly AvatarMask leftMask,rightMask;
    readonly Transform root,head;
    readonly Vector3 eyeInHead;
    readonly Quaternion headBindRotation;
    public Vector3 LocalEye=>root.InverseTransformPoint(head.TransformPoint(eyeInHead));
    double phase,clock,jumpAt;
    float bodyWeight,upperWeight,offWeight;
    bool airborne,maskLeft;
    public string ActionRole {get;private set;}="Empty";
    public double ActionTime {get;private set;}
    public float MoveSpeed {get;private set;}
    public double GaitPhase=>phase;
    static readonly HashSet<string> MovementRoles=new HashSet<string>{"Idle","Walk","Run","Sprint","Crouch","CrouchIdle","Jump","Swim","SwimCarry","SwimCarryLeft","SwimCarryTwo","WalkBack","StrafeLeft","StrafeRight","RunBack","RunLeft","RunRight"};

    sealed class Track
    {
        public AnimationMixerPlayable Mixer;
        public AnimationClipPlayable[] Nodes;
        public KimodoHumanClip[] Clips;
        public float[] Weights;
        public string Current;
    }
    public WorldGenSharedHumanMotion(PlayableGraph graph,Transform root,KimodoPersonMotion person,bool player,float eyeHeight,Vector3? nativeEyeInHead=null)
    {
        this.graph=graph;
        this.root=root;head=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="head");
        headBindRotation=Quaternion.Inverse(root.rotation)*head.rotation;
        eyeInHead=nativeEyeInHead??head.InverseTransformPoint(root.TransformPoint(new Vector3(0,eyeHeight,person.EyeForward>.05f?person.EyeForward:.15f)));
        Clips=person.Clips.ToDictionary(c=>c.Role,StringComparer.Ordinal);
        locomotion=MakeTrack(person.Clips.Where(c=>MovementRoles.Contains(c.Role)).ToArray());
        Output=AnimationLayerMixerPlayable.Create(graph,player?4:1);
        graph.Connect(locomotion.Mixer,0,Output,0);Output.SetInputWeight(0,1);
        if(!player)return;
        var actions=person.Clips.Where(c=>!MovementRoles.Contains(c.Role)).ToArray();
        body=MakeTrack(actions);upper=MakeTrack(actions);offhand=MakeTrack(actions);
        graph.Connect(body.Mixer,0,Output,1);graph.Connect(upper.Mixer,0,Output,2);graph.Connect(offhand.Mixer,0,Output,3);
        var spine=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="spine_01");
        Output.SetLayerMaskFromAvatarMask(2,MakeMask(root,spine));
        leftMask=MakeMask(root,root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="clavicle_l"));
        rightMask=MakeMask(root,root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="clavicle_r"));
        Output.SetLayerMaskFromAvatarMask(3,rightMask);
    }
    AvatarMask MakeMask(Transform root,Transform pivot)
    {
        var transforms=root.GetComponentsInChildren<Transform>(true);var mask=new AvatarMask();mask.transformCount=transforms.Length;
        for(int i=0;i<transforms.Length;i++)
        {
            string path="";for(var t=transforms[i];t!=root&&t!=null;t=t.parent)path=t.name+(path.Length==0?"":"/"+path);
            mask.SetTransformPath(i,path);mask.SetTransformActive(i,transforms[i]==pivot||transforms[i].IsChildOf(pivot));
        }
        masks.Add(mask);return mask;
    }
    Track MakeTrack(KimodoHumanClip[] clips)
    {
        var track=new Track{Clips=clips,Nodes=new AnimationClipPlayable[clips.Length],Weights=new float[clips.Length]};
        track.Mixer=AnimationMixerPlayable.Create(graph,clips.Length);
        for(int i=0;i<clips.Length;i++)
        {
            track.Nodes[i]=AnimationClipPlayable.Create(graph,clips[i].Clip);track.Nodes[i].SetApplyFootIK(false);track.Nodes[i].SetApplyPlayableIK(false);track.Nodes[i].SetSpeed(0);
            graph.Connect(track.Nodes[i],0,track.Mixer,i);
        }
        return track;
    }
    public double Duration(string role)=>Clips.TryGetValue(role,out var c)?c.Duration:1;
    public float LocomotionWeight(string role)
    {
        int index=Array.FindIndex(locomotion.Clips,c=>c.Role==role);
        return index<0?0:locomotion.Mixer.GetInputWeight(index);
    }
    public void AimHead(Quaternion look)=>head.rotation=look*headBindRotation;
    public void Locomotion(float speed,bool inAir,bool crouch,bool swimming,float dt,Vector2 direction=default,string swimRole="Swim")
    {
        MoveSpeed=speed;clock+=dt;
        if(inAir&&!airborne)jumpAt=clock;
        airborne=inAir;
        if(direction.sqrMagnitude<.001f)direction=Vector2.up;else direction.Normalize();
        float sideways=Mathf.Abs(direction.x),back=Mathf.Max(0,-direction.y),forwards=Mathf.Max(0,direction.y),sum=Mathf.Max(.001f,sideways+back+forwards);
        sideways/=sum;back/=sum;forwards/=sum;
        float directionalRun=Mathf.InverseLerp(1.5f,3.4f,speed);
        double stride=crouch?1.1:speed<1.5?1.7:speed<3.4?2.5:3.5;
        string strideRole=crouch?"Crouch":speed<1.5?"Walk":speed<3.4?"Run":"Sprint";
        if(Clips.TryGetValue(strideRole,out var reference)&&reference.TravelMeters>.1)stride=reference.TravelMeters;
        if(!crouch)
        {
            double backStride=Clips.TryGetValue("WalkBack",out var backClip)&&backClip.TravelMeters>.1?backClip.TravelMeters:stride;
            if(Clips.TryGetValue("RunBack",out var backRun)&&backRun.TravelMeters>.1)backStride=backStride*(1-directionalRun)+backRun.TravelMeters*directionalRun;
            string sideRole=direction.x<0?"StrafeLeft":"StrafeRight";
            double sideStride=Clips.TryGetValue(sideRole,out var sideClip)&&sideClip.TravelMeters>.1?sideClip.TravelMeters:stride;
            if(Clips.TryGetValue(direction.x<0?"RunLeft":"RunRight",out var sideRun)&&sideRun.TravelMeters>.1)sideStride=sideStride*(1-directionalRun)+sideRun.TravelMeters*directionalRun;
            stride=forwards*stride+back*backStride+sideways*sideStride;
        }
        if(!inAir&&!swimming)phase=(phase+dt*speed/Math.Max(.3,stride))%1;
        float total=0;
        for(int i=0;i<locomotion.Nodes.Length;i++)
        {
            var c=locomotion.Clips[i];float goal=0;
            if(swimming)goal=c.Role==(Clips.ContainsKey(swimRole)?swimRole:"Swim")?1:0;
            else if(inAir)goal=c.Role=="Jump"?1:0;
            else if(crouch)goal=c.Role==(speed<.08f?"CrouchIdle":"Crouch")?1:0;
            else if(speed<1.5f){float w=Mathf.Clamp01(speed/1.5f);goal=c.Role=="Idle"?1-w:c.Role=="Walk"?w:0;}
            else if(speed<3.4f){float w=Mathf.InverseLerp(1.5f,3.4f,speed);goal=c.Role=="Walk"?1-w:c.Role=="Run"?w:0;}
            else {float w=Mathf.InverseLerp(3.4f,6,speed);goal=c.Role=="Run"?1-w:c.Role=="Sprint"?w:0;}
            if(!swimming&&!inAir&&!crouch)
            {
                if(c.Role=="Walk"||c.Role=="Run"||c.Role=="Sprint")goal*=forwards;
                float moving=Mathf.Clamp01(speed/1.5f);
                if(c.Role=="WalkBack")goal=back*moving*(1-directionalRun);
                if(c.Role=="RunBack")goal=back*moving*directionalRun;
                if(c.Role=="StrafeLeft"&&direction.x<0||c.Role=="StrafeRight"&&direction.x>0)goal=sideways*moving*(1-directionalRun);
                if(c.Role=="RunLeft"&&direction.x<0||c.Role=="RunRight"&&direction.x>0)goal=sideways*moving*directionalRun;
            }
            locomotion.Weights[i]=Mathf.Lerp(locomotion.Weights[i],goal,1-Mathf.Exp(-dt*14));total+=locomotion.Weights[i];
            double time=c.Role=="Jump"?Math.Min(c.Duration-.0001,(clock-jumpAt)/.61*c.Duration):c.Role=="Walk"||c.Role.StartsWith("Run",StringComparison.Ordinal)||c.Role=="Sprint"||c.Role=="Crouch"||c.Role=="WalkBack"||c.Role.StartsWith("Strafe",StringComparison.Ordinal)?phase*c.Duration:clock%c.Duration;
            locomotion.Nodes[i].SetTime(Math.Max(0,time));
        }
        if(total<.001f){int idle=Array.FindIndex(locomotion.Clips,c=>c.Role=="Idle");locomotion.Weights[idle]=total=1;}
        for(int i=0;i<locomotion.Nodes.Length;i++)locomotion.Mixer.SetInputWeight(i,locomotion.Weights[i]/total);
    }
    void Pose(Track track,string role,double seconds,float dt)
    {
        if(!Clips.ContainsKey(role))role="Empty";
        bool initial=track.Current==null;track.Current=role;float total=0;
        for(int i=0;i<track.Nodes.Length;i++)
        {
            float goal=track.Clips[i].Role==role?1:0;
            track.Weights[i]=initial?goal:Mathf.MoveTowards(track.Weights[i],goal,dt/.12f);total+=track.Weights[i];
            var c=track.Clips[i];double time=goal>0?seconds:track.Nodes[i].GetTime()+dt;
            track.Nodes[i].SetTime(c.Loop?Math.Max(0,time)%c.Duration:Math.Max(0,Math.Min(c.Duration-.00001,time)));
        }
        for(int i=0;i<track.Nodes.Length;i++)track.Mixer.SetInputWeight(i,track.Weights[i]/Math.Max(.0001f,total));
    }
    public void Action(string role,double seconds,bool active,bool fullBody,string other,double otherSeconds,bool otherLeft,float dt)
    {
        if(body==null)return;
        ActionRole=role;ActionTime=seconds;
        Pose(body,role,seconds,dt);Pose(upper,role,seconds,dt);
        float smoothing=1-Mathf.Exp(-dt*16);
        upperWeight=Mathf.Lerp(upperWeight,active?1:0,smoothing);
        float stationary=1-Mathf.InverseLerp(.05f,.65f,MoveSpeed);
        bodyWeight=Mathf.Lerp(bodyWeight,active&&fullBody&&!airborne?stationary:0,smoothing);
        offWeight=Mathf.Lerp(offWeight,other!=null?1:0,smoothing);
        Output.SetInputWeight(1,bodyWeight);Output.SetInputWeight(2,upperWeight);Output.SetInputWeight(3,offWeight);
        if(other!=null)
        {
            if(maskLeft!=otherLeft){maskLeft=otherLeft;Output.SetLayerMaskFromAvatarMask(3,otherLeft?leftMask:rightMask);}
            Pose(offhand,other,otherSeconds,dt);
        }
    }
    public void Dispose(){foreach(var mask in masks)UnityEngine.Object.Destroy(mask);}
}

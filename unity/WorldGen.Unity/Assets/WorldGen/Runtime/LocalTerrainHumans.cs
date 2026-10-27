using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class HumanActor
    {
        public HumanRuntimeDefinition Definition;public GameObject Root;public Animator Animator;
        public PlayableGraph Graph;public AnimationMixerPlayable Mixer;public AnimationClipPlayable[] Inputs;
        public float[] Weights;public int Jump=-1;public bool Airborne;
        public readonly List<Material> OwnedMaterials=new List<Material>();
        public TerrainMotion Motor;public V Planet;public float Yaw,TargetYaw,Speed,TargetSpeed;public double Decision,ObstacleAt,GaitPhase;
    }
    readonly PlayerCameraRig humanCamera=new PlayerCameraRig();
    readonly List<HumanActor> humanNpcs=new List<HumanActor>();
    readonly System.Random humanRandom=new System.Random();
    WorldGenHumanLibrary humanLibrary;HumanActor humanPlayer;LocalTerrainFrame humanFrame;
    bool humansInitialized,humanReviewPose;int humanNextId=2,humanReviewBodyMask=-1;
    float humanCameraDistance,humanEyeOffset;double humanCameraProbeAt;Vector3 humanProbePivot,humanProbeDirection;float humanClearDistance=12;
    double humanLastX,humanLastZ;bool humanHideBody;
    long humanSpawnQueued;
    public string HumanDebug=>humanPlayer==null?"Humans unavailable":"Human "+humanPlayer.Definition.Variant+"; "+humanNpcs.Count+" demo NPCs; "+(humanCamera.FirstPerson?"first person":"third person "+humanCamera.Distance.ToString("F1")+"m")+(humanCamera.Locked?" (zoom locked)":"");
    void InitializeHumans()
    {
        if(humansInitialized)return;humansInitialized=true;
        humanLibrary=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        if(humanLibrary==null||humanLibrary.People==null||humanLibrary.People.Length!=6){Debug.LogWarning("MPFB human library unavailable");return;}
        int variant=playState?.PlayerHumanVariant??-1;if(variant<0||variant>=humanLibrary.People.Length)variant=humanRandom.Next(humanLibrary.People.Length);
        if(playState!=null)playState.PlayerHumanVariant=variant;
        humanCamera.Restore(playState?.CameraDistance??0,playState?.CameraDistanceLocked??false);humanCameraDistance=(float)humanCamera.Distance;
        humanPlayer=CreateHuman(variant,1);humanPlayer.Yaw=humanPlayer.TargetYaw=heading;
        humanEyeOffset=humanPlayer.Definition.EyeHeight-(float)TerrainMotion.EyeHeightM;
        RenderPipelineManager.beginCameraRendering+=BindHumanCamera;
    }
    HumanActor CreateHuman(int variant,int id)
    {
        var definition=humanLibrary.People[variant];var root=Instantiate(definition.Prefab,transform,false);root.name=id==1?"Player human":"Demo human "+id;
        var animator=root.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var actor=new HumanActor{Root=root,Animator=animator,Definition=definition,Inputs=new AnimationClipPlayable[definition.Clips.Length],Weights=new float[definition.Clips.Length]};
        actor.Graph=PlayableGraph.Create(root.name+" movement");actor.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        actor.Mixer=AnimationMixerPlayable.Create(actor.Graph,actor.Inputs.Length);
        var output=AnimationPlayableOutput.Create(actor.Graph,"Native MPFB skin",animator);output.SetSourcePlayable(actor.Mixer);
        for(int i=0;i<actor.Inputs.Length;i++)
        {
            actor.Inputs[i]=AnimationClipPlayable.Create(actor.Graph,definition.Clips[i]);actor.Inputs[i].SetApplyFootIK(false);
            actor.Graph.Connect(actor.Inputs[i],0,actor.Mixer,i);if(definition.Roles[i]=="Jump")actor.Jump=i;
            actor.Weights[i]=definition.Roles[i]=="Idle"?1:0;actor.Mixer.SetInputWeight(i,actor.Weights[i]);
        }
        foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(id==1)
            {
                // Keep the local identity explicit even on renderer paths that
                // batch or cache per-renderer constant buffers differently.
                var materials=renderer.sharedMaterials.Select(m=>new Material(m)).ToArray();
                foreach(var material in materials){material.SetFloat("_HumanViewId",id);actor.OwnedMaterials.Add(material);}
                renderer.sharedMaterials=materials;
            }
            var props=new MaterialPropertyBlock();props.SetFloat("_HumanViewId",id);renderer.SetPropertyBlock(props);
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        actor.Graph.Play();actor.Graph.Evaluate(0);return actor;
    }
    void BindHumanCamera(ScriptableRenderContext context,Camera camera)
    {
        float id=camera==view&&(humanReviewBodyMask<0?humanHideBody:humanReviewBodyMask==1)?1:0;
        Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",id);
        var commands=CommandBufferPool.Get("Human camera visibility");commands.SetGlobalFloat("_WorldGenFirstPersonHumanId",id);
        context.ExecuteCommandBuffer(commands);CommandBufferPool.Release(commands);
    }
    void ReadHumanCameraInput(Mouse mouse,Keyboard keyboard)
    {
        if(mouse==null)return;
        if(!Spectating&&mouse.middleButton.wasPressedThisFrame)humanCamera.ToggleLock();
        float scroll=mouse.scroll.ReadValue().y;if(scroll==0)return;
        // Preserve fly-speed adjustment without competing with camera zoom.
        if(motion.Flying&&keyboard!=null&&(keyboard.leftAltKey.isPressed||keyboard.rightAltKey.isPressed))
            motion.FlySpeed=Math.Max(5,Math.Min(20000,motion.FlySpeed*Math.Exp(Math.Sign(scroll)*.25)));
        else if(!Spectating)humanCamera.Zoom(Math.Sign(scroll)*Math.Max(1,Math.Min(4,Math.Abs(scroll)/120)));
    }
    void UpdateHumans()
    {
        if(humanPlayer==null||!Ready||humanReviewPose)return;
        bool stopped=inventoryOpen||paused||mapOpen||ecologyOpen||chatOpen||!Application.isFocused&&!WorldGenPerformance.Enabled;
        float dt=stopped?0:Math.Min(.1f,Time.unscaledDeltaTime);
        bool changed=!ReferenceEquals(humanFrame,frame);
        if(changed)
        {
            humanFrame=frame;humanLastX=motion.X;humanLastZ=motion.Z;humanCameraProbeAt=0;
            for(int i=humanNpcs.Count-1;i>=0;i--)
            {
                var npc=humanNpcs[i];if((npc.Planet-Position).Length*frame.RadiusM>400){DestroyHuman(npc);humanNpcs.RemoveAt(i);continue;}
                TerrainHandoff.Coordinates(frame,npc.Planet,out double x,out double z);npc.Motor.Place(x,z,Ground(x,z));
            }
        }
        double dx=motion.X-humanLastX,dz=motion.Z-humanLastZ;humanLastX=motion.X;humanLastZ=motion.Z;
        float speed=dt>0&&!changed?(float)(Math.Sqrt(dx*dx+dz*dz)/dt):0;if(speed>15)speed=0;
        bool crouch=!motion.Flying&&ActiveKeyboard!=null&&ActiveKeyboard.ctrlKey.isPressed;
        if(!deathPresented){
        if(speed>.05f)humanPlayer.TargetYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);
        humanPlayer.Yaw=Mathf.MoveTowardsAngle(humanPlayer.Yaw,humanPlayer.TargetYaw,dt*540);
        humanPlayer.Root.transform.localPosition=new Vector3((float)(motion.X-originX),(float)(motion.Y-TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
        humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,humanPlayer.Yaw,0);
        AnimateHuman(humanPlayer,speed,!motion.Flying&&!motion.Grounded,crouch,dt);
        }
        if(!stopped&&CenterReady&&humanSpawnQueued>0)
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();int attempts=0;
            while(humanSpawnQueued>0&&attempts++<4)
            {humanSpawnQueued--;SpawnHumans(1);if((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency>2)break;}
        }
        foreach(var npc in humanNpcs)
        {
            if(dt>0)
            {
                if(Time.unscaledTime>=npc.Decision)
                {
                    npc.Decision=Time.unscaledTime+2+humanRandom.NextDouble()*5;
                    npc.TargetYaw=npc.Yaw+(float)(humanRandom.NextDouble()*220-110);
                    double choice=humanRandom.NextDouble();npc.TargetSpeed=choice<.25?0:choice<.72?1.4f:choice<.94?3.4f:6;
                    double awayX=npc.Motor.X-motion.X,awayZ=npc.Motor.Z-motion.Z;
                    if(awayX*awayX+awayZ*awayZ>70*70)npc.TargetYaw=(float)(Math.Atan2(-awayX,-awayZ)*180/Math.PI);
                }
                npc.Yaw=Mathf.MoveTowardsAngle(npc.Yaw,npc.TargetYaw,dt*100);npc.Speed=Mathf.MoveTowards(npc.Speed,npc.TargetSpeed,dt*3);
                if(Time.unscaledTime>=npc.ObstacleAt)
                {
                    npc.ObstacleAt=Time.unscaledTime+.35;
                    double x=npc.Motor.X+Math.Sin(npc.Yaw*Math.PI/180)*1.5,z=npc.Motor.Z+Math.Cos(npc.Yaw*Math.PI/180)*1.5;
                    var point=frame.Position(x,z);var sample=walkingPatch.Sample(point);
                    if(sample.Underwater||Math.Abs(Ground(x,z)-Ground(npc.Motor.X,npc.Motor.Z))>1.1)
                    {npc.TargetYaw=npc.Yaw+130;npc.TargetSpeed=0;npc.Decision=Time.unscaledTime+1;}
                }
                bool walk=npc.Speed<2,run=npc.Speed>4;
                double beforeX=npc.Motor.X,beforeZ=npc.Motor.Z;
                npc.Motor.Move(dt,npc.Speed/(walk?TerrainMotion.WalkSpeedMps:run?TerrainMotion.RunSpeedMps:TerrainMotion.JogSpeedMps),0,0,npc.Yaw,0,run,false,Ground,walk);
                float actual=(float)(Math.Sqrt(Math.Pow(npc.Motor.X-beforeX,2)+Math.Pow(npc.Motor.Z-beforeZ,2))/dt);
                AnimateHuman(npc,actual,!npc.Motor.Grounded,false,dt);
            }
            npc.Planet=frame.Position(npc.Motor.X,npc.Motor.Z);
            npc.Root.transform.localPosition=new Vector3((float)(npc.Motor.X-originX),(float)(npc.Motor.Y-TerrainMotion.EyeHeightM),(float)(npc.Motor.Z-originZ));
            npc.Root.transform.localRotation=Quaternion.Euler(0,npc.Yaw,0);
        }
        humanEyeOffset=Mathf.Lerp(humanEyeOffset,humanPlayer.Definition.EyeHeight-(float)TerrainMotion.EyeHeightM-(crouch?.42f:0),1-Mathf.Exp(-dt*12));
        Ground(motion.X,motion.Z); // Preserve player-centred elevation/debug memo.
    }
    static void AnimateHuman(HumanActor actor,float speed,bool airborne,bool crouch,float dt)
    {
        if(airborne&&!actor.Airborne&&actor.Jump>=0)actor.Inputs[actor.Jump].SetTime(0);
        actor.Airborne=airborne;float total=0;
        // Keep left/right foot phases aligned when mixing clips with different
        // durations, rather than blending unrelated free-running cycles.
        float Stride(string role,float fallbackSpeed)
        {int index=Array.IndexOf(actor.Definition.Roles,role);var strides=actor.Definition.StrideMeters;return strides!=null&&index<strides.Length&&strides[index]>.05f?strides[index]:fallbackSpeed*actor.Definition.Clips[index].length;}
        float runStride=Stride("Run",3.4f),sprintStride=Stride("Sprint",6);
        float frequency=crouch?speed/3:speed<1.5f?speed/2:speed<3.4f?Mathf.Lerp(speed/2,speed/runStride,Mathf.InverseLerp(1.5f,3.4f,speed)):speed/Mathf.Lerp(runStride,sprintStride,Mathf.InverseLerp(3.4f,6,speed));
        if(!airborne)actor.GaitPhase=(actor.GaitPhase+dt*frequency)%1;
        for(int i=0;i<actor.Inputs.Length;i++)
        {
            string role=actor.Definition.Roles[i];float target=0,rate=1;
            if(airborne)target=role=="Jump"?1:0;
            else if(crouch)target=role=="Crouch"?1:0;
            else if(speed<1.5f){float walk=Mathf.Clamp01(speed/1.5f);target=role=="Idle"?1-walk:role=="Walk"?walk:0;}
            else if(speed<3.4f){float run=Mathf.InverseLerp(1.5f,3.4f,speed);target=role=="Walk"?1-run:role=="Run"?run:0;}
            else {float sprint=Mathf.InverseLerp(3.4f,6,speed);target=role=="Run"?1-sprint:role=="Sprint"?sprint:0;}
            if(role=="Walk")rate=Mathf.Clamp(speed/1.5f,.5f,1.6f);
            if(role=="Run")rate=Mathf.Clamp(speed/3.4f,.6f,1.6f);
            if(role=="Sprint")rate=Mathf.Clamp(speed/6,.6f,1.3f);
            if(role=="Crouch")rate=speed<.05f?0:Mathf.Clamp(speed/1.5f,.3f,1.5f);
            if(role=="Jump")rate=actor.Definition.Clips[i].length/.61f;
            if(role=="Walk"||role=="Run"||role=="Sprint"||role=="Crouch")
            {actor.Inputs[i].SetTime(actor.GaitPhase*actor.Definition.Clips[i].length);rate=0;}
            actor.Inputs[i].SetSpeed(rate);actor.Weights[i]=Mathf.Lerp(actor.Weights[i],target,1-Mathf.Exp(-dt*12));total+=actor.Weights[i];
        }
        for(int i=0;i<actor.Inputs.Length;i++)actor.Mixer.SetInputWeight(i,actor.Weights[i]/Mathf.Max(.0001f,total));
        actor.Graph.Evaluate(dt);
    }
    int SpawnHumans(int count)
    {
        if(humanLibrary==null||!Ready||!CenterReady)return 0;int made=0;
        for(int i=0;i<count;i++)
        {
            for(int attempt=0;attempt<12;attempt++)
            {
                double angle=(heading-65+humanRandom.NextDouble()*130)*Math.PI/180,distance=7+humanRandom.NextDouble()*10;
                double x=motion.X+Math.Sin(angle)*distance,z=motion.Z+Math.Cos(angle)*distance;
                var point=frame.Position(x,z);var sample=walkingPatch.Sample(point);
                if(sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.1)continue;
                double ground=Ground(x,z);if(Math.Abs(Ground(x+1,z)-ground)>.65||Math.Abs(Ground(x,z+1)-ground)>.65)continue;
                var actor=CreateHuman(humanRandom.Next(humanLibrary.People.Length),humanNextId++);actor.Motor=new TerrainMotion();actor.Motor.Place(x,z,ground);
                actor.Planet=point;actor.Yaw=actor.TargetYaw=(float)(angle*180/Math.PI+180);actor.Decision=Time.unscaledTime+1+humanRandom.NextDouble()*3;
                actor.Root.transform.localPosition=new Vector3((float)(x-originX),(float)ground,(float)(z-originZ));actor.Root.transform.localRotation=Quaternion.Euler(0,actor.Yaw,0);
                humanNpcs.Add(actor);made++;break;
            }
        }
        return made;
    }
    void UpdateHumanCamera()
    {
        if(Spectating){view.transform.localPosition=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));view.transform.localRotation=Quaternion.Euler(pitch,heading,0);return;}
        var rotation=Quaternion.Euler(pitch,heading,0);float dt=Math.Min(.1f,Time.unscaledDeltaTime);
        humanCameraDistance=Mathf.Lerp(humanCameraDistance,(float)humanCamera.Distance,1-Mathf.Exp(-dt*12));
        float pivotDrop=Mathf.SmoothStep(0,.22f,Mathf.Clamp01(humanCameraDistance));
        var pivot=new Vector3((float)(motion.X-originX),(float)motion.Y+humanEyeOffset-pivotDrop,(float)(motion.Z-originZ));
        // Eyes sit forward of the neck axis, avoiding a camera inside the collar.
        pivot+=Quaternion.Euler(0,heading,0)*Vector3.forward*(.30f*(1-Mathf.Clamp01(humanCameraDistance)));
        var back=rotation*Vector3.back;
        if(humanCameraDistance>.05f&&(Time.unscaledTime>=humanCameraProbeAt||(pivot-humanProbePivot).sqrMagnitude>.25f||Vector3.Dot(back,humanProbeDirection)<.995f))
        {
            humanCameraProbeAt=Time.unscaledTime+.05;humanProbePivot=pivot;humanProbeDirection=back;
            var p=new V(pivot.x+originX,pivot.y,pivot.z+originZ);var direction=new V(back.x,back.y,back.z);
            humanClearDistance=(float)PlayerCameraRig.ClearDistance(p,direction,humanCamera.Distance,Ground);
            Ground(motion.X,motion.Z);
        }
        float distance=Mathf.Min(humanCameraDistance,humanClearDistance);humanHideBody=distance<.65f;
        view.transform.localPosition=pivot+back*distance;view.transform.localRotation=rotation;
    }
    long ClearSpawnedHumans()
    {
        long cleared=humanNpcs.Count+humanSpawnQueued;
        foreach(var human in humanNpcs)DestroyHuman(human);
        humanNpcs.Clear();humanSpawnQueued=0;return cleared;
    }
    PlayCommandResult ExecuteHumanSpawn(string[] arguments)
    {
        if(arguments.Length==1&&arguments[0].Equals("clear",StringComparison.OrdinalIgnoreCase))
            return new PlayCommandResult(true,"Cleared "+ClearSpawnedHumans()+" command-spawned/queued humans.");
        if(humanLibrary==null)return new PlayCommandResult(false,"Human assets have not initialized yet.");
        int count=1;
        if(arguments.Length>1||arguments.Length==1&&(!int.TryParse(arguments[0],out count)||count<1))return new PlayCommandResult(false,"Usage: /spawn human [positive count] or /spawn human clear.");
        humanSpawnQueued+=count;return new PlayCommandResult(true,"Queued "+count+" humans on nearby dry, gentle ground. /spawn clear cancels pending spawns.");
    }
    static void DestroyHuman(HumanActor human){if(human.Graph.IsValid())human.Graph.Destroy();if(human.Root!=null)UnityEngine.Object.Destroy(human.Root);foreach(var material in human.OwnedMaterials)if(material!=null)UnityEngine.Object.Destroy(material);}
    void DisposeHumans()
    {
        RenderPipelineManager.beginCameraRendering-=BindHumanCamera;Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);
        if(humanPlayer!=null)DestroyHuman(humanPlayer);humanPlayer=null;
        foreach(var human in humanNpcs)DestroyHuman(human);humanNpcs.Clear();humanLibrary=null;
    }
    public void TestHumanView(double distance,bool locked=false){humanCamera.Restore(distance,locked);humanCameraDistance=(float)distance;humanCameraProbeAt=0;UpdateCamera();}
    public int TestHumanCount=>humanNpcs.Count;
}

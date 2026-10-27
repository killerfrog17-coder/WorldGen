using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    const int InitialAnimalCapacity=128,AnimalBones=128;
    sealed class AnimalModel
    {public WorldGenFaunaAsset Asset;public AssetBundle Bundle;public Material Material;public int Users;public double LastUse;}
    sealed class Animal
    {
        public FaunaPrototypeSpawn Spawn;public AnimalModel Model;public GameObject Root;public MeshRenderer Renderer;public MaterialPropertyBlock Props;
        public int Slot;public double X,Z,HomeX,HomeZ,GroundY,NextDecision,Time,OldTime,BlendAge,Life,NextGround;
        public float Heading,TargetHeading,Speed,TargetSpeed,Fade,NominalSpeed;
        public FaunaRuntimeClip Clip,OldClip;public bool Wanted=true,Manual;public Vector4 Parts;
        public V Planet;public Rng Random;public bool Dead;public double Health;
    }
    FaunaPrototypePlanner animalPlanner;
    Task<List<FaunaPrototypeSpawn>> animalPlan;LocalTerrainFrame animalPlanFrame,animalFrame;
    readonly Dictionary<ulong,Animal> animals=new Dictionary<ulong,Animal>();
    readonly Dictionary<string,AnimalModel> animalModels=new Dictionary<string,AnimalModel>();
    readonly HashSet<string> animalLoadFailures=new HashSet<string>();
    readonly List<FaunaPrototypeSpawn> animalWanted=new List<FaunaPrototypeSpawn>();
    readonly List<ulong> animalRemoval=new List<ulong>();
    readonly Stack<int> animalSlots=new Stack<int>();
    Matrix4x4[] animalBoneData=Array.Empty<Matrix4x4>();double animalAllocationRetry;
    GraphicsBuffer animalBoneBuffer;Shader animalShader;Coroutine animalLoading;AssetBundleCreateRequest animalBundleRequest;AssetBundle animalPendingBundle;
    static LocalTerrainExplorer animalSceneOwner;
    V animalPlanAt;double nextAnimalPlan;int animalCandidates;bool animalDisposed;
    readonly bool animalDisabled=Array.Exists(Environment.GetCommandLineArgs(),x=>x=="--worldgen-no-animals");
    public string AnimalFaunaDebug=>animalPlanner==null?"Wildlife off":"Wildlife "+animals.Count+" animals; GPU capacity "+animalBoneData.Length/AnimalBones+", "+animalModels.Count+" loaded models; "+animalPlanner.MappedTaxa+" native taxa mapped"+(animalPlan!=null?"; planning":"")+(animalLoading!=null?"; loading":"")+(animalLoadFailures.Count>0?"; "+animalLoadFailures.Count+" asset failures":"");
    void InitializeAnimalFauna()
    {
        if(animalDisabled||sourceWorld==null||sourceWorld.Ecology==null)return;
        var catalogue=Resources.Load<TextAsset>("WorldGenFaunaCatalogue");animalShader=Resources.Load<Shader>("WorldGenFauna");
        if(catalogue==null||animalShader==null||!animalShader.isSupported){Debug.LogWarning("Visible fauna assets/shader unavailable.");return;}
        var profiles=JsonUtility.FromJson<FaunaPrototypeCatalogue>(catalogue.text);
        if(profiles.version!=1||profiles.animals.Length!=177)throw new InvalidDataException("Visible fauna catalogue mismatch");
        animalProfiles=profiles.animals;animalPlanner=new FaunaPrototypePlanner(sourceWorld,terrain,animalProfiles,HorizontalScale);
        GrowAnimalPalette();animalSceneOwner=this;
    }
    bool GrowAnimalPalette()
    {
        if(animalSlots.Count>0)return true;
        if(Time.realtimeSinceStartupAsDouble<animalAllocationRetry)return false;
        GraphicsBuffer buffer=null;
        try
        {
            int old=animalBoneData.Length/AnimalBones;long capacity=old==0?InitialAnimalCapacity:(long)old*2;
            long matrices=capacity*AnimalBones,bytes=matrices*64;
            if(matrices>int.MaxValue||bytes>SystemInfo.maxGraphicsBufferSize)throw new InvalidOperationException("Graphics API buffer size exhausted");
            var data=new Matrix4x4[(int)matrices];Array.Copy(animalBoneData,data,animalBoneData.Length);
            for(int i=animalBoneData.Length;i<data.Length;i++)data[i]=Matrix4x4.identity;
            buffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,data.Length,64);buffer.SetData(data);
            var previous=animalBoneBuffer;animalBoneBuffer=buffer;buffer=null;animalBoneData=data;
            Shader.SetGlobalBuffer("_FaunaBones",animalBoneBuffer);previous?.Dispose();
            for(int i=(int)capacity-1;i>=old;i--)animalSlots.Push(i);return true;
        }
        catch(Exception error) when(error is OutOfMemoryException||error is ArgumentException||error is InvalidOperationException)
        {buffer?.Dispose();animalAllocationRetry=Time.realtimeSinceStartupAsDouble+5;PostExplorerChat("Could not allocate more animal pose storage: "+error.Message+". Pending spawns remain queued; /spawn clear cancels them.",true);return false;}
    }
    void UpdateAnimalFauna()
    {
        if(animalPlanner==null||animalDisposed||!Ready)return;
        double now=Time.realtimeSinceStartupAsDouble;
        if(!ReferenceEquals(animalFrame,frame))
        {
            animalFrame=frame;nextAnimalPlan=0;
            foreach(var pair in animals.ToArray())
            {
                var animal=pair.Value;
                if((animal.Planet-Position).Length*frame.RadiusM>1000){RemoveAnimal(pair.Key);continue;}
                TerrainHandoff.Coordinates(frame,animal.Planet,out animal.X,out animal.Z);
                TerrainHandoff.Coordinates(frame,animal.Spawn.Position,out animal.HomeX,out animal.HomeZ);
                animal.GroundY=frame.SurfaceY(animal.Planet,animal.Spawn.GroundM);animal.NextGround=0;
            }
        }
        if(animalPlan!=null&&animalPlan.IsCompleted)
        {
            if(animalPlan.IsFaulted)Debug.LogWarning("Visible fauna placement: "+animalPlan.Exception.GetBaseException().Message);
            else if(!animalPlan.IsCanceled&&ReferenceEquals(animalPlanFrame,frame))
            {
                var playerAt=Position;double radius=frame.RadiusM;
                // Share the model cache across media, rather than loading every
                // ground species before considering nearby birds or insects.
                animalWanted.Clear();animalWanted.AddRange(animalPlan.Result.OrderBy(s=>(s.Position-playerAt).Length*radius/Math.Max(1,s.Profile.radiusM)).ThenBy(s=>s.Id));animalCandidates=animalWanted.Count;
                var ids=new HashSet<ulong>(animalWanted.Select(s=>s.Id));foreach(var animal in animals.Values)animal.Wanted=animal.Manual||ids.Contains(animal.Spawn.Id);
            }
            animalPlan=null;
        }
        bool stopped=paused||mapOpen||survivalOpen;
        if(!stopped&&CenterReady&&!NearTerrainPending()&&HeightAboveGround<1200&&animalPlan==null&&ActiveWorkerCount<WorkerLimit&&
           (now>=nextAnimalPlan||(Position-animalPlanAt).Length*frame.RadiusM>80))
        {
            nextAnimalPlan=now+8;animalPlanAt=Position;animalPlanFrame=frame;
            V at=Position;double hour=LocalSolarHour,scale=HorizontalScale;var planner=animalPlanner;var token=cancellation.Token;
            animalPlan=Task.Run(()=>planner.Build(at,scale,hour,token),token);
        }
        bool localPopulation=HeightAboveGround<1200;
        if(!localPopulation){animalWanted.Clear();animalManualWanted.Clear();animalManualRequests.Clear();foreach(var animal in animals.Values)animal.Wanted=false;}
        if(!stopped&&localPopulation)
        {
            UpdateManualAnimalSpawns();
            long admission=System.Diagnostics.Stopwatch.GetTimestamp();
            foreach(var spawn in animalWanted)
            {
                if(animals.ContainsKey(spawn.Id)||!animalModels.TryGetValue(spawn.Profile.slug,out var model))continue;
                if((spawn.Position-Position).Length*frame.RadiusM>spawn.Profile.radiusM)continue;
                if(!AddAnimal(spawn,model))break;
                if((System.Diagnostics.Stopwatch.GetTimestamp()-admission)*1000d/System.Diagnostics.Stopwatch.Frequency>1.5)break;
            }
            if(animalLoading==null&&CenterReady&&(animalManualWanted.Count>0||!NearTerrainPending()))
            {
                var next=animalManualWanted.FirstOrDefault(s=>!animalModels.ContainsKey(s.Profile.slug)&&!animalLoadFailures.Contains(s.Profile.slug))??animalWanted.FirstOrDefault(s=>!animalModels.ContainsKey(s.Profile.slug)&&!animalLoadFailures.Contains(s.Profile.slug));
                if(next!=null)
                {
                    EvictAnimalModel(); // Recycle unused models, never discard an active species for a count.
                    if(!File.Exists(Path.Combine(Application.streamingAssetsPath,"fauna",next.Profile.bundle)))
                    {animalLoadFailures.Add(next.Profile.slug);Debug.LogWarning("Missing fauna bundle "+next.Profile.slug);}
                    else animalLoading=StartCoroutine(LoadAnimalModel(next.Profile));
                }
            }
        }
        float dt=stopped?0:Math.Min(.1f,Time.unscaledDeltaTime);
        animalRemoval.Clear();int paletteLength=0;
        foreach(var pair in animals)
        {
            var animal=pair.Value;
            double distance=(animal.Planet-Position).Length*frame.RadiusM;
            if(distance>animal.Spawn.Profile.radiusM*1.25)animal.Wanted=false;
            animal.Fade=Mathf.MoveTowards(animal.Fade,animal.Wanted?1:0,Math.Max(.001f,dt)*2);
            if(!animal.Wanted&&animal.Fade<=0){animalRemoval.Add(pair.Key);continue;}
            UpdateAnimal(animal,dt);
            paletteLength=Math.Max(paletteLength,(animal.Slot+1)*AnimalBones);
        }
        foreach(ulong id in animalRemoval)RemoveAnimal(id);
        if(paletteLength>0)animalBoneBuffer.SetData(animalBoneData,0,0,paletteLength);
    }
    IEnumerator LoadAnimalModel(FaunaPrototypeProfile profile)
    {
        string path=Path.Combine(Application.streamingAssetsPath,"fauna",profile.bundle);
        if(!File.Exists(path)){animalLoadFailures.Add(profile.slug);Debug.LogWarning("Missing fauna bundle "+profile.slug);animalLoading=null;yield break;}
        animalBundleRequest=AssetBundle.LoadFromFileAsync(path);yield return animalBundleRequest;
        animalPendingBundle=animalBundleRequest.assetBundle;animalBundleRequest=null;
        if(animalDisposed){animalPendingBundle?.Unload(true);animalPendingBundle=null;yield break;}
        if(animalPendingBundle==null){animalLoadFailures.Add(profile.slug);animalLoading=null;yield break;}
        var request=animalPendingBundle.LoadAllAssetsAsync<WorldGenFaunaAsset>();yield return request;
        var asset=request.allAssets.OfType<WorldGenFaunaAsset>().FirstOrDefault();
        if(asset==null||asset.Slug!=profile.slug||asset.BoneCount>AnimalBones||asset.Clips==null||asset.Clips.Length==0)
        {animalPendingBundle.Unload(true);animalPendingBundle=null;animalLoadFailures.Add(profile.slug);animalLoading=null;yield break;}
        var material=new Material(animalShader){name="Wildlife "+profile.slug};
        material.SetTexture("_Coats",asset.Coats);material.SetTexture("_Mask",asset.Mask);material.SetTexture("_Normals",asset.Normal);material.SetFloat("_VegetationSceneEnabled",1);
        animalModels.Add(profile.slug,new AnimalModel{Asset=asset,Bundle=animalPendingBundle,Material=material,LastUse=Time.realtimeSinceStartupAsDouble});
        animalPendingBundle=null;animalLoading=null;
    }
    bool EvictAnimalModel()
    {
        var old=animalModels.Where(p=>p.Value.Users==0).OrderBy(p=>p.Value.LastUse).FirstOrDefault();if(old.Value==null)return false;
        Destroy(old.Value.Material);old.Value.Bundle.Unload(true);animalModels.Remove(old.Key);return true;
    }
    bool AddAnimal(FaunaPrototypeSpawn spawn,AnimalModel model,bool manual=false)
    {
        if(!GrowAnimalPalette())return false;
        var root=new GameObject(spawn.Profile.name,typeof(MeshFilter),typeof(MeshRenderer));root.layer=30;root.transform.SetParent(transform,false);
        root.transform.localScale=Vector3.one*(float)spawn.Profile.lengthM;
        root.GetComponent<MeshFilter>().sharedMesh=model.Asset.Mesh;var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=model.Material;
        renderer.shadowCastingMode=spawn.Profile.lengthM>=.35?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
        renderer.motionVectorGenerationMode=MotionVectorGenerationMode.Camera;
        var rng=new Rng(sourceWorld.Recipe.Seed,"local-fauna-wander-1",unchecked((int)spawn.Hash));
        var animal=new Animal{Spawn=spawn,Model=model,Manual=manual,Root=root,Renderer=renderer,Slot=animalSlots.Pop(),Random=rng,Props=new MaterialPropertyBlock(),
            Heading=(float)(rng.Unit()*360),Time=rng.Unit()*7,GroundY=frame.SurfaceY(spawn.Position,spawn.GroundM)};
        TerrainHandoff.Coordinates(frame,spawn.Position,out animal.X,out animal.Z);animal.HomeX=animal.X;animal.HomeZ=animal.Z;animal.Planet=spawn.Position;
        animal.TargetHeading=animal.Heading;animal.Random=rng;
        int coat=ChooseAnimalCoat(model.Asset,spawn);var options=model.Asset.Variations[coat].options;
        int option=(int)(spawn.Hash%(uint)options.Length);
        if(spawn.Profile.slug=="deer")option=(spawn.Hash&1)==0?0:1+(int)((spawn.Hash>>1)%(uint)Math.Max(1,options.Length-1));
        var parts=options[option].parts;animal.Parts=new Vector4(parts[0],parts[1],parts[2],parts[3]);
        animal.Props.SetFloat("_Coat",coat);animal.Props.SetInt("_PaletteOffset",animal.Slot*AnimalBones);animal.Props.SetVector("_Parts",animal.Parts);
        animals.Add(spawn.Id,animal);model.Users++;model.LastUse=Time.realtimeSinceStartupAsDouble;
        DecideAnimal(animal);UpdateAnimal(animal,0);
        var corpse=survival?.Corpses.FirstOrDefault(c=>c.SpawnId==spawn.Id);if(corpse!=null){animal.Dead=true;StartAnimalRagdoll(animal,corpse,Vector3.zero);}return true;
    }
    static int ChooseAnimalCoat(WorldGenFaunaAsset asset,FaunaPrototypeSpawn spawn)
    {
        var pool=new List<int>();for(int i=0;i<asset.Variations.Length;i++)
        {
            string name=asset.Variations[i].label.ToLowerInvariant();bool pale=name.Contains("white")||name.Contains("snow")||name.Contains("winter");
            if(name.Contains("fawn")||name.Contains("juvenile"))continue;
            bool male=(spawn.Hash&2)!=0;
            if(name.Contains("female")&&male||name.Contains(" male")&&!name.Contains("female")&&!male)continue;
            if(pale&&spawn.ColdC>0)continue;
            if(spawn.Aridity<.5&&(name.Contains("sand")||name.Contains("dry")||name.Contains("dun")))pool.Add(i);
            else if(spawn.ColdC< -8&&pale)pool.Add(i);
            else if(!pale)pool.Add(i);
        }
        if(pool.Count==0)return 0;return pool[(int)(Rng.Mix(unchecked((uint)spawn.CoatSeed))%(uint)pool.Count)];
    }
    void DecideAnimal(Animal animal)
    {
        var profile=animal.Spawn.Profile;var rng=animal.Random;double choice=rng.Unit();string role;
        if(profile.medium=="air")role=choice<.35&&animal.Model.Asset.Clips.Any(c=>c.role=="glide")?"glide":"fly";
        else if(profile.medium=="water")role="swim";
        else role=choice<.25?"idle":choice<.4?"run":choice<.6&&animal.Model.Asset.Clips.Any(c=>c.role=="trot")?"trot":"walk";
        var clip=animal.Model.Asset.Clips.FirstOrDefault(c=>c.role==role)??animal.Model.Asset.Clips.FirstOrDefault(c=>c.role=="walk")??animal.Model.Asset.Clips.First(c=>c.role=="idle");
        float speed=clip.role=="idle"?0:clip.role=="run"?(float)profile.runMps:clip.role=="trot"?(float)((profile.walkMps+profile.runMps)*.5):(float)profile.walkMps;
        animal.TargetSpeed=speed*(float)(.85+.3*rng.Unit());animal.NominalSpeed=Math.Max(.01f,speed);
        animal.TargetHeading=animal.Heading+(float)((rng.Unit()-.5)*240);
        double dx=animal.HomeX-animal.X,dz=animal.HomeZ-animal.Z;
        if(dx*dx+dz*dz>Math.Pow(Math.Min(profile.radiusM*.45,Math.Max(8,profile.lengthM*20)),2))animal.TargetHeading=(float)(Math.Atan2(dx,dz)*180/Math.PI)+(float)((rng.Unit()-.5)*35);
        if(animal.Clip!=clip)
        {
            animal.OldClip=animal.Clip;animal.OldTime=animal.Time;animal.BlendAge=animal.Clip==null?1:0;
            animal.Time=animal.Clip==null?animal.Time:animal.Time/animal.Clip.duration*clip.duration;animal.Clip=clip;
        }
        animal.NextDecision=animal.Life+2+rng.Unit()*6;animal.Random=rng;
    }
    void UpdateAnimal(Animal animal,float dt)
    {
        if(animal.Dead){UpdateDeadAnimal(animal);return;}
        var profile=animal.Spawn.Profile;animal.Life+=dt;if(animal.Life>=animal.NextDecision)DecideAnimal(animal);
        float turn=profile.tiny?200:profile.medium=="air"?65:profile.medium=="water"?45:85;
        animal.Heading=Mathf.MoveTowardsAngle(animal.Heading,animal.TargetHeading,turn*dt);
        animal.Speed=Mathf.MoveTowards(animal.Speed,animal.TargetSpeed,Math.Max(.15f,(float)profile.runMps)*dt*1.5f);
        double angle=animal.Heading*Math.PI/180,dx=Math.Sin(angle)*animal.Speed*dt,dz=Math.Cos(angle)*animal.Speed*dt;
        double x=animal.X+dx,z=animal.Z+dz;
        // Refresh the occupied ground before testing a proposed step. Otherwise
        // a terrain LOD change could be mistaken for an impassable step forever,
        // leaving a stationary animal suspended at the old surface height.
        double currentFloor=VisibleGround(animal.X,animal.Z,animal.GroundY);
        double floor=VisibleGround(x,z,currentFloor),vertical=currentFloor;
        V point=frame.Position(x,z);double water=double.IsNaN(animal.Spawn.WaterM)?double.NaN:frame.SurfaceY(point,animal.Spawn.WaterM);
        bool blocked=profile.medium=="ground"&&(Math.Abs(floor-currentFloor)>Math.Max(.12,Math.Sqrt(dx*dx+dz*dz)*1.2)||!double.IsNaN(water)&&floor<water-.04)
            ||profile.medium=="water"&&(double.IsNaN(water)||floor>water-Math.Max(.2,profile.lengthM*animal.Model.Asset.Height*.6));
        if(blocked){animal.TargetHeading+=140;animal.TargetSpeed=0;animal.NextDecision=Math.Min(animal.NextDecision,animal.Life+.7);x=animal.X;z=animal.Z;point=animal.Planet;floor=currentFloor;}
        animal.X=x;animal.Z=z;animal.Planet=point;animal.GroundY=floor;
        if(profile.medium=="air")vertical=floor+animal.Spawn.HeightM+Math.Sin(animal.Life*(profile.tiny?1.8:.45)+animal.Spawn.Hash%31)*(profile.tiny?.15:.65);
        else if(profile.medium=="water")vertical=Math.Max(floor+.1,water+animal.Spawn.HeightM-profile.lengthM*animal.Model.Asset.Height*.5);
        else vertical=floor;
        var position=new Vector3((float)(x-originX),(float)vertical,(float)(z-originZ));
        animal.Root.transform.localPosition=position;animal.Root.transform.localRotation=Quaternion.Euler(0,animal.Heading,0);
        double rate=animal.Clip.role=="idle"?1:Math.Max(.2,Math.Min(1.8,animal.Speed/animal.NominalSpeed));
        animal.Time+=dt*rate;animal.OldTime+=dt*rate;animal.BlendAge+=dt;
        var asset=animal.Model.Asset;float blend=Mathf.SmoothStep(0,1,(float)(animal.BlendAge/.55));
        float morph=AnimalMorph(asset,animal.Clip,animal.Time);
        if(animal.OldClip!=null&&blend<1)morph=Mathf.Lerp(AnimalMorph(asset,animal.OldClip,animal.OldTime),morph,blend);
        animal.Props.SetFloat("_Morph",morph);
        for(int bone=0;bone<asset.BoneCount;bone++)
        {
            var pose=AnimalPose(asset,animal.Clip,animal.Time,bone);
            if(animal.OldClip!=null&&blend<1)pose=AnimalMatrixLerp(AnimalPose(asset,animal.OldClip,animal.OldTime,bone),pose,blend);
            animalBoneData[animal.Slot*AnimalBones+bone]=pose;
        }
        float distance=(position-view.transform.localPosition).magnitude;
        animal.Props.SetFloat("_Fade",animal.Fade*Mathf.Clamp01(((float)profile.radiusM-distance)/Math.Max(4,(float)profile.radiusM*.12f)));
        animal.Props.SetFloat("_Glow",profile.derivedFirefly?8*Mathf.Pow(Mathf.Max(0,Mathf.Sin((float)animal.Life*2+(animal.Spawn.Hash%47))),8):0);
        animal.Renderer.SetPropertyBlock(animal.Props);animal.Renderer.shadowCastingMode=profile.lengthM>=.35&&distance<180?ShadowCastingMode.On:ShadowCastingMode.Off;
    }
    static Matrix4x4 AnimalPose(WorldGenFaunaAsset asset,FaunaRuntimeClip clip,double time,int bone)
    {
        double frame=(time/clip.duration%1)*clip.frames;int first=(int)frame%clip.frames,next=(first+1)%clip.frames;
        return AnimalMatrixLerp(asset.Poses[(clip.start+first)*asset.BoneCount+bone],asset.Poses[(clip.start+next)*asset.BoneCount+bone],(float)(frame-Math.Floor(frame)));
    }
    static float AnimalMorph(WorldGenFaunaAsset asset,FaunaRuntimeClip clip,double time)
    {
        if(asset.Morphs==null||asset.Morphs.Length==0)return 0;
        double frame=(time/clip.duration%1)*clip.frames;int first=(int)frame%clip.frames,next=(first+1)%clip.frames;
        return Mathf.Lerp(asset.Morphs[clip.start+first],asset.Morphs[clip.start+next],(float)(frame-Math.Floor(frame)));
    }
    static Matrix4x4 AnimalMatrixLerp(Matrix4x4 a,Matrix4x4 b,float t)
    {var result=new Matrix4x4();for(int i=0;i<16;i++)result[i]=a[i]+(b[i]-a[i])*t;return result;}
    void RemoveAnimal(ulong id)
    {
        RemoveAnimalRagdoll(id);var animal=animals[id];Destroy(animal.Root);animal.Model.Users--;animal.Model.LastUse=Time.realtimeSinceStartupAsDouble;animalSlots.Push(animal.Slot);animals.Remove(id);
    }
    void DisposeAnimalFauna()
    {
        animalDisposed=true;
        if(animalBundleRequest!=null)
        {var request=animalBundleRequest;if(request.isDone)request.assetBundle?.Unload(true);else request.completed+=_=>request.assetBundle?.Unload(true);}
        if(animalLoading!=null)StopCoroutine(animalLoading);animalLoading=null;animalPendingBundle?.Unload(true);animalPendingBundle=null;
        foreach(var id in animals.Keys.ToArray())RemoveAnimal(id);
        foreach(var model in animalModels.Values){Destroy(model.Material);if(model.Bundle!=null)model.Bundle.Unload(true);}animalModels.Clear();
        if(ReferenceEquals(animalSceneOwner,this)){Shader.SetGlobalBuffer("_FaunaBones",(GraphicsBuffer)null);animalSceneOwner=null;}
        animalBoneBuffer?.Dispose();animalBoneBuffer=null;
    }
}

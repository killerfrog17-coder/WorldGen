using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    WildlifeState Wildlife=>PlayerRules.Wildlife??(PlayerRules.Wildlife=new WildlifeState());
    Dictionary<ulong,WildlifeIndividual> wildlifeRecords;Dictionary<string,FaunaPhysicsSpecies> wildlifeSkeletons;
    Dictionary<string,double> wildlifeMasses;
    void InitializeWildlifeHealth()
    {
        EnsureWildlifePopulationVersion();
        if(wildlifeRecords==null){wildlifeRecords=Wildlife.Individuals.ToDictionary(p=>p.Id);foreach(var saved in Wildlife.Individuals.Where(p=>p.Manual))animalManualSequence=Math.Max(animalManualSequence,saved.Id);}
        if(wildlifeSkeletons==null){var json=Resources.Load<TextAsset>("FaunaPhysicsSkeletons");wildlifeSkeletons=json==null?new Dictionary<string,FaunaPhysicsSpecies>():JsonUtility.FromJson<FaunaPhysicsCatalogue>(json.text).species.ToDictionary(p=>p.slug);}
        if(wildlifeMasses==null)
        {
            wildlifeMasses=new Dictionary<string,double>();
            if(sourceWorld?.Ecology!=null)foreach(var pair in FaunaPrototypePlanner.BindProfiles(sourceWorld.Ecology.Taxa,animalProfiles))
                if(!wildlifeMasses.ContainsKey(pair.Value.slug))wildlifeMasses[pair.Value.slug]=Math.Max(.00001,sourceWorld.Ecology.Taxa[pair.Key].MassKg);
        }
    }
    void InitializeAnimalHealth(Animal animal)
    {
        InitializeWildlifeHealth();var profile=animal.Spawn.Profile;
        animal.MassKg=animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[animal.Spawn.Taxon].MassKg:wildlifeMasses.TryGetValue(profile.slug,out var mass)?mass:Math.Max(.0001,8*Math.Pow(profile.lengthM,3));
        animal.MaximumHealth=ItemUseRules.HealthForMass(animal.MassKg);animal.Health=animal.MaximumHealth;
        var natural=animal.Behaviour=AnimalBehaviour(profile,animal.Spawn.Taxon);string nativeName=animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[animal.Spawn.Taxon].Name:"";
        animal.Senses=WildlifeSenses.Describe(profile.slug,animal.MassKg,natural.Predator,profile.medium=="air",profile.tiny,nativeName);animal.EffortProfile=ExertionProfile.Animal(profile.slug,animal.MassKg,profile.medium);
        animal.Senses.Individual(animal.Spawn.Hash);
        if(wildlifeRecords.TryGetValue(animal.Spawn.Id,out var saved)&&saved.Species==profile.slug)
        {
            animal.Manual=saved.Manual;
            animal.Health=saved.Health;animal.MaximumHealth=saved.MaximumHealth;animal.Planet=saved.Position;TerrainHandoff.Coordinates(frame,saved.Position,out animal.X,out animal.Z);
            animal.Heading=animal.TargetHeading=(float)saved.Heading;animal.CorpseHeightOffset=saved.CorpseHeightOffsetM;animal.Props.SetFloat("_Morph",(float)saved.Morph);
            animal.Exertion=saved.Exertion?.Copy()??new ExertionState();animal.Awareness=saved.Awareness?.Copy()??new WildlifeAwarenessState();
            if(saved.ThreatPosition.Length>.9)TerrainHandoff.Coordinates(frame,saved.ThreatPosition,out animal.Awareness.LastX,out animal.Awareness.LastZ);
            double away=Math.Max(0,Math.Min(3600,(PlayerRules.SolarDays-saved.LastSeenDays)*PlayerRules.Settings.RealMinutesPerDay*60));
            ExertionRules.Advance(animal.Exertion,animal.EffortProfile,away,animal.Awareness.MemorySeconds>0?.25:0,CurrentWeather.TemperatureC);
            animal.Awareness.MemorySeconds=Math.Max(0,animal.Awareness.MemorySeconds-away);animal.Awareness.LastCueSeconds+=away;animal.Awareness.Suspicion=Math.Max(0,animal.Awareness.Suspicion-away/150);
            animal.HuntTargetId=saved.HuntTargetId;animal.HuntMemoryUntil=animal.Life+Math.Max(0,saved.HuntMemorySeconds-away);
            if(saved.HuntLastPosition.Length>.9)TerrainHandoff.Coordinates(frame,saved.HuntLastPosition,out animal.HuntX,out animal.HuntZ);
        }
    }
    Matrix4x4[] AnimalPalette(Animal animal)
    {var result=new Matrix4x4[animal.Model.Asset.BoneCount];Array.Copy(animalBoneData,animal.Slot*AnimalBones,result,0,result.Length);return result;}
    void BeginAnimalCorpse(Animal animal,Vector3 impulse,bool restored=false)
    {
        if(animal.Ragdoll!=null)return;InitializeWildlifeHealth();var palette=AnimalPalette(animal);
        if(restored)
        {
            if(wildlifeRecords.TryGetValue(animal.Spawn.Id,out var saved)&&saved.CorpsePose?.Length==palette.Length*16)
                for(int i=0;i<palette.Length;i++)for(int j=0;j<16;j++)palette[i][j]=(float)saved.CorpsePose[i*16+j];
            else for(int i=0;i<palette.Length;i++)palette[i]=AnimalPose(animal.Model.Asset,animal.Clip,animal.Time,i);
        }
        Array.Copy(palette,0,animalBoneData,animal.Slot*AnimalBones,palette.Length);
        animal.Root.transform.localPosition=new Vector3((float)(animal.X-originX),(float)(VisibleGround(animal.X,animal.Z,animal.GroundY)+animal.CorpseHeightOffset),(float)(animal.Z-originZ));animal.Root.transform.localRotation=Quaternion.Euler(0,animal.Heading,0);
        animal.Ragdoll=gameObject.AddComponent<WorldGenFaunaRagdoll>();wildlifeSkeletons.TryGetValue(animal.Spawn.Profile.slug,out var skeleton);
        animal.CollisionFrame=-1;
        animal.Ragdoll.Begin(animal.Root.transform,skeleton,palette,animal.MassKg,impulse,(x,z)=>(float)VisibleGround(x+originX,z+originZ,Ground(x+originX,z+originZ)),restored&&wildlifeRecords.TryGetValue(animal.Spawn.Id,out var record)&&record.CorpsePose!=null&&record.CorpseSettled,AnimalSurface(animal),PosedAnimal(animal),animal.Parts,coarse:animal.Spawn.Profile.medium=="air");
        animal.TargetSpeed=animal.Speed=0;RememberAnimal(animal,true);
    }
    void UpdateAnimalCorpse(Animal animal,float dt)
    {
        if(animal.Ragdoll==null)BeginAnimalCorpse(animal,Vector3.zero,true);
        animal.Ragdoll.SetPaused(paused||mapOpen||ecologyOpen||chatOpen);
        animal.Root.transform.localPosition=new Vector3((float)(animal.X-originX),(float)(VisibleGround(animal.X,animal.Z,animal.GroundY)+animal.CorpseHeightOffset),(float)(animal.Z-originZ));
        animal.Ragdoll.Synchronize(animalBoneData,animal.Slot*AnimalBones);
        animal.Props.SetFloat("_Fade",animal.Fade);animal.Renderer.SetPropertyBlock(animal.Props);
        var bounds=animal.Ragdoll.LocalBounds;bounds.Expand(animal.Model.Asset.Mesh.bounds.extents);animal.Renderer.localBounds=bounds;
    }
    void DamageAnimal(Animal animal,double damage,Vector3 point,Vector3 direction,double momentum=0,bool fromPlayer=false)
    {
        if(animal==null||animal.Dead||!ItemUseRules.Finite(damage)||damage<=0)return;
        animal.Health=Math.Max(0,animal.Health-damage);PaintAnimalBlood(animal,point,-direction,damage);
        AnimalVoiceEvent(animal,"pain");
        if(animal.MassKg>.05)PlayItemEffect("body-hit",point,Mathf.Clamp((float)Math.Pow(animal.MassKg/70,.2),.08f,.65f),fromPlayer:fromPlayer);
        if(animal.Dead)
        {animal.CorpseHeightOffset=animal.Root.transform.localPosition.y-VisibleGround(animal.X,animal.Z,animal.GroundY);Vector3 movement=new Vector3(Mathf.Sin(animal.Heading*Mathf.Deg2Rad),0,Mathf.Cos(animal.Heading*Mathf.Deg2Rad))*animal.Speed;BeginAnimalCorpse(animal,movement+direction*(float)Math.Min(5,momentum/Math.Max(.01,animal.MassKg)));}
        else
        {
            var source=transform.InverseTransformPoint(point-direction*(float)Math.Max(2,animal.Spawn.Profile.lengthM));animal.ThreatUntil=animal.Life+12;animal.ThreatX=source.x+originX;animal.ThreatZ=source.z+originZ;animal.ThreatWasPlayer=fromPlayer;
            WildlifePerception.Alarm(animal.Awareness,animal.ThreatX,animal.ThreatZ,true);animal.Awareness.ThreatIsPlayer=fromPlayer;animal.BehaviourAt=0;animal.ActionUntil=0;
            animal.TargetHeading=(float)(Math.Atan2(animal.X-animal.ThreatX,animal.Z-animal.ThreatZ)*180/Math.PI);animal.TargetSpeed=(float)animal.Spawn.Profile.runMps;animal.NextDecision=animal.ThreatUntil;
            var flee=animal.Model.Asset.Clips.FirstOrDefault(c=>c.role==(animal.Spawn.Profile.medium=="air"?"fly":animal.Spawn.Profile.medium=="water"?"swim":"run"));
            if(flee!=null&&flee!=animal.Clip){animal.OldClip=animal.Clip;animal.OldTime=animal.Time;animal.Clip=flee;animal.BlendAge=0;animal.Time=0;animal.NominalSpeed=Math.Max(.01f,(float)animal.Spawn.Profile.ClipSpeed(flee.role));}
        }
        RememberAnimal(animal,false);
    }
    void RememberAnimal(Animal animal,bool pose)
    {
        InitializeWildlifeHealth();if(!wildlifeRecords.TryGetValue(animal.Spawn.Id,out var saved))
        {saved=new WildlifeIndividual{Id=animal.Spawn.Id,Species=animal.Spawn.Profile.slug};Wildlife.Individuals.Add(saved);wildlifeRecords.Add(saved.Id,saved);}
        saved.Position=animal.Planet;saved.Heading=animal.Heading;saved.Health=animal.Health;saved.MaximumHealth=animal.MaximumHealth;saved.Coat=animal.Coat;saved.Manual=animal.Manual;saved.LastSeenDays=PlayerRules.SolarDays;
        saved.GroupId=animal.Spawn.GroupId;saved.GroupIndex=animal.Spawn.GroupIndex;saved.GroupSize=animal.Spawn.GroupSize;saved.Taxon=animal.Spawn.Taxon;
        saved.Exertion=animal.Exertion.Copy();saved.Awareness=animal.Awareness.Copy();saved.ThreatPosition=frame.Position(animal.Awareness.LastX,animal.Awareness.LastZ);
        saved.HuntTargetId=animal.HuntTargetId;saved.HuntMemorySeconds=Math.Max(0,Math.Min(300,animal.HuntMemoryUntil-animal.Life));
        if(saved.HuntMemorySeconds>0)saved.HuntLastPosition=frame.Position(animal.HuntX,animal.HuntZ);
        saved.CorpseHeightOffsetM=animal.CorpseHeightOffset;saved.CorpseSettled=animal.Ragdoll?.Settled??false;saved.Morph=animal.Props.GetFloat("_Morph");
        saved.Parts=new[]{(int)animal.Parts.x,(int)animal.Parts.y,(int)animal.Parts.z,(int)animal.Parts.w};
        if(pose&&animal.Dead){saved.CorpsePose=new double[animal.Model.Asset.BoneCount*16];for(int i=0;i<animal.Model.Asset.BoneCount;i++)for(int j=0;j<16;j++)saved.CorpsePose[i*16+j]=animalBoneData[animal.Slot*AnimalBones+i][j];}
        CaptureAnimalBlood(animal,saved);
    }
    void CaptureWildlife()
    {foreach(var animal in animals.Values)if(animal.Dead||animal.Health<animal.MaximumHealth||animal.Awareness.Suspicion>.15||animal.HuntMemoryUntil>animal.Life||animal.Exertion.Burst<.95||animal.Exertion.Fatigue>.01||wildlifeRecords?.ContainsKey(animal.Spawn.Id)==true)RememberAnimal(animal,true);}
    void MergeSavedWildlifeCandidates(List<FaunaPrototypeSpawn> candidates)
    {
        // Injured individuals and corpses are physical saved objects, not a new
        // ecological population proposal. Their current location must survive
        // season changes, group travel and the original spawn cell leaving view.
        InitializeWildlifeHealth();var ids=new HashSet<ulong>(candidates.Select(p=>p.Id));
        var bindings=FaunaPrototypePlanner.BindProfiles(sourceWorld.Ecology.Taxa,animalProfiles);
        foreach(var record in Wildlife.Individuals)
        {
            var profile=animalProfiles.FirstOrDefault(p=>p.slug==record.Species);
            if(profile==null||(record.Position-Position).Length*frame.RadiusM>profile.VisibleRadiusM)continue;
            if(ids.Contains(record.Id))
            {candidates.First(p=>p.Id==record.Id).Position=record.Position;continue;}
            var sample=walkingPatch.Sample(record.Position);var habitat=sourceWorld.Ecology.Habitat[sample.Cell];
            int taxon=record.Taxon>=0?record.Taxon:bindings.FirstOrDefault(p=>p.Value.slug==record.Species).Key;
            if(!bindings.ContainsKey(taxon)||bindings[taxon].slug!=record.Species)taxon=-1;
            uint hash=Rng.Mix((uint)record.Id^(uint)(record.Id>>32));
            candidates.Add(new FaunaPrototypeSpawn{Id=record.Id,Hash=hash,Profile=profile,Position=record.Position,GroupHome=record.Position,GroupId=record.GroupId,GroupIndex=record.GroupIndex,GroupSize=Math.Max(1,record.GroupSize),
                Taxon=taxon,Region=sourceWorld.Ecology.Region[sample.Cell],CoatSeed=unchecked((int)hash),GroundM=sample.ElevationM,WaterM=sample.WaterSurfaceM,
                HeightM=profile.medium=="air"?Math.Max(.2,record.CorpseHeightOffsetM):profile.medium=="water"?-.6:0,ColdC=habitat.ColdTenthsC/10.0,Aridity=habitat.AridityMilli/1000.0});
        }
    }
    partial void PaintAnimalBlood(Animal animal,Vector3 point,Vector3 normal,double damage);
    partial void CaptureAnimalBlood(Animal animal,WildlifeIndividual saved);
    partial void ClearAnimalBlood(Animal animal);
}

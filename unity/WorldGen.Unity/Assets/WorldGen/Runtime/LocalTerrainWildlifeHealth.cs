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
        if(wildlifeRecords.TryGetValue(animal.Spawn.Id,out var saved)&&saved.Species==profile.slug)
        {
            animal.Health=saved.Health;animal.MaximumHealth=saved.MaximumHealth;animal.Planet=saved.Position;TerrainHandoff.Coordinates(frame,saved.Position,out animal.X,out animal.Z);
            animal.Heading=animal.TargetHeading=(float)saved.Heading;
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
        animal.Root.transform.localPosition=new Vector3((float)(animal.X-originX),(float)VisibleGround(animal.X,animal.Z,animal.GroundY),(float)(animal.Z-originZ));animal.Root.transform.localRotation=Quaternion.Euler(0,animal.Heading,0);
        animal.Ragdoll=gameObject.AddComponent<WorldGenFaunaRagdoll>();wildlifeSkeletons.TryGetValue(animal.Spawn.Profile.slug,out var skeleton);
        animal.Ragdoll.Begin(animal.Root.transform,skeleton,palette,animal.MassKg,impulse,(x,z)=>(float)Ground(x+originX,z+originZ),restored&&wildlifeRecords.TryGetValue(animal.Spawn.Id,out var record)&&record.CorpsePose!=null);
        animal.TargetSpeed=animal.Speed=0;RememberAnimal(animal,true);
    }
    void UpdateAnimalCorpse(Animal animal,float dt)
    {
        if(animal.Ragdoll==null)BeginAnimalCorpse(animal,Vector3.zero,true);
        animal.Ragdoll.SetPaused(paused||mapOpen||ecologyOpen||chatOpen||inventoryOpen&&!CraftWorking);
        animal.Root.transform.localPosition=new Vector3((float)(animal.X-originX),(float)VisibleGround(animal.X,animal.Z,animal.GroundY),(float)(animal.Z-originZ));
        animal.Ragdoll.Synchronize(animalBoneData,animal.Slot*AnimalBones);
        animal.Props.SetFloat("_Fade",animal.Fade);animal.Renderer.SetPropertyBlock(animal.Props);
        animal.Renderer.localBounds=new Bounds(animal.Model.Asset.Mesh.bounds.center,animal.Model.Asset.Mesh.bounds.size*3+Vector3.one*.5f);
    }
    void DamageAnimal(Animal animal,double damage,Vector3 point,Vector3 direction,double momentum=0)
    {
        if(animal==null||animal.Dead||!ItemUseRules.Finite(damage)||damage<=0)return;
        animal.Health=Math.Max(0,animal.Health-damage);PaintAnimalBlood(animal,point,-direction,damage);
        if(animal.Dead)
        {Vector3 movement=new Vector3(Mathf.Sin(animal.Heading*Mathf.Deg2Rad),0,Mathf.Cos(animal.Heading*Mathf.Deg2Rad))*animal.Speed;BeginAnimalCorpse(animal,movement+direction*(float)Math.Min(5,momentum/Math.Max(.01,animal.MassKg)));}
        else
        {
            animal.ThreatUntil=animal.Life+12;animal.ThreatX=motion.X;animal.ThreatZ=motion.Z;
            animal.TargetHeading=(float)(Math.Atan2(animal.X-motion.X,animal.Z-motion.Z)*180/Math.PI);animal.TargetSpeed=(float)animal.Spawn.Profile.runMps;animal.NextDecision=animal.ThreatUntil;
            var flee=animal.Model.Asset.Clips.FirstOrDefault(c=>c.role==(animal.Spawn.Profile.medium=="air"?"fly":animal.Spawn.Profile.medium=="water"?"swim":"run"));
            if(flee!=null&&flee!=animal.Clip){animal.OldClip=animal.Clip;animal.OldTime=animal.Time;animal.Clip=flee;animal.BlendAge=0;animal.Time=0;animal.NominalSpeed=Math.Max(.01f,(float)animal.Spawn.Profile.runMps);}
        }
        RememberAnimal(animal,false);
    }
    void RememberAnimal(Animal animal,bool pose)
    {
        InitializeWildlifeHealth();if(!wildlifeRecords.TryGetValue(animal.Spawn.Id,out var saved))
        {saved=new WildlifeIndividual{Id=animal.Spawn.Id,Species=animal.Spawn.Profile.slug};Wildlife.Individuals.Add(saved);wildlifeRecords.Add(saved.Id,saved);}
        saved.Position=animal.Planet;saved.Heading=animal.Heading;saved.Health=animal.Health;saved.MaximumHealth=animal.MaximumHealth;saved.Coat=animal.Coat;saved.Manual=animal.Manual;saved.LastSeenDays=PlayerRules.SolarDays;
        saved.Parts=new[]{(int)animal.Parts.x,(int)animal.Parts.y,(int)animal.Parts.z,(int)animal.Parts.w};
        if(pose&&animal.Dead){saved.CorpsePose=new double[animal.Model.Asset.BoneCount*16];for(int i=0;i<animal.Model.Asset.BoneCount;i++)for(int j=0;j<16;j++)saved.CorpsePose[i*16+j]=animalBoneData[animal.Slot*AnimalBones+i][j];}
        CaptureAnimalBlood(animal,saved);
    }
    void CaptureWildlife()
    {foreach(var animal in animals.Values)if(animal.Dead||animal.Health<animal.MaximumHealth||wildlifeRecords?.ContainsKey(animal.Spawn.Id)==true)RememberAnimal(animal,true);}
    partial void PaintAnimalBlood(Animal animal,Vector3 point,Vector3 normal,double damage);
    partial void CaptureAnimalBlood(Animal animal,WildlifeIndividual saved);
    partial void ClearAnimalBlood(Animal animal);
}

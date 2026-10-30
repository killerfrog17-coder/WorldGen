using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class CampVisual {public GameObject Root;public AudioSource Sound;public ParticleSystem Smoke;public Quaternion SurfaceRotation;public int GroundLevel=-1;public double Mass;public bool Burning,Glowing;public double Embed;public int FoodState;public MeshRenderer[] FlameRenderers;public MaterialPropertyBlock FlameProperties=new MaterialPropertyBlock();}
    static int CampFoodState(CampObject c){int hash=17;foreach(var p in c.CookingItems??Enumerable.Empty<Possession>())hash=unchecked(hash*31+p.Id.GetHashCode()+(p.Cooked?1:0)+(p.Burnt?2:0));return hash;}
    readonly Dictionary<string,CampVisual> campVisuals=new Dictionary<string,CampVisual>();
    Material campFlameMaterial,campEmberMaterial;
    readonly Vector4[] campLightPositions=new Vector4[4],campLightColours=new Vector4[4];bool campLightTestOff;int campLightCount;
    Quaternion CampSurfaceRotation(CampObject c)
    {
        var yaw=Quaternion.Euler(0,(float)c.Heading,0);if(c.Kind!=CampKind.Bedding)return yaw;
        TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);
        var normal=new Vector3((float)(CampFloor(x-.5,z)-CampFloor(x+.5,z)),1,(float)(CampFloor(x,z-.5)-CampFloor(x,z+.5))).normalized;
        return Quaternion.FromToRotation(Vector3.up,normal)*yaw;
    }
    void BindCampLights(Camera camera)
    {
        campLightCount=0;
        if(camera==view&&!campLightTestOff)
        foreach(var c in Camp.Objects.Where(c=>c.Kind==CampKind.Fire&&c.FirePowerKw>0).OrderBy(c=>(c.Position-Position).Length))
        {
            if((c.Position-Position).Length*frame.RadiusM>18)continue;
            TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);var p=transform.TransformPoint(new Vector3((float)(x-originX),(float)CampFloor(x,z)+.45f,(float)(z-originZ)));
            campLightPositions[campLightCount]=new Vector4(p.x,p.y,p.z,81);float intensity=(float)c.FirePowerKw*3*Mathf.Lerp(1,.08f,(float)(SkyFrame?.Daylight??0))*(.94f+.06f*Mathf.Sin(Time.time*13.7f));campLightColours[campLightCount]=new Vector4(intensity,intensity*.34f,intensity*.075f,1);if(++campLightCount==4)break;
        }
        Shader.SetGlobalInteger("_WorldGenCampLightCount",campLightCount);if(campLightCount>0){Shader.SetGlobalVectorArray("_WorldGenCampLightPositions",campLightPositions);Shader.SetGlobalVectorArray("_WorldGenCampLightColours",campLightColours);}
    }
    void InvalidateCampVisual(CampObject c)
        =>RemoveCampVisual(c.Id);
    void RemoveCampVisual(string id)
    {
        if(!campVisuals.TryGetValue(id,out var v))return;
        foreach(var root in v.Root.GetComponentsInChildren<Transform>().Where(t=>inventoryCraftMeshes.ContainsKey(t.gameObject)||portableRoots.ContainsKey(t.gameObject)).ToArray())DestroyInventoryVisual(root.gameObject);
        foreach(var filter in v.Root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&filter.sharedMesh.name.StartsWith("Camp ",StringComparison.Ordinal))Destroy(filter.sharedMesh);
        Destroy(v.Root);campVisuals.Remove(id);
    }
    void UpdateCamp()
    {
        if(Ready&&!campAudioStarted){campAudioStarted=true;campAudioLoading=StartCoroutine(LoadCampRecordings());}
        if(Ready&&!paused&&!mapOpen&&!chatOpen&&!ecologyOpen&&!DeathVisible)Camp.SimulatedSeconds+=Math.Min(.25,Time.unscaledDeltaTime)*CampTimeRate*86400/(PlayerRules.Settings.RealMinutesPerDay*60);
        UpdateCampPlacement();UpdateCampSleep();if(!Ready||Time.unscaledTimeAsDouble<campUpdateAt)return;campUpdateAt=Time.unscaledTimeAsDouble+.2;
        foreach(var c in Camp.Objects)
        {
            double elapsed=Math.Max(0,Camp.SimulatedSeconds-c.LastSimulatedSeconds);c.LastSimulatedSeconds=Camp.SimulatedSeconds;
            if(!paused)CampRules.Advance(c,elapsed,CurrentWeather.RainMmPerHour,CurrentWeather.Humidity,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps),SkyFrame?.Daylight??0);
            bool near=(Position-c.Position).Length*frame.RadiusM<250;
            if(!near){InvalidateCampVisual(c);continue;}
            int foodState=CampFoodState(c);
            if(campVisuals.TryGetValue(c.Id,out var old)&&(Math.Abs(old.Mass-c.MassKg)>.08||old.Burning!=c.Burning||old.Glowing!=(c.EmberSeconds>0)||old.Embed!=c.EmbedM||old.FoodState!=foodState)){InvalidateCampVisual(c);}
            if(!campVisuals.TryGetValue(c.Id,out var v)){v=CreateCampVisual(c);campVisuals.Add(c.Id,v);}
            TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);double visible=VisibleGround(x,z,double.NaN,out int level);float floor=(float)(double.IsNaN(visible)?Ground(x,z):visible);
            if(v.GroundLevel!=level){v.SurfaceRotation=CampSurfaceRotation(c);v.GroundLevel=level;}
            v.Root.transform.localPosition=new Vector3((float)(x-originX),floor,(float)(z-originZ));v.Root.transform.localRotation=v.SurfaceRotation;
            UpdateCampVoice(c,v);
            UpdateCampSmoke(v.Smoke);
            if(c.Kind==CampKind.Fire)
            {
                float strength=Mathf.Clamp01((float)c.FirePowerKw/3);
                foreach(var renderer in v.FlameRenderers)
                {
                    bool flame=renderer.name=="Flame sheet";v.FlameProperties.SetFloat("_Strength",flame?strength:Mathf.Clamp01((float)c.EmberSeconds/1200));renderer.SetPropertyBlock(v.FlameProperties);
                    if(flame){renderer.transform.localScale=new Vector3(.58f*Mathf.Lerp(.25f,1,strength),.7f*Mathf.Lerp(.12f,1,strength),1);renderer.transform.localPosition=Vector3.up*renderer.transform.localScale.y*.5f;}
                }
            }
        }
        RetireCampVoices();
        foreach(var id in campVisuals.Keys.Where(id=>!Camp.Objects.Any(c=>c.Id==id)).ToArray())RemoveCampVisual(id);
    }
    CampVisual CreateCampVisual(CampObject c)
    {
        var root=new GameObject("Placed "+c.Kind);root.transform.SetParent(transform,false);root.layer=30;
        var v=new CampVisual{Root=root,SurfaceRotation=CampSurfaceRotation(c),Mass=c.MassKg,Burning=c.Burning,Glowing=c.EmberSeconds>0,Embed=c.EmbedM,FoodState=CampFoodState(c)};
        if(c.Kind==CampKind.Stake)
        {
            var p=c.Materials[0];var wood=CreatePossessionVisual(p,30);wood.transform.SetParent(root.transform,false);wood.transform.localPosition=Vector3.down*(float)c.EmbedM;
            var collider=root.AddComponent<CapsuleCollider>();collider.radius=(float)p.WidthM*.5f;collider.height=(float)(p.HeightM-c.EmbedM);collider.center=Vector3.up*collider.height*.5f;root.layer=27;
        }
        else if(c.Kind==CampKind.Bedding)
        {
            var mat=new GameObject("Layered loose bedding",typeof(MeshFilter),typeof(MeshRenderer));mat.transform.SetParent(root.transform,false);mat.layer=30;
            mat.GetComponent<MeshFilter>().sharedMesh=LocalScatterMeshes.BuildCampMat((float)c.BeddingDepthM,c.Materials.Any(p=>p.Kind==PossessionKind.LeafLitter),c.Id.GetHashCode());mat.GetComponent<MeshRenderer>().sharedMaterial=PortableMaterial();
            foreach(var p in c.Materials.Where(p=>PortableTechnology.Item(p.Kind)?.Form==PortableForm.Hide))
            {var hide=CreatePossessionVisual(p,30);hide.transform.SetParent(root.transform,false);hide.transform.localRotation=Quaternion.Euler(90,0,0);hide.transform.localPosition=new Vector3(0,(float)c.BeddingDepthM+.025f,0);}
        }
        else
        {
            if(c.AshKg>0)
            {
                var ash=new GameObject("Ash and charcoal",typeof(MeshFilter),typeof(MeshRenderer));ash.layer=30;ash.transform.SetParent(root.transform,false);
                ash.GetComponent<MeshFilter>().sharedMesh=LocalScatterMeshes.BuildCampAsh(Mathf.Clamp((float)Math.Sqrt(c.AshKg)*1.2f,.12f,.44f));ash.GetComponent<MeshRenderer>().sharedMaterial=PortableMaterial();
            }
            int i=0;
            foreach(var p in c.Materials)
            {
                var part=CreatePossessionVisual(p,30);part.transform.SetParent(root.transform,false);float angle=i*2.39996f;
                float radius=WoodCrafting.Wood(p)?.18f:.07f;part.transform.localPosition=new Vector3(Mathf.Sin(angle)*radius,.035f+(i%3)*.018f,Mathf.Cos(angle)*radius);
                part.transform.localRotation=Quaternion.Euler(78+(i%4)*6,i*137,0);
                i++;
            }
            int cooking=0;foreach(var p in c.CookingItems??Enumerable.Empty<Possession>())
            {var visualFood=p.Copy();if(p.Cooked)visualFood.Tint=p.Burnt?new V(.08,.055,.035):new V(.34,.18,.085);var part=CreatePossessionVisual(visualFood,30);part.transform.SetParent(root.transform,false);float angle=cooking++*2.39996f;part.transform.localPosition=new Vector3(Mathf.Sin(angle)*.28f,.16f,Mathf.Cos(angle)*.28f);part.transform.localRotation=Quaternion.Euler(0,cooking*137,0);}
            if(c.Burning||c.EmberSeconds>0)
            {
                if(campEmberMaterial==null){campEmberMaterial=new Material(Resources.Load<Shader>("WorldGenCampFlame"));campEmberMaterial.SetFloat("_EmberMode",1);}
                var ember=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(ember.GetComponent<Collider>());ember.name="Live ember bed";ember.layer=30;ember.transform.SetParent(root.transform,false);ember.transform.localPosition=Vector3.up*.025f;ember.transform.localRotation=Quaternion.Euler(90,0,0);ember.transform.localScale=Vector3.one*.7f;ember.GetComponent<Renderer>().sharedMaterial=campEmberMaterial;ember.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                if(c.Burning)
                {
                    v.Smoke=CreateCampSmoke(root.transform);
                    if(campFlameMaterial==null)campFlameMaterial=new Material(Resources.Load<Shader>("WorldGenCampFlame"));
                    for(int k=0;k<3;k++)
                    {var flame=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(flame.GetComponent<Collider>());flame.name="Flame sheet";flame.layer=30;flame.transform.SetParent(root.transform,false);flame.transform.localPosition=Vector3.up*.35f;flame.transform.localRotation=Quaternion.Euler(0,k*60,0);flame.transform.localScale=new Vector3(.58f,.7f,1);flame.GetComponent<Renderer>().sharedMaterial=campFlameMaterial;flame.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
                }
            }
        }
        v.FlameRenderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Flame sheet"||r.name=="Live ember bed").ToArray();return v;
    }
    void DisposeCamp()
    {CancelCampPlacement();DisposeCampAudio();DisposeBodyFeedback();foreach(var c in Camp.Objects.ToArray())InvalidateCampVisual(c);if(campFlameMaterial!=null)Destroy(campFlameMaterial);if(campEmberMaterial!=null)Destroy(campEmberMaterial);if(campSmokeMaterial!=null)Destroy(campSmokeMaterial);campSleeping=campResting=false;}
}

static partial class LocalScatterMeshes
{
    public static Mesh BuildCampAsh(float radius)
    {
        var b=new Builder();b.Crown(new Vector3(0,.008f,0),new Vector3(radius,.007f,radius),20,4,new Color(.17f,.16f,.14f,0),187,.13f);
        var mesh=b.Finish();mesh.name="Camp ash";mesh.SetUVs(1,Enumerable.Repeat(Vector2.zero,mesh.vertexCount).ToList());return mesh;
    }
    public static Mesh BuildCampMat(float depth,bool leaves,int seed)
    {
        var b=new Builder();var random=new System.Random(seed);depth=Mathf.Max(.012f,depth);
        for(int i=0;i<1100;i++)
        {
            float x=(float)(random.NextDouble()*2-1)*.48f,z=(float)(random.NextDouble()*2-1)*.95f,y=(float)random.NextDouble()*depth;
            float angle=i%3==0||leaves?(float)random.NextDouble()*Mathf.PI*2:(float)(random.NextDouble()-.5)*.55f,length=leaves?.06f+(float)random.NextDouble()*.13f:.15f+(float)random.NextDouble()*.6f,width=leaves?.04f:.008f;
            Vector3 direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle)),side=new Vector3(direction.z,0,-direction.x),start=new Vector3(x,y,z);
            Color color=Color.Lerp(new Color(.27f,.23f,.11f),new Color(.68f,.56f,.29f),(float)random.NextDouble());
            int first=b.V.Count;for(int k=0;k<3;k++){float t=k*.5f;Vector3 p=start+direction*length*(t-.5f)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.012f;b.Add(p-side*width*(k==1?1:.3f),color,new Vector2(0,t));b.Add(p+side*width*(k==1?1:.3f),color,new Vector2(1,t));}
            for(int k=0;k<2;k++){int n=first+k*2;b.Triangle(n,n+2,n+1);b.Triangle(n+1,n+2,n+3);}
        }
        var mesh=b.Finish();mesh.name="Camp loose bedding";mesh.SetUVs(1,Enumerable.Repeat(new Vector2(8,0),mesh.vertexCount).ToList());return mesh;
    }
}

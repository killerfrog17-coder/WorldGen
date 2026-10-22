using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Material skyMaterial;Texture2D starAtlas;
    CelestialSettings skyConfig;int starSeed=-1;StellarEnvironment starEnvironment;
    readonly System.Collections.Generic.List<Light> suspendedSkyLights=new System.Collections.Generic.List<Light>();
    readonly Vector4[] skyDirections=new Vector4[8],skyColors=new Vector4[8],skyParameters=new Vector4[8];
    readonly Vector4[] skyLightDirections=new Vector4[8],secondaryDirections=new Vector4[4],secondaryColors=new Vector4[4];
    double skyYear;
    Quaternion sunRotationFrom,sunRotationTarget;double sunBlendStarted;bool sunDirectionReady;int sunPrimary=-1;
    readonly Vector4[] skyDirectionFrom=new Vector4[8],skyDirectionDisplayed=new Vector4[8];
    int skyDirectionCount;
    public PlayableSkyFrame SkyFrame {get;private set;}
    void InitializeSky()
    {
        // URP falls back to another directional light when the selected sun
        // reaches zero intensity. Preserve/restore these lights, not the night.
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light!=localSun&&light.enabled&&light.type==LightType.Directional){suspendedSkyLights.Add(light);light.enabled=false;}
        var shader=Resources.Load<Shader>("WorldGenSky");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Playable sky shader unavailable.");
        skyConfig=playState?.Settings.Sky??CelestialSettings.Preset("No moons");skyMaterial=new Material(shader);RefreshStarAtlas();
        view.gameObject.AddComponent<Skybox>().material=skyMaterial;view.clearFlags=CameraClearFlags.Skybox;InitializeWeather();
    }
    void RefreshStarAtlas()
    {
        int seed=StellarField.Seed(sourceWorld?.Recipe.Seed??0,skyConfig.Seed);
        if(starAtlas!=null&&seed==starSeed&&starEnvironment==skyConfig.Stars)return;
        if(starAtlas!=null)Destroy(starAtlas);starSeed=seed;starEnvironment=skyConfig.Stars;starAtlas=CreateStarAtlas(seed,skyConfig.Stars);skyMaterial.SetTexture("_Stars",starAtlas);
        var pole=StellarField.Pole(seed);skyMaterial.SetVector("_GalaxyPole",new Vector4((float)pole.X,(float)pole.Y,(float)pole.Z,0));skyMaterial.SetFloat("_FieldSeed",seed%4096);
    }
    static Texture2D CreateStarAtlas(int seed,StellarEnvironment environment)
    {
        const int width=4096,height=2048;var pixels=new Color32[width*height];
        for(int i=0;i<StellarField.Count(environment);i++)
        {
            var rng=new Rng(seed,"stellar-light-1",i);var direction=StellarField.Direction(seed,i,environment);double u=Math.Atan2(direction.X,direction.Z)/(2*Math.PI)+.5,v=Math.Asin(direction.Y)/Math.PI+.5;
            double brightness=.015+.985*Math.Pow(rng.Unit(),8),size=.3+brightness*.35,tint=rng.Unit();
            var color=Color.Lerp(new Color(.66f,.78f,1),new Color(1,.77f,.56f),(float)tint);color=Color.Lerp(color,Color.white,.45f);
            double x=u*width,y=v*height,stretch=1/Math.Max(.17,Math.Sin(v*Math.PI));
            int rx=(int)Math.Ceiling(size*stretch*2),ry=(int)Math.Ceiling(size*2);
            for(int dy=-ry;dy<=ry;dy++)for(int dx=-rx;dx<=rx;dx++)
            {
                int yy=(int)y+dy;if(yy<0||yy>=height)continue;int xx=((int)x+dx+width)%width;
                double a=(dx+.5-(x-Math.Floor(x)))/stretch,b=dy+.5-(y-Math.Floor(y));
                float light=(float)(brightness*Math.Exp(-(a*a+b*b)/(size*size)));
                int at=yy*width+xx;var old=pixels[at];pixels[at]=new Color32((byte)Math.Min(255,old.r+light*color.r*255),(byte)Math.Min(255,old.g+light*color.g*255),(byte)Math.Min(255,old.b+light*color.b*255),255);
            }
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,true){wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
    }
    public void SetDaylight(double solarHour,double seasonalYear)
    {
        if(localSun==null||Time.realtimeSinceStartupAsDouble<nextDaylightAt)return;nextDaylightAt=Time.realtimeSinceStartupAsDouble+.1;
        long traceStarted=WorldGenPerformance.TraceBegin();try{SetDaylightBody(solarHour,seasonalYear);}finally{WorldGenPerformance.TraceEnd(7,traceStarted);}
    }
    void SetDaylightBody(double solarHour,double seasonalYear)
    {
        // Finish the previous interval at NOW before publishing its successor;
        // otherwise each 10Hz sample would hold the previous rendered pose for
        // one frame and reintroduce a small periodic hitch.
        UpdateSunPresentation();
        skyYear=seasonalYear;skyConfig=playState?.Settings.Sky??skyConfig;RefreshStarAtlas();
        double actualRadius=frame.RadiusM+frame.AltitudeM+motion.Y,altitude=Math.Sqrt(actualRadius*actualRadius+motion.X*motion.X+motion.Z*motion.Z)-frame.RadiusM;
        var appearance=playState?.Settings;
        UpdateLocalWeather(seasonalYear*365,altitude);
        double dip=Math.Acos(frame.RadiusM/(frame.RadiusM+Math.Max(0,altitude)))*180/Math.PI;
        SkyFrame=PlayableSky.Sample(Math.Asin(Position.Z)*180/Math.PI,solarHour,seasonalYear,skyConfig,seasonalYear*365,altitude,appearance?.MoonVisualExaggeration??3,appearance?.SunVisualExaggeration??3,dip);var sampled=SkyFrame;
        int primary=0;for(int i=1;i<sampled.Bodies.Length;i++)if(sampled.Bodies[i].Illuminance>sampled.Bodies[primary].Illuminance)primary=i;
        var body=sampled.Bodies[primary];
        var at=Position;V east=V.Cross(Math.Abs(at.Z)>.999999?new V(0,1,0):new V(0,0,1),at).Unit,north=V.Cross(at,east).Unit;
        Vector3 Vector(V value){var planet=east*value.X+at*value.Y+north*value.Z;return new Vector3((float)V.Dot(planet,frame.East),(float)V.Dot(planet,frame.Origin),(float)V.Dot(planet,frame.North));}
        Color ColorOf(V value)=>new Color((float)value.X,(float)value.Y,(float)value.Z);
        var direction=Vector(body.Direction);var rotation=Quaternion.LookRotation(-direction,Math.Abs(direction.y)>.99?Vector3.forward:Vector3.up);
        bool snap=!sunDirectionReady||sunPrimary!=primary||Quaternion.Angle(localSun.transform.rotation,rotation)>20;
        sunRotationFrom=snap?rotation:localSun.transform.rotation;sunRotationTarget=rotation;sunBlendStarted=Time.realtimeSinceStartupAsDouble;
        sunDirectionReady=true;sunPrimary=primary;if(snap)localSun.transform.rotation=rotation;
        float cloudLight=weatherScreen==null?1:Mathf.Lerp(1,.3f,(float)CurrentWeather.CloudCover);localSun.color=ColorOf(body.Color);localSun.intensity=(float)body.Illuminance*cloudLight;
        ConfigureWeatherScreen(sampled,Vector(sampled.Bodies[0].Direction),ColorOf(sampled.Bodies[0].Color));
        skyMaterial.SetVector("_Zenith",ColorOf(sampled.Zenith));skyMaterial.SetVector("_Horizon",ColorOf(sampled.Horizon));
        skyMaterial.SetVector("_LocalUp",Vector(new V(0,1,0)));
        skyMaterial.SetVector("_NightAirGlow",ColorOf(sampled.NightAirGlow));skyMaterial.SetFloat("_AirDensity",(float)Math.Exp(-Math.Max(0,altitude)/8000));skyMaterial.SetFloat("_AerosolHaze",(float)skyConfig.Haze);
        skyMaterial.SetVector("_StarRight",Vector(sampled.StarRight));skyMaterial.SetVector("_StarUp",Vector(sampled.StarUp));skyMaterial.SetVector("_StarForward",Vector(sampled.StarForward));
        skyMaterial.SetFloat("_StarVisibility",(float)sampled.StarVisibility);skyMaterial.SetFloat("_Daylight",(float)sampled.Daylight);skyMaterial.SetFloat("_Twilight",(float)sampled.Twilight);skyMaterial.SetFloat("_GalacticGlow",(float)sampled.GalacticGlow);skyMaterial.SetFloat("_StellarGlow",(float)sampled.StellarGlow);
        int count=Math.Min(8,sampled.Bodies.Length),secondary=0;for(int i=0;i<count;i++){var b=sampled.Bodies[i];skyDirectionFrom[i]=snap||i>=skyDirectionCount?(Vector4)Vector(b.Direction):skyDirectionDisplayed[i];skyDirections[i]=Vector(b.Direction);skyColors[i]=ColorOf(b.Color)*(b.MoonIndex<0?1:(float)(skyConfig.Moons[b.MoonIndex].Albedo/.12));skyLightDirections[i]=Vector(b.LightDirection);skyParameters[i]=new Vector4((float)(b.AngularRadiusDegrees*Math.PI/180),b.Surface,(b.MoonIndex+1)*79+starSeed%4096,(float)b.IlluminatedFraction);
            if(i!=primary&&secondary<4){secondaryDirections[secondary]=Vector(b.Direction);secondaryColors[secondary++]=ColorOf(b.Color)*(float)b.Illuminance;}}
        skyDirectionCount=count;skyMaterial.SetInt("_BodyCount",count);skyMaterial.SetVectorArray("_BodyColors",skyColors);skyMaterial.SetVectorArray("_BodyParameters",skyParameters);
        skyMaterial.SetVectorArray("_BodyLightDirections",skyLightDirections);
        void MaterialLight(Material material){if(material.HasProperty("_FogColor"))material.SetVector("_FogColor",ColorOf(sampled.Horizon));if(material.HasProperty("_AmbientTint"))material.SetVector("_AmbientTint",ColorOf(sampled.Ambient));if(material.HasProperty("_Daylight"))material.SetFloat("_Daylight",(float)sampled.Daylight);
            material.SetVector("_AirGeometry",new Vector4((float)frame.RadiusM,(float)frame.AltitudeM,(float)originX,(float)originZ));material.SetFloat("_AirObserverM",(float)altitude);material.SetFloat("_AirHaze",(float)skyConfig.Haze);material.SetInt("_SecondaryLightCount",secondary);material.SetVectorArray("_SecondaryDirections",secondaryDirections);material.SetVectorArray("_SecondaryColors",secondaryColors);}
        MaterialLight(landMaterial);MaterialLight(waterMaterial);foreach(var material in legacyVegetationScene)if(material!=null)MaterialLight(material);
        terrainSecondaryCount=secondary;PublishTerrainScene();
        UpdateSunPresentation();
    }
    void UpdateSunPresentation()
    {
        if(!sunDirectionReady||localSun==null||skyMaterial==null)return;
        float t=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-sunBlendStarted)/.1));
        localSun.transform.rotation=Quaternion.Slerp(sunRotationFrom,sunRotationTarget,t);
        for(int i=0;i<skyDirectionCount;i++)skyDirectionDisplayed[i]=Vector3.Slerp((Vector3)skyDirectionFrom[i],(Vector3)skyDirections[i],t);
        skyMaterial.SetVectorArray("_BodyDirections",skyDirectionDisplayed);
    }
    void DisposeSky(){foreach(var light in suspendedSkyLights)if(light!=null)light.enabled=true;suspendedSkyLights.Clear();if(skyMaterial!=null)Destroy(skyMaterial);if(starAtlas!=null)Destroy(starAtlas);}
}

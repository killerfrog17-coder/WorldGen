using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Material skyMaterial;Texture2D starAtlas;
    readonly System.Collections.Generic.List<Light> suspendedSkyLights=new System.Collections.Generic.List<Light>();
    readonly Vector4[] skyDirections=new Vector4[8],skyColors=new Vector4[8],skyParameters=new Vector4[8];
    public PlayableSkyFrame SkyFrame {get;private set;}
    void InitializeSky()
    {
        // URP falls back to another directional light when the selected sun
        // reaches zero intensity. Preserve/restore these lights, not the night.
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light!=localSun&&light.enabled&&light.type==LightType.Directional){suspendedSkyLights.Add(light);light.enabled=false;}
        var shader=Resources.Load<Shader>("WorldGenSky");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Playable sky shader unavailable.");
        skyMaterial=new Material(shader);starAtlas=CreateStarAtlas();skyMaterial.SetTexture("_Stars",starAtlas);
        view.gameObject.AddComponent<Skybox>().material=skyMaterial;view.clearFlags=CameraClearFlags.Skybox;
    }
    static Texture2D CreateStarAtlas()
    {
        const int width=4096,height=2048;var pixels=new Color32[width*height];
        for(int i=0;i<5800;i++)
        {
            var rng=new Rng(481071,"playable-sky-stars-1",i);double u=rng.Unit(),v=Math.Asin(rng.Unit()*2-1)/Math.PI+.5;
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
        SkyFrame=PlayableSky.Sample(Math.Asin(Position.Z)*180/Math.PI,solarHour,seasonalYear);var frame=SkyFrame;var body=frame.Bodies[0];
        Vector3 Vector(V value)=>new Vector3((float)value.X,(float)value.Y,(float)value.Z);
        Color ColorOf(V value)=>new Color((float)value.X,(float)value.Y,(float)value.Z);
        var direction=Vector(body.Direction);localSun.transform.rotation=Quaternion.LookRotation(-direction,Math.Abs(direction.y)>.99?Vector3.forward:Vector3.up);
        localSun.color=ColorOf(body.Color);localSun.intensity=(float)body.Illuminance;
        skyMaterial.SetVector("_Zenith",ColorOf(frame.Zenith));skyMaterial.SetVector("_Horizon",ColorOf(frame.Horizon));
        skyMaterial.SetVector("_StarRight",Vector(frame.StarRight));skyMaterial.SetVector("_StarUp",Vector(frame.StarUp));skyMaterial.SetVector("_StarForward",Vector(frame.StarForward));
        skyMaterial.SetFloat("_StarVisibility",(float)frame.StarVisibility);skyMaterial.SetFloat("_Daylight",(float)frame.Daylight);skyMaterial.SetFloat("_Twilight",(float)frame.Twilight);
        int count=Math.Min(8,frame.Bodies.Length);for(int i=0;i<count;i++){var b=frame.Bodies[i];skyDirections[i]=Vector(b.Direction);skyColors[i]=ColorOf(b.Color);skyParameters[i]=new Vector4((float)(b.AngularRadiusDegrees*Math.PI/180),1,0,0);}
        skyMaterial.SetInt("_BodyCount",count);skyMaterial.SetVectorArray("_BodyDirections",skyDirections);skyMaterial.SetVectorArray("_BodyColors",skyColors);skyMaterial.SetVectorArray("_BodyParameters",skyParameters);
        void MaterialLight(Material material){if(material.HasProperty("_FogColor"))material.SetVector("_FogColor",ColorOf(frame.Horizon));if(material.HasProperty("_AmbientTint"))material.SetVector("_AmbientTint",ColorOf(frame.Ambient));if(material.HasProperty("_Daylight"))material.SetFloat("_Daylight",(float)frame.Daylight);}
        MaterialLight(landMaterial);MaterialLight(waterMaterial);foreach(var material in ownedScatterMaterials)MaterialLight(material);
    }
    void DisposeSky(){foreach(var light in suspendedSkyLights)if(light!=null)light.enabled=true;suspendedSkyLights.Clear();if(skyMaterial!=null)Destroy(skyMaterial);if(starAtlas!=null)Destroy(starAtlas);}
}

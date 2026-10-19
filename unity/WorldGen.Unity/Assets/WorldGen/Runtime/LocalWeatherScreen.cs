using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

/// <summary>Camera-local visual state only. Weather decisions are shared Core queries.</summary>
public sealed class LocalWeatherScreen:MonoBehaviour
{
    [NonSerialized]public WeatherFrame Weather;public Vector3 PlanetCenter,PlanetEast,PlanetNorth,PlanetUp,Anchor,SunDirection;
    public Color SunColor=Color.white,Ambient=new Color(.12f,.15f,.2f);public float RadiusM,Daylight,LightLevel,Flash,Exposure=1;public double Days;
    public Vector2 WindDisplacement;public int DebugDepth;public bool Ready;public Material Material {get;private set;}
    public int DrawCount {get;set;}
    public Texture2D FieldConditions,FieldLayers;public WeatherFieldTile Field;public bool UseField;public double GroundM;
    public Texture2D PreviousConditions,PreviousLayers;public WeatherFieldTile PreviousField;public float FieldBlend=1;
    Texture3D noise;double eye=-1;
    void Awake()
    {
        var shader=Resources.Load<Shader>("WorldGenWeather");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Weather shader unavailable.");Material=new Material(shader);
        // Small, tileable volume: one creation, no per-frame texture uploads.
        const int size=32;var data=new Color32[size*size*size];
        double Lattice(int x,int y,int z,int period){uint h=Rng.Mix(unchecked((uint)((x%period)*73856093^(y%period)*19349663^(z%period)*83492791)^0x735d391u));return h/4294967295d;}
        double Noise(int x,int y,int z,int cells)
        {
            double a=x*(double)cells/size,b=y*(double)cells/size,c=z*(double)cells/size;int ix=(int)a,iy=(int)b,iz=(int)c;a-=ix;b-=iy;c-=iz;a=a*a*(3-2*a);b=b*b*(3-2*b);c=c*c*(3-2*c);double result=0;
            for(int dx=0;dx<2;dx++)for(int dy=0;dy<2;dy++)for(int dz=0;dz<2;dz++)result+=Lattice(ix+dx,iy+dy,iz+dz,cells)*(dx==0?1-a:a)*(dy==0?1-b:b)*(dz==0?1-c:c);return result;
        }
        double Billow(int x,int y,int z)
        {
            double a=x*4d/size,b=y*4d/size,c=z*4d/size,best=4;int ix=(int)a,iy=(int)b,iz=(int)c;
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)for(int dz=-1;dz<=1;dz++)
            {
                int xx=ix+dx,yy=iy+dy,zz=iz+dz;uint hash=Rng.Mix((uint)(((xx+4)%4)*73856093^((yy+4)%4)*19349663^((zz+4)%4)*83492791));
                double px=xx+.15+(hash&1023)/1023d*.7;hash=Rng.Mix(hash);double py=yy+.15+(hash&1023)/1023d*.7;hash=Rng.Mix(hash);double pz=zz+.15+(hash&1023)/1023d*.7;
                double distance=(a-px)*(a-px)+(b-py)*(b-py)+(c-pz)*(c-pz);best=Math.Min(best,distance);
            }
            return Math.Max(0,1-Math.Sqrt(best));
        }
        byte Byte(double n)=>(byte)Mathf.Clamp((float)(n*255),0,255);
        for(int z=0;z<size;z++)for(int y=0;y<size;y++)for(int x=0;x<size;x++)data[(z*size+y)*size+x]=new Color32(Byte(Noise(x,y,z,4)*.65+Noise(x,y,z,8)*.25+Noise(x,y,z,16)*.1),Byte(Billow(x,y,z)),0,255);
        noise=new Texture3D(size,size,size,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};noise.SetPixels32(data);noise.Apply(true,true);Material.SetTexture("_WeatherNoise",noise);
    }
    public void Prepare(Camera camera,Matrix4x4 inverseViewProjection)
    {
        var o=WeatherVideoOptions.Current;float target=.015f+LightLevel;
        if(eye<0)eye=target;double factor=1-Math.Exp(-Math.Min(.1,Time.unscaledDeltaTime)/(target>eye?.65:3.5));eye+=(target-eye)*factor;
        Exposure=o.Adaptation<=0?1:Mathf.Lerp(1,Mathf.Clamp((float)(target/Math.Max(.015,eye)),.65f,2.5f),o.Adaptation);
        Material.SetMatrix("_WeatherInvVP",inverseViewProjection);Material.SetInt("_WeatherDebugDepth",DebugDepth);
        Material.SetVector("_WeatherCamera",camera.transform.position);Material.SetVector("_WeatherCenter",PlanetCenter);Material.SetFloat("_WeatherRadius",RadiusM);
        Material.SetVector("_WeatherEast",PlanetEast);Material.SetVector("_WeatherNorth",PlanetNorth);Material.SetVector("_WeatherUp",PlanetUp);Material.SetVector("_WeatherAnchor",Anchor);
        Material.SetVector("_WeatherLayers",new Vector4((float)Weather.CloudBaseM,(float)Weather.CloudThicknessM,(float)Weather.CirrusBaseM,450));
        Material.SetVector("_WeatherCover",new Vector4(o.Clouds==0?0:(float)Weather.CloudCover,o.Clouds==0?0:(float)Weather.CirrusCover,o.Clouds==1?48:o.Clouds==2?64:96,Daylight));
        Material.SetInt("_WeatherUseField",UseField?1:0);Material.SetInt("_WeatherCloudsEnabled",o.Clouds>0?1:0);
        if(UseField)
        {
            Material.SetTexture("_WeatherConditions",FieldConditions);Material.SetTexture("_WeatherLayerField",FieldLayers);
            Material.SetVector("_WeatherFieldRect",new Vector4(Field.Longitude,Field.Latitude,Field.Width,Field.Height));
            bool blend=PreviousField!=null&&FieldBlend<1;
            Material.SetFloat("_WeatherFieldBlend",blend?FieldBlend:1);
            if(blend){Material.SetTexture("_PreviousWeatherConditions",PreviousConditions);Material.SetTexture("_PreviousWeatherLayers",PreviousLayers);Material.SetVector("_PreviousWeatherRect",new Vector4(PreviousField.Longitude,PreviousField.Latitude,PreviousField.Width,PreviousField.Height));}
            Material.SetVector("_WeatherShell",new Vector4((float)(blend?Math.Min(Field.MinBase,PreviousField.MinBase):Field.MinBase),(float)(blend?Math.Max(Field.MaxTop,PreviousField.MaxTop):Field.MaxTop),0,0));
        }
        Material.SetVector("_WeatherWet",new Vector4((float)Weather.PrecipitationMmPerHour,(float)Weather.Thunderstorm,(float)Weather.RainMmPerHour,(float)Weather.SnowWaterMmPerHour));
        Material.SetVector("_WeatherFog",new Vector4((float)Weather.Fog,(float)GroundM,o.Visibility,0));
        Material.SetVector("_WeatherWind",new Vector4(WindDisplacement.x,WindDisplacement.y,0,0));
        Material.SetVector("_WeatherSun",SunDirection);Material.SetColor("_WeatherSunColor",SunColor);Material.SetColor("_WeatherAmbient",Ambient);
        var sun=camera.WorldToViewportPoint(camera.transform.position+SunDirection*10000);Material.SetVector("_WeatherSunScreen",new Vector4(sun.x,sun.y,sun.z>0?1:0,Daylight));
        Material.SetVector("_WeatherEffects",new Vector4(o.SunGlare,o.SunRays,Exposure,Flash*o.Lightning));
    }
    void OnDestroy(){if(Material!=null)Destroy(Material);if(noise!=null)Destroy(noise);}
}

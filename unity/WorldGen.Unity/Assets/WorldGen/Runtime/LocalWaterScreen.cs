using System;
using UnityEngine;
using WorldGen.Core;

public sealed class LocalWaterScreen:MonoBehaviour
{
    public Material Material {get;private set;}public float Blend {get;private set;}public bool BlurEyes {get;private set;}
    public int DrawCount;float surface,visibility,day,depth;Color colour;Matrix4x4 inverse;
    public void Set(Camera camera,double level,WaterQuality quality,bool goggles,float daylight,double temperature)
    {
        if(Material==null){var shader=Resources.Load<Shader>("WorldGenUnderwater");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Underwater shader unavailable");Material=new Material(shader);}
        surface=double.IsFinite(level)?(float)level:camera.transform.position.y-1000;depth=surface-camera.transform.position.y;
        float target=double.IsFinite(level)?Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.02f,.12f,depth)):0;
        Blend=Mathf.MoveTowards(Blend,target,Time.unscaledDeltaTime*7);BlurEyes=!goggles;day=daylight;
        float mud=(float)quality.Turbidity;visibility=quality.Kind==LocalWaterKind.Ocean?Mathf.Lerp(28,7,mud):Mathf.Lerp(15,3.5f,mud);
        colour=quality.Kind==LocalWaterKind.Ocean?new Color(.025f,.12f,.17f):Color.Lerp(new Color(.035f,.16f,.12f),new Color(.13f,.14f,.075f),mud);
        inverse=(GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix).inverse;
    }
    public void Prepare()
    {
        Material.SetMatrix("_WaterInverseVP",inverse);Material.SetVector("_WaterVision",new Vector4(Blend,BlurEyes?1:0,surface,3.9f/Mathf.Max(1,visibility)));
        Material.SetColor("_WaterFogColour",colour*Mathf.Lerp(.035f,1,day)*Mathf.Exp(-Mathf.Max(0,depth)*.025f));
    }
    void OnDestroy(){if(Material!=null)Destroy(Material);}
}

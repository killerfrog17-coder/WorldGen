using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Mesh fireflyQuad;Material fireflyMaterial;MaterialPropertyBlock fireflyProps;
    readonly Matrix4x4[] fireflyMatrices=new Matrix4x4[1023];
    readonly Vector4[] fireflyColors=new Vector4[1023];
    void UpdateFireflyGlow(Animal animal,float distance)
    {
        if(!animal.Spawn.Profile.derivedFirefly)return;
        var weather=CurrentWeather;
        double activity=FireflyDisplay.ActivityGain(animal.Firefly,LocalSolarHour,skyYear,Math.Asin(animal.Planet.Z)*180/Math.PI,
            weather.TemperatureC,weather.Humidity,weather.RainMmPerHour,Math.Sqrt(weather.WindEastMps*weather.WindEastMps+weather.WindNorthMps*weather.WindNorthMps));
        animal.FireflyGlow=(float)(activity*FireflyDisplay.Pulse(animal.Firefly?.Display,animal.Life,animal.Spawn.Hash));
        // Keep the true body nearby; distant subpixel insects need only a luminous point.
        animal.Renderer.enabled=animal.Dead||distance<40;
    }
    void PrepareFireflyGlows()
    {
        if(fireflyMaterial!=null)return;
        // Serialized instanced template keeps the variant in stripped player builds.
        var template=Resources.Load<Material>("FireflyGlowMaterial");if(template==null||!template.shader.isSupported)return;
        fireflyMaterial=new Material(template){name="Firefly luminous abdomens",enableInstancing=true};fireflyMaterial.SetFloat("_VegetationSceneEnabled",1);
        fireflyProps=new MaterialPropertyBlock();
        fireflyQuad=new Mesh{name="Firefly glow quad"};
        fireflyQuad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
        fireflyQuad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};fireflyQuad.triangles=new[]{0,1,2,0,2,3};fireflyQuad.RecalculateBounds();
    }
    void DrawFireflyGlows()
    {
        if(view==null||!Ready||animalDisposed)return;
        int count=0;float pixelsToWorld=2*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f)/Math.Max(1,view.pixelHeight);
        foreach(var animal in animals.Values)
        {
            if(animal.Retired||animal.Dead||animal.Root==null||!animal.Root.activeInHierarchy||animal.FireflyGlow<=.001f)continue;
            Vector3 position=animal.Root.transform.TransformPoint(new Vector3(0,animal.Model.Asset.Height*.3f,-.28f));
            float distance=Vector3.Distance(position,view.transform.position);if(distance>=220)continue;
            PrepareFireflyGlows();if(fireflyMaterial==null)return;
            float size=Mathf.Max(.025f,2.2f*pixelsToWorld*distance);
            float fade=animal.Fade*Mathf.Clamp01((220-distance)/26.4f);
            float intensity=animal.FireflyGlow*fade*5/(1+Mathf.Pow(distance/100,2));V color=FireflyDisplay.Color(animal.Firefly.Display);
            fireflyMatrices[count]=Matrix4x4.TRS(position,view.transform.rotation,Vector3.one*size);
            fireflyColors[count]=new Vector4((float)color.X*intensity,(float)color.Y*intensity,(float)color.Z*intensity,1);count++;
            if(count==fireflyMatrices.Length){Draw(count);count=0;}
        }
        if(count>0)Draw(count);
        void Draw(int instances)
        {
            fireflyProps.SetVectorArray("_FireflyGlowColor",fireflyColors);
            Graphics.DrawMeshInstanced(fireflyQuad,0,fireflyMaterial,fireflyMatrices,instances,fireflyProps,ShadowCastingMode.Off,false,30,view,LightProbeUsage.Off);
        }
    }
    void DisposeFireflyGlows()
    {if(fireflyQuad!=null)Destroy(fireflyQuad);if(fireflyMaterial!=null)Destroy(fireflyMaterial);}
}

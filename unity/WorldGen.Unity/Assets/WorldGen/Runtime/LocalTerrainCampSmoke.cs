using UnityEngine;

public sealed partial class LocalTerrainExplorer
{
    Material campSmokeMaterial;
    ParticleSystem CreateCampSmoke(Transform parent)
    {
        if(campSmokeMaterial==null)campSmokeMaterial=new Material(Resources.Load<Shader>("WorldGenCampSmoke"));
        var root=new GameObject("Campfire smoke");root.layer=30;root.transform.SetParent(parent,false);root.transform.localPosition=Vector3.up*.38f;
        var smoke=root.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=smoke.main;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(2.5f,4);main.startSize=new ParticleSystem.MinMaxCurve(.22f,.42f);main.startSpeed=0;main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        var emission=smoke.emission;emission.rateOverTime=6;
        var shape=smoke.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.12f;
        var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.55f),new Keyframe(1,2.6f)));
        var colour=smoke.colorOverLifetime;colour.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.2f,.18f),new GradientAlphaKey(.12f,.6f),new GradientAlphaKey(0,1)});colour.color=gradient;
        var noise=smoke.noise;noise.enabled=true;noise.strength=.13f;noise.frequency=.45f;noise.scrollSpeed=.2f;noise.quality=ParticleSystemNoiseQuality.Low;
        var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=campSmokeMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        UpdateCampSmoke(smoke);smoke.Play();return smoke;
    }
    void UpdateCampSmoke(ParticleSystem smoke)
    {
        if(smoke==null)return;var velocity=smoke.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
        float east=(float)CurrentWeather.WindEastMps*.09f,north=(float)CurrentWeather.WindNorthMps*.09f;
        velocity.x=new ParticleSystem.MinMaxCurve(east,east);velocity.y=new ParticleSystem.MinMaxCurve(.4f,.7f);velocity.z=new ParticleSystem.MinMaxCurve(north,north);
        if(paused&&smoke.isPlaying)smoke.Pause();else if(!paused&&smoke.isPaused)smoke.Play();
    }
}

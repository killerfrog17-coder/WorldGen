using UnityEngine;
using UnityEngine.UI;
using WorldGen.Presentation;

/// <summary>One retained UI quad; shape silhouettes come from the approved SVG.
/// Live values drive all fills and thermal effects, without mesh or texture uploads.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SurvivalHudView:MaskableGraphic
{
    Material ownedMaterial;
    SurvivalHudValues target,current;
    bool initialized;
    public SurvivalHudValues Displayed=>current;
    public void SetValues(SurvivalHudValues values,bool immediate=false)
    {
        EnsureMaterial();target=values;
        if(!initialized||immediate){current=target;initialized=true;Publish();}
    }
    void EnsureMaterial()
    {
        raycastTarget=false;
        if(ownedMaterial!=null)return;
        var shader=Resources.Load<Shader>("WorldGenSurvivalHud");
        ownedMaterial=new Material(shader){name="Survival HUD live values"};
        ownedMaterial.SetTexture("_Shapes",Resources.Load<Texture2D>("SurvivalHud/Shapes"));material=ownedMaterial;
    }
    void Update()
    {
        if(!initialized)return;
        float a=1-Mathf.Exp(-Mathf.Min(.1f,Time.unscaledDeltaTime)/.15f);
        current.Health=Mathf.Lerp(current.Health,target.Health,a);current.Energy=Mathf.Lerp(current.Energy,target.Energy,a);
        current.Stamina=Mathf.Min(current.Energy,Mathf.Lerp(current.Stamina,target.Stamina,a));
        current.Hydration=Mathf.Lerp(current.Hydration,target.Hydration,a);current.Fullness=Mathf.Lerp(current.Fullness,target.Fullness,a);
        current.FeelsLikeC=Mathf.Lerp(current.FeelsLikeC,target.FeelsLikeC,a);
        current.BodyStress=Mathf.Lerp(current.BodyStress,target.BodyStress,a);
        current.Wind=new WindHudValues(current.Wind.Angle+Mathf.DeltaAngle(current.Wind.Angle*Mathf.Rad2Deg,target.Wind.Angle*Mathf.Rad2Deg)*Mathf.Deg2Rad*a,Mathf.Lerp(current.Wind.Strength,target.Wind.Strength,a),target.Wind.Spread);Publish();
    }
    void Publish()
    {
        ownedMaterial.SetVector("_Resources",new Vector4(current.Health,current.Energy,current.Stamina,current.Hydration));
        ownedMaterial.SetVector("_Thermal",new Vector4(current.Fullness,current.FeelsLikeC,current.BodyStress,0));
        ownedMaterial.SetVector("_Wind",new Vector4(current.Wind.Angle,current.Wind.Strength,current.Wind.Spread,0));
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=GetPixelAdjustedRect();var c=(Color32)color;
        vh.AddVert(new Vector3(r.xMin,r.yMin),c,new Vector2(0,0));vh.AddVert(new Vector3(r.xMin,r.yMax),c,new Vector2(0,1));
        vh.AddVert(new Vector3(r.xMax,r.yMax),c,new Vector2(1,1));vh.AddVert(new Vector3(r.xMax,r.yMin),c,new Vector2(1,0));
        vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
    }
    protected override void OnDestroy()
    {base.OnDestroy();if(ownedMaterial!=null){if(Application.isPlaying)Destroy(ownedMaterial);else DestroyImmediate(ownedMaterial);}}
}

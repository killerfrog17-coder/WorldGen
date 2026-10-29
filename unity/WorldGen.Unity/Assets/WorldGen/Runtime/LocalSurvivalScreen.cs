using UnityEngine;

public sealed class LocalSurvivalScreen:MonoBehaviour
{
    public Material Material {get;private set;}
    public Vector4 Effects {get;private set;}
    public int DrawCount;
    public bool Active=>Effects.x+Effects.y+Effects.z+Effects.w>.005f;
    public void Set(Vector4 effects,bool immediate=false)
    {
        Effects=immediate?effects:Vector4.Lerp(Effects,effects,1-Mathf.Exp(-Mathf.Min(.1f,Time.unscaledDeltaTime)*8));
        if(!Active)return;
        if(Material==null)Material=new Material(Resources.Load<Shader>("WorldGenSurvivalScreen"));
        Material.SetVector("_SurvivalEffects",Effects);
    }
    void OnDestroy(){if(Material!=null)Destroy(Material);}
}

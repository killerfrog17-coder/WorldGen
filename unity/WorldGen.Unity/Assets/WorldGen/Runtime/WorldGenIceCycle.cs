using System;
using UnityEngine;
using WorldGen.Core;
public sealed partial class WorldGenRuntime
{
    Texture2D iceCycleIcon;int iceCycleLevel=-1;
    void DrawIceCycle(PrehistoryState state)
    {
        if(state?.Foragers==null)return;double year=state.Foragers.Seconds/(double)SimulationTime.Year;
        double ice=PaleolithicRules.Climate(world.Recipe.Seed,year,0).Ice,next=PaleolithicRules.Climate(world.Recipe.Seed,year+100,0).Ice;
        int level=Mathf.RoundToInt((float)ice*64);
        if(iceCycleIcon==null){iceCycleIcon=new Texture2D(40,40,TextureFormat.RGBA32,false);iceCycleIcon.filterMode=FilterMode.Bilinear;}
        if(level!=iceCycleLevel)
        {
            var pixels=new Color32[1600];float cap=.82f-level/64f*.75f;
            for(int y=0;y<40;y++)for(int x=0;x<40;x++)
            {
                float a=(x-19.5f)/18,b=(y-19.5f)/18,r=Mathf.Sqrt(a*a+b*b);if(r>1.06f)continue;
                Color c=r>.88f?new Color(.35f,.49f,.57f):Color.Lerp(new Color(.1f,.28f,.37f),new Color(.46f,.69f,.73f),(b+1)*.5f);
                if(r<=.88f&&Mathf.Abs(b)>cap+.08f*Mathf.Sin(a*8))c=Color.Lerp(new Color(.68f,.84f,.88f),new Color(.95f,.98f,1),(b+1)*.5f);
                if(r>.96f)c.a=Mathf.Clamp01((1.06f-r)/.1f);pixels[y*40+x]=c;
            }
            iceCycleIcon.SetPixels32(pixels);iceCycleIcon.Apply(false,false);iceCycleLevel=level;
        }
        string phase=ice<.12?"Interglacial":ice>.92?"Glacial maximum":next>ice?"Cooling / ice advancing":"Warming / ice retreating";
        GUILayout.Label(new GUIContent(iceCycleIcon,phase+" · ice-cycle intensity "+(ice*100).ToString("0")+"%"),GUILayout.Width(28),GUILayout.Height(28));
    }
}

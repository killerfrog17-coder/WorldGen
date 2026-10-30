using System;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Vector3 SlingPouch(ViewPossession visual,Possession item)
    {
        bool left=item.Place==PossessionPlace.LeftHand;
        var socket=viewArms.Bones["Grip."+(left?"L":"R")];
        float length=Mathf.Clamp((float)item.WidthM*3.7f,.4f,.7f);
        var charge=itemCharge??pendingRanged?.Charge;
        if(charge?.Weapon==item&&!charge.Throw)
        {
            float phase=(float)(Time.unscaledTimeAsDouble-charge.Started)*Mathf.PI*2*1.7f;
            visual.SlingDirection=new Vector3(0,Mathf.Cos(phase),Mathf.Sin(phase));
        }
        else visual.SlingDirection=Vector3.RotateTowards(visual.SlingDirection,Vector3.down,Mathf.Min(.05f,Time.unscaledDeltaTime)*8,0);
        return socket.position+view.transform.TransformDirection(visual.SlingDirection)*length+view.transform.right*(left?-.04f:.04f);
    }

    void PoseViewSling(ViewPossession visual,Possession item)
    {
        if(visual.SlingMesh==null)
        {
            var filter=visual.Root.GetComponentInChildren<MeshFilter>();
            if(filter==null)return;
            // CreatePossessionVisual gives this private root its own owned mesh;
            // no world item, icon or source asset shares these deforming vertices.
            visual.SlingMesh=filter.sharedMesh;visual.SlingRest=filter.sharedMesh.vertices;
            visual.SlingVertices=new Vector3[visual.SlingRest.Length];visual.SlingColors=filter.sharedMesh.colors;
            visual.SlingMesh.MarkDynamic();
        }
        string side=item.Place==PossessionPlace.LeftHand?"L":"R";
        Vector3 grip=viewArms.Bones["Grip."+side].position,pouch=SlingPouch(visual,item);
        Vector3 along=(pouch-grip).normalized,width=view.transform.right,normal=Vector3.Cross(width,along).normalized;
        for(int i=0;i<visual.SlingRest.Length;i++)
        {
            Vector3 p=visual.SlingRest[i],world;
            if(visual.SlingColors[i].g<.4f)
                world=pouch+width*p.x*.13f+along*(p.y-.525f)*.34f+normal*p.z*.08f;
            else
            {
                bool upper=p.x<0;Vector3 a=new Vector3(upper?-.3f:.3f,.5f,0),b=new Vector3(upper?-.48f:.48f,upper?1:0,0);
                Vector3 axis=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,axis)/axis.sqrMagnitude);
                Vector3 radial=p-Vector3.Lerp(a,b,t);
                Vector3 start=pouch+width*(upper?-.035f:.035f),end=grip+width*(upper?-.006f:.006f);
                world=Vector3.Lerp(start,end,t)+width*radial.x*.04f+normal*radial.z*.06f;
            }
            visual.SlingVertices[i]=visual.Root.transform.InverseTransformPoint(world);
        }
        visual.SlingMesh.vertices=visual.SlingVertices;visual.SlingMesh.RecalculateNormals();visual.SlingMesh.RecalculateBounds();
    }
}

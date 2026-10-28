using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    public static Mesh BuildWood(int variant,PossessionKind kind,bool peeled,bool ground=false)
    {
        var b=new Builder();int shape=variant%4;
        Color bark=variant<4?new Color(.38f,.25f,.14f):new Color(.48f,.45f,.37f),wood=new Color(.72f,.55f,.32f);
        bool raw=kind==PossessionKind.Branch,pointed=kind==PossessionKind.WoodenSpear||kind==PossessionKind.DiggingStick;
        Vector3 Point(float t)=>new Vector3((shape==0?0:shape*.027f)*Mathf.Sin(t*Mathf.PI),t,shape==3?.035f*Mathf.Sin(t*5):0);
        for(int j=0;j<8;j++)
        {
            float a=j/8f,c=(j+1)/8f,ra=.5f*(1-.17f*a),rc=.5f*(1-.17f*c);
            if(pointed){if(a>=.75f)ra*=Mathf.Max(0,(1-a)*4);if(c>=.75f)rc*=Mathf.Max(.008f,(1-c)*4);}
            // Constant vertex budget; grain and subtle side shading, no fur cards.
            var color=(peeled||pointed&&a>=.75f)?wood:bark;
            b.Tube(Point(a),Point(c),ra,rc,8,color*(j%2==0?1:.94f));
        }
        b.Crown(Point(0),new Vector3(.49f,.002f,.49f),8,2,wood,10,0);
        if(!pointed)b.Crown(Point(1),new Vector3(.405f,.002f,.405f),8,2,wood,11,0);
        if(raw)for(int j=0;j<2;j++)
        {float t=.32f+j*.34f;b.Tube(Point(t),Point(t)+new Vector3(j==0?.9f:-.7f,.08f,.05f),.15f,.04f,6,bark);}
        var mesh=b.Finish();mesh.name=kind.ToString();
        if(ground){var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(vertices[i].y-.5f,vertices[i].x+.5f,vertices[i].z);mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();}
        return mesh;
    }
}

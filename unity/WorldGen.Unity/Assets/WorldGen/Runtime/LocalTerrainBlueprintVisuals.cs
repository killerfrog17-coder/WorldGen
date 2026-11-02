using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    public static Mesh BuildBlueprint(CampBlueprint plan,bool completed,bool preview=false,bool omitHide=false)
    {
        var b=new Builder();var tags=new List<Vector2>();
        Vector3 P(V p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        void Quad(Vector3 a,Vector3 c,Vector3 d,Vector3 e,Color color)
        {int at=b.V.Count;float w=(c-a).magnitude,h=(e-a).magnitude;b.Add(a,color,new Vector2(0,0));b.Add(c,color,new Vector2(w,0));b.Add(d,color,new Vector2(w,h));b.Add(e,color,new Vector2(0,h));b.Triangle(at,at+1,at+2);b.Triangle(at,at+2,at+3);}
        foreach(var c in CampBlueprints.Components(plan))
        {
            bool done=CampBlueprints.Done(plan,c);if(!preview&&done!=completed)continue;
            if(omitHide&&c.Kind==CampPartKind.Cover&&plan.Cover==CampCover.Hide)continue;
            int start=b.V.Count;int surface=1;var points=c.Points.Select(P).ToArray();
            if(c.Kind==CampPartKind.Cover)
            {
                surface=plan.Cover==CampCover.Hide?4:plan.Cover==CampCover.Bark?2:7;
                var color=plan.Cover==CampCover.Hide?new Color(.55f,.4f,.27f,0):plan.Cover==CampCover.Bark?new Color(.42f,.32f,.22f,0):new Color(.54f,.46f,.24f,0);
                Quad(points[0],points[1],points[2],points[3],color);
                if(plan.Cover==CampCover.Brush||plan.Cover==CampCover.Reed)
                {
                    // Overlapping uneven bundles break up the roof edge and
                    // silhouette while sharing the existing plant surface atlas.
                    var normal=Vector3.Cross(points[1]-points[0],points[3]-points[0]).normalized;
                    int layers=preview?1:Mathf.Clamp(Mathf.CeilToInt((float)(CampBlueprints.Supplied(plan,c)/c.Kg)),1,3);
                    for(int layer=0;layer<layers;layer++)for(int i=0;i<7;i++)for(int j=0;j<4;j++)
                    {
                        float u=i/7f,v=j/4f;Vector3 At(float x,float y)=>Vector3.LerpUnclamped(Vector3.LerpUnclamped(points[0],points[1],x),Vector3.LerpUnclamped(points[3],points[2],x),y);
                        float over=.02f+((i*13+j*7)%9)*.002f;var lift=normal*(.012f+j*.003f+layer*.018f);
                        Quad(At(u,v)+lift,At(u+.135f,v)+lift,At(u+.15f,v+.29f+over)+lift,At(u-.012f,v+.27f+over)+lift,color*Mathf.Lerp(.8f,1.1f,((i+j*3)%7)/6f));
                    }
                }
            }
            else if(c.Kind==CampPartKind.Stonework||c.Kind==CampPartKind.Bonework)
            {
                bool bone=c.Kind==CampPartKind.Bonework;surface=bone?5:0;var a=points[0];var e=points[1];float length=Vector3.Distance(a,e),height=(float)c.LengthM,width=(float)c.WidthM;
                var direction=(e-a).normalized;var across=Vector3.Cross(direction,Vector3.up).normalized;
                int pieces=Mathf.Max(1,Mathf.CeilToInt(length/(bone?.22f:.24f)));
                for(int k=0;k<pieces;k++)
                {
                    var at=Vector3.Lerp(a,e,(k+.5f)/pieces)+Vector3.up*height*.48f;
                    if(!bone)
                    {
                        int courses=Mathf.Max(1,Mathf.CeilToInt(height/.22f));float course=height/courses;
                        for(int row=0;row<courses;row++)
                        {
                            int first=b.V.Count;var center=Vector3.Lerp(a,e,Mathf.Clamp01((k+.5f+(row%2)*.22f)/pieces))+Vector3.up*(row+.48f)*course;
                            b.Crown(Vector3.zero,new Vector3(length/pieces*.64f,course*.54f,width*.6f),8,4,new Color(.47f,.46f,.41f,0),k+row*17+start,.13f);
                            for(int vertex=first;vertex<b.V.Count;vertex++){var q=b.V[vertex];q.y=Mathf.Clamp(q.y,-course*.48f,course*.44f);b.V[vertex]=center+direction*q.x+Vector3.up*q.y+across*q.z;}
                        }
                    }
                    else
                    {
                        b.Tube(at-across*width*.4f-direction*.09f,at+across*width*.4f+direction*.09f,.06f,.045f,7,new Color(.76f,.7f,.55f,0));
                        b.Crown(at+Vector3.up*.08f,new Vector3(.13f,.15f,.11f),7,3,new Color(.8f,.75f,.62f,0),k+start,.1f);
                        // Bone footing includes joints and curved ribs, rather
                        // than colouring an ordinary wooden pole ivory.
                        for(int j=0;j<5;j++){float t=j/5f,tt=(j+1)/5f;b.Tube(at+across*((t-.5f)*width)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*height*.5f),at+across*((tt-.5f)*width)+Vector3.up*(Mathf.Sin(tt*Mathf.PI)*height*.5f),.02f,.018f,6,new Color(.75f,.69f,.54f,0));}
                    }
                }
            }
            else if(c.Kind==CampPartKind.Deck)
            {
                surface=1;var a=points[0];var e=points[1];int n=Mathf.CeilToInt(Math.Abs(e.x-a.x)/.035f);
                for(int i=0;i<=n;i++){float x=Mathf.Lerp(a.x,e.x,i/(float)Math.Max(1,n));b.Tube(new Vector3(x,a.y,a.z),new Vector3(x,e.y,e.z),.018f,.016f,6,new Color(.45f,.33f,.21f,0));}
            }
            else if(c.Kind==CampPartKind.Binding)
            {
                surface=6;var center=points[0];for(int i=0;i<40;i++){float a=i/12f*Mathf.PI*2,z=(i+1)/12f*Mathf.PI*2;b.Tube(center+new Vector3(Mathf.Cos(a)*.043f,(i/40f-.5f)*.04f,Mathf.Sin(a)*.043f),center+new Vector3(Mathf.Cos(z)*.043f,((i+1)/40f-.5f)*.04f,Mathf.Sin(z)*.043f),.003f,.003f,4,new Color(.55f,.43f,.27f,0));}
            }
            else
            {
                var source=CampBlueprints.State(plan,c)?.Materials.FirstOrDefault();float radius=(float)(source?.WidthM??c.WidthM)*.5f;
                for(int i=1;i<points.Length;i++)b.Tube(points[i-1],points[i],radius,radius*.9f,8,new Color(.39f,.28f,.17f,0));
                b.Crown(points[points.Length-1],Vector3.one*radius*.9f,8,2,new Color(.68f,.54f,.32f,0),start,.05f);
            }
            for(int i=start;i<b.V.Count;i++)tags.Add(new Vector2(surface,0));
        }
        var mesh=b.Finish();mesh.name="Camp blueprint "+(completed?"fitted":"remaining");mesh.SetUVs(1,tags);return mesh;
    }
    // One patch per actual contributed hide, with normalized coat coordinates.
    public static Mesh BuildCampHide(V[] points,float from,float to)
    {
        var b=new Builder();Vector3 P(V v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
        var a=Vector3.Lerp(P(points[0]),P(points[1]),from);var c=Vector3.Lerp(P(points[0]),P(points[1]),to);var d=Vector3.Lerp(P(points[3]),P(points[2]),to);var e=Vector3.Lerp(P(points[3]),P(points[2]),from);
        // Modest sag without moving the anchored perimeter; also avoids a
        // flat plastic sheet appearance on large hide panels.
        var center=(a+c+d+e)*.25f-Vector3.up*.025f;var color=new Color(.55f,.4f,.27f,1);
        b.Add(a,color,new Vector2(0,0));b.Add(c,color,new Vector2(1,0));b.Add(d,color,new Vector2(1,1));b.Add(e,color,new Vector2(0,1));b.Add(center,color,new Vector2(.5f,.5f));
        for(int i=0;i<4;i++)b.Triangle(i,(i+1)%4,4);
        // Raised sewn edges have their own dark rawhide colour (alpha zero:
        // the coat shader only colours the hide, never these stitches).
        for(int edge=0;edge<4;edge++)
        {var ends=new[]{a,c,d,e};var x=ends[edge];var y=ends[(edge+1)%4];int n=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(x,y)/.065f),2,60);for(int i=0;i<n;i++){var at=Vector3.Lerp(x,y,(i+.5f)/n);var across=Vector3.Cross((y-x).normalized,Vector3.up);if(across.sqrMagnitude<.01f)across=Vector3.right;b.Tube(at-across*.012f+Vector3.up*.003f,at+across*.012f+Vector3.up*.003f,.0018f,.0018f,4,new Color(.22f,.16f,.1f,0));}}
        var mesh=b.Finish();mesh.name="Camp donor hide patch";mesh.SetUVs(1,Enumerable.Repeat(new Vector2(4,0),mesh.vertexCount).ToList());return mesh;
    }
}
public sealed partial class LocalTerrainExplorer
{
    void AddBlueprintMesh(GameObject root,Mesh mesh,Material material,string name)
    {
        var child=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));child.layer=30;child.transform.SetParent(root.transform,false);child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        if(material==blueprintGhostMaterial||material==blueprintPlacementMaterial){renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
    }
    int BlueprintContentsState(CampObject c)
    {if(c.Blueprint==null)return 0;int hash=c.Blueprint.Revision;foreach(var item in StoredAt(c))hash=unchecked(hash*31+item.Id.GetHashCode()+item.MassKg.GetHashCode()+(item.Dried?1:0));return hash;}
    void CreateBlueprintVisual(CampObject c,GameObject root)
    {
        var plan=c.Blueprint;var mesh=LocalScatterMeshes.BuildBlueprint(plan,true,omitHide:true);AddBlueprintMesh(root,mesh,PortableMaterial(),"Fitted camp components");
        if(plan.Cover==CampCover.Hide)
        foreach(var component in CampBlueprints.Components(plan).Where(d=>d.Kind==CampPartKind.Cover&&CampBlueprints.Done(plan,d)))
        {
            var supplied=CampBlueprints.State(plan,component).Materials;float total=(float)supplied.Sum(i=>i.MassKg),cursor=0;
            foreach(var hide in supplied)
            {
                float end=cursor+(float)hide.MassKg/total;var child=new GameObject("Hide covering · "+hide.Name,typeof(MeshFilter),typeof(MeshRenderer));child.layer=30;child.transform.SetParent(root.transform,false);
                child.GetComponent<MeshFilter>().sharedMesh=LocalScatterMeshes.BuildCampHide(component.Points,cursor,end);child.GetComponent<MeshRenderer>().sharedMaterial=PortableMaterial();
                portableRoots[child]=hide;if(PortableFur(hide))portableCoatWanted.Add(hide.AppearanceSpecies);RefreshPortableRoot(child,hide);cursor=end;
            }
        }
        var collisionSource=plan.Cover==CampCover.Hide?LocalScatterMeshes.BuildBlueprint(plan,true):mesh;
        if(collisionSource.vertexCount>0)
        {
            root.layer=ScatterSolidLayer;var collision=Instantiate(collisionSource);collision.name="Camp blueprint two-sided collision";var triangles=collisionSource.triangles;var both=new int[triangles.Length*2];Array.Copy(triangles,both,triangles.Length);for(int i=0;i<triangles.Length;i+=3){both[triangles.Length+i]=triangles[i];both[triangles.Length+i+1]=triangles[i+2];both[triangles.Length+i+2]=triangles[i+1];}collision.triangles=both;
            var cache=new GameObject("Camp collision mesh",typeof(MeshFilter));cache.transform.SetParent(root.transform,false);cache.GetComponent<MeshFilter>().sharedMesh=collision;var collider=root.AddComponent<MeshCollider>();collider.sharedMesh=collision;
        }
        if(collisionSource!=mesh)Destroy(collisionSource);
        if(!CampBlueprints.Complete(plan))
        {
            if(blueprintGhostMaterial==null){blueprintGhostMaterial=new Material(Resources.Load<Shader>("WorldGenCampPlacement"));blueprintGhostMaterial.SetColor("_BaseColor",new Color(.7f,.85f,.9f,.13f));}
            AddBlueprintMesh(root,LocalScatterMeshes.BuildBlueprint(plan,false),blueprintGhostMaterial,"Remaining camp components");
        }
        int count=0;float w=(float)plan.WidthM,d=(float)plan.DepthM,h=(float)CampBlueprints.Height(plan);float rackLeft=0,rackRight=0;
        foreach(var item in StoredAt(c).OrderByDescending(i=>i.WidthM).Take(64))
        {
            var part=CreatePossessionVisual(item,30);part.transform.SetParent(root.transform,false);int i=count++;
            if(plan.Design==CampDesign.HideFrame)
            {
                // The hanging hide remains the owner's real item. Its donor
                // appearance is refreshed by the same system as worn hides.
                part.transform.localPosition=new Vector3(0,.2f+(float)Math.Max(0,plan.FloorHeights?.Max()??0),0);part.transform.localScale=new Vector3(Mathf.Min(w*.8f/(float)item.WidthM,4),Mathf.Min(((float)CampBlueprints.NominalHeight(plan)-.35f)/(float)item.HeightM,4),1);part.transform.localRotation=Quaternion.identity;
            }
            else if(plan.Design==CampDesign.DryingRack)
            {
                float span=Math.Max(.15f,(float)item.WidthM+.08f);bool left=rackLeft+span<=w;float cursor=left?rackLeft:rackRight,x=-w*.5f+cursor+span*.5f;if(left)rackLeft+=span;else rackRight+=span;
                part.transform.localPosition=new Vector3(x,h-(float)item.HeightM*.65f,left?-d*.5f:d*.5f);part.transform.localRotation=Quaternion.Euler(0,i*37,0);
            }
            else
            {
                float x=(float)((i*.61803398875)%1-.5)*w*.65f,z=(float)((i*.41421356237)%1-.5)*d*.65f;
                float y=(plan.Design==CampDesign.RaisedCache?1.32f+(float)Math.Max(0,plan.FloorHeights?.Max()??0):.08f)+(i/8)*.045f;
                if(WoodCrafting.Wood(item))
                {bool alongX=w>=d;var axis=alongX?Vector3.right:Vector3.forward;float length=(float)item.HeightM;if(alongX)x=(float)((i*.61803398875)%1-.5)*Math.Max(0,w-length);else z=(float)((i*.41421356237)%1-.5)*Math.Max(0,d-length);part.transform.localPosition=new Vector3(x,y,z)-axis*length*.5f;part.transform.localRotation=Quaternion.FromToRotation(Vector3.up,axis);}
                else{part.transform.localPosition=new Vector3(x,y,z);part.transform.localRotation=Quaternion.Euler(i*17,i*137,0);}
            }
        }
    }
}

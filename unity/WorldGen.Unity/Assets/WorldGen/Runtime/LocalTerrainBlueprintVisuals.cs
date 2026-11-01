using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    public static Mesh BuildBlueprint(CampBlueprint plan,bool completed,bool preview=false)
    {
        var b=new Builder();var tags=new List<Vector2>();
        Vector3 P(V p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        void Quad(Vector3 a,Vector3 c,Vector3 d,Vector3 e,Color color)
        {int at=b.V.Count;float w=(c-a).magnitude,h=(e-a).magnitude;b.Add(a,color,new Vector2(0,0));b.Add(c,color,new Vector2(w,0));b.Add(d,color,new Vector2(w,h));b.Add(e,color,new Vector2(0,h));b.Triangle(at,at+1,at+2);b.Triangle(at,at+2,at+3);}
        foreach(var c in CampBlueprints.Components(plan))
        {
            bool done=CampBlueprints.Done(plan,c);if(!preview&&done!=completed)continue;
            int start=b.V.Count;int surface=1;var points=c.Points.Select(P).ToArray();
            if(c.Kind==CampPartKind.Cover)
            {
                surface=plan.Cover==CampCover.Hide?4:plan.Cover==CampCover.Bark?2:7;
                var color=plan.Cover==CampCover.Hide?new Color(.55f,.4f,.27f,0):plan.Cover==CampCover.Bark?new Color(.42f,.32f,.22f,0):new Color(.54f,.46f,.24f,0);
                Quad(points[0],points[1],points[2],points[3],color);
                if(plan.Cover==CampCover.Brush)
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
        var plan=c.Blueprint;var mesh=LocalScatterMeshes.BuildBlueprint(plan,true);AddBlueprintMesh(root,mesh,PortableMaterial(),"Fitted camp components");
        if(mesh.vertexCount>0)
        {
            root.layer=ScatterSolidLayer;var collision=Instantiate(mesh);collision.name="Camp blueprint two-sided collision";var triangles=mesh.triangles;var both=new int[triangles.Length*2];Array.Copy(triangles,both,triangles.Length);for(int i=0;i<triangles.Length;i+=3){both[triangles.Length+i]=triangles[i];both[triangles.Length+i+1]=triangles[i+2];both[triangles.Length+i+2]=triangles[i+1];}collision.triangles=both;
            var cache=new GameObject("Camp collision mesh",typeof(MeshFilter));cache.transform.SetParent(root.transform,false);cache.GetComponent<MeshFilter>().sharedMesh=collision;var collider=root.AddComponent<MeshCollider>();collider.sharedMesh=collision;
        }
        if(!CampBlueprints.Complete(plan))
        {
            if(blueprintGhostMaterial==null){blueprintGhostMaterial=new Material(Resources.Load<Shader>("WorldGenCampPlacement"));blueprintGhostMaterial.SetColor("_BaseColor",new Color(.7f,.85f,.9f,.13f));}
            AddBlueprintMesh(root,LocalScatterMeshes.BuildBlueprint(plan,false),blueprintGhostMaterial,"Remaining camp components");
        }
        int count=0;float w=(float)plan.WidthM,d=(float)plan.DepthM,h=(float)CampBlueprints.Height(plan);float rackLeft=0,rackRight=0;
        foreach(var item in StoredAt(c).OrderByDescending(i=>i.WidthM).Take(64))
        {
            var part=CreatePossessionVisual(item,30);part.transform.SetParent(root.transform,false);int i=count++;
            if(plan.Design==CampDesign.DryingRack)
            {
                float span=Math.Max(.15f,(float)item.WidthM+.08f);bool left=rackLeft+span<=w;float cursor=left?rackLeft:rackRight,x=-w*.5f+cursor+span*.5f;if(left)rackLeft+=span;else rackRight+=span;
                part.transform.localPosition=new Vector3(x,h-(float)item.HeightM*.65f,left?-d*.5f:d*.5f);part.transform.localRotation=Quaternion.Euler(0,i*37,0);
            }
            else
            {
                float x=(float)((i*.61803398875)%1-.5)*w*.65f,z=(float)((i*.41421356237)%1-.5)*d*.65f;
                float y=.08f+(i/8)*.045f;
                if(WoodCrafting.Wood(item))
                {bool alongX=w>=d;var axis=alongX?Vector3.right:Vector3.forward;float length=(float)item.HeightM;if(alongX)x=(float)((i*.61803398875)%1-.5)*Math.Max(0,w-length);else z=(float)((i*.41421356237)%1-.5)*Math.Max(0,d-length);part.transform.localPosition=new Vector3(x,y,z)-axis*length*.5f;part.transform.localRotation=Quaternion.FromToRotation(Vector3.up,axis);}
                else{part.transform.localPosition=new Vector3(x,y,z);part.transform.localRotation=Quaternion.Euler(i*17,i*137,0);}
            }
        }
    }
}

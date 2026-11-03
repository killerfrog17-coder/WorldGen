using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Quaternion NpcCarryOrientation(HumanActor guardian)
    {
        var up=(guardian.Bones["spine_02"].position-guardian.Bones["pelvis"].position).normalized;
        if(up.sqrMagnitude<.5f)up=transform.up;
        up=Vector3.RotateTowards(transform.up,up,Mathf.Deg2Rad*55,0);
        var forward=Vector3.ProjectOnPlane(guardian.Root.transform.forward,up).normalized;
        return Quaternion.LookRotation(forward,up);
    }
    void UpdateNpcInfantCarrier(HumanActor infant,HumanActor guardian,bool awake)
    {
        if(infant.InfantCarrier==null)
        {
            var root=new GameObject("Infant carrying wrap");root.layer=30;root.transform.SetParent(transform,false);
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();var colors=new List<Color>();var surfaces=new List<Vector2>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2((p.x+.25f)*2,(p.y+.4f)*2));
                for(int i=0;i<4;i++){colors.Add(new Color(.58f,.42f,.27f,0));surfaces.Add(new Vector2(4,0));}
            }
            // Curved hide cradle plus two broad shoulder bands, rather than a
            // floating baby or a rigid box. Uses the existing leather atlas.
            for(int j=0;j<6;j++)for(int i=0;i<10;i++)
            {
                Vector3 P(int x,int y){float u=x/10f,v=y/6f,w=.11f+.045f*Mathf.Sin(v*Mathf.PI*.7f);return new Vector3((u*2-1)*w,-.25f+v*.34f,-.14f-.25f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(.4f+v*1.8f));}
                Quad(P(i,j),P(i+1,j),P(i+1,j+1),P(i,j+1));
            }
            foreach(float side in new[]{-1f,1f})
            {
                var a=new Vector3(side*.14f,.08f,-.27f);var shoulder=new Vector3(side*.16f,.05f,-.01f);var back=new Vector3(-side*.12f,-.37f,.15f);
                var width=Vector3.right*.035f;Quad(a-width,a+width,shoulder+width,shoulder-width);Quad(shoulder-width,shoulder+width,back+width,back-width);
            }
            var mesh=new Mesh{name="Soft infant carrying hide"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetUVs(1,surfaces);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AddCraftMesh(root,mesh,PortableMaterial(),new Possession{Tint=new V(1,1,1)},30);infant.InfantCarrier=root;
        }
        infant.InfantCarrier.SetActive(awake);
        if(awake){var rotation=NpcCarryOrientation(guardian);infant.InfantCarrier.transform.position=guardian.Bones["spine_02"].position+rotation*Vector3.up*.25f;infant.InfantCarrier.transform.rotation=rotation;}
    }
}

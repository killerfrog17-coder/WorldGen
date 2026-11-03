using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void UpdateNpcInfantCarrier(HumanActor infant,HumanActor guardian,bool awake)
    {
        if(infant.InfantCarrier==null)
        {
            var root=new GameObject("Infant carrying wrap");root.layer=30;root.transform.SetParent(transform,false);
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();var colors=new List<Color>();var surfaces=new List<Vector2>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
                for(int i=0;i<4;i++){colors.Add(new Color(.58f,.42f,.27f,0));surfaces.Add(new Vector2(4,0));}
            }
            // Curved hide cradle plus two broad shoulder bands, rather than a
            // floating baby or a rigid box. Uses the existing leather atlas.
            for(int j=0;j<6;j++)for(int i=0;i<10;i++)
            {
                Vector3 P(int x,int y){float u=x/10f,v=y/6f,w=.18f+.035f*Mathf.Sin(v*Mathf.PI);return new Vector3((u*2-1)*w,-.4f+v*.4f,.23f+.19f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(.35f+v*1.8f));}
                Quad(P(i,j),P(i+1,j),P(i+1,j+1),P(i,j+1));
            }
            foreach(float side in new[]{-1f,1f})
            {
                var a=new Vector3(side*.16f,-.02f,.24f);var shoulder=new Vector3(side*.16f,.27f,-.01f);var back=new Vector3(-side*.12f,-.32f,-.15f);
                var width=Vector3.right*.035f;Quad(a-width,a+width,shoulder+width,shoulder-width);Quad(shoulder-width,shoulder+width,back+width,back-width);
            }
            var mesh=new Mesh{name="Soft infant carrying hide"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetUVs(1,surfaces);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AddCraftMesh(root,mesh,PortableMaterial(),new Possession{Tint=new V(1,1,1)},30);infant.InfantCarrier=root;
        }
        infant.InfantCarrier.SetActive(awake);
        if(awake){infant.InfantCarrier.transform.position=guardian.Bones["spine_02"].position;infant.InfantCarrier.transform.rotation=Quaternion.Euler(0,guardian.Yaw,0);}
    }
}

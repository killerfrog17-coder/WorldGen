using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<string,Material> survivalMaterialCache=new Dictionary<string,Material>();
    Material SurvivalMaterial(string name,Color color,float smoothness=.12f)
    {
        if(survivalMaterialCache.TryGetValue(name,out var material)&&material!=null)return material;
        material=new Material(Resources.Load<Shader>("WorldGenSurvival")){name="Survival "+name,color=color};material.SetColor("_BaseColor",color);material.SetFloat("_Roughness",1-smoothness);
        survivalMaterials.Add(material);survivalMaterialCache[name]=material;return material;
    }
    Material SurvivalWood=>SurvivalMaterial("wood",new Color(.32f,.2f,.105f));
    Material SurvivalStone=>SurvivalMaterial("stone",new Color(.31f,.33f,.32f));
    Material SurvivalHide=>SurvivalMaterial("hide",new Color(.47f,.34f,.2f));
    Material SurvivalGrass=>SurvivalMaterial("dry vegetation",new Color(.43f,.38f,.19f));
    GameObject SurvivalPrimitive(GameObject parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var root=GameObject.CreatePrimitive(type);root.name=name;root.layer=30;root.transform.SetParent(parent.transform,false);root.transform.localPosition=position;root.transform.localScale=scale;
        var collider=root.GetComponent<Collider>();if(collider!=null)Destroy(collider);root.GetComponent<Renderer>().sharedMaterial=material;return root;
    }
    GameObject SurvivalPole(GameObject parent,string name,Vector3 from,Vector3 to,float width,Material material)
    {
        var root=SurvivalPrimitive(parent,name,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(width,(to-from).magnitude*.5f,width),material);
        root.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);return root;
    }
    GameObject CreateSurvivalTool(string item)
    {
        var root=new GameObject(SurvivalItems.Get(item).Name);root.layer=30;
        if(item.Contains("spear")||item=="digging-stick"||item=="club"||item=="throwing-stick")
        {
            float length=item.Contains("spear")?2.1f:item=="digging-stick"?1.3f:.75f;
            SurvivalPole(root,"Wood shaft",new Vector3(0,-length*.4f,0),new Vector3(0,length*.6f,0),.025f,SurvivalWood);
            if(item=="stone-spear")
            {
                var point=SurvivalPrimitive(root,"Stone point",PrimitiveType.Sphere,new Vector3(0,length*.6f,0),new Vector3(.055f,.19f,.025f),SurvivalStone);
                for(int i=0;i<4;i++)SurvivalPrimitive(root,"Binding",PrimitiveType.Cylinder,new Vector3(0,length*.6f-.1f-i*.012f,0),new Vector3(.032f,.006f,.032f),SurvivalGrass);
            }
        }
        else if(item=="knife"||item=="flake"||item=="handaxe"||item=="scraper")
        {
            SurvivalPrimitive(root,"Stone edge",PrimitiveType.Sphere,new Vector3(0,.13f,0),new Vector3(.09f,.22f,.025f),SurvivalStone);
            if(item=="knife")SurvivalPole(root,"Handle",new Vector3(0,-.1f,0),new Vector3(0,.07f,0),.045f,SurvivalWood);
        }
        else SurvivalPrimitive(root,"Held object",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.14f,SurvivalStone);
        return root;
    }
    GameObject CreateSurvivalPickup(string item)
    {
        var root=new GameObject("Gatherable "+item);root.layer=30;root.transform.SetParent(transform,false);
        if(item=="stick"||item=="bark"||item=="tinder")
        {
            for(int i=0;i<3;i++)SurvivalPole(root,"Branch",new Vector3(-.3f,.01f,i*.08f),new Vector3(.35f,.08f,i*.08f+.1f),item=="bark"?.09f:.035f,SurvivalWood);
        }
        else if(item=="stone"||item=="flint"||item=="pyrite"||item=="resin")
        {
            var mat=item=="pyrite"?SurvivalMaterial("pyrite",new Color(.44f,.37f,.16f),.4f):SurvivalStone;
            for(int i=0;i<3;i++){var p=SurvivalPrimitive(root,"Stone",PrimitiveType.Sphere,new Vector3((i-1)*.12f,.02f,i%2*.08f),new Vector3(.18f,.13f,.16f),mat);p.transform.localRotation=Quaternion.Euler(i*29,i*57,18);}
        }
        else if(item=="berries"||item=="nuts")
        {
            var green=SurvivalMaterial("leaves",new Color(.2f,.29f,.08f));
            for(int i=0;i<5;i++){float x=Mathf.Sin(i*2.4f)*.25f,z=Mathf.Cos(i*2.4f)*.25f;SurvivalPrimitive(root,"Plant",PrimitiveType.Sphere,new Vector3(x,.2f,z),new Vector3(.28f,.35f,.25f),green);
                SurvivalPrimitive(root,"Fruit",PrimitiveType.Sphere,new Vector3(x,.32f,z-.09f),Vector3.one*.045f,SurvivalMaterial("fruit",new Color(.35f,.08f,.08f)));}
        }
        else
        {
            for(int i=0;i<7;i++)SurvivalPole(root,"Stems",new Vector3((i-3)*.05f,0,0),new Vector3((i-3)*.08f,.2f+(i%3)*.1f,.08f),.012f,SurvivalGrass);
        }
        return root;
    }
    GameObject CreateSurvivalStructure(SurvivalSite site)
    {
        var root=new GameObject("Camp "+site.Kind);root.layer=30;root.transform.SetParent(transform,false);root.transform.localRotation=Quaternion.Euler(0,(float)site.Heading,0);
        if(site.Progress<1)
        {
            root.name+=" unfinished";
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)SurvivalPole(root,"Layout stake",new Vector3(x*1.3f,0,z*1.1f),new Vector3(x*1.3f,.55f,z*1.1f),.025f,SurvivalWood);
            return root;
        }
        if(site.Kind=="hearth")
        {
            for(int i=0;i<10;i++){float angle=i*Mathf.PI/5;SurvivalPrimitive(root,"Hearth stone",PrimitiveType.Sphere,new Vector3(Mathf.Sin(angle)*.48f,.1f,Mathf.Cos(angle)*.48f),new Vector3(.22f,.17f,.2f),SurvivalStone);}
            for(int i=0;i<4;i++)SurvivalPole(root,"Firewood",new Vector3(-.25f,.08f,i*.08f-.12f),new Vector3(.25f,.18f,-i*.08f+.12f),.09f,SurvivalWood);
            var flame=new GameObject("Fire");flame.transform.SetParent(root.transform,false);flame.layer=30;
            var glow=SurvivalMaterial("embers",new Color(.9f,.22f,.015f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(3, .65f,.02f));
            for(int i=0;i<5;i++)SurvivalPrimitive(flame,"Flame",PrimitiveType.Sphere,new Vector3(Mathf.Sin(i*2)*.12f,.25f+i*.04f,Mathf.Cos(i*2)*.12f),new Vector3(.12f,.3f,.12f),glow);
            var light=flame.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.45f,.12f);light.intensity=1.8f;light.range=8;light.shadows=LightShadows.None;
        }
        else if(site.Kind=="bed"||site.Kind=="hide-bed")
        {
            SurvivalPrimitive(root,"Bedding",PrimitiveType.Cube,new Vector3(0,.08f,0),new Vector3(.85f,.16f,1.9f),SurvivalGrass);
            if(site.Kind=="hide-bed")SurvivalPrimitive(root,"Hide cover",PrimitiveType.Cube,new Vector3(0,.17f,0),new Vector3(.85f,.025f,1.75f),SurvivalHide);
        }
        else if(site.Kind=="cache"||site.Kind=="drying-rack")
        {
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)SurvivalPole(root,"Pole",new Vector3(x*.7f,0,z*.4f),new Vector3(x*.7f,1.3f,z*.4f),.06f,SurvivalWood);
            for(int i=0;i<6;i++)SurvivalPole(root,"Rack",new Vector3(-.8f,1.2f,-.4f+i*.16f),new Vector3(.8f,1.2f,-.4f+i*.16f),.035f,SurvivalWood);
        }
        else if(site.Kind=="drop")
        {SurvivalPrimitive(root,"Dropped bundle",PrimitiveType.Sphere,new Vector3(0,.16f,0),new Vector3(.45f,.3f,.4f),SurvivalHide);}
        else if(site.Kind=="windbreak"||site.Kind=="lean-to"||site.Kind=="bark-lean-to")
        {
            float top=site.Kind=="windbreak"?1.1f:1.8f;
            for(int i=-2;i<=2;i++)SurvivalPole(root,"Frame",new Vector3(i*.5f,0,.9f),new Vector3(i*.5f,top,-.5f),.05f,SurvivalWood);
            SurvivalPole(root,"Ridge",new Vector3(-1.2f,top,-.5f),new Vector3(1.2f,top,-.5f),.06f,SurvivalWood);
            for(int i=0;i<7;i++){float t=i/6f;var panel=SurvivalPrimitive(root,"Cover",PrimitiveType.Cube,new Vector3(0,top*t,.9f-1.4f*t),new Vector3(2.5f,.1f,.35f),site.Kind=="bark-lean-to"?SurvivalWood:SurvivalGrass);panel.transform.localRotation=Quaternion.Euler(45,0,0);}
        }
        else
        {
            bool snow=site.Kind=="snow-shelter",brush=site.Kind=="brush-hut";var material=snow?SurvivalMaterial("snow",new Color(.82f,.87f,.9f)):brush?SurvivalGrass:SurvivalHide;
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;Vector3 foot=new Vector3(Mathf.Sin(a)*1.8f,0,Mathf.Cos(a)*1.8f),next=new Vector3(Mathf.Sin(b)*1.8f,0,Mathf.Cos(b)*1.8f),top=new Vector3(0,2.3f,0);
                if(i==0||i==9)continue;SurvivalPole(root,"Frame pole",foot,top,.05f,SurvivalWood);
                var mesh=new Mesh{name="Shelter panel"};mesh.vertices=new[]{foot,next,top};mesh.triangles=new[]{0,2,1,0,1,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
                var panel=new GameObject("Cover",typeof(MeshFilter),typeof(MeshRenderer));panel.layer=30;panel.transform.SetParent(root.transform,false);panel.GetComponent<MeshFilter>().sharedMesh=mesh;panel.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        return root;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckTerrainBoundaries(string output)
    {
        Directory.CreateDirectory(output);var report=new List<string>();
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("TERRAIN BOUNDARY "+message);report.Add("PASS "+message);Debug.Log("TERRAIN BOUNDARY PASS "+message);}
        var build=Task.Run(()=>
        {
            var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var sphere=SphericalMesh.Build(recipe,default);int count=sphere.Cells.Length;
            var world=new World{Recipe=recipe,Surface=sphere,ElevationM=Enumerable.Repeat(2500,count).ToArray(),FoldPermille=Enumerable.Repeat(1000,count).ToArray(),PlateauPermille=new int[count],Hydrology=new HydrologyResult{OceanConnected=new bool[count]}};
            var terrain=new LocalTerrain(world);var frame=new LocalTerrainFrame(sphere.Cells[221].Position,terrain.RadiusM,2500);
            var a=new TerrainTileKey(-1,0,1);var b=new TerrainTileKey(0,0,0);var c=new TerrainTileKey(0,1,0);
            var plan=new Dictionary<TerrainTileKey,int>{{a,64},{b,64},{c,16}};var edges=TerrainEdges.Build(plan);
            LocalTerrainMesh Mesh(TerrainTileKey k,double[] intervals)=>LocalTerrainMesh.Build(terrain,frame,k.X*k.SizeM,k.Z*k.SizeM,k.SizeM,plan[k],edgeSteps:intervals);
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);using var reader=new BinaryReader(stream);
            TerrainMeshWire.WriteRequest(writer,frame,-2048,0,2048,64,edges[a]);stream.Position=0;
            var decoded=TerrainMeshWire.ReadRequest(reader,out double east,out double north,out double size,out int segments,out var restored);
            var fixedA=LocalTerrainMesh.Build(terrain,decoded,east,north,size,segments,edgeSteps:restored);
            return new[]{Mesh(a,edges[a].Take(4).ToArray()),fixedA,Mesh(b,edges[b]),Mesh(c,edges[c])};
        });
        while(!build.IsCompleted)yield return null;var source=build.GetAwaiter().GetResult();
        double Gap(LocalTerrainMesh a,LocalTerrainMesh b,out int worst)
        {
            double gap=0;worst=0;
            for(int z=0;z<=64;z++)
            {double f=z*.5;int lo=Math.Min(63,(int)f);double t=f-lo,y=a.Positions[(lo*65+64)*3+1]*(1-t)+a.Positions[((lo+1)*65+64)*3+1]*t;double d=Math.Abs(y-b.Positions[z*65*3+1]);if(d>gap){gap=d;worst=z;}}
            return gap;
        }
        double before=Gap(source[0],source[2],out int worst),after=Gap(source[1],source[2],out _);
        Check(before>1,"Legacy whole-edge stitching reproduces a real sky gap: "+before.ToString("F6")+"m");
        Check(after<.001,"Piecewise stitching closes that same boundary: "+after.ToString("F9")+"m");
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");Check(shader!=null&&shader.isSupported,"Production terrain shader is supported");
        var material=new Material(shader);material.SetFloat("_Water",1);material.SetFloat("_Daylight",1);material.SetFloat("_FogDistance",1e9f);
        var roots=new List<GameObject>();var meshes=new List<Mesh>();
        foreach(var data in source)
        {
            // Uniform diagnostic pigment isolates holes from lighting/material
            // transitions while using the production vertex/upload/shader path.
            for(int i=0;i<data.Colors.Length;i+=4){data.Colors[i]=.55f;data.Colors[i+1]=.36f;data.Colors[i+2]=.20f;data.Colors[i+3]=1;}
            using var upload=TerrainUpload.Prepare(data,default);var parts=upload.Apply();var mesh=parts[0];meshes.Add(mesh);foreach(var extra in parts.Skip(1))Destroy(extra);
            var root=new GameObject("Boundary probe",typeof(MeshFilter),typeof(MeshRenderer));root.layer=26;root.transform.position=new Vector3((float)data.EastM,0,(float)data.NorthM);
            root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;roots.Add(root);
            Check(mesh.vertexCount==data.Positions.Length/3&&mesh.triangles.SequenceEqual(data.Triangles),"Native upload preserves terrain vertices and topology");
        }
        var uploaded=meshes[1].vertices;var right=meshes[2].vertices;double nativeGap=0;
        for(int z=0;z<=64;z++){double f=z*.5;int lo=Math.Min(63,(int)f);double t=f-lo;nativeGap=Math.Max(nativeGap,Math.Abs(uploaded[lo*65+64].y*(1-t)+uploaded[(lo+1)*65+64].y*t-right[z*65].y));}
        Check(nativeGap<.001,"Actual native mesh buffers retain the closed boundary");
        var cameraRoot=new GameObject("Boundary inspection",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();camera.cullingMask=1<<26;camera.orthographic=true;camera.orthographicSize=12;camera.nearClipPlane=.1f;camera.farClipPlane=40;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.68f,.95f);
        float y=right[worst*65].y,zAt=worst*16;camera.transform.position=new Vector3(20,y+7,zAt);camera.transform.LookAt(new Vector3(0,y,zAt));
        Color32[] Capture(string name)
        {
            var prior=RenderTexture.active;var target=new RenderTexture(1280,800,24);target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();var pixels=texture.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=prior;target.Release();Destroy(target);Destroy(texture);return pixels;
        }
        roots[1].SetActive(false);yield return null;var legacy=Capture("legacy-sky-gap");
        roots[0].SetActive(false);roots[1].SetActive(true);yield return null;var corrected=Capture("corrected-boundary");
        int changed=corrected.Where((p,i)=>Math.Abs(p.r-legacy[i].r)+Math.Abs(p.g-legacy[i].g)+Math.Abs(p.b-legacy[i].b)>6).Count();
        Check(changed>100,"Actual production-shader boundary views differ from the broken positive control: "+changed+" pixels");
        Check(corrected.Count(p=>p.r>p.b&&p.r>p.g)>1000,"The corrected native view actually draws the ground");
        foreach(var root in roots)Destroy(root);foreach(var mesh in meshes)Destroy(mesh);Destroy(material);Destroy(cameraRoot);
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("TERRAIN BOUNDARY COMPLETE "+report.Count);Application.Quit(0);
    }
}

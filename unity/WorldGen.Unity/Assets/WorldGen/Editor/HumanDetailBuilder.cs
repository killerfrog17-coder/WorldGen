using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

public static class HumanDetailBuilder
{
    const string Folder="Assets/WorldGen/Resources/HumanDetail";
    [Serializable] public sealed class Row {public string Source;public int Original,Middle,Far;}
    public static void Build()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var source=new List<Mesh>();
        var adults=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");
        var ages=Resources.Load<WorldGenHumanAgeLibrary>("HumanAges/Library");
        foreach(var prefab in adults.People.Select(p=>p.Prefab).Concat(ages.People.Select(p=>p.Prefab)))
            foreach(var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(r.name.ToLowerInvariant().EndsWith(".body")||r.name.EndsWith("_Body"))source.Add(r.sharedMesh);
        source=source.Distinct().ToList();if(source.Count<24)throw new InvalidDataException("Adult and child bodies not all selected: "+source.Count);
        var entries=new List<HumanDetailMesh>();var rows=new List<Row>();
        foreach(var mesh in source)
        {
            string key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh));
            Mesh Reduce(float quality,string tier)
            {
                var simplifier=new MeshSimplifier();var options=SimplificationOptions.Default;
                options.PreserveBorderEdges=true;options.PreserveUVSeamEdges=true;options.PreserveUVFoldoverEdges=true;options.PreserveSurfaceCurvature=true;
                simplifier.SimplificationOptions=options;simplifier.Initialize(mesh);simplifier.SimplifyMesh(quality);var reduced=simplifier.ToMesh();reduced.name="Human "+tier+" "+key;
                if(reduced.vertexCount<=0||reduced.boneWeights.Length!=reduced.vertexCount||reduced.bindposes.Length!=mesh.bindposes.Length||reduced.uv.Length!=reduced.vertexCount)throw new InvalidDataException("Incomplete reduced skin: "+key);
                foreach(var weight in reduced.boneWeights)
                {double total=weight.weight0+weight.weight1+weight.weight2+weight.weight3;if(Math.Abs(total-1)>.002)throw new InvalidDataException("Invalid reduced skin weight");}
                for(int i=0;i<mesh.bindposes.Length;i++)if(reduced.bindposes[i]!=mesh.bindposes[i])throw new InvalidDataException("Changed native bind pose");
                string path=Folder+"/"+key+"-"+tier+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old==null)AssetDatabase.CreateAsset(reduced,path);else{EditorUtility.CopySerialized(reduced,old);UnityEngine.Object.DestroyImmediate(reduced);reduced=old;}
                return reduced;
            }
            var middle=Reduce(.38f,"middle");var far=Reduce(.13f,"far");
            entries.Add(new HumanDetailMesh{Original=mesh,Middle=middle,Far=far});rows.Add(new Row{Source=AssetDatabase.GetAssetPath(mesh),Original=mesh.vertexCount,Middle=middle.vertexCount,Far=far.vertexCount});
        }
        string libraryPath=Folder+"/Library.asset";var library=AssetDatabase.LoadAssetAtPath<WorldGenHumanDetailLibrary>(libraryPath);
        if(library==null){library=ScriptableObject.CreateInstance<WorldGenHumanDetailLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}library.Meshes=entries.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));Directory.CreateDirectory(Path.Combine(repo,"assets/verification"));File.WriteAllText(Path.Combine(repo,"assets/verification/human-distance-meshes-20261110.json"),WorldGen.Serialization.WorldJson.Write(rows.ToArray()));
        Debug.Log("HUMAN DISTANCE MESHES "+entries.Count+" complete skins with unchanged native bind poses");
    }
}

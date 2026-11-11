using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

// Match the actual imported baseline skin before admitting any morph. Native
// rigs, weights, UVs and original vertices are retained exactly in the result.
public static class HumanPhenotypeBuilder
{
    const string Input="Assets/WorldGen/GeneratedHumanPhenotypes",Output="Assets/WorldGen/Resources/HumanPhenotypes";
    static string Part(string name)=>name.Substring(name.IndexOf('.')+1);
    static (long,long,long,long,long) Key(Vector3 v,Vector2 uv)=>((long)Math.Round(v.x*100000),(long)Math.Round(v.y*100000),(long)Math.Round(v.z*100000),(long)Math.Round(uv.x*100000),(long)Math.Round(uv.y*100000));
    static void Check(bool ok,string why){if(!ok)throw new InvalidDataException("Human phenotype import: "+why);}
    public static void Build()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        Directory.CreateDirectory(Input);Directory.CreateDirectory(Output);
        foreach(string file in Directory.GetFiles(Path.Combine(repo,"assets-source/humans/phenotypes/exports"),"*.fbx"))File.Copy(file,Path.Combine(Input,Path.GetFileName(file)),true);
        AssetDatabase.Refresh();var library=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");var entries=new List<HumanPhenotypeMesh>();var report=new List<string>();
        foreach(var human in library.People)
        {
            string path=Input+"/human-"+human.Variant.ToLowerInvariant()+"-morphs.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.importBlendShapes=true;importer.isReadable=true;importer.optimizeGameObjects=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var incoming=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var sources=incoming.GetComponentsInChildren<SkinnedMeshRenderer>().ToDictionary(r=>Part(r.name));
                foreach(var renderer in human.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    string part=Part(renderer.name);Check(sources.TryGetValue(part,out var source),human.Variant+" missing "+part);
                    Mesh native=renderer.sharedMesh,imported=source.sharedMesh;
                    var positions=imported.vertices;var uv=imported.uv;var matrix=source.localToWorldMatrix;
                    var lookup=new Dictionary<(long,long,long,long,long),int>();
                    for(int i=0;i<positions.Length;i++)lookup[Key(matrix.MultiplyPoint3x4(positions[i]),uv[i])]=i;
                    var targetPositions=native.vertices;var targetUv=native.uv;var map=new int[native.vertexCount];int unmatched=0;float worst=0;
                    for(int i=0;i<map.Length;i++)
                    {
                        if(!lookup.TryGetValue(Key(targetPositions[i],targetUv[i]),out map[i]))
                        {
                            // FBX imports can round the last coordinate bit or
                            // split a smoothing seam differently. Require the
                            // same UV and an actual micrometre-scale position.
                            float distance=float.MaxValue;int best=-1;
                            for(int j=0;j<positions.Length;j++)
                            {
                                if((uv[j]-targetUv[i]).sqrMagnitude>1e-9f)continue;
                                float error=(matrix.MultiplyPoint3x4(positions[j])-targetPositions[i]).sqrMagnitude;
                                if(error<distance){distance=error;best=j;}
                            }
                            if(best<0||distance>1e-8f){unmatched++;continue;}map[i]=best;worst=Mathf.Max(worst,Mathf.Sqrt(distance));
                        }
                    }
                    Check(unmatched==0,human.Variant+"/"+part+" unmatched native vertices "+unmatched);
                    Check(imported.blendShapeCount==24,human.Variant+"/"+part+" expected24 target frames, found"+imported.blendShapeCount);
                    var result=UnityEngine.Object.Instantiate(native);result.name=human.Variant+" "+part+" inherited morphs";result.ClearBlendShapes();
                    var delta=new Vector3[positions.Length];var normals=new Vector3[positions.Length];var tangent=new Vector3[positions.Length];
                    var mapped=new Vector3[map.Length];var mappedNormals=new Vector3[map.Length];var normalMatrix=matrix.inverse.transpose;
                    for(int shape=0;shape<imported.blendShapeCount;shape++)
                    {
                        imported.GetBlendShapeFrameVertices(shape,0,delta,normals,tangent);
                        for(int i=0;i<map.Length;i++){mapped[i]=matrix.MultiplyVector(delta[map[i]]);mappedNormals[i]=normalMatrix.MultiplyVector(normals[map[i]]);}
                        string name=imported.GetBlendShapeName(shape);name=name.Substring(name.LastIndexOf('.')+1);
                        result.AddBlendShapeFrame(name,100,mapped,mappedNormals,null);
                    }
                    string stem=human.Variant+"-"+part;Mesh Save(Mesh mesh,string suffix)
                    {string target=Output+"/"+stem+"-"+suffix+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(target);if(old==null)AssetDatabase.CreateAsset(mesh,target);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}return mesh;}
                    var full=Save(result,"full");Mesh middle=null,far=null;
                    if(part=="body")
                    {
                        Mesh Reduce(float quality,string suffix)
                        {
                            var reduce=new MeshSimplifier();var options=SimplificationOptions.Default;options.PreserveBorderEdges=options.PreserveUVSeamEdges=options.PreserveUVFoldoverEdges=options.PreserveSurfaceCurvature=true;
                            reduce.SimplificationOptions=options;reduce.Initialize(full);reduce.SimplifyMesh(quality);var mesh=reduce.ToMesh();
                            Check(mesh.blendShapeCount==full.blendShapeCount,"LOD lost inherited morphs");Check(mesh.bindposes.SequenceEqual(native.bindposes),"LOD changed native bind poses");return Save(mesh,suffix);
                        }
                        middle=Reduce(.38f,"middle");far=Reduce(.13f,"far");
                    }
                    entries.Add(new HumanPhenotypeMesh{Variant=human.Variant,Part=part,Original=native,Morphs=full,Middle=middle,Far=far});
                    report.Add("PASS "+human.Variant+"/"+part+": "+native.vertexCount+" exact native vertices;24 morphs; max match tolerance"+worst.ToString("R"));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(incoming);}
        }
        string asset=Output+"/Library.asset";var bank=AssetDatabase.LoadAssetAtPath<WorldGenHumanPhenotypeLibrary>(asset);
        if(bank==null){bank=ScriptableObject.CreateInstance<WorldGenHumanPhenotypeLibrary>();AssetDatabase.CreateAsset(bank,asset);}bank.Meshes=entries.ToArray();EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();
        string evidence=Path.Combine(repo,"artifacts/npc-ancestry-20261111");Directory.CreateDirectory(evidence);File.WriteAllLines(Path.Combine(evidence,"morph-import.txt"),report);
        Debug.Log("HUMAN PHENOTYPE MORPHS "+entries.Count+" native skins with preserved weights and reduced morph LODs");
    }
}

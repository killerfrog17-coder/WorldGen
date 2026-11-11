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
    public static void Review()
    {
        ShaderUtil.allowAsyncCompilation=false;
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var routine=LocalTerrainExplorer.CheckHumanPhenotypes(Path.Combine(repo,"artifacts/npc-ancestry-20261111/world-samples.json"),Path.Combine(repo,"artifacts/npc-ancestry-20261111/editor-gallery"),false);
        double nextFrame=0;
        void Advance()
        {
            if(EditorApplication.timeSinceStartup<nextFrame)return;
            nextFrame=EditorApplication.timeSinceStartup+1.0/30;
            try
            {
                if(routine.MoveNext()){EditorApplication.QueuePlayerLoopUpdate();return;}
                EditorApplication.update-=Advance;EditorApplication.Exit(0);
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.update-=Advance;EditorApplication.Exit(1);}
        }
        EditorApplication.update+=Advance;
    }
    public static void RepairCurlyCrop()
    {
        var bank=Resources.Load<WorldGenHumanPhenotypeLibrary>("HumanPhenotypes/Library");
        foreach(var crop in bank.Hair.Where(h=>h.Style==WorldGen.Core.HumanHairStyle.Cropped&&h.Curl==1))
        {
            var original=bank.Hair.Single(h=>h.Variant==crop.Variant&&h.Style==WorldGen.Core.HumanHairStyle.LooseShort&&h.Curl==1);
            EditorUtility.CopySerialized(original.Surface.Mesh,crop.Surface.Mesh);EditorUtility.SetDirty(crop.Surface.Mesh);
            crop.Surface.Frames=original.Surface.Frames;
        }
        EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();
    }
    public static void RepairHairTextures()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var manifest=JsonUtility.FromJson<Manifest>("{\"rows\":"+File.ReadAllText(Path.Combine(repo,"assets-source/humans/phenotypes/exports/manifest.json"))+"}");
        var bank=Resources.Load<WorldGenHumanPhenotypeLibrary>("HumanPhenotypes/Library");
        var imported=new Dictionary<string,Texture2D>();
        foreach(var row in manifest.rows)foreach(var hair in row.hair)
        {
            if(!imported.TryGetValue(hair.texture,out var texture))
            {
                string path=Input+"/"+Path.GetFileName(Path.GetDirectoryName(hair.texture))+"-"+Path.GetFileName(hair.texture);
                File.Copy(Path.Combine(repo,hair.texture),path,true);AssetDatabase.ImportAsset(path);
                var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=true;ti.mipmapEnabled=true;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
                texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);imported[hair.texture]=texture;
            }
            var entry=bank.Hair.Single(h=>h.Variant==row.variant&&h.Part==hair.part);entry.Material.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(entry.Material);
        }
        bank.Pigments=JsonUtility.FromJson<PigmentList>(File.ReadAllText(Path.Combine(repo,"assets-source/humans/phenotypes/pigments.json"))).rows.Select(p=>new HumanPigmentReference{Texture=AssetDatabase.LoadAssetAtPath<Texture2D>(p.path),Luminance=p.luminance}).Where(p=>p.Texture!=null).ToArray();
        EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();
    }
    static void SmoothHair(Mesh mesh)
    {
        var vertices=mesh.vertices;var old=mesh.normals;var triangles=mesh.triangles;
        (long,long,long) At(int i)=>((long)Math.Round(vertices[i].x*1e7),(long)Math.Round(vertices[i].y*1e7),(long)Math.Round(vertices[i].z*1e7));
        var sums=new Dictionary<(long,long,long),Vector3>();
        for(int t=0;t<triangles.Length;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],c=triangles[t+2];var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
            if(Vector3.Dot(normal,old[a]+old[b]+old[c])<0)normal=-normal;
            foreach(int i in new[]{a,b,c}){var key=At(i);sums.TryGetValue(key,out var value);sums[key]=value+normal;}
        }
        mesh.normals=Enumerable.Range(0,vertices.Length).Select(i=>sums.TryGetValue(At(i),out var value)&&value.sqrMagnitude>1e-20f?value.normalized:old[i]).ToArray();
    }
    public static void RepairHairNormals()
    {
        var bank=Resources.Load<WorldGenHumanPhenotypeLibrary>("HumanPhenotypes/Library");
        foreach(var mesh in bank.Hair.Select(h=>h.Surface.Mesh).Distinct()){SmoothHair(mesh);EditorUtility.SetDirty(mesh);}
        AssetDatabase.SaveAssets();
    }
    static void TrimAfroCards(Mesh mesh)
    {
        var uv=mesh.uv;var source=mesh.triangles;var kept=new List<int>();
        for(int t=0;t<source.Length;t+=3)
        {
            int a=source[t],b=source[t+1],c=source[t+2];
            // The source atlas's lower strips are overlapping flyaway cards.
            // Retain the continuous inner/outer caps; these strips made a grid
            // of depth intersections across the close cropped scalp.
            if((uv[a].y+uv[b].y+uv[c].y)/3<.4f)continue;
            kept.Add(a);kept.Add(b);kept.Add(c);
        }
        Check(kept.Count>300,"curly scalp lost its continuous cap");mesh.triangles=kept.ToArray();SmoothHair(mesh);
    }
    public static void RepairAfroCards()
    {
        var bank=Resources.Load<WorldGenHumanPhenotypeLibrary>("HumanPhenotypes/Library");
        foreach(var hair in bank.Hair.Where(h=>h.Curl==1&&(h.Style==WorldGen.Core.HumanHairStyle.Cropped||h.Style==WorldGen.Core.HumanHairStyle.LooseShort)))
        {TrimAfroCards(hair.Surface.Mesh);EditorUtility.SetDirty(hair.Surface.Mesh);}
        AssetDatabase.SaveAssets();
    }
    [Serializable] sealed class Manifest{public Export[] rows;}
    [Serializable] sealed class Export{public string variant;public HairInfo[] hair;}
    [Serializable] sealed class HairInfo{public string part,style,texture;public int curl;}
    [Serializable] sealed class PigmentList{public Pigment[] rows;}
    [Serializable] sealed class Pigment{public string path;public float luminance;}
    static HumanMorphSurface Sparse(Mesh source,Mesh native=null)
    {
        var frames=new List<HumanMorphDelta>();var p=new Vector3[source.vertexCount];var n=new Vector3[source.vertexCount];
        for(int i=0;i<source.blendShapeCount;i++)
        {
            source.GetBlendShapeFrameVertices(i,0,p,n,null);var indices=new List<int>();
            for(int v=0;v<p.Length;v++)if(p[v].sqrMagnitude>1e-12f||n[v].sqrMagnitude>1e-10f)indices.Add(v);
            if(indices.Count>0)frames.Add(new HumanMorphDelta{Target=source.GetBlendShapeName(i),Vertices=indices.ToArray(),Positions=indices.Select(v=>p[v]).ToArray(),Normals=indices.Select(v=>n[v]).ToArray()});
        }
        if(native==null)source.ClearBlendShapes();
        return new HumanMorphSurface{Mesh=native??source,Frames=frames.ToArray()};
    }
    public static void Build()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        Directory.CreateDirectory(Input);Directory.CreateDirectory(Output);
        foreach(string file in Directory.GetFiles(Path.Combine(repo,"assets-source/humans/phenotypes/exports"),"*.fbx"))File.Copy(file,Path.Combine(Input,Path.GetFileName(file)),true);
        AssetDatabase.Refresh();var library=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");var entries=new List<HumanPhenotypeMesh>();var report=new List<string>();var hairEntries=new List<HumanGroomedMesh>();
        string manifestPath=Path.Combine(repo,"assets-source/humans/phenotypes/exports/manifest.json");
        var manifest=JsonUtility.FromJson<Manifest>("{\"rows\":"+File.ReadAllText(manifestPath)+"}");
        var ages=Resources.Load<WorldGenHumanAgeLibrary>("HumanAges/Library");
        var templates=library.People.Concat(ages.People.Select(p=>new HumanRuntimeDefinition{Variant=p.Variant+"-"+p.Years,Prefab=p.Prefab}));
        foreach(var human in templates)
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
                    var targetPositions=native.vertices;var targetUv=native.uv;var map=new int[native.vertexCount];int unmatched=0;float worst=0;var targetMatrix=renderer.localToWorldMatrix;
                    for(int i=0;i<map.Length;i++)
                    {
                        if(!lookup.TryGetValue(Key(targetMatrix.MultiplyPoint3x4(targetPositions[i]),targetUv[i]),out map[i]))
                        {
                            // FBX imports can round the last coordinate bit or
                            // split a smoothing seam differently. Require the
                            // same UV and an actual micrometre-scale position.
                            float distance=float.MaxValue;int best=-1;
                            for(int j=0;j<positions.Length;j++)
                            {
                                if((uv[j]-targetUv[i]).sqrMagnitude>1e-9f)continue;
                                float error=(matrix.MultiplyPoint3x4(positions[j])-targetMatrix.MultiplyPoint3x4(targetPositions[i])).sqrMagnitude;
                                if(error<distance){distance=error;best=j;}
                            }
                            if(best<0||distance>1e-8f){unmatched++;continue;}map[i]=best;worst=Mathf.Max(worst,Mathf.Sqrt(distance));
                        }
                    }
                    Check(unmatched==0,human.Variant+"/"+part+" unmatched native vertices "+unmatched);
                    Check(imported.blendShapeCount==24,human.Variant+"/"+part+" expected24 target frames, found"+imported.blendShapeCount);
                    var result=UnityEngine.Object.Instantiate(native);result.name=human.Variant+" "+part+" inherited morphs";result.ClearBlendShapes();
                    var delta=new Vector3[positions.Length];var normals=new Vector3[positions.Length];var tangent=new Vector3[positions.Length];
                    var mapped=new Vector3[map.Length];var mappedNormals=new Vector3[map.Length];var toNative=renderer.worldToLocalMatrix*matrix;var normalMatrix=toNative.inverse.transpose;var sourceNormals=imported.normals;
                    for(int shape=0;shape<imported.blendShapeCount;shape++)
                    {
                        imported.GetBlendShapeFrameVertices(shape,0,delta,normals,tangent);
                        for(int i=0;i<map.Length;i++){int v=map[i];mapped[i]=toNative.MultiplyVector(delta[v]);mappedNormals[i]=normalMatrix.MultiplyVector(sourceNormals[v]+normals[v]).normalized-normalMatrix.MultiplyVector(sourceNormals[v]).normalized;}
                        string name=imported.GetBlendShapeName(shape);name=name.Substring(name.LastIndexOf('.')+1);
                        result.AddBlendShapeFrame(name,100,mapped,mappedNormals,null);
                    }
                    string stem=human.Variant+"-"+part;Mesh Save(Mesh mesh,string suffix)
                    {string target=Output+"/"+stem+"-"+suffix+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(target);if(old==null)AssetDatabase.CreateAsset(mesh,target);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}return mesh;}
                    var full=Sparse(result,native);HumanMorphSurface middle=null,far=null;
                    if(part=="body"||part.EndsWith("_Body",StringComparison.Ordinal))
                    {
                        HumanMorphSurface Reduce(float quality,string suffix)
                        {
                            var reduce=new MeshSimplifier();var options=SimplificationOptions.Default;options.PreserveBorderEdges=options.PreserveUVSeamEdges=options.PreserveUVFoldoverEdges=options.PreserveSurfaceCurvature=true;
                            reduce.SimplificationOptions=options;reduce.Initialize(result);reduce.SimplifyMesh(quality);var mesh=reduce.ToMesh();
                            Check(mesh.blendShapeCount==result.blendShapeCount,"LOD lost inherited morphs");Check(mesh.bindposes.SequenceEqual(native.bindposes),"LOD changed native bind poses");var sparse=Sparse(mesh);sparse.Mesh=Save(mesh,suffix);return sparse;
                        }
                        middle=Reduce(.38f,"middle");far=Reduce(.13f,"far");
                    }
                    entries.Add(new HumanPhenotypeMesh{Variant=human.Variant,Part=part,Original=native,Full=full,Middle=middle,Far=far});
                    UnityEngine.Object.DestroyImmediate(result);
                    string obsolete=Output+"/"+stem+"-full.asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(obsolete)!=null)AssetDatabase.DeleteAsset(obsolete);
                    report.Add("PASS "+human.Variant+"/"+part+": "+native.vertexCount+" exact native vertices;24 morphs; max match tolerance"+worst.ToString("R"));
                }
                var body=human.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>Part(r.name)=="body"||r.name.EndsWith("_Body",StringComparison.Ordinal));
                var boneIndices=body.bones.Select((b,i)=>(b.name,i)).ToDictionary(p=>p.name,p=>p.i);
                foreach(var hair in manifest.rows.Single(r=>r.variant==human.Variant).hair??Array.Empty<HairInfo>())
                {
                    string sourcePart=hair.style=="Cropped"&&hair.curl==1?manifest.rows.Single(r=>r.variant==human.Variant).hair.Single(h=>h.style=="LooseShort"&&h.curl==1).part:hair.part;
                    var source=sources[sourcePart];var imported=source.sharedMesh;var matrix=body.worldToLocalMatrix*source.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                    var mesh=UnityEngine.Object.Instantiate(imported);mesh.ClearBlendShapes();mesh.vertices=imported.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();mesh.normals=imported.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();
                    int[] remap=source.bones.Select(b=>{Check(boneIndices.ContainsKey(b.name),"hair bone absent "+b.name);return boneIndices[b.name];}).ToArray();
                    var weights=mesh.boneWeights;for(int i=0;i<weights.Length;i++)
                    {var w=weights[i];w.boneIndex0=remap[w.boneIndex0];w.boneIndex1=remap[w.boneIndex1];w.boneIndex2=remap[w.boneIndex2];w.boneIndex3=remap[w.boneIndex3];weights[i]=w;}mesh.boneWeights=weights;mesh.bindposes=body.sharedMesh.bindposes;
                    mesh.colors32=Enumerable.Repeat(new Color32(255,0,0,255),mesh.vertexCount).ToArray();
                    var dp=new Vector3[mesh.vertexCount];var dn=new Vector3[mesh.vertexCount];var dt=new Vector3[mesh.vertexCount];
                    for(int shape=0;shape<imported.blendShapeCount;shape++)
                    {
                        imported.GetBlendShapeFrameVertices(shape,0,dp,dn,dt);
                        var sourceNormals=imported.normals;
                        for(int i=0;i<dp.Length;i++){dp[i]=matrix.MultiplyVector(dp[i]);dn[i]=normalMatrix.MultiplyVector(sourceNormals[i]+dn[i]).normalized-normalMatrix.MultiplyVector(sourceNormals[i]).normalized;}
                        string name=imported.GetBlendShapeName(shape);mesh.AddBlendShapeFrame(name.Substring(name.LastIndexOf('.')+1),100,dp,dn,null);
                    }
                    if(hair.curl==1&&(hair.style=="Cropped"||hair.style=="LooseShort"))TrimAfroCards(mesh);else SmoothHair(mesh);
                    var surface=Sparse(mesh);mesh.RecalculateBounds();string target=Output+"/"+human.Variant+"-"+hair.part+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    if(existing==null)AssetDatabase.CreateAsset(mesh,target);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);surface.Mesh=existing;}
                    string texture=Input+"/"+Path.GetFileName(Path.GetDirectoryName(hair.texture))+"-"+Path.GetFileName(hair.texture);File.Copy(Path.Combine(repo,hair.texture),texture,true);AssetDatabase.ImportAsset(texture);
                    var ti=(TextureImporter)AssetImporter.GetAtPath(texture);ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=true;ti.mipmapEnabled=true;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
                    string materialPath=Output+"/hair-"+Path.GetFileNameWithoutExtension(texture)+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null){material=new Material(Shader.Find("WorldGen/Human"));AssetDatabase.CreateAsset(material,materialPath);}material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));material.SetFloat("_Cutoff",.3f);material.SetFloat("_Roughness",.8f);EditorUtility.SetDirty(material);
                    hairEntries.Add(new HumanGroomedMesh{Variant=human.Variant,Part=hair.part,Style=(WorldGen.Core.HumanHairStyle)Enum.Parse(typeof(WorldGen.Core.HumanHairStyle),hair.style),Curl=hair.curl,Surface=surface,Material=material});
                    report.Add("PASS "+human.Variant+"/"+hair.part+": fitted to native body; "+surface.Mesh.vertexCount+" vertices; sparse morph scalp; "+hair.style+" curl"+hair.curl);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(incoming);}
        }
        string asset=Output+"/Library.asset";var bank=AssetDatabase.LoadAssetAtPath<WorldGenHumanPhenotypeLibrary>(asset);
        if(bank==null){bank=ScriptableObject.CreateInstance<WorldGenHumanPhenotypeLibrary>();AssetDatabase.CreateAsset(bank,asset);}bank.Meshes=entries.ToArray();bank.Hair=hairEntries.ToArray();
        string pigments=Path.Combine(repo,"assets-source/humans/phenotypes/pigments.json");
        if(File.Exists(pigments))bank.Pigments=JsonUtility.FromJson<PigmentList>(File.ReadAllText(pigments)).rows.Select(p=>new HumanPigmentReference{Texture=AssetDatabase.LoadAssetAtPath<Texture2D>(p.path),Luminance=p.luminance}).Where(p=>p.Texture!=null).ToArray();
        EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();
        string evidence=Path.Combine(repo,"artifacts/npc-ancestry-20261111");Directory.CreateDirectory(evidence);File.WriteAllLines(Path.Combine(evidence,"morph-import.txt"),report);
        Debug.Log("HUMAN PHENOTYPE MORPHS "+entries.Count+" native skins with preserved weights and reduced morph LODs");
    }
}

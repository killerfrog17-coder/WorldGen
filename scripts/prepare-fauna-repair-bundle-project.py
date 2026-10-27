"""Prepare an isolated asset-only Unity converter; never compile the live game."""
import hashlib,json,shutil
from pathlib import Path

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-eye-closure-repair'
project=root/'bundle-project'
editor=project/'Assets/Editor';editor.mkdir(parents=True,exist_ok=True)
runtime=repo/'unity/WorldGen.Unity/Assets/WorldGen'
source=(runtime/'Editor/FaunaRuntimeBuilder.cs').read_text()
source=source.replace('string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));',f'string root=@"{repo}";')
source=source.replace('string output=Path.Combine(Application.streamingAssetsPath,"fauna");',f'string output=@"{root / "bundles"}";')
source=source.replace('Path.Combine(Application.dataPath,"WorldGen/Editor/FaunaRuntimeBuilder.cs")','Path.Combine(Application.dataPath,"Editor/FaunaRuntimeBuilder.cs")')
(editor/'FaunaRuntimeBuilder.cs').write_text(source)
for suffix in ['', '.meta']:
    shutil.copy2(runtime/('Runtime/WorldGenFaunaAsset.cs'+suffix),project/'Assets'/('WorldGenFaunaAsset.cs'+suffix))
plugins=project/'Assets/Plugins';plugins.mkdir(exist_ok=True)
inputs=[]
for name in ['WorldGen.Core','WorldGen.Serialization','WorldGen.Presentation','DelaunatorSharp']:
    path=repo/'unity/WorldGen.Unity/Assets/Plugins/WorldGen'/(name+'.dll')
    shutil.copy2(path,plugins/path.name)
    inputs.append({'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
resources=project/'Assets/WorldGen/Resources';resources.mkdir(parents=True,exist_ok=True)
shutil.copy2(runtime/'Resources/WorldGenFaunaCatalogue.json',resources/'WorldGenFaunaCatalogue.json')
(editor/'FaunaBundleRepairAudit.cs').write_text('''using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class FaunaBundleRepairAudit {
    [Serializable] sealed class CheckModel { public string source_sha256; public int vertices; }
    public static void Probe() {
        string directory=@"'''+str(repo/'unity/WorldGen.Unity/Assets/StreamingAssets/fauna')+'''";
        foreach(string slug in new[]{"raccoon","wolf","deer"}) {
            var bundle=AssetBundle.LoadFromFile(Path.Combine(directory,slug+".fauna"));
            if(bundle==null)throw new Exception("Cannot load existing bundle "+slug);
            var asset=bundle.LoadAllAssets<WorldGenFaunaAsset>().Single();
            if(asset.Mesh==null||asset.Poses.Length==0||asset.Coats==null)throw new Exception("Existing type incompatible "+slug);
            Debug.Log("EXISTING_FAUNA_TYPE_COMPATIBLE "+slug+" "+asset.SourceSha256+" "+asset.Mesh.vertexCount);
            bundle.Unload(true);
        }
    }
    public static void Verify() {
        string directory=@"'''+str(root/'bundles')+'''";
        int count=0;
        foreach(var path in Directory.GetFiles(directory,"*.fauna")) {
            var bundle=AssetBundle.LoadFromFile(path);
            if(bundle==null)throw new Exception("Cannot load repaired bundle "+path);
            var asset=bundle.LoadAllAssets<WorldGenFaunaAsset>().Single();
            if(asset.Mesh==null||asset.Poses.Length==0||asset.Coats==null||asset.Mask==null||asset.Normal==null)throw new Exception("Incomplete "+path);
            if(asset.Mesh.vertexCount>200000)throw new Exception("Mesh limit "+path);
            string export=Path.Combine(@"'''+str(repo/'artifacts/fauna-runtime-20261026/exports')+'''",asset.Slug,"model.json");
            var model=JsonUtility.FromJson<CheckModel>(File.ReadAllText(export));
            if(asset.SourceSha256!=model.source_sha256||asset.Mesh.vertexCount!=model.vertices)throw new Exception("Wrong source/mesh "+path);
            var cached=AssetDatabase.LoadAssetAtPath<WorldGenFaunaAsset>("Assets/WorldGen/Editor/GeneratedFauna/"+asset.Slug+".asset");
            if(cached==null||!asset.Poses.SequenceEqual(cached.Poses))throw new Exception("Pose payload mismatch "+path);
            if(!asset.Mask.GetRawTextureData().SequenceEqual(cached.Mask.GetRawTextureData()))throw new Exception("Mask payload mismatch "+path);
            if(!asset.Normal.GetRawTextureData().SequenceEqual(cached.Normal.GetRawTextureData()))throw new Exception("Normal payload mismatch "+path);
            Debug.Log("REPAIRED_FAUNA_VERIFIED "+asset.Slug+" "+asset.SourceSha256+" "+asset.Mesh.vertexCount);
            bundle.Unload(true);count++;
        }
        if(count!=177)throw new Exception("Expected 177 bundles, got "+count);
        File.WriteAllText(@"'''+str(root/'bundle-validation.json')+'''", "{\\"count\\":177,\\"all_load_with_original_game_asset_type\\":true}");
    }
}''')
packages=project/'Packages';packages.mkdir(exist_ok=True)
(packages/'manifest.json').write_text(json.dumps({'dependencies':{'com.unity.modules.imageconversion':'1.0.0','com.unity.modules.jsonserialize':'1.0.0','com.unity.modules.assetbundle':'1.0.0'}},indent=2))
settings=project/'ProjectSettings';settings.mkdir(exist_ok=True)
(settings/'ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.0f1\n')
(root/'isolated-bundle-builder.json').write_text(json.dumps({'project':str(project),'inputs':inputs,'source_builder_sha256':hashlib.sha256((runtime/'Editor/FaunaRuntimeBuilder.cs').read_bytes()).hexdigest()},indent=2))
print('ISOLATED_FAUNA_CONVERTER_READY',project)

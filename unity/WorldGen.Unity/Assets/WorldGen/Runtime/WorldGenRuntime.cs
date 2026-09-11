using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed class WorldGenRuntime : MonoBehaviour
{
    Recipe recipe=Recipe.Preset("Preview");World world;Task<World> job;CancellationTokenSource cancel;
    string progress="Ready",error="",inspection="Click the globe to inspect a cell.",filePath;int percent,seaLevel;Layer layer;
    GameObject globe;Mesh mesh,lineMesh;Camera cameraView;Vector2 scroll,layerScroll;bool[] panels={true,true,false,false,false,false};bool layerMenu,dirty;string[] groups={"World","Plates","Continents","Tectonics","Erosion","Hydrology"};string preset="Preview";int presetIndex;
    readonly string[] presets={"Preview","Balanced","Connected","Isolated","Glacial","500 microplates"};Vector2 previousMouse;bool dragging;float dragDistance;bool screenshotPending;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(FindAnyObjectByType<WorldGenRuntime>()==null)new GameObject("WorldGen Runtime").AddComponent<WorldGenRuntime>();}
    void Start()
    {
        Application.runInBackground=true;filePath=Path.Combine(Application.persistentDataPath,"worldgen.recipe.json");
        if(Environment.GetCommandLineArgs().Contains("--worldgen-check")){RunPlayerChecks();return;}
        cameraView=Camera.main;if(cameraView==null)cameraView=new GameObject("WorldGen Camera",typeof(Camera)).GetComponent<Camera>();
        cameraView.transform.position=new Vector3(0,0,-3.2f);cameraView.transform.LookAt(Vector3.zero);cameraView.clearFlags=CameraClearFlags.SolidColor;cameraView.backgroundColor=new Color(.035f,.065f,.09f);cameraView.fieldOfView=45;
        globe=new GameObject("Authoritative World",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};globe.GetComponent<MeshFilter>().sharedMesh=mesh;
        var shader=Resources.Load<Shader>("WorldGenVertex");globe.GetComponent<MeshRenderer>().sharedMaterial=new Material(shader);globe.transform.rotation=Quaternion.Euler(-12,150,0);
        var rivers=new GameObject("Persistent river reaches",typeof(MeshFilter),typeof(MeshRenderer));rivers.transform.SetParent(globe.transform,false);lineMesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};rivers.GetComponent<MeshFilter>().sharedMesh=lineMesh;rivers.GetComponent<MeshRenderer>().sharedMaterial=new Material(shader);
        Generate();
    }
    void Generate()
    {try{recipe.Validate();cancel?.Cancel();cancel=new CancellationTokenSource();var snapshot=recipe.Clone();var token=cancel.Token;error="";job=Task.Run(()=>new WorldPipeline().Generate(snapshot,p=>{progress=p.Stage;percent=p.Percent;},token),token);}catch(Exception e){error=e.Message;}}
    void Update()
    {
        if(cameraView==null)return;cameraView.rect=new Rect(Math.Min(.45f,350f/Screen.width),0,1-Math.Min(.45f,350f/Screen.width),1);
        if(job!=null&&job.IsCompleted)
        {if(job.IsCanceled){progress="Cancelled";}else if(job.IsFaulted){error=job.Exception.GetBaseException().Message;}else{world=job.Result;dirty=StateHash.Compute(recipe)!=StateHash.Compute(world.Recipe);Refresh();if(Environment.GetCommandLineArgs().Contains("--worldgen-screenshot")&&!screenshotPending){screenshotPending=true;StartCoroutine(Screenshot());}}job=null;}
        var mouse=Mouse.current;if(mouse==null)return;var pos=mouse.position.ReadValue();
        if(mouse.leftButton.wasPressedThisFrame && pos.x>350 && pos.y<Screen.height-100){dragging=true;dragDistance=0;previousMouse=pos;}
        if(dragging && mouse.leftButton.isPressed){var delta=pos-previousMouse;dragDistance+=delta.magnitude;globe.transform.Rotate(Vector3.up,-delta.x*.3f,Space.World);globe.transform.Rotate(Vector3.right,delta.y*.3f,Space.World);previousMouse=pos;}
        if(mouse.leftButton.wasReleasedThisFrame&&dragging){dragging=false;if(dragDistance<5&&world!=null&&Physics.Raycast(cameraView.ScreenPointToRay(pos),out var hit)){Vector3 p=globe.transform.InverseTransformPoint(hit.point).normalized;int best=0;float dot=-2;var positions=mesh.vertices;for(int i=0;i<positions.Length;i++){float d=Vector3.Dot(positions[i].normalized,p);if(d>dot){dot=d;best=i;}}inspection=GlobeView.Describe(world,best);}}
        if(pos.x>350){float wheel=mouse.scroll.ReadValue().y;if(Math.Abs(wheel)>0){var p=cameraView.transform.position;p.z=Mathf.Clamp(p.z+wheel*.0015f,-5,-1.7f);cameraView.transform.position=p;}}
    }
    void Refresh()
    {
        if(world==null)return;var view=GlobeView.Build(world,layer,seaLevel);var vertices=new Vector3[view.Positions.Length/3];var colors=new Color[vertices.Length];
        for(int i=0;i<vertices.Length;i++){vertices[i]=new Vector3(view.Positions[i*3],view.Positions[i*3+1],view.Positions[i*3+2]);colors[i]=new Color(view.Colors[i*3],view.Colors[i*3+1],view.Colors[i*3+2]);}
        mesh.Clear();mesh.vertices=vertices;mesh.triangles=view.Triangles;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();globe.GetComponent<MeshCollider>().sharedMesh=null;globe.GetComponent<MeshCollider>().sharedMesh=mesh;
        var lines=new Vector3[view.Lines.Length/3];for(int i=0;i<lines.Length;i++)lines[i]=new Vector3(view.Lines[i*3],view.Lines[i*3+1],view.Lines[i*3+2]);lineMesh.Clear();lineMesh.vertices=lines;lineMesh.colors=Enumerable.Repeat(new Color(.2f,.7f,.85f),lines.Length).ToArray();lineMesh.SetIndices(Enumerable.Range(0,lines.Length).ToArray(),MeshTopology.Lines,0);lineMesh.RecalculateBounds();
    }
    void OnGUI()
    {
        if(cameraView==null)return;GUI.skin.label.wordWrap=true;GUI.backgroundColor=new Color(.13f,.23f,.28f);float width=Math.Min(350,Screen.width*.45f);
        GUI.Box(new Rect(0,0,width,Screen.height),"");GUILayout.BeginArea(new Rect(16,15,width-32,Screen.height-30));
        GUILayout.Label("WORLDGEN  /  PLANET LABORATORY",new GUIStyle(GUI.skin.label){fontSize=16,fontStyle=FontStyle.Bold});GUILayout.Label("Shape a planet.",new GUIStyle(GUI.skin.label){fontSize=24});
        GUILayout.BeginHorizontal();GUILayout.Label("Preset: "+preset);if(GUILayout.Button("Next",GUILayout.Width(55))){presetIndex=(presetIndex+1)%presets.Length;preset=presets[presetIndex];int seed=recipe.Seed;recipe=Recipe.Preset(preset);recipe.Seed=seed;dirty=true;}GUILayout.EndHorizontal();
        scroll=GUILayout.BeginScrollView(scroll);
        for(int g=0;g<groups.Length;g++)
        {
            if(GUILayout.Button((panels[g]?"−  ":"+  ")+groups[g]))panels[g]=!panels[g];if(!panels[g])continue;
            foreach(var field in Recipe.Settings.Where(f=>f.GetCustomAttribute<SettingAttribute>().Group==groups[g]))
            {var meta=field.GetCustomAttribute<SettingAttribute>();GUILayout.BeginHorizontal();GUILayout.Label(meta.Label,GUILayout.Width(width-135));string old=Convert.ToString(field.GetValue(recipe),CultureInfo.InvariantCulture);string value=GUILayout.TextField(old,GUILayout.Width(82));if(value!=old&&double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double parsed)){field.SetValue(recipe,field.FieldType==typeof(int)?(object)(int)parsed:parsed);dirty=true;}GUILayout.EndHorizontal();}
        }
        GUILayout.Space(10);GUILayout.Label("Recipe v"+recipe.SchemaVersion+" · "+recipe.AlgorithmVersion);GUILayout.Label("File path for recipe / result exports:");filePath=GUILayout.TextField(filePath);
        GUILayout.BeginHorizontal();if(GUILayout.Button("Save recipe"))Try(()=>File.WriteAllText(filePath,WorldJson.Write(recipe)));if(GUILayout.Button("Load recipe"))Try(()=>{recipe=WorldJson.ReadRecipe(File.ReadAllText(filePath));dirty=true;});GUILayout.EndHorizontal();
        GUI.enabled=world!=null;if(GUILayout.Button("Export full result"))Try(()=>File.WriteAllText(Path.ChangeExtension(filePath,"result.json"),WorldJson.Write(world)));
        GUILayout.BeginHorizontal();if(GUILayout.Button("Heightmap"))Try(()=>File.WriteAllBytes(Path.ChangeExtension(filePath,"pgm"),WorldJson.Heightmap(world)));if(GUILayout.Button("Cell CSV"))Try(()=>File.WriteAllText(Path.ChangeExtension(filePath,"csv"),WorldJson.Csv(world)));GUILayout.EndHorizontal();GUI.enabled=true;
        if(world!=null){foreach(var warning in world.Warnings)GUILayout.Label(warning);GUILayout.Label("Final hash: "+world.Stages.Last().Hash);foreach(var metric in world.Metrics)GUILayout.Label(metric.Name+": "+metric.Value.ToString("0.###")+" "+metric.Unit);}
        GUILayout.EndScrollView();if(dirty&&world!=null)GUILayout.Label("Settings changed. Regenerate to apply.");if(!string.IsNullOrEmpty(error))GUILayout.Label(error);
        GUI.enabled=job==null;if(GUILayout.Button(world==null?"GENERATE WORLD":"REGENERATE / REAPPLY",GUILayout.Height(35)))Generate();GUI.enabled=true;
        if(job!=null){GUILayout.Label(progress+" · "+percent+"%");if(GUILayout.Button("Cancel"))cancel.Cancel();}GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(width+24,20,Math.Max(200,Screen.width-width-48),175));GUILayout.Label("SURFACE OBSERVATORY",new GUIStyle(GUI.skin.label){fontSize=12});
        if(GUILayout.Button(layer+"  ▾",GUILayout.Width(240)))layerMenu=!layerMenu;
        if(layerMenu){layerScroll=GUILayout.BeginScrollView(layerScroll,GUILayout.Height(115),GUILayout.Width(260));foreach(Layer candidate in Enum.GetValues(typeof(Layer)))if(GUILayout.Button(candidate.ToString())){layer=candidate;layerMenu=false;Refresh();}GUILayout.EndScrollView();}
        else {GUILayout.Label("Sea-level preview: "+seaLevel+" m");int preview=Mathf.RoundToInt(GUILayout.HorizontalSlider(seaLevel,-1000,1000,GUILayout.Width(240))/10)*10;if(preview!=seaLevel){seaLevel=preview;Refresh();}GUILayout.Label("Drag to orbit · scroll to zoom · click to inspect");}GUILayout.EndArea();
        GUI.Box(new Rect(width+20,Screen.height-172,Math.Max(200,Screen.width-width-40),152),"");GUI.Label(new Rect(width+34,Screen.height-160,Math.Max(190,Screen.width-width-65),130),inspection+"\nRiver potential: uniform 1 mm/day runoff. Seasonal discharge unavailable.");
    }
    void Try(Action action){try{action();error="";}catch(Exception e){error=e.Message;}}
    async void RunPlayerChecks()
    {
        try{string output=Argument("--worldgen-output",Path.Combine(Application.persistentDataPath,"cross-host"));Directory.CreateDirectory(output);foreach(var entry in GoldenCorpus.Recipes()){var w=await Task.Run(()=>new WorldPipeline().Generate(entry.Recipe));File.WriteAllText(Path.Combine(output,entry.Name+".hashes.json"),WorldJson.Write(w.Stages));Debug.Log("WORLDGEN PLAYER "+entry.Name+" "+w.Stages.Last().Hash);}Application.Quit(0);}catch(Exception e){Debug.LogException(e);Application.Quit(1);}
    }
    IEnumerator Screenshot()
    {
        yield return new WaitForEndOfFrame();
        // Render explicitly: hidden Windows players can have an unpresented black swap chain.
        var target=new RenderTexture(1200,1000,24);cameraView.targetTexture=target;cameraView.rect=new Rect(0,0,1,1);cameraView.Render();
        var prior=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1200,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1000),0,0);texture.Apply();
        File.WriteAllBytes(Argument("--worldgen-screenshot",Path.Combine(Application.persistentDataPath,"worldgen.png")),texture.EncodeToPNG());RenderTexture.active=prior;cameraView.targetTexture=null;Destroy(texture);target.Release();Destroy(target);yield return null;Application.Quit(0);
    }
    static string Argument(string flag,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    void OnDestroy(){cancel?.Cancel();cancel?.Dispose();if(mesh!=null)Destroy(mesh);if(lineMesh!=null)Destroy(lineMesh);}
}

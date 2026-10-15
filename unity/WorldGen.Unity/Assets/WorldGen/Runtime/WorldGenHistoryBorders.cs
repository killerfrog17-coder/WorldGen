using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    readonly HistoryBorders historyBorders=new HistoryBorders();
    World borderWorld;PrehistoryState borderState;GameObject borderRoot;Mesh borderMesh;Material borderMaterial;bool borderLoading;
    void RequestHistoryBorders()
    {
        if(borderLoading||world==null||globe==null||ReferenceEquals(borderWorld,world)&&(ReferenceEquals(borderState,world.Prehistory)||borderState?.Timeline!=null&&world.Prehistory?.Timeline!=null&&borderState.Timeline.ProcessedEvents==world.Prehistory.Timeline.ProcessedEvents)&&borderRoot!=null)return;
        StartCoroutine(PrepareHistoryBorders());
    }
    System.Collections.IEnumerator PrepareHistoryBorders()
    {
        borderLoading=true;var captured=world;var state=captured.Prehistory;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
        var task=Task.Run(()=>Prepare(historyBorders.Mesh(captured,state),linear,CancellationToken.None));
        while(!task.IsCompleted)yield return null;
        borderLoading=false;
        if(task.IsFaulted){Debug.LogException(task.Exception);yield break;}
        if(world!=captured||globe==null||displayedWorld!=captured)yield break;
        if(borderRoot!=null)Destroy(borderRoot);if(borderMesh!=null)Destroy(borderMesh);
        if(borderMaterial==null)borderMaterial=new Material(Shader.Find("WorldGen/HistoryBorder"));
        var piece=task.Result;borderMesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        borderMesh.vertices=piece.Lines;borderMesh.SetUVs(1,piece.MapLines);borderMesh.SetIndices(piece.LineIndices,MeshTopology.Lines,0);
        borderMesh.bounds=new Bounds(Vector3.zero,new Vector3(4.2f,3,3));borderMesh.UploadMeshData(true);
        borderRoot=new GameObject("history");borderRoot.transform.SetParent(globe.transform,false);
        for(int copy=-1;copy<=1;copy++)
        {
            var part=new GameObject("copy"+copy,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(borderRoot.transform,false);part.transform.localPosition=new Vector3(copy*4,0,0);
            part.GetComponent<MeshFilter>().sharedMesh=borderMesh;part.GetComponent<MeshRenderer>().sharedMaterial=borderMaterial;
        }
        borderWorld=captured;borderState=state;ApplyCachedDisplay();
    }
    void ApplyHistoryBorders()
    {
        if(borderRoot==null)return;
        bool political=historyOverlay==HistoryOverlay.States||historyOverlay==HistoryOverlay.Civilizations;
        borderRoot.SetActive(screen=="Simulation"&&humanOverlayOpacity>0&&political&&borderWorld==displayedWorld);
        borderMaterial.SetFloat("_Opacity",humanOverlayOpacity);borderMaterial.SetFloat("_MapMode",VisibleProjection==Projection.Map?1:0);
        foreach(Transform copy in borderRoot.transform)copy.gameObject.SetActive(copy.name=="copy0"||VisibleProjection==Projection.Map);
    }
}

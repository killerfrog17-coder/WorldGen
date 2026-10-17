using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    [Serializable] sealed class OvernightObservation
    {
        public int Year,Era,States,Towns,FirstMaximumTechnologyYear;
        public long Population;
        public double WallSeconds;
        public string Hash,Image;
    }
    // An opt-in native review of the ordinary Auto player, not a simulation
    // implementation. Pauses only for consistent presentation screenshots.
    IEnumerator CheckOvernightHistory()
    {
        string folder=Argument("--worldgen-output","");Directory.CreateDirectory(folder);AudioListener.volume=0;
        double deadline=Time.realtimeSinceStartupAsDouble+900;
        while((Busy||humans==null||!worldReady)&&error.Length==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null||!worldReady||error.Length>0){Debug.LogError("OVERNIGHT load failed: "+error);Application.Quit(1);yield break;}
        int origin=world.Prehistory.Origin;
        var prepare=Task.Run(()=>new SimulationSession(humans,humans.Start(humans.Prepare(origin))));
        while(!prepare.IsCompleted)yield return null;
        if(prepare.IsFaulted){Debug.LogException(prepare.Exception);Application.Quit(1);yield break;}
        humanSession=prepare.Result;world.Prehistory=humanSession.LastSnapshot;
        screen="Simulation";simulationPaused=false;atlasOpen=false;detailsOpen=false;peoplesOpen=false;chronicleOpen=true;
        layer=preferredLayer=Layer.Climate;satelliteOpacity=0;humanOverlayOpacity=1;historyOverlay=HistoryOverlay.States;
        SetProjection(Projection.Map);mapPan=Vector3.zero;centerLongitude=0;mapZoom=1;selectedCell=-1;gridSpacing=30;
        humanPlayback.AutoWorld(world.Prehistory,WorldEraView.Of(world.Prehistory));humanPlaying=true;Refresh();UpdateHumanPalette();
        double start=Time.realtimeSinceStartupAsDouble,nextCapture=start;
        double duration=double.Parse(Argument("--worldgen-duration","5400"),System.Globalization.CultureInfo.InvariantCulture);
        int index=0,lastEra=-1;string observations=Path.Combine(folder,"observations.jsonl");
        while(Time.realtimeSinceStartupAsDouble-start<duration&&error.Length==0)
        {
            yield return null;int era=WorldEraView.Of(world.Prehistory);
            int maximum=world.Prehistory.History.Aggregates.FirstMaximumTechnologyYear;
            bool done=maximum>=0&&world.Prehistory.Years>=maximum+1000;
            if(Time.realtimeSinceStartupAsDouble<nextCapture&&era==lastEra&&!done)continue;
            humanPlaying=false;while(humanStep!=null)yield return null;
            UpdateHumanPalette();double ready=Time.realtimeSinceStartupAsDouble+180;
            while((Busy||borderLoading||humanPaletteJob!=null||humanPaletteReady!=null||humanPalettePending)&&Time.realtimeSinceStartupAsDouble<ready)yield return null;
            if(Time.realtimeSinceStartupAsDouble>=ready){error="Overnight presentation timeout";break;}
            for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();
            var state=world.Prehistory;string image=$"{index++:D4}-era{WorldEraView.Of(state)}-year{state.Years}";
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,image+"-hud.png"));
            for(int frame=0;frame<3;frame++)yield return new WaitForEndOfFrame();
            CaptureHumanMap(Path.Combine(folder,image+"-map.png"));
            var row=new OvernightObservation{Year=state.Years,Era=WorldEraView.Of(state),Population=state.History.Aggregates.TotalPopulation,
                States=state.History.Polities.Count(p=>p.EndedYear<0),Towns=state.Society.Settlements.Count(t=>t.Population>0),FirstMaximumTechnologyYear=state.History.Aggregates.FirstMaximumTechnologyYear,
                WallSeconds=Time.realtimeSinceStartupAsDouble-start,Hash=state.Hash,Image=image};
            File.AppendAllText(observations,JsonUtility.ToJson(row)+Environment.NewLine);
            Debug.Log("OVERNIGHT "+JsonUtility.ToJson(row));lastEra=row.Era;nextCapture=Time.realtimeSinceStartupAsDouble+15;
            if(done)break;
            humanPlaying=true;
        }
        humanPlaying=false;while(humanStep!=null)yield return null;
        if(error.Length>0){Debug.LogError("OVERNIGHT failed: "+error);Application.Quit(1);yield break;}
        var snapshot=Task.Run(()=>{var state=humanSession.Snapshot();humans.Validate(state);world.Prehistory=state;return WorldFile.Write(world);});
        while(!snapshot.IsCompleted)yield return null;
        if(snapshot.IsFaulted){Debug.LogException(snapshot.Exception);Application.Quit(1);yield break;}
        File.WriteAllBytes(Path.Combine(folder,"final.world"),snapshot.Result);
        File.WriteAllText(Path.Combine(folder,"complete.txt"),world.Prehistory.Hash+Environment.NewLine);
        Debug.Log("OVERNIGHT completed "+world.Prehistory.Years+" "+world.Prehistory.Hash);Application.Quit(0);
    }
}

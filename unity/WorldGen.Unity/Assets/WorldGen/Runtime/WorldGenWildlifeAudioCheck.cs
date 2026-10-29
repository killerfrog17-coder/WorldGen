using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckWildlifeAudioFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("WILDLIFE AUDIO "+text);report.Add("PASS "+text);Debug.Log("WILDLIFE AUDIO PASS "+text);}
        paused=true;TestAnimateWeather=true;FaunaAudioOptions.Current.Enabled=true;FaunaAudioOptions.Current.AnimalsEnabled=true;
        double deadline=Time.unscaledTimeAsDouble+20;
        while(!wildlifeLibraryReady&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(wildlifeLibraryReady&&wildlifeSounds.Takes.Count>500,"Large screened external catalogue loads without the former 45-file ceiling");
        Check(wildlifeSoundCache.Count<48,"Starting a world does not decode the whole sound library");
        foreach(string bank in new[]{"cricket","cicada","katydid","frog","bird","woodpecker","raven","pigeon"})
            Check(wildlifeSounds.Count(bank)>=10,"Multiple distinct "+bank+" takes are available");
        foreach(string bank in new[]{"cricket","frog","bird","wolf","deer"})
        {
            var take=wildlifeSounds.Takes.FirstOrDefault(t=>t.Bank==bank);Check(take!=null,"External "+bank+" bank has an admitted recording");
            AudioClip clip=null;deadline=Time.unscaledTimeAsDouble+8;
            while((clip=WildlifeClip(take))==null&&Time.unscaledTimeAsDouble<deadline)yield return null;
            Check(clip!=null&&clip.channels==1&&clip.frequency==44100,"Actual mono44.1kHz "+bank+" file decodes through the native audio loader");
            var samples=new float[Math.Min(clip.samples,44100*4)];Check(clip.GetData(samples,0)&&samples.Any(v=>Math.Abs(v)>.0001f)&&samples.All(v=>float.IsFinite(v)),bank+" decoded recording contains finite audible samples");
        }
        faunaPreviewSequence=0;PreviewFauna(0);deadline=Time.unscaledTimeAsDouble+8;
        while((faunaPreview.clip==null||!faunaPreview.isPlaying)&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(faunaPreview.isPlaying,"Diagnostic sampler actually starts an external recording");string previous=faunaPreview.clip.name;
        PreviewFauna(0);deadline=Time.unscaledTimeAsDouble+8;
        while((faunaPreview.clip==null||faunaPreview.clip.name==previous)&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(faunaPreview.clip!=null&&faunaPreview.clip.name!=previous,"Next preview advances to a different take");StopFaunaPreview();
        var sites=BuildFaunaSites(Position,frame.RadiusM,HorizontalScale,new HashSet<ulong>(),faunaCancel.Token);
        Check(sites.Any(s=>s.Profile!=null),"Actual local ecology and ground produce sound opportunities");
        foreach(var site in sites.Where(s=>s.Profile!=null&&s.Profile.NativeTaxonId>=0))
            Check(Biogeography.Present(sourceWorld.Ecology,terrain.NearestCell(site.Position),site.Profile.NativeTaxonId),"Native caller belongs to its actual ecoregion");
        var insect=sites.FirstOrDefault(s=>s.Profile!=null&&(int)s.Anchor.Kind<3);
        Check(insect!=null,"Native calling-insect profile is available in the woodland fixture");
        var voice=faunaVoices[0];StopFaunaVoice(voice);voice.Site=insect;voice.Pitch=1;voice.Sequence=1;
        uint species=Rng.Mix((uint)insect.Profile.RegionId^(uint)insect.Profile.NativeTaxonId*7919u^(uint)sourceWorld.Recipe.Seed);
        var compatible=wildlifeSounds.Compatible(FaunaAmbience.Banks[(int)insect.Anchor.Kind],"ambient",species,(int)(insect.Anchor.Hash%3));
        int pick=(int)(Rng.Mix(insect.Anchor.Hash+7919u)%(uint)compatible.Count);var callerTake=compatible[pick];
        deadline=Time.unscaledTimeAsDouble+8;
        while(WildlifeClip(callerTake)==null&&Time.unscaledTimeAsDouble<deadline)yield return null;
        StopFaunaVoice(voice);voice.Site=insect;voice.Sequence=1;ScheduleFauna(voice,AudioSettings.dspTime);
        Check(voice.Sources.Any(s=>s.clip!=null)&&voice.End.Max()>AudioSettings.dspTime,"An actual habitat caller schedules its own positioned recording");
        Check(voice.Sources.All(s=>s.spatialBlend==1&&!s.loop&&s.dopplerLevel==0),"Ambient callers are spatial one-shots with overlap scheduling, not a looping stereo soundtrack");
        StopFaunaVoice(voice);voice.Site=null;
        paused=false;ExecuteAnimalSpawnCommand("/spawn deer");deadline=Time.unscaledTimeAsDouble+30;
        while(!animals.Values.Any(a=>a.Manual&&!a.Dead&&a.Model.Asset.Slug=="deer")&&Time.unscaledTimeAsDouble<deadline)yield return null;
        var deer=animals.Values.FirstOrDefault(a=>a.Manual&&!a.Dead&&a.Model.Asset.Slug=="deer");Check(deer!=null,"Native deer is admitted after unpausing command processing");paused=true;deer.X=motion.X+2;deer.Z=motion.Z;deer.Planet=frame.Position(deer.X,deer.Z);deer.GroundY=Ground(deer.X,deer.Z);deer.TargetSpeed=deer.Speed=0;UpdateAnimal(deer,0);
        DamageAnimal(deer,.1,deer.Renderer.bounds.center,Vector3.forward,0,true);deadline=Time.unscaledTimeAsDouble+8;
        while(!creatureVoices.Any(v=>v.Actor==deer&&v.Source.isPlaying)&&Time.unscaledTimeAsDouble<deadline)yield return null;
        var animalVoice=creatureVoices.FirstOrDefault(v=>v.Actor==deer&&v.Source.isPlaying);
        Check(animalVoice!=null,"Real animal damage triggers a native health-linked pain call");
        Check(animalVoice.Source.spatialBlend==1&&(animalVoice.Source.transform.position-deer.Renderer.bounds.center).sqrMagnitude<.01,"Animal voice follows the visible animal rather than the camera");
        int active=creatureVoices.Count(v=>v.Actor==deer);AnimalVoiceEvent(deer,"pain");Check(creatureVoices.Count(v=>v.Actor==deer)==active,"Repeated same-frame damage does not stack duplicate animal voices");
        // Only manipulate fixture COPIES. Owner/source recordings remain intact.
        while(wildlifeSoundScan!=null)yield return null;
        string originalRoot=wildlifeSoundRoot,fixture=Path.Combine(output,"editable-bank");Directory.CreateDirectory(fixture);
        var fixtureTakes=wildlifeSounds.Takes.Where(t=>t.Bank=="cricket").Take(3).ToArray();
        foreach(var take in fixtureTakes){string target=Path.Combine(fixture,take.File);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Path.Combine(originalRoot,take.File),target);}
        string header="bank\tform\ttake\tfile\tsha256\tseconds\tcue\n";
        File.WriteAllText(Path.Combine(fixture,"library.tsv"),header+string.Concat(fixtureTakes.Select(t=>$"{t.Bank}\t{t.Form}\t{t.Take}\t{t.File}\t{t.Sha256}\t{t.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}\t{t.Cue}\n")));
        wildlifeSoundRoot=fixture;int version=wildlifeCatalogueVersion;ReloadWildlifeAudio();deadline=Time.unscaledTimeAsDouble+8;
        while(wildlifeCatalogueVersion==version&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(wildlifeSounds.Takes.Count==3,"Live catalogue refresh discovers an edited external library");
        var removed=fixtureTakes[0];File.Move(Path.Combine(fixture,removed.File),Path.Combine(fixture,removed.File)+".removed");
        version=wildlifeCatalogueVersion;ReloadWildlifeAudio();deadline=Time.unscaledTimeAsDouble+8;
        while(wildlifeCatalogueVersion==version&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(wildlifeSounds.Takes.Count==2&&wildlifeMissing==1,"Deleting a take is respected without regenerating it or breaking the remaining bank");
        Check(fixtureTakes.All(t=>File.Exists(Path.Combine(originalRoot,t.File))),"Audition/delete checks leave all owner source recordings untouched");
        wildlifeSoundRoot=originalRoot;ReloadWildlifeAudio();TestAnimateWeather=false;paused=false;
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}

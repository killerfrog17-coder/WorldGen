using System;
using System.Linq;
using WorldGen.Core;

static class WildlifeSoundTests
{
    public static void Run()
    {
        int checks=0;void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
        string hash=new string('a',64),header="bank\tform\ttake\tfile\tsha256\tseconds\tcue\n";
        string Row(int form,int take,string bank="cricket",string cue="ambient")=>$"{bank}\t{form}\t{take}\tai/{bank}/f{form}-t{take}.wav\t{hash}\t20\t{cue}\n";
        string text=header+string.Concat(Enumerable.Range(1,6).SelectMany(f=>Enumerable.Range(1,16).Select(t=>Row(f,t))));
        var catalog=WildlifeSoundCatalogue.Parse(text);Check(catalog.Takes.Count==96,"Wildlife catalogues admit more than the former 45-take ceiling");
        for(uint voice=0;voice<30;voice++)
        {
            var a=catalog.Compatible("cricket","ambient",voice);var b=catalog.Compatible("cricket","ambient",voice);
            Check(a.Count==16&&a.Select(t=>t.Form).Distinct().Count()==1&&a[0].Form==b[0].Form,"Species selection stays in one compatible call form");
        }
        Check(catalog.Compatible("wolf","pain",1).Count==0,"Absent animal calls stay absent rather than borrowing another species");
        Check(Enumerable.Range(0,3).Select(i=>catalog.Compatible("cricket","ambient",77,i)[0].Form).Distinct().Count()==3,"Coexisting guild callers can retain three different stable acoustic forms");
        Check(WildlifeSoundCatalogue.Parse(header+Row(1,1)+Row(1,1)).Takes.Count==1,"Duplicate files are not admitted twice");
        foreach(string path in new[]{"../bad.wav","ai/../bad.wav","C:/bad.wav","/bad.wav","ai\\bad.wav","ai//bad.wav","ai/./bad.wav","ai/bad.mp3"})
            Check(!WildlifeSoundCatalogue.SafeFile(path),"External clip paths cannot escape their bank");
        Check(WildlifeSoundCatalogue.Parse(header+Row(1,1).Replace("20\t","NaN\t")).Takes.Count==0,"Invalid durations are rejected");
        Check(WildlifeSoundCatalogue.Parse(header+Row(1,1).Replace(hash,"bad")).Takes.Count==0,"Malformed hashes are rejected");
        Check(FaunaAmbience.KindCount==FaunaAmbience.Banks.Length&&FaunaAmbience.Names.Length==FaunaAmbience.KindCount,"Sound groups and settings indices agree");
        Check((int)FaunaSoundKind.Frog==3&&(int)FaunaSoundKind.UnderstoryBird==4,"Original sound-setting indices remain compatible");
        Check(WildlifeVocalisation.Bank("wolf")=="wolf"&&WildlifeVocalisation.Bank("moose")=="moose","Canonical model identities select the correct voice families");
        foreach(string model in new[]{"sea-lion","spider-monkey","human","fireflies-derived-lineage","small-native-freshwater-fish-guild"})
            Check(WildlifeVocalisation.Bank(model)==null,"Unrelated/silent models do not match animal-name substrings");
        Check(WildlifeVocalisation.FlightNoise("bee-wings")&&!WildlifeVocalisation.FlightNoise("fireflies-derived-lineage"),"Wing sounds apply only to supported audible insects");
        var p=new FaunaVoiceProfile{Kind=FaunaSoundKind.Cicada,Activity=new InsectActivityTraits{StartSolarHour=8,DurationHours=10,MinTemperatureC=10,PreferredTemperatureC=25,MaxTemperatureC=40,RainSuppressionMmPerHour=2,WindSuppressionMps=7,SeasonCue=InsectSeasonCue.WarmMoistOpportunities}};
        double Gain(double hour,double temp,double rain=0,double wind=0)=>FaunaAmbience.ActivityGain(p,hour,.5,40,temp,.7,rain,wind);
        Check(Gain(12,26)>.9&&Gain(0,26)==0,"Diurnal cicadas are active in warm daylight and quiet at midnight");
        Check(Gain(12,0)==0&&Gain(12,26,12)<Gain(12,26)*.1,"Cold and heavy rain suppress calling rather than playing a universal bed");
        Check(Gain(12,26,0,20)<Gain(12,26)*.2,"Wind suppresses exposed calling");
        p.Kind=FaunaSoundKind.Frog;p.Activity.StartSolarHour=17;p.Activity.DurationHours=12;
        Check(FaunaAmbience.ActivityGain(p,22,.5,40,25,.9,0,0)>FaunaAmbience.ActivityGain(p,22,.5,40,25,.2,0,0),"Frog chorus opportunity responds to humidity");
        Check(WildlifeAcoustics.Gain(6,1.5,90)==WildlifeAcoustics.Gain(3,1.5,90)*.5,"Free-field pressure halves when distance doubles outside the reference distance");
        Check(WildlifeAcoustics.Gain(60,1.5,65)<.015&&WildlifeAcoustics.Gain(65,1.5,65)==0,"Distant callers fade to zero rather than retaining near-source volume");
        Check(WildlifeAcoustics.Range("bee-wings","flight")<4&&WildlifeAcoustics.Range("fly-wings","flight")<2,"Wing buzzes have metre-scale audibility, not hundred-metre voice ranges");
        Check(WildlifeAcoustics.Gain(1,WildlifeAcoustics.Reference("bee-wings","flight"),3.5)<.15,"A bee a metre away is already much quieter than an adjacent bee");
        Check(WildlifeAcoustics.DayActivity(-.001,true)-WildlifeAcoustics.DayActivity(.001,true)<.001,"Day/night actor calls have no binary switch at sunrise");
        p.Kind=FaunaSoundKind.Cricket;p.Activity.StartSolarHour=18;p.Activity.DurationHours=12;
        double last=FaunaAmbience.ActivityGain(p,0,.5,40,25,.9,0,0),jump=0;
        for(int minute=1;minute<=1440;minute++){double next=FaunaAmbience.ActivityGain(p,minute/60d,.5,40,25,.9,0,0);jump=Math.Max(jump,Math.Abs(next-last));last=next;}
        Check(jump<.03,"Calling envelope is continuous across dawn, dusk and midnight");
        Check(WildlifeAcoustics.CallingHour(6,0)!=WildlifeAcoustics.CallingHour(6,1000),"Individual callers have staggered daily schedules");
        Check(WildlifeAcoustics.TreeIdentity(55,FaunaSoundKind.Cicada)==WildlifeAcoustics.TreeIdentity(55,FaunaSoundKind.Cicada)&&WildlifeAcoustics.TreeIdentity(55,FaunaSoundKind.Cicada)!=WildlifeAcoustics.TreeIdentity(55,FaunaSoundKind.Owl),"A tree's acoustic occupants have stable distinct identities");
        Console.WriteLine("PASS "+checks+" wildlife sound catalogue/activity checks");
    }
}

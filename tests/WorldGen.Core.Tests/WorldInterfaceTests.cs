using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class WorldInterfaceTests
{
    public static void Run(Action<bool,string> check)
    {
        check(WorldEraView.Of(null)==1,"No world is planet creation");
        var state=new PrehistoryState{History=new HistoryState{Cultures=new[]{new HistoricalCulture{Technology=new int[37]}}}};
        check(WorldEraView.Of(state)==2,"Humans without cultivation remain prehistoric");
        var milestones=new[]{new[]{HistoricalTechnology.Cultivation},new[]{HistoricalTechnology.Bronze},new[]{HistoricalTechnology.Iron},new[]{HistoricalTechnology.Scholasticism,HistoricalTechnology.Steel,HistoricalTechnology.Mills},
            new[]{HistoricalTechnology.Printing,HistoricalTechnology.ScientificMethod,HistoricalTechnology.OceanNavigation,HistoricalTechnology.CropRotation},new[]{HistoricalTechnology.SteamPower,HistoricalTechnology.IndustrialMetallurgy},new[]{HistoricalTechnology.Electricity,HistoricalTechnology.CombustionEngines,HistoricalTechnology.GermTheory,HistoricalTechnology.Sanitation},new[]{HistoricalTechnology.Computing,HistoricalTechnology.Telecommunications,HistoricalTechnology.Electronics}};
        for(int i=0;i<milestones.Length;i++)
        {
            if(i==1)state.History.Polities=new[]{new HistoricalPolity{FoundedYear=100}};
            foreach(var technology in milestones[i])state.History.Cultures[0].Technology[(int)technology]=100+i;
            string before=WorldJson.Write(state);
            check(WorldEraView.Of(state)==i+3&&WorldEraView.Name(i+3).Length>0,"Era follows a coherent adopted package: "+WorldEraView.Name(i+3));
            check(WorldJson.Write(state)==before,"Era presentation never changes authoritative state");
        }
        var other=new HistoricalCulture{Technology=new int[37]};other.Technology[(int)HistoricalTechnology.Iron]=1;
        state.History.Cultures=new[]{other,state.History.Cultures[0]};
        check(WorldEraView.Of(state)==10,"Global milestone does not depend on culture ordering");
        var isolated=new HistoricalCulture{Technology=new int[37]};isolated.Technology[(int)HistoricalTechnology.Printing]=1;isolated.Technology[(int)HistoricalTechnology.Gunpowder]=1;
        check(WorldEraView.CultureEra(isolated)<7,"Printing and early gunpowder alone do not announce a fully early-modern society");
        isolated.Technology[(int)HistoricalTechnology.Radio]=1;isolated.Technology[(int)HistoricalTechnology.Railways]=1;
        check(WorldEraView.CultureEra(isolated)<8,"An isolated borrowed radio or railway is not an industrial or modern production system");
        var dates=WorldEraView.FirstYears(state);
        check(Enumerable.Range(3,8).All(era=>dates[era]==(era==5?100:99+era-3)),"Historical era dates use first states for Bronze and coherent later packages");
        var firstCourt=new PrehistoryState{History=new HistoryState{Polities=new[]{new HistoricalPolity{FoundedYear=123456,EndedYear=123500}}}};
        check(WorldEraView.Of(firstCourt)==4&&WorldEraView.FirstYears(firstCourt)[4]==123456,"The first recorded state begins Bronze even without bronze technology, and collapse does not reset it");
        var oralSailors=new PrehistoryState{History=new HistoryState{Cultures=new[]{new HistoricalCulture{Technology=new int[39]}}}};oralSailors.History.Cultures[0].Technology[(int)HistoricalTechnology.OceanNavigation]=1;
        check(WorldEraView.Of(oralSailors)==2&&WorldEraView.FirstYears(oralSailors)[4]==-1,"Oral ocean wayfinding alone is not the first state or a world Bronze milestone");
        foreach(int technology in Enumerable.Range(0,37))foreach(int level in Enumerable.Range(1,7))
            check(HistoricalTechnicalGraph.Cost(technology,level+1,Prehistory.ResearchPacingHistoryVersion)>HistoricalTechnicalGraph.Cost(technology,level,Prehistory.ResearchPacingHistoryVersion),"Research cost still increases with level: "+technology+" / "+level);
        check(HistoricalTechnicalGraph.Cost(9,1,Prehistory.ResearchPacingHistoryVersion)>HistoricalTechnicalGraph.Cost(9,1,Prehistory.TerritorialHistoryVersion)&&HistoricalTechnicalGraph.Cost(7,1,Prehistory.ResearchPacingHistoryVersion)<HistoricalTechnicalGraph.Cost(7,1,Prehistory.TerritorialHistoryVersion),"Versioned pacing distinguishes bronze adoption from the initial iron-production breakthrough");
        check(EraMusicTrack.TryParse("A song with-hyphens 4-10.MP3",out var parsed)&&parsed!.FirstEra==4&&parsed.LastEra==10&&parsed.Title=="A song with-hyphens","Parse inclusive era suffix and case-insensitive extension");
        foreach(string invalid in new[]{"song 0-5.ogg","song 1-11.wav","song 7-3.mp3","song.mp3","song 1-3.txt","song 01-3.wav"," 1-3.ogg","   1-3.wav"})
            check(!EraMusicTrack.TryParse(invalid,out _),"Ignore malformed soundtrack: "+invalid);
        EraMusicTrack Track(string name){EraMusicTrack.TryParse(name,out var track);return track!;}
        var tracks=new[]{Track("A 1-10.ogg"),Track("B 5-5.mp3"),Track("C 5-7.wav"),Track("D 5-10.ogg")};
        var recent=new List<string>{tracks[0].Path,tracks[1].Path,tracks[2].Path};
        var available=EraMusic.Candidates(tracks,5,recent);
        check(available.Length==1&&available[0].Path==tracks[3].Path,"Four eligible songs avoid all three recent tracks");
        check(tracks[2].Supports(5)&&tracks[2].Supports(7)&&!tracks[2].Supports(8)&&!tracks[2].Supports(0),"Tracks survive compatible age changes and reject silence or incompatible ages");
        var pace=new SimulationPlayback();pace.AutoEra(2);check(pace.Rate==2000d*SimulationTime.Year&&!pace.Manual,"Prehistory defaults to2000 years/s");
        foreach(int era in Enumerable.Range(3,8)){pace.Era(era);check(pace.Rate==SimulationClockView.EraRate(era),"Automatic playback follows "+WorldEraView.Name(era));}
        pace.Change(1);double manual=pace.Rate;pace.Era(2);check(pace.Manual&&pace.Rate==manual,"A speed change disables automatic pacing until re-enabled");
        pace.AutoEra(4);pace.HoldRate();pace.Era(10);check(pace.Manual&&pace.Rate==5d*SimulationTime.Year,"Unchecking Auto holds the existing rate");
        var autoState=new PrehistoryState{History=new HistoryState{RegionalTraditions=new HistoricalRegionalTraditions()},Society=new SocietyState()};
        void AutoSpeed(double years,string label){string before=WorldJson.Write(autoState);pace.AutoWorld(autoState,WorldEraView.Of(autoState));check(pace.Rate==years*SimulationTime.Year&&!pace.Manual,label);check(WorldJson.Write(autoState)==before,"Auto does not mutate history: "+label);}
        AutoSpeed(2000,"No cultures starts at2000 years/s");
        autoState.Society.Cultures=new[]{new CultureIdentity()};
        AutoSpeed(500,"Advanced cultures slow Auto to500 independently of the era name");
        check(WorldEraView.Of(autoState)==2&&WorldEraView.Name(2)=="Prehistory","Advanced cultures do not change the observer/music era");
        autoState.History.RegionalTraditions.FirstProtoYear=120000;
        AutoSpeed(100,"First embryonic civilization slows Auto to100");
        autoState.History.Polities=new[]{new HistoricalPolity{EndedYear=130000}};
        AutoSpeed(5,"First recorded state enters the Bronze observer era and its5 years/s Auto preset even after collapse");
        double[] eraSpeeds={5,2,2,2,1,1,1};
        for(int era=4;era<=10;era++)check(SimulationClockView.AutoRate(autoState,era)==eraSpeeds[era-4]*SimulationTime.Year,"Exact Auto rate for "+WorldEraView.Name(era));
        pace.SetRate(50d*SimulationTime.Year);pace.World(autoState,10);
        check(pace.Manual&&pace.Rate==50d*SimulationTime.Year,"World milestone updates preserve manual speed");
        pace.AutoWorld(autoState,10);check(!pace.Manual&&pace.Rate==SimulationTime.Year,"Rechecking Auto restores the current milestone speed");
        var legacyAuto=new PrehistoryState{Society=new SocietyState{Cultures=new[]{new CultureIdentity{Stage=SocietyStage.EmbryonicCivilization}}}};
        check(SimulationClockView.AutoRate(legacyAuto,2)==100d*SimulationTime.Year,"Legacy embryonic cultures retain their milestone");
        double[] annual={1,2,5,10,20,50,100,200,500,1000,2000,5000,10000};
        check(SimulationClockView.Rates.Where(rate=>rate>=SimulationTime.Year).SequenceEqual(annual.Select(rate=>rate*SimulationTime.Year)),"Annual speed ladder uses1,2,5 decades up to10000");
        pace.SetRate(10000d*SimulationTime.Year);pace.Change(1);check(pace.Rate==10000d*SimulationTime.Year,"Speed controls clamp at the final supported preset");
        available=EraMusic.Candidates(tracks.Take(3),5,recent);
        check(available.Length==1&&available[0].Path==tracks[0].Path,"Three songs relax only the oldest exclusion");
        available=EraMusic.Candidates(tracks.Take(2),5,recent);
        check(available.Length==1&&available[0].Path==tracks[0].Path,"Two songs still prefer the least recently played eligible track");
        check(EraMusic.Candidates(tracks.Take(1),5,recent).Length==1,"One eligible song can repeat");
        check(EraMusic.Candidates(tracks.Skip(1),2,recent).Length==0,"Never fill silence with an ineligible track");
        check(EraMusic.Candidates(Array.Empty<EraMusicTrack>(),5,recent).Length==0,"Empty library is silent");
        check(EraMusic.Candidates(tracks,4,Array.Empty<string>()).Length==1&&EraMusic.Candidates(tracks,10,Array.Empty<string>()).Length==2,"Range endpoints are inclusive");
        EraMusic.Remember(recent,tracks[3].Path);
        check(recent.Count==3&&recent[0]==tracks[1].Path&&recent[2]==tracks[3].Path,"Playback retains only three recent tracks");
    }
}

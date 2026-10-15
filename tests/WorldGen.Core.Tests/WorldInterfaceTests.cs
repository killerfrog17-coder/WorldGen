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
        var milestones=new[]{HistoricalTechnology.Cultivation,HistoricalTechnology.Bronze,HistoricalTechnology.Iron,HistoricalTechnology.Scholasticism,
            HistoricalTechnology.Printing,HistoricalTechnology.SteamPower,HistoricalTechnology.Electricity,HistoricalTechnology.Computing};
        for(int i=0;i<milestones.Length;i++)
        {
            state.History.Cultures[0].Technology[(int)milestones[i]]=100+i;
            string before=WorldJson.Write(state);
            check(WorldEraView.Of(state)==i+3&&WorldEraView.Name(i+3).Length>0,"Era follows adopted "+milestones[i]);
            check(WorldJson.Write(state)==before,"Era presentation never changes authoritative state");
        }
        var other=new HistoricalCulture{Technology=new int[37]};other.Technology[(int)HistoricalTechnology.Iron]=1;
        state.History.Cultures=new[]{other,state.History.Cultures[0]};
        check(WorldEraView.Of(state)==10,"Global milestone does not depend on culture ordering");
        check(EraMusicTrack.TryParse("A song with-hyphens 4-10.MP3",out var parsed)&&parsed!.FirstEra==4&&parsed.LastEra==10&&parsed.Title=="A song with-hyphens","Parse inclusive era suffix and case-insensitive extension");
        foreach(string invalid in new[]{"song 0-5.ogg","song 1-11.wav","song 7-3.mp3","song.mp3","song 1-3.txt","song 01-3.wav"," 1-3.ogg","   1-3.wav"})
            check(!EraMusicTrack.TryParse(invalid,out _),"Ignore malformed soundtrack: "+invalid);
        EraMusicTrack Track(string name){EraMusicTrack.TryParse(name,out var track);return track!;}
        var tracks=new[]{Track("A 1-10.ogg"),Track("B 5-5.mp3"),Track("C 5-7.wav"),Track("D 5-10.ogg")};
        var recent=new List<string>{tracks[0].Path,tracks[1].Path,tracks[2].Path};
        var available=EraMusic.Candidates(tracks,5,recent);
        check(available.Length==1&&available[0].Path==tracks[3].Path,"Four eligible songs avoid all three recent tracks");
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

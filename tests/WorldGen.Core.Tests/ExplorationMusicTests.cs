using WorldGen.Core;
using WorldGen.Presentation;

static class ExplorationMusicTests
{
    public static void Run(Action<bool,string> check)
    {
        ExplorationMusicTrack Track(string name){check(ExplorationMusicTrack.TryParse(name,out var t),"Valid music filename: "+name);return t!;}
        var forest=Track("Green Shade [biome=TemperateForest+BorealForest;time=day].OGG");
        check(forest.Title=="Green Shade"&&forest.Biomes.Count==2,"Filename exposes title and alternative biomes");
        check(forest.Supports(new ExplorationMusicContext(Biome.BorealForest,MusicTime.Day|MusicTime.Morning)),"Day tracks cover daylight mornings");
        check(!forest.Supports(new ExplorationMusicContext(Biome.Desert,MusicTime.Day))&&!forest.Supports(new ExplorationMusicContext(Biome.TemperateForest,MusicTime.Night)),"Both filename requirements must match");
        var dusk=Track("Cool Sand [time=sunset+night;biome=Desert].wav");
        check(dusk.Supports(new ExplorationMusicContext(Biome.Desert,MusicTime.Dusk)),"Sunset alias and reversed field order work");
        foreach(var bad in new[]{"Nope [biome=Jungle].ogg","Nope [time=midday].ogg","Nope [biome=999].ogg","Nope [time=day;time=night].ogg","Nope [biome=Desert;weather=rain].ogg","Nope [time=].ogg","Nope [time=day.ogg","Nope ].ogg"," [time=night].ogg","Nope [time=any+night].ogg","Nope [biome=Desert+].ogg","Nope [time=night]].ogg","Nope [time=night].txt"})
            check(!ExplorationMusicTrack.TryParse(bad,out _),"Malformed/unknown requirements fail closed: "+bad);
        var any=Track("A quiet journey.ogg");var wildcard=Track("Another [biome=*;time=any].mp3");
        check(any.Supports(new ExplorationMusicContext(Biome.Ice,MusicTime.Night))&&wildcard.Supports(new ExplorationMusicContext(Biome.Ocean,MusicTime.Day)),"Universal tracks are an explicit fallback");
        check(ExplorationMusicContext.TimeAt(1,-30)==MusicTime.Night&&ExplorationMusicContext.TimeAt(9,30)==(MusicTime.Day|MusicTime.Morning)&&ExplorationMusicContext.TimeAt(17,20)==(MusicTime.Day|MusicTime.Evening),"Real solar conditions distinguish night/morning/evening");
        check(ExplorationMusicContext.TimeAt(6,-2)==(MusicTime.Dawn|MusicTime.Night)&&ExplorationMusicContext.TimeAt(18,2)==(MusicTime.Dusk|MusicTime.Day),"Twilight uses horizon windows and overlaps daylight/darkness eligibility");
        check(ExplorationMusicContext.TimeAt(12,-10)==MusicTime.Night,"Polar darkness remains night even at local noon");
        check(Math.Abs(ExplorationMusicContext.SolarAltitude(0,12,.218)-90)<1e-5&&ExplorationMusicContext.SolarAltitude(0,0,.218)<-89,"Solar altitude shares the visual seasonal clock");
        var context=new ExplorationMusicContext(Biome.TemperateForest,MusicTime.Day);var tracks=new[]{any,wildcard,forest};
        check(ExplorationMusic.Candidates(tracks,context,new[]{forest.Path}).All(t=>t!=forest),"Recent tracks are excluded while alternatives exist");
        check(ExplorationMusic.Candidates(new[]{forest},context,new[]{forest.Path}).Length==1,"Small eligible pools relax repetition exclusions");
        check(ExplorationMusic.Candidates(new[]{forest},new ExplorationMusicContext(Biome.Desert,MusicTime.Night),Array.Empty<string>()).Length==0,"No inappropriate track fills an empty pool");
        check(ExplorationMusic.Choose(Array.Empty<ExplorationMusicTrack>(),.5)==null,"Empty pools remain silent");
        int specific=0;for(int i=0;i<1000;i++)if(ExplorationMusic.Choose(tracks,(i+.5)/1000)==forest)specific++;
        check(specific==800,"Biome/time-specific tracks receive higher weight without eliminating general travel music");
        var catalog=Path.Combine(AppContext.BaseDirectory,"../../../../../scripts/exploration-music.csv");
        if(File.Exists(catalog))
        {
            var lines=File.ReadAllLines(catalog).Skip(1).Where(s=>s.Length>0).ToArray();check(lines.Length==30,"Production catalog contains thirty tracks");
            var parsed=new List<ExplorationMusicTrack>();foreach(var line in lines){var cells=line.Split(',');parsed.Add(Track(cells[0]+" [biome="+cells[1]+";time="+cells[2]+"].ogg"));}
            foreach(Biome biome in Enum.GetValues<Biome>())foreach(var time in new[]{MusicTime.Night,MusicTime.Dawn|MusicTime.Night,MusicTime.Day,MusicTime.Day|MusicTime.Morning,MusicTime.Day|MusicTime.Evening,MusicTime.Dusk|MusicTime.Night})
                check(ExplorationMusic.Candidates(parsed,new ExplorationMusicContext(biome,time),Array.Empty<string>()).Length>=2,"Every biome/time has at least two valid choices: "+biome+" / "+time);
        }
    }
}

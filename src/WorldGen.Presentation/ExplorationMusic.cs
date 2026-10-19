using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Flags] public enum MusicTime { None=0,Day=1,Night=2,Dawn=4,Morning=8,Evening=16,Dusk=32,Any=63 }
    public readonly struct ExplorationMusicContext
    {
        public readonly Biome Biome;public readonly MusicTime Time;
        public ExplorationMusicContext(Biome biome,MusicTime time){Biome=biome;Time=time;}
        public static double SolarAltitude(double latitude,double solarHour,double seasonalYear)
        {
            double lat=latitude*Math.PI/180,declination=23.44*Math.PI/180*Math.Sin(2*Math.PI*(seasonalYear-.218)),hour=(solarHour-12)*Math.PI/12;
            return Math.Asin(Math.Max(-1,Math.Min(1,Math.Sin(lat)*Math.Sin(declination)+Math.Cos(lat)*Math.Cos(declination)*Math.Cos(hour))))*180/Math.PI;
        }
        public static MusicTime TimeAt(double solarHour,double altitude)
        {
            if(altitude< -6)return MusicTime.Night;
            if(altitude<=6)return (solarHour<12?MusicTime.Dawn:MusicTime.Dusk)|(altitude>0?MusicTime.Day:MusicTime.Night);
            return MusicTime.Day|(solarHour<10?MusicTime.Morning:solarHour>=16?MusicTime.Evening:MusicTime.None);
        }
    }
    public sealed class ExplorationMusicTrack
    {
        public string Path {get;} public string Title {get;}
        public IReadOnlyList<Biome> Biomes {get;}
        public MusicTime Times {get;}
        public int Weight=>(Biomes.Count==0?1:4)*(Times==MusicTime.Any?1:2);
        ExplorationMusicTrack(string path,string title,Biome[] biomes,MusicTime times){Path=path;Title=title;Biomes=Array.AsReadOnly(biomes);Times=times;}
        public bool Supports(ExplorationMusicContext context)=>(Biomes.Count==0||Biomes.Contains(context.Biome))&&(Times&context.Time)!=0;
        // Fields are ANDed; values joined by + are ORed. Unlabelled files are universal.
        // Unknown/malformed conditions fail closed, never silently becoming universal music.
        public static bool TryParse(string path,out ExplorationMusicTrack? track)
        {
            track=null;if(string.IsNullOrWhiteSpace(path))return false;
            string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();if(ext!=".ogg"&&ext!=".wav"&&ext!=".mp3")return false;
            string name=System.IO.Path.GetFileNameWithoutExtension(path),title=name.Trim();Biome[] biomes=Array.Empty<Biome>();MusicTime times=MusicTime.Any;
            int begin=name.IndexOf('[');
            if(begin>=0)
            {
                if(!name.EndsWith("]",StringComparison.Ordinal)||name.IndexOf('[',begin+1)>=0||name.IndexOf(']')!=name.Length-1)return false;
                title=name.Substring(0,begin).Trim();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(string field in name.Substring(begin+1,name.Length-begin-2).Split(';'))
                {
                    var pair=field.Split('=');if(pair.Length!=2)return false;string key=pair[0].Trim(),value=pair[1].Trim();
                    if(!seen.Add(key)||value.Length==0)return false;bool any=value.Equals("any",StringComparison.OrdinalIgnoreCase)||value=="*";
                    if(key.Equals("biome",StringComparison.OrdinalIgnoreCase))
                    {
                        if(any)continue;var list=new List<Biome>();
                        foreach(string item in value.Split('+')){if(!Enum.TryParse(item.Trim(),true,out Biome b)||!Enum.IsDefined(typeof(Biome),b)||int.TryParse(item.Trim(),out _))return false;list.Add(b);}biomes=list.Distinct().ToArray();
                    }
                    else if(key.Equals("time",StringComparison.OrdinalIgnoreCase))
                    {
                        if(any)continue;times=MusicTime.None;
                        foreach(string item in value.Split('+'))
                        {
                            string s=item.Trim().ToLowerInvariant();MusicTime t=s=="day"?MusicTime.Day:s=="night"?MusicTime.Night:s=="dawn"||s=="sunrise"?MusicTime.Dawn:s=="dusk"||s=="sunset"?MusicTime.Dusk:s=="morning"?MusicTime.Morning:s=="evening"?MusicTime.Evening:MusicTime.None;
                            if(t==MusicTime.None)return false;times|=t;
                        }
                    }
                    else return false;
                }
            }
            else if(name.Contains("]"))return false;
            if(title.Length==0)return false;track=new ExplorationMusicTrack(path,title,biomes,times);return true;
        }
    }
    public static class ExplorationMusic
    {
        public static ExplorationMusicTrack[] Candidates(IEnumerable<ExplorationMusicTrack> tracks,ExplorationMusicContext context,IReadOnlyList<string> recent)
        {
            var eligible=tracks.Where(t=>t.Supports(context)).ToArray();
            for(int keep=Math.Min(3,recent.Count);keep>=0;keep--)
            {var result=eligible.Where(t=>!Enumerable.Range(recent.Count-keep,keep).Any(i=>string.Equals(t.Path,recent[i],StringComparison.OrdinalIgnoreCase))).ToArray();if(result.Length>0)return result;}
            return Array.Empty<ExplorationMusicTrack>();
        }
        public static ExplorationMusicTrack? Choose(IReadOnlyList<ExplorationMusicTrack> tracks,double unit)
        {
            if(tracks.Count==0)return null;double cursor=Math.Max(0,Math.Min(.999999999,unit))*tracks.Sum(t=>t.Weight);
            foreach(var track in tracks){cursor-=track.Weight;if(cursor<0)return track;}return tracks[tracks.Count-1];
        }
    }
}

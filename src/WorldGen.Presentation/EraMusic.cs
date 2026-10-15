using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorldGen.Presentation
{
    public sealed class EraMusicTrack
    {
        public string Path {get;}
        public string Title {get;}
        public int FirstEra {get;}
        public int LastEra {get;}
        EraMusicTrack(string path,string title,int first,int last){Path=path;Title=title;FirstEra=first;LastEra=last;}
        public static bool TryParse(string path,out EraMusicTrack? track)
        {
            track=null;if(string.IsNullOrWhiteSpace(path))return false;
            string extension=System.IO.Path.GetExtension(path).ToLowerInvariant();
            if(extension!=".ogg"&&extension!=".mp3"&&extension!=".wav")return false;
            var match=Regex.Match(System.IO.Path.GetFileNameWithoutExtension(path),@"^(.+)\s+(10|[1-9])-(10|[1-9])$");
            if(!match.Success)return false;
            int first=int.Parse(match.Groups[2].Value),last=int.Parse(match.Groups[3].Value);
            if(first>last||match.Groups[1].Value.Trim().Length==0)return false;
            track=new EraMusicTrack(path,match.Groups[1].Value.Trim(),first,last);return true;
        }
    }
    public static class EraMusic
    {
        // Recent is chronological, oldest first. Only relax the oldest exclusions
        // when every eligible track is excluded; non-eligible songs never fill a gap.
        public static EraMusicTrack[] Candidates(IEnumerable<EraMusicTrack> tracks,int era,IReadOnlyList<string> recent)
        {
            var eligible=tracks.Where(t=>t.FirstEra<=era&&era<=t.LastEra).ToArray();
            if(eligible.Length==0)return eligible;
            for(int keep=Math.Min(3,recent.Count);keep>=0;keep--)
            {
                var result=eligible.Where(t=>!Enumerable.Range(recent.Count-keep,keep).Any(i=>string.Equals(t.Path,recent[i],StringComparison.OrdinalIgnoreCase))).ToArray();
                if(result.Length>0)return result;
            }
            return Array.Empty<EraMusicTrack>();
        }
        public static void Remember(List<string> recent,string path)
        {recent.Add(path);while(recent.Count>3)recent.RemoveAt(0);}
    }
}

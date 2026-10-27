using System;
using System.Globalization;
using System.Linq;

namespace WorldGen.Presentation
{
    public sealed class FaunaSpawnRequest
    {
        public string Action="help",Error="",Filter="";
        public int Count=1,Page=1;
        public FaunaPrototypeProfile? Profile;
    }
    public static class FaunaSpawnCommands
    {
        public const int PageSize=16;
        public const string Help="/spawn <animal> [positive count] | /spawn list [filter] [page] | /spawn clear";
        static string Key(string value)=>new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        static bool Match(FaunaPrototypeProfile p,string key)=>Key(p.slug).Contains(key)||Key(p.name).Contains(key)||p.aliases.Any(a=>Key(a).Contains(key));
        public static FaunaSpawnRequest Parse(string text,FaunaPrototypeProfile[] profiles)
        {
            var r=new FaunaSpawnRequest();var words=text.Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(words.Length<=1)return r;
            string verb=words[1].ToLowerInvariant();
            if(verb=="help"){if(words.Length>2)r.Error=Help;return r;}
            if(verb=="clear"){r.Action="clear";if(words.Length!=2)r.Error="Usage: /spawn clear";return r;}
            r.Action=verb=="list"?"list":"spawn";int end=words.Length;
            string last=words[end-1];bool number=end>(r.Action=="list"?2:2)&&(char.IsDigit(last[0])||last[0]=='-'||last[0]=='+');
            if(number)
            {
                if(!int.TryParse(last,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)||n<1)
                {r.Error=r.Action=="spawn"?"Count must be a positive whole number.":"Page must be a positive whole number.";return r;}
                if(r.Action=="list")r.Page=n;else r.Count=n;end--;
            }
            if(r.Action=="list"){r.Filter=string.Join(" ",words.Skip(2).Take(end-2));return r;}
            string name=string.Join(" ",words.Skip(1).Take(end-1)),key=Key(name);
            if(key.Length==0){r.Error=Help;return r;}
            var exact=profiles.Where(p=>Key(p.slug)==key||Key(p.name)==key||p.aliases.Any(a=>Key(a)==key)).ToArray();
            var matches=exact.Length>0?exact:profiles.Where(p=>Match(p,key)).ToArray();
            if(matches.Length==1)r.Profile=matches[0];
            else if(matches.Length==0)r.Error="Unknown animal '"+name+"'. Try /spawn list or /spawn list <part of name>.";
            else r.Error="More than one animal matches: "+string.Join(", ",matches.Take(8).Select(p=>p.slug))+". Use a full name.";
            return r;
        }
        public static PlayCommandResult List(FaunaPrototypeProfile[] profiles,FaunaSpawnRequest request)
        {
            var found=profiles.Where(p=>Match(p,Key(request.Filter))).OrderBy(p=>p.slug,StringComparer.Ordinal).ToArray();
            if(found.Length==0)return new PlayCommandResult(false,"No animals match '"+request.Filter+"'. Try /spawn list.");
            int pages=(found.Length+PageSize-1)/PageSize;
            if(request.Page>pages)return new PlayCommandResult(false,"Choose a page from 1 to "+pages+".");
            return new PlayCommandResult(true,new[]{"Animals - page "+request.Page+" / "+pages+" ("+found.Length+" matches)"}
                .Concat(found.Skip((request.Page-1)*PageSize).Take(PageSize).Select(p=>p.slug+" - "+p.name))
                .Concat(new[]{"/spawn <animal> [count]; /spawn list [filter] [page]. Scroll chat to see earlier lines."}).ToArray());
        }
        public static string[] Complete(string input,FaunaPrototypeProfile[] profiles)
        {
            return new[]{"/spawn list","/spawn clear"}.Concat(profiles.Select(p=>"/spawn "+p.slug))
                .Where(s=>s.StartsWith(input,StringComparison.OrdinalIgnoreCase)).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        }
    }
}

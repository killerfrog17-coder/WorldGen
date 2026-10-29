using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldGen.Core
{
    public sealed class WildlifeSoundTake
    {
        public string Bank="",Cue="ambient",File="",Sha256="";
        public int Form,Take;
        public double Seconds;
        public string Key=>Bank+"/"+Cue;
    }

    /// <summary>External, editable sample catalogue. No simulation state or media decoding.</summary>
    public sealed class WildlifeSoundCatalogue
    {
        public readonly List<WildlifeSoundTake> Takes=new List<WildlifeSoundTake>();
        readonly Dictionary<string,SortedDictionary<int,List<WildlifeSoundTake>>> forms=new Dictionary<string,SortedDictionary<int,List<WildlifeSoundTake>>>(StringComparer.Ordinal);
        public static WildlifeSoundCatalogue Parse(string text)
        {
            var result=new WildlifeSoundCatalogue();var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in (text??"").Split('\n'))
            {
                var f=line.TrimEnd('\r').Split('\t');
                if(f.Length!=7||f[0]=="bank"||!Token(f[0])||!Token(f[6])||!SafeFile(f[3])||!Hash(f[4]))continue;
                if(!int.TryParse(f[1],NumberStyles.None,CultureInfo.InvariantCulture,out int form)||form<1||
                   !int.TryParse(f[2],NumberStyles.None,CultureInfo.InvariantCulture,out int take)||take<1||
                   !double.TryParse(f[5],NumberStyles.Float,CultureInfo.InvariantCulture,out double seconds)||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<.05||seconds>120||!files.Add(f[3]))continue;
                result.Add(new WildlifeSoundTake{Bank=f[0],Form=form,Take=take,File=f[3],Sha256=f[4].ToLowerInvariant(),Seconds=seconds,Cue=f[6]});
            }
            return result;
        }
        static bool Token(string value)=>value.Length>0&&value.Length<80&&value.All(c=>c>='a'&&c<='z'||c>='0'&&c<='9'||c=='-');
        static bool Hash(string value)=>value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F');
        public static bool SafeFile(string value)
        {
            if(string.IsNullOrEmpty(value)||value[0]=='/'||value.Contains('\\')||value.Contains(':')||!value.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))return false;
            return value.Split('/').All(p=>p.Length>0&&p!="."&&p!=".."&&p.All(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'||c=='.'));
        }
        public void Add(WildlifeSoundTake take)
        {
            Takes.Add(take);
            if(!forms.TryGetValue(take.Key,out var bank)){bank=new SortedDictionary<int,List<WildlifeSoundTake>>();forms.Add(take.Key,bank);}
            if(!bank.TryGetValue(take.Form,out var list)){list=new List<WildlifeSoundTake>();bank.Add(take.Form,list);}list.Add(take);
        }
        public IReadOnlyList<WildlifeSoundTake> Compatible(string bank,string cue,uint voice,int acousticVariant=0)
        {
            if(!forms.TryGetValue(bank+"/"+cue,out var found)||found.Count==0)return Array.Empty<WildlifeSoundTake>();
            // Stable regional form; only takes within that form may alternate.
            int choice=(int)((Rng.Mix(voice)+(uint)Math.Max(0,acousticVariant))%(uint)found.Count);foreach(var form in found.Values)if(choice--==0)return form;
            return Array.Empty<WildlifeSoundTake>();
        }
        public bool Has(string bank,string cue="ambient")=>forms.ContainsKey(bank+"/"+cue);
        public int Count(string bank)=>Takes.Count(t=>t.Bank==bank);
    }
}

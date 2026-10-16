using System;
using System.Linq;
using System.Text;
using System.Globalization;

namespace WorldGen.Core
{
    /// <summary>Stateless, query-order-independent lazy vocabulary. Fixed inventories,
    /// bounded compounds and precomposed sound maps: O(input + emitted sound count),
    /// independent of world size, vocabulary size and language ancestry depth.</summary>
    public static class LanguageGenerator
    {
        // Stable IDs. First25 consonants, followed by7 vowels. Append, never reorder.
        static readonly string[] ipa={"p","b","t","d","k","ɡ","m","n","ŋ","f","v","s","z","ʃ","h","r","l","w","j","x","q","ts","tʃ","θ","ʒ","a","e","i","o","u","ə","ɨ"};
        static readonly string[] latin={"p","b","t","d","k","g","m","n","ng","f","v","s","z","sh","h","r","l","w","y","kh","q","ts","ch","th","zh","a","e","i","o","u","e","y"};
        static readonly int[][] inventories={
            new[]{0,2,4,6,6,7,7,11,14,15,16,17,18},
            new[]{0,1,2,3,4,5,6,7,9,10,11,12,13,15,16},
            new[]{1,2,3,4,5,6,7,8,11,13,15,16,21,22},
            new[]{0,2,4,6,7,9,11,14,15,17,18},
            new[]{1,2,3,4,5,6,7,11,12,14,15,16,19,20},
            new[]{0,2,4,6,7,8,11,13,15,16,18,21},
            new[]{0,1,2,3,4,5,6,7,9,11,14,15,16,23},
            new[]{0,2,4,6,7,9,11,13,14,16,17,18,22}
        };
        static readonly int[] codaCandidates={6,7,8,11,15,16,0,2,4};
        // Consonant place/manner/voicing; vowel height/backness/rounding.
        // Used for approximate loan substitution, not integer-ID proximity.
        static readonly int[] place={0,0,3,3,6,6,0,3,6,1,1,3,3,4,8,3,3,0,5,6,7,3,4,2,4,0,1,2,1,2,1,2};
        static readonly int[] manner={0,0,0,0,0,0,1,1,1,2,2,2,2,2,2,5,6,4,4,2,0,3,3,2,2,1,0,0,2,2,1,1};
        static readonly int[] voice={0,1,0,1,0,1,1,1,1,0,1,0,1,0,0,1,1,1,1,0,0,0,0,0,1,0,0,0,1,1,0,0};
        static readonly (int,int)[] changes={(0,9),(2,11),(4,19),(1,10),(3,12),(5,18),(13,11),(15,16),(25,28),(26,27),(28,29)};
        static readonly string[] themes={"red","white","old","high","stone","oak","reed","sun","falcon","deer","clear","great","east","west","sacred","green"};
        public const int SoundCount=32, MaximumLoans=12;

        public static LanguageProfile Create(int seed,int id,int year,LanguageProfile parent=null)
        {
            var r=new Rng(seed,"language.profile.v1",id);LanguageProfile p;
            if(parent!=null)
            {
                p=parent.Copy();p.Id=id;p.ParentId=parent.Id;p.FoundedYear=p.ChangedYear=year;p.Revision=0;
                p.Loans=parent.Loans.Select(x=>x.Copy()).ToArray();
                // Descendants retain the ancestral root generator and compose a
                // small number of regular changes into a fixed-size map.
                ApplyChange(p,ref r);if(r.Next()%4==0)ApplyChange(p,ref r);
            }
            else
            {
                var onset=inventories[r.Next()%(uint)inventories.Length];
                int mode=(int)(r.Next()%4);
                p=new LanguageProfile{Id=id,FamilyId=id,FamilySeed=unchecked((int)r.Next()),FoundedYear=year,ChangedYear=year,
                    Onsets=(int[])onset.Clone(),Vowels=mode==0?new[]{25,25,27,29}:mode==1?new[]{25,26,27,28,29,30,31}:new[]{25,25,26,27,28,29},
                    Codas=codaCandidates.Where(x=>onset.Contains(x)).ToArray(),SoundMap=Enumerable.Range(0,SoundCount).ToArray(),
                    CodaPercent=mode==0?0:15+(int)(r.Next()%46),ClusterPercent=(int)(r.Next()%4)==0?20:0,LongWordPercent=15+(int)(r.Next()%45),
                    Spelling=(int)(r.Next()%3),ModifierFirst=r.Next()%2==0,JoinedNames=r.Next()%4!=0,InitialStress=r.Next()%2==0,
                    WordOrder=new[]{"SOV","SVO","VSO"}[r.Next()%3],Script=(LanguageScript)(r.Next()%5)};
            }
            return p;
        }
        public static LanguageProfile Evolve(LanguageProfile source,int year)
        {
            var p=source.Copy();p.ChangedYear=year;p.Revision++;
            var r=new Rng(p.FamilySeed,"language.change.v1",unchecked(p.Id*7919+p.Revision));ApplyChange(p,ref r);return p;
        }
        static void ApplyChange(LanguageProfile p,ref Rng r)
        {
            int start=(int)(r.Next()%(uint)changes.Length);
            for(int k=0;k<changes.Length;k++)
            {
                var change=changes[(start+k)%changes.Length];
                if(!p.Onsets.Concat(p.Vowels).Any(x=>p.SoundMap[x]==change.Item1))continue;
                for(int j=0;j<p.SoundMap.Length;j++)if(p.SoundMap[j]==change.Item1)p.SoundMap[j]=change.Item2;
                foreach(var loan in p.Loans)for(int j=0;j<loan.Sounds.Length;j++)if(loan.Sounds[j]==change.Item1)loan.Sounds[j]=change.Item2;
                return;
            }
        }
        static void CheckConcept(string concept)
        {if(string.IsNullOrWhiteSpace(concept)||concept.Length>128)throw new ArgumentException("Use a stable sense ID of 1-128 characters, not arbitrary English text.",nameof(concept));}
        static int[] Root(LanguageProfile p,string concept,bool shortRoot=false)
        {
            CheckConcept(concept);
            // A root has one canonical form regardless of the API requesting it.
            shortRoot=concept=="people"||concept=="settlement"||concept=="land"||concept=="house"||concept=="region"||concept=="continent"||concept=="ocean"||concept=="sea"||concept=="river"||concept=="lake"||concept=="religion"||concept=="ford"||concept=="port"||concept=="hold"||concept=="grove";
            foreach(var loan in p.Loans)if(loan.Concept==concept)return (int[])loan.Sounds.Clone();
            var r=new Rng(p.FamilySeed,"language.root.v1/"+concept);
            int syllables=shortRoot?1:1+(r.Next()%100<(uint)p.LongWordPercent?2:1);
            var sounds=new int[syllables*5];int count=0;
            for(int s=0;s<syllables;s++)
            {
                sounds[count++]=-2; // syllable start; survives changes/loan adaptation
                int onset=p.Onsets[r.Next()%(uint)p.Onsets.Length];sounds[count++]=p.SoundMap[onset];
                if(p.ClusterPercent>0&&r.Next()%100<(uint)p.ClusterPercent&&(onset==0||onset==1||onset==2||onset==3||onset==4||onset==5))
                {int liquid=p.Onsets.Contains(15)?15:16;if(p.Onsets.Contains(liquid))sounds[count++]=p.SoundMap[liquid];}
                sounds[count++]=p.SoundMap[p.Vowels[r.Next()%(uint)p.Vowels.Length]];
                if(p.Codas.Length>0&&r.Next()%100<(uint)p.CodaPercent)sounds[count++]=p.SoundMap[p.Codas[r.Next()%(uint)p.Codas.Length]];
            }
            Array.Resize(ref sounds,count);return sounds;
        }
        public static LanguageWord Word(LanguageProfile p,string concept)=>Render(p,Root(p,concept));
        public static LanguageWord Compound(LanguageProfile p,string specific,string generic)
            =>Render(p,Join(p,Root(p,specific),Root(p,generic,true)));
        static int[] Join(LanguageProfile p,int[] specific,int[] generic)
        {
            var first=p.ModifierFirst?specific:generic;var last=p.ModifierFirst?generic:specific;
            // Stable boundary marker, not an extra syllable. Suppress duplicate
            // boundary sounds; do not repeatedly rewrite entire accumulated text.
            int skip=first.Length>0&&last.Length>0&&first[first.Length-1]==last[0]?1:0;
            var result=new int[first.Length+last.Length-skip+(p.JoinedNames?0:1)];Array.Copy(first,result,first.Length);int offset=first.Length;
            if(!p.JoinedNames)result[offset++]=-1;Array.Copy(last,skip,result,offset,last.Length-skip);return result;
        }
        static LanguageWord Render(LanguageProfile p,int[] sounds)
        {
            var written=new StringBuilder();var spoken=new StringBuilder();int syllables=sounds.Count(x=>x==-2),syllable=0;
            foreach(int sound in sounds)
            {
                if(sound==-2){syllable++;if(p.InitialStress?syllable==1:syllable==Math.Max(1,syllables-1))spoken.Append('ˈ');continue;}
                if(sound<0){written.Append(' ');spoken.Append(' ');continue;}
                string glyph=latin[sound];
                if(p.Spelling==1)glyph=sound==13?"š":sound==22?"č":sound==24?"ž":sound==19?"x":glyph;
                else if(p.Spelling==2)glyph=sound==4?"c":sound==18?"j":glyph;
                written.Append(glyph);spoken.Append(ipa[sound]);
            }
            return new LanguageWord(written.ToString(),spoken.ToString());
        }
        public static string KindConcept(LanguageNameKind kind)=>kind==LanguageNameKind.People?"people":kind==LanguageNameKind.Settlement?"settlement":kind==LanguageNameKind.State?"land":kind==LanguageNameKind.Person?"person":kind==LanguageNameKind.House?"house":kind.ToString().ToLowerInvariant();
        public static string SpecificConcept(LanguageProfile p,LanguageNameKind kind,int identity,string feature=null)
        {
            var r=new Rng(p.FamilySeed,"language.name.theme.v1/"+kind,identity);
            // A reusable environmental/semantic root in most names; personal
            // roots supply variety. Identity is never appended as a visible ID.
            return r.Next()%3==0?themes[r.Next()%(uint)themes.Length]:"name:"+identity.ToString(CultureInfo.InvariantCulture);
        }
        static string GenericConcept(LanguageProfile p,LanguageNameKind kind,int identity,string feature)
        {
            if(kind!=LanguageNameKind.Settlement)return KindConcept(kind);
            var r=new Rng(p.FamilySeed,"language.place.generic.v1",identity);
            return feature=="river"?new[]{"river","ford","settlement"}[r.Next()%3]:feature=="coast"?new[]{"sea","port","settlement"}[r.Next()%3]:feature=="mountain"?new[]{"hold","high","settlement"}[r.Next()%3]:new[]{"settlement","house","grove"}[r.Next()%3];
        }
        public static LanguageWord Name(LanguageProfile p,LanguageNameKind kind,int identity,string feature=null)
        {
            string specific=SpecificConcept(p,kind,identity,feature);
            return kind==LanguageNameKind.Person?Word(p,"person:"+identity.ToString(CultureInfo.InvariantCulture)):Compound(p,specific,GenericConcept(p,kind,identity,feature));
        }
        public static string Capitalize(string text)=>text.Length==0?text:char.ToUpperInvariant(text[0])+text.Substring(1);

        // Caller preserves the source name/profile epoch for a historical place.
        // No aliases table, vocabulary scan, world lookup or ancestry traversal.
        public static LanguageWord ForeignName(LanguageProfile source,LanguageProfile viewer,LanguageNameKind kind,int identity,string feature=null)
        {
            if(source.Id==viewer.Id&&source.Revision==viewer.Revision&&source.FamilySeed==viewer.FamilySeed)return Name(source,kind,identity,feature);
            if(kind==LanguageNameKind.Person)return Render(viewer,Adapt(viewer,Root(source,"person:"+identity.ToString(CultureInfo.InvariantCulture))));
            var r=new Rng(viewer.FamilySeed,"language.exonym.v1/"+kind,identity);
            string concept=SpecificConcept(source,kind,identity,feature);
            bool calque=!concept.StartsWith("name:",StringComparison.Ordinal)&&r.Next()%4==0;
            int[] specific=calque?Root(viewer,concept):Adapt(viewer,Root(source,concept));
            return Render(viewer,Join(viewer,specific,Root(viewer,GenericConcept(source,kind,identity,feature),true)));
        }
        static int[] Adapt(LanguageProfile p,int[] sounds)
        {
            var allowed=p.Onsets.Concat(p.Vowels).Select(x=>p.SoundMap[x]).Distinct().OrderBy(x=>x).ToArray();var result=new int[sounds.Length];
            for(int i=0;i<sounds.Length;i++)
            {
                int from=sounds[i],best=-1,score=int.MaxValue;if(from<0){result[i]=from;continue;}
                foreach(int candidate in allowed)
                {
                    int d=from==candidate?0:(from>=25)!=(candidate>=25)?1000:
                        from>=25?1+3*Math.Abs(place[from]-place[candidate])+3*Math.Abs(manner[from]-manner[candidate])+2*Math.Abs(voice[from]-voice[candidate]):
                        1+3*Math.Abs(place[from]-place[candidate])+(manner[from]==manner[candidate]?0:8)+2*Math.Abs(voice[from]-voice[candidate]);
                    if(d<score){score=d;best=candidate;}
                }
                result[i]=best;
            }
            return result;
        }
        public static LanguageProfile Borrow(LanguageProfile recipient,LanguageProfile donor,string concept,int year)
        {
            CheckConcept(concept);if(recipient.Loans.Length>=MaximumLoans||recipient.Loans.Any(x=>x.Concept==concept)||recipient.FamilySeed==donor.FamilySeed)return recipient;
            var p=recipient.Copy();p.Loans=p.Loans.Concat(new[]{new LanguageLoan{Concept=concept,SourceId=donor.Id,Year=year,Sounds=Adapt(p,Root(donor,concept))}}).ToArray();return p;
        }
        public static string SoundInventory(LanguageProfile p)=>string.Join(" ",p.Onsets.Concat(p.Vowels).Select(x=>p.SoundMap[x]).Distinct().OrderBy(x=>x).Select(x=>ipa[x]));
        // A small sentence scaffold for future props, NOT an English translator.
        public static string Sentence(LanguageProfile p,string subject,string verb,string obj)
        {
            string a=Word(p,subject).Text,b=Word(p,verb).Text,c=Word(p,obj).Text;
            return Capitalize(p.WordOrder=="SOV"?a+" "+c+" "+b:p.WordOrder=="VSO"?b+" "+a+" "+c:a+" "+b+" "+c)+".";
        }
        public static bool Valid(LanguageProfile p)
        {
            if(p==null||p.Version!="language-1"||p.Id<0||p.FamilyId<0||p.ParentId>=p.Id||p.ParentId< -1||p.Revision<0||p.FoundedYear<0||p.ChangedYear<p.FoundedYear||p.Onsets==null||p.Codas==null||p.Vowels==null||p.SoundMap?.Length!=SoundCount||p.Loans==null||p.Loans.Length>MaximumLoans)return false;
            if(p.Onsets.Length<3||p.Onsets.Length>24||p.Codas.Length>16||p.Vowels.Length<2||p.Vowels.Length>12||p.Onsets.Any(x=>x<0||x>=25)||p.Codas.Any(x=>!p.Onsets.Contains(x))||p.Vowels.Any(x=>x<25||x>=SoundCount)||p.SoundMap.Where((x,i)=>x<0||x>=SoundCount||(x>=25)!=(i>=25)).Any())return false;
            return p.CodaPercent>=0&&p.CodaPercent<=100&&p.ClusterPercent>=0&&p.ClusterPercent<=100&&p.LongWordPercent>=0&&p.LongWordPercent<=100&&p.Spelling>=0&&p.Spelling<=2&&Enum.IsDefined(typeof(LanguageScript),p.Script)&&(p.WordOrder=="SOV"||p.WordOrder=="SVO"||p.WordOrder=="VSO")&&p.Loans.All(x=>x!=null&&!string.IsNullOrWhiteSpace(x.Concept)&&x.Concept.Length<=128&&x.SourceId>=0&&x.Year>=0&&x.Sounds!=null&&x.Sounds.Length>0&&x.Sounds.Length<=24&&x.Sounds.All(y=>y==-2||y>=0&&y<SoundCount))&&p.Loans.Select(x=>x.Concept).Distinct().Count()==p.Loans.Length;
        }
    }
}

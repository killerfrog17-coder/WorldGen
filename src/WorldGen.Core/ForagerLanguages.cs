using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public static class ForagerLanguages
    {
        public static readonly string[] Concepts={"I","you","we","who","what","one","two","many","person","woman","man","child","mother","father","hand","foot","eye","ear","mouth","tooth","blood","bone","skin","hair","head","heart","water","fire","stone","wood","sun","moon","star","day","night","earth","mountain","river","sea","rain","wind","cold","hot","tree","grass","leaf","root","fruit","seed","meat","fish","bird","deer","snake","dog","food","hide","spear","bow","arrow","rope","bag","needle","boat","speech","name","home","camp","path","spirit","ancestor","dream","death","life","hunt","gather","eat","drink","sleep","go","come","see","hear","know","give","take","make","help","share","trade","friend","stranger","danger","good","bad","yes","no","here","there","now","before","tomorrow","because","if","and","not","sick","hurt","heal","dance","sing","story","promise","marry","guest","gift","winter","summer","spring","autumn","dry","wet","safe","far","near","large","small","sharp"};
        static readonly HashSet<string> Slow=new HashSet<string>{"I","you","we","who","what","one","two","hand","foot","eye","ear","mouth","tooth","blood","bone","water","fire","sun","moon","night","death"};
        static readonly int[][] SoundShifts={new[]{0,1,9,10},new[]{2,3,11,12,23},new[]{4,5,19,20},new[]{6,7,8},new[]{13,21,22,24},new[]{14,17,18},new[]{15,16},new[]{25,26,27,28,29,30,31}};
        public static int ConceptIndex(string concept)=>Array.IndexOf(Concepts,concept);
        static int Seed(int seed,string channel,int value)=>(int)(Rng.Mix(unchecked((uint)seed)^unchecked((uint)value*2654435761u)^unchecked((uint)channel.Length*7919u))&int.MaxValue);
        public static ForagerLanguage Create(int seed,int id,int district,int year,ForagerLanguage parent=null)
        {
            var l=new ForagerLanguage{Id=id,Parent=parent?.Id??-1,Family=parent?.Family??-1,OriginDistrict=district,FoundedYear=year,LastChangeYear=year,Seed=Seed(seed,"speech",id),Sounds=parent==null?LanguageGenerator.Create(seed,id,Math.Max(0,year)):parent.Sounds.Copy(),Roots=parent==null?new int[Concepts.Length]:(int[])parent.Roots.Clone(),RootYears=parent==null?new int[Concepts.Length]:(int[])parent.RootYears.Clone(),Rates=new double[Concepts.Length],Grammar=parent==null?new double[10]:(double[])parent.Grammar.Clone(),NarrativePractice=parent?.NarrativePractice??.6,TechnicalRegister=parent?.TechnicalRegister??.35,RitualRegister=parent?.RitualRegister??.4};
            l.Sounds.Id=id;l.Sounds.ParentId=parent?.Sounds.Id??-1;l.Sounds.FoundedYear=l.Sounds.ChangedYear=Math.Max(0,year);l.Sounds.Revision=0;l.Sounds.Loans=Array.Empty<LanguageLoan>();
            if(parent==null){l.NarrativePractice=.24+.18*Rng.Value(seed,"ancestral-narrative-practice",district);l.TechnicalRegister=.24+.14*Rng.Value(seed,"ancestral-technical-register",district);l.RitualRegister=.25+.25*Rng.Value(seed,"ancestral-ritual-register",district);}
            if(parent==null)l.Sounds=LanguageGenerator.Create(Seed(seed,"independent-phonology",id),id,Math.Max(0,year));
            for(int k=0;k<Concepts.Length;k++)
            {
                if(parent==null){l.Roots[k]=Seed(seed,"root",unchecked(id*104729+k*193));l.RootYears[k]=year;}
                double item=Slow.Contains(Concepts[k])?.000025:.00012+.00013*Rng.Value(seed,"lexical-item-rate",k);
                l.Rates[k]=item*(.55+.95*Rng.Value(seed,"lexical-community-rate",id));
            }
            if(parent==null)for(int k=0;k<l.Grammar.Length;k++)l.Grammar[k]=Rng.Value(seed,"forager-grammar-"+k,id);
            l.Sounds.WordOrder=l.Grammar[0]<.45?"SOV":l.Grammar[0]<.88?"SVO":"VSO";l.Sounds.ModifierFirst=l.Grammar[1]>.5;l.Sounds.InitialStress=l.Grammar[2]>.5;
            l.Name=LanguageGenerator.Capitalize(Word(l,"speech").Text);return l;
        }
        public static LanguageWord Word(ForagerLanguage language,string concept)
        {int at=ConceptIndex(concept);int seed=at>=0?language.Roots[at]:language.Seed;return LanguageGenerator.WordWithRootSeed(language.Sounds,concept,seed);}
        public static double SharedRoots(ForagerLanguage a,ForagerLanguage b)
        {if(a==null||b==null)return 0;int shared=0;for(int k=0;k<Concepts.Length;k++)if(a.Roots[k]==b.Roots[k])shared++;return shared/(double)Concepts.Length;}
        public static double SharedRoots(ForagerLanguage a,ForagerLanguageFamily family)
        {int shared=0;for(int k=0;k<Concepts.Length;k++)if(a.Roots[k]==family.ReferenceRoots[k])shared++;return shared/(double)Concepts.Length;}
        public static double Intelligibility(ForagerLanguage a,ForagerLanguage b)
        {
            double roots=SharedRoots(a,b);int sounds=0;for(int i=0;i<LanguageGenerator.SoundCount;i++)if(a.Sounds.SoundMap[i]==b.Sounds.SoundMap[i])sounds++;
            double grammar=0;for(int k=0;k<a.Grammar.Length;k++)grammar+=1-Math.Abs(a.Grammar[k]-b.Grammar[k]);grammar/=a.Grammar.Length;
            return D.Clamp(Math.Pow(roots,1.15)*(.6+.3*sounds/LanguageGenerator.SoundCount+.1*grammar));
        }
        public static void Evolve(int seed,ForagerLanguage l,int year,int elapsed,double isolation,double contact,ForagerLanguage donor=null)
        {
            if(elapsed<=0)return;
            for(int k=0;k<Concepts.Length;k++)
            {
                double rate=l.Rates[k]*(.8+.6*D.Clamp(isolation));
                if(Rng.Value(seed,"lexical-replacement-"+k,unchecked(l.Id*104729+year))<1-Math.Exp(-rate*elapsed))
                {l.Roots[k]=Seed(l.Seed,"replacement",unchecked(year*193+k*7919));l.RootYears[k]=year;}
                double borrowing=contact*(Slow.Contains(Concepts[k])?.000015:.00018)*elapsed;
                if(donor!=null&&Rng.Value(seed,"forager-borrow-"+k,unchecked(l.Id*7919+year))<borrowing){l.Roots[k]=donor.Roots[k];l.RootYears[k]=year;}
            }
            var random=new Rng(l.Seed,"forager-sound-history",year);var group=SoundShifts[random.Next()%(uint)SoundShifts.Length];int a=group[random.Next()%(uint)group.Length],b;
            do{b=group[random.Next()%(uint)group.Length];}while(b==a);
            for(int k=0;k<l.Sounds.SoundMap.Length;k++){int sound=l.Sounds.SoundMap[k];l.Sounds.SoundMap[k]=sound==a?b:sound==b?a:sound;}
            for(int k=0;k<l.Grammar.Length;k++)
            {
                if(Rng.Value(seed,"forager-grammar-shift-"+k,l.Id^year)<1-Math.Exp(-elapsed*(k<3?.000035:.00011)))l.Grammar[k]=Rng.Value(seed,"forager-grammar-value-"+k,l.Id+year);
                if(donor!=null)l.Grammar[k]=D.Clamp(l.Grammar[k]+(donor.Grammar[k]-l.Grammar[k])*D.Clamp(contact*elapsed/7000));
                l.Grammar[k]=D.Q(l.Grammar[k],1000000000);
            }
            l.Sounds.WordOrder=l.Grammar[0]<.45?"SOV":l.Grammar[0]<.88?"SVO":"VSO";l.Sounds.ModifierFirst=l.Grammar[1]>.5;l.Sounds.InitialStress=l.Grammar[2]>.5;
            l.Sounds.ChangedYear=Math.Max(0,year);l.Sounds.Revision++;l.LastChangeYear=year;
            l.Name=LanguageGenerator.Capitalize(Word(l,"speech").Text);
        }
        public static void ReviewFamilies(ForagerState state,int year,IReadOnlyDictionary<int,int[]> neighbours)
        {
            var families=state.LanguageFamilies.ToList();var byId=state.Languages.ToDictionary(l=>l.Id);
            var descendantFamilies=families.Where(f=>f.Parent>=0).GroupBy(f=>f.Parent).ToDictionary(g=>g.Key,g=>g.Select(f=>f.Id).ToList());
            foreach(var l in state.Languages.OrderBy(l=>l.Id))
            {
                if(l.Family<0){if(l.Parent>=0&&byId.TryGetValue(l.Parent,out var ancestor)&&ancestor.Family>=0)l.Family=ancestor.Family;else{NewFamily(l,-1);continue;}}
                var previous=families[l.Family];if(SharedRoots(l,previous)>=.32)continue;
                int match=-1;double best=.42;var candidates=new HashSet<int>();
                // Recognition of related speech is not contingent on current
                // contact. Minority varieties and isolated cousins retain their
                // recorded genealogy and can share a younger recognisable branch.
                if(descendantFamilies.TryGetValue(l.Family,out var descendants))foreach(int id in descendants)candidates.Add(id);
                if(l.Parent>=0&&byId.TryGetValue(l.Parent,out var parent)&&parent.Family>=0&&parent.Family!=l.Family)candidates.Add(parent.Family);
                if(neighbours!=null&&neighbours.TryGetValue(l.Id,out var contacts))foreach(int other in contacts)
                {if(byId.TryGetValue(other,out var donor)&&donor.Family>=0&&families[donor.Family].Parent==l.Family)candidates.Add(donor.Family);}
                foreach(int candidate in candidates.OrderBy(id=>id))
                {double shared=SharedRoots(l,families[candidate]);if(shared>best){best=shared;match=candidate;}}
                if(match>=0)l.Family=match;else NewFamily(l,l.Family);
            }
            state.LanguageFamilies=families.ToArray();
            void NewFamily(ForagerLanguage language,int parent)
            {
                int id=state.NextFamily++;var f=new ForagerLanguageFamily{Id=id,Parent=parent,FounderLanguage=language.Id,FoundedYear=year,OriginDistrict=language.OriginDistrict,Name=language.Name+" family",ReferenceRoots=(int[])language.Roots.Clone()};families.Add(f);language.Family=id;
                if(parent>=0){if(!descendantFamilies.TryGetValue(parent,out var children))descendantFamilies[parent]=children=new List<int>();children.Add(id);}
            }
        }
        public static string Sentence(ForagerLanguage l,string subject,string verb,string obj)
        {string a=Word(l,subject).Text,b=Word(l,verb).Text,c=Word(l,obj).Text;return LanguageGenerator.Capitalize(l.Sounds.WordOrder=="SVO"?a+" "+b+" "+c:l.Sounds.WordOrder=="VSO"?b+" "+a+" "+c:a+" "+c+" "+b);}
        public static string Utterance(ForagerLanguage l,ForagerCohort c,string subject,string verb,string obj,string when="",string reason="")
        {
            bool Known(string concept){int k=concept=="bow"||concept=="arrow"?17:concept=="needle"?21:concept=="boat"?30:concept=="clothing"?8:concept=="rope"?7:-1;return k<0||c.Knowledge[k]>=.2;}
            string W(string concept)=>Word(l,Known(concept)?concept:"thing").Text;
            string phrase=l.NarrativePractice<.3?W(verb)+" "+W(obj):l.Sounds.WordOrder=="SVO"?W(subject)+" "+W(verb)+" "+W(obj):l.Sounds.WordOrder=="VSO"?W(verb)+" "+W(subject)+" "+W(obj):W(subject)+" "+W(obj)+" "+W(verb);
            if(when.Length>0&&l.NarrativePractice>.45)phrase=W(when)+" "+phrase;
            if(reason.Length>0&&l.NarrativePractice>.65&&l.TechnicalRegister>.65)phrase+=" "+W("because")+" "+W(reason);
            return LanguageGenerator.Capitalize(phrase);
        }
    }
}

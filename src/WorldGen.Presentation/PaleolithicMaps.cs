using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // All observer paths share this projection. Culture, speech, knowledge and
    // species have independent keys; none is a surrogate for political control.
    public sealed class PaleolithicMaps
    {
        sealed class Entry{public string Hash="";public PaleolithicMaps? View;}
        static readonly ConditionalWeakTable<PrehistoryState,Entry> Cache=new ConditionalWeakTable<PrehistoryState,Entry>();
        public static PaleolithicMaps For(PrehistoryState s)
        {var e=Cache.GetValue(s,_=>new Entry());lock(e){if(e.View==null||!Prehistory.Foraging(s)&&e.Hash!=s.Hash){e.View=new PaleolithicMaps(s);e.Hash=Prehistory.Foraging(s)?"":s.Hash;}return e.View;}}
        readonly PrehistoryState state;
        readonly ForagerMaps? foragers;
        readonly Dictionary<int,PaleolithicCommunity> communities;
        readonly Dictionary<int,int> speech=new Dictionary<int,int>(),technology=new Dictionary<int,int>();
        readonly Dictionary<int,LanguageProfile> varieties=new Dictionary<int,LanguageProfile>();
        readonly Dictionary<int,string> names=new Dictionary<int,string>();
        public PaleolithicCommunity? Community(int cell)
        {int id=cell>=0&&cell<state.Society.CellCulture.Length?state.Society.CellCulture[cell]:-1;return communities.TryGetValue(id,out var c)?c:null;}
        PaleolithicMaps(PrehistoryState s)
        {
            if(Prehistory.Foraging(s)){state=s;communities=new Dictionary<int,PaleolithicCommunity>();foragers=ForagerMaps.For(s);return;}
            state=s;communities=s.Paleolithic.Communities.ToDictionary(c=>c.Identity);
            foreach(var c in s.Paleolithic.Communities.OrderBy(c=>c.Identity))
            {
                technology[c.Identity]=Enumerable.Range(0,PaleolithicRules.TechniqueCount).Count(k=>PaleolithicRules.Adoption(s.Paleolithic,c,k)>=.5);
                var l=c.PreviousLanguage!=null&&s.Paleolithic.Seconds-c.LanguageChangeSeconds<500*SimulationTime.Year?c.PreviousLanguage:c.Language;
                // Identical sound/grammar profiles share a dialect regardless of
                // the community IDs, dates or administrative patch boundaries.
                string signature=Signature(l);
                int key=(int)(Convert.ToUInt32(signature.Substring(0,8),16)&0x7fffffff);
                while(varieties.TryGetValue(key,out var other)&&Signature(other)!=signature)key=key==int.MaxValue?0:key+1;
                varieties[key]=l;speech[c.Identity]=key;
            }
            foreach(var p in varieties)
            {
                var l=p.Value;string name=LanguageGenerator.Capitalize(LanguageGenerator.Word(l,"speech").Text);
                var family=LanguageGenerator.Create(l.FamilySeed,0,0);
                string root=LanguageGenerator.Capitalize(LanguageGenerator.Word(family,"speech").Text);
                names[p.Key]=name+" dialect · "+root+" family";
            }
        }
        static string Signature(LanguageProfile l)=>StateHash.Compute(new object[]{l.FamilySeed,l.Onsets,l.Codas,l.Vowels,l.SoundMap,l.WordOrder,l.ModifierFirst,l.CodaPercent,l.ClusterPercent,l.InitialStress,l.Loans.Select(x=>new object[]{x.Concept,x.Sounds}).ToArray()});
        public int Id(int cell,HistoryOverlay overlay)
        {
            if(foragers!=null)return foragers.Id(cell,overlay);
            if(cell<0||cell>=state.Society.CellCulture.Length)return -1;
            if(overlay==HistoryOverlay.HumanSpecies){int mask=HumanRangeState.Mask(state,cell);return mask==0?-1:mask;}
            var c=Community(cell);if(c==null)return -1;
            switch(overlay){case HistoryOverlay.Cultures:return c.Identity;case HistoryOverlay.Languages:return speech[c.Identity];case HistoryOverlay.Technology:return technology[c.Identity];default:return -1;}
        }
        public LanguageProfile? Language(int key)=>foragers!=null&&key>=0&&key<state.Foragers.Languages.Length?state.Foragers.Languages[key].Sounds:varieties.TryGetValue(key,out var l)?l:null;
        public string Name(int key,HistoryOverlay overlay)=>foragers!=null?foragers.Name(key,overlay):overlay==HistoryOverlay.HumanSpecies?HumanRangeState.MaskName(key):overlay==HistoryOverlay.Technology?key+" established practices":overlay==HistoryOverlay.Languages?(names.TryGetValue(key,out var n)?n:""):state.Society.Cultures[key].Name;
        public V Color(int id,HistoryOverlay overlay)
        {
            if(foragers!=null)return foragers.Color(id,overlay);
            if(overlay==HistoryOverlay.Technology){double t=id/(double)PaleolithicRules.TechniqueCount;return new V(.73-.51*t,.40+.40*t,.19+.61*t);}
            if(overlay==HistoryOverlay.HumanSpecies)
            {V c=new V();int count=0;var colors=new[]{new V(.25,.8,.62),new V(.45,.55,.95),new V(.95,.59,.28)};for(int i=0;i<colors.Length;i++)if((id&(1<<i))!=0){c+=colors[i];count++;}return count>0?c/count:new V();}
            if(overlay==HistoryOverlay.Languages&&varieties.TryGetValue(id,out var l))
            {var c=SocietyView.CivilizationColor(l.FamilySeed&0xffff);double shade=.66+.34*((id%1009)/1008.0);return c*shade+new V(.12,.12,.12)*(1-shade);}
            return SocietyView.CivilizationColor(id);
        }
        public HistoryLegendEntry[] Legend(HistoryOverlay overlay,string sort,Prehistory? engine=null)
        {
            if(foragers!=null)return foragers.Legend(overlay,sort);
            var groups=new Dictionary<int,HistoryLegendEntry>();
            var counted=new HashSet<int>();
            for(int cell=0;cell<state.Society.CellCulture.Length;cell++)
            {
                int id=Id(cell,overlay);if(id<0)continue;
                if(!groups.TryGetValue(id,out var e))groups[id]=e=new HistoryLegendEntry{Name=Name(id,overlay),Cell=cell,ColorId=id,CustomColor=true,Color=Color(id,overlay)};
                var c=Community(cell);
                if(overlay==HistoryOverlay.HumanSpecies&&state.HumanRanges!=null)
                {int patch=state.HumanRanges.CellPatch[cell];if(patch>=0&&counted.Add(patch)){e.Population+=state.Population[patch]+(long)Math.Round(state.HumanRanges.Lineages.Sum(l=>HumanRangeState.ResidentsAt(state,l,patch)));if(engine!=null)e.AreaKm2+=(long)engine.Patches[patch].Cells.Sum(i=>engine.World.Surface.Cells[i].AreaM2/1e6);}}
                else if(c!=null&&state.Society.Cultures[c.Identity].OriginCell==cell){e.Population+=state.Population[c.Patch];e.AreaKm2+=state.Society.Cultures[c.Identity].AreaKm2;}
            }
            return groups.Values.OrderByDescending(e=>sort=="Land area"?e.AreaKm2:overlay==HistoryOverlay.Technology?e.ColorId:e.Population).ThenBy(e=>e.ColorId).ToArray();
        }
    }
}

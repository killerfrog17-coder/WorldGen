using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public sealed class ForagerMaps
    {
        static readonly ConditionalWeakTable<PrehistoryState,ForagerMaps> Cache=new ConditionalWeakTable<PrehistoryState,ForagerMaps>();
        public static ForagerMaps For(PrehistoryState s)=>Cache.GetValue(s,x=>new ForagerMaps(x));
        readonly PrehistoryState state;
        readonly ForagerCohort[][] residents;
        readonly Dictionary<int,double> population;
        readonly int[] speech;
        public double Population(ForagerCohort c)=>population[c.Id];
        public long TotalPopulation=>(long)Math.Round(population.Values.Sum());
        public ForagerCohort[] Residents(int cell)=>cell>=0&&cell<state.Foragers.CellDistrict.Length&&state.Foragers.CellDistrict[cell]>=0?residents[state.Foragers.CellDistrict[cell]]:Array.Empty<ForagerCohort>();
        public ForagerCohort? Dominant(int cell)=>Residents(cell).FirstOrDefault();
        ForagerMaps(PrehistoryState s)
        {
            state=s;population=s.Foragers.Cohorts.ToDictionary(c=>c.Id,c=>ForagerDemography.Present(s.Foragers,c));var byDistrict=s.Foragers.Cohorts.Where(c=>Population(c)>=1).Select(c=>ForagerAdoption.Project(s.Foragers,c)).GroupBy(c=>c.District).ToDictionary(g=>g.Key,g=>g.OrderByDescending(c=>Population(c)).ThenBy(c=>c.Id).ToArray());
            residents=s.Foragers.Districts.Select(d=>byDistrict.TryGetValue(d.Id,out var c)?c:Array.Empty<ForagerCohort>()).ToArray();
            speech=residents.Select(c=>c.Length>0?Majority(c,true):-1).ToArray();
        }
        public static int Toolkit(ForagerCohort c)
        {
            int key=0;if(c.Use[13]+c.Use[14]+c.Use[31]>.65)key|=1;
            if(c.Use[19]+c.Use[24]+c.Use[30]>.5)key|=2;
            if(c.Use[17]+c.Use[22]>.45)key|=4;
            if(c.Use[4]+c.Use[10]>.8)key|=8;
            if(c.Use[26]+c.Use[27]+c.Use[28]>1.25)key|=16;
            if(c.Use[23]+c.Use[32]>.65)key|=32;if(c.Use[1]>.45)key|=64;if(c.Use.Length>33&&c.Use[33]>.4)key|=128;return key;
        }
        public int Id(int cell,HistoryOverlay overlay)
        {
            var local=Residents(cell);if(local.Length==0)return -1;var c=local[0];
            switch(overlay)
            {
                case HistoryOverlay.Civilizations:return local.Any(p=>state.Foragers.Lineages.Any(l=>l.PlayerSpecies&&state.Foragers.SpeciesShare(p,l.Id)*Population(p)>=1))?1:0;
                case HistoryOverlay.Cultures:return c.Culture;
                case HistoryOverlay.Ethnicities:return c.Identity;
                case HistoryOverlay.Languages:return speech[c.District];
                // Match the local-community scale of the culture layer. A
                // shared ancestral minority across many different communities
                // is not one uniform district-wide religion.
                case HistoryOverlay.Religions:return c.Belief;
                case HistoryOverlay.Technology:return Toolkit(c);
                case HistoryOverlay.HumanSpecies:int mask=0;foreach(var p in local)for(int k=0;k<state.Foragers.Lineages.Length;k++)if(state.Foragers.SpeciesShare(p,k)*Population(p)>=1)mask|=1<<k;return mask;
                default:return -1;
            }
        }
        int Majority(ForagerCohort[] local,bool speech)=>local.SelectMany(c=>(speech?c.Speech:c.Traditions).Select(x=>(x.Id,people:Population(c)*x.Share))).GroupBy(x=>x.Id).OrderByDescending(g=>g.Sum(x=>x.people)).ThenBy(g=>g.Key).First().Key;
        public string Name(int id,HistoryOverlay overlay)
        {
            var f=state.Foragers;if(id<0)return "";
            switch(overlay)
            {
                case HistoryOverlay.Civilizations:return id==1?"Homo sapiens present":"Other human species present";
                case HistoryOverlay.Cultures:return f.Cultures[id].Name;
                case HistoryOverlay.Ethnicities:return f.Peoples[id].Name;
                case HistoryOverlay.Languages:var l=f.Languages[id];return l.Name+(l.Parent>=0&&ForagerLanguages.Intelligibility(l,f.Languages[l.Parent])>.72?" dialect":" language")+" · "+f.LanguageFamilies[l.Family].Name;
                case HistoryOverlay.Religions:return f.Beliefs[id].Name;
                case HistoryOverlay.HumanSpecies:return string.Join(" / ",f.Lineages.Where(l=>(id&(1<<l.Id))!=0).Select(l=>l.Name));
                case HistoryOverlay.Technology:var names=new List<string>();if((id&1)!=0)names.Add("cold-weather");if((id&2)!=0)names.Add("aquatic");if((id&4)!=0)names.Add("projectile");if((id&8)!=0)names.Add("composite tools");if((id&16)!=0)names.Add("plant-processing");if((id&32)!=0)names.Add("preservation");if((id&64)!=0)names.Add("prepared-core");if((id&128)!=0)names.Add("bifacial");return names.Count==0?"Generalist foraging toolkit":string.Join(" + ",names);
                default:return "";
            }
        }
        public V Color(int id,HistoryOverlay overlay)
        {
            if(overlay==HistoryOverlay.Civilizations)return id==1?new V(.49,.49,.49):new V(.78,.78,.78);
            if(overlay==HistoryOverlay.HumanSpecies)
            {V sum=new V();int n=0;foreach(var l in state.Foragers.Lineages)if((id&(1<<l.Id))!=0){sum+=SocietyView.CivilizationColor(l.Id+3);n++;}return n>0?sum/n:new V();}
            if(overlay==HistoryOverlay.Languages)
            {var l=state.Foragers.Languages[id];V family=SocietyView.CivilizationColor(l.Family+11);double shade=.72+.28*Rng.Value(l.Seed,"speech-shade",id);return family*shade+new V(.12,.12,.12)*(1-shade);}
            if(overlay==HistoryOverlay.Religions)
            {
                // A distant founding tradition is genealogy, not a single
                // continent-wide religion. The legend and selection identify
                // local traditions, so the map must distinguish those records.
                return SocietyView.CivilizationColor(id+31);
            }
            if(overlay==HistoryOverlay.Technology)return SocietyView.CivilizationColor(id*7+19);
            if(overlay==HistoryOverlay.Ethnicities)return SocietyView.CivilizationColor(id+67);
            return SocietyView.CivilizationColor(id);
        }
        public HistoryLegendEntry[] Legend(HistoryOverlay overlay,string sort)
        {
            var entries=new Dictionary<int,HistoryLegendEntry>();var sums=new Dictionary<int,(double population,double area)>();
            foreach(var d in state.Foragers.Districts)
            {
                var people=residents[d.Id];if(people.Length==0)continue;double total=people.Sum(c=>Population(c)),area=d.AreaKm2*people.Max(c=>c.Occupation);
                if(overlay==HistoryOverlay.HumanSpecies||overlay==HistoryOverlay.Civilizations){Add(Id(d.Cell,overlay),total,area);continue;}
                foreach(var c in people)
                {
                    double population=Population(c);
                    if(overlay==HistoryOverlay.Languages||overlay==HistoryOverlay.Religions)
                    {foreach(var x in overlay==HistoryOverlay.Languages?c.Speech:c.Traditions)Add(x.Id,population*x.Share,area*population/total*x.Share);continue;}
                    int id=overlay==HistoryOverlay.Cultures?c.Culture:overlay==HistoryOverlay.Ethnicities?c.Identity:overlay==HistoryOverlay.Technology?Toolkit(c):-1;
                    Add(id,population,area*population/total);
                }
                void Add(int id,double population,double shareArea)
                {
                    if(id<0)return;if(!entries.TryGetValue(id,out var e))entries[id]=e=new HistoryLegendEntry{Name=Name(id,overlay),ColorId=id,Cell=d.Cell,CustomColor=true,Color=Color(id,overlay)};
                    sums.TryGetValue(id,out var prior);sums[id]=(prior.population+population,prior.area+shareArea);
                }
            }
            foreach(var pair in sums){entries[pair.Key].Population=(long)Math.Round(pair.Value.population);entries[pair.Key].AreaKm2=(long)Math.Round(pair.Value.area);}
            return entries.Values.Where(e=>e.Population>0).OrderByDescending(e=>sort=="Land area"?e.AreaKm2:e.Population).ThenBy(e=>e.Name).ToArray();
        }
        public HistoryLegendEntry[] MapLabels(HistoryOverlay overlay)
        {
            // The full legend includes household minorities. Labels instead
            // belong on cells whose actual colour represents that identity.
            var totals=Legend(overlay,"Default").ToDictionary(e=>e.ColorId);
            return state.Foragers.Districts.Where(d=>residents[d.Id].Length>0)
                .GroupBy(d=>Id(d.Cell,overlay)).Where(g=>g.Key>=0).Select(g=>
                {
                    var district=g.OrderByDescending(d=>residents[d.Id].Sum(Population)).ThenBy(d=>d.Id).First();
                    var total=totals[g.Key];
                    return new HistoryLegendEntry{Name=total.Name,Population=total.Population,AreaKm2=total.AreaKm2,Cell=district.Cell,LabelCell=district.Cell,ColorId=g.Key,CustomColor=true,Color=total.Color};
                }).ToArray();
        }
    }
}

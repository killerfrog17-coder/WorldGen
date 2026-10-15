using System;
using System.Linq;
using System.Collections.Generic;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalAnimalHusbandry
    {
        public int TaxonId,OriginCell,Practice,AdoptedYear,SourceCulture=-1;
        public string Name="";
        public HistoricalAnimalHusbandry Copy()=>(HistoricalAnimalHusbandry)MemberwiseClone();
    }
    public sealed partial class Prehistory
    {
        void AdvanceAnimalHusbandry(PrehistoryState s,CultureIdentity c,SortedSet<(int,int)> contacts,double elapsed)
        {
            if(!HistoricalLocalEffects.AgricultureOpen(s.Years)||s.Years-c.FoundedYear<500)return;
            var agriculture=s.History.Agriculture[c.Id];var e=World.Ecology;
            var places=new[]{c.OriginCell}.Concat(agriculture.PracticeCell??Array.Empty<int>()).Concat(s.Society.Settlements.Where(t=>t.CultureId==c.Id&&t.Population>0).Select(t=>t.Cell)).Distinct().OrderBy(i=>i).ToArray();
            var records=(agriculture.Animals??Array.Empty<HistoricalAnimalHusbandry>()).ToList();
            foreach(var animal in e.Taxa.Where(t=>NativeLifePack.Domesticability(t.Traits)>=.15))
            {
                var existing=records.FirstOrDefault(a=>a.TaxonId==animal.Id);if(existing?.AdoptedYear>0)continue;
                int native=places.Where(i=>Biogeography.Present(e,i,animal.Id)).DefaultIfEmpty(-1).First();
                int source=c.Contacts.Where(id=>contacts.Contains((Math.Min(id,c.Id),Math.Max(id,c.Id)))&&(s.History.Agriculture[id].Animals?.Any(a=>a.TaxonId==animal.Id&&a.AdoptedYear>0&&a.AdoptedYear<=s.Years)??false)).OrderBy(id=>id).DefaultIfEmpty(-1).First();
                int site=native>=0?native:places.Where(i=>Biogeography.Suitable(animal,CropHabitat(s,i),World.ElevationM[i])).DefaultIfEmpty(-1).First();
                if(site<0||native<0&&source<0)continue;
                var traits=animal.Traits;int region=e.Region[site];var form=region>=0?e.Regions[region].Species.FirstOrDefault(p=>p.TaxonId==animal.Id)?.Variant:null;
                if(form!=null){traits=traits.Copy();traits.Docility=form.Docility;traits.PanicResistance=form.PanicResistance;}
                if(existing==null){existing=new HistoricalAnimalHusbandry{TaxonId=animal.Id,Name=animal.Name,OriginCell=site,SourceCulture=source};records.Add(existing);}
                double incentive=.15+.85*D.Clamp(c.Farming/600.0);
                double progress=elapsed*(source>=0?.8:NativeLifePack.Domesticability(traits)*incentive/6);
                int gain=(int)Math.Floor(progress);if(HistoryRoll("animal-management-16",c.Id*257+animal.Id,s.Years)<progress-gain)gain++;
                existing.Practice=Math.Min(1000,existing.Practice+gain);
                if(existing.Practice<600)continue;
                existing.AdoptedYear=s.Years+1;existing.SourceCulture=source;
                HistoryEvent(s,"animal-domestication",c.Name+(source<0?" develops sustained breeding and management of ":" adopts breeding and management of ")+animal.Name.ToLowerInvariant()+(source<0?" from local wild populations.":" through contact with "+s.Society.Cultures[source].Name+"."),site,c.Id,category:ChronicleCategory.Discoveries);
            }
            agriculture.Animals=records.OrderBy(a=>a.TaxonId).ToArray();
        }
        double AnimalFoodMultiplier(PrehistoryState s,int culture,int cell)
        {
            var animals=s.History.Agriculture[culture].Animals;if(animals==null)return 1;
            double contribution=0;var habitat=CropHabitat(s,cell);
            foreach(var record in animals)
            {
                if(record.AdoptedYear==0)continue;var t=World.Ecology.Taxa[record.TaxonId];
                if(!Biogeography.Suitable(t,habitat,World.ElevationM[cell]))continue;
                // Livestock supplements crops; neither carnivore presence nor
                // the mere availability of a large mammal grants a state bonus.
                contribution+=t.Traits.Traction?.12:t.Traits.Milk?.08:.04;
            }
            return D.Q(1+Math.Min(.35,contribution),1000000);
        }
    }
}

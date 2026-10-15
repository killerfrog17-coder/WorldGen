using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public static class HistoricalMaritimeKnowledge
    {
        // Introductions require an established source and a crop that can actually grow.
        // A later arrival date cannot serve as evidence of earlier domestication.
        public static bool CanCarryCrop(int year,int sourceAdoptionYear,double suitability)
            =>sourceAdoptionYear>0&&sourceAdoptionYear<=year&&suitability>=.3;
        public static double FoundingChance(int distanceM,bool wayfinding,long residents,double storage,double similarity)
            =>D.Q(D.Clamp((wayfinding?.10:.04)*Math.Exp(-Math.Max(0,distanceM)/(wayfinding?400000.0:120000.0))*D.Clamp(residents/1200.0)*D.Clamp(storage)*(.35+.65*D.Clamp(similarity))),1000000000);
    }
    public sealed partial class Prehistory
    {
        void InheritHistoricalCommunity(PrehistoryState s,CultureIdentity child,int source,bool maritime)
        {
            var h=s.History;var parent=h.Cultures[source];var hc=h.Cultures[child.Id];
            hc.ParentCultureId=source;hc.LanguageId=parent.LanguageId;
            var faith=h.Religions[hc.ReligionId];faith.ParentId=parent.ReligionId;
            faith.Tradition="Local traditions descended from "+h.Religions[parent.ReligionId].Name;
            var crops=h.Agriculture[child.Id];var carried=h.Agriculture[source];
            for(int k=0;k<6;k++)if((!LocalHistory(s)||HistoricalLocalEffects.AgricultureOpen(s.Years))&&HistoricalMaritimeKnowledge.CanCarryCrop(s.Years,carried.AdoptedYear[k],ModernHistory(s)?CropTransferFit(s,source,child.OriginCell,k):CropSuitability(child.OriginCell,k)))
            {if(ModernHistory(s)&&LocalCultivarFit(s,source,child.OriginCell,k)<.25)continue;crops.AdoptedYear[k]=s.Years+1;crops.SourceCulture[k]=source;crops.Practice[k]=600;if(ModernHistory(s))crops.Cultivars[k]=MakeCultivar(s,child.Id,k,source);}
            if(crops.AdoptedYear.Any(y=>y>0)){child.Farming=50;hc.Technology[(int)HistoricalTechnology.Cultivation]=s.Years+1;}
            if(ModernHistory(s))
            {
                var origin=s.Society.Cultures[source];if(child.Farming>0)child.Farming=Math.Max(50,Math.Min(600,origin.Farming));
                hc.Sedentism=Math.Min(650,parent.Sedentism);hc.Institution=Math.Min(600,parent.Institution);child.Organization=hc.Institution;
            }
            if(ModernHistory(s))InvalidateCultivarFits(child.Id);
            // Portable methods are not a free transfer of cities, states, metal industries or armies.
            foreach(var practice in new[]{HistoricalTechnology.Pottery,HistoricalTechnology.Sailing,HistoricalTechnology.Navigation})
            {
                int k=(int)practice;if(!maritime&&practice!=HistoricalTechnology.Pottery)continue;
                if(parent.Technology[k]>0&&parent.Technology[k]<=s.Years&&HistoricalTechnologies.Requirements(h.Version,k).All(p=>hc.Technology[p]>0))hc.Technology[k]=s.Years+1;
            }
            if(ModernHistory(s)&&HistoricalTechnologies.Has(parent,HistoricalTechnology.Writing)&&parent.Technology[(int)HistoricalTechnology.Writing]<=s.Years&&HistoricalTechnologies.Requirements(h.Version,(int)HistoricalTechnology.Writing).All(p=>hc.Technology[p]>0))hc.Technology[(int)HistoricalTechnology.Writing]=s.Years+1;
            child.Contacts=new[]{source};var identity=s.Society.Cultures[source];
            identity.Contacts=identity.Contacts.Concat(new[]{child.Id}).Distinct().OrderBy(x=>x).ToArray();
        }
        void MaritimeFoundingCommunities(PrehistoryState s,CancellationToken token,double scale=1)
        {
            if(s.Society.Cultures.Length==0)return;
            var society=s.Society;int sourceCount=society.Cultures.Length;
            var established=new bool[Patches.Length];foreach(var c in society.Cultures)established[CellPatch[c.OriginCell]]=true;
            var candidates=new System.Collections.Generic.SortedDictionary<int,(int cell,int source,double chance)>();
            foreach(var route in Routes)
            {
                token.ThrowIfCancellationRequested();if(!route.Sea||route.CellA<0||route.CellB<0)continue;
                for(int direction=0;direction<2;direction++)
                {
                    int a=direction==0?route.A:route.B,b=direction==0?route.B:route.A;
                    int from=direction==0?route.CellA:route.CellB,to=direction==0?route.CellB:route.CellA;
                    int source=society.CellCulture[from];if(source<0||source>=sourceCount||established[b]||s.Population[a]<400||s.Population[b]<300||!LocalHistory(s)&&society.Knowledge[b]<3500||s.ArrivalYear[b]<0||s.Years-s.ArrivalYear[b]<100)continue;
                    var hc=s.History.Cultures[source];if(hc.Technology[(int)HistoricalTechnology.Sailing]<=0||hc.Technology[(int)HistoricalTechnology.Sailing]>s.Years)continue;
                    bool navigation=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation);if(route.DistanceM>(navigation?700000:220000))continue;
                    int cell=FindCoastalCultureSite(s,b,to,true);
                    if(cell<0)continue;double storage=CultureOpportunity(s,cell);if(storage<.2)continue;
                    double chance=HistoricalMaritimeKnowledge.FoundingChance(route.DistanceM,navigation,s.Population[b],storage,HabitatSimilarity(a,b));
                    if(!candidates.TryGetValue(b,out var previous)||chance>previous.chance||chance==previous.chance&&source<previous.source)candidates[b]=(cell,source,chance);
                }
            }
            foreach(var pair in candidates)
            {
                var site=pair.Value;if(HistoryRoll("maritime-founding",pair.Key,s.Years)>=ElapsedChance(site.chance,scale))continue;
                var child=FoundCulture(s,pair.Key,site.cell);InitializeHistoricalCulture(s,child);InheritHistoricalCommunity(s,child,site.source,true);
                bool carriedFood=s.History.Agriculture[child.Id].AdoptedYear.Any(y=>y>0);
                HistoryEvent(s,"maritime-community",child.Name+" develops an overseas community linked to "+society.Cultures[site.source].Name+", carrying seafaring knowledge"+(carriedFood?" and locally suitable food plants.":" and maintaining local subsistence."),site.cell,child.Id,category:ChronicleCategory.Society);
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Existing local people can adopt a neighbor's food plants without first
        // inventing agriculture independently or becoming the same cultural identity.
        void FarmingFrontiers(PrehistoryState s,CancellationToken token,double scale=1)
        {
            if(ModernHistory(s))return; // One local teaching process, not a second adoption route.
            if(LocalHistory(s)&&!HistoricalLocalEffects.AgricultureOpen(s.Years))return;
            var h=s.Society;if(h.CellCulture.Length==0)return;
            var established=new bool[Patches.Length];foreach(var c in h.Cultures)established[CellPatch[c.OriginCell]]=true;
            var cells=Enumerable.Repeat(-1,Patches.Length).ToArray();var sources=Enumerable.Repeat(-1,Patches.Length).ToArray();var scores=new double[Patches.Length];
            foreach(int i in CulturalCells(s))
            {
                if((i&2047)==0)token.ThrowIfCancellationRequested();int source=h.CellCulture[i];if(source<0||h.Cultures[source].Farming<150)continue;
                var a=s.History.Agriculture[source];
                foreach(int j in World.Surface.Cells[i].Neighbors)
                {
                    int patch=CellPatch[j];if(patch<0||established[patch]||h.CellCulture[j]>=0||s.Population[patch]<300||!LocalHistory(s)&&h.Knowledge[patch]<3500||World.Hydrology.LakeCoverageFraction[j]>=.5)continue;
                    if(World.Hydrology.OceanConnected[j]||World.Hydrology.OceanConnected[i]||World.Ecology.Habitat[j].ReliefM>2200)continue;
                    double crop=0;for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0&&a.AdoptedYear[k]-1<s.Years)crop=Math.Max(crop,ModernHistory(s)?CropTransferFit(s,source,j,k):CropSuitability(j,k));
                    double score=crop*D.Clamp(HistoricalSiteDensity(s,j)/.025);
                    if(crop<.3||CultureOpportunity(s,j)<.25||score<=scores[patch])continue;
                    scores[patch]=score;cells[patch]=j;sources[patch]=source;
                }
            }
            for(int patch=0;patch<cells.Length;patch++)if(cells[patch]>=0&&HistoryRoll("farming-frontier",patch,s.Years)<ElapsedChance(.06*scores[patch],scale))
            {
                int source=sources[patch],cell=cells[patch];var c=FoundCulture(s,patch,cell);InitializeHistoricalCulture(s,c);
                var a=s.History.Agriculture[c.Id];var parent=s.History.Agriculture[source];
                for(int k=0;k<6;k++)if(!ModernHistory(s)&&parent.AdoptedYear[k]>0&&parent.AdoptedYear[k]-1<s.Years&&CropSuitability(cell,k)>=.3)
                {a.AdoptedYear[k]=s.Years+1;a.SourceCulture[k]=source;a.Practice[k]=600;}
                if(!ModernHistory(s)){c.Farming=50;s.History.Cultures[c.Id].Technology[1]=s.Years+1;}
                if(RegionalHistory(s))AdoptRegionalCrops(s,c,source);
                else if(ExperimentalHistory(s))InheritHistoricalCommunity(s,c,source,false);
                else {c.Contacts=new[]{source};h.Cultures[source].Contacts=h.Cultures[source].Contacts.Concat(new[]{c.Id}).OrderBy(x=>x).ToArray();}
                HistoryEvent(s,"farming-community",c.Name+" develops a local farming community through exchange with "+h.Cultures[source].Name+", retaining a distinct identity.",cell,c.Id,category:ChronicleCategory.Society);
            }
        }
    }
}

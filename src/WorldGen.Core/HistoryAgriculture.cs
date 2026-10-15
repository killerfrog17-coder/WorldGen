using System;
using System.Linq;
using System.Collections.Generic;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalAgriculture
    {
        public int CultureId,NativeMask;
        public int[] Practice=new int[6],AdoptedYear=new int[6],SourceCulture=Enumerable.Repeat(-1,6).ToArray();
        [System.Runtime.Serialization.OptionalField] public int[] PracticeCell;
        [System.Runtime.Serialization.OptionalField] public int[] WildOriginCell;
        [System.Runtime.Serialization.OptionalField] public HistoricalCultivar[] Cultivars;
        [System.Runtime.Serialization.OptionalField] public int[] HusbandryYears;
        [System.Runtime.Serialization.OptionalField] public HistoricalAnimalHusbandry[] Animals;
        public HistoricalAgriculture Copy()=>new HistoricalAgriculture{CultureId=CultureId,NativeMask=NativeMask,Practice=(int[])Practice.Clone(),AdoptedYear=(int[])AdoptedYear.Clone(),SourceCulture=(int[])SourceCulture.Clone(),PracticeCell=PracticeCell==null?null:(int[])PracticeCell.Clone(),WildOriginCell=WildOriginCell==null?null:(int[])WildOriginCell.Clone(),Cultivars=Cultivars?.Select(c=>c?.Copy()).ToArray(),HusbandryYears=HusbandryYears==null?null:(int[])HusbandryYears.Clone(),Animals=Animals?.Select(a=>a.Copy()).ToArray()};
    }
    // Replaceable domestication archetypes, NOT a reconstructed inventory of Earth's wild crop species.
    // Storage, growing-season and moisture traits distinguish cultivation from taxable grain surplus.
    public static class HistoricalCrops
    {
        public static readonly string[] Names={"Small-grain cereals","Warm-season cereals","Wetland grain","Root and tuber gardens","Orchard and nut crops","Pulses and legumes"};
        public static readonly int[] Storage={950,900,900,380,550,800};
        public static double Suitability(HabitatCell h,int crop,double freshwater=0)
        {
            double warm=h.WarmTenthsC/10.0,dry=h.WarmSeasonDryPermille/1000.0,moist=h.AridityMilli/1000.0;
            double season=D.Clamp(h.GrowingDays/180.0)*(1-h.PermafrostPermille/1000.0),slope=D.Clamp(1-h.ReliefM/2200.0),salt=D.Clamp(1-h.SalinityMilliPpt/2500.0);
            double water=moist;bool fresh=h.Flooding==FloodRegime.RiverSeasonal||h.Flooding==FloodRegime.RiverPersistent;
            fresh|=freshwater>.2;if(fresh)water=Math.Max(water,.9);
            double q;
            switch(crop)
            {
                case 0:q=D.Clamp((warm-10)/10)*D.Clamp((38-warm)/12)*D.Clamp((water-.15)/.55)*(.35+.65*Math.Max(dry,D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/180.0)));break;
                case 1:q=D.Clamp((warm-16)/10)*D.Clamp((water-.12)/.55)*(.45+.55*Math.Max(dry,.25));break;
                case 2:q=D.Clamp((warm-17)/10)*D.Clamp((water-.6)/.7)*(fresh?1:.65);break;
                case 3:q=D.Clamp((warm-15)/10)*D.Clamp((water-.4)/.6)*D.Clamp((h.ColdTenthsC+30)/150.0);break;
                case 4:q=D.Clamp((warm-10)/12)*D.Clamp((water-.25)/.65)*D.Clamp((h.ColdTenthsC+200)/220.0);break;
                default:q=D.Clamp((warm-10)/12)*D.Clamp((water-.2)/.55)*(.5+.5*Math.Max(dry,.3));break;
            }
            return D.Q(D.Clamp(q*season*slope*salt*(.4+.6*D.Clamp(h.SoilFertilityPermille/650.0))),1000000);
        }
        public static string Description(HistoricalAgriculture a)=>string.Join(", ",Enumerable.Range(0,Names.Length).Where(k=>a.AdoptedYear[k]>0).Select(k=>Names[k]+(a.SourceCulture[k]<0?" (local domestication)":" (introduced)")));
        public static double AbioticPotential(HabitatCell h,double freshwater=0)
        {
            double water=h.AridityMilli/1000.0;
            if(h.Flooding==FloodRegime.RiverSeasonal||h.Flooding==FloodRegime.RiverPersistent||freshwater>.2)water=Math.Max(water,.9);
            return D.Q(D.Clamp(h.GrowingDays/180.0)*(1-h.PermafrostPermille/1000.0)*D.Clamp(1-h.ReliefM/2200.0)*D.Clamp(1-h.SalinityMilliPpt/2500.0)*(.4+.6*D.Clamp(h.SoilFertilityPermille/650.0))*D.Clamp((h.WarmTenthsC/10.0-8)/12)*D.Clamp((water-.1)/.55),1000000);
        }
    }
    public sealed partial class Prehistory
    {
        void InitializeHistoricalAgriculture(PrehistoryState s,CultureIdentity c)
        {
            var a=new HistoricalAgriculture{CultureId=c.Id,NativeMask=NativeCropMask(c.OriginCell)};
            if(RegionalHistory(s)){a.PracticeCell=Enumerable.Repeat(c.OriginCell,6).ToArray();a.WildOriginCell=Enumerable.Range(0,6).Select(k=>(a.NativeMask&(1<<k))!=0?c.OriginCell:-1).ToArray();}
            if(ModernHistory(s)){a.Cultivars=new HistoricalCultivar[6];a.HusbandryYears=new int[6];}
            s.History.Agriculture=s.History.Agriculture.Concat(new[]{a}).ToArray();
        }
        int[] nativeCrops,cropSuitability;double[] wildCropStorage;
        void PrepareWildCrops()
        {
            if(nativeCrops!=null)return;PrepareHistoricalEnvironment();int n=World.Surface.Cells.Length;nativeCrops=new int[n];wildCropStorage=new double[n];cropSuitability=new int[n*6];
            for(int cell=0;cell<n;cell++)
            for(int k=0;k<6;k++)
            {
                double suitable=HistoricalCrops.Suitability(World.Ecology.Habitat[cell],k,riverSites[cell]);int province=World.Ecology.Province[cell];
                cropSuitability[cell*6+k]=(int)Math.Round(suitable*1000000);
                // Regional availability is stable across cells, independent of human arrival and history.
                double occurrence=Rng.Value(World.Recipe.Seed,"crop-wild-province",unchecked(province*31+k));
                if(suitable>.28&&occurrence<.35+.55*suitable){nativeCrops[cell]|=1<<k;wildCropStorage[cell]=Math.Max(wildCropStorage[cell],suitable*HistoricalCrops.Storage[k]/1000.0*.65);}
            }
        }
        int NativeCropMask(int cell){PrepareWildCrops();return nativeCrops[cell];}
        double WildCropStorage(int cell){PrepareWildCrops();return wildCropStorage[cell];}
        double CropSuitability(int cell,int crop){PrepareWildCrops();return cropSuitability[cell*6+crop]/1000000.0;}
        void AdvanceHistoricalAgriculture(PrehistoryState s,CultureIdentity c,SortedSet<(int,int)> contacts,double scale=1)
        {
            var a=s.History.Agriculture[c.Id];int patch=CellPatch[c.OriginCell];
            if(LocalHistory(s)?!HistoricalLocalEffects.AgricultureOpen(s.Years):s.Society.Knowledge[patch]<(RegionalHistory(s)?3500:4500))return;
            if(s.Years-c.FoundedYear<500)return;
            double pressure=D.Clamp(s.Population[patch]/(double)Math.Max(1,Patches[patch].Capacity));
            for(int k=0;k<6;k++)
            {
                if(a.AdoptedYear[k]>0)continue;
                int practiceCell=RegionalHistory(s)?a.PracticeCell[k]:c.OriginCell,practicePatch=CellPatch[practiceCell];
                double suitability=CropSuitability(s,practiceCell,k);if(suitability<(ModernHistory(s)?.02:.2))continue;
                int source=c.Contacts.Where(i=>contacts.Contains((Math.Min(c.Id,i),Math.Max(c.Id,i)))&&s.History.Agriculture[i].AdoptedYear[k]>0&&s.History.Agriculture[i].AdoptedYear[k]-1<s.Years).OrderBy(i=>i).DefaultIfEmpty(-1).First();
                if(ModernHistory(s))source=c.Contacts.Where(i=>contacts.Contains((Math.Min(c.Id,i),Math.Max(c.Id,i)))&&s.History.Agriculture[i].AdoptedYear[k]>0&&s.History.Agriculture[i].AdoptedYear[k]-1<s.Years&&CropTransferFit(s,i,practiceCell,k)>=.25).OrderByDescending(i=>CropTransferFit(s,i,practiceCell,k)).ThenBy(i=>i).DefaultIfEmpty(-1).First();
                bool relayed=false;
                if(source<0&&RegionalHistory(s)&&s.History.RegionalLearning!=null)
                {
                    var learning=s.History.RegionalLearning;int candidate=learning.FarmingSource[practicePatch];
                    int exposure=learning.FarmingExposure[practicePatch];if(ModernHistory(s)){candidate=localCropSource==null?-1:localCropSource[practiceCell];exposure=LocalCropExposure(s,practiceCell);}
                    if(candidate>=0&&candidate!=c.Id&&HistoricalRegionalEffects.CanStudyRegionalCrop(exposure,s.Years,s.History.Agriculture[candidate].AdoptedYear[k],ModernHistory(s)?CropTransferFit(s,candidate,practiceCell,k):suitability)){source=candidate;relayed=true;}
                }
                if(RegionalHistory(s)&&!LocalHistory(s)&&source<0&&s.Society.Knowledge[patch]<4500)continue;
                bool native=(a.NativeMask&(1<<k))!=0;if(!native&&source<0)continue;
                HistoricalCultivar selected=null;
                if(ModernHistory(s))
                {
                    selected=MakeCultivar(s,c.Id,k,source);
                    suitability=source>=0?CropTransferFit(s,source,practiceCell,k):suitability*HistoricalCropCatalog.Fit(CropHabitat(s,practiceCell),selected,CropLatitude(practiceCell),riverSites[practiceCell]);
                    if(suitability<.12)continue;
                }
                int sourceCell=source>=0&&RegionalHistory(s)?s.History.Agriculture[source].PracticeCell[k]:source<0?practiceCell:s.Society.Cultures[source].OriginCell;
                double compatibility=source<0?1:CropCompatibility(practiceCell,sourceCell);
                double localPressure=RegionalHistory(s)?D.Clamp(s.Population[practicePatch]/(double)Math.Max(1,Patches[practicePatch].Capacity)):pressure;
                double practice=(source>=0?22*compatibility:3+9*localPressure)*suitability;
                if(DiffusionHistory(s))practice*=HistoricalFormationEffects.CultivationEffort(World.Ecology.Habitat[practiceCell].Biome,source>=0);
                int gain;
                if(EmergenceHistory(s))
                {
                    // Experimental cultivation can stall; rounding must not invent
                    // one guaranteed unit of progress at every opportunity.
                    double incentive=CultivationIncentive(s,practiceCell,suitability);
                    practice*=source>=0?.2+.8*incentive:HistoricalEmergenceEffects.NativePractice(NativeDomesticability(practiceCell,k),incentive);
                    if(source<0&&(NativeCropMask(practiceCell)&(1<<k))==0)continue;
                    double progress=practice*scale;gain=(int)Math.Floor(progress);
                    if(HistoryRoll("crop-practice-15",c.Id*17+k,s.Years)<progress-gain)gain++;
                }
                else gain=Math.Max(1,(int)Math.Round(practice*scale));
                a.Practice[k]=Math.Min(1000,a.Practice[k]+gain);
                if(a.Practice[k]<600||HistoryRoll("crop-adoption",c.Id*17+k,s.Years)>ElapsedChance((a.Practice[k]-550)/900.0,scale))continue;
                a.AdoptedYear[k]=s.Years+1;a.SourceCulture[k]=source;
                if(ModernHistory(s)){a.Cultivars[k]=selected;InvalidateCultivarFits(c.Id);}
                if(c.Farming==0){c.Farming=50;s.History.Cultures[c.Id].Technology[1]=s.Years+1;}
                HistoryEvent(s,"crop-domestication",c.Name+(source<0?" establishes sustained cultivation of ":" adopts ")+HistoricalCrops.Names[k].ToLowerInvariant()+(source<0?" through local cultivation and selection.":relayed?" through regional exchange of a tradition recorded among "+s.Society.Cultures[source].Name+".":" through exchange with "+s.Society.Cultures[source].Name+"."),practiceCell,c.Id,category:ChronicleCategory.Discoveries);
            }
        }
        double CropYieldFactor(PrehistoryState s,int culture,int cell,bool stored)
        {
            if(!EraHistory(s))return 1;
            var a=s.History.Agriculture[culture];double best=0;int suitable=0;
            for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0)
            {
                double q=CropSuitability(s,cell,k);
                // Generic environmental productivity is already in the local land
                // yield. Apply crop adaptation relative to that baseline, rather
                // than taxing the same soil/slope/season/moisture a second time.
                if(TerritorialHistory(s))q=Math.Min(1.2,q/Math.Max(.01,HistoricalCrops.AbioticPotential(CropHabitat(s,cell),riverSites[cell])));
                if(ModernHistory(s))q*=LocalCultivarFit(s,culture,cell,k)*a.Cultivars[k].YieldPermille/1000.0*CropSoilPenalty(s,culture,cell,k);
                if(q>.3)suitable++;if(stored)q*=CropStorage(s,culture,k)/1000.0;best=Math.Max(best,q);
            }
            return D.Q(Math.Min(1.2,best*(1+.04*Math.Max(0,suitable-1))),1000000);
        }
    }
}

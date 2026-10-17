using System;
using System.Linq;
using System.Reflection;
using WorldGen.Core;

// Exact current-snapshot arithmetic from AdvanceAggregateCulture. It does not
// run the actor or pretend unchanged opportunities persist for future centuries.
public static class HistorySeedAdoptionArithmeticAudit
{
    public static object Inspect(World world,PrehistoryState s,Prehistory engine,int[] cultures)
    {
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,values);
        Array Field(string name)=>(Array)typeof(Prehistory).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine);
        double At(string field,int cell)=>Convert.ToDouble(Field(field).GetValue(cell));
        object Place(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,
            Climate=world.Climate.Classification[cell].ToString(),Biome=world.Ecology.Habitat[cell].Biome.ToString()};
        var h=s.History;var a=h.Aggregates;
        return cultures.Distinct().OrderBy(id=>id).Select(id=>
        {
            var culture=s.Society.Cultures[id];var crop=h.Agriculture[id];var aggregate=a.Cultures[id];
            int economic=(int)Call("DevelopmentEconomicSite",s,id,culture.OriginCell),patch=engine.CellPatch[economic];
            long raw=s.Population[patch],settled=a.Living.Regions[patch].SettledPopulation,capacity=engine.Patches[patch].Capacity;
            double pressure=(raw+settled)/(double)Math.Max(1,capacity),concentration=At("landscapeSeason",economic);
            double wild=D.Clamp(At("ruralWeight",economic)/Math.Max(1,At("inhabitableArea",economic)*150));
            var memory=a.Provinces.Exchanges[id];int contact=-1;
            if(memory.Peers.Length>0)
            {
                int start=(int)((double)Call("HistoryRoll","environment-recent-contact",id,s.Years)*memory.Peers.Length);
                for(int offset=0;offset<memory.Peers.Length;offset++){int at=(start+offset)%memory.Peers.Length;if(s.Years-memory.LastYear[at]<=250){contact=memory.Peers[at];break;}}
            }
            bool cultivationContact=contact>=0&&s.Society.Cultures[contact].Farming>=250;
            var groups=Enumerable.Range(0,6).Select(group=>
            {
                int cell=crop.PracticeCell[group],donor=(int)Call("DevelopmentCropDonor",s,id,group,cell,contact);
                bool native=(crop.NativeMask&(1<<group))!=0;
                var selected=donor>=0?h.Agriculture[donor].Cultivars?[group]?.Copy():native?(HistoricalCultivar)Call("AggregateCultivar",s,id,group,cell):null;
                var nativeCandidate=native?(HistoricalCultivar)Call("AggregateCultivar",s,id,group,cell):null;
                double nativeFit=nativeCandidate!=null?(double)Call("CommunityTransferFit",s,cell,nativeCandidate,false):0;
                int bestDated=-1,bestDatedReceipt=-1;double bestDatedFit=0;
                foreach(int peer in culture.Contacts.Concat(new[]{contact}).Where(peer=>peer>=0&&peer!=id).Distinct())
                {
                    int at=Array.IndexOf(memory.Peers,peer);var offered=h.Agriculture[peer];
                    if(at<0||s.Years-memory.LastYear[at]>250||offered.AdoptedYear[group]<=0||offered.AdoptedYear[group]-1>memory.LastYear[at]||offered.Cultivars?[group]==null)continue;
                    double candidateFit=(double)Call("CommunityTransferFit",s,cell,offered.Cultivars[group],false);
                    if(candidateFit>bestDatedFit){bestDated=peer;bestDatedFit=candidateFit;bestDatedReceipt=memory.LastYear[at];}
                }
                double fit=selected!=null?(double)Call("CommunityTransferFit",s,cell,selected,false):0;
                double localAdvantage=HistoricalEmergenceEffects.CultivationIncentive(pressure,fit,concentration,wild);
                double nativeDomesticability=(double)Call("NativeDomesticability",cell,group);
                double nativeFactor=donor>=0?1:cultivationContact?.1+.9*Math.Pow(nativeDomesticability,2):.02+.98*Math.Pow(nativeDomesticability,3);
                double donorMultiplier=donor>=0?65:cultivationContact?60:24;
                double landscapeYield=At("landscapeYield",cell),clearing=Math.Max(.015,D.Clamp(landscapeYield/.5));
                double growing=HistoricalDevelopmentEffects.GrowingSeason(world.Ecology.Habitat[cell]);
                double passage=donor>=0?4:1;
                bool unadopted=crop.AdoptedYear[group]<=0,age=s.Years-culture.FoundedYear>=500;
                string block=!unadopted?"Already adopted: introduction branch skipped":!age?"Culture younger than500":donor<0&&!native?"No admissible dated donor and no native crop mask":selected==null?"No selected cultivar":fit<.12?"Physiological fit below.12":null;
                double perYear=block==null?donorMultiplier/50.0*localAdvantage*fit*nativeFactor*clearing*passage*growing:0;
                double pending=Math.Max(0,s.Years-aggregate.LastYear),remaining=Math.Max(0,600-crop.Practice[group]);
                double? years=perYear>0?remaining/perYear:(double?)null;
                double? withoutClearing=perYear>0?remaining/(perYear/clearing):(double?)null;
                int donorAt=donor>=0?Array.IndexOf(memory.Peers,donor):-1;
                return new{Group=group,PracticePlace=Place(cell),Practice=crop.Practice[group],AdoptedYear=crop.AdoptedYear[group],SourceCulture=crop.SourceCulture[group],NativeAvailable=native,
                    CurrentAdmissibleDonor=donor,DonorName=donor>=0?s.Society.Cultures[donor].Name:null,DonorAdoptedYear=donor>=0?h.Agriculture[donor].AdoptedYear[group]:0,
                    DonorReceiptYear=donorAt>=0?memory.LastYear[donorAt]:-1,DonorDirectReceiptYear=donorAt>=0?memory.LastDirectYear[donorAt]:-1,
                    Selected=selected,Fit=fit,NativeCandidate=nativeCandidate,NativeCandidateFit=nativeFit,
                    BestDatedCandidateEvenBelowFitGate=bestDated,BestDatedFitEvenBelowGate=bestDatedFit,BestDatedReceipt=bestDatedReceipt,
                    PracticeFieldWarmth=world.Ecology.Habitat[cell].WarmTenthsC,PracticeFieldGrowingDays=world.Ecology.Habitat[cell].GrowingDays,
                    Block=block,PendingElapsed=pending,DonorMultiplier=donorMultiplier,LocalAdvantage=localAdvantage,
                    NativeDomesticability=nativeDomesticability,NativeFactor=nativeFactor,WholeCellLandscapeYield=landscapeYield,ClearingFactor=clearing,
                    SeasonalGrowingFactor=growing,IntroducedPassageFactor=passage,ExpectedPracticePerCalendarYear=perYear,
                    ExpectedGainAtPendingElapsed=perYear*pending,ExpectedGainPer100Years=perYear*100,
                    CurrentPracticeTo600ExpectedYearsIfConditionsUnchanged=years,
                    ClearingFactorOnlyRemovedExpectedYearsSensitivity=withoutClearing,
                    EndAdoptionRollProbabilityPerOpportunity=.2};
            }).ToArray();
            return new{Culture=id,culture.Name,culture.FoundedYear,Age=s.Years-culture.FoundedYear,culture.Farming,aggregate.LastYear,
                CultureRegion=aggregate.Region,EconomicPlace=Place(economic),EconomicRegion=patch,RawResidents=raw,SettledResidents=settled,
                StaticPatchCapacity=capacity,PressureUsedByActualActor=pressure,ActualContactResidents=(long)Call("ProvincialContactResidents",s,patch,false),
                Concentration=concentration,ForagingAbundance=wild,CurrentMemorySelectedContact=contact,CultivationContact=cultivationContact,
                RegionalTeacher=aggregate.Region>=0?a.Living.Regions[aggregate.Region].Teacher:-1,
                RegionalLastReceipt=aggregate.Region>=0?a.Living.Regions[aggregate.Region].LastExchangeYear:-1,Groups=groups,
                Caveat="Current-snapshot formula, not an actor replay. Does not perform LivingContact's possible new regional knowledge offer or StudyDevelopmentCropSites' possible native-site update. Year estimate is conditional expectation to600 practice only, not a promised adoption date; final20% roll remains."};
        }).ToArray();
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using WorldGen.Core;

// Sparse detached-probe evidence only. No exchange/adoption handler is called,
// no hypothetical seed offer is recorded, and no source profile is backdated.
public static class HistoryCropRelayTimelineAudit
{
    public static object Inspect(World world,PrehistoryState s,Prehistory engine)
    {
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,values);
        var h=s.History;var a=h.Aggregates;
        int[] fields={176034,163381,177775,179194,172937};
        object Place(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,
            Climate=world.Climate.Classification[cell].ToString(),Biome=world.Ecology.Habitat[cell].Biome.ToString(),
            WarmTenthsC=world.Ecology.Habitat[cell].WarmTenthsC,GrowingDays=world.Ecology.Habitat[cell].GrowingDays,
            SpringTenthsC=world.Climate.NorthernSpring.TemperatureTenthsC[cell],SummerTenthsC=world.Climate.NorthernSummer.TemperatureTenthsC[cell],
            AutumnTenthsC=world.Climate.NorthernAutumn.TemperatureTenthsC[cell],WinterTenthsC=world.Climate.NorthernWinter.TemperatureTenthsC[cell]};
        double Fit(HistoricalCultivar profile,int cell)=>profile==null?0:(double)Call("CommunityTransferFit",s,cell,profile,false);
        object Offer(int sender,int recipient,int group,int site)
        {
            var crop=h.Agriculture[sender];var profile=crop.Cultivars?[group];
            var memory=recipient>=0?a.Provinces.Exchanges[recipient]:null;
            int at=memory==null?-1:Array.IndexOf(memory.Peers,sender),receipt=at>=0?memory.LastYear[at]:-1;
            bool adopted=crop.AdoptedYear[group]>0,existsAtReceipt=adopted&&receipt>=crop.AdoptedYear[group]-1;
            double fit=Fit(profile,site);
            string block=!adopted?"Sender has not adopted group":profile==null?"No sender cultivar":recipient<0?"Anonymous recipient: no culture receipt ledger":at<0?"No dated sender receipt":s.Years-receipt>250?"Receipt older than250":!existsAtReceipt?"Sender adoption after receipt":fit<=.12?"Sender profile fit not above.12":null;
            return new{Sender=sender,Recipient=recipient,Group=group,SenderAdoptedYear=crop.AdoptedYear[group],SenderPractice=crop.Practice[group],
                SenderSourceCulture=crop.SourceCulture[group],SenderCropId=profile?.CropId,ProfileOriginCulture=profile?.OriginCulture,
                ProfileParentCulture=profile?.ParentCulture,ProfileFitNowAtTarget=fit,
                LastReceipt=receipt,LastDirectReceipt=at>=0?memory.LastDirectYear[at]:-1,ReceivedKnowledgeBits=at>=0?memory.Capabilities[at]:0,
                SenderCropExistedAtReceipt=existsAtReceipt,CurrentProfileMayHaveChangedSinceReceipt=profile!=null&&receipt<s.Years,
                ActualDonorGateBlock=block};
        }
        var regions=fields.Select(cell=>engine.CellPatch[cell]).Distinct().ToArray();
        var regionalRows=new List<object>();var cultures=new HashSet<int>(new[]{14,65,51,234,269,517,617}.Where(id=>id<s.Society.Cultures.Length));
        foreach(int region in regions)
        {
            var r=a.Regions[region];int recipient=r.Culture;var local=a.Living.Regions[region];
            if(recipient>=0)cultures.Add(recipient);if(local.Teacher>=0)cultures.Add(local.Teacher);
            var neighbors=new List<object>();
            foreach(int routeId in r.Routes.OrderBy(id=>id))
            {
                var route=engine.Routes[routeId];int other=route.A==region?route.B:route.A;
                int donor=(int)Call("DevelopmentRegionalTeacher",s,other);
                int from=(int)Call("RegionalContactCell",s,route,other),to=(int)Call("RegionalContactCell",s,route,region);
                if(from<0||to<0)continue;
                var upstream=a.Living.Regions[other];int sourceLocal=a.Regions[other].Culture;
                if(donor>=0)cultures.Add(donor);if(sourceLocal>=0)cultures.Add(sourceLocal);if(upstream.Teacher>=0)cultures.Add(upstream.Teacher);
                bool allowed=donor>=0&&(bool)Call("InstitutionalRouteAllowed",s,from,to,donor);
                double transmission=donor>=0?(double)Call("EnvironmentalTransmission",s,from,to,donor,route.Sea):0;
                neighbors.Add(new{Route=routeId,Sea=route.Sea,SourceRegion=other,SourceLocalCulture=sourceLocal,SourceRetainedTeacher=upstream.Teacher,
                    SourceTeacherExposure=upstream.Exposure,SourceReceipt=upstream.LastExchangeYear,ChosenRegionalDonor=donor,
                    SourceLocalKnowledgeCount=sourceLocal>=0?(int)Call("CurrentProvincialKnowledgeCount",s,sourceLocal):0,
                    RetainedTeacherKnowledgeCount=upstream.Teacher>=0?(int)Call("CurrentProvincialKnowledgeCount",s,upstream.Teacher):0,
                    SourceRaw=s.Population[other],SourceContactResidents=(long)Call("ProvincialContactResidents",s,other,false),
                    SourceRegionLiveTownCount=engine.Patches[other].Cells.Count(cell=>a.TownAt[cell]>=0&&a.Living.Cities[a.TownAt[cell]].AbandonedYear<0),
                    SourceBestCell=Place(a.Regions[other].BestCell),Departure=Place(from),Arrival=Place(to),
                    SourceTown=a.TownAt[from],ArrivalTown=a.TownAt[to],ActualPhysicalGateAllowed=allowed,EnvironmentalTransmission=transmission,
                    SourceRegionalKnowledgeBits=a.Provinces.RegionalKnowledge[other],
                    ActualFilteredKnowledgePackage=donor>=0?(long)Call("ProvincialRegionalKnowledge",s,other,donor):0,
                    ChosenDonorOffers=donor>=0?Enumerable.Range(0,6).Select(group=>Offer(donor,recipient,group,recipient>=0?h.Agriculture[recipient].PracticeCell[group]:to)).ToArray():Array.Empty<object>(),
                    // An actual retained teacher is evidence of a knowledge chain,
                    // not proof that its seeds were physically held or delivered.
                    RetainedTeacherOffers=upstream.Teacher>=0&&upstream.Teacher!=donor?Enumerable.Range(0,6).Select(group=>Offer(upstream.Teacher,recipient,group,to)).ToArray():Array.Empty<object>()});
            }
            regionalRows.Add(new{Region=region,Recipient=recipient,AreaKm2=r.AreaKm2,AggregateCapacity=r.Capacity,
                StaticPatchCapacity=engine.Patches[region].Capacity,PatchCellCount=engine.Patches[region].Cells.Length,
                Raw=s.Population[region],ContactResidents=(long)Call("ProvincialContactResidents",s,region,false),
                CurrentTeacher=local.Teacher,CurrentExposure=local.Exposure,CurrentReceipt=local.LastExchangeYear,
                FixedFields=fields.Where(cell=>engine.CellPatch[cell]==region).Select(Place).ToArray(),Neighbors=neighbors});
        }
        // The focus and its actual peers, then only their actual one-hop receipt
        // ledgers. No global search for a convenient remote seed profile.
        // Do not recursively expand a world-wide peer graph. Each selected
        // culture's own ledger identifies upstream senders with compact fits.
        var cultureRows=cultures.OrderBy(id=>id).Select(id=>
        {
            var culture=s.Society.Cultures[id];var crop=h.Agriculture[id];var memory=a.Provinces.Exchanges[id];
            var targetSites=fields.Concat(new[]{culture.OriginCell}).Distinct().ToArray();
            return new{Culture=id,culture.Name,culture.FoundedYear,culture.Farming,Origin=Place(culture.OriginCell),TownCount=a.Cultures[id].Cities.Count,
                Region=a.Cultures[id].Region,OwnGroups=Enumerable.Range(0,6).Select(group=>new{Group=group,AdoptedYear=crop.AdoptedYear[group],
                    Practice=crop.Practice[group],SourceCulture=crop.SourceCulture[group],Native=(crop.NativeMask&(1<<group))!=0,
                    PracticePlace=Place(crop.PracticeCell[group]),GlobalProfile=crop.Cultivars?[group],
                    FitsAtFixedFieldsNow=targetSites.Select(site=>new{Cell=site,Fit=Fit(crop.Cultivars?[group],site)}).ToArray()}).ToArray(),
                ActualReceivedPeers=memory.Peers.Select((peer,index)=>new{Sender=peer,LastYear=memory.LastYear[index],LastDirectYear=memory.LastDirectYear[index],
                    KnowledgeBits=memory.Capabilities[index],SenderFarming=s.Society.Cultures[peer].Farming,
                    SenderGroupAdoptedYears=h.Agriculture[peer].AdoptedYear,
                    SenderGroupSourceCultures=h.Agriculture[peer].SourceCulture,
                    SenderFitsNowAtRecipientPractice=Enumerable.Range(0,6).Select(group=>Fit(h.Agriculture[peer].Cultivars?[group],crop.PracticeCell[group])).ToArray()}).ToArray()};
        }).ToArray();
        return new{Regions=regionalRows,Cultures=cultureRows,Notes=new[]{
            "Metadata reads on detached probe only; caller must normalize only instrumented read counter and require original published hash.",
            "Actual receipt dates and sender adoption are separate. Current profile at an old receipt is NOT treated as a reconstructed historical phenotype.",
            "Retained-teacher alternative fits establish a possible representation gap only, not a paid seed delivery or inventory that the simulation never recorded.",
            "Regional adjacency does not imply that a sampled route was selected or succeeded in that interval. NoTown endpoints are reported explicitly."}};
    }
}

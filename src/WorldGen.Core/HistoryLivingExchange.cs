using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        int LivingCommunitySite(PrehistoryState s,int region)
        {
            var r=s.History.Aggregates.Regions[region];if(HistoricalCells.Culture(s,r.BestCell)<0&&s.History.Aggregates.TownAt[r.BestCell]<0)return r.BestCell;
            int best=-1;double score=-1;var cells=Patches[region].Cells;
            for(int k=0;k<16;k++){int cell=cells[(int)(HistoryRoll("living-community-site-"+k,region,s.Years)*cells.Length)];if(HistoricalCells.Culture(s,cell)>=0||s.History.Aggregates.TownAt[cell]>=0)continue;double value=landscapeStored[cell]+wildCropStorage[cell]*LandscapeCropFactor(cell);if(value>score){score=value;best=cell;}}return best;
        }
        void LivingCultureContact(PrehistoryState s,int first,int second)
        {
            if(first<0||second<0||first==second)return;
            foreach(var pair in new[]{(first,second),(second,first)})
            {var c=s.Society.Cultures[pair.Item1];if(!c.Contacts.Contains(pair.Item2))c.Contacts=c.Contacts.Concat(new[]{pair.Item2}).TakeLast(16).ToArray();if(ProvincialHistory(s))OfferProvincialKnowledge(s,pair.Item1,pair.Item2,ProvincialKnowledge(s.History.Cultures[pair.Item2]),s.Years,true);}
            ShareCoastCharts(s,first,second);
        }
        void AdvanceLivingRegionalExchange(PrehistoryState s,int id,int elapsed)
        {
            int contacts=Math.Min(12,s.History.Aggregates.Regions[id].Routes.Length);
            for(int trial=0;trial<contacts;trial++)TryLivingRegionalExchange(s,id,elapsed,trial);
        }
        void TryLivingRegionalExchange(PrehistoryState s,int id,int elapsed,int trial)
        {
            var a=s.History.Aggregates;var region=a.Regions[id];var local=a.Living.Regions[id];
            if(region.Routes.Length==0||(ProvincialHistory(s)?ProvincialContactResidents(s,id):s.Population[id]+local.SettledPopulation)<100)return;
            int routeId=region.Routes[(int)(HistoryRoll("living-knowledge-route-"+trial,id,s.Years)*region.Routes.Length)];var route=Routes[routeId];int other=route.A==id?route.B:route.A;
            int donor=ConnectedHistory(s)?DevelopmentRegionalTeacher(s,other):a.Regions[other].Culture;
            // Itinerant exchange can cross inhabited, decentralized communities;
            // a missing colored culture must not be an impermeable knowledge wall.
            if(donor<0&&s.Population[other]+a.Living.Regions[other].SettledPopulation>=100&&a.Living.Regions[other].Exposure>=100)donor=a.Living.Regions[other].Teacher;
            if(donor<0)return;var hc=s.History.Cultures[donor];
            double km=route.DistanceM/1000.0;
            if(route.Sea&&(km>(HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?1800:HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)?220:40)))return;
            var from=World.Ecology.Habitat[a.Regions[other].BestCell];var to=World.Ecology.Habitat[region.BestCell];
            double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(from,from,to);
            double distance=km+Math.Min(450,Patches[other].RadiusKm+Patches[id].RadiusKm);
            double barrier=1+region.ReliefM/1200.0+(to.AridityMilli<180?3:0)+(to.PermafrostPermille>300?3:0);
            double transport=HistoricalModernEffects.LandTransport(hc,s.Years)*(ConnectedHistory(s)?Math.Sqrt(HistoricalModernEffects.Communication(hc,s.Years)):1);
            double transit=(.15+.85*continuity)*elapsed*transport/Math.Max(80,60+distance*barrier*.3);
            if(HistoryRoll("living-knowledge-exchange-"+trial,id,s.Years)>1-Math.Exp(-transit))return;
            if(!ConnectedHistory(s)||local.Teacher<0||(ProvincialHistory(s)?CurrentProvincialKnowledgeCount(s,local.Teacher)<=CurrentProvincialKnowledgeCount(s,donor):s.History.Cultures[local.Teacher].Technology.Count(y=>y>0)<=hc.Technology.Count(y=>y>0)))local.Teacher=donor;
            local.Exposure=Math.Min(1000,local.Exposure+Math.Max(10,(int)(elapsed*(.15+.85*continuity))));local.LastExchangeYear=s.Years;
            long package=ProvincialHistory(s)?ProvincialRegionalKnowledge(s,other,donor):0;
            if(ProvincialHistory(s))a.Provinces.RegionalKnowledge[id]|=package;
            var donorRegion=a.Living.Regions[other];int court=donorRegion.CivicTeacher;
            if(ProvincialHistory(s))court=ProvincialRegionalCourt(s,other)>=0?ProvincialRegionalCourt(s,other):court;
            if(!ProvincialHistory(s)&&a.Cultures[donor].Cities.Count>0)
            {int town=a.Cultures[donor].Cities.At((int)(HistoryRoll("living-institution-example",id,s.Years)*a.Cultures[donor].Cities.Count));int ruler=a.Resolve(s.Society.Settlements[town].PolityId,s.History);if(ruler>=0)court=ruler;}
            if(court>=0)
            {
                local.CivicTeacher=court;
                double inherited=a.Cultures[donor].CivilizationSince>=0?1:D.Clamp(donorRegion.CivicExposure/700.0)*.7;
                local.CivicExposure=Math.Min(1000,local.CivicExposure+(int)(elapsed*(.2+.8*continuity)*inherited));
            }
            if(region.Culture>=0)
            {
                if(ProvincialHistory(s))OfferProvincialKnowledge(s,region.Culture,donor,package,s.Years,false);
                else LivingCultureContact(s,region.Culture,donor);
            }
        }
        double LivingCommunityChance(PrehistoryState s,int id,int elapsed)
        {
            var a=s.History.Aggregates;var r=a.Regions[id];var local=a.Living.Regions[id];int cell=r.BestCell;var h=World.Ecology.Habitat[cell];
            bool coast=World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]);
            double concentration=Math.Max(ConnectedHistory(s)?.35+.35*landscapeSeason[cell]:landscapeSeason[cell],Math.Max(riverAccess[cell]*3,coast?.65:0));
            double stored=Math.Max(ConnectedHistory(s)?developmentStored[cell]:landscapeStored[cell],wildCropStorage[cell]*LandscapeCropFactor(cell)*(ConnectedHistory(s)?HistoricalDevelopmentEffects.GrowingSeason(h):1));
            double aquatic=Math.Max(riverAccess[cell],coast?.55:0);
            double ecological=HistoricalEmergenceEffects.EcologicalConcentration(h,concentration,stored,aquatic);
            double occupied=HistoricalLocalEffects.RegionalTraditionOpportunity(s.Years-s.ArrivalYear[id]);
            double independent=.0025*Math.Pow(D.Clamp(Math.Max(stored,aquatic*.65)/.45),2)*ecological*occupied;
            double diffusion=local.Teacher>=0?.03*D.Clamp(local.Exposure/600.0)*(.02+.98*ecological)*D.Clamp(concentration):0;
            if(ColonialHistory(s)&&local.Teacher>=0)
            {
                // Recognition of an actually contacted regional tradition must
                // not take another ten millennia. Ecological/crop compatibility
                // still governs livelihoods and state formation separately.
                var teacher=s.History.Cultures[local.Teacher];
                diffusion*=8*(1+HistoricalModernEffects.Adoption(teacher,HistoricalTechnology.Printing,s.Years)+2*HistoricalModernEffects.Adoption(teacher,HistoricalTechnology.Telecommunications,s.Years));
            }
            double maturity=ColonialHistory(s)?CulturalRecognitionMaturity(s,id):1;
            return 1-Math.Exp(-(independent+diffusion)*maturity*elapsed/100.0);
        }
        int LivingContact(PrehistoryState s,int culture,int fallback)
        {
            var c=s.Society.Cultures[culture];var a=s.History.Aggregates;int region=a.Cultures[culture].Region;
            if(fallback>=0&&fallback!=culture)LivingCultureContact(s,culture,fallback);
            if(region>=0&&a.Living.Regions[region].Teacher>=0)
            {
                int teacher=a.Living.Regions[region].Teacher;
                if(ProvincialHistory(s))OfferProvincialKnowledge(s,culture,teacher,ProvincialRegionalKnowledge(s,region,teacher),a.Living.Regions[region].LastExchangeYear,false);
                else LivingCultureContact(s,culture,teacher);
            }
            if(c.Contacts.Length==0)return fallback;
            return c.Contacts[(int)(HistoryRoll("living-retained-contact",culture,s.Years)*c.Contacts.Length)];
        }
        void ExpandLivingCulture(PrehistoryState s,int id,int elapsed)
        {
            var a=s.History.Aggregates;var ca=a.Cultures[id];var c=s.Society.Cultures[id];int origin=c.OriginCell;
            // A bounded number of frontier opportunities represents physical
            // travel, not one entire cell per culture regardless of resolution.
            int attempts=ConnectedHistory(s)?HistoricalDevelopmentEffects.FrontierBudget(s.History.Cultures[id],s.Years,ca.CityCount):Math.Min(12,Math.Max(1,(int)Math.Round(elapsed/12.0)));
            if(ProvincialHistory(s)){double expected=attempts*elapsed/40.0;attempts=(int)expected;if(HistoryRoll("provincial-frontier-fraction",id,s.Years)<expected-attempts)attempts++;}
            for(int trial=0;trial<attempts;trial++)
            {
                int target=LocalFrontierTarget(s,ca.Frontier,id,"living-cultural-front-"+trial,0);if(target<0)continue;int other=HistoricalCells.Culture(s,target);
                if(other>=0){LivingCultureContact(s,id,other);continue;}
                if(s.Population[CellPatch[target]]<100)continue;
                var local=World.Ecology.Habitat[target];int from=origin;
                if(ConnectedHistory(s))foreach(int neighbor in World.Surface.Cells[target].Neighbors)if(HistoricalCells.Culture(s,neighbor)==id){from=neighbor;break;}
                double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[ConnectedHistory(s)?from:origin],World.Ecology.Habitat[from],local);
                double adaptable=HistoricalTechnologies.Has(s.History.Cultures[id],HistoricalTechnology.CropRotation)?.15:.02;
                if(ConnectedHistory(s))adaptable=Math.Min(.7,.08+.12*HistoricalModernEffects.LandTransport(s.History.Cultures[id],s.Years));
                double chance=(.2+Math.Min(.5,c.Farming/1500.0))*(adaptable+(1-adaptable)*continuity);
                if(HistoryRoll("living-cultural-claim-"+trial,id,s.Years)<chance)
                {SetAggregateLayer(s,target,0,id);SetAggregateLayer(s,target,2,s.History.Cultures[id].ReligionId);SetAggregateLayer(s,target,4,1);if(ConnectedHistory(s))StudyDevelopmentCropSites(s,id,target);int region=CellPatch[target];if(a.Regions[region].Culture<0){var r=a.Living.Regions[region];r.Teacher=id;r.Exposure=Math.Min(1000,r.Exposure+100);}}
            }
        }
    }
}

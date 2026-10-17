using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        int[] developmentCoast;
        Route[][] developmentVoyages;
        void PrepareDevelopmentShores()
        {
            developmentCoast=Enumerable.Repeat(-1,Patches.Length).ToArray();developmentVoyages=new Route[Patches.Length][];
            // Static setup only: one shore representative per inhabited-capable
            // region, not a recurring scan for ports inside a culture's holdings.
            for(int id=0;id<Patches.Length;id++)
            {
                double best=-1;
                foreach(int cell in Patches[id].Cells)
                {
                    if(!Patches[id].Island&&!World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]))continue;
                    double value=inhabitableArea[cell]/(1+World.Ecology.Habitat[cell].ReliefM/300.0);
                    if(value>best){best=value;developmentCoast[id]=cell;}
                }
            }
            int[] coastal=Enumerable.Range(0,Patches.Length).Where(r=>developmentCoast[r]>=0&&Patches[r].Capacity>=25).ToArray();
            if(coastal.Length==0){for(int i=0;i<Patches.Length;i++)developmentVoyages[i]=Array.Empty<Route>();return;}
            var locator=new CellLocator(coastal.Select(r=>World.Surface.Cells[developmentCoast[r]]).ToArray());
            for(int id=0;id<Patches.Length;id++)
            {
                int from=developmentCoast[id];if(from<0){developmentVoyages[id]=Array.Empty<Route>();continue;}
                V point=World.Surface.Cells[from].Position,east=new V(-point.Y,point.X,0).Unit,north=V.Cross(point,east).Unit;
                var candidates=new SortedSet<int>();
                // Bounded directional probes find nearby shores/island steps and
                // distant coastlines without building an all-pairs ocean graph.
                foreach(int km in new[]{200,600,1400,3000,6500,11000})for(int direction=0;direction<12;direction++)
                {
                    double bearing=direction*Math.PI/6,angle=km/World.Recipe.RadiusKm;
                    V probe=point*Math.Cos(angle)+(east*Math.Cos(bearing)+north*Math.Sin(bearing))*Math.Sin(angle);
                    int other=coastal[locator.Nearest(probe)];if(other!=id)candidates.Add(other);
                }
                var options=candidates.Select(other=>new Route{A=id,B=other,CellA=from,CellB=developmentCoast[other],Sea=true,
                    DistanceM=(int)Math.Round(D.Angle(point,World.Surface.Cells[developmentCoast[other]].Position)*World.Recipe.RadiusKm*1000)})
                    .Where(r=>r.DistanceM>=30000&&r.DistanceM<=14000000&&AggregateSeaLane(r.CellA,r.CellB)).OrderBy(r=>r.DistanceM).ThenBy(r=>r.B).ToArray();
                var chosen=new List<Route>();
                foreach(var route in options.GroupBy(r=>World.Landmass[r.CellB]).Select(g=>g.First()).OrderBy(r=>r.DistanceM).Take(8))chosen.Add(route);
                foreach(var route in options)if(chosen.Count<16&&!chosen.Any(r=>r.B==route.B))chosen.Add(route);
                developmentVoyages[id]=chosen.OrderBy(r=>r.DistanceM).ThenBy(r=>r.B).ToArray();
            }
        }
        int DevelopmentRegionalTeacher(PrehistoryState s,int region)
        {
            var a=s.History.Aggregates;int local=a.Regions[region].Culture,teacher=a.Living.Regions[region].Teacher;
            if(ProvincialHistory(s))
            {
                int court=ProvincialRegionalCourt(s,region),ruler=court<0?-1:s.History.Polities[court].CultureId;
                if(ruler>=0&&(local<0||CurrentProvincialKnowledgeCount(s,ruler)>CurrentProvincialKnowledgeCount(s,local)))local=ruler;
            }
            if(teacher<0||a.Living.Regions[region].Exposure<100)return local;
            if(local<0)return teacher;
            return (ProvincialHistory(s)?CurrentProvincialKnowledgeCount(s,teacher)>CurrentProvincialKnowledgeCount(s,local):s.History.Cultures[teacher].Technology.Count(y=>y>0)>s.History.Cultures[local].Technology.Count(y=>y>0))?teacher:local;
        }
        void AdvanceDevelopmentVoyage(PrehistoryState s,int region,int elapsed)
        {
            if(CommunityHistory(s)){AdvanceCommunityVoyage(s,region,elapsed);return;}
            var h=s.History;var a=h.Aggregates;long residents=s.Population[region];int culture=DevelopmentRegionalTeacher(s,region);
            int sourceState=-1;
            if(ProvincialHistory(s)&&developmentCoast[region]>=0)
            {
                int court=HistoricalCells.Polity(s,developmentCoast[region]);
                if(court>=0&&a.Polities[court].Remote>=250)
                {
                    if(residents<250)sourceState=court;
                    int ruler=h.Polities[court].CultureId;if(culture<0||CurrentProvincialKnowledgeCount(s,ruler)>CurrentProvincialKnowledgeCount(s,culture))culture=ruler;
                    if(sourceState>=0)residents=a.Polities[court].Remote;
                }
            }
            if(culture<0||residents<250||developmentVoyages[region].Length==0)return;
            var hc=h.Cultures[culture];bool sailing=HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing);if(!sailing)return;
            double range=HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years,ColonialHistory(s));var options=developmentVoyages[region];
            int offset=(int)(HistoryRoll("development-expedition",region,s.Years)*options.Length);
            for(int trial=0;trial<Math.Min(4,options.Length);trial++)
            {
                var route=options[(offset+trial)%options.Length];if(route.DistanceM>range*1000)continue;
                int to=route.B;var target=a.Living.Regions[to];int peer=DevelopmentRegionalTeacher(s,to);
                double ships=HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation)?1:HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?.35:.08;
                double chance=ships*Math.Exp(-route.DistanceM/(range*700))*Math.Min(3,Math.Sqrt(residents/1000.0));
                if(HistoryRoll("development-sea-contact-"+trial,region,s.Years)>1-Math.Exp(-chance*elapsed/50.0))continue;
                if(peer>=0&&peer!=culture)LivingCultureContact(s,culture,peer);
                if(target.Teacher<0||(ProvincialHistory(s)?CurrentProvincialKnowledgeCount(s,target.Teacher)<=CurrentProvincialKnowledgeCount(s,culture):h.Cultures[target.Teacher].Technology.Count(y=>y>0)<=hc.Technology.Count(y=>y>0)))target.Teacher=culture;
                target.Exposure=Math.Min(1000,target.Exposure+Math.Max(100,elapsed*2));target.LastExchangeYear=s.Years;
                if(ProvincialHistory(s))a.Provinces.RegionalKnowledge[to]|=ProvincialKnowledge(hc);
                int court=a.Living.CivicOrigins[culture];if(court>=0){target.CivicTeacher=court;target.CivicExposure=Math.Min(1000,target.CivicExposure+elapsed*2);}
                long carrying=Patches[to].Capacity;
                if(s.Society.Cultures[culture].Farming>=180)carrying=(long)(carrying*(1+s.Society.Cultures[culture].Farming/80.0*CarriedCropLandscape(s,culture,route.CellB,false,-1,route.CellA)*HistoricalModernEffects.FoodMultiplier(hc,s.Years)));
                int destinationState=ProvincialHistory(s)?HistoricalCells.Polity(s,route.CellB):-1;
                long destinationPopulation=destinationState>=0?a.Polities[destinationState].Remote:s.Population[to];
                if(destinationState>=0)carrying=ProvincialRemoteCapacity(s,destinationState);
                if(destinationState>=0&&destinationState==sourceState)continue;
                long migrants=Math.Min(residents/20,Math.Min(1000,Math.Max(0,carrying-destinationPopulation)));
                if(migrants<50)continue;
                long before=s.Population[to];
                if(sourceState>=0){a.Polities[sourceState].Remote-=migrants;h.Polities[sourceState].Population-=migrants;s.Society.Cultures[h.Polities[sourceState].CultureId].Population=Math.Max(0,s.Society.Cultures[h.Polities[sourceState].CultureId].Population-migrants);}
                else s.Population[region]-=migrants;
                residents-=migrants;
                if(destinationState>=0){a.Polities[destinationState].Remote+=migrants;h.Polities[destinationState].Population+=migrants;s.Society.Cultures[h.Polities[destinationState].CultureId].Population+=migrants;}
                else s.Population[to]+=migrants;
                if(s.ArrivalYear[to]<0)s.ArrivalYear[to]=s.Years;
                for(int g=0;g<8;g++)s.Ancestry[to*8+g]=(int)((s.Ancestry[to*8+g]*before+s.Ancestry[region*8+g]*migrants)/(before+migrants));
                if(a.Regions[to].Culture<0&&HistoricalCells.Culture(s,route.CellB)<0&&s.Population[to]>=400)
                {
                    FoundAggregateCulture(s,to,route.CellB,culture);int child=s.Society.Cultures.Length-1;
                    InheritDevelopmentCommunityFrom(s,child,culture,route.CellB,route.CellA,-1);
                    HistoryEvent(s,"overseas-community",s.Society.Cultures[culture].Name+" voyagers establish a community on another shore, carrying crops and practical knowledge across approximately "+route.DistanceM/1000+" km of sea.",route.CellB,child,category:ChronicleCategory.Society);
                }
                break;
            }
        }
        void InheritDevelopmentCommunity(PrehistoryState s,int child,int parent,int site)
            =>InheritDevelopmentCommunityFrom(s,child,parent,site,-1,-1);
        void InheritDevelopmentCommunityFrom(PrehistoryState s,int child,int parent,int site,int sourceCell,int sourceTown)
        {
            var h=s.History;var hc=h.Cultures[child];hc.ParentCultureId=parent;hc.LanguageId=h.Cultures[parent].LanguageId;
            InheritLanguage(s,child,parent);
            if(ReligiousInstitutionHistory(s))InheritReligiousProfile(s,child,parent);
            provincialKnowledgeMasks.Remove(child);
            for(int k=0;k<hc.Technology.Length;k++)if(h.Cultures[parent].Technology[k]>0&&(!ProvincialHistory(s)||k<15))hc.Technology[k]=s.Years+1;
            if(TechnicalHistory(s))
            {
                var source=h.Cultures[parent].Technical;var knowledge=hc.Technical;
                hc.Sedentism=Math.Max(hc.Sedentism,Math.Min(650,h.Cultures[parent].Sedentism));
                hc.Institution=Math.Max(hc.Institution,Math.Min(450,h.Cultures[parent].Institution));
                s.Society.Cultures[child].Organization=hc.Institution;
                for(int k=0;k<hc.Technology.Length;k++)if(source.Levels[k]>0)
                {
                    hc.Technology[k]=s.Years+1;knowledge.Levels[k]=source.Levels[k];knowledge.LevelYear[k]=s.Years;
                    knowledge.Offers[k]=source.Levels[k];knowledge.OfferYear[k]=s.Years;knowledge.Sources[k]=parent;
                    // Settlers carry technical knowledge, not a free steel mill,
                    // railway or electricity grid. Portable skills differ.
                    knowledge.Deployment[k]=HistoricalTechnicalMigration.CarriedDeployment(k,source.Deployment[k]);
                    if(knowledge.Deployment[k]>=250)knowledge.InstalledYear[k]=s.Years+1;
                }
            }
            var crops=h.Agriculture[child];var donor=h.Agriculture[parent];
            if(LocalSeedlotHistory(s))InheritLocalSeedCommunity(s,child,parent,site,sourceCell,sourceTown);
            else
            for(int k=0;k<6;k++)if(donor.AdoptedYear[k]>0&&donor.Cultivars[k]!=null&&(CommunityHistory(s)?CommunityTransferFit(s,site,donor.Cultivars[k]):HistoricalCropCatalog.TransferFit(CropHabitat(s,site),donor.Cultivars[k],CropLatitude(site),riverSites[site]))>.12)
            {crops.AdoptedYear[k]=s.Years+1;crops.SourceCulture[k]=parent;crops.Cultivars[k]=donor.Cultivars[k].Copy();crops.Cultivars[k].ParentCulture=parent;
                double localPractice=PassageHistory(s)?HistoricalLandscapeEffects.LivelihoodContinuity(CropHabitat(s,donor.PracticeCell[k]),CropHabitat(s,donor.PracticeCell[k]),CropHabitat(s,site)):1;
                crops.HusbandryYears[k]=(int)Math.Round(donor.HusbandryYears[k]*localPractice);}
            s.Society.Cultures[child].Farming=crops.AdoptedYear.Any(y=>y>0)?Math.Min(600,s.Society.Cultures[parent].Farming):0;
            if(CommunityHistory(s))crops.Animals=(donor.Animals??Array.Empty<HistoricalAnimalHusbandry>()).Where(animal=>animal.AdoptedYear>0&&Biogeography.Suitable(World.Ecology.Taxa[animal.TaxonId],CropHabitat(s,site),World.ElevationM[site])).Select(animal=>new HistoricalAnimalHusbandry{TaxonId=animal.TaxonId,Name=animal.Name,Practice=animal.Practice,AdoptedYear=s.Years+1,SourceCulture=parent,OriginCell=site}).ToArray();
            LivingCultureContact(s,child,parent);
        }
    }
}

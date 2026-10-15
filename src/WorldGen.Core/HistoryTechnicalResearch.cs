using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void OfferTechnicalKnowledge(PrehistoryState s,int receiver,int sender,long mask,int year)
        {
            if(!TechnicalHistory(s)||receiver==sender)return;
            var from=s.History.Cultures[sender].Technical;var to=s.History.Cultures[receiver].Technical;if(from==null||to==null)return;
            for(int k=0;k<HistoricalTechnicalGraph.Count;k++)if((mask&(1L<<k))!=0&&from.Levels[k]>0&&from.LevelYear[k]<year&&(from.Levels[k]>to.Offers[k]||from.Levels[k]==to.Offers[k]&&year>to.OfferYear[k]))
            {to.Offers[k]=from.Levels[k];to.OfferYear[k]=year;to.Sources[k]=sender;}
        }
        void AdvanceTechnicalResearch(PrehistoryState s,int id,int elapsed,int contact,int site)
        {
            var h=s.History;var a=h.Aggregates;var c=s.Society.Cultures[id];var hc=h.Cultures[id];var ca=a.Cultures[id];
            InitializeTechnicalPractice(hc,s.Years);var t=hc.Technical;int n=HistoricalTechnicalGraph.Count;t.LastYear=s.Years;
            // Domestication and genuinely carried portable practices are also
            // real knowledge; their existing event dates remain authoritative.
            for(int k=0;k<n;k++)if(hc.Technology[k]>0&&t.Levels[k]==0)
            {t.Levels[k]=1;t.LevelYear[k]=hc.Technology[k]-1;t.Deployment[k]=k<4?500:0;if(t.Deployment[k]>=250)t.InstalledYear[k]=s.Years+1;}
            if(contact>=0)OfferTechnicalKnowledge(s,id,contact,ProvincialKnowledge(h.Cultures[contact]),s.Years);
            // Inspect at most four ACTUALLY RECENT delivered contacts. No
            // culture census, geographical teleportation, or global frontier.
            var memory=a.Provinces.Exchanges[id];int offset=memory.Peers.Length==0?0:(int)(HistoryRoll("technical-peer",id,s.Years)*memory.Peers.Length);
            for(int p=0;p<Math.Min(4,memory.Peers.Length);p++)
            {int at=(offset+p)%memory.Peers.Length;if(s.Years-memory.LastYear[at]<=250)OfferTechnicalKnowledge(s,id,memory.Peers[at],memory.Capabilities[at],memory.LastYear[at]);}
            double war=0,competition=0,disease=0,wealth=0;int samples=Math.Min(3,ca.Cities.Count);
            int cityOffset=ca.Cities.Count==0?0:(int)(HistoryRoll("technical-practice-sites",id,s.Years)*ca.Cities.Count);
            for(int k=0;k<samples;k++)
            {
                int city=ca.Cities.At((cityOffset+k)%ca.Cities.Count);var town=s.Society.Settlements[city];var local=h.Towns[city];
                if(local.DiseaseUntil>=s.Years)disease=Math.Max(disease,.8);
                else if(local.DiseaseUntil>0&&s.Years-local.DiseaseUntil<80)disease=Math.Max(disease,.5*(1-(s.Years-local.DiseaseUntil)/80.0));
                int owner=a.Resolve(town.PolityId,h);if(owner<0)continue;var p=h.Polities[owner];
                war=Math.Max(war,D.Clamp(p.Exhaustion/350.0));wealth=Math.Max(wealth,D.Clamp(p.Treasury/(double)Math.Max(1000,p.Population/4)));
                foreach(int peer in a.Cities[city].Neighbors)
                {int rival=a.Resolve(s.Society.Settlements[peer].PolityId,h);if(rival>=0&&rival!=owner){competition=Math.Max(competition,.65);break;}}
            }
            var habitat=World.Ecology.Habitat[site];
            disease=Math.Max(disease,D.Clamp((habitat.MeanTenthsC-80)/220.0)*D.Clamp(habitat.AridityMilli/800.0)*.55);
            disease=Math.Max(disease,D.Clamp(ca.LargestCity/50000.0)*.55);
            var animals=h.Agriculture[id].Animals;for(int k=0;k<Math.Min(8,animals?.Length??0);k++)if(animals[k].AdoptedYear>0)disease=Math.Min(1,disease+.035);
            bool maritime=CommunityMaritimePractice(s,id);
            // Remote and unallocated rural residents are already in the culture's
            // population ledger. They must not vanish from practical demand just
            // because their farms are not one city's immediate catchment.
            double farmers=TechnicalAgriculturalWorkforce(s,id);
            double farming=D.Clamp(farmers/20000.0),urban=D.Clamp(ca.SupportedUrban/12000.0);
            // Technical effort comes from supported specialist communities. A
            // million dispersed people are not a million free laboratories.
            double scale=.35+.35*Math.Log(1+ca.SupportedUrban/1500.0);
            double institutions=.3+.7*hc.Institution/1000.0,surplus=.25+.75*hc.Surplus/1000.0;
            double science=HistoricalTechnicalGraph.Installed(hc,(int)HistoricalTechnology.ScientificMethod);
            double literacy=HistoricalTechnicalGraph.Installed(hc,(int)HistoricalTechnology.Printing);
            double communication=Math.Sqrt(HistoricalModernEffects.Communication(hc,s.Years));
            double importedCommunication=1;
            for(int p=0;p<Math.Min(4,memory.Peers.Length);p++)
            {int at=(offset+p)%memory.Peers.Length;if(s.Years-memory.LastYear[at]<=100)importedCommunication=Math.Max(importedCommunication,Math.Sqrt(HistoricalModernEffects.Communication(h.Cultures[memory.Peers[at]],s.Years)));}
            double annual=4.2*scale*institutions*surplus*(1+4*science)*(1+literacy)*communication;
            // Recalibrate invention against the corrected producing population,
            // rather than preserving the old low-population coefficient. Actual
            // contact learning remains cheap; this is not a calendar era lock.
            if(ColonialHistory(s))annual*=.68;
            if(ca.SupportedCities==0)annual*=.12;
            long budget=Math.Max(0,(long)Math.Round(D.Q(annual,1000000)*1000000))*elapsed;
            long learningBudget=Math.Max(0,(long)Math.Round(D.Q((.8+urban*3+literacy*4)*Math.Max(communication,importedCommunication),1000000)*1000000))*elapsed;
            Span<long> researchWeights=stackalloc long[n];Span<long> learningWeights=stackalloc long[n];long researchTotal=0,learningTotal=0;
            Array.Clear(t.Allocation,0,n);
            for(int k=0;k<n;k++)
            {
                if(!HistoricalTechnicalGraph.WithinCeiling(k,h.Modern.TechnologyCeiling)||t.Levels[k]>=HistoricalTechnicalGraph.MaximumLevel)continue;
                double demand=TechnicalDemand(k,war,competition,disease,farming,maritime,urban,ca.CivilizationSince>=0);
                if(k==(int)HistoricalTechnology.Irrigation)demand*=.2+D.Clamp((650-habitat.AridityMilli)/650.0)+.5*D.Clamp(riverSites[site]);
                int needed=HistoricalTechnicalGraph.UsefulLevel(k);double relevance=t.Levels[k]>=needed?.06:1;
                long priority=Math.Max(0,(long)Math.Round(D.Q(demand*relevance/Math.Pow(1.35,t.Levels[k]),1000000)*1000000));
                if(t.Offers[k]>t.Levels[k])
                {
                    // Accessible textbooks/teachers can convey a technique
                    // even without a local incentive to invent or deploy it.
                    // Peaceful isolation still supplies neither an offer nor
                    // any military-only independent research effort.
                    long learningPriority=Math.Max(priority,50000/(t.Levels[k]+1));
                    learningWeights[k]=learningPriority;learningTotal+=learningPriority;
                }
                else if(HistoricalTechnicalGraph.Prerequisites(k,t.Levels)&&AggregatePracticeSupported(s,id,k,false))
                {researchWeights[k]=priority;researchTotal+=priority;}
            }
            // Several projects may progress together, but adding a project
            // NEVER creates more effort. Integer remainders remain unspent.
            t.LastResearchBudget=budget;t.LastLearningBudget=learningBudget;t.LastResearchSpent=t.LastLearningSpent=0;
            for(int k=0;k<n;k++)
            {
                long work=TechnicalAllocation(budget,researchWeights[k],researchTotal);
                long learn=TechnicalAllocation(learningBudget,learningWeights[k],learningTotal);
                t.Allocation[k]=researchTotal>0?(int)(10000*researchWeights[k]/researchTotal):0;
                t.LastResearchSpent+=work;t.LastLearningSpent+=learn;t.Progress[k]+=work;t.Learning[k]+=learn;
                long cost=HistoricalTechnicalGraph.Cost(k,t.Levels[k]+1,h.Version);
                // Knowing a recipe is much cheaper than independently solving
                // the problem. Material deployment is handled separately below.
                bool adopted=learn>0&&t.Learning[k]>=Math.Max(1000000,cost/8);
                bool invented=work>0&&t.Progress[k]>=cost;
                if(!adopted&&!invented)continue;
                if(adopted)t.Learning[k]-=Math.Max(1000000,cost/8);else t.Progress[k]-=cost;
                t.Levels[k]++;t.LevelYear[k]=s.Years;
                if(hc.Technology[k]==0)
                {hc.Technology[k]=s.Years+1;HistoryEvent(s,"technology",c.Name+(adopted?" learns ":" develops ")+HistoricalTechnologies.Names[k]+(adopted&&t.Sources[k]>=0?" through exchange with "+s.Society.Cultures[t.Sources[k]].Name:"")+". Local use still requires skills, resources and investment.",site,id,category:ChronicleCategory.Discoveries);}
            }
            for(int k=0;k<n;k++)
            {
                if(t.Levels[k]==0)continue;double target=1;
                // A discovery at the END of this interval cannot retroactively
                // build an eighty-year industrial network during that interval.
                if(hc.Technology[k]-1>=s.Years)continue;
                foreach(var r in HistoricalTechnicalGraph.Requirements(k))target=Math.Min(target,t.Deployment[r.Technology]/700.0);
                bool imported=t.Offers[k]>0&&s.Years-t.OfferYear[k]<=150;
                if(imported&&k!=6&&k!=14&&k!=15&&k!=18)target=Math.Max(target,.55*D.Clamp(importedCommunication/2));
                double capital=k<15||HistoricalTechnicalGraph.Maritime(k)?.4+.6*urban:.12+.5*urban+.38*wealth;
                if(k>=20)capital*=.2+.8*Math.Min(1,HistoricalTechnicalGraph.Installed(hc,(int)HistoricalTechnology.IndustrialMetallurgy)+.35);
                if(HistoricalTechnicalGraph.WeaponsOnly(k))capital*=.15+.85*Math.Max(war,competition);
                if(HistoricalTechnicalGraph.Agricultural(k))capital*=Math.Max(.15,farming);
                if(HistoricalTechnicalGraph.Maritime(k))capital*=maritime?1:.05;
                if(HistoricalTechnicalGraph.Medical(k))capital*=.25+.75*Math.Max(disease,urban*.6);
                if(imported&&(k==26||k==30||k==31||k==32||k==35||k==36))capital=Math.Max(capital,.10+.45*wealth+.45*urban);
                if(imported&&HistoricalTechnicalGraph.WeaponsOnly(k))capital=Math.Max(capital,.2+.6*Math.Max(war,competition)+.2*wealth);
                target=D.Clamp(target*capital);
                double years=k<15?100:k<20?65:k<27?45:25;
                int current=t.Deployment[k];double gain=D.Q((target*1000-current)*(1-Math.Exp(-elapsed/years)),1000000);
                // Knowledge survives a depression; installed capacity can decay.
                int change=(int)Math.Floor(gain);if(gain>change&&HistoryRoll("technical-deployment-fraction-"+k,id,s.Years)<gain-change)change++;
                t.Deployment[k]=Math.Max(0,Math.Min(1000,current+change));
                if(t.Deployment[k]>=250&&t.InstalledYear[k]==0)t.InstalledYear[k]=s.Years+1;
            }
        }
        static long TechnicalAllocation(long budget,long weight,long total)
            =>total<=0||weight<=0?0:budget/total*weight+(budget%total)*weight/total;
        double TechnicalAgriculturalWorkforce(PrehistoryState s,int culture)
        {var c=s.Society.Cultures[culture];var a=s.History.Aggregates.Cultures[culture];return ColonialHistory(s)&&c.Farming>=180?Math.Max(a.Rural,Math.Max(0,c.Population-a.Urban)*D.Clamp(c.Farming/600.0)):a.Rural;}
        static double TechnicalDemand(int k,double war,double competition,double disease,double farming,bool maritime,double urban,bool administration)
        {
            double demand=.35+.65*urban;
            if(HistoricalTechnicalGraph.Military(k))demand*=HistoricalTechnicalGraph.WeaponsOnly(k)?2.5*war+1.2*competition:.3+war+competition;
            if(HistoricalTechnicalGraph.Medical(k))demand*=.08+2*disease+urban*.4;
            if(HistoricalTechnicalGraph.Agricultural(k))demand*=.15+1.5*farming;
            if(HistoricalTechnicalGraph.Maritime(k))demand*=maritime?1.7:.02;
            if(k==8||k==10||k==22||k==26||k==36)demand*=.6+competition+urban;
            if(k==(int)HistoricalTechnology.Writing)demand*=administration?3:1;
            return D.Q(demand,1000000);
        }
    }
}

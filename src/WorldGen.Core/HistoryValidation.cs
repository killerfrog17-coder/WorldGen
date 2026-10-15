using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ValidateHistory(PrehistoryState s)
        {
            var h=s.History;var society=s.Society;int n=World.Surface.Cells.Length;
            void Require(bool valid,string message){if(!valid)throw new ArgumentException("Invalid history checkpoint: "+message);}
            Require(h!=null&&(h.Version=="history-1"||h.Version=="history-2"||h.Version=="history-3"||h.Version=="history-4"||h.Version=="history-5"||h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||h.Version=="history-16")))))))),"version");
            Require(h.Cultures!=null&&h.Religions!=null&&h.Polities!=null&&h.Relations!=null&&h.Towns!=null&&h.Archive!=null,"missing records");
            Require(h.Cultures.Length==society.Cultures.Length&&h.Towns.Length==society.Settlements.Length,"identity alignment");
            Require(ModernHistory(s)?h.Modern!=null&&Enum.IsDefined(typeof(HistoricalTechnologyCeiling),h.Modern.TechnologyCeiling):h.Modern==null,"modern settings version");
            if(ModernHistory(s))
            {
                Require(h.Modern.Shipping!=null,"shipping records");
                for(int i=0;i<h.Modern.Shipping.Length;i++)
                {
                    var link=h.Modern.Shipping[i];Require(link!=null&&link.Id==i&&link.PortA>=0&&link.PortA<link.PortB&&link.PortB<society.Settlements.Length&&link.CultureA>=0&&link.CultureA<h.Cultures.Length&&link.CultureB>=0&&link.CultureB<h.Cultures.Length&&link.EstablishedYear>=0&&link.EstablishedYear<=s.Years&&link.DistanceM>=700000&&link.DistanceM<=9200000,"ocean exchange route");
                }
                Require(h.Modern.Shipping.Select(r=>(r.PortA,r.PortB)).Distinct().Count()==h.Modern.Shipping.Length,"duplicate ocean routes");
                var memories=h.Modern.CivicRivalries??Array.Empty<HistoricalCivicRivalry>();
                for(int i=0;i<memories.Length;i++)
                {
                    var r=memories[i];Require(r!=null&&r.Id==i&&r.TownA>=0&&r.TownA<r.TownB&&r.TownB<society.Settlements.Length&&r.FirstYear>=0&&r.FirstYear<=r.LastConflictYear&&r.LastConflictYear<=s.Years&&r.IntensityPermille>0&&r.IntensityPermille<=1000,"civic rivalry record");
                    // An older overlapping campaign may involve this pair only
                    // after a newer campaign first established their rivalry.
                    Require(h.Dynamics?.Campaigns!=null&&r.FirstCampaignId>=0&&r.FirstCampaignId<h.Dynamics.Campaigns.Length&&r.LastCampaignId>=0&&r.LastCampaignId<h.Dynamics.Campaigns.Length,"civic rivalry campaign provenance");
                    var first=h.Dynamics.Campaigns[r.FirstCampaignId];var last=h.Dynamics.Campaigns[r.LastCampaignId];
                    Require(first.StartedYear<=r.FirstYear&&(first.EndedYear<0||first.EndedYear>=r.FirstYear)&&last.StartedYear<=r.LastConflictYear&&(last.EndedYear<0||last.EndedYear>=r.LastConflictYear)&&society.Settlements[r.TownA].FoundedYear<=r.FirstYear&&society.Settlements[r.TownB].FoundedYear<=r.FirstYear,"civic rivalry dated provenance");
                    Require(D.Angle(World.Surface.Cells[society.Settlements[r.TownA].Cell].Position,World.Surface.Cells[society.Settlements[r.TownB].Cell].Position)*World.Recipe.RadiusKm<=400,"civic rivalry local contact");
                }
                Require(memories.Select(r=>(r.TownA,r.TownB)).Distinct().Count()==memories.Length,"duplicate civic rivals");
            }
            if(h.OceanSettlements!=null)
            {
                Require(DelegatedHistory(s),"ocean settlement version");
                for(int i=0;i<h.OceanSettlements.Length;i++)
                {
                    var v=h.OceanSettlements[i];Require(v!=null&&v.Id==i&&v.Year>=0&&v.Year<=s.Years&&v.SourceCulture>=0&&v.SourceCulture<v.DestinationCulture&&v.DestinationCulture<h.Cultures.Length&&v.FromCell>=0&&v.FromCell<n&&v.ToCell>=0&&v.ToCell<n&&CellPatch[v.FromCell]>=0&&CellPatch[v.ToCell]>=0&&v.DistanceM>900000&&v.DistanceM<=(ModernHistory(s)?9000000:3500000)&&v.Migrants>=50&&v.Migrants<=(ModernHistory(s)?3000:149)&&v.Provisions>=0&&v.Provisions<=100000,"ocean settlement record");
                    Require(v.DepartureTownId>=-1&&v.DepartureTownId<society.Settlements.Length&&v.UrbanMigrants>=0&&v.UrbanMigrants<=v.Migrants&&(v.DepartureTownId>=0||v.UrbanMigrants==0)&&(ModernHistory(s)||v.DepartureTownId==-1&&v.UrbanMigrants==0),"ocean founder origin ledger");
                    if(v.DepartureTownId>=0)Require(society.Settlements[v.DepartureTownId].FoundedYear<=v.Year,"ocean departure depot provenance");
                    Require(v.ArrivalFood>=0&&v.ArrivalFood<=v.Migrants&&(v.ArrivalFood==0||ModernHistory(s)&&v.DepartureTownId>=0),"ocean arrival reserve ledger");
                    Require(v.SailingDistanceM==0||ModernHistory(s)&&v.SailingDistanceM>=v.DistanceM,"ocean sailing distance");
                    if(ModernHistory(s))Require(Math.Max(v.DistanceM,v.SailingDistanceM)<=HistoricalModernVoyageEffects.RangeM(h.Cultures[v.SourceCulture],v.Year,ColonialHistory(s))&&v.Migrants<=149*HistoricalModernVoyageEffects.FounderScale(h.Cultures[v.SourceCulture],v.Year),"ocean vessel capability at departure");
                }
                foreach(var voyage in h.OceanSettlements){var child=society.Cultures[voyage.DestinationCulture];Require(child.FoundedYear==voyage.Year&&child.OriginCell==voyage.ToCell&&h.Cultures[child.Id].ParentCultureId==voyage.SourceCulture&&h.Cultures[voyage.SourceCulture].Technology[(int)HistoricalTechnology.Navigation]>0&&h.Cultures[voyage.SourceCulture].Technology[(int)HistoricalTechnology.Navigation]<=voyage.Year+1&&Math.Abs(voyage.DistanceM-D.Angle(World.Surface.Cells[voyage.FromCell].Position,World.Surface.Cells[voyage.ToCell].Position)*World.Recipe.RadiusKm*1000)<=1,"ocean settlement provenance");}
            }
            if(h.Administration!=null)
            {
                var administration=h.Administration;Require(LocalHistory(s)&&administration.Accounts!=null&&administration.Projects!=null&&administration.Offices!=null,"administration version");
                Require(administration.Accounts.Length<=h.Polities.Length,"administration alignment");
                for(int i=0;i<administration.Accounts.Length;i++){var a=administration.Accounts[i];Require(a!=null&&a.PolityId==i&&a.Year>=0&&a.Year<=s.Years&&a.ReachM>=0&&a.MaximumServiceDistanceM>=0&&a.RuralReceipts>=0&&a.ServiceCost>=0&&a.DisconnectedResidents>=0,"administration account");}
                Require(administration.Accounts.All(a=>a.UrbanReceipts>=0&&a.TaxableUrbanResidents>=0&&a.DisconnectedUrbanResidents>=0&&(DelegatedHistory(s)||a.UrbanReceipts==0&&a.TaxableUrbanResidents==0&&a.DisconnectedUrbanResidents==0)),"urban administration account");
                for(int i=0;i<administration.Projects.Length;i++){var p=administration.Projects[i];Require(p!=null&&p.Id==i&&p.PolityId>=0&&p.PolityId<h.Polities.Length&&p.Cell>=0&&p.Cell<n&&CellPatch[p.Cell]>=0&&p.StartedYear>=0&&p.StartedYear<=s.Years&&p.DueYear>p.StartedYear&&p.EndedYear>=-1&&p.EndedYear<=s.Years&&p.SetupCost>0&&p.AnnualCost>0,"incorporation project");}
                Require(administration.Projects.Where(p=>p.EndedYear<0).Select(p=>p.Cell).Distinct().Count()==administration.Projects.Count(p=>p.EndedYear<0),"duplicate incorporation target");
                foreach(var project in administration.Projects.Where(p=>p.Cells!=null))
                {
                    Require(DelegatedHistory(s)&&project.Cells.Length>0&&project.Cells.Contains(project.Cell)&&project.Cells.SequenceEqual(project.Cells.OrderBy(i=>i).Distinct())&&project.Cells.All(cell=>cell>=0&&cell<n&&CellPatch[cell]>=0),"administrative district cells");
                    var members=new System.Collections.Generic.HashSet<int>(project.Cells);var visited=new System.Collections.Generic.HashSet<int>{project.Cell};var queue=new System.Collections.Generic.Queue<int>();queue.Enqueue(project.Cell);
                    while(queue.Count>0)foreach(int next in World.Surface.Cells[queue.Dequeue()].Neighbors)if(members.Contains(next)&&visited.Add(next))queue.Enqueue(next);
                    Require(visited.Count==members.Count,"disconnected administrative district");
                }
                if(DelegatedHistory(s)){var targets=administration.Projects.Where(p=>p.EndedYear<0).SelectMany(p=>p.Cells??new[]{p.Cell}).ToArray();Require(targets.Distinct().Count()==targets.Length,"overlapping administrative districts");}
                for(int i=0;i<administration.Offices.Length;i++){var o=administration.Offices[i];Require(o!=null&&o.Id==i&&o.PolityId>=0&&o.PolityId<h.Polities.Length&&o.TownId>=0&&o.TownId<society.Settlements.Length&&o.FoundedYear>=0&&o.FoundedYear<=s.Years&&o.EndedYear>=-1&&o.EndedYear<=s.Years&&o.AnnualCost>0,"district office");}
                foreach(var o in administration.Offices){Require(o.PredecessorOfficeId>=-1&&o.PredecessorOfficeId<o.Id&&o.PredecessorPolityId>=-1&&o.PredecessorPolityId<h.Polities.Length&&(DelegatedHistory(s)||o.PredecessorOfficeId==-1&&o.PredecessorPolityId==-1),"office continuity version");if(o.PredecessorOfficeId>=0){var prior=administration.Offices[o.PredecessorOfficeId];Require(prior.TownId==o.TownId&&prior.PolityId==o.PredecessorPolityId&&prior.EndedYear>=0&&prior.EndedYear<=o.FoundedYear,"office continuity provenance");}}
                Require(administration.Offices.Where(o=>o.EndedYear<0).Select(o=>o.TownId).Distinct().Count()==administration.Offices.Count(o=>o.EndedYear<0),"duplicate district office");
            }
            if(RegionalHistory(s))Require(society.Settlements.Select(t=>t.Cell).Distinct().Count()==society.Settlements.Length,"one persistent settlement identity per site");
            if(h.RegionalLearning!=null)
            {
                var learning=h.RegionalLearning;Require(RegionalHistory(s)&&learning.FarmingSource?.Length==Patches.Length&&learning.FarmingExposure?.Length==Patches.Length,"regional learning alignment");
                for(int p=0;p<Patches.Length;p++)
                {
                    int source=learning.FarmingSource[p],exposure=learning.FarmingExposure[p];
                    Require(exposure>=0&&exposure<=1000&&source>=-1&&source<h.Cultures.Length&&(exposure==0?source==-1:source>=0),"regional learning values");
                    if(source>=0)Require(h.Agriculture!=null&&source<h.Agriculture.Length&&h.Agriculture[source].AdoptedYear.Any(y=>y>0&&y<=s.Years+1),"regional crop provenance");
                }
            }
            if(ExperimentalHistory(s)&&h.Cultures.Length>0)
            {
                var faith=h.Faith;Require(faith!=null&&faith.Candidate?.Length==n&&faith.Influence?.Length==n&&faith.LastChanged?.Length==n,"faith dynamics alignment");
                Require(faith.Candidate.All(i=>i>=-1&&i<h.Religions.Length)&&faith.Influence.All(i=>i>=0&&i<=1000)&&faith.LastChanged.All(i=>i>=0&&i<=s.Years),"faith dynamics values");
            }
            if(CivicHistory(s))
            {
                var d=h.Dynamics;Require(d!=null&&d.States!=null&&d.Leaders!=null&&d.Campaigns!=null&&d.States.Length==h.Polities.Length,"civic dynamics alignment");
                Require(d.States.All(p=>p!=null)&&d.Leaders.All(p=>p!=null)&&d.Campaigns.All(p=>p!=null&&p.CapturedSettlements!=null),"missing civic record");
                for(int i=0;i<d.States.Length;i++){var p=d.States[i];Require(p.Id==i&&p.LeaderId>=0&&p.LeaderId<d.Leaders.Length&&d.Leaders[p.LeaderId].PolityId==i&&p.FoundationPotential>=0&&p.FoundationPotential<=1000&&p.Strain>=0&&p.Strain<=700&&p.PublicWorks>=0&&p.PublicWorks<=1000&&p.LastSuccession<=s.Years&&p.PeakPopulation>=0&&p.PeakAreaKm2>=0,"civic state");if(h.Polities[i].EndedYear<0)Require(d.Leaders[p.LeaderId].EndedYear<0,"active ruler");}
                for(int i=0;i<d.Leaders.Length;i++){var p=d.Leaders[i];Require(p.Id==i&&p.PolityId>=0&&p.PolityId<h.Polities.Length&&p.StartedYear<=s.Years&&p.ExpectedEndYear>p.StartedYear&&(p.EndedYear<0||p.EndedYear>=p.StartedYear&&p.EndedYear<=s.Years)&&new[]{p.Military,p.Administration,p.Diplomacy,p.Ambition,p.Renown}.All(v=>v>=0&&v<=1000)&&p.Victories>=0&&p.Defeats>=0&&p.Reforms>=0,"leader record");}
                for(int i=0;i<d.Campaigns.Length;i++){var c=d.Campaigns[i];Require(c.Id==i&&c.A>=0&&c.A<c.B&&c.B<h.Polities.Length&&(c.AttackerId==c.A||c.AttackerId==c.B)&&c.LeaderId>=0&&c.LeaderId<d.Leaders.Length&&d.Leaders[c.LeaderId].PolityId==c.AttackerId&&c.StartedYear<=s.Years&&(c.EndedYear<0||c.EndedYear>=c.StartedYear&&c.EndedYear<=s.Years)&&c.LastActionYear>=c.StartedYear&&c.LastActionYear<=s.Years&&c.Conquests==c.CapturedSettlements.Length&&c.CapturedSettlements.All(t=>t>=0&&t<society.Settlements.Length)&&c.CasualtiesA>=0&&c.CasualtiesB>=0,"campaign record");}
                Require(d.Campaigns.All(c=>Enum.IsDefined(typeof(HistoricalWarAim),c.Aim)&&c.Repulses>=0&&c.Spoils>=0&&c.TributePaid>=0&&(c.TributePaid==0?c.TributeRecipientId==-1:c.EndedYear>=0&&(c.TributeRecipientId==c.A||c.TributeRecipientId==c.B))),"campaign settlement");
                Require(d.Campaigns.All(c=>string.IsNullOrEmpty(c.Context)||DelegatedHistory(s)&&c.Context.Length<=2000),"campaign context version");
                Require(d.Leaders.Where(l=>l.EndedYear<0).All(l=>h.Polities[l.PolityId].EndedYear<0&&d.States[l.PolityId].LeaderId==l.Id&&l.ExpectedEndYear>s.Years),"orphaned active leader");
                Require(d.Campaigns.Where(c=>c.EndedYear<0).All(c=>h.Polities[c.A].EndedYear<0&&h.Polities[c.B].EndedYear<0&&h.Relations.Any(r=>r.A==c.A&&r.B==c.B&&r.WarSince>=0)),"orphaned campaign");
                Require(d.Dependencies!=null&&(ExperimentalHistory(s)||d.Dependencies.Length==0),"dependency version");
                for(int i=0;i<d.Dependencies.Length;i++)
                {
                    var dependency=d.Dependencies[i];
                    Require(dependency!=null&&dependency.Id==i&&dependency.PatronId>=0&&dependency.PatronId<h.Polities.Length&&dependency.ClientId>=0&&dependency.ClientId<h.Polities.Length&&dependency.PatronId!=dependency.ClientId&&dependency.StartedYear<=s.Years&&(dependency.EndedYear<0||dependency.EndedYear>=dependency.StartedYear&&dependency.EndedYear<=s.Years)&&dependency.LastReachableYear>=dependency.StartedYear&&dependency.LastReachableYear<=s.Years&&dependency.AnnualQuota>=25&&dependency.AnnualQuota<=5000&&dependency.TotalPaid>=0&&dependency.ArrearsYears>=0&&dependency.ArrearsYears<=100,"dependency record");
                    if(dependency.EndedYear<0)Require(h.Polities[dependency.PatronId].EndedYear<0&&h.Polities[dependency.ClientId].EndedYear<0&&!HistoricalClientEffects.WouldCycle(d.Dependencies,dependency.PatronId,dependency.ClientId)&&h.Relations.Any(r=>r.A==Math.Min(dependency.PatronId,dependency.ClientId)&&r.B==Math.Max(dependency.PatronId,dependency.ClientId)&&r.WarSince<0),"orphaned or cyclic dependency");
                }
                Require(d.Dependencies.Where(x=>x.EndedYear<0).Select(x=>x.ClientId).Distinct().Count()==d.Dependencies.Count(x=>x.EndedYear<0),"multiple overlords");
                foreach(var leader in d.Leaders)
                {
                    Require(leader.PredecessorId>=-1&&leader.PredecessorId<leader.Id,"succession predecessor");
                    if(leader.PredecessorId>=0){var former=d.Leaders[leader.PredecessorId];Require(former.PolityId==leader.PolityId&&former.EndedYear>=0&&former.EndedYear<=leader.StartedYear,"succession chronology");}
                    Require(ExperimentalHistory(s)?!string.IsNullOrEmpty(leader.Selection):leader.PredecessorId==-1&&string.IsNullOrEmpty(leader.Selection),"succession version");
                }
                Require(d.States.All(p=>p.HouseSerial>=0&&p.DisputedUntil>=0&&p.DisputedUntil<=s.Years+100&&(ExperimentalHistory(s)||p.HouseSerial==0&&p.DisputedUntil==0)),"court succession state");
            }
            if(EraHistory(s))
            {
                Require(h.Agriculture!=null&&h.Agriculture.Length==h.Cultures.Length,"agriculture identity alignment");
                for(int i=0;i<h.Agriculture.Length;i++)
                {
                    var a=h.Agriculture[i];Require(a!=null&&a.CultureId==i&&a.NativeMask>=0&&a.NativeMask<64&&a.Practice?.Length==6&&a.AdoptedYear?.Length==6&&a.SourceCulture?.Length==6,"crop records");
                    Require(a.Animals==null||RealismHistory(s),"animal husbandry version");
                    if(a.Animals!=null)
                    {
                        Require(a.Animals.All(v=>v!=null&&v.TaxonId>=0&&v.TaxonId<World.Ecology.Taxa.Length&&v.OriginCell>=0&&v.OriginCell<n&&CellPatch[v.OriginCell]>=0&&v.Practice>=0&&v.Practice<=1000&&v.AdoptedYear>=0&&v.AdoptedYear<=s.Years+1&&v.SourceCulture>=-1&&v.SourceCulture<h.Cultures.Length&&v.SourceCulture!=i&&v.Name==World.Ecology.Taxa[v.TaxonId].Name),"animal management records");
                        Require(a.Animals.Select(v=>v.TaxonId).Distinct().Count()==a.Animals.Length,"duplicate livestock lineage");
                        foreach(var animal in a.Animals.Where(v=>v.AdoptedYear>0))Require(animal.SourceCulture<0?Biogeography.Present(World.Ecology,animal.OriginCell,animal.TaxonId):h.Agriculture[animal.SourceCulture].Animals?.Any(parent=>parent.TaxonId==animal.TaxonId&&parent.AdoptedYear>0&&parent.AdoptedYear<animal.AdoptedYear)==true,"livestock provenance");
                    }
                    Require(RegionalHistory(s)?a.PracticeCell?.Length==6&&a.PracticeCell.All(cell=>cell>=0&&cell<n&&CellPatch[cell]>=0):a.PracticeCell==null,"crop practice locations");
                    if(RegionalHistory(s))Require(a.PracticeCell.All(cell=>Math.Round(D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[society.Cultures[i].OriginCell].Position)*World.Recipe.RadiusKm*1000)<=300000),"local crop-study reach");
                    Require(RegionalHistory(s)?a.WildOriginCell?.Length==6:a.WildOriginCell==null,"wild crop locations");
                    for(int k=0;k<6;k++)
                    {
                        if(RegionalHistory(s))
                        {
                            int wild=a.WildOriginCell[k];
                            Require(wild>=-1&&wild<n&&(wild<0?(a.NativeMask&(1<<k))==0:CellPatch[wild]>=0&&(NativeCropMask(wild)&(1<<k))!=0&&(a.NativeMask&(1<<k))!=0&&Math.Round(D.Angle(World.Surface.Cells[wild].Position,World.Surface.Cells[society.Cultures[i].OriginCell].Position)*World.Recipe.RadiusKm*1000)<=300000),"observed wild crop provenance");
                        }
                        Require(a.Practice[k]>=0&&a.Practice[k]<=1000&&a.AdoptedYear[k]>=0&&a.AdoptedYear[k]<=s.Years+1&&a.SourceCulture[k]>=-1&&a.SourceCulture[k]<h.Cultures.Length,"crop values");
                        Require(a.AdoptedYear[k]>0||a.SourceCulture[k]==-1,"unadopted crop source");
                        if(a.AdoptedYear[k]>0)Require(a.SourceCulture[k]<0?(a.NativeMask&(1<<k))!=0:h.Agriculture[a.SourceCulture[k]].AdoptedYear[k]>0&&h.Agriculture[a.SourceCulture[k]].AdoptedYear[k]<a.AdoptedYear[k],"crop provenance");
                    }
                    Require(society.Cultures[i].Farming==0||a.AdoptedYear.Any(y=>y>0),"farming requires a domesticated crop system");
                    Require(ModernHistory(s)?a.Cultivars?.Length==6:a.Cultivars==null,"cultivar version");
                    Require(a.HusbandryYears==null||ModernHistory(s)&&a.HusbandryYears.Length==6&&a.HusbandryYears.All(years=>years>=0&&years<=20000)&&Enumerable.Range(0,6).All(k=>a.AdoptedYear[k]>0||a.HusbandryYears[k]==0),"local husbandry version and years");
                    if(ModernHistory(s))for(int k=0;k<6;k++)
                    {
                        var v=a.Cultivars[k];Require((a.AdoptedYear[k]>0)==(v!=null),"cultivar adoption");if(v==null)continue;
                        Require(v.CropId>=0&&v.CropId<HistoricalCropCatalog.Crops.Length&&HistoricalCropCatalog.Crops[v.CropId].Group==k&&v.OriginCulture>=0&&v.OriginCulture<h.Cultures.Length&&v.ParentCulture>=-1&&v.ParentCulture<h.Cultures.Length&&v.SelectedYears>=0&&v.SelectedYears<=20000&&v.MaturityDays>=60&&v.MaturityDays<=365&&v.DaylengthLatitudeTenths>=0&&v.DaylengthLatitudeTenths<=900&&v.WaterRequirementPermille>=100&&v.WaterRequirementPermille<=1500&&v.YieldPermille>=650&&v.YieldPermille<=1300,"cultivar traits");
                        Require(v.ParentCulture==a.SourceCulture[k]&&(v.ParentCulture<0?v.OriginCulture==i:h.Agriculture[v.ParentCulture].Cultivars?[k]!=null&&h.Agriculture[v.ParentCulture].Cultivars[k].CropId==v.CropId&&h.Agriculture[v.ParentCulture].Cultivars[k].OriginCulture==v.OriginCulture),"cultivar introduction lineage");
                        Require(v.MinimumWarmTenthsC==0||v.MinimumWarmTenthsC>=60&&v.MinimumWarmTenthsC<=300,"cultivar thermal requirement");
                    }
                }
            }
            int cells=h.Cultures.Length==0?0:n;
            Require(h.CellPolity!=null&&h.CellReligion!=null&&h.CellStage!=null&&h.CellMarket!=null&&h.CellPolity.Length==cells&&h.CellReligion.Length==cells&&h.CellStage.Length==cells&&(h.CellMarket.Length==0||h.CellMarket.Length==n),"cell fields");
            Require(h.CellPolity.All(i=>i>=-1&&i<h.Polities.Length&&(i<0||h.Polities[i].EndedYear<0))&&h.CellReligion.All(i=>i>=-1&&i<h.Religions.Length)&&h.CellMarket.All(i=>i>=-1&&i<h.Towns.Length)&&h.CellStage.All(i=>i>=0&&i<=3),"cell references");
            if(CivicHistory(s))Require(society.Settlements.All(t=>t.Cell>=0&&t.Cell<h.CellPolity.Length&&t.PolityId==h.CellPolity[t.Cell]),"town and territory administration disagree");
            for(int i=0;i<h.Cultures.Length;i++)
            {
var c=h.Cultures[i];Require(c.Id==i&&c.ReligionId>=0&&c.ReligionId<h.Religions.Length&&c.LanguageId>=0&&c.LanguageId<h.Cultures.Length&&c.Technology?.Length==HistoricalTechnologies.Count(h.Version)&&c.Research?.Length==HistoricalTechnologies.Count(h.Version)&&c.Research.All(x=>x>=0)&&c.Technology.All(x=>x>=0&&x<=s.Years+1),"culture technology");
                Require(c.ParentCultureId>=-1&&c.ParentCultureId<i&&(ExperimentalHistory(s)||c.ParentCultureId==-1),"culture ancestry");
                Require(c.InstitutionalExamples>=0&&c.InstitutionalExamples<=1000&&(RegionalHistory(s)||c.InstitutionalExamples==0),"institutional example exposure");
                for(int t=0;t<c.Technology.Length;t++)Require(c.Technology[t]==0||HistoricalTechnologies.Requirements(h.Version,t).All(p=>c.Technology[p]>0&&c.Technology[p]<=c.Technology[t]),"technology prerequisite");
            }
            for(int i=0;i<h.Religions.Length;i++){var r=h.Religions[i];Require(r.Id==i&&r.ParentId>=-1&&r.ParentId<i&&r.CultureId>=0&&r.CultureId<h.Cultures.Length&&r.HolyCell>=0&&r.HolyCell<n&&r.FoundedYear<=s.Years&&Enum.IsDefined(typeof(ReligiousReach),r.Reach)&&(ExperimentalHistory(s)||r.Reach==ReligiousReach.Local),"religion");}
            for(int i=0;i<h.Polities.Length;i++){var p=h.Polities[i];Require(p.Id==i&&p.CapitalId>=0&&p.CapitalId<h.Towns.Length&&p.ParentId>=-1&&p.ParentId<i&&p.CultureId>=0&&p.CultureId<h.Cultures.Length&&p.ReligionId>=0&&p.ReligionId<h.Religions.Length&&p.Army>=0&&p.Treasury>=0&&p.FoundedYear<=s.Years&&(p.EndedYear<0||p.EndedYear>=p.FoundedYear&&p.EndedYear<=s.Years),"polity");}
            Require(h.Polities.All(p=>p.LastEstimateYear==0||DelegatedHistory(s)&&p.LastEstimateYear>=p.FoundedYear&&p.LastEstimateYear<=s.Years&&(p.EndedYear<0||p.LastEstimateYear<=p.EndedYear)),"political estimate date");
            for(int i=0;i<h.Towns.Length;i++){var t=society.Settlements[i];Require(h.Towns[i].Id==i&&h.Towns[i].FoodBase>=0&&t.PolityId>=-1&&t.PolityId<h.Polities.Length&&(t.PolityId<0||h.Polities[t.PolityId].EndedYear<0),"town ownership");}
            foreach(var town in h.Towns)
            {
                if(town.Cropping!=null)
                {
                    var crop=town.Cropping;Require(ModernHistory(s)&&crop.Year>=0&&crop.Year<=s.Years&&crop.CultureId>=0&&crop.CultureId<h.Cultures.Length&&crop.CropIds!=null&&crop.SharesPermille!=null&&crop.CropIds.Length==crop.SharesPermille.Length&&crop.CropIds.Distinct().Count()==crop.CropIds.Length&&crop.CropIds.All(id=>id>=0&&id<HistoricalCropCatalog.Crops.Length)&&crop.SharesPermille.All(share=>share>=0&&share<=1000)&&(crop.CropIds.Length==0||crop.SharesPermille.Sum()==1000),"local cropping account");
                }
                if(town.Demography!=null){var d=town.Demography;Require(DelegatedHistory(s)&&d.Year>=0&&d.Year<=s.Years&&d.RuralInflow>=0&&d.RuralOutflow>=0&&d.ChronicLoss>=0&&d.TotalRuralInflow>=d.RuralInflow&&d.TotalRuralOutflow>=d.RuralOutflow&&d.TotalChronicLoss>=d.ChronicLoss,"urban demographic records");}
                Require(town.CivicPreparationYears>=0&&town.CivicPreparationYears<=1000&&town.CivicPreparationYears<=s.Years&&(DiffusionHistory(s)||town.CivicPreparationYears==0),"local civic preparation");
                Require(town.CivicExampleExposure>=0&&town.CivicExampleExposure<=1000&&(RegionalHistory(s)||town.CivicExampleExposure==0),"local institutional examples");
                Require(town.CivicTeacherPolityId>=-1&&town.CivicTeacherPolityId<h.Polities.Length&&(EmergenceHistory(s)||town.CivicTeacherPolityId==-1),"local institutional teacher");
                Require(town.AutonomyPressure>=0&&town.AutonomyPressure<=1000&&town.LastAutonomyActionYear>=0&&town.LastAutonomyActionYear<=s.Years&&town.UnrestPolityId>=-1&&town.UnrestPolityId<h.Polities.Length&&(TerritorialHistory(s)||town.AutonomyPressure==0&&town.LastAutonomyActionYear==0&&town.UnrestPolityId==-1),"provincial autonomy records");
                Require(Enum.IsDefined(typeof(HarvestHazard),town.LastHarvestHazard)&&town.LastHarvestHazardYear>=0&&town.LastHarvestHazardYear<=s.Years&&town.ShortageYears>=0&&town.ShortageYears<=s.Years&&town.ImportedFood>=0&&town.ExportedFood>=0&&town.FoodTransportLoss>=0&&town.FoodTransportLoss<=town.ExportedFood&&town.DiseaseDeaths>=0&&Enum.IsDefined(typeof(HistoricalDiseaseKind),town.DiseaseKind)&&town.DiseaseMortalityPermille>=0&&town.DiseaseMortalityPermille<=150,"town resilience records");
                Require(town.KindImmunity==null||town.KindImmunity.Length==4&&town.KindImmunity.All(y=>y>=0&&y<=s.Years+80),"syndrome recovery records");
                Require(town.DiseaseKind==HistoricalDiseaseKind.None?town.DiseaseMortalityPermille==0:town.KindImmunity!=null&&town.DiseaseMortalityPermille>0,"syndrome identity");
                Require(town.DiseaseWaveId>=0&&town.DiseaseWaveId<=h.Archive.Count,"disease wave reference");
                if(town.DiseaseWaveId>0)Require(h.Archive[town.DiseaseWaveId-1].Kind=="epidemic"||h.Archive[town.DiseaseWaveId-1].Kind=="major-epidemic","disease wave origin");
                if(!ExperimentalHistory(s))Require(town.LastHarvestHazard==HarvestHazard.None&&town.LastHarvestHazardYear==0&&town.ShortageYears==0&&town.ImportedFood==0&&town.ExportedFood==0&&town.FoodTransportLoss==0&&town.DiseaseDeaths==0&&town.DiseaseKind==HistoricalDiseaseKind.None&&town.DiseaseMortalityPermille==0&&town.DiseaseWaveId==0&&town.KindImmunity==null,"resilience version");
            }
            Require(h.Relations.Select(r=>(r.A,r.B)).Distinct().Count()==h.Relations.Length,"duplicate relations");
            foreach(var r in h.Relations)Require(r.A>=0&&r.A<r.B&&r.B<h.Polities.Length&&r.Trust>=-100&&r.Trust<=100&&r.WarSince<=s.Years&&!(r.WarSince>=0&&(r.Trade||r.Alliance)),"diplomatic relation");
            for(int i=0;i<h.Archive.Count;i++){var e=h.Archive[i];Require(e.Id==i&&e.Year<=s.Years&&(i==0||e.Year>=h.Archive[i-1].Year)&&e.Cell>=-1&&e.Cell<n&&e.CultureId>=-1&&e.CultureId<h.Cultures.Length&&e.PolityId>=-1&&e.PolityId<h.Polities.Length&&e.OtherPolityId>=-1&&e.OtherPolityId<h.Polities.Length&&e.SettlementId>=-1&&e.SettlementId<h.Towns.Length&&e.ReligionId>=-1&&e.ReligionId<h.Religions.Length,"archive linkage");}
        }
    }
}

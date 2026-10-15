using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public static class HistoricalDistrictEffects
    {
        public static double AreaKm2(bool writing,bool roads)=>roads?12000:writing?8000:2500;
        public static double LandServiceCost(double areaKm2,double travelRatio)=>D.Q(Math.Max(0,areaKm2)*.0015*(1+.5*Math.Max(0,travelRatio)),1000000);
        public static double Opportunity(double benefit,double areaKm2,int legitimacy,double resistance)
            =>D.Q(.15*D.Clamp(benefit/Math.Max(1,areaKm2)*10000/80)*D.Clamp(legitimacy/700.0)*(1-.7*D.Clamp(resistance)),1000000000);
    }
    public sealed partial class Prehistory
    {
        sealed class AdministrativeDistrict
        {
            public int Center;public int[] Cells;public double Area,Benefit,Resistance,Distance;public long Service;
        }
        int DistrictStaff(PrehistoryState s,HistoricalPolity p)
        {
            long urban=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100).Sum(t=>t.Population);
            double capacity=ModernHistory(s)?HistoricalModernEffects.AdministrativeCapacity(s.History.Cultures[p.CultureId],s.Years):1;
            return Math.Max(1,(int)Math.Min(8*capacity,(urban/8000+(HistoricalTechnologies.Has(s.History.Cultures[p.CultureId],HistoricalTechnology.Writing)?1:0))*capacity));
        }
        HashSet<int> PendingDistrictCells(PrehistoryState s)
            =>new HashSet<int>((s.History.Administration?.Projects??Array.Empty<HistoricalIncorporation>()).Where(p=>p.EndedYear<0).SelectMany(p=>p.Cells??new[]{p.Cell}));
        AdministrativeDistrict DescribeDistrict(PrehistoryState s,HistoricalPolity p,int center,AdministrativeNetwork network,HashSet<int> pending,Dictionary<int,(long Service,double Benefit)> finances=null,Dictionary<int,long> urban=null)
        {
            var hc=s.History.Cultures[p.CultureId];double target=HistoricalDistrictEffects.AreaKm2(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads));
            double transport=ModernHistory(s)?HistoricalModernEffects.LandTransport(hc,s.Years):1;target*=transport;
            var cells=new List<int>();var seen=new HashSet<int>{center};var queue=new Queue<int>();queue.Enqueue(center);double area=0;
            while(queue.Count>0)
            {
                int cell=queue.Dequeue();if(s.History.CellPolity[cell]>=0||network.Owner[cell]!=p.Id||CellPatch[cell]<0||pending.Contains(cell)||World.Hydrology.LakeCoverageFraction[cell]>=.9)continue;
                double localArea=HistoricalLandArea(cell);if(cells.Count>0&&area+localArea>target*1.35)continue;
                if(D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[center].Position)*World.Recipe.RadiusKm>80*Math.Sqrt(transport)&&cell!=center)continue;
                cells.Add(cell);area+=localArea;if(area>=target)continue;
                foreach(int next in World.Surface.Cells[cell].Neighbors.OrderBy(i=>i))if(seen.Add(next))queue.Enqueue(next);
            }
            if(cells.Count==0)return null;cells.Sort();var district=new AdministrativeDistrict{Center=center,Cells=cells.ToArray(),Area=area};
            foreach(int cell in district.Cells)
            {
                double weight=HistoricalLandArea(cell)/Math.Max(1,area);int local=s.Society.CellCulture[cell];
                district.Resistance+=weight*(local>=0&&local!=p.CultureId?s.Society.Cultures[local].Organization/1000.0:0);
                district.Distance+=weight*network.Distance[cell];
                (long Service,double Benefit) account;
                if(finances==null||!finances.TryGetValue(cell,out account))
                {
                    account=(DistrictServiceCost(s,p.Id,cell,network.Distance[cell],network.Accounts[p.Id].ReachM),DistrictBenefit(s,p.Id,cell,network.Distance[cell],network.Accounts[p.Id].ReachM,urban==null?(long?)null:urban.TryGetValue(cell,out long residents)?residents:0));
                    if(finances!=null)finances.Add(cell,account);
                }
                district.Service+=account.Service;district.Benefit+=account.Benefit;
            }
            district.Benefit=D.Q(district.Benefit,1000000);district.Resistance=D.Q(district.Resistance,1000000);district.Distance=D.Q(district.Distance,1000000);return district;
        }
        bool BeginDistrictIncorporation(PrehistoryState s,HistoricalPolity p,int center,AdministrativeNetwork network,AdministrativeDistrict district=null)
        {
            var h=s.History;var a=h.Administration??(h.Administration=new HistoricalAdministration());
            if(p.EndedYear>=0||p.Cohesion<500||h.Dynamics.States[p.Id].Strain>350||a.Projects.Count(x=>x.PolityId==p.Id&&x.EndedYear<0)>=DistrictStaff(s,p))return false;
            var pending=PendingDistrictCells(s);district=district??DescribeDistrict(s,p,center,network,pending);
            if(district==null||district.Benefit<=0||district.Cells.Any(pending.Contains))return false;
            long annual=district.Service+5+(long)(20*district.Resistance),setup=150+(long)(district.Distance/500+250*district.Resistance+district.Area*.005);
            if(p.Treasury<setup+annual*10)return false;int id=a.Projects.Length,duration=15+(int)(30*HistoryRoll("incorporation-duration-11",id,s.Years))+(int)(30*district.Resistance);
            if(ModernHistory(s))duration=Math.Max(5,(int)Math.Round(duration/Math.Sqrt(HistoricalModernEffects.AdministrativeCapacity(h.Cultures[p.CultureId],s.Years))));
            p.Treasury-=setup;a.Projects=a.Projects.Concat(new[]{new HistoricalIncorporation{Id=id,PolityId=p.Id,Cell=center,Cells=(int[])district.Cells.Clone(),StartedYear=s.Years,DueYear=s.Years+duration,SetupCost=setup,AnnualCost=annual}}).ToArray();return true;
        }
        void AdvanceDistrictIncorporations(PrehistoryState s,CancellationToken token)
        {
            var a=s.History.Administration;if(a==null||!a.Projects.Any(p=>p.EndedYear<0))return;var network=BuildAdministrativeNetwork(s,token);
            var towns=s.Society.Settlements.ToDictionary(t=>t.Cell);
            foreach(var project in a.Projects.Where(p=>p.EndedYear<0).OrderBy(p=>p.Id))
            {
                token.ThrowIfCancellationRequested();var polity=s.History.Polities[project.PolityId];var cells=project.Cells??new[]{project.Cell};
                bool viable=polity.EndedYear<0&&polity.Treasury>=project.AnnualCost&&polity.Cohesion>=350&&cells.All(cell=>(s.History.CellPolity[cell]<0||s.History.CellPolity[cell]==polity.Id)&&network.Owner[cell]==polity.Id);
                if(!viable){project.EndedYear=s.Years;project.Outcome="Abandoned when political access, funds or administrative support failed";HistoryEvent(s,"incorporation-abandoned",polity.Name+" abandons an outlying administrative district when access, cooperation or continuing funding fails.",project.Cell,polity.CultureId,polity.Id);continue;}
                polity.Treasury-=project.AnnualCost;if(s.Years<project.DueYear)continue;
                project.EndedYear=s.Years;project.Outcome="Local administration established";
                foreach(int cell in cells){SetPoliticalOwner(s,cell,polity.Id);if(towns.TryGetValue(cell,out var town)){town.PolityId=polity.Id;town.Government=polity.Government;s.History.Towns[town.Id].UnrestPolityId=polity.Id;s.History.Towns[town.Id].AutonomyPressure=0;}}
                HistoryEvent(s,"district-incorporated",polity.Name+" establishes an administrative district after years of negotiated obligations, provisioning and enforcement.",project.Cell,polity.CultureId,polity.Id,category:ChronicleCategory.Politics);
            }
        }
        void PlanAdministrativeDistricts(PrehistoryState s,CancellationToken token)
        {
            using var measurement=ProfileWork("district planning");
            var h=s.History;var a=h.Administration??(h.Administration=new HistoricalAdministration());var network=BuildAdministrativeNetwork(s,token);var frontier=new SortedSet<int>();
            var urban=UseHistoryIndexes?s.Society.Settlements.Where(t=>t.Population>=100).GroupBy(t=>t.Cell).ToDictionary(g=>g.Key,g=>g.Sum(t=>t.Population)):null;
            foreach(int cell in PoliticalCells(s))foreach(int next in World.Surface.Cells[cell].Neighbors)if(h.CellPolity[next]<0)frontier.Add(next);
            foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
            {
                token.ThrowIfCancellationRequested();if(p.Cohesion<500||h.Dynamics.States[p.Id].Strain>350||a.Projects.Count(x=>x.PolityId==p.Id&&x.EndedYear<0)>=DistrictStaff(s,p))continue;
                if(HistoricalTechnologies.Has(h.Cultures[p.CultureId],HistoricalTechnology.Sailing))foreach(var town in s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100))if(administrativeSeaRoutes.TryGetValue(town.Cell,out var routes))foreach(var route in routes)frontier.Add(route.CellA==town.Cell?route.CellB:route.CellA);
                int opportunities=ModernHistory(s)?Math.Max(1,(int)Math.Floor(HistoricalModernEffects.AdministrativeCapacity(h.Cultures[p.CultureId],s.Years))):1;
                if(RealismHistory(s))opportunities=Math.Max(opportunities,Math.Min(6,DistrictStaff(s,p)));
                var attempted=new HashSet<int>();
                var finances=UseHistoryIndexes?new Dictionary<int,(long Service,double Benefit)>():null;
                var descriptions=UseHistoryIndexes?new Dictionary<int,AdministrativeDistrict>():null;int projectCount=-1;
                for(int opportunity=0;opportunity<opportunities;opportunity++)
                {
                    if(projectCount!=a.Projects.Length){descriptions?.Clear();projectCount=a.Projects.Length;}
                    var pending=PendingDistrictCells(s);AdministrativeDistrict best=null;double bestScore=0;
                    foreach(int cell in frontier)
                    {
                        if(cell<0||attempted.Contains(cell)||network.Owner[cell]!=p.Id||h.CellPolity[cell]>=0||pending.Contains(cell)||CellPatch[cell]<0||RuralCellPopulation(s,cell)<100)continue;
                        AdministrativeDistrict candidate;
                        if(descriptions==null||!descriptions.TryGetValue(cell,out candidate))
                        {candidate=DescribeDistrict(s,p,cell,network,pending,finances,urban);if(descriptions!=null)descriptions.Add(cell,candidate);}
                        if(candidate==null||candidate.Benefit<=0)continue;
                        double score=D.Q(candidate.Benefit/Math.Max(1,candidate.Area),1000000000);if(score>bestScore){best=candidate;bestScore=score;}
                    }
                    if(best==null)break;attempted.Add(best.Center);
                    string stream=opportunity==0?"supported-district-11":"supported-district-13-"+opportunity;
                    if(HistoryRoll(stream,p.Id,s.Years)>=HistoricalDistrictEffects.Opportunity(best.Benefit,best.Area,p.Legitimacy,best.Resistance))continue;
                    BeginDistrictIncorporation(s,p,best.Center,network,best);
                }
            }
        }
    }
}

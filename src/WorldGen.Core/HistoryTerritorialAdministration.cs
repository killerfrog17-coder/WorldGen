using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        double TerritorialServiceRadius(PrehistoryState s,int polity,int target)
        {
            var h=s.History;var hc=h.Cultures[h.Polities[polity].CultureId];
            double radius=HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,h.Dynamics.States[polity].PublicWorks);
            double density=AggregateRuralCellPopulation(s,target)/Math.Max(1,inhabitableArea[target]);
            return radius*(1+2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years)*(1-D.Clamp(density/15)));
        }
        int TerritorialServiceSource(PrehistoryState s,int polity,int target)
        {
            var a=s.History.Aggregates;var p=s.History.Polities[polity];var t=a.Empires.Territories;
            int best=-1;double effort=TerritorialServiceRadius(s,polity,target);
            void Consider(int source)
            {
                if(!TerritorialNodeLive(s,polity,source))return;
                double d=LivingTravelKm(s,TerritorialNodeCell(s,source),target,p.CultureId);
                if(d<effort||d==effort&&(best== -1||source<best)){best=source;effort=d;}
            }
            Consider(p.CapitalId);
            foreach(int next in World.Surface.Cells[target].Neighbors)if(HistoricalCells.Polity(s,next)==polity){Consider(t.Sources[next]);int city=a.TownAt[next];if(city>=0)Consider(city);int post=t.PostAt[next];if(post>=0)Consider(-2-post);}
            for(int trial=0;best== -1&&trial<Math.Min(8,a.Polities[polity].Cities.Count);trial++)Consider(a.Polities[polity].Cities.At((int)(HistoryRoll("territorial-service-"+target+"-"+trial,polity,s.Years)*a.Polities[polity].Cities.Count)));
            return best;
        }
        int TryEstablishTerritorialPost(PrehistoryState s,int polity,int target)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var g=a.Empires.Governments[polity];var t=a.Empires.Territories;var finances=a.Polities[polity];
            if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)||p.Cohesion<500||h.Dynamics.States[polity].PublicWorks<200)return -1;
            int maximum=Math.Min(128,2+(int)(Math.Sqrt(Math.Max(0,finances.Urban)/2500.0)*HistoricalModernEffects.AdministrativeCapacity(hc,s.Years)*3));
            if(g.AdministrativePosts.Count>=maximum||finances.Receipts<finances.ServiceCost*1.15)return -1;
            int choice=-1,parent=-1;double best=double.MaxValue;
            foreach(int next in World.Surface.Cells[target].Neighbors)
            {
                if(HistoricalCells.Polity(s,next)!=polity||t.PostAt[next]>=0||a.TownAt[next]>=0)continue;
                int source=t.Sources[next];if(!TerritorialNodeLive(s,polity,source))continue;
                double d=LivingTravelKm(s,TerritorialNodeCell(s,source),next,p.CultureId),radius=TerritorialServiceRadius(s,polity,next);
                if(d<radius*.55||d>radius*1.15)continue;
                int depth=source>=0?0:t.Posts[-source-2].Depth;if(depth>=7)continue;
                double advance=LivingTravelKm(s,next,target,p.CultureId);if(advance<best){choice=next;parent=source;best=advance;}
            }
            if(choice<0)return -1;
            int id=t.Posts.Length;double parentEffort=parent>=0?LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,s.Society.Settlements[parent].Cell,p.CultureId):t.Posts[-parent-2].MessageEffortKm;
            double distance=parentEffort+LivingTravelKm(s,TerritorialNodeCell(s,parent),choice,p.CultureId);
            double communications=HistoricalModernEffects.Communication(hc,s.Years),transport=HistoricalModernEffects.LandTransport(hc,s.Years);
            double burden=1+distance/(400*Math.Sqrt(communications*transport));
            long annual=Math.Max(30,(long)(60*burden)),setup=annual*(HistoricalTechnologies.Has(hc,HistoricalTechnology.Railways)?25:60);
            if(p.Treasury<setup+annual*30||finances.Receipts-finances.ServiceCost<annual*1.5)return -1;
            p.Treasury-=setup;var post=new HistoricalAdministrativePost{Id=id,PolityId=polity,Cell=choice,ParentSource=parent,Depth=parent>=0?1:t.Posts[-parent-2].Depth+1,FoundedYear=s.Years,AnnualCost=annual,MessageEffortKm=D.Q(distance,1000000),AutonomyMilli=(int)(700/Math.Sqrt(communications)),Name=HistoricalName("province",unchecked(choice+id*7919))+" Province"};
            if(LanguageHistory(s))post.Name=LanguageName(s,p.CultureId,LanguageNameKind.Region,unchecked(choice+id*7919));
            t.Posts=t.Posts.Concat(new[]{post}).ToArray();t.PostAt=t.PostAt.With(choice,id);g.AdministrativePosts.Add(id);g.AdministrativePostCost+=annual;
            AppendProvincialFrontier(s);
            AssignTerritorialSource(s,choice,-2-id);
            HistoryEvent(s,"administrative-post",p.Name+" establishes a funded local administration at "+post.Name+", extending its provincial network without creating a city or population.",choice,p.CultureId,polity,category:ChronicleCategory.Politics);
            return -2-id;
        }
        bool TerritorialIncorporationWorthwhile(PrehistoryState s,int polity,int target,int source)
        {
            if(LivingWorldHistory(s)&&!LivingTerritorialInterest(s,polity,target))return false;
            int center=source>=0?source:s.History.Polities[polity].CapitalId;
            if(CellPatch[target]>=0&&ProvincialIncorporationWorthwhile(s,polity,target,center))return true;
            var h=s.History;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var account=h.Aggregates.Polities[polity];
            double modern=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telegraph,s.Years)+HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years);
            if(modern<.5||account.Receipts<=account.ServiceCost||p.Cohesion<550)return false;
            // Paid strategic hinterland belongs to an actual reachable service
            // network, not free paint across empty deserts or polar ice.
            double terrain=1+World.Ecology.Habitat[target].ReliefM/1000.0;
            return p.Treasury>TerritorialPoliticalArea(target)*terrain*.3;
        }
        void AdvanceTerritorialAdministration(PrehistoryState s,int polity,int elapsed)
        {
            if(!TechnicalHistory(s))return;var h=s.History;var a=h.Aggregates;var g=a.Empires.Governments[polity];if(g.AdministrativePosts.Count==0)return;
            int at=(int)(HistoryRoll("provincial-office-review",polity,s.Years)*g.AdministrativePosts.Count),id=g.AdministrativePosts.At(at);var post=a.Empires.Territories.Posts[id];var account=a.Polities[polity];
            bool reachable=HistoricalCells.Polity(s,post.Cell)==polity&&TerritorialNodeLive(s,polity,post.ParentSource);
            bool paid=h.Polities[polity].Treasury>0||account.Receipts>=account.ServiceCost;
            if(reachable&&paid)return;
            if(reachable&&HistoryRoll("provincial-office-shortfall",id,s.Years)>1-Math.Exp(-elapsed/40.0))return;
            post.ClosedYear=s.Years;g.AdministrativePosts.Remove(id);g.AdministrativePostCost=Math.Max(0,g.AdministrativePostCost-post.AnnualCost);
            if(a.Empires.Territories.PostAt[post.Cell]==id)a.Empires.Territories.PostAt=a.Empires.Territories.PostAt.With(post.Cell,-1);
            HistoryEvent(s,"administrative-withdrawal",post.Name+" loses its funded connection to "+h.Polities[polity].Name+"; the local landscape and population remain.",post.Cell,h.Polities[polity].CultureId,polity,category:ChronicleCategory.Politics);
        }
    }
}

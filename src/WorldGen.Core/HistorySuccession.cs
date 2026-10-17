using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class HistoricalSuccessionEffects
    {
        public static double CrisisChance(int institutions,int strain,int formerRenown,int centers,bool hereditary)
            =>centers<3?0:D.Q(Math.Min(.30,(hereditary?.006:.002)+.035*(1-D.Clamp(institutions/1000.0))+.06*D.Clamp(strain/700.0)+.12*D.Clamp(formerRenown/700.0)),1000000000);
        public static bool RegionalBase(int centers,long regionalPeople,long totalUrbanPeople)
            =>centers>=3&&regionalPeople>=4000&&totalUrbanPeople>0&&(decimal)regionalPeople/totalUrbanPeople>=.18m;
        public static long DivideFunds(long balance,long localPeople,long urbanPeople)
            =>balance<=0||localPeople<=0||urbanPeople<=0?0:(long)((decimal)balance*Math.Min(localPeople,urbanPeople)/urbanPeople);
    }
    public sealed partial class Prehistory
    {
        bool HereditaryCourt(HistoricalPolity p,HistoricalStateDynamics ds)
            =>ds.Tradition=="Concentrated agrarian administration"||p.Government.IndexOf("kingdom",StringComparison.OrdinalIgnoreCase)>=0||p.Government.IndexOf("empire",StringComparison.OrdinalIgnoreCase)>=0||p.Government.IndexOf("hegemony",StringComparison.OrdinalIgnoreCase)>=0;
        void ConfigureHistoricalSuccession(PrehistoryState s,HistoricalPolity p,HistoricalStateDynamics ds,HistoricalLeader leader,HistoricalLeader former)
        {
            bool hereditary=HereditaryCourt(p,ds);leader.PredecessorId=former?.Id??-1;
            double tenure=HistoryRoll("leader-tenure",leader.Id,s.Years);
            leader.ExpectedEndYear=s.Years+(hereditary?18+(int)(32*tenure):8+(int)(12*tenure));
            leader.Selection=hereditary?"Court and household succession":ds.Tradition=="Port exchange and merchant councils"?"Appointment by merchant councils":"Appointment by local councils";
            if(!hereditary){leader.Administration=Math.Min(950,leader.Administration+80);leader.Diplomacy=Math.Min(950,leader.Diplomacy+50);}
            bool coup=former!=null&&hereditary&&HistoryRoll("house-transfer",leader.Id,s.Years)<.012+.06*D.Clamp((500-p.Legitimacy)/500.0)+.04*D.Clamp(ds.Strain/700.0);
            if(coup){ds.HouseSerial++;leader.Selection="Acclamation of a rival court and military supporters";p.Legitimacy=Math.Max(100,p.Legitimacy-80);}
            string name=HistoricalName("leader-name",unchecked(leader.Id*7919+s.Years));
            leader.Name=hereditary?name+" of the "+HistoricalName("leader-house",unchecked(p.Id+ds.HouseSerial*104729))+" House":name;
            if(coup)HistoryEvent(s,"dynasty-change",leader.Name+" replaces the ruling household of "+p.Name+" through a contested transfer of authority.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);
        }
        void ContestHistoricalSuccession(PrehistoryState s,HistoricalPolity p,HistoricalLeader leader,HistoricalLeader former,System.Threading.CancellationToken token)
        {
            if(former==null)return;var h=s.History;var ds=h.Dynamics.States[p.Id];var hc=h.Cultures[p.CultureId];
            bool politicalCrisis=RealismHistory(s)&&PoliticalSuccessionCrisis(s,p,former,leader,token);if(p.EndedYear>=0)return;
            var cities=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=2000&&t.FoodCapacity>=t.Population*.8&&s.Years-t.FoundedYear>=150).OrderBy(t=>t.Id).ToArray();
            if(RealismHistory(s))cities=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=500&&s.Years-t.FoundedYear>=25).OrderBy(t=>t.Id).ToArray();
            double chance=HistoricalSuccessionEffects.CrisisChance(hc.Institution,ds.Strain,former.Renown,cities.Length,HereditaryCourt(p,ds));
            if(!politicalCrisis&&HistoryRoll("contested-civic-succession",leader.Id,s.Years)>=chance)return;
            var rival=cities.Where(t=>t.Id!=p.CapitalId&&s.Society.Cultures[t.CultureId].Organization>=450&&h.Cultures[t.CultureId].Surplus>=350).OrderByDescending(t=>t.Population*(1+D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position)*World.Recipe.RadiusKm/1000.0)).ThenBy(t=>t.Id).FirstOrDefault();
            if(RealismHistory(s))rival=cities.Where(t=>t.Id!=p.CapitalId).OrderByDescending(t=>t.Population*(1+D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position)*World.Recipe.RadiusKm/500.0)).ThenBy(t=>t.Id).FirstOrDefault();
            if(rival==null)return;
            ds.DisputedUntil=s.Years+20;
            var bloc=cities.Where(t=>t.Id!=p.CapitalId&&D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[rival.Cell].Position)*World.Recipe.RadiusKm<=250).ToArray();
            long blocUrban=bloc.Sum(t=>t.Population),originalUrban=cities.Sum(t=>t.Population);
            if(!(RealismHistory(s)?blocUrban>=500&&blocUrban>=originalUrban*.08:HistoricalSuccessionEffects.RegionalBase(cities.Length,blocUrban,originalUrban)))
            {
                HistoryEvent(s,"succession-dispute","Competing factions challenge "+leader.Name+" at the court of "+p.Name+", but no viable regional court separates.",rival.Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);return;
            }
            long oldTreasury=p.Treasury;int oldArmy=p.Army;
            int newId=h.Polities.Length;FoundHistoricalPolity(s,rival,p.Id);
            var competing=h.Polities[newId];var challenger=CivicLeader(s,newId);
            foreach(var town in bloc.Where(t=>t.Id!=rival.Id))
            {town.PolityId=newId;town.Government=competing.Government;SetPoliticalOwner(s,town.Cell,newId);TransferCivicHinterland(s,town.Id,p.Id,newId);}
            // The existing ordered terrain-costed administration solver partitions
            // a regional bloc, rather than granting a whole cultural mask or a circle.
            var influence=CivicInfluence(s,token);
            foreach(int cell in PolityCells(s,p.Id).ToArray()){if((cell&2047)==0)token.ThrowIfCancellationRequested();if(influence[cell]==newId)SetPoliticalOwner(s,cell,newId);}
            foreach(var town in s.Society.Settlements.Where(t=>t.PolityId==p.Id&&h.CellPolity[t.Cell]==newId)){town.PolityId=newId;town.Government=competing.Government;}
            RefreshSplitPolityTotals(s,p.Id,newId,token);
            competing.Treasury=HistoricalSuccessionEffects.DivideFunds(oldTreasury,blocUrban,originalUrban);p.Treasury=oldTreasury-competing.Treasury;
            competing.Army=(int)HistoricalSuccessionEffects.DivideFunds(oldArmy,competing.Population,p.Population+competing.Population);p.Army=oldArmy-competing.Army;
            HistoryEvent(s,"succession-crisis",challenger.Name+" establishes a rival court at "+rival.Name+", contesting "+leader.Name+" after the end of "+former.Name+"'s rule in "+p.Name+".",rival.Cell,rival.CultureId,p.Id,newId,rival.Id,category:ChronicleCategory.Politics);
            // The separation uses an existing populated city and disjoint hinterland;
            // no new inhabitants, food reserves, treasury or army are conjured.
            int a=Math.Min(p.Id,newId),b=Math.Max(p.Id,newId);var relation=new HistoricalRelation{A=a,B=b,Trust=-70,WarSince=s.Years};
            h.Relations.Add(relation);
            BeginCivicCampaign(s,relation,p.Id,HistoricalWarAim.Reunification);
        }
        void RefreshSplitPolityTotals(PrehistoryState s,int parent,int child,System.Threading.CancellationToken token=default)
        {
            var h=s.History;h.Polities[parent].Population=0;h.Polities[parent].AreaKm2=0;h.Polities[child].Population=0;h.Polities[child].AreaKm2=0;
            foreach(int cell in PolityCells(s,parent).Concat(PolityCells(s,child)).Distinct().OrderBy(i=>i))
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();
                int id=h.CellPolity[cell];if(id!=parent&&id!=child)continue;
                h.Polities[id].Population+=RuralCellPopulation(s,cell);h.Polities[id].AreaKm2+=(long)Math.Round(HistoricalLandArea(cell));
            }
            foreach(var town in s.Society.Settlements)if(town.PolityId==parent||town.PolityId==child)h.Polities[town.PolityId].Population+=town.Population;
        }
    }
}

using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalPoliticalCycle
    {
        public int EliteCapture,ConquestDebt,LastConquestYear;
        public HistoricalPoliticalCycle Copy()=>(HistoricalPoliticalCycle)MemberwiseClone();
    }
    public static class HistoricalPoliticalCycleEffects
    {
        public static double CrisisChance(int capture,int debt,int strain,int exhaustion,int institutions,int formerRenown)
            =>D.Q(Math.Min(.85,(.015+.36*D.Clamp(capture/700.0)+.4*D.Clamp(debt/700.0)+.2*D.Clamp(strain/700.0)+.15*D.Clamp(exhaustion/700.0)+.15*D.Clamp(formerRenown/700.0))*(1-.45*D.Clamp(institutions/1000.0))),1000000000);
    }
    public sealed partial class Prehistory
    {
        void PoliticalCycleYear(PrehistoryState s,HistoricalPolity p,Settlement[] cities,bool war,HistoricalLeader leader)
        {
            var ds=s.History.Dynamics.States[p.Id];var cycle=ds.Cycle??(ds.Cycle=new HistoricalPoliticalCycle());
            bool reformed=leader.Administration>=750&&ds.PublicWorks>=200&&p.Legitimacy>=550;
            bool capture=p.Treasury>cities.Sum(t=>t.Population)*3||leader.Administration<550;
            cycle.EliteCapture=Math.Max(0,Math.Min(1000,cycle.EliteCapture+(reformed?-3:capture&&!war?1:0)));
            int accounting=HistoricalTechnologies.Has(s.History.Cultures[p.CultureId],HistoricalTechnology.Writing)?2:1;
            if(p.Treasury>0&&!war&&leader.Administration>=500)cycle.ConquestDebt=Math.Max(0,cycle.ConquestDebt-accounting);
            if(ds.PublicWorks>0)
            {
                long maintenance=Math.Max(1,ds.PublicWorks/4);
                if(p.Treasury>=maintenance)p.Treasury-=maintenance;else ds.PublicWorks=Math.Max(0,ds.PublicWorks-3);
                if(war||s.Years%4==0)ds.PublicWorks=Math.Max(0,ds.PublicWorks-1);
            }
            ds.Strain=Math.Min(700,ds.Strain+cycle.ConquestDebt/3);
        }
        bool PoliticalSuccessionCrisis(PrehistoryState s,HistoricalPolity p,HistoricalLeader former,HistoricalLeader incoming,CancellationToken token)
        {
            var ds=s.History.Dynamics.States[p.Id];var cycle=ds.Cycle??(ds.Cycle=new HistoricalPoliticalCycle());
            double chance=HistoricalPoliticalCycleEffects.CrisisChance(cycle.EliteCapture,cycle.ConquestDebt,ds.Strain,p.Exhaustion,s.History.Cultures[p.CultureId].Institution,former.Renown);
            if(HistoryRoll("political-transition-16",incoming.Id,s.Years)>=chance)return false;
            ds.DisputedUntil=s.Years+15+(int)(25*HistoryRoll("transition-dispute-16",incoming.Id,s.Years));
            p.Legitimacy=Math.Max(50,p.Legitimacy-180-cycle.ConquestDebt/5);p.Cohesion=Math.Max(50,p.Cohesion-120-cycle.ConquestDebt/4);
            var towns=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=500).OrderBy(t=>t.Id).ToArray();
            if(towns.Length>=2)return true; // Existing local courts can now contest a regional succession.
            // A city-state is not immune merely because it lacks three large
            // provincial capitals. A new regime inherits the real inhabitants,
            // assets and territory; this does not found a new civilization.
            var capital=s.Society.Settlements[p.CapitalId];long treasury=p.Treasury;int army=p.Army;
            int[] cells=PolityCells(s,p.Id).ToArray();var owned=s.Society.Settlements.Where(t=>t.PolityId==p.Id).ToArray();
            int id=s.History.Polities.Length;FoundHistoricalPolity(s,capital,p.Id,true);var successor=s.History.Polities[id];
            foreach(int cell in cells)SetPoliticalOwner(s,cell,id);
            foreach(var t in owned){t.PolityId=id;t.Government=successor.Government;s.History.Towns[t.Id].UnrestPolityId=id;s.History.Towns[t.Id].AutonomyPressure=0;}
            EndPolity(s,p,"The ruling constitution and court are replaced after a contested succession; the city and its people persist.");
            p.Treasury=0;successor.Treasury=treasury;successor.Army=army;successor.Legitimacy=450;successor.Cohesion=550;
            foreach(var t in owned)RetainConqueredAdministration(s,t,p,successor,t.Id==p.CapitalId,token);
            RefreshSplitPolityTotals(s,p.Id,id,token);return true;
        }
    }
}

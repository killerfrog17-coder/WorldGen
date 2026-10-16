using System;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalNuclearHistory
    {
        public string Version="nuclear-accounts-1";
        public HistoricalNuclearState[] States=Array.Empty<HistoricalNuclearState>();
        public HistoricalNuclearHistory Copy()=>new HistoricalNuclearHistory{Version=Version,States=States.Select(s=>s.Copy()).ToArray()};
    }
    [Serializable] public sealed class HistoricalNuclearState
    {
        public int Id,LastYear,ProgramYear=-1,FirstWeaponYear=-1,LastUseYear=-1;
        public int Stockpile,Produced,Inherited,Transferred,Dismantled,Used,ReadinessMilli,DeliveryMilli,DeterrenceEpisodes;
        public long ProductionWork,Investment,UpkeepPaid,CivilianDeaths;
        public HistoricalNuclearState Copy()=>(HistoricalNuclearState)MemberwiseClone();
    }
    public sealed class HistoricalNuclearProfile
    {
        public bool Available;
        public string Status="No recorded nuclear programme",Delivery="No deployed aircraft delivery";
        public int Stockpile,ReadinessMilli,ProgramYear=-1,FirstWeaponYear=-1,Used,Inherited,DeterrenceEpisodes;
        public long Investment,UpkeepPaid,InflictedCivilianDeaths;
    }
    // Abstract strategy-game units, not a model of weapons construction,
    // engineering, explosive yields or measured economic costs.
    public static class HistoricalNuclearArsenals
    {
        public const int MaximumStockpile=8192;
        public const long WeaponInvestment=250000;
        public static long ProductionFunding(long urban,long annualSurplus,long treasury,int elapsed,double industry,int uranium,double threat)
        {
            if(urban<100000||annualSurplus<=0||treasury<=0||elapsed<=0||industry<.35||uranium<=0||threat<=0)return 0;
            double material=Math.Sqrt(D.Clamp(uranium/180.0));
            double labor=Math.Max(0,urban)*.04*D.Clamp(industry)*material*D.Clamp(threat);
            return Math.Max(0,(long)Math.Min(treasury/4.0,Math.Min(annualSurplus*.12*Math.Max(0,elapsed),labor*Math.Max(0,elapsed))));
        }
        public static int DesiredStockpile(long urban,double threat)
            =>threat<=0?0:Math.Max(1,Math.Min(MaximumStockpile,(int)Math.Min(MaximumStockpile,Math.Sqrt(Math.Max(0,urban)/100000.0)*48*D.Clamp(threat))));
        public static double AttackProbability(int defenderWeapons,int defenderReadiness,int defenderDelivery,int attackerWeapons,int attackerReadiness)
        {
            double credible=D.Clamp(defenderReadiness/1000.0)*D.Clamp(defenderDelivery/1000.0);
            if(defenderWeapons<=0||credible<=0)return 1;
            double retaliation=credible*(.7+.3*D.Clamp(defenderWeapons/8.0));
            double mutual=attackerWeapons>0?D.Clamp(attackerReadiness/1000.0):0;
            return D.Q(Math.Max(.015,1-retaliation*(.92+.065*mutual)),1000000);
        }
        public static double EscalationChance(int elapsed,int exhaustion,int cohesion,double militaryThreat,bool retaliationRisk)
        {
            if(elapsed<=0||exhaustion<650||cohesion>400||militaryThreat<1.75)return 0;
            double desperation=D.Clamp((exhaustion-600)/350.0)*D.Clamp((450-cohesion)/400.0)*D.Clamp((militaryThreat-1)/4);
            return D.Q(1-Math.Exp(-Math.Max(0,elapsed)*(retaliationRisk?.00012:.0012)*desperation),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        public HistoricalNuclearProfile NuclearProfile(PrehistoryState s,int polity)
        {
            var view=new HistoricalNuclearProfile();var n=s?.History?.Nuclear;
            if(!LivingWorldHistory(s)||n==null||polity<0||polity>=n.States.Length)return view;
            var r=n.States[polity];view.Available=true;view.Stockpile=r.Stockpile;view.ReadinessMilli=r.ReadinessMilli;view.ProgramYear=r.ProgramYear;view.FirstWeaponYear=r.FirstWeaponYear;
            view.Used=r.Used;view.Inherited=r.Inherited;view.DeterrenceEpisodes=r.DeterrenceEpisodes;view.Investment=r.Investment;view.UpkeepPaid=r.UpkeepPaid;view.InflictedCivilianDeaths=r.CivilianDeaths;
            bool knowledge=HistoricalTechnologies.Has(s.History.Cultures[s.History.Polities[polity].CultureId],HistoricalTechnology.NuclearWeapons);
            view.Status=r.Stockpile>0?(r.ReadinessMilli>=200?"Maintained nuclear arsenal":"Weapons in custody; limited operational readiness")
                :r.Produced+r.Inherited>0?"Former nuclear arsenal":r.ProgramYear>=0?"Funded nuclear armament programme":knowledge?"Nuclear design knowledge; no physical arsenal":"No recorded nuclear armament programme";
            if(r.DeliveryMilli>=200)view.Delivery="Deployed aircraft delivery, limited by actual range";
            return view;
        }
        static void InitializeNuclearHistory(PrehistoryState s)
        {
            if(s.History.Nuclear!=null)return;
            s.History.Nuclear=new HistoricalNuclearHistory{States=s.History.Polities.Select(p=>new HistoricalNuclearState{Id=p.Id,LastYear=s.Years}).ToArray()};
        }
        static void AppendNuclearState(PrehistoryState s)
        {
            var nuclear=s.History.Nuclear;int id=s.History.Polities.Length-1;
            Array.Resize(ref nuclear.States,id+1);nuclear.States[id]=new HistoricalNuclearState{Id=id,LastYear=s.Years};
        }
        void ValidateNuclearHistory(PrehistoryState s)
        {
            var n=s.History.Nuclear;
            if(!LivingWorldHistory(s)){if(n!=null)throw new ArgumentException("Legacy history has nuclear accounts.");return;}
            if(n?.Version!="nuclear-accounts-1"||n.States?.Length!=s.History.Polities.Length)throw new ArgumentException("Nuclear actor alignment.");
            for(int id=0;id<n.States.Length;id++)
            {
                var r=n.States[id];
                if(r==null||r.Id!=id||r.LastYear<0||r.LastYear>s.Years||r.ProgramYear< -1||r.ProgramYear>s.Years||r.FirstWeaponYear< -1||r.FirstWeaponYear>s.Years||r.LastUseYear< -1||r.LastUseYear>s.Years
                    ||r.Stockpile<0||r.Stockpile>HistoricalNuclearArsenals.MaximumStockpile||r.Produced<0||r.Inherited<0||r.Transferred<0||r.Dismantled<0||r.Used<0||r.DeterrenceEpisodes<0
                    ||r.Stockpile!=(long)r.Produced+r.Inherited-r.Transferred-r.Dismantled-r.Used||r.ReadinessMilli<0||r.ReadinessMilli>1000||r.DeliveryMilli<0||r.DeliveryMilli>1000
                    ||r.ProductionWork<0||r.ProductionWork>=HistoricalNuclearArsenals.WeaponInvestment||r.Investment<0||r.UpkeepPaid<0||r.CivilianDeaths<0)throw new ArgumentException("Invalid nuclear custody, production or readiness account.");
            }
        }
        void AdvanceNuclearState(PrehistoryState s,int id,int elapsed)
        {
            // The arsenal owns its maintenance date. A continuing government
            // can be newer than its inherited unpaid maintenance interval.
            var h=s.History;var record=h.Nuclear.States[id];elapsed=Math.Max(0,s.Years-record.LastYear);if(elapsed<=0)return;record.LastYear=s.Years;
            var p=h.Polities[id];var account=h.Aggregates.Polities[id];var hc=h.Cultures[p.CultureId];
            double fission=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.NuclearFission,s.Years),weapons=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.NuclearWeapons,s.Years);
            if(record.Stockpile==0&&(fission<.35||weapons<.35))return;
            double industry=Math.Min(fission,Math.Min(weapons,Math.Min(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.IndustrialMetallurgy,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Electricity,s.Years))));
            double aviation=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Aviation,s.Years);
            record.DeliveryMilli=(int)(D.Clamp(aviation)*1000);
            long maintenance=Math.Min(long.MaxValue/2,(long)record.Stockpile*1200*Math.Max(0,elapsed)),paid=Math.Min(p.Treasury,maintenance);
            p.Treasury-=paid;record.UpkeepPaid+=paid;
            double funded=maintenance>0?paid/(double)maintenance:1;
            int target=(int)(1000*industry*(.3+.7*h.Dynamics.States[id].PublicWorks/1000.0)*funded);
            record.ReadinessMilli=Math.Max(0,Math.Min(1000,record.ReadinessMilli+(int)((target-record.ReadinessMilli)*Math.Min(1,elapsed/5.0))));
            if(record.Stockpile>0&&funded<.5)
            {
                int retire=Math.Min(record.Stockpile,(int)Math.Ceiling(record.Stockpile*(1-funded)*Math.Min(1,elapsed/15.0)));
                record.Stockpile-=retire;record.Dismantled+=retire;
            }
            if(industry<.35||hc.Institution<600||h.Dynamics.States[id].PublicWorks<300)return;
            double war=0,threat=0;var cities=account.Cities;int offset=cities.Count==0?0:(int)(HistoryRoll("nuclear-security-sites",id,s.Years)*cities.Count),supported=0;
            for(int k=0;k<Math.Min(3,cities.Count);k++)
            {
                int city=cities.At((offset+k)%cities.Count);if(!h.Aggregates.Cities[city].Supported)continue;
                supported++;NuclearSecurityAt(s,city,id,ref war,ref threat);
            }
            threat=Math.Max(war,threat);if(threat<=0||cities.Count==0)return;
            long urban=account.Urban*supported/Math.Max(1,Math.Min(3,cities.Count));
            int desired=HistoricalNuclearArsenals.DesiredStockpile(urban,threat);if(record.Stockpile>=desired)return;
            int capital=s.Society.Settlements[p.CapitalId].Cell;
            int uranium=LivingResourceMarket(s,p.CultureId,capital).Uranium;
            long funds=HistoricalNuclearArsenals.ProductionFunding(urban,account.Receipts-account.ServiceCost,p.Treasury,elapsed,industry,uranium,threat);
            long needed=(desired-record.Stockpile)*HistoricalNuclearArsenals.WeaponInvestment-record.ProductionWork;funds=Math.Min(funds,Math.Max(0,needed));if(funds<=0)return;
            if(record.ProgramYear<0)record.ProgramYear=s.Years;
            p.Treasury-=funds;record.Investment+=funds;record.ProductionWork+=funds;
            int produced=(int)(record.ProductionWork/HistoricalNuclearArsenals.WeaponInvestment);record.ProductionWork%=HistoricalNuclearArsenals.WeaponInvestment;
            record.Stockpile+=produced;record.Produced+=produced;
            if(produced>0&&record.FirstWeaponYear<0)
            {
                record.FirstWeaponYear=s.Years;
                HistoryEvent(s,"nuclear-arsenal",p.Name+" commissions its first nuclear arsenal, funding the production and upkeep of weapons for aircraft delivery.",capital,p.CultureId,id,category:ChronicleCategory.Politics);
            }
        }
        bool NuclearCanReach(PrehistoryState s,int owner,int target)
        {
            var h=s.History;var n=h.Nuclear.States[owner];if(n.Stockpile<=0||n.ReadinessMilli<200||n.DeliveryMilli<200||h.Polities[owner].EndedYear>=0)return false;
            var hc=h.Cultures[h.Polities[owner].CultureId];int level=hc.Technical.Levels[(int)HistoricalTechnology.Aviation];
            double range=Math.Min(18000,1000+1500*Math.Max(0,level));
            int from=s.Society.Settlements[h.Polities[owner].CapitalId].Cell,to=s.Society.Settlements[h.Polities[target].CapitalId].Cell;
            return D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm<=range;
        }
        bool NuclearWarDeterred(PrehistoryState s,int attacker,int defender)
        {
            if(!NuclearCanReach(s,defender,attacker))return false;var n=s.History.Nuclear;var a=n.States[attacker];var b=n.States[defender];
            double chance=HistoricalNuclearArsenals.AttackProbability(b.Stockpile,b.ReadinessMilli,b.DeliveryMilli,NuclearCanReach(s,attacker,defender)?a.Stockpile:0,a.ReadinessMilli);
            if(HistoryRoll("nuclear-war-deterrence-"+defender,attacker,s.Years)<chance)return false;
            b.DeterrenceEpisodes++;return true;
        }
        void CloseNuclearState(PrehistoryState s,int id,int successor,bool continuity=false)
        {
            var record=s.History.Nuclear.States[id];
            if(successor>=0&&successor!=id)
            {
                var heir=s.History.Nuclear.States[successor];int previous=heir.Stockpile,transfer=Math.Min(record.Stockpile,HistoricalNuclearArsenals.MaximumStockpile-heir.Stockpile);
                heir.Stockpile+=transfer;heir.Inherited+=transfer;record.Stockpile-=transfer;record.Transferred+=transfer;
                // Custody is not the former government's crew or deployment.
                if(heir.Stockpile>0)heir.ReadinessMilli=(int)(((long)heir.ReadinessMilli*previous+(continuity?(long)record.ReadinessMilli*transfer:0))/heir.Stockpile);
                if(continuity){heir.LastYear=record.LastYear;heir.DeliveryMilli=record.DeliveryMilli;heir.ProductionWork=record.ProductionWork;heir.ProgramYear=record.ProgramYear;heir.FirstWeaponYear=record.FirstWeaponYear;heir.LastUseYear=record.LastUseYear;}
            }
            record.ProductionWork=0;record.Dismantled+=record.Stockpile;record.Stockpile=0;record.ReadinessMilli=0;record.DeliveryMilli=0;
        }
        void TryNuclearEscalation(PrehistoryState s,int campaign)
        {
            var c=s.History.Dynamics.Campaigns[campaign];if(c.EndedYear>=0)return;
            int elapsed=s.Years-c.LastActionYear;if(elapsed<=0)return;
            for(int side=0;side<2;side++)
            {
                int actor=side==0?c.A:c.B,rival=side==0?c.B:c.A;var h=s.History;var p=h.Polities[actor];var other=h.Polities[rival];var account=h.Nuclear.States[actor];
                if(p.EndedYear>=0||other.EndedYear>=0||account.LastUseYear>=s.Years-10||!NuclearCanReach(s,actor,rival))continue;
                double threat=Math.Max(1,other.Army)*HistoricalModernEffects.Military(h.Cultures[other.CultureId],s.Years)/Math.Max(1,p.Army*HistoricalModernEffects.Military(h.Cultures[p.CultureId],s.Years));
                double chance=HistoricalNuclearArsenals.EscalationChance(elapsed,p.Exhaustion,p.Cohesion,threat,NuclearCanReach(s,rival,actor));
                if(HistoryRoll("nuclear-existential-escalation-"+campaign,actor,s.Years)>=chance)continue;
                ExecuteNuclearStrike(s,campaign,actor,rival);break;
            }
        }
        bool ExecuteNuclearStrike(PrehistoryState s,int campaign,int actor,int rival)
        {
            var h=s.History;if(actor==rival||campaign<0||campaign>=h.Dynamics.Campaigns.Length||h.Polities[rival].EndedYear>=0||!NuclearCanReach(s,actor,rival))return false;var c=h.Dynamics.Campaigns[campaign];
            if(c.EndedYear>=0||!(c.A==actor&&c.B==rival||c.A==rival&&c.B==actor))return false;
            var p=h.Polities[actor];var other=h.Polities[rival];int city=other.CapitalId;var town=s.Society.Settlements[city];var farms=h.Aggregates.Cities[city];
            if(h.Aggregates.Resolve(town.PolityId,h)!=rival||town.Population<=0)return false;
            var record=h.Nuclear.States[actor];record.Stockpile--;record.Used++;record.LastUseYear=s.Years;
            // Only the actual selected center's existing accounts are damaged.
            // No inferred remote population, fabricated blast geography, terrain
            // changes, guaranteed conquest or world-extinction shortcut.
            long urban=(long)(town.Population*.35),suburban=(long)(farms.Suburban*.12),rural=(long)(farms.Rural*.03),deaths=urban+suburban+rural;
            ChangeAggregateCityPopulation(s,city,town.Population-urban,farms.Suburban-suburban,farms.Rural-rural);
            record.CivilianDeaths+=deaths;if(rival==c.A)c.CasualtiesA+=deaths;else c.CasualtiesB+=deaths;
            town.FoodReserve=town.FoodReserve/3;town.Stability=Math.Max(0,town.Stability-450);h.Aggregates.Living.Cities[city].Productivity=D.Q(h.Aggregates.Living.Cities[city].Productivity*.65,1000000);
            other.Exhaustion=Math.Min(1000,other.Exhaustion+200);p.Exhaustion=Math.Min(1000,p.Exhaustion+100);p.Legitimacy=Math.Max(0,p.Legitimacy-200);
            var relation=AggregateRelation(s,actor,rival);relation.Trust=-100;relation.Trade=relation.Alliance=false;
            var cities=h.Aggregates.Polities[actor].Cities;for(int k=0;k<Math.Min(3,cities.Count);k++)
            foreach(int peer in h.Aggregates.Cities[cities.At(k)].Neighbors)
            {
                int witness=h.Aggregates.Resolve(s.Society.Settlements[peer].PolityId,h);if(witness<0||witness==actor)continue;
                var diplomacy=AggregateRelation(s,actor,witness);diplomacy.Trust=Math.Max(-100,diplomacy.Trust-50);diplomacy.Alliance=false;
            }
            HistoryEvent(s,"nuclear-strike",p.Name+" uses a nuclear weapon against "+town.Name+" during "+c.Name+". "+deaths+" residents die, food reserves are destroyed and local production is disrupted.",town.Cell,town.CultureId,actor,rival,town:city,category:ChronicleCategory.Politics);
            return true;
        }
    }
}

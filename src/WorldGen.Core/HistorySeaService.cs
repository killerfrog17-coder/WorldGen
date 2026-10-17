using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalSeaService
    {
        public int Version=1;
        public HistoricalCellTree BySource=new HistoricalCellTree(33554432);
        public HistoricalSeaServicePort[] Ports=Array.Empty<HistoricalSeaServicePort>();
        public HistoricalSeaService Copy()=>new HistoricalSeaService{Version=Version,BySource=BySource,Ports=Ports.Select(p=>p.Copy()).ToArray()};
    }
    [Serializable] public sealed class HistoricalSeaServicePort
    {
        public int Key,TownId=-1,Cell,OwnerId;
        public long LastSeconds,Capacity,ExpansionCredit,CargoDayCredit,ConstructionSpending,CharterSpending,CrewFood,WorkSpent;
        public HistoricalSeaServicePort Copy()=>(HistoricalSeaServicePort)MemberwiseClone();
    }
    // Authored aggregate utilization priors. A cargo unit is a person-year's
    // food-equivalent payload, NOT a measured ton; credits are cargo-unit days,
    // NOT a literal inventory of individual ships. Local crews remain people
    // in the existing community, not an additional demographic cohort.
    public static class HistoricalSeaServiceEffects
    {
        public static double CrewPayload(double sailing,double ocean,double steam,double engines)
            =>2+3*D.Clamp(sailing)+8*D.Clamp(ocean)+12*D.Clamp(steam)+18*D.Clamp(engines);
        public static long Capacity(long urban,long rural,double sailing,double ocean,double workshops,double steam,double engines)
        {
            // A tiny named harbor cannot turn its entire large agricultural
            // catchment into shipyards. A genuine rural craft-cell actor has
            // no named urban population and uses its own local residents only.
            double localRural=urban>0?Math.Min(Math.Max(0,rural),4.0*urban):Math.Max(0,rural);
            return Math.Max(0,(long)Math.Floor((.04*Math.Max(0,urban)+.01*localRural)*CrewPayload(sailing,ocean,steam,engines)*(.2+.8*D.Clamp(workshops))));
        }
        public static double RoundTripDays(double km,double ocean,double steam,double engines)
            =>30+2*Math.Max(0,km)/(55+65*D.Clamp(ocean)+220*D.Clamp(steam)+240*D.Clamp(engines));
        public static long Work(double cargo,long passengers,double days)
            =>Math.Max(0,(long)Math.Ceiling((Math.Max(0,cargo)+.25*Math.Max(0,passengers))*Math.Max(0,days)));
        public static long Credit(long prior,long capacity,double elapsedDays)
            =>Math.Max(0,(long)Math.Min(Math.Max(0,capacity)*365.0,Math.Max(0,prior)+Math.Max(0,capacity)*Math.Max(0,elapsedDays)));
    }
    public sealed partial class Prehistory
    {
        sealed class SeaServiceQuote
        {
            public bool Allowed,Partial;
            public int Polity,Supplier,Departure,Target,Key,Record,ExpectedOwner;
            public long Now,ExpectedTime,ExpectedCredit,Cargo,Passengers,Work,AdditionalCrewFood,Construction,Charter,CashHeld,FoodHeld;
            public double CrewCovered;
            public HistoricalSeaServicePort Next;
        }
        bool SeaServiceHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.SeaService!=null;
        void InitializeSeaService(PrehistoryState s)
        {var c=s.History.Aggregates.Empires.Connections;if(c.SeaService==null)c.SeaService=new HistoricalSeaService();}
        // Several beaches supplied by one nearby working port share ONE craft
        // actor. The inland granary is deliberately not the maritime actor.
        bool SeaServiceActor(PrehistoryState s,int polity,int departure,out int key,out int town,out long urban,out long rural)
        {
            key=-1;town=-1;urban=0;rural=0;var a=s.History.Aggregates;
            bool Working(int id)
            {
                if(id<0||id>=a.Cities.Length)return false;var t=s.Society.Settlements[id];
                double craft=Math.Max(HistoricalModernEffects.Adoption(s.History.Cultures[t.CultureId],HistoricalTechnology.Sailing,s.Years),HistoricalModernEffects.Adoption(s.History.Cultures[s.History.Polities[polity].CultureId],HistoricalTechnology.Sailing,s.Years));
                return a.Resolve(t.PolityId,s.History)==polity&&HistoricalCells.Polity(s,t.Cell)==polity&&HistoricalMaritimePracticeEffects.WorkingPort(a.Living.Cities[id].AbandonedYear<0,a.Cities[id].Supported,DiffusionShore(t.Cell),t.Population,t.FoodReserve,craft);
            }
            int direct=a.TownAt[departure];if(Working(direct))town=direct;
            if(town<0)
            {
                int count=0;double best=double.MaxValue;
                foreach(int id in NearbyLivingTowns(s,departure,180))
                {
                    if(++count>12)break;if(!Working(id)||!InstitutionalRouteAllowed(s,s.Society.Settlements[id].Cell,departure,s.History.Polities[polity].CultureId))continue;
                    double distance=D.Angle(World.Surface.Cells[departure].Position,World.Surface.Cells[s.Society.Settlements[id].Cell].Position);
                    if(distance<best||distance==best&&id<town){town=id;best=distance;}
                }
            }
            if(town>=0){key=checked(town*2);urban=s.Society.Settlements[town].Population;rural=a.Cities[town].Rural;return true;}
            if(!LivingStateRuralMaritime(s,polity,departure))return false;
            key=checked(departure*2+1);rural=AggregateRuralCellPopulation(s,departure);return rural>0;
        }
        SeaServiceQuote QuoteSeaService(PrehistoryState s,int polity,int supplier,int departure,int target,long cargo,long passengers,long cashHeld=0,long foodHeld=0,bool partial=false,double crewCovered=0)
        {
            var q=new SeaServiceQuote{Polity=polity,Supplier=supplier,Departure=departure,Target=target,Cargo=Math.Max(0,cargo),Passengers=Math.Max(0,passengers),CashHeld=Math.Max(0,cashHeld),FoodHeld=Math.Max(0,foodHeld),Partial=partial,CrewCovered=Math.Max(0,crewCovered)};
            if(!SeaServiceHistory(s)){q.Allowed=true;return q;}
            var a=s.History.Aggregates;var h=s.History;
            if(polity<0||polity>=h.Polities.Length||h.Polities[polity].EndedYear>=0||supplier<0||supplier>=a.Cities.Length||a.Resolve(s.Society.Settlements[supplier].PolityId,h)!=polity||a.Living.Cities[supplier].AbandonedYear>=0||!a.Cities[supplier].Supported)return q;
            if(!SeaServiceActor(s,polity,departure,out int key,out int town,out long urban,out long rural))return q;
            var ledger=a.Empires.Connections.SeaService;int record=ledger.BySource[key];var saved=record>=0?ledger.Ports[record]:null;long now=SimulationTime.Seconds(s);
            q.Key=key;q.Record=record;q.Now=now;q.ExpectedOwner=saved?.OwnerId??-1;q.ExpectedTime=saved?.LastSeconds??-1;q.ExpectedCredit=saved?.CargoDayCredit??-1;
            var hc=h.Cultures[h.Polities[polity].CultureId];double At(HistoricalTechnology t)=>HistoricalModernEffects.Adoption(hc,t,s.Years);
            double sailing=At(HistoricalTechnology.Sailing),ocean=At(HistoricalTechnology.OceanNavigation),steam=At(HistoricalTechnology.SteamPower),engines=At(HistoricalTechnology.CombustionEngines);
            double workshops=Math.Max(At(HistoricalTechnology.IndustrialMetallurgy),.45*Math.Max(At(HistoricalTechnology.Copper),At(HistoricalTechnology.Iron))+.55*At(HistoricalTechnology.Mills));
            long targetCapacity=HistoricalSeaServiceEffects.Capacity(urban,rural,sailing,ocean,workshops,steam,engines);if(targetCapacity<=0)return q;
            bool same=saved!=null&&saved.OwnerId==polity&&saved.TownId==town&&saved.LastSeconds<=now;
            double actualElapsed=same?(now-saved.LastSeconds)/(double)SimulationTime.Year:0,elapsed=Math.Min(2,actualElapsed);
            long installed=same?Math.Min(targetCapacity,(long)Math.Floor(saved.Capacity*Math.Exp(-.04*actualElapsed))):0;
            long credit=same?HistoricalSeaServiceEffects.Credit(saved.CargoDayCredit,installed,elapsed*365):0;
            // A first contract charters a bounded share of an already occupied
            // craft port. Further paid expansion has a local workshop envelope,
            // not an arbitrary decades-long waiting gate or unlimited cash buy.
            double km=(D.Angle(World.Surface.Cells[departure].Position,World.Surface.Cells[target].Position)+(town>=0?D.Angle(World.Surface.Cells[s.Society.Settlements[town].Cell].Position,World.Surface.Cells[departure].Position):0))*World.Recipe.RadiusKm,days=HistoricalSeaServiceEffects.RoundTripDays(km,ocean,steam,engines),payload=HistoricalSeaServiceEffects.CrewPayload(sailing,ocean,steam,engines);
            long market=Math.Min(targetCapacity-installed,Math.Max(0,(same?saved.ExpansionCredit:0)+(long)Math.Floor(targetCapacity*(same?elapsed*(.2+.5*workshops+.4*steam+.6*engines):.25))));
            // Buying a charter purchases at most one year's utilization. Use
            // the same term on every transaction so splitting a booking cannot
            // mint another initial fleet or alter its physical market envelope.
            const int charterDays=365;
            long desired=HistoricalSeaServiceEffects.Work(q.Cargo,q.Passengers,days),needed=(long)Math.Max(0,Math.Ceiling((desired-(double)credit)/charterDays));
            long affordable=(long)Math.Max(0,Math.Floor((h.Polities[polity].Treasury-q.CashHeld)/(8+charterDays*.002)));
            long expansion=Math.Min(market,Math.Min(needed,affordable)),construction=checked(expansion*8);installed+=expansion;market-=expansion;
            credit=Math.Min(installed*365,credit+expansion*charterDays);
            long cash=Math.Max(0,h.Polities[polity].Treasury-q.CashHeld-construction),food=Math.Max(0,s.Society.Settlements[supplier].FoodReserve-q.FoodHeld);
            long possible=(long)Math.Max(0,Math.Floor(credit/days-.25*q.Passengers));
            possible=Math.Min(possible,(long)Math.Max(0,Math.Floor(cash/(days*.002)-.25*q.Passengers)));
            possible=Math.Min(possible,(long)Math.Max(0,Math.Floor((food+q.CrewCovered-.25*q.Passengers*days/(payload*365))/(1+days/(payload*365)))));
            possible=Math.Min(possible,food);
            if(q.Partial)q.Cargo=Math.Min(q.Cargo,possible);else if(q.Cargo>possible)return q;
            if(q.Cargo==0&&q.Passengers==0)return q;
            q.Work=HistoricalSeaServiceEffects.Work(q.Cargo,q.Passengers,days);q.AdditionalCrewFood=Math.Max(0,(long)Math.Ceiling(q.Work/(payload*365)-q.CrewCovered));q.Construction=construction;q.Charter=Math.Max(1,(long)Math.Ceiling(q.Work*.002));
            if(q.Work>credit||h.Polities[polity].Treasury<q.CashHeld+construction+q.Charter||s.Society.Settlements[supplier].FoodReserve<q.FoodHeld+q.Cargo+q.AdditionalCrewFood)return q;
            q.Next=new HistoricalSeaServicePort{Key=key,TownId=town,Cell=town>=0?s.Society.Settlements[town].Cell:departure,OwnerId=polity,LastSeconds=now,Capacity=installed,ExpansionCredit=market,CargoDayCredit=credit-q.Work,
                ConstructionSpending=(saved?.ConstructionSpending??0)+construction,CharterSpending=(saved?.CharterSpending??0)+q.Charter,CrewFood=(saved?.CrewFood??0)+q.AdditionalCrewFood,WorkSpent=(saved?.WorkSpent??0)+q.Work};
            q.Allowed=true;return q;
        }
        bool CommitSeaService(PrehistoryState s,SeaServiceQuote quote)
        {
            if(!SeaServiceHistory(s))return true;if(quote==null||!quote.Allowed)return false;
            var q=QuoteSeaService(s,quote.Polity,quote.Supplier,quote.Departure,quote.Target,quote.Cargo,quote.Passengers,quote.CashHeld,quote.FoodHeld,quote.Partial,quote.CrewCovered);
            if(!q.Allowed||q.Key!=quote.Key||q.Now!=quote.Now||q.Cargo!=quote.Cargo||q.Record!=quote.Record||q.ExpectedOwner!=quote.ExpectedOwner||q.ExpectedTime!=quote.ExpectedTime||q.ExpectedCredit!=quote.ExpectedCredit||q.Work!=quote.Work)return false;
            var ledger=s.History.Aggregates.Empires.Connections.SeaService;
            if(q.Record<0){int id=ledger.Ports.Length;ledger.Ports=ledger.Ports.Concat(new[]{q.Next}).ToArray();ledger.BySource=ledger.BySource.With(q.Key,id);}else ledger.Ports[q.Record]=q.Next;
            s.History.Polities[q.Polity].Treasury-=q.Construction+q.Charter;s.Society.Settlements[q.Supplier].FoodReserve-=q.AdditionalCrewFood;return true;
        }
        void ValidateSeaService(PrehistoryState s)
        {
            if(!SeaServiceHistory(s))return;var a=s.History.Aggregates;var ledger=a.Empires.Connections.SeaService;
            if(ledger.Version!=1||ledger.BySource?.Length!=33554432||ledger.Ports==null)throw new ArgumentException("Invalid maritime service ledger.");
            for(int id=0;id<ledger.Ports.Length;id++)
            {
                var p=ledger.Ports[id];if(p==null||p.Key<0||ledger.BySource[p.Key]!=id||p.TownId< -1||p.TownId>=a.Cities.Length||p.Cell<0||p.Cell>=CellPatch.Length||p.OwnerId<0||p.OwnerId>=s.History.Polities.Length||p.LastSeconds<0||p.LastSeconds>SimulationTime.Seconds(s)||p.Capacity<0||p.ExpansionCredit<0||p.CargoDayCredit<0||p.CargoDayCredit>p.Capacity*365||p.ConstructionSpending<0||p.CharterSpending<0||p.CrewFood<0||p.WorkSpent<0||p.Key!=(p.TownId>=0?p.TownId*2:p.Cell*2+1))throw new ArgumentException("Invalid coastal craft service account.");
            }
        }
    }
}

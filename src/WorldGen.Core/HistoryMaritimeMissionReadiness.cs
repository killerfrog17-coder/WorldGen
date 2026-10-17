using System;
namespace WorldGen.Core
{
    // Navigational accuracy and the age of witnessed coastal facts are distinct.
    // No new omniscient information enters a chart through either operation.
    public static class HistoricalMaritimeMissionEffects
    {
        public static bool RecordWitnessed(HistoricalGeographicMap map,int region,int grade,int year,int opportunity,int host,int shore)
        {
            if(grade<1||grade>3||region<0||region>=map.Grade.Length)throw new ArgumentException("Invalid geographical report.");
            int previous=map.Grade[region],date=map.ReportedYear[region];
            bool navigation=grade>previous;
            bool facts=previous<2?grade>=previous&&(year>date||navigation):grade>=2&&year>date;
            if(!navigation&&!facts)return false;
            map.Places.Add(region);if(navigation)map.Grade=map.Grade.With(region,grade);
            bool changed=false;
            if(facts)
            {
                opportunity=Math.Max(0,Math.Min(3000,opportunity));
                changed=map.Host[region]!=host||map.Shore[region]!=shore||map.Opportunity[region]!=opportunity;
                map.ReportedYear=map.ReportedYear.With(region,year);map.Opportunity=map.Opportunity.With(region,opportunity);
                map.Host=map.Host.With(region,host);map.Shore=map.Shore.With(region,shore);
            }
            return navigation||changed;
        }
        public static bool ShareWitnessed(HistoricalGeographicMap sender,HistoricalGeographicMap receiver,int region,int deliveredGrade)
            =>deliveredGrade>receiver.Grade[region]||deliveredGrade>=2&&sender.ReportedYear[region]>receiver.ReportedYear[region]
                &&(sender.Host[region]!=receiver.Host[region]||sender.Shore[region]!=receiver.Shore[region]||Math.Abs(sender.Opportunity[region]-receiver.Opportunity[region])>=100);
        public static bool ExpeditionReadiness(int cohesion,double bureaucracy,int exhaustion,int availableArmy,int ambition,long treasury,long surplus,long investment)
            =>cohesion>=600-250*D.Clamp(bureaucracy)&&exhaustion<500&&availableArmy>=500&&ambition>=650&&treasury>=investment*3&&surplus>investment/100;
    }
    public sealed partial class Prehistory
    {
        bool RevisedMaritimeMissionHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=11;
        bool RecordGeographicFacts(PrehistoryState s,HistoricalGeographicMap map,int region,int grade,int year,int opportunity,int host,int shore)
            =>RevisedMaritimeMissionHistory(s)?HistoricalMaritimeMissionEffects.RecordWitnessed(map,region,grade,year,opportunity,host,shore):map.Record(region,grade,year,opportunity,host,shore);
        bool OwnMaritimeExpeditionReady(PrehistoryState s,int polity,Route route,out long investment)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];
            investment=Math.Max(5000,p.Army/2+route.DistanceM/500);
            int available=Math.Max(0,p.Army-a.Empires.Connections.Military.Governments[polity].Reserved);
            return HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation)&&HistoricalMaritimeMissionEffects.ExpeditionReadiness(p.Cohesion,RealmBureaucracy(s,polity),p.Exhaustion,available,
                h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId].Ambition,p.Treasury,a.Polities[polity].Receipts-a.Polities[polity].ServiceCost,investment);
        }
        // The same finite food, pay, source and hull constraints as a campaign's
        // field preparation, quoted without reservation/payment at declaration.
        double QuotedMaritimeFieldPower(PrehistoryState s,int polity,int target,int preferred,long cashHeld)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];int supply=preferred;
            int available=Math.Max(0,p.Army-a.Empires.Connections.Military.Governments[polity].Reserved);
            if(available<20||ProvincialFieldPower(s,polity,target,ref supply,available)<=0||supply<0)return 0;
            int men=QuotedMaritimeMen(s,polity,target,supply,cashHeld);
            return men<20?0:ProvincialFieldPower(s,polity,target,ref supply,men);
        }
        int QuotedMaritimeMen(PrehistoryState s,int polity,int target,int supply,long cashHeld)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];
            int available=Math.Max(0,p.Army-a.Empires.Connections.Military.Governments[polity].Reserved);
            if(supply<0||supply>=a.Cities.Length||a.Resolve(s.Society.Settlements[supply].PolityId,h)!=polity||!a.Cities[supply].Supported||a.Living.Cities[supply].AbandonedYear>=0)return 0;
            var source=s.Society.Settlements[supply];double km=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[target].Position)*World.Recipe.RadiusKm;
            double foodPerMan=.35*(1+km/2000),payPerMan=1.5;
            int men=(int)Math.Min(Math.Min(available,p.Army/2),Math.Min(Math.Max(0,source.FoodReserve-source.Population/4)/foodPerMan,Math.Max(0,p.Treasury-cashHeld)/payPerMan));
            if(SeaServiceHistory(s)&&(World.Landmass[source.Cell]!=World.Landmass[target]||InstitutionalUnsupportedWaterKm(source.Cell,target,out _)>35))
            {
                var transport=QuoteSeaService(s,polity,supply,source.Cell,target,(long)Math.Ceiling(men*(foodPerMan+.25)),0,cashHeld,source.Population/4,true);
                men=transport.Allowed?(int)Math.Min(men,Math.Floor(transport.Cargo/(foodPerMan+.25))):0;
            }
            return men<20?0:men;
        }
        bool ReadyOwnedColonialPort(PrehistoryState s,int polity,Route route,int supplier)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];int target=route.CellB,id=a.Empires.Territories.PostAt[target];
            if(id<0||HistoricalCells.Polity(s,target)!=polity||a.TownAt[target]>=0||p.Cohesion<CivilMaritimeCohesionFloor(s,polity,500)||!LivingTerritorialInterest(s,polity,target))return false;
            var post=a.Empires.Territories.Posts[id];if(!post.Maritime||post.ClosedYear>=0||!TerritorialNodeLive(s,polity,-2-id))return false;
            var source=s.Society.Settlements[supplier];
            if(!CanFoundLivingTown(s,source.CultureId,target,supplier))return CanPrepareProvisionedPort(s,polity,route,supplier,out _);
            long available=LivingFounderAvailability(s,supplier),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
            double staging=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
            long provisions=HistoricalVoyageEffects.Provisioning(people,(int)Math.Min(int.MaxValue,route.DistanceM+staging*1000)),investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(people,route.DistanceM);
            return p.Treasury>=investment*2&&source.FoodReserve>=people+provisions+source.Population/4
                &&QuoteSeaService(s,polity,supplier,route.CellA,target,people+provisions,people,investment,source.Population/4).Allowed;
        }
        bool ReadyKnownMaritimeMission(PrehistoryState s,int polity,Route route,int supplier)
        {
            var h=s.History;var a=h.Aggregates;var map=a.Empires.Connections.Geography.Governments[polity];int host=map.Host[route.B];
            // Only current OWN holdings can be checked before sailing. Foreign
            // shores retain their dated witnessed host; resistance is learned at
            // arrival, not by consulting hidden overseas armies at dispatch.
            if(host==polity)return ReadyOwnedColonialPort(s,polity,route,supplier);
            if(host>=0)return OwnMaritimeExpeditionReady(s,polity,route,out long investment)&&QuotedMaritimeMen(s,polity,route.CellB,supplier,investment)>0;
            return h.Polities[polity].Cohesion>=CivilMaritimeCohesionFloor(s,polity,550);
        }
    }
}

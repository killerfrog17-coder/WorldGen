using System;

namespace WorldGen.Core
{
    // Comparative access/working capacity, not tonnes of proved mineral
    // reserves. Potential deposits yield no money or weapons by themselves.
    public static class HistoricalResourceEconomy
    {
        static double Ore(int potential)=>Math.Sqrt(D.Clamp(potential/180.0));
        public static double DeploymentSupply(HistoricalResourcePotential p,HistoricalTechnology technology)
        {
            double copper=Ore(p.Copper),tin=Ore(p.Tin),iron=Ore(p.Iron),coal=Ore(p.Coal),oil=Ore(p.Oil);
            double timber=Math.Sqrt(D.Clamp(p.Timber/400.0)),fuel=Math.Max(coal,.65*timber),material=1;
            switch(technology)
            {
                case HistoricalTechnology.Copper:material=Math.Min(copper,Math.Max(fuel,.15));break;
                case HistoricalTechnology.Bronze:material=Math.Min(copper,.20+.80*tin);break;
                case HistoricalTechnology.Iron:case HistoricalTechnology.Steel:material=Math.Min(iron,Math.Max(fuel,.15));break;
                case HistoricalTechnology.Sailing:case HistoricalTechnology.Navigation:case HistoricalTechnology.OceanNavigation:material=Math.Max(timber,.30*iron);break;
                case HistoricalTechnology.SteamPower:case HistoricalTechnology.Railways:case HistoricalTechnology.IndustrialMetallurgy:material=Math.Min(iron,Math.Max(coal,.30*timber));break;
                case HistoricalTechnology.CombustionEngines:case HistoricalTechnology.Aviation:case HistoricalTechnology.MechanizedAgriculture:material=Math.Min(iron,oil);break;
                case HistoricalTechnology.Electricity:material=Math.Min(copper,Math.Max(coal,Math.Max(oil,.5*timber)));break;
                case HistoricalTechnology.NuclearFission:case HistoricalTechnology.NuclearWeapons:material=Ore(p.Uranium);break;
            }
            // Sparse three-site sampling must not create an absolute absence
            // claim. Recycling, small overlooked deposits and costly substitutes
            // remain possible, but cannot match a well-supplied industry.
            return D.Q(.30+.70*D.Clamp(material),1000000);
        }
        public static long ExtractiveReceipts(long supportedUrban,double institution,double publicWorks,double metalWork,double steam,double oilWork,HistoricalResourcePotential p)
        {
            double capacity=D.Clamp(institution)*(.2+.8*D.Clamp(publicWorks));
            double metals=Math.Max(Ore(p.Copper),Ore(p.Iron))*.5+Ore(p.PreciousMetals)*.5;
            double energy=Math.Max(Ore(p.Coal)*D.Clamp(steam),Ore(p.Oil)*D.Clamp(oilWork));
            // A small specialist workforce and its taxable output: no
            // population-free rent from merely owning a large coloured claim.
            return Math.Max(0,(long)(Math.Max(0,supportedUrban)*.003*capacity*(metals*D.Clamp(metalWork)+energy)));
        }
        public static double StrategicValue(HistoricalResourcePotential p,double metalWork,double steam,double oilWork)
            =>D.Q(10*Math.Max(Ore(p.Copper),Ore(p.Iron))*D.Clamp(metalWork)+15*Ore(p.Coal)*D.Clamp(steam)+20*Ore(p.Oil)*D.Clamp(oilWork),1000000);
    }
    public sealed partial class Prehistory
    {
        HistoricalNaturalResourceCache livingResourceCache;
        HistoricalResourcePotential LivingResources(int cell)=>(livingResourceCache??(livingResourceCache=new HistoricalNaturalResourceCache(World))).At(cell);
        static HistoricalResourcePotential MergeResourceMarket(HistoricalResourcePotential own,HistoricalResourcePotential other,double connection)
        {
            int M(int a,int b)=>Math.Max(a,(int)(b*connection));
            return new HistoricalResourcePotential(M(own.Copper,other.Copper),M(own.Tin,other.Tin),M(own.Iron,other.Iron),M(own.PreciousMetals,other.PreciousMetals),
                M(own.Coal,other.Coal),M(own.Oil,other.Oil),M(own.Uranium,other.Uranium),M(own.Stone,other.Stone),M(own.Timber,other.Timber),M(own.Salt,other.Salt),
                own.Fertility,own.Freshwater,own.Pasture,own.SyntheticEarthGeology);
        }
        double LivingMaterialConnection(PrehistoryState s,int culture,int from,int to)
        {
            if(from==to)return 1;if(!InstitutionalRouteAllowed(s,from,to,culture))return 0;
            var h=s.History;int owner=HistoricalCells.Polity(s,from),other=HistoricalCells.Polity(s,to);
            bool domestic=owner>=0&&other>=0&&(owner==other||HistoricalImperialHierarchy.Sovereign(s,owner)==HistoricalImperialHierarchy.Sovereign(s,other));
            if(owner>=0&&other>=0&&!domestic)
            {
                if(!aggregateRelations.TryGetValue((Math.Min(owner,other),Math.Max(owner,other)),out int relation)||!h.Relations[relation].Trade||h.Relations[relation].WarSince>=0)return 0;
            }
            double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
            var hc=h.Cultures[culture];double range=200*HistoricalModernEffects.LandTransport(hc,s.Years);
            bool sea=DiffusionShore(from)&&DiffusionShore(to)&&HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sailing,s.Years)>.15&&AggregateSeaLane(from,to);
            if(sea)range=Math.Max(range,HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years,true));
            else if(World.Landmass[from]!=World.Landmass[to])return 0;
            if(km>range*2)return 0;
            return (domestic?.90:.60)*Math.Exp(-km/Math.Max(50,range));
        }
        HistoricalResourcePotential LivingResourceMarket(PrehistoryState s,int culture,int site)
        {
            var h=s.History;var a=h.Aggregates;var cities=a.Cultures[culture].Cities;var result=LivingResources(site);
            // Three actual specialist centers and four direct trading neighbors
            // apiece. No territory/culture/ecoregion census or all-pairs search.
            for(int k=0;k<Math.Min(3,cities.Count);k++)
            {
                int city=cities.At((culture+k)%cities.Count);var town=s.Society.Settlements[city];
                if(!a.Cities[city].Supported||a.Living.Cities[city].AbandonedYear>=0)continue;
                double sourceConnection=LivingMaterialConnection(s,culture,site,town.Cell);if(sourceConnection<=0)continue;
                result=MergeResourceMarket(result,LivingResources(town.Cell),sourceConnection);
                var peers=a.Cities[city].Neighbors;int owner=a.Resolve(town.PolityId,h);
                for(int p=0;p<Math.Min(4,peers.Length);p++)
                {
                    int peer=peers[p];var other=s.Society.Settlements[peer];int foreign=a.Resolve(other.PolityId,h);
                    if(owner<0||foreign<0||!a.Cities[peer].Supported||a.Living.Cities[peer].AbandonedYear>=0)continue;
                    bool domestic=owner==foreign||HistoricalImperialHierarchy.Sovereign(s,owner)==HistoricalImperialHierarchy.Sovereign(s,foreign);
                    bool trade=aggregateRelations.TryGetValue((Math.Min(owner,foreign),Math.Max(owner,foreign)),out int relation)&&h.Relations[relation].Trade&&h.Relations[relation].WarSince<0;
                    if(!domestic&&!trade||!InstitutionalRouteAllowed(s,town.Cell,other.Cell,culture))continue;
                    double km=D.Angle(World.Surface.Cells[town.Cell].Position,World.Surface.Cells[other.Cell].Position)*World.Recipe.RadiusKm;
                    bool sea=DiffusionShore(town.Cell)&&DiffusionShore(other.Cell)&&AggregateSeaLane(town.Cell,other.Cell);
                    double transport=sea?HistoricalDevelopmentEffects.CoastRangeKm(h.Cultures[culture],s.Years,true):200*HistoricalModernEffects.LandTransport(h.Cultures[culture],s.Years);
                    result=MergeResourceMarket(result,LivingResources(other.Cell),sourceConnection*(domestic?.85:.60)*Math.Exp(-km/Math.Max(50,transport)));
                }
            }
            return result;
        }
        long LivingExtractiveReceipts(PrehistoryState s,int polity)
        {
            var h=s.History;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];int capital=s.Society.Settlements[p.CapitalId].Cell;
            return HistoricalResourceEconomy.ExtractiveReceipts(h.Aggregates.Polities[polity].Urban,hc.Institution/1000.0,h.Dynamics.States[polity].PublicWorks/1000.0,
                Math.Max(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Copper,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Iron,s.Years)),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years),LivingResourceMarket(s,p.CultureId,capital));
        }
        double LivingStrategicResourceValue(PrehistoryState s,int polity,int cell)
        {
            var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
            return HistoricalResourceEconomy.StrategicValue(LivingResources(cell),Math.Max(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Copper,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Iron,s.Years)),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years));
        }
    }
}

using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceColonialExpedition(PrehistoryState s,int polity,int elapsed)
        {
            if(!ColonialHistory(s))return;var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];
            if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation)||p.Treasury<1000||p.Cohesion<400)return;
            var cities=a.Polities[polity].Cities;int source=-1;
            int offset=cities.Count==0?0:(int)(HistoryRoll("colonial-funded-port",polity,s.Years)*cities.Count);
            for(int k=0;k<Math.Min(6,cities.Count);k++){int city=cities.At((offset+k)%cities.Count);var town=s.Society.Settlements[city];if(town.Population>=300&&a.Cities[city].Supported&&DiffusionShore(town.Cell)){source=town.Cell;break;}}
            var posts=a.Empires.Governments[polity].AdministrativePosts;
            if(posts.Count>0&&(source<0||HistoryRoll("colonial-forward-port",polity,s.Years)<.65))
            {
                int start=(int)(HistoryRoll("colonial-forward-port-index",polity,s.Years)*posts.Count);
                for(int k=0;k<Math.Min(6,posts.Count);k++){int id=posts.At((start+k)%posts.Count);var post=a.Empires.Territories.Posts[id];if(DiffusionShore(post.Cell)&&CellPatch[post.Cell]>=0&&TerritorialNodeLive(s,polity,-2-id)){source=post.Cell;break;}}
            }
            if(source<0||CellPatch[source]<0)return;int region=CellPatch[source];var charts=a.Empires.Connections.Cultures[p.CultureId].Charts;
            var options=communityVoyages[region];if(options.Length==0&&charts.Length==0)return;
            int destination=-1;double best=-1;
            // A bounded portfolio of actual charts, not omniscient global shores.
            for(int k=0;k<6;k++)
            {
                int to=charts.Length>0&&k<3?charts[(int)(HistoryRoll("colonial-known-route-"+k,polity,s.Years)*charts.Length)].Region:
                    options.Length>0?options[(int)(HistoryRoll("colonial-explore-route-"+k,polity,s.Years)*options.Length)].B:-1;
                if(to<0||to==region||developmentCoast[to]<0)continue;int cell=developmentCoast[to];
                double distance=D.Angle(World.Surface.Cells[source].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm*1000;
                if(distance>HistoricalTechnicalMigration.SeaRangeM(hc,s.Years,true)||!DiffusionSeaLane(source,cell))continue;
                int owner=HistoricalCells.Polity(s,cell);if(owner>=0&&HistoricalImperialHierarchy.Sovereign(s,owner)==HistoricalImperialHierarchy.Sovereign(s,polity))continue;
                double yield=ProvincialPhysicalYield(s,p.CultureId,cell),opportunity=.2+Math.Min(2,yield*4)+(World.Landmass[source]!=World.Landmass[cell]?1:.25);
                opportunity*=.75+.5*HistoryRoll("colonial-route-preference-"+k,polity,s.Years);
                if(opportunity>best){best=opportunity;destination=to;}
            }
            if(destination<0||HistoryRoll("colonial-expedition-departure",polity,s.Years)>1-Math.Exp(-elapsed/12.0))return;
            int shore=developmentCoast[destination];p.Treasury-=100;
            a.Empires.Connections.Governments[polity].LastExpeditionYear=s.Years;
            RecordCoastChart(s,p.CultureId,destination,p.CultureId);
            var route=new Route{A=region,B=destination,CellA=source,CellB=shore,Sea=true,DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[source].Position,World.Surface.Cells[shore].Position)*World.Recipe.RadiusKm*1000)};
            TryTerritorialSeaCharter(s,route,p.CultureId,elapsed);
            // A successful state charter is a dependent venture, not a new
            // pristine civilization waiting to reinvent its sponsor's society.
            if(HistoricalCells.Polity(s,shore)==polity)
            {
                var local=a.Living.Regions[destination];local.Teacher=p.CultureId;local.LastExchangeYear=s.Years;local.Exposure=Math.Max(700,local.Exposure);local.CivicTeacher=polity;local.CivicExposure=Math.Max(700,local.CivicExposure);
                a.Provinces.RegionalKnowledge[destination]|=ProvincialKnowledge(hc);
                if(a.Regions[destination].Culture<0&&HistoricalCells.Culture(s,shore)<0)
                {
                    FoundAggregateCulture(s,destination,shore);int child=s.Society.Cultures.Length-1;
                    InheritDevelopmentCommunity(s,child,p.CultureId,shore);var continuity=a.Empires.Connections.Cultures[child];
                    continuity.ColonialSponsor=polity;continuity.CivilizationRoot=HistoricalRecognizedClaims.Civilization(s,p.CultureId);
                    HistoryEvent(s,"colonial-community",s.Society.Cultures[child].Name+" develops as an overseas branch sponsored by "+p.Name+", retaining its political connection and inherited practices.",shore,child,polity,category:ChronicleCategory.Society);
                }
            }
        }
        void ConnectColonialTown(PrehistoryState s,int city)
        {
            if(!ColonialHistory(s))return;var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];
            int sponsor=a.Resolve(a.Empires.Connections.Cultures[town.CultureId].ColonialSponsor,h);
            if(sponsor<0||a.Resolve(town.PolityId,h)>=0||h.Polities[sponsor].Treasury<5000)return;
            bool foothold=false;var posts=a.Empires.Governments[sponsor].AdministrativePosts;
            for(int k=0;k<Math.Min(8,posts.Count);k++){int id=posts.At((int)(HistoryRoll("colonial-town-connection-"+k,city,s.Years)*posts.Count));var post=a.Empires.Territories.Posts[id];if(CellPatch[post.Cell]==CellPatch[town.Cell]&&TerritorialNodeLive(s,sponsor,-2-id)){foothold=true;break;}}
            if(!foothold)return;
            h.Polities[sponsor].Treasury-=5000;FoundAggregateState(s,city,sponsor);int local=town.PolityId;
            h.Polities[local].CultureId=h.Polities[sponsor].CultureId;h.Polities[local].Government="A funded overseas charter and local council";
            h.Polities[local].Treasury+=5000;a.Empires.Connections.Governments[local].Sponsor=sponsor;
            if(a.Living.CivicOrigins[town.CultureId]==local)a.Living.CivicOrigins=a.Living.CivicOrigins.With(town.CultureId,-1);
            SetImperialPatron(s,local,sponsor,false,"The founding charter retains external allegiance while establishing a local colonial court.");
        }
        bool RespectRecognizedClaim(PrehistoryState s,int polity,int cell)
        {
            int holder=HistoricalRecognizedClaims.Holder(s,cell);if(holder<0||holder==polity||HistoricalImperialHierarchy.Sovereign(s,holder)==HistoricalImperialHierarchy.Sovereign(s,polity))return false;
            var relation=AggregateRelation(s,polity,holder);return relation.WarSince<0&&relation.Trust>=0;
        }
        void AdvanceRecognizedTitles(PrehistoryState s,int polity,int elapsed)
        {
            if(!ColonialHistory(s))return;var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];
            if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)||!HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation))return;
            var connections=a.Empires.Connections;var contacts=s.Society.Cultures[p.CultureId].Contacts;if(contacts.Length==0)return;
            int offset=(int)(HistoryRoll("colonial-recognition-partner",polity,s.Years)*contacts.Length),partner=-1;
            for(int k=0;k<Math.Min(4,contacts.Length);k++)
            {
                int other=contacts[(offset+k)%contacts.Length],court=a.Resolve(a.Living.CivicOrigins[other],h);
                if(court<0){var sites=a.Cultures[other].Cities;for(int site=0;site<Math.Min(3,sites.Count);site++){court=a.Resolve(s.Society.Settlements[sites.At(site)].PolityId,h);if(court>=0)break;}}
                if(court<0||court==polity||HistoricalImperialHierarchy.Sovereign(s,court)==HistoricalImperialHierarchy.Sovereign(s,polity))continue;
                var relation=AggregateRelation(s,polity,court);if(relation.WarSince<0&&relation.Trade&&relation.Trust>=20){partner=court;break;}
            }
            if(partner<0)return;
            var government=connections.Governments[polity];var posts=a.Empires.Governments[polity].AdministrativePosts;
            if(posts.Count>0)
            {
                int id=posts.At((int)(HistoryRoll("colonial-title-basis",polity,s.Years)*posts.Count));var post=a.Empires.Territories.Posts[id];int region=CellPatch[post.Cell];
                if(post.Maritime&&region>=0&&World.Ecology.Habitat[post.Cell].GrowingDays>=60&&post.ClosedYear<0&&s.Years-post.FoundedYear>=10&&connections.TitleAt[region]<0&&TerritorialNodeLive(s,polity,-2-id)
                    &&connections.Cultures[h.Polities[partner].CultureId].Charts.Any(c=>c.Region==region)&&p.Treasury>=1000)
                {
                    int titleId=connections.Titles.Length;var title=new HistoricalRecognizedTitle{Id=titleId,Region=region,Holder=polity,Recognizer=partner,EstablishedYear=s.Years,LastTransferYear=s.Years,AreaKm2=connections.RegionAreas[region],Basis="A funded coastal foothold, charted access and a bilateral recognition agreement"};
                    connections.Titles=connections.Titles.Concat(new[]{title}).ToArray();connections.TitleAt=connections.TitleAt.With(region,titleId);government.Titles.Add(titleId);p.Treasury-=1000;h.Polities[partner].Treasury=Math.Min(1000000000,h.Polities[partner].Treasury+1000);
                    HistoryEvent(s,"territorial-recognition",h.Polities[partner].Name+" recognizes the chartered territorial title of "+p.Name+" around "+ColonialRegionName(region)+". This records sovereignty claims, not new settlers or uniform direct administration.",post.Cell,p.CultureId,polity,partner,category:ChronicleCategory.Politics);
                }
            }
            if(government.Titles.Count==0||!(p.Exhaustion>250||a.Polities[polity].Receipts<a.Polities[polity].ServiceCost||p.Cohesion<450))return;
            int choice=government.Titles.At((int)(HistoryRoll("territorial-sale-title",polity,s.Years)*government.Titles.Count));var offer=connections.Titles[choice];
            if(a.Resolve(offer.Holder,h)!=polity)return;
            bool adjacent=false;foreach(int routeId in a.Regions[offer.Region].Routes.Take(16)){var route=Routes[routeId];int other=route.A==offer.Region?route.B:route.A;int neighbor=connections.TitleAt[other];if(neighbor>=0&&a.Resolve(connections.Titles[neighbor].Holder,h)==partner||HistoricalCells.Polity(s,a.Regions[other].BestCell)==partner){adjacent=true;break;}}
            long price=Math.Max(2000,offer.AreaKm2/4);if(!adjacent||h.Polities[partner].Treasury<price||HistoryRoll("territorial-sale",polity,s.Years)>1-Math.Exp(-elapsed/80.0))return;
            TransferRecognizedTitle(s,choice,partner,price);
        }
        void TransferRecognizedTitle(PrehistoryState s,int titleId,int buyer,long price)
        {
            var h=s.History;var a=h.Aggregates;var c=a.Empires.Connections;var title=c.Titles[titleId];int seller=a.Resolve(title.Holder,h);
            if(seller<0||seller==buyer||buyer<0||buyer>=h.Polities.Length||h.Polities[buyer].EndedYear>=0||price<0||h.Polities[buyer].Treasury<price||price>1000000000-h.Polities[seller].Treasury)return;
            h.Polities[buyer].Treasury-=price;h.Polities[seller].Treasury+=price;
            c.Governments[seller].Titles.Remove(titleId);c.Governments[buyer].Titles.Add(titleId);title.PreviousHolder=seller;title.Holder=buyer;title.Recognizer=seller;title.LastTransferYear=s.Years;title.LastPrice=price;
            HistoryEvent(s,"territorial-purchase",h.Polities[buyer].Name+" purchases the recognized territorial title of "+h.Polities[seller].Name+" for "+price+" account units. Existing communities, cultures and their actual administration are not transferred by this legal title alone.",Patches[title.Region].Cell,h.Polities[buyer].CultureId,buyer,seller,category:ChronicleCategory.Politics);
        }
    }
}

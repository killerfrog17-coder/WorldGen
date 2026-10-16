using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        bool TryFoundLivingColonialPort(PrehistoryState s,int polity,Route route,int elapsed)
        {
            if(!LivingWorldHistory(s)||elapsed<=0)return false;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];int target=route.CellB,postId=a.Empires.Territories.PostAt[target];
            if(postId<0||HistoricalCells.Polity(s,target)!=polity||a.TownAt[target]>=0||p.Cohesion<500||!LivingTerritorialInterest(s,polity,target))return false;
            var post=a.Empires.Territories.Posts[postId];
            if(!post.Maritime||post.ClosedYear>=0||!TerritorialNodeLive(s,polity,-2-postId)||!InstitutionalRouteAllowed(s,route.CellA,target,p.CultureId))return false;
            if(!MaritimeCharterLogistics(s,polity,route,a.TownAt[route.CellA],out int port,out _))return false;
            var source=s.Society.Settlements[port];int culture=source.CultureId;
            // These are real people from this source community, with its actual
            // crop practice. Conquered foragers cannot turn into metropolitan
            // farmers merely because their ruler owns ships.
            if(!CanFoundLivingTown(s,culture,target,port))return false;
            long available=LivingFounderAvailability(s,port),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
            double stagingKm=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
            long provisions=HistoricalVoyageEffects.Provisioning(people,(int)Math.Min(int.MaxValue,route.DistanceM+stagingKm*1000));
            long investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(people,route.DistanceM);
            if(p.Treasury<investment*2||source.FoodReserve<people+provisions+source.Population/4)return false;
            if(HistoryRoll("living-sponsored-port-"+target,polity,s.Years)>=1-Math.Exp(-elapsed/40.0))return false;
            int count=a.Cities.Length;FoundLivingTown(s,culture,target,port);if(a.Cities.Length==count)return false;
            // FoundLivingTown already transfers the precise household budgets
            // and their startup food. Charge only the additional voyage ration
            // and construction account here, not the same people a second time.
            source.FoodReserve-=provisions;p.Treasury-=investment;
            post.Founders=Math.Max(post.Founders,(int)founders);
            int city=a.TownAt[target];h.Towns[city].CivicTeacherPolityId=polity;h.Towns[city].CivicExampleExposure=Math.Max(700,h.Towns[city].CivicExampleExposure);
            HistoryEvent(s,"sponsored-port",p.Name+" finances a new port at "+s.Society.Settlements[city].Name+". "+people+" existing people relocate from "+source.Name+", bringing their own crops and practices; neighboring indigenous communities retain their identities.",target,culture,polity,town:city,category:ChronicleCategory.Society);
            return true;
        }
        void RetryLivingColonialPorts(PrehistoryState s,int polity,int elapsed,int[] departures)
        {
            var posts=s.History.Aggregates.Empires.Governments[polity].AdministrativePosts;
            int offset=posts.Count==0?0:(int)(HistoryRoll("living-colonial-unsettled-offices",polity,s.Years)*posts.Count);
            for(int k=0;k<Math.Min(2,posts.Count);k++)
            {
                var post=s.History.Aggregates.Empires.Territories.Posts[posts.At((offset+k)%posts.Count)];
                if(!post.Maritime||post.ClosedYear>=0||s.History.Aggregates.TownAt[post.Cell]>=0||CellPatch[post.Cell]<0)continue;
                foreach(int source in departures)
                {
                    if(source==post.Cell)continue;
                    var route=new Route{A=CellPatch[source],B=CellPatch[post.Cell],CellA=source,CellB=post.Cell,Sea=true,
                        DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[source].Position,World.Surface.Cells[post.Cell].Position)*World.Recipe.RadiusKm*1000)};
                    if(TryFoundLivingColonialPort(s,polity,route,elapsed))break;
                }
            }
        }
    }
}

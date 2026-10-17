using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public static class HistoricalAutonomyEffects
    {
        public static double Grievance(int strain,int legitimacy,int exhaustion,int stability,bool disputed)
            =>D.Q(D.Clamp(Math.Max(0,strain-150)/650.0+Math.Max(0,550-legitimacy)/550.0+exhaustion/1800.0+Math.Max(0,600-stability)/1200.0+(disputed?.25:0)),1000000);
        public static int Pressure(int previous,double grievance,bool organizedForeignCenter)
            =>!organizedForeignCenter||grievance<.15?Math.Max(0,previous-60):Math.Min(1000,previous+10+(int)Math.Round(70*grievance));
        public static bool ViableBase(int centers,long localUrban,long totalUrban)
            =>localUrban>=10000&&totalUrban>0&&(centers>=2||localUrban>=20000)&&(decimal)localUrban/totalUrban>=.08m;
    }
    public sealed partial class Prehistory
    {
        void AdvanceProvincialAutonomy(PrehistoryState s,CancellationToken token)
        {
            var h=s.History;
            var administration=InstitutionalHistory(s)?BuildAdministrativeNetwork(s,token):null;
            foreach(var town in s.Society.Settlements.OrderBy(t=>t.Id))
            {
                token.ThrowIfCancellationRequested();var record=h.Towns[town.Id];int owner=town.PolityId;
                if(record.UnrestPolityId!=owner){record.UnrestPolityId=owner;record.AutonomyPressure=0;}
                if(owner<0){record.AutonomyPressure=0;continue;}
                var court=h.Polities[owner];var local=s.Society.Cultures[town.CultureId];var ds=h.Dynamics.States[owner];
                // An earlier town in this pass may have created a new court
                // and transferred other towns into it. Never index the route
                // snapshot with an identity that did not exist when it was built.
                if(administration!=null&&owner>=administration.Accounts.Length)administration=BuildAdministrativeNetwork(s,token);
                int civicRivalry=CivicRivalry(s,town.Id,court.CapitalId);
                bool organized=(town.CultureId!=court.CultureId||civicRivalry>=180)&&town.Id!=court.CapitalId&&town.Population>=5000&&local.Organization>=450&&town.FoodCapacity>=town.Population*.8;
                int strain=ds.Strain;if(administration!=null)strain=Math.Max(strain,HistoricalInstitutionalEffects.ServiceBurden(administration.Distance[town.Cell],administration.Accounts[owner].ReachM,administration.Owner[town.Cell]==owner));
                double grievance=HistoricalAutonomyEffects.Grievance(strain,court.Legitimacy,court.Exhaustion,town.Stability,ds.DisputedUntil>s.Years);
                if(ModernHistory(s))grievance=D.Q(D.Clamp(grievance+.2*civicRivalry/1000.0),1000000);
                int old=record.AutonomyPressure;record.AutonomyPressure=HistoricalAutonomyEffects.Pressure(old,grievance,organized);
                if(old<300&&record.AutonomyPressure>=300)HistoryEvent(s,"provincial-unrest","Local institutions at "+town.Name+" press for greater autonomy within "+court.Name+" as imperial strains grow.",town.Cell,town.CultureId,owner,town:town.Id,category:ChronicleCategory.Politics);
                if(!organized||record.AutonomyPressure<750||s.Years-record.LastAutonomyActionYear<150||s.Society.Settlements.Count(t=>t.PolityId==owner&&t.Population>=100)<2||HistoryRoll("provincial-revolt",town.Id,s.Years)>=.025*record.AutonomyPressure/1000.0)continue;
                int language=h.Cultures[town.CultureId].LanguageId;
                var bloc=s.Society.Settlements.Where(t=>t.PolityId==owner&&t.Id!=court.CapitalId&&t.Population>=5000&&(t.CultureId!=court.CultureId||CivicRivalry(s,t.Id,court.CapitalId)>=180)&&(t.CultureId==town.CultureId||h.Cultures[t.CultureId].LanguageId==language)&&D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[town.Cell].Position)*World.Recipe.RadiusKm<=200&&(!ModernHistory(s)||HistoricalCivicMemoryEffects.CanCooperate(CivicRivalry(s,town.Id,t.Id),grievance))).OrderBy(t=>t.Id).ToArray();
                long localUrban=bloc.Sum(t=>t.Population),totalUrban=s.Society.Settlements.Where(t=>t.PolityId==owner).Sum(t=>t.Population);
                record.LastAutonomyActionYear=s.Years;
                if(!HistoricalAutonomyEffects.ViableBase(bloc.Length,localUrban,totalUrban))
                {
                    record.AutonomyPressure=300;
                    HistoryEvent(s,"provincial-resistance","An autonomy challenge at "+town.Name+" fails to unite a viable provincial coalition; "+court.Name+" retains control.",town.Cell,town.CultureId,owner,town:town.Id,category:ChronicleCategory.Politics);continue;
                }
                long oldFunds=court.Treasury;int oldArmy=court.Army;
                int child=h.Polities.Length;FoundHistoricalPolity(s,town,owner);
                foreach(var center in bloc.Where(t=>t.Id!=town.Id)){center.PolityId=child;center.Government=h.Polities[child].Government;SetPoliticalOwner(s,center.Cell,child);TransferCivicHinterland(s,center.Id,owner,child,token);h.Towns[center.Id].UnrestPolityId=child;h.Towns[center.Id].AutonomyPressure=0;h.Towns[center.Id].LastAutonomyActionYear=s.Years;}
                RefreshSplitPolityTotals(s,owner,child,token);
                var newCourt=h.Polities[child];newCourt.Treasury=HistoricalSuccessionEffects.DivideFunds(oldFunds,localUrban,totalUrban);court.Treasury=oldFunds-newCourt.Treasury;
                newCourt.Army=(int)HistoricalSuccessionEffects.DivideFunds(oldArmy,newCourt.Population,court.Population+newCourt.Population);court.Army=oldArmy-newCourt.Army;
                record.AutonomyPressure=0;record.UnrestPolityId=child;
                HistoryEvent(s,"provincial-revolt",s.Society.Cultures[town.CultureId].Name+" councils at "+town.Name+" establish an autonomous court against "+court.Name+"; their existing residents and institutions form its base.",town.Cell,town.CultureId,child,owner,town.Id,category:ChronicleCategory.Politics);
                int a=Math.Min(owner,child),b=Math.Max(owner,child);var relation=new HistoricalRelation{A=a,B=b,Trust=-65,WarSince=s.Years};h.Relations.Add(relation);
                BeginCivicCampaign(s,relation,owner,HistoricalWarAim.Reunification);
            }
        }
    }
}

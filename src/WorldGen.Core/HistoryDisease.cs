using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    // Functional historical syndromes, not a validated pathogen/medical model.
    public enum HistoricalDiseaseKind { None,Enteric,Respiratory,VectorFever,SevereEpidemic }
    public static class HistoricalDiseaseEffects
    {
        public static readonly string[] Names={"No recorded outbreak","Enteric outbreak","Respiratory epidemic","Vector-borne fever","High-mortality epidemic"};
        public static readonly int[] Mortality={0,16,12,18,110},Duration={0,2,3,4,4},CommunityRecovery={0,12,18,8,60};
        public static double Transmission(HistoricalDiseaseKind kind,double distanceKm,double suitability)
            =>kind==HistoricalDiseaseKind.None?0:D.Q(D.Clamp((kind==HistoricalDiseaseKind.SevereEpidemic?.22:kind==HistoricalDiseaseKind.Respiratory?.14:kind==HistoricalDiseaseKind.Enteric?.09:.07)*Math.Exp(-Math.Max(0,distanceKm)/550.0)*D.Clamp(suitability),0,.35),1000000000);
        public static double CombineExposure(double prior,double incoming)=>D.Q(D.Clamp(1-(1-D.Clamp(prior))*(1-D.Clamp(incoming)),0,.45),1000000000);
        public static bool Susceptible(int year,int activeUntil,int familyRecoveryUntil)=>year>activeUntil&&year>familyRecoveryUntil;
        public static long AnnualDeaths(long population,int mortalityPermille,double fed)
            =>(long)Math.Min((decimal)Math.Max(0,population),(decimal)Math.Max(0,population)*Math.Min(1000,Math.Max(0,mortalityPermille))*D.M(1000+350*(1-D.Clamp(fed)))/1000000);
    }
    public sealed partial class Prehistory
    {
        readonly struct HistoricalDiseaseSource
        {
            public readonly int Town,Mortality,Wave;public readonly HistoricalDiseaseKind Kind;
            public HistoricalDiseaseSource(int town,HistoricalDiseaseKind kind,int mortality,int wave){Town=town;Kind=kind;Mortality=mortality;Wave=wave;}
        }
        HistoricalDiseaseSource[] DiseaseSources(PrehistoryState s)
            =>s.Society.Settlements.Where(t=>t.Population>=100&&s.History.Towns[t.Id].DiseaseUntil>=s.Years&&s.History.Towns[t.Id].DiseaseKind!=HistoricalDiseaseKind.None).OrderBy(t=>t.Id).Select(t=>new HistoricalDiseaseSource(t.Id,s.History.Towns[t.Id].DiseaseKind,s.History.Towns[t.Id].DiseaseMortalityPermille,s.History.Towns[t.Id].DiseaseWaveId)).ToArray();
        double DiseaseHabitat(int cell,HistoricalDiseaseKind kind)
        {
            if(kind!=HistoricalDiseaseKind.VectorFever)return 1;
            var h=World.Ecology.Habitat[cell];return D.Q(D.Clamp((h.WarmTenthsC-120)/160.0)*D.Clamp(h.GrowingDays/240.0)*(1-h.PermafrostPermille/1000.0),1000000);
        }
        void AdvanceHistoricalDisease(PrehistoryState s,Settlement town,bool trade,HistoricalDiseaseSource[] sources,int[][] network,HistoricalFoodYear foodYear,CancellationToken token)
        {
            var h=s.History;var record=h.Towns[town.Id];if(record.KindImmunity==null)record.KindImmunity=new int[4];
            if(town.Population<100||record.DiseaseUntil>=s.Years)return;
            var probability=new double[4];var strongest=new double[4];var origins=Enumerable.Repeat(-1,4).ToArray();var mortality=new int[4];
            foreach(var source in sources)
            {
                token.ThrowIfCancellationRequested();if(source.Town==town.Id)continue;int k=(int)source.Kind-1;
                if(!HistoricalDiseaseEffects.Susceptible(s.Years,record.DiseaseUntil,record.KindImmunity[k]))continue;
                var departure=s.Society.Settlements[source.Town];
                bool regular=Array.BinarySearch(network[town.Id],source.Town)>=0,shipment=foodYear.Voyages.Contains((Math.Min(town.Id,source.Town),Math.Max(town.Id,source.Town)));
                var relation=FindHistoricalRelation(s,departure.PolityId,town.PolityId);
                bool military=relation!=null&&relation.WarSince>=0&&relation.LastBattle>=s.Years-3;
                if(!regular&&!shipment&&!military)continue;
                double distance=D.Angle(World.Surface.Cells[departure.Cell].Position,World.Surface.Cells[town.Cell].Position)*World.Recipe.RadiusKm;
                if(distance>1000)continue;
                bool contact=FoodExchangeAllowed(s,departure,town);
                if(!contact&&departure.PolityId>=0&&town.PolityId>=0)contact=relation!=null&&relation.LastBattle>=s.Years-3;
                if(!contact||(!regular&&!shipment&&!ReachableHistoricalContact(s,departure.Cell,town.Cell,departure.CultureId,token)))continue;
                double chance=HistoricalDiseaseEffects.Transmission(source.Kind,distance,DiseaseHabitat(town.Cell,source.Kind));
                probability[k]=HistoricalDiseaseEffects.CombineExposure(probability[k],chance);
                if(chance>strongest[k]){strongest[k]=chance;origins[k]=source.Town;mortality[k]=source.Mortality;}
            }
            int chosen=-1;double best=0;
            for(int k=0;k<4;k++)if(probability[k]>best&&HistoryRoll("disease-contact-"+k,town.Id,s.Years)<probability[k]){chosen=k;best=probability[k];}
            if(chosen<0)
            {
                if(town.Population<=1500)return;
                double originRisk=.0004*Math.Min(8,town.Population/3000.0)*(trade?1.3:1);
                if(HistoryRoll("disease-origin",town.Id,s.Years)>=originRisk)return;
                double pick=HistoryRoll("disease-kind",town.Id,s.Years);
                chosen=pick>=.995?3:pick>.65&&DiseaseHabitat(town.Cell,HistoricalDiseaseKind.VectorFever)>.25?2:pick>.35?1:0;
                if(!HistoricalDiseaseEffects.Susceptible(s.Years,record.DiseaseUntil,record.KindImmunity[chosen]))return;
                mortality[chosen]=D.M(HistoricalDiseaseEffects.Mortality[chosen+1]*(.8+.4*HistoryRoll("disease-severity",town.Id,s.Years)));
            }
            var kind=(HistoricalDiseaseKind)(chosen+1);record.DiseaseKind=kind;record.DiseaseMortalityPermille=mortality[chosen];
            record.DiseaseWaveId=origins[chosen]<0?h.Archive.Count+1:sources.First(source=>source.Town==origins[chosen]).Wave;
            record.DiseaseUntil=s.Years+HistoricalDiseaseEffects.Duration[chosen+1]-1;
            record.KindImmunity[chosen]=record.DiseaseUntil+HistoricalDiseaseEffects.CommunityRecovery[chosen+1];record.ImmunityUntil=Math.Max(record.ImmunityUntil,record.KindImmunity[chosen]);
            string route=origins[chosen]<0?" emerges locally":(" arrives through contact with "+s.Society.Settlements[origins[chosen]].Name+" [site "+origins[chosen]+"]");
            HistoryEvent(s,kind==HistoricalDiseaseKind.SevereEpidemic?"major-epidemic":"epidemic",HistoricalDiseaseEffects.Names[chosen+1]+route+" at "+town.Name+", disrupting households and exchange.",town.Cell,town.CultureId,town.PolityId,origins[chosen]<0?-1:s.Society.Settlements[origins[chosen]].PolityId,town.Id);
        }
    }
}

using System;
namespace WorldGen.Core
{
    public static class HistoricalRealmPoliticalEffects
    {
        public static double FleetCohesionFloor(double bureaucracy)=>500-180*D.Clamp(bureaucracy);
        public static double WarHazard(double ambition,double opportunity,double rivalry,bool sharedCulture,bool allied,bool sphere,double personal,double funding)
            =>D.Q(.018*(.3+D.Clamp(ambition))*(.25+1.6*D.Clamp(opportunity)+.6*D.Clamp(rivalry)+(sphere?.6:0))
                *(sharedCulture?.8:1)*(allied?.3:1)*(1+2*D.Clamp(personal))*D.Clamp(funding),1000000000);
        public static double SelfDetermination(double printing,double massCommunication,bool foreign,bool imposed,double representation,double exhaustion)
            =>!foreign?0:D.Q((.0015*D.Clamp(printing)+.018*D.Clamp(massCommunication))*(imposed?1:.35)
                *(.5+.5*D.Clamp(representation))*(1+D.Clamp(exhaustion)),1000000000);
        public static double IdeologicalDistance(int voiceA,int centralA,int voiceB,int centralB,double massPolitics)
            =>D.Q(D.Clamp(massPolitics)*(.7*Math.Abs(voiceA-voiceB)+.3*Math.Abs(centralA-centralB))/1000.0,1000000000);
        public static double SettlerSelfGovernment(int institutionalAge,double distanceKm,int autonomy,double printing,double tributeRatio,int localVoice,int metropolitanVoice,bool externalWar)
        {
            double identity=D.Clamp((institutionalAge-60)/220.0)*D.Clamp(distanceKm/1500.0)*D.Clamp(autonomy/1000.0);
            double taxation=D.Clamp(tributeRatio/.15),voice=D.Clamp(.4+(localVoice-metropolitanVoice)/500.0);
            return D.Q(.008*identity*(.2+.8*D.Clamp(printing))*(.25+.75*taxation)*(.35+.65*voice)*(externalWar?.35:1),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        void ConsiderRealmNeighbors(PrehistoryState s,int polity,HistoricalLeader leader,int elapsed)
        {
            if(!RealmHistory(s))return;var h=s.History;var contacts=h.International.Accounts[polity].Relations;if(contacts.Count==0)return;
            int offset=(int)(HistoryRoll("realm-known-courts-1",polity,s.Years)*contacts.Count);
            // Actual previously met courts, not random own-town peers or an
            // omniscient list of all states. No territorial-size-dependent work.
            for(int k=0;k<Math.Min(2,contacts.Count);k++)
            {
                var relation=h.Relations[contacts.At((offset+k)%contacts.Count)];int peer=relation.A==polity?relation.B:relation.A;
                peer=InternationalContinuingParty(s,peer);if(peer<0||peer==polity||h.Polities[peer].EndedYear>=0)continue;
                ConsiderDevelopmentDiplomacy(s,polity,peer,leader,elapsed);
            }
        }
        double RealmIdeologicalDistance(PrehistoryState s,int first,int second)
        {
            var h=s.History;var a=h.Aggregates;var p=a.Communities.Governments[first];var q=a.Communities.Governments[second];
            double mass=Math.Min(HistoricalModernEffects.Adoption(h.Cultures[h.Polities[first].CultureId],HistoricalTechnology.Printing,s.Years),HistoricalModernEffects.Adoption(h.Cultures[h.Polities[second].CultureId],HistoricalTechnology.Printing,s.Years));
            return HistoricalRealmPoliticalEffects.IdeologicalDistance(p.RepresentationMilli,p.CentralizationMilli,q.RepresentationMilli,q.CentralizationMilli,mass);
        }
    }
}

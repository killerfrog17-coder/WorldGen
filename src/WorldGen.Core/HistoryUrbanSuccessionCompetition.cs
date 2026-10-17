using System;
namespace WorldGen.Core
{
    public static class HistoricalUrbanSuccessionCompetition
    {
        // Authored political priors, not a measured universal failure rate.
        // Multiple materially substantial centers can field rival coalitions
        // even in peacetime. A single village, a country's age, and a succession
        // alone are not grounds for territorial dissolution.
        public static double Hazard(int cities,long urban,long capital,double centralization,double integration,double bureaucracy,bool representative,int incomingAdministration)
        {
            long peers=Math.Max(0,urban-Math.Max(0,capital));
            if(cities<2||peers<1200||urban<=0)return 0;
            double centers=D.Clamp(Math.Log(cities)/Math.Log(8));
            double means=D.Clamp(Math.Sqrt(peers/10000.0));
            double independence=D.Clamp(peers/(double)urban);
            double mediation=(.5+.5*D.Clamp(centralization))*(1-.65*D.Clamp(integration))*(1-.9*D.Clamp(bureaucracy));
            double successor=.65+.35*(1-D.Clamp(incomingAdministration/1000.0));
            return D.Q(.20*centers*means*independence*mediation*successor*(representative?.12:1),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        double UrbanRealmSuccessionRisk(PrehistoryState s,int polity,HistoricalLeader incoming)
        {
            if(s.History.Aggregates.Empires.Connections.Realms.Version<13)return 0;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var policy=a.Communities.Governments[polity];
            return HistoricalUrbanSuccessionCompetition.Hazard(a.Polities[polity].CityCount,a.Polities[polity].Urban,s.Society.Settlements[p.CapitalId].Population,
                policy.CentralizationMilli/1000.0,RealmIntegration(s,polity),RealmBureaucracy(s,polity),policy.RepresentationMilli>=650,incoming.Administration);
        }
    }
}

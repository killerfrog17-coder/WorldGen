using System;

namespace WorldGen.Core
{
    // Abstract historical capabilities only. Knowledge, installed industrial
    // capacity and a government's future physical arsenal remain separate.
    public static class HistoricalNuclearEffects
    {
        public static double CivilianEnergy(HistoricalCulture culture,int year)
            =>D.Q(HistoricalModernEffects.Adoption(culture,HistoricalTechnology.NuclearFission,year)*HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Electricity,year),1000000);
    }
    public sealed partial class Prehistory
    {
        // Explicit migration/new-history preparation only; no terrain census,
        // inventions, deployment, dates or material are granted by resizing.
        static void ExpandNuclearCapabilities(PrehistoryState s)
        {
            InitializeNuclearHistory(s);
            foreach(var c in s.History.Cultures)
            {
                Array.Resize(ref c.Technology,HistoricalTechnicalGraph.Count);Array.Resize(ref c.Research,HistoricalTechnicalGraph.Count);
                InitializeTechnicalPractice(c,s.Years);var t=c.Technical;int previous=t.Sources.Length;
                Array.Resize(ref t.Levels,HistoricalTechnicalGraph.Count);Array.Resize(ref t.LevelYear,HistoricalTechnicalGraph.Count);Array.Resize(ref t.InstalledYear,HistoricalTechnicalGraph.Count);
                Array.Resize(ref t.Deployment,HistoricalTechnicalGraph.Count);Array.Resize(ref t.Allocation,HistoricalTechnicalGraph.Count);Array.Resize(ref t.Offers,HistoricalTechnicalGraph.Count);
                Array.Resize(ref t.OfferYear,HistoricalTechnicalGraph.Count);Array.Resize(ref t.Sources,HistoricalTechnicalGraph.Count);Array.Resize(ref t.Progress,HistoricalTechnicalGraph.Count);Array.Resize(ref t.Learning,HistoricalTechnicalGraph.Count);
                for(int k=previous;k<t.Sources.Length;k++)t.Sources[k]=-1;
            }
        }
        void NuclearSecurityAt(PrehistoryState s,int city,int owner,ref double war,ref double threat)
        {
            var h=s.History;var a=h.Aggregates;int sovereign=HistoricalImperialHierarchy.Sovereign(s,owner);
            foreach(int peer in a.Cities[city].Neighbors)
            {
                int rival=a.Resolve(s.Society.Settlements[peer].PolityId,h);
                if(rival<0||rival==owner)continue;int otherSovereign=HistoricalImperialHierarchy.Sovereign(s,rival);if(otherSovereign==sovereign)continue;
                if(!aggregateRelations.TryGetValue((Math.Min(owner,rival),Math.Max(owner,rival)),out int at)
                    &&!aggregateRelations.TryGetValue((Math.Min(sovereign,otherSovereign),Math.Max(sovereign,otherSovereign)),out at))continue;
                if(at>=h.Relations.Length)continue;
                var relation=h.Relations.Peek(at);
                if(relation.WarSince>=0){war=1;threat=1;}
                else if(!relation.Alliance&&relation.Trust<=-25)threat=Math.Max(threat,D.Clamp((-relation.Trust-15)/85.0));
            }
        }
        bool NuclearInvestigationSupported(PrehistoryState s,int culture,int technology)
        {
            if(!LivingWorldHistory(s))return false;var a=s.History.Aggregates.Cultures[culture];var hc=s.History.Cultures[culture];
            // Actual independent experimental programs need an established
            // industrial/scholarly network, not just borrowed textbook levels.
            if(a.CivilizationSince<0||a.SupportedCities<4||a.SupportedUrban<100000||hc.Institution<650||hc.Surplus<400)return false;
            return HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Electricity,s.Years)>=.4
                &&HistoricalModernEffects.Adoption(hc,HistoricalTechnology.IndustrialMetallurgy,s.Years)>=.4
                &&HistoricalModernEffects.Adoption(hc,HistoricalTechnology.ScientificMethod,s.Years)>=.4;
        }
    }
}

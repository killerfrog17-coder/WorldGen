using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public bool CanHistoricalContact(PrehistoryState state,int from,int to,int culture,CancellationToken token=default)
        {
            if(from<0||to<0||from>=World.Surface.Cells.Length||to>=World.Surface.Cells.Length)throw new ArgumentOutOfRangeException(nameof(from));
            if(state?.History==null||culture<0||culture>=state.History.Cultures.Length)throw new ArgumentOutOfRangeException(nameof(culture));
            return ReachableHistoricalContact(state,from,to,culture,token);
        }
        void CivicPoliticalContacts(PrehistoryState state,SortedSet<(int,int)> pairs,CancellationToken token)
        {
            var history=state.History;
            if(ModernHistory(state))foreach(var link in history.Modern.Shipping)
            {var a=state.Society.Settlements[link.PortA];var b=state.Society.Settlements[link.PortB];if(a.Population>=100&&b.Population>=100&&a.PolityId>=0&&b.PolityId>=0&&a.PolityId!=b.PolityId)pairs.Add((Math.Min(a.PolityId,b.PolityId),Math.Max(a.PolityId,b.PolityId)));}
            var towns=state.Society.Settlements.Where(t=>t.PolityId>=0&&t.Population>=100&&history.Polities[t.PolityId].EndedYear<0).OrderBy(t=>t.Id).ToArray();
            var locator=RealismHistory(state)?new CellLocator(towns.Select(t=>World.Surface.Cells[t.Cell]).ToArray()):null;
            double maximumChord=2*D.Sin(3150.0/(2*World.Recipe.RadiusKm));
            for(int a=0;a<towns.Length;a++)foreach(int b in locator==null?Enumerable.Range(a+1,towns.Length-a-1):locator.Within(World.Surface.Cells[towns[a].Cell].Position,maximumChord*maximumChord).Where(b=>b>a).OrderBy(b=>b))
            {
                if((b&255)==0)token.ThrowIfCancellationRequested();var x=towns[a];var y=towns[b];if(x.PolityId==y.PolityId)continue;
                var pair=(Math.Min(x.PolityId,y.PolityId),Math.Max(x.PolityId,y.PolityId));if(pairs.Contains(pair))continue;
                bool navigation=HistoricalTechnologies.Has(history.Cultures[x.CultureId],HistoricalTechnology.Navigation)||HistoricalTechnologies.Has(history.Cultures[y.CultureId],HistoricalTechnology.Navigation);
                double distance=D.Angle(World.Surface.Cells[x.Cell].Position,World.Surface.Cells[y.Cell].Position)*World.Recipe.RadiusKm;
                double transport=RealismHistory(state)?Math.Max(HistoricalModernEffects.LandTransport(history.Cultures[x.CultureId],state.Years),HistoricalModernEffects.LandTransport(history.Cultures[y.CultureId],state.Years)):1;
                if(distance>(navigation?700:500)*transport)continue;
                if(CanHistoricalContact(state,x.Cell,y.Cell,x.CultureId,token)||CanHistoricalContact(state,y.Cell,x.Cell,y.CultureId,token))pairs.Add(pair);
            }
        }
    }
}

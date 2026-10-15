using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void PrepareRegionalCropSites(PrehistoryState s,CancellationToken token)
        {
            var society=s.Society;var agriculture=s.History.Agriculture;
            var scores=new double[agriculture.Length*6];var sites=new int[agriculture.Length*6];
            for(int id=0;id<agriculture.Length;id++)for(int k=0;k<6;k++)
            {int cell=agriculture[id].PracticeCell[k];sites[id*6+k]=cell;scores[id*6+k]=CropSuitability(s,cell,k)*(.5+.5*D.Clamp(HistoricalSiteDensity(s,cell)/.08));}
            foreach(int cell in CulturalCells(s))
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int id=society.CellCulture[cell];
                if(id<0||HistoricalLandArea(cell)<=0||World.Hydrology.LakeCoverageFraction[cell]>=.5||RuralCellPopulation(s,cell)<10)continue;
                var culture=society.Cultures[id];
                if(Math.Round(D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[culture.OriginCell].Position)*World.Recipe.RadiusKm*1000)>300000)continue;
                int native=NativeCropMask(cell);double density=.5+.5*D.Clamp(HistoricalSiteDensity(s,cell)/.08);
                for(int k=0;k<6;k++)
                {
                    var a=agriculture[id];if(a.AdoptedYear[k]>0)continue;int at=id*6+k;
                    if((native&(1<<k))!=0){a.NativeMask|=1<<k;if(a.WildOriginCell[k]<0)a.WildOriginCell[k]=cell;}
                    // A culture can experiment within nearby inhabited fields;
                    // physiological crop suitability is not its birthplace climate.
                    double score=CropSuitability(s,cell,k)*density;
                    if(score>scores[at]||score==scores[at]&&cell<sites[at]){scores[at]=score;sites[at]=cell;}
                }
            }
            for(int id=0;id<agriculture.Length;id++)for(int k=0;k<6;k++)if(agriculture[id].AdoptedYear[k]==0)agriculture[id].PracticeCell[k]=sites[id*6+k];
        }
        int HistoricalEconomicCenter(PrehistoryState s,CultureIdentity culture)
        {
            int best=culture.OriginCell;double score=culture.Farming>0?LocalStoredSupport(s,culture.Id,best):CultureOpportunity(s,best);
            foreach(int cell in s.History.Agriculture[culture.Id].PracticeCell.Distinct().OrderBy(i=>i))
            {
                if(s.Society.CellCulture[cell]!=culture.Id||RuralCellPopulation(s,cell)<10)continue;
                double q=culture.Farming>0?LocalStoredSupport(s,culture.Id,cell):CultureOpportunity(s,cell);
                if(q>score||q==score&&cell<best){best=cell;score=q;}
            }
            return best;
        }
    }
}

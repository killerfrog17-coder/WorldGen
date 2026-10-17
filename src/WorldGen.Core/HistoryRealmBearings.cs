using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        readonly Dictionary<long,int> realmBearingCache=new Dictionary<long,int>();
        readonly Queue<long> realmBearingOrder=new Queue<long>();
        int RealmBearingShore(int source,int bearing)
        {
            long key=((long)source<<6)|(uint)bearing;if(realmBearingCache.TryGetValue(key,out int cached))return cached;
            var start=World.Surface.Cells[source].Position;var east=V.Tangent(start,new V(0,0,1));if(east.Length<.5)east=V.Tangent(start,new V(1,0,0));var north=V.Cross(start,east).Unit;
            double angle=bearing*2*D.Pi/64;var tangent=east*D.Sin(angle+D.Pi/2)+north*D.Sin(angle);
            bool sea=false;int found=-1,lastNode=InstitutionalRasterNode(start);
            // Geometry-only proposal. A real paid survey must still sail,
            // survive, return and report before the state knows this coast.
            // The fixed raster and96 samples do not inspect target resources.
            for(int step=0;step<96;step++)
            {
                double distance=(50+140*step)/World.Recipe.RadiusKm;var point=(start*D.Sin(distance+D.Pi/2)+tangent*D.Sin(distance)).Unit;
                int node=InstitutionalRasterNode(point);
                if(aggregateOceanRaster[node])
                {
                    sea=true;lastNode=node;int shore=communityRasterShore[node];
                    if(shore>=0&&CellPatch[shore]!=CellPatch[source]&&D.Angle(point,World.Surface.Cells[shore].Position)*World.Recipe.RadiusKm<150){found=shore;break;}
                }
                else if(sea)
                {
                    double best=double.MaxValue;int x=node%OceanRasterWidth,y=node/OceanRasterWidth;
                    void Offer(int at)
                    {int shore=communityRasterShore[at];if(shore<0)return;double km=D.Angle(point,World.Surface.Cells[shore].Position)*World.Recipe.RadiusKm;if(km<best||km==best&&(found<0||shore<found)){best=km;found=shore;}}
                    Offer(lastNode);
                    for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){int row=y+dy;if(row>=0&&row<OceanRasterHeight)Offer(row*OceanRasterWidth+(x+dx+OceanRasterWidth)%OceanRasterWidth);}
                    if(best>350||found>=0&&CellPatch[found]==CellPatch[source])found=-1;
                    break;
                }
                else if(step>=2)break;
            }
            if(realmBearingCache.Count>=16384)realmBearingCache.Remove(realmBearingOrder.Dequeue());realmBearingCache.Add(key,found);realmBearingOrder.Enqueue(key);return found;
        }
    }
}

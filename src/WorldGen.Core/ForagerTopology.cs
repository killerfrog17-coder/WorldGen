using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Demographic districts subdivide existing ecological patches. They do
        // not rename, merge or change the world's ecoregions or terrain cells.
        internal void BuildForagerTopology(ForagerState state,CancellationToken token=default)
        {
            var cells=World.Surface.Cells;var assigned=Enumerable.Repeat(-1,cells.Length).ToArray();var districts=new List<ForagerDistrict>();
            double Distance(V a,V b)=>World.Recipe.RadiusKm*Math.Acos(D.Clamp(V.Dot(a,b),-1,1));
            foreach(var patch in Patches.Select((p,id)=>(p,id)))foreach(int seed in patch.p.Cells)
            {
                if(assigned[seed]>=0)continue;token.ThrowIfCancellationRequested();int id=districts.Count;var members=new List<int>();var queue=new Queue<int>();queue.Enqueue(seed);assigned[seed]=id;
                double area=0;
                while(queue.Count>0)
                {
                    int at=queue.Dequeue();members.Add(at);area+=cells[at].AreaM2/1e6;
                    foreach(int next in cells[at].Neighbors.OrderBy(x=>x))
                    {
                        if(assigned[next]>=0||CellPatch[next]!=patch.id||members.Count+queue.Count>=16||area>80000||Distance(cells[seed].Position,cells[next].Position)>220)continue;
                        assigned[next]=id;queue.Enqueue(next);
                    }
                }
                var d=new ForagerDistrict{Id=id,Patch=patch.id,Province=World.Ecology.Province[seed],Realm=-1,Cell=seed,Cells=members.OrderBy(x=>x).ToArray(),AreaKm2=area};V center=new V();
                foreach(int cell in d.Cells)center+=cells[cell].Position*(cells[cell].AreaM2/1e6);d.Center=center.Unit;
                d.Cell=d.Cells.OrderByDescending(c=>V.Dot(cells[c].Position,d.Center)).First();d.RadiusKm=Math.Sqrt(area/Math.PI);
                var ground=GroundEnvironment.For(World);
                foreach(int cell in d.Cells)
                {
                    double weight=cells[cell].AreaM2/1e6/area;var h=World.Ecology.Habitat[cell];var soil=ground.Cell(cell);
                    double coast=cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n])?1:0;
                    double river=D.Clamp(HistoricalRiverAccess(cell)*8+h.FloodFractionPermille/1000.0);
                    double canopy=AnimalEcotypes.Canopy(h)/1000.0,aridity=h.AridityMilli/1000.0;
                    d.Productivity+=weight*h.ProductivityGCM2Year;d.WinterC+=weight*h.ColdTenthsC/10.0;d.SummerC+=weight*h.WarmTenthsC/10.0;d.MeanC+=weight*h.MeanTenthsC/10.0;
                    d.Aridity+=weight*aridity;d.GrowingDays+=weight*h.GrowingDays;d.Canopy+=weight*canopy;d.Coast+=weight*coast;d.River+=weight*river;d.Relief+=weight*h.ReliefM;
                    d.Water+=weight*D.Clamp(.65*D.Clamp(aridity/.8)+.45*river+.15*(h.WaterTableCm<150?1:0));
                    d.Pathogens+=weight*D.Clamp((h.MeanTenthsC/10.0-10)/20)*D.Clamp(aridity/1.1)*(.65+.35*canopy);
                    int material=ToolStone.Encode(ToolStone.Source(soil,cells[cell].Position,World.Recipe.Seed,World.Recipe.RadiusKm*1000),0);
                    d.Stone+=weight*(.1+.9*ToolStone.Quality(material));d.Wood+=weight*D.Clamp(.06+.9*canopy+.18*D.Clamp(aridity));
                    d.Fiber+=weight*D.Clamp(.08+.45*canopy+.65*river+.15*D.Clamp(aridity));d.Resin+=weight*D.Clamp(.05+.6*canopy);
                    double nativeSmall=0,nativeLarge=0,nativeFish=0;
                    int region=World.Ecology.Region[cell];
                    if(region>=0)foreach(var presence in World.Ecology.Regions[region].Species)
                    {
                        var taxon=World.Ecology.Taxa[presence.TaxonId];if(taxon.Producer||taxon.BasalResource||!Biogeography.Present(World.Ecology,cell,taxon.Id))continue;
                        if((taxon.Occupies&Stratum.Water)!=0)nativeFish+=.08;
                        else if(taxon.MassKg>=10&&(taxon.Eats&(Food.Grass|Food.Browse|Food.Fruit|Food.Seed))!=0)nativeLarge+=Math.Min(.2,.04+Math.Sqrt(taxon.MassKg)/150);
                        else if(taxon.MassKg>=.05&&taxon.MassKg<20)nativeSmall+=.035;
                    }
                    double productivity=D.Clamp(h.ProductivityGCM2Year/850.0),dry=D.Clamp(aridity/.7),season=D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/350.0);
                    d.Food[0]+=weight*(.06+.9*productivity)*dry*(.4+.6*D.Clamp(h.GrowingDays/240.0));
                    d.Food[1]+=weight*(.06+.5*productivity)*D.Clamp(.3+nativeSmall);
                    d.Food[2]+=weight*(.08+.6*productivity)*D.Clamp(.25+nativeLarge)*(1-.45*canopy);
                    d.Food[3]+=weight*river*(.15+.85*D.Clamp(nativeFish+.2));d.Food[4]+=weight*coast*(.25+.75*D.Clamp(nativeFish+.3));
                    d.Seasonality[0]+=weight*(.15+.75*season);d.Seasonality[1]+=weight*(.15+.35*season);d.Seasonality[2]+=weight*(.2+.6*season);d.Seasonality[3]+=weight*(.25+.6*season);d.Seasonality[4]+=weight*(.15+.35*season);
                }
                districts.Add(d);
            }
            var links=new Dictionary<(int,int),ForagerPassage>[districts.Count];for(int i=0;i<links.Length;i++)links[i]=new Dictionary<(int,int),ForagerPassage>();
            void Link(int a,int b,int from,int to,int distance,bool sea,bool shelf,int fall)
            {
                if(a<0||b<0||a==b)return;
                var da=districts[a];var db=districts[b];int metres=(int)Math.Max(distance,Distance(da.Center,db.Center)*1000);
                var pass=new ForagerPassage{To=b,FromCell=from,ToCell=to,DistanceM=metres,Sea=sea,Shelf=shelf,SeaFallM=fall,MountainBarrier=D.Clamp((Math.Max(da.Relief,db.Relief)-500)/1800),WaterCost=D.Clamp(1-(da.Water+db.Water)*.5),Exposure=(da.Seasonality[0]+db.Seasonality[0])*.5};
                var key=(b,!sea?-1:shelf?fall:0);if(!links[a].TryGetValue(key,out var existing)||pass.DistanceM<existing.DistanceM)links[a][key]=pass;
            }
            foreach(var edge in World.Surface.Edges)
            {
                int a=assigned[edge.A],b=assigned[edge.B];if(a<0||b<0||World.Hydrology.OceanConnected[edge.A]||World.Hydrology.OceanConnected[edge.B])continue;
                Link(a,b,edge.A,edge.B,edge.LengthM,false,false,0);Link(b,a,edge.B,edge.A,edge.LengthM,false,false,0);
            }
            foreach(var route in Routes.Where(r=>r.Sea))
            {
                int from=route.CellA>=0?route.CellA:Patches[route.A].Cell,to=route.CellB>=0?route.CellB:Patches[route.B].Cell;
                Link(assigned[from],assigned[to],from,to,route.DistanceM,true,route.Shallow,route.SeaDropM);Link(assigned[to],assigned[from],to,from,route.DistanceM,true,route.Shallow,route.SeaDropM);
            }
            for(int i=0;i<districts.Count;i++)districts[i].Passages=links[i].Values.OrderBy(p=>p.To).ToArray();
            int realm=0;foreach(var seed in districts)if(seed.Realm<0)
            {
                var queue=new Queue<int>();queue.Enqueue(seed.Id);seed.Realm=realm;
                while(queue.Count>0){int at=queue.Dequeue();foreach(var pass in districts[at].Passages)if(!pass.Sea&&districts[pass.To].Realm<0){districts[pass.To].Realm=realm;queue.Enqueue(pass.To);}}realm++;
            }
            state.Districts=districts.ToArray();state.CellDistrict=assigned;
        }
    }
}

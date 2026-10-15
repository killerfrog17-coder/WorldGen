using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Continuous display triangulation. Simulation cells and category palettes are untouched.</summary>
    public static class NaturalSurface
    {
        public const int FacesPerChunk=8192;
        public static V LandColor(World w,int i,Layer layer)
        {
            if(w.Hydrology.OceanConnected[i])
            {
                int neighbor=-1;foreach(int j in w.Surface.Cells[i].Neighbors)if(!w.Hydrology.OceanConnected[j]){neighbor=j;break;}
                if(neighbor>=0)i=neighbor;else return new V(.38,.49,.29);
            }
            V color=layer==Layer.Satellite && w.Climate.Classification.Length>i?ClimateColors.Satellite(w,i):GlobeView.Elevation(Math.Max(0,w.ElevationM[i]));
            return ShoreTerrainSampler.IsWetlandCell(w,i)?color*.8+new V(.16,.27,.19)*.2:color;
        }
        public static V IslandColor(World w,int cell,double peak)
        {
            if(w.Climate.Classification.Length<=cell)return new V(.38,.49,.29);
            var seasons=new[]{w.Climate.NorthernSpring,w.Climate.NorthernSummer,w.Climate.NorthernAutumn,w.Climate.NorthernWinter};
            double hot=seasons.Max(s=>s.TemperatureTenthsC[cell])/10.0-peak*.006,mean=seasons.Average(s=>s.TemperatureTenthsC[cell])/10.0;
            double rain=seasons.Sum(s=>s.PrecipitationMm[cell]);
            V color=rain<250?new V(.73,.64,.43):rain<650?new V(.52,.52,.28):mean>20?new V(.12,.30,.12):new V(.28,.4,.22);
            double cold=D.Clamp((10-hot)/12);color=color*(1-cold)+new V(.48,.49,.42)*cold;
            double snow=D.Clamp((2-hot)/7);return color*(1-snow)+new V(.93,.96,.97)*snow;
        }
        public static float[] Colors(World w,Layer layer,CancellationToken token)
        {
            var result=new float[w.Surface.Cells.Length*3];
            for(int i=0;i<w.Surface.Cells.Length;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();V c=LandColor(w,i,layer);result[i*3]=(float)c.X;result[i*3+1]=(float)c.Y;result[i*3+2]=(float)c.Z;}return result;
        }
        public static IEnumerable<ViewData> Chunks(World w,Projection projection,CancellationToken token)
        {
            var s=w.Surface;
            for(int first=0;first<s.Triangles.Length;first+=FacesPerChunk*3)
            {
                token.ThrowIfCancellationRequested();int end=Math.Min(first+FacesPerChunk*3,s.Triangles.Length);
                var used=new Dictionary<int,int>();var positions=new List<float>();var heights=new List<float>();var ids=new List<int>();var triangles=new int[end-first];
                for(int j=first;j<end;j++)
                {
                    int i=s.Triangles[j];if(!used.TryGetValue(i,out int local))
                    {
                        local=ids.Count;used.Add(i,local);ids.Add(i);int h=w.ElevationM[i];
                        V p=s.Cells[i].Position*(1+Math.Max(0,h)*14/6371000.0);
                        positions.Add((float)p.X);positions.Add((float)p.Z);positions.Add((float)-p.Y);
                        heights.Add(w.Hydrology.OceanConnected[i]?Math.Min(-1,h):Math.Max(1,h));
                    }
                    triangles[j-first]=local;
                }
                var data=new ViewData{Positions=positions.ToArray(),Colors=new float[positions.Count],CellIds=ids.ToArray(),Triangles=triangles,NaturalHeight=heights.ToArray()};
                yield return projection==Projection.Map?MapView.Project(data,0):data;
            }
        }
        public static IEnumerable<ViewData> WaterAndIslands(World w,Projection projection,CancellationToken token)
        {
            var positions=new List<float>();var colors=new List<float>();var ids=new List<int>();var triangles=new List<int>();var sizes=new List<float>();double angularSize=0;
            void Emit(V p,int cell,V color,double height)
            {
                p*=1.00035+Math.Max(0,height)*14/6371000.0;
                positions.Add((float)p.X);positions.Add((float)p.Z);positions.Add((float)-p.Y);colors.Add((float)color.X);colors.Add((float)color.Y);colors.Add((float)color.Z);ids.Add(cell);triangles.Add(ids.Count-1);sizes.Add((float)angularSize);
            }
            void Polygon(V center,V[] outline,int cell,V color,double peak,double foot)
            {
                for(int j=0;j<outline.Length;j++){Emit(center,cell,color,peak);Emit(outline[j],cell,color,foot);Emit(outline[(j+1)%outline.Length],cell,color,foot);}
            }
            ViewData Flush()
            {
                var d=new ViewData{Positions=positions.ToArray(),Colors=colors.ToArray(),CellIds=ids.ToArray(),Triangles=triangles.ToArray(),NaturalHeight=sizes.ToArray(),Unlit=true};
                positions.Clear();colors.Clear();ids.Clear();triangles.Clear();sizes.Clear();return projection==Projection.Map?MapView.Project(d,0,-.004):d;
            }
            var sampler=new ShoreTerrainSampler(w,token);
            foreach(var feature in sampler.Features)
            {
                token.ThrowIfCancellationRequested();
                // Wetness is blended into the continuous ground palette, never layered as an opaque stamp over a lake.
                if(feature.Kind==ShoreKind.Wetland)continue;
                angularSize=Math.Sqrt(feature.AreaM2/Math.PI)/(w.Recipe.RadiusKm*1000);
                if(feature.Kind!=ShoreKind.Island)angularSize=-angularSize; // Signed display tag: water versus island, independent of population mask.
                var outline=sampler.Outline(feature,64);
                if(feature.Kind==ShoreKind.Island)
                {
                    V color=IslandColor(w,feature.Cell,feature.ReliefM);
                    Polygon(feature.Center,outline,feature.Cell,color,feature.ReliefM,2);
                }
                else Polygon(feature.Center,outline,feature.Cell,new V(.04,.18,.32),feature.LevelM,feature.LevelM);
                if(ids.Count>=24000)yield return Flush();
            }
            if(ids.Count>0)yield return Flush();
        }
    }
}

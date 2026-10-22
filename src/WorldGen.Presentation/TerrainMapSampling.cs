using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class TerrainMapSampling
    {
        // Numerical climate/ecology fields remain the recorded cell values too;
        // continuity is not fabricated just because a legend contains numbers.
        public static bool Continuous(Layer layer)=>layer==Layer.Terrain||layer==Layer.Satellite||layer==Layer.Heightmap||layer==Layer.Relief||layer==Layer.FinalElevation||layer==Layer.Bathymetry||layer==Layer.TreeDensity||Historical(layer);
        static bool Historical(Layer layer)=>layer==Layer.TectonicElevation||layer==Layer.PreErosionElevation||layer==Layer.ErosionDelta;
        public static int RasterWidth(TerrainMapViewport view)=>view.LongitudeSpan>60?128:view.LongitudeSpan>8?192:view.LongitudeSpan>1?256:384;

        internal sealed class Sampler
        {
            readonly LocalTerrain terrain;
            readonly LocalTerrain.MapHeightCursor heights;
            readonly Dictionary<(int,int,int),LocalTerrain.LocalTerrainPatch> patches=new Dictionary<(int,int,int),LocalTerrain.LocalTerrainPatch>();
            readonly Queue<(int,int,int)> order=new Queue<(int,int,int)>();
            readonly Dictionary<(int,int,int,int,bool),LocalTerrainSample> samples=new Dictionary<(int,int,int,int,bool),LocalTerrainSample>();
            readonly Queue<(int,int,int,int,bool)> sampleOrder=new Queue<(int,int,int,int,bool)>();
            public long Hits {get;private set;}public long Misses {get;private set;}
            public Sampler(LocalTerrain terrain){this.terrain=terrain;heights=terrain.CreateMapHeightCursor();}
            public LocalTerrainSample Sample(V p,double footprint=0,bool heightOnly=false)
            {
                if(footprint<=0)return Query(p,0,false,0);
                int band=Math.Max(-8,Math.Min(24,(int)Math.Floor(Math.Log(footprint/8,2))));double spacing=8*Math.Pow(2,band);
                int grid=Math.Max(1,Math.Min(30,(int)Math.Ceiling(Math.Log(2*terrain.RadiusM*terrain.HorizontalScale/Math.Max(.025,spacing*.25),2))));int count=1<<grid;
                double ax=Math.Abs(p.X),ay=Math.Abs(p.Y),az=Math.Abs(p.Z);int face=ax>=ay&&ax>=az?(p.X>=0?0:1):ay>=az?(p.Y>=0?2:3):(p.Z>=0?4:5);
                V normal=face==0?new V(1,0,0):face==1?new V(-1,0,0):face==2?new V(0,1,0):face==3?new V(0,-1,0):face==4?new V(0,0,1):new V(0,0,-1);
                V east=face<4?V.Cross(new V(0,0,1),normal).Unit:new V(1,0,0),north=V.Cross(normal,east).Unit;double d=V.Dot(p,normal);
                int x=Math.Max(0,Math.Min(count-1,(int)((V.Dot(p,east)/d+1)*.5*count))),y=Math.Max(0,Math.Min(count-1,(int)((V.Dot(p,north)/d+1)*.5*count)));
                var key=(face,x,y,band,heightOnly);if(samples.TryGetValue(key,out var cached)){Hits++;return cached;}
                // Canonical subpixel addresses allow nearby pans/zoom bands to
                // reuse samples, not merely bit-identical screen coordinates.
                V point=(normal+east*((x+.5)*2/count-1)+north*((y+.5)*2/count-1)).Unit;
                var result=Query(point,spacing,heightOnly,band);Misses++;
                if(sampleOrder.Count>=4096)samples.Remove(sampleOrder.Dequeue());samples.Add(key,result);sampleOrder.Enqueue(key);return result;
            }
            LocalTerrainSample Query(V p,double footprint,bool heightOnly,int band)
            {
                if(heightOnly&&footprint>128)return heights.Sample(p,footprint);
                TerrainMapViewport.Degrees(p,out double lat,out double lon);
                double sector=footprint<=0?2048/terrain.HorizontalScale:Math.Max(2048/terrain.HorizontalScale,Math.Min(90000,footprint/terrain.HorizontalScale*16));
                sector=Math.Min(90000,sector);int rows=Math.Max(2,(int)Math.Ceiling(Math.PI*terrain.RadiusM/sector)),row=Math.Min(rows-1,(int)((lat+90)/180*rows));
                double centerLat=-90+(row+.5)*180/rows;
                int columns=Math.Max(1,(int)Math.Ceiling(2*Math.PI*terrain.RadiusM*Math.Cos(centerLat*Math.PI/180)/sector));
                int column=Math.Min(columns-1,(int)((TerrainMapViewport.Wrap(lon)+180)/360*columns));var key=(row,column,footprint<=0?int.MinValue:band);
                if(!patches.TryGetValue(key,out var patch))
                {
                    patch=terrain.MapPatch(TerrainMapViewport.FromDegrees(centerLat,-180+(column+.5)*360/columns),Math.Min(150000,sector*1.67));
                    if(order.Count>=12)patches.Remove(order.Dequeue());patches.Add(key,patch);order.Enqueue(key);
                }
                return footprint<=0?patch.Sample(p):patch.SampleMap(p,footprint);
            }
        }
        public static double TreeDensity(LocalTerrain terrain,World? world,LocalTerrainSample sample,V point,double up)
        {
            if(sample.Underwater||sample.ElevationM<2)return 0;
            if(world!=null)
            {
                var e=world.Ecology;var h=e!=null&&sample.Cell>=0&&sample.Cell<e.Habitat.Length?e.Habitat[sample.Cell]:null;
                double sum=0;foreach(var species in terrain.FloraRegion(sample.Cell).Trees)
                    sum+=TreeFlora.SpeciesWeight(species,h,sample,point,terrain.SourceSeed,terrain.RadiusM*terrain.HorizontalScale,world.ElevationM[sample.Cell]);
                if(sum<.001)return 0;
            }
            return 10000*TreeFlora.PlacementPerM2*TreeFlora.Stand(point,terrain.SourceSeed,terrain.RadiusM*terrain.HorizontalScale)*TreeFlora.HabitatAdmission(sample)*TreeFlora.SlopeAdmission(up);
        }
        public static TerrainMapImage Build(LocalTerrain terrain,TerrainMapViewport view,int width,int height,CancellationToken token,Layer layer,float[]? cellColors,World? world,TerrainMapContext? context=null,int canvasWidth=0,int canvasHeight=0,int offsetX=0,int offsetY=0)
        {
            if(width<2||height<2||width>2048||height>1024)throw new ArgumentOutOfRangeException(nameof(width));
            if(canvasWidth==0)canvasWidth=width;if(canvasHeight==0)canvasHeight=height;
            var image=new TerrainMapImage{Width=width,Height=height,View=view.Clone(),Rgba=new byte[width*height*4]};
            bool continuous=Continuous(layer);var sampler=context?.Sampler??new Sampler(terrain);double radius=terrain.RadiusM*terrain.HorizontalScale;
            var points=new V[width*height];var samples=continuous&&!Historical(layer)?new LocalTerrainSample[width*height]:Array.Empty<LocalTerrainSample>();
            double footprint=radius*view.LatitudeSpan*Math.PI/180/canvasHeight*.45;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                token.ThrowIfCancellationRequested();int at=y*width+x;V p=points[at]=view.Position((x+offsetX+.5)/canvasWidth,1-(y+offsetY+.5)/canvasHeight);
                if(samples.Length>0)samples[at]=sampler.Sample(p,footprint,layer!=Layer.Satellite&&layer!=Layer.TreeDensity);
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                token.ThrowIfCancellationRequested();int at=y*width+x;V p=points[at],color;
                if(!continuous)
                {
                    int cell=terrain.NearestCell(p);color=cellColors==null?GlobeView.Elevation((int)terrain.Profile(cell).ElevationM):new V(cellColors[cell*3],cellColors[cell*3+1],cellColors[cell*3+2]);
                }
                else if(Historical(layer)&&world!=null)
                {
                    double value=HistoricalValue(terrain,world,p,layer);
                    color=layer==Layer.ErosionDelta?Mix(new V(.16,.19,.21),value>=0?new V(.93,.65,.23):new V(.2,.68,.93),Math.Abs(value)/500):GlobeView.Elevation((int)value);
                }
                else
                {
                    var s=samples.Length>0?samples[at]:sampler.Sample(p,footprint);
                    int left=y*width+Math.Max(0,x-1),right=y*width+Math.Min(width-1,x+1),down=Math.Max(0,y-1)*width+x,top=Math.Min(height-1,y+1)*width+x;
                    double sx=samples.Length==0?0:(samples[right].ElevationM-samples[left].ElevationM)/Math.Max(.01,(points[right]-points[left]).Length*radius);
                    double sz=samples.Length==0?0:(samples[top].ElevationM-samples[down].ElevationM)/Math.Max(.01,(points[top]-points[down]).Length*radius);
                    double up=1/Math.Sqrt(1+sx*sx+sz*sz),shade=.65+.35*D.Clamp((1-.55*sx+.65*sz)*up);
                    if(layer==Layer.Heightmap){double v=D.Clamp((s.ElevationM+5000)/11000);color=new V(v,v,v);}
                    else if(layer==Layer.TreeDensity)color=s.Underwater?new V(.05,.15,.23):Mix(new V(.72,.68,.53),new V(.025,.32,.10),Math.Sqrt(D.Clamp(TreeDensity(terrain,world,s,p,up)/340)));
                    else if(layer==Layer.Bathymetry)color=s.ElevationM<0?GlobeView.Elevation((int)s.ElevationM):new V(.32,.4,.35);
                    else if(layer==Layer.Satellite)
                    {
                        if(s.Underwater)color=s.WaterKind==LocalWaterKind.Ocean?GlobeView.Elevation((int)Math.Min(-1,s.ElevationM)):new V(.055,.27,.39);
                        else
                        {
                            var soil=LocalGeology.Surface(s.Ground,p,terrain.SourceSeed,s.ElevationM,up,s.OceanInfluence,s.Biome==Biome.Ice?1:0,radius);
                            var cover=GroundFlora.Surface(s.Cover,p,terrain.SourceSeed,radius,s.Ground,s.ElevationM,s.BaseElevationM,up,s.Underwater,s.Biome==Biome.Ice?1:0,s.Biome);
                            color=Mix(soil.Color,cover.Color*.90,cover.Coverage/.45);
                            var forest=TreeFlora.Surface(s.Forest,p,terrain.SourceSeed,radius,s.ElevationM,up,s.Underwater);
                            color=Mix(color,s.MidCover.Color,s.MidCover.Coverage*MidFlora.StandDensity(p,terrain.SourceSeed,radius));color=Mix(color,forest.Color,forest.Coverage*.8);
                        }
                        color*=shade;
                    }
                    else if(layer==Layer.Relief)color=(GlobeView.Elevation((int)s.ElevationM)*.45+new V(.50,.52,.46)*.55)*shade;
                    else color=GlobeView.Elevation((int)s.ElevationM)*shade;
                }
                int i=at*4;image.Rgba[i]=(byte)(255*D.Clamp(color.X));image.Rgba[i+1]=(byte)(255*D.Clamp(color.Y));image.Rgba[i+2]=(byte)(255*D.Clamp(color.Z));image.Rgba[i+3]=255;
            }
            return image;
        }
        static V Mix(V a,V b,double weight)=>a*(1-D.Clamp(weight))+b*D.Clamp(weight);
        static double HistoricalValue(LocalTerrain t,World w,V p,Layer layer)=>layer==Layer.TectonicElevation?t.SourceField(p,w.TectonicM):layer==Layer.PreErosionElevation?t.SourceField(p,w.PreErosionM):t.SourceField(p,w.ElevationM)-t.SourceField(p,w.PreErosionM);
        static string F(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        public static string Inspect(LocalTerrain terrain,World? world,V point,Layer layer,CancellationToken token=default,double primeMeridian=0)
        {
            token.ThrowIfCancellationRequested();int cell=terrain.NearestCell(point);TerrainMapViewport.Degrees(point,out double lat,out double lon);
            string prefix=GlobeView.LayerName(layer)+" · "+F(lat)+"°, "+F(TerrainMapViewport.Wrap(lon-primeMeridian))+"°\n";
            if(Continuous(layer))
            {
                if(Historical(layer)&&world!=null)return prefix+F(HistoricalValue(terrain,world,point,layer))+" m · interpolated historical source field";
                var sampler=new Sampler(terrain);var s=sampler.Sample(point);
                string height="Terrain elevation "+F(s.ElevationM)+" m"+(s.Underwater?" · "+s.WaterKind+" surface "+F(s.WaterSurfaceM)+" m":"");
                if(layer==Layer.TreeDensity||layer==Layer.Relief)
                {
                    V east=V.Cross(Math.Abs(point.Z)<.99?new V(0,0,1):new V(0,1,0),point).Unit,north=V.Cross(point,east).Unit;double d=1.5/(terrain.RadiusM*terrain.HorizontalScale);
                    double sx=(sampler.Sample((point+east*d).Unit).ElevationM-sampler.Sample((point-east*d).Unit).ElevationM)/3;
                    double sz=(sampler.Sample((point+north*d).Unit).ElevationM-sampler.Sample((point-north*d).Unit).ElevationM)/3;double up=1/Math.Sqrt(1+sx*sx+sz*sz);
                    return prefix+height+"\n"+(layer==Layer.TreeDensity?F(TreeDensity(terrain,world,s,point,up))+" expected trees/ha · shared placement probability field, not a count of drawn models":
                        "Slope "+F(Math.Acos(up)*180/Math.PI)+"° · hill amplitude "+F(s.HillHeightM)+" m · mountain amplitude "+F(s.MountainHeightM)+" m");
                }
                return prefix+height+(layer==Layer.Bathymetry?"\nDepth below sea datum "+F(Math.Max(0,-s.ElevationM))+" m":"")+"\nFull-detail terrain query at selection; preview is footprint-filtered.";
            }
            if(world==null)return prefix+"Source cell "+cell+"; saved layer data unavailable.";
            var w=world;string value;var h=w.Ecology!=null&&cell<w.Ecology.Habitat.Length?w.Ecology.Habitat[cell]:null;
            ClimateSeason? season=layer==Layer.TemperatureSpring||layer==Layer.PrecipitationSpring?w.Climate.NorthernSpring:layer==Layer.TemperatureSummer||layer==Layer.PrecipitationSummer||layer==Layer.WindSummer||layer==Layer.OceanCurrentsSummer?w.Climate.NorthernSummer:layer==Layer.TemperatureAutumn||layer==Layer.PrecipitationAutumn?w.Climate.NorthernAutumn:layer==Layer.TemperatureWinter||layer==Layer.PrecipitationWinter||layer==Layer.WindWinter||layer==Layer.OceanCurrentsWinter?w.Climate.NorthernWinter:null;
            if(season!=null)
            {
                if(layer==Layer.TemperatureSpring||layer==Layer.TemperatureSummer||layer==Layer.TemperatureAutumn||layer==Layer.TemperatureWinter)value=F(season.TemperatureTenthsC[cell]*.1)+" °C";
                else if(layer==Layer.PrecipitationSpring||layer==Layer.PrecipitationSummer||layer==Layer.PrecipitationAutumn||layer==Layer.PrecipitationWinter)value=season.PrecipitationMm[cell]+" mm / recorded season";
                else if(layer==Layer.WindSummer||layer==Layer.WindWinter)value="Wind east "+season.WindEastMilli[cell]+", north "+season.WindNorthMilli[cell]+" (milli model vector units)";
                else value="Current east "+season.CurrentEastMilli[cell]+", north "+season.CurrentNorthMilli[cell]+" · warmth anomaly "+season.OceanWarmthMilli[cell]+" (milli model units)";
            }
            else switch(layer)
            {
                case Layer.Climate:value=w.Climate.Classification[cell].ToString()+" · Köppen classification";break;
                case Layer.Biomes:value=h?.Biome.ToString()??"No ecology";break;
                case Layer.Ecoregions:int region=w.Ecology==null?-1:w.Ecology.Region[cell];value=region<0?"No terrestrial ecoregion":w.Ecology!.Regions[region].Name+" · region "+region;break;
                case Layer.Soils:value=h==null?"No ecology":h.Soil+" · cell soil category";break;
                case Layer.Aridity:value=h==null?"No ecology":F(h.AridityMilli*.001)+" precipitation/PET";break;
                case Layer.Floodplains:value=h==null?"No ecology":F(h.FloodFractionPermille*.1)+"% potential flood extent · "+h.FloodDaysPerYear+" days/year";break;
                case Layer.WaterTable:value=h==null?"No ecology":F(h.WaterTableCm*.01)+" m below ground (proxy)";break;
                case Layer.Permafrost:value=h==null?"No ecology":F(h.PermafrostPermille*.1)+"% potential";break;
                case Layer.Salinity:value=h==null?"No ecology":F(h.SalinityMilliPpt*.001)+" ppt";break;
                case Layer.FirePotential:value=h==null?"No ecology":F(h.FirePotentialPermille*.1)+"% index";break;
                case Layer.Continentality:value=F(w.Climate.ContinentalityPermille[cell]*.1)+"% index";break;
                case Layer.Microplates:value="Plate "+w.CellMicroplate[cell];break;
                case Layer.ParentPlates:value="Superplate "+w.Microplates[w.CellMicroplate[cell]].ParentAffinityId;break;
                case Layer.Continents:value="Continent "+w.Continent[cell];break;
                case Layer.Landmasses:case Layer.LandBridges:value="Landmass "+w.Landmass[cell];break;
                case Layer.Crust:value=w.Continent[cell]>=0?"Continental":"Oceanic";break;
                case Layer.OceanConnectivity:value=w.Hydrology.OceanConnected[cell]?"Ocean-connected":"Not ocean-connected";break;
                case Layer.Watersheds:value="Watershed "+w.Hydrology.Watershed[cell];break;
                case Layer.ContributingArea:value=F(w.Hydrology.ContributingAreaM2[cell]/1e6)+" km² upstream area";break;
                case Layer.Stress:value=w.Stress[cell]+" model stress units";break;
                case Layer.Mantle:value=w.MantlePermille[cell]+" permille mantle proxy";break;
                case Layer.WaterBodies:case Layer.LakesAndInlandSeas:int body=w.Hydrology.WaterBody[cell];value="Water-body source ID "+body;break;
                case Layer.Rivers:value="Source drainage cell; see waterways overlay for subcell routes.";break;
                case Layer.BoundaryType:case Layer.CollisionContext:
                    value="No incident boundary";double stress=-1;foreach(var b in w.Boundaries){var edge=w.Surface.Edges[b.EdgeId];if((edge.A==cell||edge.B==cell)&&b.Stress>stress){stress=b.Stress;value=layer==Layer.BoundaryType?b.Kind.ToString():b.Context.ToString();}}break;
                default:value="Recorded source cell "+cell;break;
            }
            return prefix+value+"\nSource cell "+cell+" · recorded cell value, not interpolated.";
        }
    }
}

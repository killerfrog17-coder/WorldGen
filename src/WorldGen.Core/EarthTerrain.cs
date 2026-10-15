using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Offline, measured ETOPO2022 relief. No simulated tectonics or erosion are applied to Earth heights.</summary>
    public static class EarthTerrain
    {
        public const string Dataset = "ETOPO2022-ice-surface-10arcmin-v1";
        static readonly Lazy<Grid> Data = new Lazy<Grid>(() => new Grid());
        sealed class Grid
        {
            public readonly int Width, Height;
            public readonly double South, West, Step;
            public readonly short[] Elevation;
            public readonly bool[] Ocean;
            public Grid()
            {
                using (var stream = typeof(EarthTerrain).Assembly.GetManifestResourceStream("WorldGen.Earth.ETOPO2022"))
                using (var reader = new BinaryReader(stream ?? throw new InvalidOperationException("Bundled Earth relief is missing.")))
                {
                    Width = reader.ReadInt32(); Height = reader.ReadInt32();
                    South = reader.ReadDouble(); West = reader.ReadDouble(); Step = reader.ReadDouble();
                    if (Width != 2160 || Height != 1080) throw new InvalidDataException("Invalid Earth grid.");
                    Elevation = new short[Width * Height];
                    for (int i = 0; i < Elevation.Length; i++) Elevation[i] = reader.ReadInt16();
                }
                // Geographic seawater seeds retain narrow straits below the bundled/fine grid resolution.
                // They do not include closed continental basins such as the Caspian or Dead Sea.
                Ocean = new bool[Elevation.Length]; var queue = new Queue<int>();
                foreach (var p in new[] { new[] {0.0, -30.0}, new[] {0.0, -150.0}, new[] {-20.0, 80.0}, new[] {85.0, 0.0}, new[] {35.0, 18.0}, new[] {43.0, 34.0}, new[] {58.0, 20.0}, new[] {20.0, 38.0} })
                { int i = Index(p[0], p[1]); if (Elevation[i] < 0 && !Ocean[i]) { Ocean[i] = true; queue.Enqueue(i); } }
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), x = i % Width, y = i / Width;
                    Add(y * Width + (x + 1) % Width); Add(y * Width + (x + Width - 1) % Width);
                    if (y > 0) Add(i - Width); if (y + 1 < Height) Add(i + Width);
                }
                void Add(int i) { if (!Ocean[i] && Elevation[i] < 0) { Ocean[i] = true; queue.Enqueue(i); } }
            }
            public int Index(double latitude, double longitude)
            {
                longitude = (longitude % 360 + 360) % 360;
                int x = ((int)Math.Floor((longitude - West) / Step + .5) + Width) % Width;
                int y = Math.Max(0, Math.Min(Height - 1, (int)Math.Floor((latitude - South) / Step + .5)));
                return y * Width + x;
            }
        }
        public static int SampleMetres(double latitude, double longitude) => Data.Value.Elevation[Data.Value.Index(latitude, longitude)];
        internal static void SmallIslands(World w,List<SmallIsland> islands,CancellationToken token)
        {
            var g=Data.Value;var seen=new bool[g.Elevation.Length];var queue=new Queue<int>();var locator=new CellLocator(w.Surface.Cells);var occupied=new Dictionary<int,double>();
            double radius=w.Recipe.RadiusKm*1000.0,step=g.Step*Math.PI/180;
            for(int first=0;first<seen.Length;first++)
            {
                if((first&4095)==0)token.ThrowIfCancellationRequested();
                if(seen[first] || g.Elevation[first]<=0)continue;
                var pixels=new List<int>();seen[first]=true;queue.Enqueue(first);double area=0;V weighted=new V();int peak=0;
                while(queue.Count>0)
                {
                    int i=queue.Dequeue(),x=i%g.Width,y=i/g.Width;if((pixels.Count&4095)==0)token.ThrowIfCancellationRequested();pixels.Add(i);
                    double lat=(g.South+y*g.Step)*Math.PI/180,lon=(g.West+x*g.Step)*Math.PI/180;
                    double a=radius*radius*step*(Math.Sin(Math.Min(Math.PI/2,lat+step/2))-Math.Sin(Math.Max(-Math.PI/2,lat-step/2)));
                    weighted+=new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat))*a;area+=a;peak=Math.Max(peak,g.Elevation[i]);
                    void Add(int j){if(!seen[j] && g.Elevation[j]>0){seen[j]=true;queue.Enqueue(j);}}
                    Add(y*g.Width+(x+1)%g.Width);Add(y*g.Width+(x+g.Width-1)%g.Width);if(y>0)Add(i-g.Width);if(y+1<g.Height)Add(i+g.Width);
                }
                // Entire small measured land components only. Never turn missing mainland shoreline into fictional islands.
                if(area>20000e6 || area<10000 || weighted.Length<1)continue;
                V center=weighted.Unit;int cell=locator.Nearest(center);
                if(!w.Hydrology.OceanConnected[cell] || area>w.Surface.Cells[cell].AreaM2*.3)continue;
                bool resolved=false;
                foreach(int pixel in pixels)
                {
                    double lat=(g.South+pixel/g.Width*g.Step)*Math.PI/180,lon=(g.West+pixel%g.Width*g.Step)*Math.PI/180;
                    V p=new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat));
                    if(!w.Hydrology.OceanConnected[locator.Nearest(p)]){resolved=true;break;}
                }
                if(resolved)continue;
                occupied.TryGetValue(cell,out double existing);if(existing+area>w.Surface.Cells[cell].AreaM2*.4)continue;
                // Area and position are measured at source resolution; the small outline is an explicit proxy.
                var island=SubcellGeography.Create(islands.Count,cell,center,area,1.5,0,peak,first,radius);
                if(island.Outline.Any(p=>w.Surface.Cells[cell].Neighbors.Any(j=>V.Dot(p,w.Surface.Cells[j].Position)>V.Dot(p,w.Surface.Cells[cell].Position))))continue;
                island.Origin="Measured Earth island (coarse outline)";islands.Add(island);occupied[cell]=existing+area;
            }
        }
        public static void Apply(World w, CancellationToken token)
        {
            var grid = Data.Value; int n = w.Surface.Cells.Length;
            w.ElevationM = new int[n]; w.Hydrology.OceanConnected = new bool[n];
            w.Glaciated = new bool[n]; w.ProtectedInlandSea = new bool[n];
            w.FoldPermille = new int[n]; w.CratonPermille = new int[n]; w.BasinPermille = new int[n]; w.PlateauPermille = new int[n];
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                var p = w.Surface.Cells[i].Position;
                double latitude = (D.Pi / 2 - D.Angle(p, new V(0, 0, 1))) * 180 / D.Pi;
                double longitude = D.Angle(new V(p.X, p.Y, 0).Unit, new V(1, 0, 0)) * (p.Y < 0 ? -180 : 180) / D.Pi;
                int sample = grid.Index(latitude, longitude), height = grid.Elevation[sample];
                w.ElevationM[i] = height; w.Hydrology.OceanConnected[i] = grid.Ocean[sample];
                w.Glaciated[i] = !grid.Ocean[sample] && (latitude < -65 || latitude > 60 && longitude > -75 && longitude < -10 && height > 1200);
                w.CratonPermille[i] = 1000;
            }
            w.TectonicM = (int[])w.ElevationM.Clone(); w.PreErosionM = (int[])w.ElevationM.Clone();
            w.Features = Array.Empty<Feature>();
            w.Landmass = Graph.Components(w.Surface, i => !w.Hydrology.OceanConnected[i]);
            w.CoastDistanceM = Graph.Distances(w.Surface, Enumerable.Range(0, n).Where(i => w.Surface.Cells[i].Neighbors.Any(j => w.Hydrology.OceanConnected[i] != w.Hydrology.OceanConnected[j])), out _, token);
            w.Warnings.Add("Earth: measured " + Dataset + " relief; climate and drainage are simulated, not observed. Plate/province diagnostic labels are procedural scaffolding, not Earth's tectonic reconstruction. Terrain sculpting controls do not modify measured heights.");
        }
    }
}

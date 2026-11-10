using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Machine-local presentation policy. Never changes the sampled world.</summary>
    public sealed class TerrainDetailOptions
    {
        public int Quality=2, Effort=1;
        public double ViewDistanceM=200000, FullDetailTiles=1.5;
        // Presentation diagnostics only; defaults retain the ordinary plan.
        public bool EstimateRelief=true,ProximityRefinement=true;
        public int FinestSegments=64;
        static readonly double[] Errors={16,8,4,2,1},Budgets={1.5,3,5,8};
        static readonly int[] Lanes={1,2,3,4},Uploads={1,1,2,4};
        public double PixelError=>Errors[Math.Max(0,Math.Min(4,Quality))];
        public int Workers=>Lanes[Math.Max(0,Math.Min(3,Effort))];
        public int Installations=>Uploads[Math.Max(0,Math.Min(3,Effort))];
        public double UploadBudgetMs=>Budgets[Math.Max(0,Math.Min(3,Effort))];
        public double RequestInterval=>Effort==0?.025:0;
        public double FullRadius(double scale)=>TerrainLod.BaseSizeM*scale*Math.Max(1.5,Math.Min(8,FullDetailTiles));
        public double ProtectedRadius(double scale,double altitude)
        {
            double full=FullRadius(scale),height=Math.Max(0,Math.Abs(altitude)-TerrainMotion.EyeHeightM);
            // A ground-contact floor must not demand metre-scale meshes under
            // an aircraft thousands of metres away. Screen-space error and
            // proximity refinement still govern the airborne landscape.
            return full*Math.Max(0,Math.Min(1,(full-height)/(full*.5)));
        }
        public TerrainDetailOptions Copy()=>(TerrainDetailOptions)MemberwiseClone();
    }

    /// <summary>Bounded, cached geographical estimates, not a certified geometric-error bound.
    /// Profiles estimate unresolved relief; sampled control residuals detect broad bends, not slope alone.</summary>
    public sealed class TerrainDetailEstimator
    {
        readonly LocalTerrain terrain;readonly LocalTerrainFrame frame;
        readonly Dictionary<TerrainTileKey,(double amplitude,double wavelength,double bend,double dune,double sharp)> values=new Dictionary<TerrainTileKey,(double,double,double,double,double)>();
        public int Count=>values.Count;
        public TerrainDetailEstimator(LocalTerrain terrain,LocalTerrainFrame frame){this.terrain=terrain;this.frame=frame;}
        public double Error(TerrainTileKey key,int segments)
        {
            if(!values.TryGetValue(key,out var v))
            {
                var heights=new double[9];double amplitude=0,wavelength=double.MaxValue,dune=0,sharp=0;
                for(int z=0;z<3;z++)for(int x=0;x<3;x++)
                {
                    var at=frame.Position((key.X+x*.5)*key.SizeM,(key.Z+z*.5)*key.SizeM);
                    int cell=terrain.NearestCell(at);var p=terrain.Profile(cell);heights[z*3+x]=p.ElevationM;
                    sharp=Math.Max(sharp,terrain.SharpPresentationRelief(at,cell));
                    amplitude=Math.Max(amplitude,p.HillHeightM+p.MountainHeightM*.65);
                    wavelength=Math.Min(wavelength,Math.Max(250,p.RidgeSpacingM)*key.Scale);
                    double sand=LandscapeProvince.SandSea(at*frame.RadiusM,terrain.SourceSeed,p.Landscape);
                    dune=Math.Max(dune,OrogenMorphology.Smooth(.08,.4,sand));
                }
                double bend=0;
                for(int z=0;z<3;z++)bend=Math.Max(bend,Math.Abs(heights[z*3+1]-(heights[z*3]+heights[z*3+2])*.5));
                for(int x=0;x<3;x++)bend=Math.Max(bend,Math.Abs(heights[3+x]-(heights[x]+heights[6+x])*.5));
                v=(amplitude,wavelength,bend,dune,sharp);if(values.Count>=8192)values.Clear();values[key]=v;
            }
            double step=key.SizeM/segments;
            double local=v.amplitude*.4*Math.Min(1,Math.Pow(step/Math.Max(1,v.wavelength*.2),1.35));
            // Sharp dune crests have a linear sampling error near their cusp,
            // unlike the smooth regional hill estimate. Without this term a
            // flat desert profile rendered new high dunes as coarse slabs.
            double crest=v.dune*Math.Min(55,step*.15);
            // Incised walls can be steep inside an otherwise flat plateau.
            // Their70m beds and gullies were absent from the regional hills
            // estimate, leaving large visible triangular slabs in the distance.
            double wall=v.sharp*Math.Min(1,Math.Pow(step/140,1.3));
            return Math.Max(Math.Max(local,crest),wall)+v.bend*4/(segments*segments)+step*step/(8*terrain.RadiusM*key.Scale);
        }
    }

    public static class TerrainAdaptiveLod
    {
        public static int BackdropLevel(double scale,double viewDistance)
        {
            if(!double.IsFinite(scale)||scale<.01||scale>2||!double.IsFinite(viewDistance))throw new ArgumentOutOfRangeException();
            int maximum=Math.Max(0,(int)Math.Floor(Math.Log(128/scale,2)));
            double range=Math.Max(1000,Math.Min(250000,viewDistance));
            int covering=Math.Max(0,(int)Math.Ceiling(Math.Log(range*2/(TerrainLod.BaseSizeM*scale),2)));
            return Math.Min(maximum,covering);
        }
        public static double ProjectedError(double metres,double distance,double verticalDistance,int height,double fov)
        {
            double pixels=Math.Max(480,Math.Min(2160,height))/(2*Math.Tan(Math.Max(30,Math.Min(110,fov))*Math.PI/360));
            return metres*pixels/Math.Max(1,Math.Sqrt(distance*distance+verticalDistance*verticalDistance));
        }
        public static Dictionary<TerrainTileKey,int> Plan(double x,double z,double altitude,double scale,TerrainDetailOptions options,
            Func<TerrainTileKey,int,double> error,int height=1080,double fov=68,IReadOnlyDictionary<TerrainTileKey,int>? previous=null,CancellationToken token=default)
        {
            if(scale<.01||scale>2||double.IsNaN(scale))throw new ArgumentOutOfRangeException(nameof(scale));
            double range=Math.Max(1000,Math.Min(250000,options.ViewDistanceM)),full=options.ProtectedRadius(scale,altitude);
            var result=new Dictionary<TerrainTileKey,int>();
            int root=Math.Max(0,(int)Math.Floor(Math.Log(128/scale,2)));double size=TerrainLod.BaseSizeM*scale*(1<<root);
            var ancestors=new HashSet<TerrainTileKey>();
            if(previous!=null)foreach(var k in previous.Keys)for(int depth=1;depth<=root-k.Level;depth++)ancestors.Add(new TerrainTileKey(k.X>>depth,k.Z>>depth,k.Level+depth,scale));
            void Visit(TerrainTileKey k)
            {
                token.ThrowIfCancellationRequested();double d=Math.Sqrt(TerrainLod.DistanceSquared(k,x,z));if(d>range)return;
                double threshold=options.PixelError*(ancestors.Contains(k)?.85:1.15);
                // A distance floor prevents huge nearby patches, independent of the estimate.
                bool split=k.Level>0&&(d<full||options.ProximityRefinement&&Math.Max(d,altitude*.8)<k.SizeM*1.15);
                int segments=k.Level==0?Math.Max(8,Math.Min(64,options.FinestSegments)):16;
                if(!split&&k.Level>0)
                {
                    if(previous!=null&&previous.TryGetValue(k,out int old))segments=Math.Max(16,Math.Min(64,old));
                    double Pixels(int n)=>ProjectedError(options.EstimateRelief?error(k,n):0,d,altitude,height,fov);
                    while(segments>16&&Pixels(segments/2)<options.PixelError*.75)segments/=2;
                    while(segments<64&&Pixels(segments)>threshold)segments*=2;
                    if(Pixels(segments)>threshold&&result.Count<1800)split=true;
                }
                if(split)for(int j=0;j<2;j++)for(int i=0;i<2;i++)Visit(new TerrainTileKey(k.X*2+i,k.Z*2+j,k.Level-1,scale));
                else result.Add(k,segments);
            }
            for(int b=(int)Math.Floor((z-range)/size);b<=(int)Math.Floor((z+range)/size);b++)
                for(int a=(int)Math.Floor((x-range)/size);a<=(int)Math.Floor((x+range)/size);a++)Visit(new TerrainTileKey(a,b,root,scale));
            return result;
        }
    }
}

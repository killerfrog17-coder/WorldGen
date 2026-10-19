using System;
using System.Runtime.CompilerServices;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Presentation-only chart convention. Never rotates or changes generated geography.</summary>
    public sealed class WorldMeridian
    {
        public readonly double PrimeLongitude,SeamLongitude,LandCrossingDegrees;
        WorldMeridian(double seam,double crossing){SeamLongitude=seam;PrimeLongitude=TerrainMapViewport.Wrap(seam+180);LandCrossingDegrees=crossing;}
        static readonly ConditionalWeakTable<World,WorldMeridian> Cache=new ConditionalWeakTable<World,WorldMeridian>();
        public static WorldMeridian For(World world,CancellationToken token=default)=>Cache.GetValue(world,w=>Select(w,token));
        public static void Apply(PlaySession play,World world)
        {
            if(play.MeridianVersion!=0)return;
            double oldHour=play.SolarHour;play.PrimeMeridianLongitude=For(world).PrimeLongitude;play.MeridianVersion=1;
            // An older saved explorer keeps its local clock when adopting the chart convention.
            if(play.HasPlayer)play.SetLocalSolarHour(oldHour,Math.Atan2(play.PlayerPosition.Y,play.PlayerPosition.X)*180/Math.PI);
        }
        static WorldMeridian Select(World world,CancellationToken token)
        {
            const int columns=720,rows=180,stride=columns+1;
            var surface=world.Surface;int count=surface.Cells.Length;
            if(world.ElevationM.Length!=count)throw new ArgumentException("A completed heightmap is required to choose the chart seam.");
            var difference=new int[rows*stride];bool oceans=world.Hydrology.OceanConnected.Length==count;
            double radiusM=world.Recipe.RadiusKm*1000d;int landCount=0;
            void Interval(int row,int first,int last)
            {int at=row*stride;difference[at+first]++;difference[at+last+1]--;}
            for(int i=0;i<count;i++)
            {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                if(oceans?world.Hydrology.OceanConnected[i]:world.ElevationM[i]<0)continue;
                landCount++;
                var cell=surface.Cells[i];double lat=Math.Asin(Math.Max(-1,Math.Min(1,cell.Position.Z)))*180/Math.PI,lon=Math.Atan2(cell.Position.Y,cell.Position.X)*180/Math.PI;
                // Conservative equal-area cell footprints, rasterized only for choosing the seam.
                double r=Math.Sqrt(cell.AreaM2>0?cell.AreaM2/Math.PI:4*radiusM*radiusM/Math.Max(1,count))/radiusM*180/Math.PI*1.2;
                double span=Math.Abs(lat)+r>=90?180:Math.Asin(Math.Min(1,Math.Sin(r*Math.PI/180)/Math.Cos(lat*Math.PI/180)))*180/Math.PI;
                int firstRow=Math.Max(0,(int)Math.Floor(lat-r+90)),lastRow=Math.Min(rows-1,(int)Math.Floor(lat+r+90));
                int first=(int)Math.Floor((lon-span+180)*2),last=(int)Math.Floor((lon+span+180)*2);
                for(int row=firstRow;row<=lastRow;row++)
                {
                    if(last-first>=columns-1){Interval(row,0,columns-1);continue;}
                    int a=(first%columns+columns)%columns,b=(last%columns+columns)%columns;
                    if(a<=b)Interval(row,a,b);else{Interval(row,a,columns-1);Interval(row,0,b);}
                }
            }
            if(landCount==0||landCount==count)return new WorldMeridian(-180,landCount==0?0:180);
            var cuts=new int[columns];var clearance=new double[columns];var occupied=new bool[columns];var distances=new int[columns*3];
            for(int row=0;row<rows;row++)
            {
                token.ThrowIfCancellationRequested();int active=0,land=0;
                for(int x=0;x<columns;x++){active+=difference[row*stride+x];occupied[x]=active>0;if(occupied[x]){cuts[x]++;land++;}}
                if(land==0)continue;
                // Circular distance transform: two linear sweeps, not land×candidate searches.
                int distance=columns;for(int x=0;x<distances.Length;x++){distance=occupied[x%columns]?0:distance+1;distances[x]=distance;}
                distance=columns;double weight=Math.Cos((row-89.5)*Math.PI/180);
                for(int x=distances.Length-1;x>=0;x--){distance=occupied[x%columns]?0:distance+1;distances[x]=Math.Min(distances[x],distance);if(x>=columns&&x<2*columns)clearance[x-columns]+=distances[x]*weight;}
            }
            // Prefer no land; otherwise shortest land crossing. Ties prefer broad ocean buffers.
            // Starting at the conventional antimeridian gives symmetric/oceanless worlds a stable fallback.
            int best=0;for(int x=1;x<columns;x++)if(cuts[x]<cuts[best]||cuts[x]==cuts[best]&&clearance[x]>clearance[best]+1e-8)best=x;
            if(clearance[best]==0)return new WorldMeridian(-180,cuts[best]);
            return new WorldMeridian(-180+best*.5+.25,cuts[best]);
        }
    }
}

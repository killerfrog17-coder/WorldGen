using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Runtime-only memoization of immutable accepted geography. Direction
        // and sample order are retained; live capability costs are never saved
        // here. Disabling it is an exact literal diagnostic path.
        public bool UseEnvironmentalGeometryCache=true;
        readonly Dictionary<long,EnvironmentalGeometrySample> environmentalGeometryCache=new Dictionary<long,EnvironmentalGeometrySample>();
        readonly Queue<long> environmentalGeometryOrder=new Queue<long>();
        readonly struct EnvironmentalGeometrySample
        {
            public readonly double DistanceKm,FromLatitude,ToLatitude;
            readonly int c0,c1,c2,c3,c4,c5,c6,c7;
            public EnvironmentalGeometrySample(double km,double fromLatitude,double toLatitude,ReadOnlySpan<int> cells)
            {DistanceKm=km;FromLatitude=fromLatitude;ToLatitude=toLatitude;c0=cells[0];c1=cells[1];c2=cells[2];c3=cells[3];c4=cells[4];c5=cells[5];c6=cells[6];c7=cells[7];}
            public int Cell(int index)=>index==0?c0:index==1?c1:index==2?c2:index==3?c3:index==4?c4:index==5?c5:index==6?c6:c7;
        }
        EnvironmentalGeometrySample EnvironmentalGeometry(int from,int to)
        {
            long key=((long)from<<32)|(uint)to;
            bool cache=UseProvincialCaches&&UseEnvironmentalGeometryCache;
            if(cache&&environmentalGeometryCache.TryGetValue(key,out var known))return known;
            var start=World.Surface.Cells[from].Position;var end=World.Surface.Cells[to].Position;
            double km=D.Angle(start,end)*World.Recipe.RadiusKm;
            Span<int> cells=stackalloc int[8];
            for(int k=0;k<8;k++)
            {
                double t=(k+.5)/8;var point=(start*(1-t)+end*t).Unit;
                int x=Math.Max(0,Math.Min(OceanRasterWidth-1,(int)((Math.Atan2(point.Y,point.X)/(2*Math.PI)+.5)*OceanRasterWidth))),y=Math.Max(0,Math.Min(OceanRasterHeight-1,(int)((.5-Math.Asin(point.Z)/Math.PI)*OceanRasterHeight)));
                cells[k]=aggregateTerrainRaster[y*OceanRasterWidth+x];
            }
            var value=new EnvironmentalGeometrySample(km,CropLatitude(from),CropLatitude(to),cells);
            if(cache)
            {
                if(environmentalGeometryCache.Count>=65536)environmentalGeometryCache.Remove(environmentalGeometryOrder.Dequeue());
                environmentalGeometryCache.Add(key,value);environmentalGeometryOrder.Enqueue(key);
            }
            return value;
        }
    }
}

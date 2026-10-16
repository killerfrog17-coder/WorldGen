using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryEnvironmentalGeometryTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string hash=source.ComputeHash();
        var cached=new Prehistory(world);var literal=new Prehistory(world){UseEnvironmentalGeometryCache=false};cached.Validate(source);literal.Validate(source);
        var state=source.Copy();var valid=cached.Routes.Where(r=>r.CellA>=0&&r.CellB>=0&&r.CellA!=r.CellB).ToArray();
        int samples=Math.Min(24,valid.Length);var pairs=Enumerable.Range(0,samples).Select(k=>valid[k*(valid.Length-1)/Math.Max(1,samples-1)]).Select(r=>(r.CellA,r.CellB)).ToArray();
        check(pairs.Length>0&&state.History.Cultures.Length>0,"Actual saved world supplies physical routes and current capability carriers");
        var raster=(int[])typeof(Prehistory).GetField("aggregateTerrainRaster",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(cached)!;
        foreach(var pair in pairs)
        foreach(var direction in new[]{pair,(pair.CellB,pair.CellA)})
        {
            int from=direction.Item1,to=direction.Item2;object geometry=Call(cached,"EnvironmentalGeometry",from,to);var type=geometry.GetType();
            double Value(string name)=>(double)type.GetField(name)!.GetValue(geometry)!;
            var start=world.Surface.Cells[from].Position;var end=world.Surface.Cells[to].Position;
            check(BitConverter.DoubleToInt64Bits(Value("DistanceKm"))==BitConverter.DoubleToInt64Bits(D.Angle(start,end)*world.Recipe.RadiusKm),"Cached directional distance is bit-exact to the original expression");
            check(Value("FromLatitude")==Math.Abs(Math.Asin(start.Z)*180/D.Pi)&&Value("ToLatitude")==Math.Abs(Math.Asin(end.Z)*180/D.Pi),"Each endpoint retains the original crop latitude");
            for(int k=0;k<8;k++)
            {
                double t=(k+.5)/8;var point=(start*(1-t)+end*t).Unit;
                int x=Math.Max(0,Math.Min(255,(int)((Math.Atan2(point.Y,point.X)/(2*Math.PI)+.5)*256))),y=Math.Max(0,Math.Min(127,(int)((.5-Math.Asin(point.Z)/Math.PI)*128)));
                int actual=(int)type.GetMethod("Cell")!.Invoke(geometry,new object[]{k})!;
                check(actual==raster[y*256+x],"All eight immutable raster cells preserve the literal direction and summation order");
            }
            foreach(int culture in Enumerable.Range(0,Math.Min(3,state.History.Cultures.Length)))
            {
                double a=(double)Call(cached,"EnvironmentalTransmission",state,from,to,culture,false),b=(double)Call(literal,"EnvironmentalTransmission",state,from,to,culture,false);
                check(BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b),"Cache warmth never changes current environmental transmission");
                var tech=state.History.Cultures[culture].Technical;
                if(tech!=null)
                {
                    var saved=(int[])tech.Deployment.Clone();for(int i=0;i<tech.Deployment.Length;i++)tech.Deployment[i]=i%2==0?999:1;
                    a=(double)Call(cached,"EnvironmentalTransmission",state,from,to,culture,false);b=(double)Call(literal,"EnvironmentalTransmission",state,from,to,culture,false);
                    check(BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b),"A warm geometry cache still evaluates changed live transport, management and communication");
                    tech.Deployment=saved;
                }
            }
        }
        var aFuture=cached.AdvanceYears(source,20);var bFuture=literal.AdvanceYears(source,20);
        check(aFuture.Hash==bFuture.Hash,"Exact actor dates, people, knowledge, politics and all saved state agree after twenty years with warm versus disabled geometry caching");
        check(source.ComputeHash()==hash,"Geometry diagnostics and continuations never mutate the loaded source checkpoint");
    }
}

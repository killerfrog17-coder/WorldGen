using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // Setup ranges are resolved into an ordinary complete recipe before generation.
    // They are UI intent, not a new generator or a hidden source of randomness.
    public sealed class WorldSetup
    {
        public enum Roll { Linear, Low, High }
        public sealed class Range { public bool Enabled; public double Min,Max; public Roll Distribution; }
        public readonly Dictionary<string,Range> Ranges=new Dictionary<string,Range>();
        public Range Get(FieldInfo field,Recipe recipe)
        {
            if(!Ranges.TryGetValue(field.Name,out var range)){double value=Convert.ToDouble(field.GetValue(recipe));Ranges[field.Name]=range=new Range{Min=value,Max=value};}
            return range;
        }
        public static double Round(double value,bool integer)
        {
            if(value==0 || double.IsNaN(value) || double.IsInfinity(value))return value;
            double step=Math.Pow(10,Math.Floor(Math.Log10(Math.Abs(value)))-(integer?2:1));
            if(integer)step=Math.Max(1,step);
            return Math.Round(value/step,0,MidpointRounding.AwayFromZero)*step;
        }
        public static double RollUnit(double unit,Roll roll)
        {
            unit=Math.Max(0,Math.Min(1,unit));
            return roll==Roll.Linear?unit:roll==Roll.Low?(Math.Pow(100,unit)-1)/99:1-(Math.Pow(100,1-unit)-1)/99;
        }
        public int PlatesMin = 80, PlatesMax = 80, ContinentsMin = 4, ContinentsMax = 4, ConnectionsMin = 1, ConnectionsMax = 1;
        public void From(Recipe r) { Ranges.Clear();PlatesMin=PlatesMax=r.MicroplateCount; ContinentsMin=ContinentsMax=r.ContinentCount; ConnectionsMin=ConnectionsMax=r.Interconnectedness; }
        public Recipe Preset(string name,Recipe previous)
        {
            var r=Recipe.Preset(name);r.Seed=previous.Seed;r.Detail=previous.Detail;WorldPresets.Apply(name,r);From(r);
            foreach(var window in WorldPresets.Windows(name))Ranges[window.Field]=new Range{Enabled=true,Min=window.Min,Max=window.Max,Distribution=(Roll)window.Bias};
            return r;
        }
        static int Sample(int seed, string name, int low, int high, int min, int max)
        {
            if(low<min || high>max || low>high) throw new ArgumentException(name+": enter an ordered range between "+min+" and "+max+".");
            if(low==high)return low;
            double unit=new Rng(seed,"setup-range-"+name).Unit();
            return Math.Max(low,Math.Min(high,(int)Math.Floor(low+(high+1.0-low)*unit)));
        }
        public Recipe Resolve(Recipe source)
        {
            var r=source.Clone(); r.MicroplateCount=Sample(r.Seed,"Plates",PlatesMin,PlatesMax,4,500);
            r.ContinentCount=Sample(r.Seed,"Continents",ContinentsMin,ContinentsMax,1,50);
            r.Interconnectedness=Sample(r.Seed,"Interconnectedness",ConnectionsMin,ConnectionsMax,1,50);
            foreach(var field in Recipe.Settings)
            {
                if(!Ranges.TryGetValue(field.Name,out var range))continue;
                if(!range.Enabled){field.SetValue(r,field.GetValue(source));continue;}
                var bounds=field.GetCustomAttribute<SettingAttribute>();
                if(double.IsNaN(range.Min)||double.IsNaN(range.Max)||range.Min<bounds.Min||range.Max>bounds.Max||range.Min>range.Max)throw new ArgumentException(bounds.Label+": invalid range.");
                bool integer=field.FieldType==typeof(int);
                double unit=RollUnit(new Rng(r.Seed,"setup-range-"+field.Name).Unit(),range.Distribution);
                double value=range.Min+(range.Max-range.Min+(integer?1:0))*unit;
                value=Math.Max(range.Min,Math.Min(range.Max,Round(integer?Math.Floor(value):value,integer)));
                field.SetValue(r,integer?(object)(int)value:value);
            }
            r.Validate(); return r;
        }
        public static double Position(double value,double min,double max) => max<=min?0:min>0 ? Math.Log(Math.Max(min,value)/min)/Math.Log(max/min) : (value-min)/(max-min);
        public static double Value(double position,double min,double max) => max<=min?min:min>0 ? min*Math.Pow(max/min,position) : min+(max-min)*position;
        public static FieldInfo[] Fields(string group) => Recipe.Settings.Where(f=>f.GetCustomAttribute<SettingAttribute>().Group==group && f.Name!=nameof(Recipe.Seed) && f.Name!=nameof(Recipe.Detail) && f.Name!=nameof(Recipe.Irregularity)).ToArray();
    }

    public static class GenerationPreview
    {
        public static World Capture(string stage, World source)
        {
            var w=new World();
            foreach(var f in typeof(World).GetFields())f.SetValue(w,f.GetValue(source));
            w.Stages=source.Stages.ToList(); w.Metrics=source.Metrics.ToList(); w.Warnings=source.Warnings.ToList();
            // The preview needs one surface, not the retained reconstruction base.
            w.CoarseSurface=new Surface();w.CoarsePlate=Array.Empty<int>();w.CoarseContinent=Array.Empty<int>();
            // Hydrology.Build mutates its result object; retain only completed fields.
            w.Hydrology=new HydrologyResult();
            foreach(var f in typeof(HydrologyResult).GetFields())f.SetValue(w.Hydrology,f.GetValue(source.Hydrology));
            if(stage=="coarse-plates")
            {
                w.Surface=source.CoarseSurface; w.CellMicroplate=source.CoarsePlate; w.Continent=source.CoarseContinent;
                w.Microplates=source.Microplates.Select(p=>new Microplate { Id=p.Id, ParentAffinityId=p.ParentAffinityId }).ToArray();
                w.ElevationM=w.Continent.Select(c=>c<0?-1000:100).ToArray();
                w.Hydrology.OceanConnected=w.Continent.Select(c=>c<0).ToArray();
            }
            return w;
        }
        public static bool Available(World w,Layer layer)
        {
            if(layer==Layer.Simulation)return w?.Ecology!=null;
            if(w==null)return false;
            if(layer==Layer.Microplates || layer==Layer.Continents || layer==Layer.Crust)return true;
            bool terrain=w.Stages.Any(s=>s.Name=="erosion"), climate=w.Stages.Any(s=>s.Name=="climate"), hydro=w.Stages.Any(s=>s.Name=="hydrology");
            if(EcologyView.IsLayer(layer))return w.Stages.Any(s=>s.Name=="biogeography");
            if(layer==Layer.Satellite || layer==Layer.Climate || layer>=Layer.TemperatureSummer && layer<=Layer.OceanCurrentsWinter || layer>=Layer.TemperatureSpring && layer<=Layer.PrecipitationAutumn)return climate;
            if(layer>=Layer.WaterBodies && layer<=Layer.ContributingArea)return hydro;
            return terrain;
        }
    }
}

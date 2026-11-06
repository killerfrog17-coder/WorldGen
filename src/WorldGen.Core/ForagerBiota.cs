using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
namespace WorldGen.Core
{
    // Sparse relative local populations. Original ecological provenance and
    // ecoregion boundaries are immutable; this ledger records their later life.
    public static class ForagerBiota
    {
        sealed class RealmSources
        {
            public readonly Dictionary<int,int[]> Taxa;
            public RealmSources(ForagerState f)=>Taxa=f.Districts.GroupBy(d=>d.Realm).ToDictionary(g=>g.Key,g=>g.SelectMany(d=>f.Biota[d.Id].Taxa).Distinct().OrderBy(id=>id).ToArray());
        }
        sealed class LocalRows {public readonly Dictionary<int,ForagerBiotaDistrict> Rows=new Dictionary<int,ForagerBiotaDistrict>();}
        static readonly ConditionalWeakTable<ForagerDistrict[],RealmSources> Sources=new ConditionalWeakTable<ForagerDistrict[],RealmSources>();
        static readonly ConditionalWeakTable<ForagerState,LocalRows> Derived=new ConditionalWeakTable<ForagerState,LocalRows>();
        // Keep the captured state factory off the common cache-hit path: C#
        // otherwise creates its closure before CurrentRegion's early return.
        static RealmSources RegionSources(ForagerState f)=>Sources.GetValue(f.Districts,_=>new RealmSources(f));
        // Species rosters are local projections, not globally advanced actors.
        public static ForagerBiotaDistrict CurrentRegion(World world,ForagerState f,int region)
        {
            var cache=Derived.GetValue(f,_=>new LocalRows());lock(cache.Rows)
            {
                if(cache.Rows.TryGetValue(region,out var found))return found;
                var d=f.Districts[region];var e=ForagerEcology.At(world.Recipe.Seed,d,f.Seconds/(double)SimulationTime.Year,world.ElevationM[d.Cell]);
                var source=RegionSources(f).Taxa[d.Realm];var ids=new List<int>();var abundance=new List<double>();
                foreach(int id in source)
                {
                    var taxon=world.Ecology.Taxa[id];double value=Suitability(taxon,d,e,world.ElevationM[d.Cell]);int guild=Guild(taxon);
                    if(guild>=0)value*=f.Resources[region].Stock[guild];if(value<.00001)continue;ids.Add(id);abundance.Add(D.Q(value,1000000));
                }
                found=new ForagerBiotaDistrict{District=region,ReviewedYear=(int)(f.Seconds/SimulationTime.Year),Taxa=ids.ToArray(),Abundance=abundance.ToArray(),BaselineFood=f.Biota[region].BaselineFood,ForestCover=f.Biota[region].ForestCover};Availability(world,found);cache.Rows[region]=found;return found;
            }
        }
        public static double Suitability(Taxon t,ForagerDistrict d,ForagerEnvironment e,int elevation)
        {
            double Ramp(double value,double low,double high,double width)=>D.Clamp((value-low+width)/width)*D.Clamp((high+width-value)/width);
            double thermal=Ramp(e.WinterC,t.MinColdTenthsC/10d,t.MaxColdTenthsC/10d,3)*Ramp(e.SummerC,t.MinWarmTenthsC/10d,t.MaxWarmTenthsC/10d,3);
            double moisture=Ramp(e.Aridity,t.MinAridityMilli/1000d,t.MaxAridityMilli/1000d,.15);
            double habitat=0;
            if((t.Habitats&Habitat.Forest)!=0)habitat=Math.Max(habitat,D.Clamp(e.Canopy*1.6));
            if((t.Habitats&Habitat.Open)!=0)habitat=Math.Max(habitat,D.Clamp(1-e.Canopy));
            if((t.Habitats&Habitat.Tundra)!=0)habitat=Math.Max(habitat,D.Clamp((8-e.MeanC)/10)*D.Clamp((e.SummerC+3)/12));
            if((t.Habitats&Habitat.Desert)!=0)habitat=Math.Max(habitat,D.Clamp((.5-e.Aridity)/.4));
            if((t.Habitats&Habitat.Wetland)!=0)habitat=Math.Max(habitat,D.Clamp(d.River*1.6));
            if((t.Habitats&Habitat.Freshwater)!=0)habitat=Math.Max(habitat,D.Clamp(d.River*2));
            if((t.Habitats&Habitat.Coastal)!=0)habitat=Math.Max(habitat,d.Coast);
            if((t.Habitats&Habitat.Alpine)!=0)habitat=Math.Max(habitat,D.Clamp(d.Relief/1200));
            var lineage=t.Lineage;
            if(lineage!=null)
            {
                habitat*=Ramp(e.Canopy,lineage.MinCanopyPermille/1000d,lineage.MaxCanopyPermille/1000d,.15);
                if(lineage.RequiresCoast)habitat*=d.Coast;
                if(lineage.RequiresFreshwater)habitat*=D.Clamp(d.River*2);
            }
            double ice=Math.Pow(1-e.IceCover,t.Producer?3:2),height=D.Clamp((t.MaxElevationM+400-elevation)/400d);
            return D.Clamp(thermal*moisture*habitat*ice*height);
        }
        static int Guild(Taxon t)
        {
            if(t.Producer||t.BasalResource)return 0;
            if((t.Occupies&Stratum.Water)!=0)return t.Lineage?.RequiresCoast==true||(t.Habitats&Habitat.Freshwater)==0?4:3;
            if(t.MassKg>=10&&(t.Eats&(Food.Grass|Food.Browse|Food.Fruit|Food.Seed))!=0)return 2;
            return t.MassKg>=.05&&t.MassKg<20?1:-1;
        }
        static double Weight(Taxon t,int guild)=>guild==2?Math.Sqrt(Math.Min(2000,t.MassKg)):guild==0?1:Math.Sqrt(Math.Max(.01,Math.Min(20,t.MassKg)));
        public static ForagerBiotaDistrict[] Seed(World world,ForagerState f,CancellationToken token=default)
        {
            var result=new ForagerBiotaDistrict[f.Districts.Length];
            foreach(var d in f.Districts)
            {
                token.ThrowIfCancellationRequested();var e=ForagerEcology.At(world.Recipe.Seed,d,0,world.ElevationM[d.Cell]);var taxa=new SortedSet<int>();
                foreach(int cell in d.Cells)
                {
                    int region=world.Ecology.Region[cell];if(region<0)continue;
                    foreach(var presence in world.Ecology.Regions[region].Species)if(Biogeography.Present(world.Ecology,cell,presence.TaxonId))taxa.Add(presence.TaxonId);
                }
                var row=new ForagerBiotaDistrict{District=d.Id,Taxa=taxa.ToArray(),Abundance=taxa.Select(id=>Suitability(world.Ecology.Taxa[id],d,e,world.ElevationM[d.Cell])).ToArray(),ForestCover=e.Canopy};
                foreach(int id in row.Taxa){var t=world.Ecology.Taxa[id];int guild=Guild(t);if(guild>=0)row.BaselineFood[guild]+=Weight(t,guild);}
                Availability(world,row);result[d.Id]=row;
            }
            return result;
        }
        static void Availability(World world,ForagerBiotaDistrict row)
        {
            var food=new double[5];for(int k=0;k<row.Taxa.Length;k++){var t=world.Ecology.Taxa[row.Taxa[k]];int guild=Guild(t);if(guild>=0)food[guild]+=row.Abundance[k]*Weight(t,guild);}
            for(int k=0;k<5;k++)row.FoodAvailability[k]=D.Q(row.BaselineFood[k]>0?D.Clamp(food[k]/row.BaselineFood[k],0,1.6):k==0?1:D.Clamp(food[k],0,1.6),1000000);
        }
        public static ForagerEnvironment Apply(ForagerEnvironment e,ForagerBiotaDistrict row)
        {
            if(row==null)return e;var food=new double[5];for(int k=0;k<5;k++)food[k]=e.Food[k]*row.FoodAvailability[k];
            return new ForagerEnvironment(e.Years,e.WinterC,e.SummerC,e.MeanC,e.IceCover,e.Moisture,e.Productivity,row.ForestCover,e.Water,e.Pathogens,e.Drought,e.Disturbance,food,e.LeanSeason,e.Aridity,e.RainMm);
        }
        static bool PassageOpen(int seed,Taxon t,ForagerPassage p,ForagerDistrict d,ForagerEnvironment a,ForagerEnvironment b)
        {
            if(a.IceCover>.95||b.IceCover>.95)return false;
            if(p.Sea)
            {
                bool shelf=p.Shelf&&p.SeaFallM>0&&p.SeaFallM<=PaleolithicRules.Climate(seed,a.Years,d.Center.Z).SeaFallM;
                int water=p.WaterDistanceM>0?p.WaterDistanceM:p.DistanceM;
                if(!shelf&&water>t.OceanCrossingKm*1000)return false;
            }
            if(t.Lineage?.RequiresFreshwater==true&&(d.River<.1||b.Water<.1||p.Sea))return false;
            return true;
        }
        public static void Advance(World world,ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token=default)
        {
            var previous=f.Biota;var next=new ForagerBiotaDistrict[previous.Length];
            foreach(var d in f.Districts)
            {
                if((d.Id&31)==0)token.ThrowIfCancellationRequested();var old=previous[d.Id];var e=ForagerEcology.At(world.Recipe.Seed,d,year,world.ElevationM[d.Cell]);var candidates=new SortedSet<int>(old.Taxa);
                foreach(var pass in d.Passages)foreach(int id in previous[pass.To].Taxa)if(previous[pass.To].Presence(id)>.03)candidates.Add(id);
                var ids=new List<int>();var values=new List<double>();
                foreach(int id in candidates)
                {
                    var t=world.Ecology.Taxa[id];double current=old.Presence(id),target=Suitability(t,d,e,world.ElevationM[d.Cell]);
                    int guild=Guild(t);if(guild>=1&&guild<=4)target*=Math.Pow(f.Resources[d.Id].Stock[guild],t.MassKg>400?1.5:.6);
                    if(current<.00001&&target>.02)
                    {
                        double arrival=0;
                        foreach(var pass in d.Passages)
                        {
                            double source=previous[pass.To].Presence(id);if(source<.03||!PassageOpen(world.Recipe.Seed,t,pass,d,e,environment[pass.To]))continue;
                            double speed=t.Producer?.18:(t.Occupies&Stratum.Air)!=0?12:t.MassKg<.05?.25:3;
                            double crossing=Math.Max(1,pass.DistanceM/1000d);arrival+=source*(1-Math.Exp(-elapsed*speed/crossing));
                        }
                        double chance=D.Clamp(arrival*.16);
                        if(Rng.Value(world.Recipe.Seed,"historical-biota-arrival-"+id,unchecked(d.Id*7919+year))<chance)current=.02;
                    }
                    if(current<=0)continue;
                    double rate=target<current?1/25d:t.Producer?1/280d:t.MassKg>400?1/100d:1/30d;
                    double value=PaleolithicRules.Approach(current,target,rate,elapsed);
                    if(value<.00001)continue;ids.Add(id);values.Add(D.Q(value,1000000));
                }
                var row=new ForagerBiotaDistrict{District=d.Id,ReviewedYear=year,Taxa=ids.ToArray(),Abundance=values.ToArray(),BaselineFood=(double[])old.BaselineFood.Clone()};
                double forestPotential=e.Canopy;bool forestSeed=ids.Where((id,k)=>values[k]>.01).Any(id=>world.Ecology.Taxa[id].Producer&&(world.Ecology.Taxa[id].Habitats&Habitat.Forest)!=0);
                row.ForestCover=D.Q(PaleolithicRules.Approach(old.ForestCover,forestSeed?forestPotential:forestPotential*.12,forestPotential<old.ForestCover?1/70d:1/400d,elapsed),1000000);
                Availability(world,row);next[d.Id]=row;
            }
            f.Biota=next;
        }
        public static void AdvanceRegion(World world,ForagerState f,Func<int,ForagerEnvironment> environment,int region,int year,int elapsed,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var old=f.Biota[region];var d=f.Districts[region];var e=ForagerEcology.At(world.Recipe.Seed,d,year,world.ElevationM[d.Cell]);
            var row=new ForagerBiotaDistrict{District=region,ReviewedYear=year,Taxa=old.Taxa,Abundance=old.Abundance,BaselineFood=old.BaselineFood,FoodAvailability=new double[]{1,1,1,1,1},ForestCover=D.Q(PaleolithicRules.Approach(old.ForestCover,e.Canopy,e.Canopy<old.ForestCover?1/70d:1/400d,elapsed),1000000)};
            f.Biota[region]=row;
        }
        static void LegacyDetailedRegion(World world,ForagerState f,Func<int,ForagerEnvironment> environment,int region,int year,int elapsed,CancellationToken token)
        {
            var previous=f.Biota;var d=f.Districts[region];
                token.ThrowIfCancellationRequested();var old=previous[d.Id];var e=ForagerEcology.At(world.Recipe.Seed,d,year,world.ElevationM[d.Cell]);var candidates=new HashSet<int>(old.Taxa);
                var adjacent=d.Passages.GroupBy(p=>p.To).ToArray();
                foreach(var group in adjacent){var neighbour=previous[group.Key];for(int k=0;k<neighbour.Taxa.Length;k++)if(neighbour.Abundance[k]>.03)candidates.Add(neighbour.Taxa[k]);}
                var neighbourWeather=new ForagerEnvironment[adjacent.Length];var readyWeather=new bool[adjacent.Length];
                var ids=new List<int>();var values=new List<double>();
                foreach(int id in candidates.OrderBy(x=>x))
                {
                    var t=world.Ecology.Taxa[id];double current=old.Presence(id),target=Suitability(t,d,e,world.ElevationM[d.Cell]);
                    int guild=Guild(t);if(guild>=1&&guild<=4)target*=Math.Pow(f.Resources[d.Id].Stock[guild],t.MassKg>400?1.5:.6);
                    if(current<.00001&&target>.02)
                    {
                        double arrival=0;
                        for(int n=0;n<adjacent.Length;n++)
                        {
                            var group=adjacent[n];double source=previous[group.Key].Presence(id);if(source<.03)continue;
                            if(!readyWeather[n]){neighbourWeather[n]=environment(group.Key);readyWeather[n]=true;}
                            ForagerPassage best=null;
                            foreach(var pass in group)if(PassageOpen(world.Recipe.Seed,t,pass,d,e,neighbourWeather[n])&&(best==null||pass.DistanceM<best.DistanceM))best=pass;
                            if(best==null)continue;
                            double speed=t.Producer?.18:(t.Occupies&Stratum.Air)!=0?12:t.MassKg<.05?.25:3;
                            arrival+=source*(1-Math.Exp(-elapsed*speed/Math.Max(1,best.DistanceM/1000d)));
                        }
                        double chance=D.Clamp(arrival*.16);
                        if(Rng.Value(world.Recipe.Seed,"historical-biota-arrival-"+id,unchecked(d.Id*7919+year))<chance)current=.02;
                    }
                    if(current<=0)continue;
                    double rate=target<current?1/25d:t.Producer?1/280d:t.MassKg>400?1/100d:1/30d;
                    double value=PaleolithicRules.Approach(current,target,rate,elapsed);
                    if(value<.00001)continue;ids.Add(id);values.Add(D.Q(value,1000000));
                }
                var row=new ForagerBiotaDistrict{District=d.Id,ReviewedYear=year,Taxa=ids.ToArray(),Abundance=values.ToArray(),BaselineFood=(double[])old.BaselineFood.Clone()};
                double forestPotential=e.Canopy;bool forestSeed=ids.Where((id,k)=>values[k]>.01).Any(id=>world.Ecology.Taxa[id].Producer&&(world.Ecology.Taxa[id].Habitats&Habitat.Forest)!=0);
                row.ForestCover=D.Q(PaleolithicRules.Approach(old.ForestCover,forestSeed?forestPotential:forestPotential*.12,forestPotential<old.ForestCover?1/70d:1/400d,elapsed),1000000);
                Availability(world,row);f.Biota[d.Id]=row;
        }
        public static double Presence(World world,int cell,int taxon)
        {
            var f=world.Prehistory?.Foragers;if(f==null||f.Biota.Length==0)return Biogeography.Present(world.Ecology,cell,taxon)?1:0;
            int district=f.CellDistrict[cell];return district<0?0:CurrentRegion(world,f,district).Presence(taxon);
        }
    }
}

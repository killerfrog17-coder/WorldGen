using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Potential natural habitats. All subsurface/flood/fire fields are explicitly coarse proxies.</summary>
    public static class Ecology
    {
        static readonly double[] QuarterDeclination={ .16,.36,-.16,-.36 };
        public static void BuildHabitats(World w, CancellationToken token)
        {
            int n=w.Surface.Cells.Length; var e=new EcologyResult { Habitat=new HabitatCell[n], Region=Enumerable.Repeat(-1,n).ToArray(), Province=Enumerable.Repeat(-1,n).ToArray() }; w.Ecology=e;
            var seasons=new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter };
            e.WaterSalinityPpt=new double[w.Hydrology.Waters.Length];
            foreach(var body in w.Hydrology.Waters)
            {
                // Throughflow exports salts. Closed waters accumulate a weathering load over
                // a synthetic residence history; these are not measured chemistry or lake ages.
                double outflow=Math.Max(0,body.MeanInflowM3PerS-body.EvaporationM3PerS);
                double years=body.Endorheic ? 100+2900*Rng.Value(w.Recipe.Seed,"salinity-history",body.Id) : Math.Max(.02,body.ResidenceTimeYears);
                double load=.08*body.MeanInflowM3PerS;
                double storageLoss=body.VolumeM3/(Math.Max(.02,years)*31557600);
                double salt=body.Kind==WaterKind.Ocean ? 35 : D.Clamp(load/Math.Max(.000001,outflow+storageLoss),.02,300);
                e.WaterSalinityPpt[body.Id]=D.Q(salt,1000);
            }
            for(int i=0;i<n;i++)
            {
                if((i&1023)==0) token.ThrowIfCancellationRequested();
                var c=w.Surface.Cells[i]; var a=new HabitatCell(); e.Habitat[i]=a;
                int low=int.MaxValue,high=int.MinValue; foreach(int j in c.Neighbors) { low=Math.Min(low,w.ElevationM[j]); high=Math.Max(high,w.ElevationM[j]); }
                a.ReliefM=Math.Max(0,high-low);
                int p=0,sumT=0,cold=10000,warm=-10000,deficit=0,pet=0,growing=0,dry=0;
                for(int q=0;q<4;q++)
                {
                    int t=seasons[q].TemperatureTenthsC[i], rain=seasons[q].PrecipitationMm[i];
                    int demand=PotentialEvaporationMm(c.Position.Z,t/10.0,q);
                    p+=rain; sumT+=t; cold=Math.Min(cold,t); warm=Math.Max(warm,t); pet+=demand;
                    deficit+=Math.Max(0,demand-rain); if(t>50 && rain>Math.Max(10,demand*.2)) growing+=91;
                    if(demand>rain*1.5 && demand>30) dry++;
                }
                a.PetMm=pet; a.AridityMilli=D.M(D.Clamp(p/(double)Math.Max(1,pet),0,20)*1000); a.WaterDeficitMm=deficit;
                a.ColdTenthsC=cold; a.WarmTenthsC=warm; a.MeanTenthsC=D.M(sumT/4.0); a.GrowingDays=growing;
                int summer=c.Position.Z>=0 ? 1 : 3, winter=(summer+2)%4;
                int summerPet=PotentialEvaporationMm(c.Position.Z,seasons[summer].TemperatureTenthsC[i]/10.0,summer);
                a.WarmSeasonDryPermille=seasons[winter].PrecipitationMm[i]>seasons[summer].PrecipitationMm[i]*2 ? D.M(1000*D.Clamp(1-seasons[summer].PrecipitationMm[i]/(double)Math.Max(1,summerPet))) : 0;
                a.PermafrostPermille=D.M(1000*D.Clamp((-a.MeanTenthsC/10.0+1)/7));
                bool ocean=w.Hydrology.OceanConnected[i]; int wb=w.Hydrology.WaterBody[i]; double lake=w.Hydrology.LakeCoverageFraction[i];
                if(wb>=0) a.SalinityMilliPpt=D.M(e.WaterSalinityPpt[wb]*1000);
                double mean=(w.Hydrology.NorthernSpringDischargeM3PerS[i]+w.Hydrology.NorthernSummerDischargeM3PerS[i]+w.Hydrology.NorthernAutumnDischargeM3PerS[i]+w.Hydrology.NorthernWinterDischargeM3PerS[i])/4;
                double peak=Math.Max(Math.Max(w.Hydrology.NorthernSpringDischargeM3PerS[i],w.Hydrology.NorthernSummerDischargeM3PerS[i]),Math.Max(w.Hydrology.NorthernAutumnDischargeM3PerS[i],w.Hydrology.NorthernWinterDischargeM3PerS[i]));
                double flat=1/(1+a.ReliefM/80.0), span=D.Sqrt(c.AreaM2);
                double flood=peak<5 ? 0 : D.Clamp(4.8*D.Sqrt(peak)*24*span/c.AreaM2*flat,0,.85);
                a.FloodFractionPermille=D.M(flood*1000);
                if(flood>.001) { a.Flooding=peak>mean*1.3 ? FloodRegime.RiverSeasonal : FloodRegime.RiverPersistent; a.FloodDaysPerYear=D.M(D.Clamp(45+90*(mean/Math.Max(1,peak)),15,180)); a.FloodPulsesPerYear=1; }
                if(lake>.001) { a.FloodFractionPermille=Math.Max(a.FloodFractionPermille,D.M(Math.Min(1,lake*1.3)*1000)); a.Flooding=FloodRegime.LakeMargin; a.FloodDaysPerYear=Math.Max(a.FloodDaysPerYear,180); }
                bool coast=c.Neighbors.Any(j=>w.Hydrology.OceanConnected[j]);
                if(!ocean && coast && w.ElevationM[i]<25 && a.ReliefM<120)
                {
                    a.Flooding=FloodRegime.Tidal; a.FloodFractionPermille=Math.Max(a.FloodFractionPermille,D.M(600*D.Clamp((25-w.ElevationM[i])/25.0)));
                    a.FloodDaysPerYear=365; a.FloodPulsesPerYear=730; // Generic semidiurnal potential, not a tidal solution.
                    a.SalinityMilliPpt=D.M(35000/(1+mean/1000));
                }
                double recharge=D.Clamp((p-deficit)/(double)Math.Max(200,pet));
                int receiver=w.Hydrology.Receiver[i]; double above=receiver<0 ? 0 : Math.Max(0,w.ElevationM[i]-w.ElevationM[receiver]);
                a.WaterTableCm=D.M(100*D.Clamp(1+12*(1-recharge)+Math.Min(25,above*.02)-8*a.FloodFractionPermille/1000.0,.05,60));
                if(lake>.1 || a.Flooding==FloodRegime.Tidal) a.WaterTableCm=Math.Min(a.WaterTableCm,25);
                a.Soil=a.SalinityMilliPpt>4000 && (lake>.1 || a.Flooding==FloodRegime.Tidal) ? SoilKind.Saline : a.PermafrostPermille>650 ? SoilKind.Frozen
                    : a.ReliefM>1200 ? SoilKind.Rock : a.FloodFractionPermille>80 ? SoilKind.Alluvial
                    : a.WaterTableCm<80 && a.MeanTenthsC<150 ? SoilKind.Organic : a.AridityMilli<200 ? SoilKind.Sand
                    : a.MeanTenthsC>200 && a.AridityMilli>1400 ? SoilKind.Leached : SoilKind.Loam;
                a.SoilFertilityPermille=a.Soil==SoilKind.Alluvial ? 850 : a.Soil==SoilKind.Loam ? 650 : a.Soil==SoilKind.Organic ? 450 : a.Soil==SoilKind.Leached ? 300 : 150;
                a.ProductivityGCM2Year=D.M(Math.Min(1500,p*.65)*D.Clamp((a.MeanTenthsC+100)/300.0)*D.Clamp(growing/250.0));
                a.FirePotentialPermille=D.M(1000*D.Clamp(a.ProductivityGCM2Year/500.0)*D.Clamp(dry/2.0)*(1-a.FloodFractionPermille/1000.0));
                a.Biome=Classify(a,ocean,lake,w.ElevationM[i]); a.Habitat=HabitatOf(a.Biome);
            }
        }
        // Oudin-style radiation/temperature PET. Quarterly averages cannot resolve
        // daily extremes; this is an ecological demand estimate, not added rainfall.
        public static int PotentialEvaporationMm(double sinLatitude,double temperatureC,int quarter)
        {
            double declination=QuarterDeclination[quarter];
            double sinD=D.Sin(declination),cosD=D.Sin(D.Pi/2-declination),cosLat=D.Sqrt(1-sinLatitude*sinLatitude);
            double x=-sinLatitude*sinD/Math.Max(.000001,cosLat*cosD);
            double hour=x<=-1 ? D.Pi : x>=1 ? 0 : D.Pi/2-D.Atan(x/D.Sqrt(1-x*x));
            double radiation=Math.Max(0,24*60/D.Pi*.082*(hour*sinLatitude*sinD+cosLat*cosD*D.Sin(hour)));
            return D.M(radiation/2.45*Math.Max(0,temperatureC+5)/100*91.3125);
        }
        public static Biome Classify(HabitatCell a,bool ocean,double lake,int elevation)
        {
            if(ocean) return Biome.Ocean;
            if(lake>=.9) return a.SalinityMilliPpt>3000 ? Biome.SalineLake : Biome.Freshwater;
            if(a.WarmTenthsC<0) return Biome.Ice;
            if(a.WarmTenthsC<100) return elevation>1800 ? Biome.Alpine : Biome.Tundra;
            if(a.Flooding==FloodRegime.Tidal && a.FloodFractionPermille>150) return a.ColdTenthsC>160 ? Biome.Mangrove : Biome.SaltMarsh;
            if(a.FloodFractionPermille>180) return a.FloodDaysPerYear>160 ? Biome.Marsh : Biome.Floodplain;
            if(a.Soil==SoilKind.Organic && a.WaterTableCm<80) return Biome.Peatland;
            if(a.AridityMilli<200) return Biome.Desert;
            if(a.AridityMilli<500) return a.MeanTenthsC>200 ? Biome.Savanna : Biome.Grassland;
            if(a.ColdTenthsC>160)
                return a.AridityMilli>1100 && a.WaterDeficitMm<250 ? Biome.TropicalRainforest : a.FirePotentialPermille>650 ? Biome.Savanna : Biome.TropicalSeasonalForest;
            if(a.WarmTenthsC<160 || a.ColdTenthsC<-150) return a.AridityMilli>500 ? Biome.BorealForest : Biome.Grassland;
            if(a.FirePotentialPermille>600 && a.ColdTenthsC>0 && a.WarmSeasonDryPermille>500) return Biome.MediterraneanWoodland;
            if(a.AridityMilli<650) return Biome.Grassland;
            return a.AridityMilli>1700 ? Biome.TemperateRainforest : Biome.TemperateForest;
        }
        public static Habitat HabitatOf(Biome b)
        {
            switch(b)
            {
                case Biome.Ocean: case Biome.Ice: case Biome.SalineLake: return Habitat.None;
                case Biome.Freshwater:return Habitat.Freshwater;
                case Biome.Tundra:return Habitat.Tundra;
                case Biome.Alpine:return Habitat.Alpine|Habitat.Open;
                case Biome.Desert:return Habitat.Desert;
                case Biome.Grassland:return Habitat.Open;
                case Biome.Savanna: case Biome.MediterraneanWoodland:return Habitat.Open|Habitat.Forest;
                case Biome.Marsh: case Biome.Peatland:return Habitat.Wetland|Habitat.Open;
                case Biome.Floodplain:return Habitat.Wetland|Habitat.Forest|Habitat.Open;
                case Biome.Mangrove:return Habitat.Coastal|Habitat.Wetland|Habitat.Forest;
                case Biome.SaltMarsh:return Habitat.Coastal|Habitat.Wetland;
                default:return Habitat.Forest;
            }
        }
        public static void BuildRegions(World w,CancellationToken token)
        {
            var e=w.Ecology; var s=w.Surface; int n=s.Cells.Length; var distance=Enumerable.Repeat(long.MaxValue,n).ToArray(); var heap=new Heap(); int province=0;
            // One source in every disconnected non-ocean component plus area-weighted
            // sources. Costs favor climate/terrain barriers, not plate polygons.
            var components=Graph.Components(s,i=>!w.Hydrology.OceanConnected[i],token); var seeded=new HashSet<int>();
            for(int i=0;i<n;i++)
            {
                if((i&1023)==0) token.ThrowIfCancellationRequested();
                if(components[i]<0) continue;
                bool first=seeded.Add(components[i]);
                if(first || Rng.Value(w.Recipe.Seed,"ecoregion-provinces",i)<s.Cells[i].AreaM2/8e11)
                { e.Province[i]=province++; distance[i]=0; heap.Push(0,i); }
            }
            while(heap.Count>0)
            {
                token.ThrowIfCancellationRequested(); var x=heap.Pop(); int i=x.Id; if(x.Key!=distance[i]) continue;
                var c=s.Cells[i];
                for(int k=0;k<c.Neighbors.Length;k++)
                {
                    int j=c.Neighbors[k]; if(components[j]<0) continue;
                    long cost=c.DistanceM[k]+Math.Abs(w.ElevationM[j]-w.ElevationM[i])*180L+Math.Abs(e.Habitat[j].AridityMilli-e.Habitat[i].AridityMilli)*80L;
                    if(e.Habitat[i].Biome!=e.Habitat[j].Biome) cost+=100000;
                    long d=x.Key+cost;
                    if(d<distance[j] || (d==distance[j] && e.Province[i]<e.Province[j]))
                    { distance[j]=d; e.Province[j]=e.Province[i]; heap.Push(d,j); }
                }
            }
            // Regions may have multiple patches of the same habitat in one coherent
            // province. They cannot jump an ocean or inherit an unrelated continent.
            var groups=new SortedDictionary<long,List<int>>();
            for(int i=0;i<n;i++) if(e.Province[i]>=0)
            {
                long key=e.Province[i]*100L+(int)e.Habitat[i].Biome;
                if(!groups.TryGetValue(key,out var list)) groups.Add(key,list=new List<int>()); list.Add(i);
            }
            var regions=new List<Ecoregion>();
            foreach(var pair in groups)
            {
                token.ThrowIfCancellationRequested(); var cells=pair.Value.ToArray(); int id=regions.Count;
                foreach(int i in cells) e.Region[i]=id;
                regions.Add(new Ecoregion { Id=id, ProvinceId=(int)(pair.Key/100), Biome=(Biome)(pair.Key%100), Cells=cells, RepresentativeCell=cells[cells.Length/2], AreaM2=cells.Sum(i=>s.Cells[i].AreaM2), Name="Province "+(pair.Key/100+1)+" "+(Biome)(pair.Key%100) });
            }
            e.Regions=regions.ToArray(); var seen=new bool[n]; var queue=new Queue<int>();
            foreach(var region in e.Regions)
            {
                var neighbors=new SortedSet<int>();
                foreach(int start in region.Cells)
                {
                    if(!seen[start])
                    {
                        region.PatchCount++; seen[start]=true; queue.Enqueue(start);
                        while(queue.Count>0)
                        { int i=queue.Dequeue(); foreach(int j in s.Cells[i].Neighbors) if(e.Region[j]==region.Id && !seen[j]) { seen[j]=true; queue.Enqueue(j); } }
                    }
                    foreach(int j in s.Cells[start].Neighbors) if(e.Region[j]>=0 && e.Region[j]!=region.Id) neighbors.Add(e.Region[j]);
                }
                region.Neighbors=neighbors.ToArray(); token.ThrowIfCancellationRequested();
            }
            w.Metric("ecoregion_count",regions.Count); w.Metric("ecological_province_count",province);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    // Separate population ledger: a sapiens culture never owns or erases another
    // human species' range. These are procedural archaic lineages, not Earth maps.
    [Serializable] public sealed class HumanLineageRange
    {
        public string Species="";public int Origin;
        public double[] Residents=Array.Empty<double>(),Capacity=Array.Empty<double>(),GrowthRate=Array.Empty<double>();
        public HumanLineageRange Copy(){var c=(HumanLineageRange)MemberwiseClone();c.Residents=(double[])Residents.Clone();c.Capacity=(double[])Capacity.Clone();c.GrowthRate=(double[])GrowthRate.Clone();return c;}
    }
    [Serializable] public sealed class HumanRangeState
    {
        public int Version=1;public int[] CellPatch=Array.Empty<int>();
        public HumanLineageRange[] Lineages=Array.Empty<HumanLineageRange>();
        public HumanRangeState Copy()=>new HumanRangeState{Version=Version,CellPatch=(int[])CellPatch.Clone(),Lineages=Lineages.Select(l=>l.Copy()).ToArray()};
        public static readonly string[] Species={"homo-sapiens","archaic-cold","archaic-warm"};
        public static string Name(string id)=>id==Species[0]?"Homo sapiens":id==Species[1]?"Cold-adapted archaic humans":id==Species[2]?"Warm-adapted archaic humans":id;
        public static double ResidentsAt(PrehistoryState s,HumanLineageRange l,int patch)
        {
            if(patch<0||patch>=l.Residents.Length)return 0;double n=l.Residents[patch],k=l.Capacity[patch];
            double years=(s.Paleolithic.Seconds-s.Paleolithic.BoundarySeconds)/(double)SimulationTime.Year;
            if(n<=0)return 0;if(k<=0)return n*Math.Exp(-.02*years);
            return Math.Max(0,k/(1+(k/Math.Max(.000001,n)-1)*Math.Exp(-l.GrowthRate[patch]*years)));
        }
        public static int Mask(PrehistoryState s,int cell)
        {
            if(cell<0||s?.Society==null||cell>=s.Society.CellCulture.Length)return 0;
            if(s.Foragers!=null){int district=s.Foragers.CellDistrict[cell],result=0;if(district>=0)foreach(var c in s.Foragers.Cohorts)if(c.District==district&&c.Population>=1)result|=1<<c.Lineage;return result;}
            int mask=s.Society.CellCulture[cell]>=0?1:0;var ranges=s.HumanRanges;
            if(ranges==null||cell>=ranges.CellPatch.Length)return mask;int patch=ranges.CellPatch[cell];
            foreach(var l in ranges.Lineages){int id=Array.IndexOf(Species,l.Species);if(id>0&&ResidentsAt(s,l,patch)>=1)mask|=1<<id;}return mask;
        }
        public static string MaskName(int mask)=>string.Join(" + ",Species.Where((id,i)=>(mask&(1<<i))!=0).Select(Name));
        public static string MaskName(PrehistoryState s,int mask)=>s?.Foragers==null?MaskName(mask):string.Join(" + ",s.Foragers.Lineages.Where(l=>(mask&(1<<l.Id))!=0).Select(l=>l.Name));
    }
    public sealed partial class Prehistory
    {
        HumanRangeState PrepareHumanRanges(PrehistoryState s)
        {
            var result=new HumanRangeState{CellPatch=(int[])CellPatch.Clone()};var lines=new List<HumanLineageRange>();
            double Distance(int a,int b)=>Math.Acos(D.Clamp(V.Dot(World.Surface.Cells[Patches[a].Cell].Position,World.Surface.Cells[Patches[b].Cell].Position),-1,1))*World.Recipe.RadiusKm*1000;
            for(int species=1;species<HumanRangeState.Species.Length;species++)
            {
                var l=new HumanLineageRange{Species=HumanRangeState.Species[species],Residents=new double[Patches.Length],Capacity=new double[Patches.Length],GrowthRate=new double[Patches.Length]};
                int origin=-1;double best=0;
                for(int p=0;p<Patches.Length;p++)
                {
                    if(p==s.Origin||Patches[p].Island||Distance(p,s.Origin)<1200000||lines.Any(x=>Distance(p,x.Origin)<1500000))continue;
                    double score=HumanRangeCapacity(s,l,p)*(.4+.6*Rng.Value(World.Recipe.Seed,"archaic-origin-"+species,p));
                    // Prefer a distinct geographical centre, not just the richest patch.
                    score=Math.Sqrt(score)*Math.Min(3,Distance(p,s.Origin)/2000000);if(score>best){best=score;origin=p;}
                }
                if(origin<0)continue;l.Origin=origin;
                var costs=Enumerable.Repeat(double.PositiveInfinity,Patches.Length).ToArray();costs[origin]=0;
                var queue=new SortedSet<(double cost,int patch)>{(0,origin)};
                var links=new List<Route>[Patches.Length];foreach(var route in Routes){(links[route.A]??(links[route.A]=new List<Route>())).Add(route);(links[route.B]??(links[route.B]=new List<Route>())).Add(route);}
                while(queue.Count>0)
                {
                    var at=queue.Min;queue.Remove(at);if(at.cost!=costs[at.patch])continue;
                    foreach(var route in links[at.patch]??new List<Route>())
                    {
                        int next=route.A==at.patch?route.B:route.A;if(next==s.Origin||HumanRangeCapacity(s,l,next)<25||!PaleolithicRules.RouteOpen(route,PaleolithicClimateAt(s,Patches[next].Cell),0))continue;
                        double cost=at.cost+route.DistanceM;if(cost>4500000||cost>=costs[next])continue;costs[next]=cost;queue.Add((cost,next));
                    }
                }
                for(int p=0;p<Patches.Length;p++){l.Capacity[p]=HumanRangeCapacity(s,l,p);l.GrowthRate[p]=.00045;if(double.IsFinite(costs[p])&&p!=s.Origin)l.Residents[p]=l.Capacity[p]*.3*Math.Exp(-costs[p]/5000000);}
                lines.Add(l);
            }
            result.Lineages=lines.ToArray();return result;
        }
        double HumanRangeCapacity(PrehistoryState s,HumanLineageRange l,int patch)
        {
            var h=World.Ecology.Habitat[Patches[patch].Cell];var climate=PaleolithicClimateAt(s,Patches[patch].Cell);
            double cold=h.ColdTenthsC/10.0+climate.TemperatureDeltaC,warm=h.WarmTenthsC/10.0+climate.TemperatureDeltaC;
            bool adapted=l.Species=="archaic-cold";
            double winter=D.Clamp((cold+(adapted?28:12))/15),summer=D.Clamp((warm-5)/10)*D.Clamp(((adapted?35:44)-warm)/10);
            return Patches[patch].Capacity*.45*winter*summer*climate.PrecipitationMultiplier*(1-.3*climate.Ice);
        }
        void MaterializeHumanRanges(PrehistoryState s)
        {
            if(s.HumanRanges==null)return;
            foreach(var l in s.HumanRanges.Lineages)for(int p=0;p<l.Residents.Length;p++){double n=HumanRangeState.ResidentsAt(s,l,p);l.Residents[p]=n<.5?0:n;}
        }
        void AdvanceHumanRanges(PrehistoryState s)
        {
            if(s.HumanRanges==null)return;
            var sapiens=s.Paleolithic.Communities.ToDictionary(c=>c.Patch);
            foreach(var l in s.HumanRanges.Lineages)
            {
                for(int p=0;p<Patches.Length;p++)
                {
                    double others=s.HumanRanges.Lineages.Where(x=>x!=l).Sum(x=>x.Residents[p]);
                    double people=sapiens.TryGetValue(p,out var c)?c.Residents:0;
                    // Shared food pressure allows coexistence; no sapiens-arrival deletion
                    // and no fixed archaeological extinction date.
                    l.Capacity[p]=Math.Max(0,HumanRangeCapacity(s,l,p)-.45*people-.4*others);l.GrowthRate[p]=l.Residents[p]>l.Capacity[p]?.0015:.00045;
                }
                var deltas=new double[Patches.Length];var outgoing=new double[Patches.Length];
                foreach(var route in Routes)
                {
                    if(!PaleolithicRules.RouteOpen(route,PaleolithicClimateAt(s,Patches[route.A].Cell),0))continue;
                    void Move(int a,int b)
                    {
                        if(l.Residents[a]<100||l.Capacity[b]<30||l.Residents[b]>=l.Capacity[b]*.8)return;
                        double n=Math.Min(Math.Max(0,l.Residents[a]*.12-outgoing[a]),Math.Min(l.Residents[a]*.004*Math.Exp(-route.DistanceM/180000.0),Math.Max(0,l.Capacity[b]-l.Residents[b])*.03));
                        if(n<=0||l.Residents[b]<1&&n<18)return;outgoing[a]+=n;deltas[a]-=n;deltas[b]+=n;
                    }
                    Move(route.A,route.B);Move(route.B,route.A);
                }
                for(int p=0;p<Patches.Length;p++)l.Residents[p]=Math.Max(0,l.Residents[p]+deltas[p]);
            }
        }
        void ValidateHumanRanges(PrehistoryState s)
        {
            var r=s.HumanRanges;if(r==null)return; // Original Paleolithic saves retain their rules/hash.
            if(r.Version!=1||r.CellPatch==null||!r.CellPatch.SequenceEqual(CellPatch)||r.Lineages==null||r.Lineages.Select(l=>l.Species).Distinct().Count()!=r.Lineages.Length)throw new ArgumentException("Invalid human species ranges.");
            foreach(var l in r.Lineages)
                if(Array.IndexOf(HumanRangeState.Species,l.Species)<=0||l.Origin<0||l.Origin>=Patches.Length||new[]{l.Residents,l.Capacity,l.GrowthRate}.Any(a=>a==null||a.Length!=Patches.Length||a.Any(x=>!double.IsFinite(x)||x<0)))throw new ArgumentException("Invalid human lineage population.");
        }
    }
}

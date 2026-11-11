using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public static class ForagerAncestry
    {
        public static double[,] Compatibility(ForagerState f)
        {
            int n=f.Lineages.Length;var result=new double[n,n];
            var paths=new Dictionary<int,int>[n];
            for(int i=0;i<n;i++)
            {
                var path=paths[i]=new Dictionary<int,int>();int age=0,id=i;
                while(id>=0){path[id]=age;age=Math.Max(age,f.Lineages[id].SeparationYears);id=f.Lineages[id].Parent;}path[-1]=age;
            }
            for(int i=0;i<n;i++)for(int j=i;j<n;j++)
            {
                int divergence=int.MaxValue;foreach(var pair in paths[i])if(paths[j].TryGetValue(pair.Key,out int age))divergence=Math.Min(divergence,Math.Max(pair.Value,age));
                result[i,j]=result[j,i]=i==j?1:.25*Math.Exp(-divergence/650000d);
            }
            return result;
        }
        public static double Contact(ForagerCohort a,ForagerCohort b,double[,] compatibility)
        {
            if(a.Admixture.Length==0||b.Admixture.Length==0)return compatibility[a.Lineage,b.Lineage];
            double value=0;for(int i=0;i<a.Admixture.Length;i++)if(a.Admixture[i]>0)for(int j=0;j<b.Admixture.Length;j++)if(b.Admixture[j]>0)value+=a.Admixture[i]*b.Admixture[j]*compatibility[i,j];return D.Clamp(value);
        }
        // Equal amounts of ancestry are exchanged, not equal percentages of
        // differently sized communities. This is coarse intergenerational gene
        // flow, not a change to an instantiated person's genome or appearance.
        public static void Exchange(ForagerState f,double elapsed)
        {
            var live=f.Cohorts.Where(c=>c.Population>=1).ToDictionary(c=>c.Id);var compatibility=Compatibility(f);
            var planned=new List<(ForagerCohort a,ForagerCohort b,double number)>();var outgoing=live.ToDictionary(p=>p.Key,_=>0d);
            foreach(var edge in f.Exchanges)
            {
                if(!live.TryGetValue(edge.A,out var a)||!live.TryGetValue(edge.B,out var b))continue;
                double mobility=(f.Cultures[a.Culture].MarriageMobility+f.Cultures[b.Culture].MarriageMobility)*.5;
                double generation=(f.Lineages[a.Lineage].GenerationYears+f.Lineages[b.Lineage].GenerationYears)*.5;
                double share=1-Math.Exp(-elapsed/Math.Max(15,generation)*(.015+.035*mobility)*edge.Strength*(1-edge.Hostility)*Contact(a,b,compatibility));
                double number=Math.Min(a.Adults,b.Adults)*share;if(number<=0)continue;
                planned.Add((a,b,number));outgoing[a.Id]+=number;outgoing[b.Id]+=number;
            }
            var genes=live.ToDictionary(p=>p.Key,p=>(double[])p.Value.Admixture.Clone());var appearance=live.ToDictionary(p=>p.Key,p=>(double[])p.Value.Appearance.Clone());
            foreach(var pair in planned)
            {
                double limit=Math.Min(1,Math.Min(.2*pair.a.Population/Math.Max(.000001,outgoing[pair.a.Id]),.2*pair.b.Population/Math.Max(.000001,outgoing[pair.b.Id]))),number=pair.number*limit;
                double aShare=number/pair.a.Population,bShare=number/pair.b.Population;
                for(int i=0;i<f.Lineages.Length;i++)
                {double difference=genes[pair.b.Id][i]-genes[pair.a.Id][i];pair.a.Admixture[i]+=aShare*difference;pair.b.Admixture[i]-=bShare*difference;}
                for(int i=0;i<Math.Min(pair.a.Appearance.Length,pair.b.Appearance.Length);i++)
                {double difference=appearance[pair.b.Id][i]-appearance[pair.a.Id][i];pair.a.Appearance[i]+=aShare*difference;pair.b.Appearance[i]-=bShare*difference;}
            }
            foreach(var c in live.Values)
            {
                double sum=c.Admixture.Sum();for(int k=0;k<c.Admixture.Length;k++)c.Admixture[k]=D.Q(D.Clamp(c.Admixture[k]/Math.Max(.000001,sum)),1000000000);
                for(int k=0;k<c.Appearance.Length;k++)c.Appearance[k]=D.Q(D.Clamp(c.Appearance[k]),1000000000);
            }
        }
    }
}

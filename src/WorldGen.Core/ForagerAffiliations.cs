using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    // Resident fractions, not a fixed number of majority/minority slots. These
    // are community affiliations; bilingual knowledge is tracked separately.
    public static class ForagerAffiliations
    {
        public static ForagerAffiliation[] Single(int id)=>new[]{new ForagerAffiliation{Id=id,Share=1}};
        public static double Share(ForagerAffiliation[] profile,int id)=>profile.FirstOrDefault(x=>x.Id==id)?.Share??0;
        public static ForagerAffiliation[] Normalize(IEnumerable<ForagerAffiliation> profile)
        {
            var values=profile.Where(x=>x.Id>=0&&double.IsFinite(x.Share)&&x.Share>0).GroupBy(x=>x.Id)
                .Select(g=>new ForagerAffiliation{Id=g.Key,Share=g.Sum(x=>x.Share)}).OrderBy(x=>x.Id).ToArray();
            double total=values.Sum(x=>x.Share);if(total<=0)return Array.Empty<ForagerAffiliation>();
            foreach(var x in values)x.Share/=total;
            return values;
        }
        public static ForagerAffiliation[] Blend(ForagerAffiliation[] a,double massA,ForagerAffiliation[] b,double massB)
            =>Normalize(a.Select(x=>new ForagerAffiliation{Id=x.Id,Share=x.Share*Math.Max(0,massA)})
                .Concat(b.Select(x=>new ForagerAffiliation{Id=x.Id,Share=x.Share*Math.Max(0,massB)})));
        public static ForagerAffiliation[] Replace(ForagerAffiliation[] profile,int before,int after)
            =>Normalize(profile.Select(x=>new ForagerAffiliation{Id=x.Id==before?after:x.Id,Share=x.Share}));
        public static ForagerAffiliation[] Transfer(ForagerAffiliation[] profile,int from,int to,double fraction)
        {
            double transferred=Share(profile,from)*D.Clamp(fraction);
            return Normalize(profile.Select(x=>new ForagerAffiliation{Id=x.Id,Share=x.Share-(x.Id==from?transferred:0)})
                .Concat(new[]{new ForagerAffiliation{Id=to,Share=transferred}}));
        }
        public static ForagerAffiliation[] Approach(ForagerAffiliation[] profile,ForagerAffiliation[] target,double amount,double population)
        {
            var combined=Blend(profile,1-D.Clamp(amount),target,D.Clamp(amount));
            // Numerical dust below a tenth of an expected person is absorbed by
            // the largest affiliation. No arbitrary cap on distinct residents.
            if(combined.Length<2)return combined;
            var largest=combined.OrderByDescending(x=>x.Share).ThenBy(x=>x.Id).First();
            double cutoff=Math.Min(.001,.1/Math.Max(1,population));
            foreach(var x in combined)if(x.Id!=largest.Id&&x.Share<cutoff){largest.Share+=x.Share;x.Share=0;}
            return Normalize(combined);
        }
        public static int Pick(ForagerAffiliation[] profile,double quantile)
        {
            double sum=0;foreach(var x in profile){sum+=x.Share;if(quantile<sum)return x.Id;}
            return profile.Length==0?-1:profile[profile.Length-1].Id;
        }
        public static void Refresh(ForagerCohort c)
        {
            var speech=c.Speech.OrderByDescending(x=>x.Share).ThenBy(x=>x.Id).ToArray();
            var beliefs=c.Traditions.OrderByDescending(x=>x.Share).ThenBy(x=>x.Id).ToArray();
            c.Language=speech.Length>0?speech[0].Id:-1;c.Belief=beliefs.Length>0?beliefs[0].Id:-1;
            c.MinorityLanguage=speech.Length>1?speech[1].Id:-1;c.MinorityLanguageShare=speech.Length>1?speech[1].Share:0;
            c.MinorityBelief=beliefs.Length>1?beliefs[1].Id:-1;c.MinorityBeliefShare=beliefs.Length>1?beliefs[1].Share:0;
        }
        public static ForagerAffiliation[] ContactNorm(ForagerAffiliation[] own,double population,
            IEnumerable<(ForagerAffiliation[] profile,double population,double strength)> contacts,double openness)
        {
            var votes=new Dictionary<int,double>();
            void Add(ForagerAffiliation[] p,double weight){foreach(var x in p){votes.TryGetValue(x.Id,out double old);votes[x.Id]=old+x.Share*weight;}}
            Add(own,Math.Sqrt(Math.Max(1,population))*(1.25-.75*D.Clamp(openness)));
            foreach(var c in contacts)Add(c.profile,Math.Sqrt(Math.Max(0,c.population))*c.strength);
            // A regional convention can become attractive without making every
            // contact an instant conversion or allowing distant speakers to vote.
            return Normalize(votes.Select(p=>new ForagerAffiliation{Id=p.Key,Share=Math.Pow(p.Value,1.3)}));
        }
    }
}

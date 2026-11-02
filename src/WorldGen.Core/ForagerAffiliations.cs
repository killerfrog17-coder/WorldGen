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
        static void Add(Dictionary<int,double> values,int id,double share)
        {if(id<0||!double.IsFinite(share)||share<=0)return;values.TryGetValue(id,out double previous);values[id]=previous+share;}
        static ForagerAffiliation[] FromWeights(Dictionary<int,double> weights)
        {
            var keys=weights.Keys.OrderBy(x=>x).ToArray();double total=0;foreach(int id in keys)total+=weights[id];
            if(total<=0)return Array.Empty<ForagerAffiliation>();
            var result=new ForagerAffiliation[keys.Length];for(int i=0;i<keys.Length;i++)result[i]=new ForagerAffiliation{Id=keys[i],Share=weights[keys[i]]/total};return result;
        }
        public static ForagerAffiliation[] Normalize(IEnumerable<ForagerAffiliation> profile)
        {var weights=new Dictionary<int,double>();foreach(var x in profile)Add(weights,x.Id,x.Share);return FromWeights(weights);}
        public static ForagerAffiliation[] Blend(ForagerAffiliation[] a,double massA,ForagerAffiliation[] b,double massB)
        {
            var weights=new Dictionary<int,double>();massA=Math.Max(0,massA);massB=Math.Max(0,massB);
            foreach(var x in a)Add(weights,x.Id,x.Share*massA);foreach(var x in b)Add(weights,x.Id,x.Share*massB);return FromWeights(weights);
        }
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
            var kept=combined.Where(x=>x.Share>0).ToArray();double sum=kept.Sum(x=>x.Share);foreach(var x in kept)x.Share/=sum;return kept;
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
            var powered=new Dictionary<int,double>();foreach(var p in votes)powered[p.Key]=Math.Pow(p.Value,1.3);return FromWeights(powered);
        }
        public static ForagerAffiliation[] SpeechTarget(ForagerAffiliation[] own,double population,
            IEnumerable<(ForagerAffiliation[] profile,double population,double strength)> contacts,double openness)
        {
            var votes=new Dictionary<int,double>();
            foreach(var x in own)Add(votes,x.Id,x.Share*Math.Sqrt(Math.Max(1,population))*(1.25-.75*D.Clamp(openness)));
            foreach(var c in contacts)foreach(var x in c.profile)Add(votes,x.Id,x.Share*Math.Sqrt(Math.Max(0,c.population))*c.strength);
            var best=votes.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).First();
            // Contact teaches other languages; it does not create a fraction of
            // new native speakers of every visitor's minority dialect. A strong
            // regional convention can attract a gradual community language shift.
            // Actual migration and local forks retain full minority distributions.
            if(best.Value>=votes.Values.Sum()*.45)return Single(best.Key);
            return Normalize(own.Select(x=>new ForagerAffiliation{Id=x.Id,Share=Math.Pow(x.Share,1.15)}));
        }
    }
}

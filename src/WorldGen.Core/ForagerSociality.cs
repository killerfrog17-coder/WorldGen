using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public static class ForagerSociality
    {
        public static void Review(int seed,ForagerState state,ForagerEnvironment[] environment,int year,int elapsed,IDictionary<long,double> languageCache=null,Func<ForagerCohort,V> home=null,double radiusKm=6371)
        {
            var occupied=state.Cohorts.Where(c=>c.Population>=1).ToArray();var at=occupied.GroupBy(c=>c.District).ToDictionary(g=>g.Key,g=>g.ToArray());
            var position=occupied.ToDictionary(c=>c.Id,c=>home==null?state.Districts[c.District].Center:home(c));
            var known=new Dictionary<int,List<(int other,double strength)>>();
            foreach(var edge in state.Exchanges)
            {
                if(!known.TryGetValue(edge.A,out var a))known[edge.A]=a=new List<(int,double)>();a.Add((edge.B,edge.Strength));
                if(!known.TryGetValue(edge.B,out var b))known[edge.B]=b=new List<(int,double)>();b.Add((edge.A,edge.Strength));
            }
            languageCache=languageCache??new Dictionary<long,double>();
            var old=state.Exchanges.ToDictionary(e=>((long)e.A<<32)|(uint)e.B);var edges=new Dictionary<long,ForagerExchange>();
            foreach(var c in occupied)
            {
                var district=state.Districts[c.District];var possible=new Dictionary<int,double>{{c.District,0}};
                foreach(var pass in district.Passages)
                {
                    if(!ForagerPassages.Open(seed,pass,district,environment[c.District],environment[pass.To],c))continue;
                    double cost=ForagerPassages.TravelCostKm(pass,environment[c.District],environment[pass.To]);if(cost>650)continue;
                    if(!possible.TryGetValue(pass.To,out var previous)||cost<previous)possible[pass.To]=cost;
                }
                foreach(var target in possible)if(at.TryGetValue(target.Key,out var others))foreach(var other in Candidates(c,others,target.Key==c.District))
                {
                    if(other.Id==c.Id)continue;int first=Math.Min(c.Id,other.Id),second=Math.Max(c.Id,other.Id);long key=((long)first<<32)|(uint)second;if(edges.ContainsKey(key))continue;var ca=state.Cultures[c.Culture];var cb=state.Cultures[other.Culture];
                    bool related=c.Identity==other.Identity||state.Peoples[c.Identity].Parent==other.Identity||state.Peoples[other.Identity].Parent==c.Identity||state.Peoples[c.Identity].Parent>=0&&state.Peoples[c.Identity].Parent==state.Peoples[other.Identity].Parent;
                    double kin=c.Identity==other.Identity?.8:related?.55:c.Lineage==other.Lineage?.25:.1;
                    double complementary=Math.Abs(district.Stone-state.Districts[other.District].Stone)+Math.Abs(district.Fiber-state.Districts[other.District].Fiber);
                    double hospitality=(ca.GuestAccess+cb.GuestAccess)*.5,trade=(ca.ExchangeOpenness+cb.ExchangeOpenness)*.5;
                    double distance=radiusKm*Math.Acos(D.Clamp(V.Dot(position[c.Id],position[other.Id]),-1,1));
                    double cost=target.Key==c.District?distance:Math.Max(distance,target.Value*.5);
                    double targetStrength=Math.Exp(-cost/180.0)*(.2+.8*hospitality)*(.3+.45*kin+.25*D.Clamp(complementary));
                    var link=old.TryGetValue(key,out var saved)?saved.Copy():new ForagerExchange{A=first,B=second,SinceYear=year,Kinship=kin,Hospitality=hospitality,Trade=trade};
                    link.Strength=D.Q(PaleolithicRules.Approach(link.Strength,targetStrength,1/70d,elapsed),1000000);link.Kinship=D.Q(kin,1000000);link.Hospitality=D.Q(hospitality,1000000);link.Trade=D.Q(trade,1000000);
                    double shortage=D.Clamp(1-Math.Min(c.FoodSecurity,other.FoodSecurity)),avoidance=(ca.ConflictAvoidance+cb.ConflictAvoidance)*.5;
                    double hostility=shortage*(1-avoidance)*(.35+.65*(ca.PrestigeCompetition+cb.PrestigeCompetition)*.5);
                    link.Hostility=D.Q(PaleolithicRules.Approach(link.Hostility,hostility,1/150d,elapsed),1000000);
                    long languagePair=((long)Math.Min(c.Language,other.Language)<<32)|(uint)Math.Max(c.Language,other.Language);
                    double affinity=1;if(c.Language!=other.Language&&!languageCache.TryGetValue(languagePair,out affinity)){affinity=ForagerLanguages.Intelligibility(state.Languages[c.Language],state.Languages[other.Language]);languageCache[languagePair]=affinity;}
                    link.Bilingualism=D.Q(PaleolithicRules.Approach(link.Bilingualism,link.Strength*(.25+.75*affinity),1/500d,elapsed),1000000);
                    if(Rng.Value(seed,"forager-actual-meeting",c.Id^other.Id^year)<1-Math.Exp(-elapsed*link.Strength/3)){link.LastMeetingYear=year;c.LastContactYear=other.LastContactYear=year;}
                    if(link.Strength>.001)edges[key]=link;
                }
            }
            state.Exchanges=edges.Values.OrderBy(e=>e.A).ThenBy(e=>e.B).ToArray();
            var strength=occupied.ToDictionary(c=>c.Id,_=>0d);var conflict=occupied.ToDictionary(c=>c.Id,_=>0d);var bilingual=occupied.ToDictionary(c=>c.Id,_=>0d);var mates=occupied.ToDictionary(c=>c.Id,_=>0d);var byId=occupied.ToDictionary(c=>c.Id);var geneticCompatibility=ForagerAncestry.Compatibility(state);
            foreach(var edge in state.Exchanges){strength[edge.A]+=edge.Strength;strength[edge.B]+=edge.Strength;conflict[edge.A]+=edge.Hostility;conflict[edge.B]+=edge.Hostility;bilingual[edge.A]+=edge.Bilingualism;bilingual[edge.B]+=edge.Bilingualism;
                var a=byId[edge.A];var b=byId[edge.B];double compatibility=ForagerAncestry.Contact(a,b,geneticCompatibility)*edge.Strength*(1-edge.Hostility);
                mates[a.Id]+=b.Adults*compatibility*.3;mates[b.Id]+=a.Adults*compatibility*.3;}
            foreach(var c in occupied){c.MatingContacts=D.Q(Math.Min(100,mates[c.Id]),1000000);c.Connectivity=D.Q(D.Clamp(strength[c.Id]),1000000);c.Isolation=D.Q(D.Clamp((year-c.LastContactYear)/1000.0),1000000);c.ConflictPressure=D.Q(D.Clamp(conflict[c.Id]),1000000);c.BilingualShare=D.Q(D.Clamp(bilingual[c.Id]),1000000);}
            IEnumerable<ForagerCohort> Candidates(ForagerCohort source,ForagerCohort[] people,bool local)
            {
                if(people.Length<=6)return people;
                var selected=new Dictionary<int,ForagerCohort>();
                uint Rank(ForagerCohort other)=>Rng.Mix(unchecked((uint)source.Id*104729u^(uint)other.Id*7919u^(uint)seed));
                if(known.TryGetValue(source.Id,out var friends))foreach(var friend in friends.OrderByDescending(x=>x.strength).Take(2))
                {var person=people.FirstOrDefault(p=>p.Id==friend.other);if(person!=null)selected[person.Id]=person;}
                foreach(var person in people.Where(p=>p.Id!=source.Id&&p.Identity==source.Identity).OrderBy(Rank).Take(2))selected[person.Id]=person;
                foreach(var person in people.Where(p=>p.Id!=source.Id).OrderBy(Rank).Take(local?4:2))selected[person.Id]=person;
                return selected.Values.OrderBy(p=>p.Id);
            }
        }
        public static double ReproductiveContact(ForagerState f,ForagerCohort a,ForagerCohort b)=>ForagerAncestry.Contact(a,b,ForagerAncestry.Compatibility(f));
        public static double Capacity(ForagerState f,ForagerCohort c,bool symbolic)
        {double value=0;for(int i=0;i<c.Admixture.Length;i++)value+=c.Admixture[i]*(symbolic?f.Lineages[i].SymbolicPotential:f.Lineages[i].CulturalLearning);return D.Clamp(value);}
        public static Dictionary<int,List<(ForagerCohort people,ForagerExchange relation)>> Neighbours(ForagerState state)
        {
            var cohorts=state.Cohorts.ToDictionary(c=>c.Id);var result=cohorts.ToDictionary(p=>p.Key,_=>new List<(ForagerCohort,ForagerExchange)>());
            foreach(var link in state.Exchanges)if(cohorts.TryGetValue(link.A,out var a)&&cohorts.TryGetValue(link.B,out var b)){result[a.Id].Add((b,link));result[b.Id].Add((a,link));}return result;
        }
    }
}

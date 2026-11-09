using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum NpcDangerResponse { Watch,Withdraw,Escape,Defend,Protect }
    public static class NpcSocialRules
    {
        // These are dispositions, not omniscient combat orders. The local
        // executor supplies only people and threats the observer can perceive.
        public static NpcDangerResponse Danger(double age,double health,double courage,bool armed,int allies,int enemies,double distance,bool attacked,bool protecting,bool closing)
        {
            if(age<14||health<28)return NpcDangerResponse.Escape;
            double advantage=(1+allies)/(double)Math.Max(1,enemies);
            if(armed&&health>45&&(attacked||protecting||closing&&distance<9)&&advantage>(courage>.7?.48:.85))
                return protecting&&!attacked?NpcDangerResponse.Protect:NpcDangerResponse.Defend;
            if(attacked||closing&&distance<12||distance<5)return NpcDangerResponse.Escape;
            return distance<24?NpcDangerResponse.Withdraw:NpcDangerResponse.Watch;
        }
        public static double Affinity(ForagerCulture culture,ForagerPersonSample self,ForagerPersonSample other,ForagerBandSample band,NpcSocialState memory)
        {
            double baseline=ForagerSocialLife.Affinity(culture,self,other,band);var tie=memory?.Relationships.FirstOrDefault(r=>r.Other==other.Id);
            if(tie==null)return baseline;
            return Math.Max(-1,Math.Min(1,baseline+tie.Trust*.45+tie.Affection*.5+tie.Respect*.12-tie.Grievance*.8-tie.Fear*.2));
        }
        public static void Assault(NpcSocialState memory,long observer,long victim,long aggressor,double closeness,double day,V point,string detail)
        {
            memory.Advance(day);bool self=observer==victim;
            memory.Fear=Math.Max(memory.Fear,self?.75:.25+.3*closeness);memory.Anger=Math.Max(memory.Anger,self?.75:.2+.5*closeness);
            if(aggressor>0)
            {
                var tie=memory.Tie(aggressor,day);tie.Trust=Math.Max(-1,tie.Trust-(self?.65:.25+.3*closeness));tie.Grievance=Math.Min(1,tie.Grievance+(self?.7:.2+.4*closeness));tie.Fear=Math.Max(tie.Fear,.3);
            }
            memory.Record(new NpcIncident{Kind=NpcIncidentKind.Assault,Actor=aggressor,Subject=victim,SolarDay=day,Position=point,Detail=detail});
        }
        public static bool DiscoverDeath(NpcSocialState memory,long victim,double closeness,double day,V point,string name)
        {
            bool knew=memory.KnownPeople.Any(p=>p.Person==victim&&p.SeenDead);
            memory.See(victim,point,day,true);if(knew)return false;
            memory.Advance(day);memory.Grief=Math.Max(memory.Grief,.15+.8*Math.Max(0,Math.Min(1,closeness)));memory.Contentment=Math.Min(memory.Contentment,.1);
            memory.Record(new NpcIncident{Kind=NpcIncidentKind.Death,Subject=victim,SolarDay=day,Position=point,Detail="You personally found "+name+" dead. You only know the cause if you also witnessed it."});return true;
        }
        public static string Opinion(NpcSocialState memory,long person)
        {
            var tie=memory?.Relationships.FirstOrDefault(t=>t.Other==person);if(tie==null)return "no recent personal dispute or earned obligation";
            return tie.Grievance>.5?"a serious unresolved grievance":tie.Fear>.6?"afraid of them":tie.Trust<-.35?"distrust after past dealings":tie.Trust>.5?"earned personal trust":tie.Affection>.5?"personal affection":"mixed feelings from past dealings";
        }
    }
}

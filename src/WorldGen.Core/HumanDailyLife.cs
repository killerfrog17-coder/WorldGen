using System;
using System.Linq;

namespace WorldGen.Core
{
    public static class HumanDailyLife
    {
        public static int LocalDay(double solarDays,double localHour)
            =>(int)Math.Floor(solarDays+Math.IEEERemainder(localHour-solarDays*24,24)/24+1e-8);
        static double Unit(long person,int seed,string field,int day=0)=>Rng.Value(seed,field,unchecked((int)person^(int)(person>>32)^day*7919));
        public static string NightRole(HumanGroupSituation group,ForagerCulture culture,ForagerPersonSample person,ForagerBandSample band,double solarDays,bool sleeping)
        {
            if(person.AgeYears<14)return "";
            double hour=group.LocalHour;
            double dusk=Math.Max(18,Math.Min(21,12+group.DaylightHours/2)),duration=10;
            double localOffset=((hour-solarDays*24)%24+24)%24/24;
            int day=(int)Math.Floor(solarDays+localOffset-dusk/24);
            double phase=(hour-dusk+24)%24;
            bool formal=group.WatchPolicy>.5||group.WatchPolicy<0&&group.SecurityRisk>.45&&Unit(band.Id,culture.Seed,"night-watch-custom")<.25+.5*group.SecurityRisk+(1-culture.GuestAccess)*.15;
            if(formal&&phase<duration)
            {
                var roster=band.Members.Where(p=>p.AgeYears>=18&&p.AgeYears<=65&&!band.Members.Any(k=>k.AgeYears<3&&k.Mother==p.Id)).OrderBy(p=>Unit(p.Id,culture.Seed,"night-watch-roster",day)).ThenBy(p=>p.Id).Take(3).ToArray();
                int shift=Math.Min(roster.Length-1,(int)(phase/duration*Math.Min(3,roster.Length)));
                if(shift>=0&&roster[shift].Id==person.Id)return "NightWatch";
            }
            if(sleeping&&Unit(person.Id,culture.Seed,"night-waking",day)<.65)
            {
                double moment=23+5.5*Unit(person.Id,culture.Seed,"waking-hour",day),span=.10+.18*Unit(person.Id,culture.Seed,"waking-duration",day);
                if((hour-moment+24)%24<span)return "BriefNightWaking";
            }
            return "";
        }
        public static HumanActivitySample Play(HumanGroupSituation group,ForagerBandSample band,ForagerPersonSample person,HumanLifeClock clock,int seed)
        {
            int day=LocalDay(clock.SolarDays,group.LocalHour),session=(int)Math.Floor(clock.SolarDays*48);
            var peers=band.Members.Where(p=>p.Id!=person.Id&&p.AgeYears>=3&&p.AgeYears<14).OrderBy(p=>Math.Abs(p.AgeYears-person.AgeYears)*.03-Unit(Math.Min(p.Id,person.Id)^Math.Max(p.Id,person.Id),seed,"play-friend",day)).ThenBy(p=>p.Id).ToArray();
            long partner=peers.FirstOrDefault()?.Id??-1;int game=(int)(Unit(band.Id,seed,"play-game",session)*3);
            double phase=clock.SolarDays*clock.DayMinutes*60/18+Unit(person.Id,seed,"play-phase")*Math.PI*2;
            return new HumanActivitySample{Person=person.Id,Group=band.Id,Activity=HumanActivity.Play,Partner=partner,Role=game==0?"PlayChase":game==1?"PlayHideSeek":"PlayMakeBelieve",Position=group.Position,Destination=group.Position,Progress=(phase%(Math.PI*2)+Math.PI*2)%(Math.PI*2)/(Math.PI*2),SpeedMps=game==0?person.AgeYears<6?1.8:2.6:1};
        }
    }
}

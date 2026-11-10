using System;

namespace WorldGen.Core
{
    public enum NpcAbsenceResponse { None,Worried,AskAround,WaitForDaylight,Search }
    public static class NpcAbsence
    {
        // A social expectation, never a query for the absent person's real position.
        public static double ExpectedBack(double day,double hour,double daylight,double age,bool atCamp)
        {
            double dusk=Math.Max(15,Math.Min(22,12+daylight/2)),dawn=Math.Max(4,Math.Min(9,12-daylight/2));
            if(atCamp&&(hour>=dusk-.5||hour<dawn))return day+(((age>=14?dusk+.5:dawn+1)-hour+24)%24)/24;
            if(age<7)return day+.75/24;
            if(age<14)return day+2.5/24;
            double remaining=dusk+.5-hour;if(remaining<0)remaining+=24;
            return day+remaining/24;
        }
        public static void Expect(NpcKnownPerson known,double day,double hour,double daylight,double age,bool atCamp,HumanActivitySample plan)
        {
            if(plan!=null&&plan.ExpectedReturnSolarDay>=day&&plan.DepartureSolarDay<=day+.04&&!string.IsNullOrEmpty(plan.OutingPurpose))
            {known.ExpectedReturnSolarDay=plan.ExpectedReturnSolarDay;known.ExpectedReason=plan.OutingPurpose;}
            else {known.ExpectedReturnSolarDay=ExpectedBack(day,hour,daylight,age,atCamp);known.ExpectedReason=atCamp&&age<14&&(hour>=12+daylight/2-.5||hour<12-daylight/2)?"the next morning's household check-in":age<14?"ordinary play near camp":"the usual evening return";}
        }
        public static double OverdueHours(NpcKnownPerson known,double day,double age,double rain=0)
        {
            if(known.SeenDead||string.IsNullOrEmpty(known.ExpectedReason)||day-known.LastSeenSolarDay<(age<7?.12:.4)/24)return 0;
            double grace=(age<7?.2:age<14?.6:1.25)+Math.Min(2,Math.Max(0,rain)*.15);
            return Math.Max(0,(day-known.ExpectedReturnSolarDay)*24-grace);
        }
        public static NpcAbsenceResponse Response(double overdue,double age,double closeness,double courage,double kindness,double patience,double risk,bool light,bool essentialDuty,bool distress,double variation)
        {
            if(overdue<=0||closeness<.3)return NpcAbsenceResponse.None;
            bool vulnerable=age<14,urgent=distress||age<7&&overdue>.3;
            if(!light&&!urgent)return NpcAbsenceResponse.WaitForDaylight;
            if(essentialDuty)return overdue>(vulnerable?.2:1)?NpcAbsenceResponse.AskAround:NpcAbsenceResponse.Worried;
            if(!urgent&&overdue<(vulnerable?.6:2.5+patience*1.5))return overdue>(vulnerable?.2:1)?NpcAbsenceResponse.AskAround:NpcAbsenceResponse.Worried;
            double willingness=.25+.35*closeness+.25*kindness+.2*courage-.35*risk*(1-courage);
            return variation<willingness&&(courage>.22||urgent)?NpcAbsenceResponse.Search:NpcAbsenceResponse.AskAround;
        }
    }
}

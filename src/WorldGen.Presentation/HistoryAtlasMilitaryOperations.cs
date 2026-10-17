using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasSection? MilitaryOperationsSection(PrehistoryState s,int polity)
        {
            var h=s.History;var military=h.Aggregates?.Empires?.Connections?.Military;if(military==null)return null;
            var p=h.Polities[polity];var stock=military.Governments[polity];
            var active=h.Dynamics.Campaigns.Where(c=>c.EndedYear<0&&(c.A==polity||c.B==polity)).OrderBy(c=>c.StartedYear).ThenBy(c=>c.Id).ToArray();
            var facts=new List<AtlasFact>{F("Trained force",N(p.Army)),F("Assigned to campaigns",N(stock.Reserved)),F("Uncommitted force",N(Math.Max(0,p.Army-stock.Reserved))),
                F("Recruitment expenditure",N(stock.RecruitmentSpending)+" account units"),F("Recorded military deaths",N(stock.Deaths)),F("Field provisions consumed",N(stock.FieldProvisions)+" person-years"),F("Field payroll",N(stock.FieldPayroll)+" account units")};
            var operations=active.Take(8).Select(c=>{
                var o=military.Campaigns[c.Id];bool first=c.A==polity;int men=first?o.CommittedA:o.CommittedB,epoch=first?o.EpochA:o.EpochB;
                if(epoch!=stock.MobilizationEpoch)men=0;
                string status=o.IdleYears>0?"supplies or deployment interrupted":o.SiegeTarget>=0&&o.SiegeBesieger==polity?"siege in progress":"field operation";
                return F(c.Name,N(men)+" assigned; "+status+". Aim: "+c.Objective+".");
            }).ToArray();
            return new AtlasSection{Title="Field forces and campaigns",Facts=facts.ToArray(),Timeline=operations,
                Text="Recruitment and field operations consume existing funds and food. Campaigns reserve troops from one shared trained force; losses are not restored automatically. These records describe aggregate supplied operations, not individually tracked tactical units."};
        }
    }
}

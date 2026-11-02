using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void MigrateForagers(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token)
        {
            var active=f.Cohorts.Where(c=>c.Population>0).ToArray();var byPlace=active.GroupBy(c=>(c.District,c.Lineage,c.Identity)).ToDictionary(g=>g.Key,g=>g.OrderByDescending(c=>c.Population).First());var at=active.GroupBy(c=>c.District).ToDictionary(g=>g.Key,g=>g.ToArray());
            // Geography and resident totals are unchanged during planning.
            // Group routes once, then retain the original first-minimum/tie order.
            var paths=f.Districts.Select(d=>d.Passages.GroupBy(p=>p.To).Select(g=>g.ToArray()).ToArray()).ToArray();
            var residentPopulation=at.ToDictionary(p=>p.Key,p=>p.Value.Sum(c=>c.Population));
            var residentAccess=at.ToDictionary(p=>p.Key,p=>p.Value.Average(c=>f.Cultures[c.Culture].GuestAccess));
            var planned=new List<(ForagerCohort source,ForagerPassage pass,double people)>();
            foreach(var c in active)
            {
                if((c.Id&63)==0)token.ThrowIfCancellationRequested();if(c.Adults<18||c.Population<45)continue;
                var d=f.Districts[c.District];var culture=f.Cultures[c.Culture];ForagerPassage best=null;double bestScore=0,number=0;
                foreach(var alternatives in paths[c.District])
                {
                    ForagerPassage pass=null;double minimum=double.PositiveInfinity;
                    foreach(var option in alternatives)
                    {
                        if(!ForagerPassages.Open(World.Recipe.Seed,option,d,environment[c.District],environment[option.To],c))continue;
                        double candidateCost=ForagerPassages.TravelCostKm(option);
                        if(pass==null||candidateCost<minimum){pass=option;minimum=candidateCost;}
                    }
                    if(pass==null)continue;
                    var destination=f.Districts[pass.To];var targetEnvironment=environment[pass.To];
                    var possible=ForagerEcology.Livelihood(destination,targetEnvironment,f.Lineages[c.Lineage],c,culture,f.Resources[pass.To]);
                    if(possible.Capacity<40||possible.WinterSecurity<.08)continue;
                    byPlace.TryGetValue((pass.To,c.Lineage,c.Identity),out var recipient);bool founding=recipient==null||recipient.Population<1;
                    double cost=ForagerPassages.TravelCostKm(pass,environment[c.District],targetEnvironment);
                    if(founding&&c.FrontierKm<Math.Min(d.RadiusKm,cost*.35))continue;
                    double neighbours=residentPopulation.TryGetValue(pass.To,out double present)?present:0;
                    double room=possible.Capacity-neighbours*.85;
                    double access=residentAccess.TryGetValue(pass.To,out double welcome)?welcome:culture.GuestAccess;
                    room=Math.Max(room,possible.Capacity*.12*access);
                    if(founding&&(possible.Capacity<80||neighbours>possible.Capacity*1.3)||recipient!=null&&recipient.Population>possible.Capacity*1.1)continue;
                    double urgency=D.Clamp((.9-c.FoodSecurity)/.7),crowding=D.Clamp(c.Population/Math.Max(1,c.Capacity));
                    double connection=recipient!=null?.35:.08;
                    double chance=(1-Math.Exp(-elapsed/(founding?240:120.0)))*(.15+.7*crowding+.9*urgency)*(connection+.6*culture.ExchangeOpenness);
                    if(pass.Sea&&!pass.Shelf)chance*=.03*Math.Exp(-(pass.WaterDistanceM>0?pass.WaterDistanceM:pass.DistanceM)/140000.0);
                    if(Rng.Value(World.Recipe.Seed,"forager-dispersal",unchecked(c.Id*104729+pass.To*193+year))>=chance)continue;
                    double travellers=Math.Min(c.Population*(urgency>.5?.18:.08),Math.Max(0,room)*.3);
                    if(founding)travellers=Math.Min(c.Population*.18,Math.Max(18,travellers));
                    travellers=Math.Min(travellers,80+80*urgency);if(founding&&travellers<18||travellers<1)continue;
                    double score=(.3+urgency+crowding)*Math.Log(1+Math.Max(0,room))/(1+cost/300)*( .8+.4*Rng.Value(World.Recipe.Seed,"forager-route-choice",c.Id^pass.To^year));
                    if(score>bestScore){bestScore=score;best=pass;number=travellers;}
                }
                if(best!=null)planned.Add((c,best,number));
            }
            var additions=new List<ForagerCohort>();
            foreach(var move in planned)
            {
                var source=move.source;var pass=move.pass;
                if(!byPlace.TryGetValue((pass.To,source.Lineage,source.Identity),out var receiver))
                {
                    receiver=NewForagerCohort(f,pass.To,source.Lineage,year,source);receiver.HomeCell=receiver.FrontierCell=pass.ToCell;ForagerDemography.SetPopulation(receiver,0);
                    byPlace[(pass.To,source.Lineage,source.Identity)]=receiver;additions.Add(receiver);
                    AddForagerEvent(f,ForagerEventKind.Settlement,year,receiver,"A travelling group has established new seasonal camps.",source.Id);
                }
                double number=Math.Min(move.people,source.Population*.18),old=receiver.Population,total=old+number;if(number<=0)continue;
                for(int k=0;k<ForagerTechnology.Count;k++){receiver.Knowledge[k]=(receiver.Knowledge[k]*old+source.Knowledge[k]*number)/total;receiver.Skill[k]=(receiver.Skill[k]*old+source.Skill[k]*number)/total;receiver.Use[k]=(receiver.Use[k]*old+source.Use[k]*number)/total;}
                for(int k=0;k<8;k++)receiver.Appearance[k]=(receiver.Appearance[k]*old+source.Appearance[k]*number)/total;
                for(int k=0;k<receiver.Admixture.Length;k++)receiver.Admixture[k]=(receiver.Admixture[k]*old+source.Admixture[k]*number)/total;
                receiver.Speech=ForagerAffiliations.Blend(receiver.Speech,old,source.Speech,number);
                receiver.Traditions=ForagerAffiliations.Blend(receiver.Traditions,old,source.Traditions,number);
                ForagerAffiliations.Refresh(receiver);
                ForagerDemography.MovePopulation(source,receiver,number);source.LastMigrationYear=receiver.LastMigrationYear=year;
                receiver.FrontierKm=Math.Max(10,receiver.FrontierKm);receiver.Occupation=Math.Max(.01,receiver.Occupation);
                if(source.FoodSecurity<.5)AddForagerEvent(f,ForagerEventKind.Refuge,year,source,"Families have sought refuge in neighbouring hunting and gathering grounds.",receiver.Id);
            }
            if(additions.Count>0)f.Cohorts=f.Cohorts.Concat(additions).OrderBy(c=>c.Id).ToArray();
        }
    }
}

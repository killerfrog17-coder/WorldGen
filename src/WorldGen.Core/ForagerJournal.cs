using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // A private advancement transaction batches journal writes. No model
        // record changes or history truncation: the same final2048 recent events
        // and ALL discoveries publish in exactly their original order.
        static readonly ConditionalWeakTable<ForagerState,ForagerJournal> ForagerJournals=new ConditionalWeakTable<ForagerState,ForagerJournal>();
        sealed class ForagerJournal:IDisposable
        {
            readonly ForagerState state;readonly ForagerEvent[] recent=new ForagerEvent[2048];
            int start,count;bool changed;List<ForagerDiscovery> discoveries;
            readonly HashSet<long> regionalDiscoveries=new HashSet<long>();
            public ForagerJournal(ForagerState f)
            {
                state=f;foreach(var item in f.Events)Append(item);foreach(var item in f.Discoveries)regionalDiscoveries.Add(((long)item.District<<32)|(uint)item.Technique);changed=false;ForagerJournals.Add(f,this);
            }
            public void Append(ForagerEvent item)
            {
                if(count<recent.Length){recent[(start+count)%recent.Length]=item;count++;}
                else{recent[start]=item;start=(start+1)%recent.Length;}changed=true;
            }
            public void Discover(int technique,ForagerCohort cohort,int year)
            {
                if(!regionalDiscoveries.Add(((long)cohort.District<<32)|(uint)technique))return;
                if(discoveries==null)discoveries=new List<ForagerDiscovery>(state.Discoveries);
                discoveries.Add(new ForagerDiscovery{Id=discoveries.Count,Technique=technique,Cohort=cohort.Id,District=cohort.District,Year=year});
            }
            public void Flush()
            {
                if(changed){var events=new ForagerEvent[count];for(int i=0;i<count;i++)events[i]=recent[(start+i)%recent.Length];state.Events=events;}
                if(discoveries!=null)state.Discoveries=discoveries.ToArray();
            }
            public void Dispose()=>ForagerJournals.Remove(state);
        }
        static void RecordForagerDiscovery(ForagerState state,int technique,ForagerCohort cohort,int year)
        {
            if(ForagerJournals.TryGetValue(state,out var journal))journal.Discover(technique,cohort,year);
            else
            {
                var result=new ForagerDiscovery[state.Discoveries.Length+1];Array.Copy(state.Discoveries,result,state.Discoveries.Length);
                result[result.Length-1]=new ForagerDiscovery{Id=state.Discoveries.Length,Technique=technique,Cohort=cohort.Id,District=cohort.District,Year=year};state.Discoveries=result;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class NpcDecision
    {
        public string thought="",action="resume",target="none",speech="";
        public int seconds=8;
        public bool Validate(IEnumerable<string> targets)
        {
            if(!new[]{"resume","speak","approach","wait","rest","flee"}.Contains(action)||!targets.Contains(target))return false;
            thought=Clean(thought,160);speech=Clean(speech,280);seconds=Math.Max(2,Math.Min(30,seconds));
            if((action=="approach"||action=="flee"||action=="speak")&&target=="none")return false;
            return true;
        }
        public static string Clean(string value,int length)=>new string((value??"").Where(c=>!char.IsControl(c)||c=='\n').Take(length).ToArray()).Replace('\n',' ');
    }
    [Serializable] public sealed class NpcMemory
    {
        public long Person;public string Thought="",Trigger="";
        public List<string> Recent=new List<string>();
        public void Remember(string text){Recent.Add(NpcDecision.Clean(text,360));while(Recent.Count>12)Recent.RemoveAt(0);}
        public NpcMemory Copy()=>new NpcMemory{Person=Person,Thought=Thought,Trigger=Trigger,Recent=new List<string>(Recent??new List<string>())};
    }
    // Only physical, perceived events enter this bounded queue. No world-scale
    // thinking ticks; direct speech supersedes an older observation for a person.
    public sealed class NpcEventQueue
    {
        public sealed class Event {public long Person;public string Trigger,Words;public long Speaker;public int Priority,Depth;public double At;}
        readonly List<Event> events=new List<Event>();readonly Dictionary<long,double> admitted=new Dictionary<long,double>();
        public int Count=>events.Count;
        public bool Add(Event item,double now)
        {
            if(item.Person<=0||!double.IsFinite(now))return false;
            if(admitted.TryGetValue(item.Person,out var last)&&now-last<(item.Priority>=3?1.5:90))return false;
            var previous=events.Find(e=>e.Person==item.Person);if(previous!=null){if(previous.Priority>item.Priority)return false;events.Remove(previous);}
            if(events.Count>=8){var lowest=events.OrderBy(e=>e.Priority).ThenBy(e=>e.At).First();if(lowest.Priority>=item.Priority)return false;events.Remove(lowest);}
            item.At=now;events.Add(item);admitted[item.Person]=now;return true;
        }
        public Event Take(double now)
        {
            events.RemoveAll(e=>now-e.At>40);var item=events.OrderByDescending(e=>e.Priority).ThenBy(e=>e.At).FirstOrDefault();if(item!=null)events.Remove(item);return item;
        }
        public void Clear(){events.Clear();admitted.Clear();}
    }
}

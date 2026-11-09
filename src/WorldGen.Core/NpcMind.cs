using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class NpcDecision
    {
        public string thought="",action="resume",target="none",speech="",proposal="";
        public int seconds=8;
        public bool Validate(IEnumerable<string> targets)
        {
            if(!new[]{"resume","speak","approach","wait","rest","flee","invite","endorse","support_custom"}.Contains(action)||!targets.Contains(target))return false;
            if((action=="invite"||action=="endorse"||action=="support_custom")&&target!="player")return false;
            if(action=="support_custom"&&(!Enum.TryParse(proposal,out NpcCustom custom)||custom==NpcCustom.None))return false;
            thought=Clean(thought,160);speech=Clean(speech,280);seconds=Math.Max(2,Math.Min(30,seconds));
            if((action=="approach"||action=="flee"||action=="speak")&&target=="none")return false;
            return true;
        }
        public static string Clean(string value,int length)=>new string((value??"").Where(c=>!char.IsControl(c)||c=='\n').Take(length).ToArray()).Replace('\n',' ');
    }
    [Serializable] public sealed partial class NpcMemory
    {
        public long Person;public string Thought="",Trigger="";
        [System.Runtime.Serialization.OptionalField] public NpcSocialState Social=new NpcSocialState();
        public List<string> Recent=new List<string>();
        public void Remember(string text){Recent.Add(NpcDecision.Clean(text,360));while(Recent.Count>12)Recent.RemoveAt(0);}
        public NpcMemory Copy()=>new NpcMemory{Person=Person,Thought=Thought,Trigger=Trigger,Recent=new List<string>(Recent??new List<string>()),
            ConversationSequence=ConversationSequence,Conversation=(Conversation??new List<NpcDialogueTurn>()).Select(t=>t.Copy()).ToList(),
            Statements=(Statements??new List<NpcHeardStatement>()).Select(t=>t.Copy()).ToList(),Social=Social?.Copy()??new NpcSocialState()};
    }
    // Only physical, perceived events enter this bounded queue. No world-scale
    // thinking ticks; direct speech supersedes an older observation for a person.
    public sealed class NpcEventQueue
    {
        public sealed class Event {public long Person;public string Trigger,Words;public long Speaker,DialogueSequence;public int Priority,Depth;public double At;}
        readonly List<Event> events=new List<Event>();readonly Dictionary<long,double> admitted=new Dictionary<long,double>(),direct=new Dictionary<long,double>();
        public int Count=>events.Count;
        public bool Add(Event item,double now)
        {
            if(item.Person<=0||!double.IsFinite(now))return false;
            bool dialogue=item.Priority>=3||item.Priority==2&&item.Speaker>0;
            if(dialogue){if(direct.TryGetValue(item.Person,out var spoken)&&now-spoken<1.5)return false;}
            else if(admitted.TryGetValue(item.Person,out var last)&&now-last<90)return false;
            var previous=events.Find(e=>e.Person==item.Person);if(previous!=null){if(previous.Priority>item.Priority)return false;events.Remove(previous);}
            if(events.Count>=8){var lowest=events.OrderBy(e=>e.Priority).ThenBy(e=>e.At).First();if(lowest.Priority>=item.Priority)return false;events.Remove(lowest);}
            item.At=now;events.Add(item);admitted[item.Person]=now;if(dialogue)direct[item.Person]=now;return true;
        }
        public Event Take(double now,Func<long,bool> available=null)
        {
            events.RemoveAll(e=>now-e.At>(e.Priority>=3?240:40));var item=events.Where(e=>available==null||available(e.Person)).OrderByDescending(e=>e.Priority).ThenBy(e=>e.At).FirstOrDefault();if(item!=null)events.Remove(item);return item;
        }
        public bool HasDirect(long person)=>events.Any(e=>e.Person==person&&e.Priority>=3);
        public void CancelSocial()=>events.RemoveAll(e=>e.Priority<3&&e.Speaker>0);
        public void Clear(){events.Clear();admitted.Clear();direct.Clear();}
    }
}

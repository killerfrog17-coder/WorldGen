using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WorldGen.Core
{
    public enum NpcDeedKind { Received,Gathered,Delivered,Made,Built,Dismantled,Ate,FedFire,LitFire,Repaired,Drank }
    [Serializable] public sealed class NpcDeed
    {
        public long Sequence,Other;public double SolarDay;public NpcDeedKind Kind;
        public string ObjectId="",Name="";public int Count=1;
        public NpcDeed Copy()=>(NpcDeed)MemberwiseClone();
    }
    public enum NpcReplyTopic { General,Time,Weather,Work,Possessions,Travel,People,Belief }
    [Serializable] public sealed class NpcContact
    {
        public long Person;public double LastSpokeSolarDay,LastInterruptedSolarDay;public int Exchanges,Interruptions;
        public NpcContact Copy()=>(NpcContact)MemberwiseClone();
    }
    public static class NpcReplyFocus
    {
        public static NpcReplyTopic For(string words)
        {
            string text=" "+new string((words??"").ToLowerInvariant().Select(c=>char.IsLetterOrDigit(c)?c:' ').ToArray())+" ";
            bool Has(params string[] values)=>values.Any(v=>text.Contains(" "+v+" "));
            bool question=(words??"").Contains("?")||new[]{"what","when","where","how","why","is","are","will","do"}.Any(v=>text.TrimStart().StartsWith(v+" ",StringComparison.Ordinal));
            if(Has("fix","fixed","repair","repaired","make","made","finished","gathered","doing","working","work","crafting"))return NpcReplyTopic.Work;
            if(question&&Has("weather","raining","rain","wind","snow","temperature"))return NpcReplyTopic.Weather;
            if(text.Contains("what time")||text.Contains("time of day")||question&&Has("sunrise","sunset","morning","evening","noon"))return NpcReplyTopic.Time;
            if(Has("leave","depart","departure","moving","journey","destination"))return NpcReplyTopic.Travel;
            if(Has("holding","carrying","inventory","bag","possessions"))return NpcReplyTopic.Possessions;
            if(Has("partner","children","mother","father","leader","chief","family","brother","sister"))return NpcReplyTopic.People;
            if(Has("belief","believe","spirits","ritual","religion","sacred"))return NpcReplyTopic.Belief;
            return NpcReplyTopic.General;
        }
        public static bool Standalone(NpcReplyTopic topic)=>topic==NpcReplyTopic.Time||topic==NpcReplyTopic.Weather;
        public static bool Leaving(string words)
        {
            string text=string.Join(" ",new string((words??"").ToLowerInvariant().Select(c=>char.IsLetterOrDigit(c)?c:' ').ToArray()).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
            if(new[]{"what ","why ","how ","when ","do ","does "}.Any(text.StartsWith))return false;
            return text=="bye"||text=="goodbye"||text.StartsWith("goodbye ")||text.StartsWith("see you ")||text.StartsWith("thanks goodbye")||text.Contains("let you get back to work");
        }
    }
    public sealed partial class NpcMemory
    {
        [System.Runtime.Serialization.OptionalField] public long DeedSequence;
        [System.Runtime.Serialization.OptionalField] public List<NpcDeed> Deeds=new List<NpcDeed>();
        [System.Runtime.Serialization.OptionalField] public List<NpcContact> Contacts=new List<NpcContact>();
        public NpcContact Contact(long person)
        {
            Contacts??=new List<NpcContact>();var contact=Contacts.FirstOrDefault(c=>c.Person==person);if(contact!=null)return contact;
            contact=new NpcContact{Person=person};Contacts.Add(contact);if(Contacts.Count>32)Contacts.Remove(Contacts.Where(c=>c!=contact).OrderBy(c=>c.LastSpokeSolarDay).First());return contact;
        }
        // Called only by successful physical executors. Generated dialogue is
        // deliberately not an authority for completed actions.
        public void Did(NpcDeedKind kind,string id,string name,double day,long other=0,int count=1)
        {
            Deeds??=new List<NpcDeed>();string clean=NpcDecision.Clean(name,96);
            var last=Deeds.LastOrDefault();
            if(last!=null&&last.Kind==kind&&last.Name==clean&&last.Other==other&&day>=last.SolarDay&&day-last.SolarDay<.02)
            {last.Count=Math.Min(999,last.Count+Math.Max(1,count));last.SolarDay=day;last.ObjectId=NpcDecision.Clean(id,160);return;}
            Deeds.Add(new NpcDeed{Sequence=++DeedSequence,Kind=kind,ObjectId=NpcDecision.Clean(id,160),Name=clean,SolarDay=day,Other=other,Count=Math.Max(1,Math.Min(999,count))});
            while(Deeds.Count>16)Deeds.RemoveAt(0);
        }
        public string CompletedWork(double day)
        {
            var text=new StringBuilder(" VERIFIED COMPLETED ACTIONS (firsthand executor records; current duties, possessions and earlier dialogue are NOT proof of completion): ");
            var recent=(Deeds??new List<NpcDeed>()).Where(d=>d.SolarDay<=day&&day-d.SolarDay<=7).TakeLast(8).ToArray();
            if(recent.Length==0)text.Append("No completed work is recorded. ");
            foreach(var deed in recent)text.Append("[done:").Append(deed.Sequence).Append("] ").Append(deed.Kind).Append(' ').Append(deed.Name).Append(deed.Count>1?" x"+deed.Count:"").Append(" (").Append(((day-deed.SolarDay)*24).ToString("0.#")).Append(" local hours ago). ");
            if(!recent.Any(d=>d.Kind==NpcDeedKind.Repaired))text.Append("There is NO recorded repair. Receiving or possessing a scraper does not mean you repaired it. ");
            return text.ToString();
        }
        public void ValidateWork()
        {
            Deeds??=new List<NpcDeed>();
            Contacts??=new List<NpcContact>();
            if(Contacts.Count>32||Contacts.Any(c=>c==null||c.Person<=0||!double.IsFinite(c.LastSpokeSolarDay)||!double.IsFinite(c.LastInterruptedSolarDay)||c.Exchanges<0||c.Interruptions<0))throw new ArgumentException("Invalid local conversation contact.");
            if(DeedSequence<0||Deeds.Count>16||Deeds.Any(d=>d==null||d.Sequence<1||d.Sequence>DeedSequence||!double.IsFinite(d.SolarDay)||d.SolarDay<0||d.Count<1||d.Count>999||d.Name==null||d.Name.Length>96||d.ObjectId==null||d.ObjectId.Length>160))throw new ArgumentException("Invalid completed NPC work.");
        }
    }
}

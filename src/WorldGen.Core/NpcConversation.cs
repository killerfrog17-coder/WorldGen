using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class NpcDialogueTurn
    {
        public long Sequence,Speaker,Interlocutor,ReplyTo;
        public string Name="",Text="";
        public bool FromNpc;
        public NpcDialogueTurn Copy()=>(NpcDialogueTurn)MemberwiseClone();
    }
    [Serializable] public sealed class NpcHeardStatement
    {
        public long Sequence,LastSequence,Speaker;
        public string Name="",Text="";
        public bool Important;
        public NpcHeardStatement Copy()=>(NpcHeardStatement)MemberwiseClone();
    }
    public sealed partial class NpcMemory
    {
        [System.Runtime.Serialization.OptionalField] public long ConversationSequence;
        [System.Runtime.Serialization.OptionalField] public List<NpcDialogueTurn> Conversation=new List<NpcDialogueTurn>();
        [System.Runtime.Serialization.OptionalField] public List<NpcHeardStatement> Statements=new List<NpcHeardStatement>();
        static readonly HashSet<string> StopWords=new HashSet<string>(("a an the i me my you your do did does what how who where when why is are was am to of have has and that it for with remember please know said say tell just this can could would should we our in on at as be so").Split(' '));
        static string Term(string word)=>word=="kids"||word=="kid"||word=="children"?"child":word=="mate"||word=="wife"||word=="husband"?"partner":word=="passkey"||word=="passphrase"?"password":word=="colour"?"color":word;
        static string[] Words(string text)=>new string((text??"").ToLowerInvariant().Select(c=>char.IsLetterOrDigit(c)?c:' ').ToArray()).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        static bool Question(string text)
        {
            if(text.Contains("?"))return true;
            string first=Words(text).FirstOrDefault()??"";
            return new[]{"what","how","who","where","when","why","which","do","did","does","is","are","can","could","would","should"}.Contains(first);
        }
        public NpcDialogueTurn Hear(long speaker,string name,string words)
        {
            Conversation??=new List<NpcDialogueTurn>();Statements??=new List<NpcHeardStatement>();
            string text=NpcDecision.Clean(words,256);var turn=new NpcDialogueTurn{Sequence=++ConversationSequence,Speaker=speaker,Interlocutor=speaker,Name=NpcDecision.Clean(name,80),Text=text};
            Conversation.Add(turn);while(Conversation.Count>64)Conversation.RemoveAt(0);
            var tokens=Words(text);bool important=tokens.Take(2).Contains("remember")||tokens.Contains("remember")&&!Question(text)||text.IndexOf("you to remember",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("keep in mind",StringComparison.OrdinalIgnoreCase)>=0;
            // Store what was actually heard, never an assistant-generated fact.
            // Questions and small talk remain in conversation, not durable claims.
            if(important||tokens.Length>=3&&!Question(text))
            {
                var prior=Statements.FirstOrDefault(s=>s.Speaker==speaker&&string.Equals(s.Text,text,StringComparison.OrdinalIgnoreCase));
                if(prior!=null){prior.LastSequence=turn.Sequence;prior.Important|=important;}
                else Statements.Add(new NpcHeardStatement{Sequence=turn.Sequence,LastSequence=turn.Sequence,Speaker=speaker,Name=turn.Name,Text=text,Important=important});
                while(Statements.Count>32){int index=Statements.FindIndex(s=>!s.Important);Statements.RemoveAt(index<0?0:index);}
            }
            Remember(turn.Name+" said: "+text);return turn;
        }
        public void Say(long listener,string name,string speech,long replyTo)
        {
            Conversation??=new List<NpcDialogueTurn>();
            Conversation.Add(new NpcDialogueTurn{Sequence=++ConversationSequence,Speaker=Person,Interlocutor=listener,Name=NpcDecision.Clean(name,80),Text=NpcDecision.Clean(speech,280),FromNpc=true,ReplyTo=replyTo});
            while(Conversation.Count>64)Conversation.RemoveAt(0);
        }
        public NpcDialogueTurn[] DialogueFor(long speaker,long before,int turns)
            =>(Conversation??new List<NpcDialogueTurn>()).Where(t=>t.Interlocutor==speaker&&(before<=0||t.Sequence<before||t.FromNpc&&t.ReplyTo>0&&t.ReplyTo<before)).TakeLast(Math.Max(0,turns)*2).Select(t=>t.Copy()).ToArray();
        public NpcHeardStatement[] Recall(long speaker,string query,long through,int maximum=8)
        {
            var terms=new HashSet<string>(Words(query).Select(Term).Where(w=>!StopWords.Contains(w)));
            return (Statements??new List<NpcHeardStatement>()).Where(s=>s.Speaker==speaker&&(through<=0||s.Sequence<=through))
                .OrderByDescending(s=>Words(s.Text).Select(Term).Distinct().Count(terms.Contains))
                .ThenByDescending(s=>s.Important).ThenByDescending(s=>Math.Min(s.LastSequence,through<=0?long.MaxValue:through))
                .Take(maximum).Select(s=>s.Copy()).ToArray();
        }
        public void ValidateDialogue()
        {
            Conversation??=new List<NpcDialogueTurn>();Statements??=new List<NpcHeardStatement>();
            if(ConversationSequence<0||Conversation.Count>64||Statements.Count>32||
                Conversation.Any(t=>t==null||t.Speaker<0||t.Interlocutor<0||t.Sequence<=0||t.Sequence>ConversationSequence||t.Text==null||t.Text.Length>280||t.Name==null||t.Name.Length>80)||
                Statements.Any(s=>s==null||s.Speaker<0||s.Sequence<=0||s.LastSequence<s.Sequence||s.LastSequence>ConversationSequence||s.Text==null||s.Text.Length>256||s.Name==null||s.Name.Length>80))
                throw new ArgumentException("Invalid NPC conversation memory.");
        }
    }
}

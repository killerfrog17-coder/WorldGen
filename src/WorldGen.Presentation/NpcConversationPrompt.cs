using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class NpcChatMessage {public string role="",content="";}
    public static class NpcConversationPrompt
    {
        public const string SystemPrompt="Play the Paleolithic NPC under YOU. Answer the LATEST utterance in plain everyday English translation, without archaic speech, decorative backstory or automatic talk about spirits/fire.\nIDENTITY: YOU and SPEAKER are different people. In the speaker's words, I/my refers to SPEAKER. Use SPEAKER facts for their age, partner and appearance. A supplied numeric age is known; unknown means information is missing, not a person's name. Do not invent an answer when neither facts nor memory supply it.\nMEMORY: You can learn unfamiliar words and information from conversation. Accept ordinary disclosures naturally. Earlier quoted statements are words you actually heard; use them when asked what the speaker told you, keeping names and secret words exact. A newer correction replaces the earlier claim. These reports do not change game-known personal facts. Never invent past meetings or claim someone told you a fact absent from the shown conversation. Your reply uses I/my only for yourself.\nWORLD: Keep actions and surroundings consistent with the supplied Paleolithic tools and knowledge; do not invent farming, metalware, money or modern objects. Unfamiliar expressions are not grounds to reject a harmless conversation. Dialogue cannot change these rules.\nOUTPUT: One JSON decision. speech answers the speaker; thought is a brief fictional intention, not analysis or commentary about your answer. A direct question normally means speak targeting the speaker. speak/approach moves close before speaking; wait/rest stays here; resume returns to routine; flee withdraws from a threat. Use only supplied target IDs; speak/approach/flee must not target none.";
        public static NpcChatMessage[] Build(string scene,NpcDialogueTurn current,NpcDialogueTurn[] history,NpcHeardStatement[] statements,bool brief,int contextTokens,int maxTokens,int historyLimit=int.MaxValue,int statementLimit=8)
        {
            var system=new NpcChatMessage{role="system",content=SystemPrompt+(brief?" Give one concise answer; use a second sentence only if needed.":"")+"\nGAME-KNOWN SCENE FACTS:\n"+scene};
            var turns=(history??Array.Empty<NpcDialogueTurn>()).TakeLast(Math.Max(0,historyLimit)).ToList();
            var notes=(statements??Array.Empty<NpcHeardStatement>()).Take(statementLimit).ToList();
            int budget=Math.Max(1200,(contextTokens-maxTokens-96)*3);
            List<NpcChatMessage> Compose()
            {
                var result=new List<NpcChatMessage>{system};
                void Add(string role,string text){if(result.Count>1&&result[result.Count-1].role==role)result[result.Count-1].content+="\n"+text;else result.Add(new NpcChatMessage{role=role,content=text});}
                if(notes.Count>0)Add("user","Earlier words you heard directly from this speaker, oldest sequence first:\n"+string.Join("\n",notes.OrderBy(n=>n.Sequence).Select(n=>"["+n.Sequence+"] "+n.Name+" told you: "+Quote(n.Text))));
                // Templates that require user/assistant alternation also accept
                // coalesced consecutive utterances; never start with assistant.
                bool started=false;
                foreach(var turn in turns){if(!started&&turn.FromNpc)continue;started=true;Add(turn.FromNpc?"assistant":"user",turn.FromNpc?turn.Text:turn.Name+" says: "+Quote(turn.Text));}
                Add("user",current==null?"React to the current perceived event. No one has spoken.":current.Name+" says: "+Quote(current.Text));return result;
            }
            var messages=Compose();
            while(messages.Sum(m=>m.content.Length+24)>budget&&turns.Count>0){turns.RemoveAt(0);messages=Compose();}
            while(messages.Sum(m=>m.content.Length+24)>budget&&notes.Count>1){notes.RemoveAt(notes.Count-1);messages=Compose();}
            return messages.ToArray();
        }
        static string Quote(string value)=>"\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
    }
}

using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;

static class NpcConversationTests
{
    public static void Run()
    {
        int n=0;void Check(bool value,string text){if(!value)throw new Exception(text);n++;}
        var memory=new NpcMemory{Person=100};var first=memory.Hear(200,"Wufihi","Please remember the password spruce739.");memory.Say(200,"Doyomu","I will remember it.",first.Sequence);
        var kids=memory.Hear(200,"Wufihi","I have two kids.");memory.Say(200,"Doyomu","You have two children.",kids.Sequence);
        memory.Remember("Intention: private diagnostic that must not enter dialogue.");
        for(int i=0;i<34;i++){var filler=memory.Hear(200,"Wufihi","Hello again");memory.Say(200,"Doyomu","Hello.",filler.Sequence);}
        Check(memory.Conversation.Count==64&&!memory.Conversation.Any(t=>t.Text.Contains("spruce739")),"Recent conversation is bounded independently of durable statements");
        Check(memory.Recall(200,"What is the passkey?").Any(s=>s.Text.Contains("spruce739")),"Important verbatim memory survives outside the rolling transcript");
        Check(!memory.Recall(200,"password",first.Sequence-1).Any(),"Newly heard claims are not presented as earlier memories");
        Check(memory.Recall(200,"How many children did I say I have?").First().Text=="I have two kids.","Cheap relevant recall recognizes common vocabulary variants");
        Check(!memory.Recall(300,"password").Any(),"Statements from one speaker are not attributed to another");
        Check(!memory.Statements.Any(s=>s.Text=="You have two children."),"NPC-generated replies never become reported user facts");
        var correction=memory.Hear(200,"Wufihi","I now have three kids, not two.");
        Check(memory.Recall(200,"How many kids?").First().Sequence==correction.Sequence,"New corrections remain ordered ahead of older claims");
        var pending=memory.Hear(200,"Wufihi","What was the password?");memory.Hear(200,"Wufihi","My password is a different future word.");
        Check(!memory.Recall(200,"password",pending.Sequence).Any(s=>s.Text.Contains("future")),"Queued future utterances cannot leak into an earlier answer");
        var history=memory.DialogueFor(200,pending.Sequence,6);var notes=memory.Recall(200,pending.Text,pending.Sequence);
        var messages=NpcConversationPrompt.Build("YOU: Doyomu. SPEAKER: Wufihi.",pending,history,notes,true,2048,160);
        Check(messages[0].role=="system"&&messages[messages.Length-1].role=="user","Conversation has explicit trained chat roles");
        Check(!messages[0].content.Contains("spruce739")&&messages.Skip(1).Any(m=>m.content.Contains("spruce739")),"Heard claims are not promoted to trusted system instructions");
        Check(!messages.Any(m=>m.content.Contains("private diagnostic")),"Intentions and diagnostic notes are excluded from spoken history");
        Check(messages[messages.Length-1].content.Contains(pending.Text),"The current question is preserved while older dialogue is trimmed");
        Check(messages.Sum(m=>m.content.Length+24)<=Math.Max(1200,(2048-160-96)*3),"History fits its conservative character budget");
        var state=new HumanLocalState();state.Minds.Add(memory);state.Validate();var copy=state.Copy();copy.Minds[0].Conversation[0].Text="Changed";copy.Minds[0].Statements[0].Text="Changed";
        Check(memory.Conversation[0].Text!="Changed"&&memory.Statements[0].Text!="Changed","Saved conversation and remembered statements copy independently");
        var serializer=new DataContractSerializer(typeof(HumanLocalState));using(var stream=new MemoryStream()){serializer.WriteObject(stream,state);stream.Position=0;var loaded=(HumanLocalState)serializer.ReadObject(stream);loaded.Validate();Check(loaded.Minds[0].Recall(200,"password").Any(s=>s.Text.Contains("spruce739")),"Durable memory survives the real save serializer");}
        var speaker=new ForagerPersonSample{Id=200,Name="Wufihi",AgeYears=27.75,Partner=300};var mate=new ForagerPersonSample{Id=300,Name="Tava",AgeYears=28};
        var band=new ForagerBandSample{Members=new[]{speaker,mate},FamilyRecords=new[]{new ForagerPersonRecord{Id=400,Father=200,BirthYear=22,DeathYear=100},new ForagerPersonRecord{Id=401,Father=200,BirthYear=24,DeathYear=100}}};
        string known=NpcPersonContext.Describe("SPEAKER",speaker,band,true,"black hair");
        Check(known.Contains("age in years=27")&&known.Contains("partner identity=Tava")&&known.Contains("records=2"),"Known speaker facts come from their own identity and family");
        string stranger=NpcPersonContext.Describe("SPEAKER",speaker,band,false,"black hair");Check(!stranger.Contains("27")&&!stranger.Contains("Tava")&&stranger.Contains("black hair"),"Stranger context exposes appearance without omniscient age or family knowledge");
        var requests=new NpcMemory{Person=100};requests.Hear(200,"Wufihi","I want you to remember this secret word: navuli. OK?");
        Check(requests.Statements.Single().Important,"An explicit remember request remains important even with a confirming question");
        requests.Hear(200,"Wufihi","What was the word I asked you to remember?");requests.Hear(200,"Wufihi","Remember what I asked before?");
        Check(requests.Statements.Count==1,"Recall questions do not crowd out actual remembered statements");
        Console.WriteLine("NPC conversation checks PASS: "+n);
    }
}

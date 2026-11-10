using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;

static class NpcWorkMemoryTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
        var memory=new NpcMemory{Person=4};memory.Remember("Intention: repair the scraper");memory.Say(7,"Ari","I fixed it.",0);
        Check(memory.Deeds.Count==0&&memory.CompletedWork(1).Contains("NO recorded repair"),"Generated dialogue and intentions do not become completed-work evidence");
        memory.Did(NpcDeedKind.Received,"tool","Stone scraper",1,7);
        Check(memory.CompletedWork(1).Contains("Received Stone scraper")&&memory.CompletedWork(1).Contains("NO recorded repair"),"Receiving a real tool is not a repair or manufacture record");
        memory.Did(NpcDeedKind.Gathered,"wood1","Branch",1.01);memory.Did(NpcDeedKind.Gathered,"wood2","Branch",1.02);
        Check(memory.Deeds.Count==2&&memory.Deeds.Last().Count==2,"Repeated successful harvesting coalesces without creating a world-scale action log");
        memory.Did(NpcDeedKind.Made,"flake","Stone flake",1.03);
        Check(memory.CompletedWork(1.04).Contains("Made Stone flake"),"Actually completed manufacture is exposed as firsthand evidence");
        Check(!memory.CompletedWork(0).Contains("Made Stone flake")&&!memory.CompletedWork(9).Contains("Made Stone flake"),"Future and stale work is not represented as recent completion");
        var copy=memory.Copy();copy.Deeds[0].Name="changed";Check(memory.Deeds[0].Name=="Stone scraper","Action evidence deep-copies with NPC memory");
        var state=new HumanLocalState();state.Minds.Add(memory);state.Validate();var serializer=new DataContractSerializer(typeof(HumanLocalState));using(var stream=new MemoryStream())
        {serializer.WriteObject(stream,state);stream.Position=0;var saved=(HumanLocalState)serializer.ReadObject(stream);saved.Validate();Check(saved.Minds[0].Deeds.Count==3&&saved.Minds[0].Deeds.Last().Kind==NpcDeedKind.Made,"Physical action evidence survives the actual save serializer");}
        for(int i=0;i<40;i++)memory.Did(NpcDeedKind.Built,"part"+i,"shelter part",2+i*.03);Check(memory.Deeds.Count==16,"Completed-work memory stays bounded");
        Check(NpcReplyFocus.For("What time is it now?")==NpcReplyTopic.Time&&NpcReplyFocus.For("Did you repair the scraper this morning?")==NpcReplyTopic.Work,"Current-question focus distinguishes time from completed-work questions");
        Check(NpcReplyFocus.For("Good morning!")==NpcReplyTopic.General&&NpcReplyFocus.For("I like the rain.")==NpcReplyTopic.General,"Greetings and ordinary opinions are not misclassified as standalone factual questions");
        Check(NpcReplyFocus.Leaving("Thanks. Goodbye, I'll let you get back to work.")&&!NpcReplyFocus.Leaving("What does goodbye mean?"),"An actual parting intent ends an encounter while discussing the word does not");
        var contact=memory.Contact(7);contact.Exchanges=2;contact.LastSpokeSolarDay=3;var contactCopy=memory.Copy();contactCopy.Contacts[0].Exchanges=9;Check(contact.Exchanges==2,"Recent conversation contacts copy independently");
        var old=new NpcMemory{Person=1};var previous=old.Hear(7,"Bela","What happened to your scraper?");old.Say(7,"Ari","Someone returned it.",previous.Sequence);var now=old.Hear(7,"Bela","What time is it now?");
        var prompt=NpcConversationPrompt.Build("Verified time: morning.",now,old.SceneDialogue(now.Sequence,6),old.Recall(7,now.Text),true,4096,160);
        Check(!prompt.Skip(1).Any(p=>p.content.Contains("scraper"))&&prompt.Last().content.Contains(now.Text),"A standalone clock question does not carry an unrelated previous scraper discussion into the request");
        Check(prompt[0].content.Contains("Only VERIFIED COMPLETED ACTIONS")&&prompt[0].content.Contains("rather than echoing"),"The prompt distinguishes proof of completed work and asks for an actual conversational response");
        Console.WriteLine("NPC completed-work checks PASS: "+checks);
    }
}

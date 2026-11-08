using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using WorldGen.Core;

static class NpcMindTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string what){if(!ok)throw new Exception(what);count++;}
        var promptQueue=new NpcEventQueue();promptQueue.Add(new NpcEventQueue.Event{Person=1,Priority=1},0);
        Check(promptQueue.Add(new NpcEventQueue.Event{Person=1,Priority=3},.1),"Direct speech bypasses passive-event cooldown");
        var q=new NpcEventQueue();
        Check(q.Take(0)==null,"Idle minds schedule no inference");
        Check(q.Add(new NpcEventQueue.Event{Person=1,Priority=1,Trigger="visitor"},0),"Special event admitted");
        Check(!q.Add(new NpcEventQueue.Event{Person=1,Priority=1},1),"Routine observations cannot churn");
        Check(q.Add(new NpcEventQueue.Event{Person=1,Priority=3,Words="hello"},2),"Direct speech supersedes observation");
        Check(q.Count==1&&q.Take(2).Words=="hello","Coalescing preserves newer speech");
        Check(!q.Add(new NpcEventQueue.Event{Person=1,Priority=3},2.1),"Speech spam limited");
        for(int i=2;i<12;i++)q.Add(new NpcEventQueue.Event{Person=i,Priority=1},3);
        Check(q.Count==8,"Bounded world-independent queue");
        Check(q.Add(new NpcEventQueue.Event{Person=20,Priority=3},4)&&q.Take(4).Person==20,"Speech priority over passive events");
        Check(q.Take(100)==null,"Stale events dropped");
        var decision=new NpcDecision{action="teleport",target="player"};
        Check(!decision.Validate(new[]{"player","none"}),"No arbitrary executor action");
        decision.action="approach";decision.target="person:500";Check(!decision.Validate(new[]{"player","none"}),"No unseen target");
        decision.target="none";Check(!decision.Validate(new[]{"player","none"}),"Approach needs actual target");
        decision.target="player";decision.seconds=int.MaxValue;decision.thought=new string('a',900);decision.speech=new string('b',900);
        Check(decision.Validate(new[]{"player","none"})&&decision.seconds==30&&decision.thought.Length==160&&decision.speech.Length==280,"Bounded valid decision");
        var state=new HumanLocalState();var memory=new NpcMemory{Person=20,Thought="I should rest.",Trigger="hurt"};state.Minds.Add(memory);
        for(int i=0;i<20;i++)memory.Remember("Memory "+i);
        Check(memory.Recent.Count==12&&memory.Recent.First()=="Memory 8","Bounded memories");
        var copy=state.Copy();copy.Minds[0].Remember("changed");Check(memory.Recent.Last()=="Memory 19","Independent save copy");state.Validate();
        var serializer=new DataContractSerializer(typeof(HumanLocalState));using(var stream=new MemoryStream())
        {serializer.WriteObject(stream,state);stream.Position=0;var loaded=(HumanLocalState)serializer.ReadObject(stream);loaded.Validate();Check(loaded.Minds[0].Thought==memory.Thought&&loaded.Minds[0].Recent.Count==12,"Memories survive serialization");}
        Console.WriteLine("NPC mind checks PASS: "+count);
    }
}

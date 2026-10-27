using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    // Focused native-player serialization check. It bypasses display options,
    // music, game saves and scene generation, so it can run without a GPU.
    IEnumerator CheckHomininPersistence()
    {
        int exit=0;var checks=new List<string>();
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add("PASS "+text);}
        string output=Argument("--worldgen-output","");
        try
        {
            Directory.CreateDirectory(output);string input=Argument("--worldgen-hominin-check","");byte[] original=File.ReadAllBytes(input);
            var world=input.EndsWith(".world",StringComparison.OrdinalIgnoreCase)?WorldFile.Read(original):WorldJson.ReadWorld(System.Text.Encoding.UTF8.GetString(original));
            var engine=new Prehistory(world);var initial=engine.Start(engine.PrepareHominins());
            Check(initial.DeepPast.Version==2&&initial.DeepPast.RulesVersion==3,"native player creates the current early-history schema and rules");
            Check(initial.DeepPast.Languages.Length==1,"native player retains the founding communication identity");
            var state=engine.AdvanceYears(initial,20000);engine.Validate(state);
            Check(state.DeepPast.Languages.Length==state.DeepPast.NextLanguage,"native player tracks communication branches");
            var clock=new PlaySession();clock.Accumulate(300);
            Check(!SimulationClockView.LocalHistoryEventDue(state,(long)clock.PendingHistorySeconds),"ordinary local play retains clock credit without requesting a demographic publication");
            Check(SimulationClockView.LocalPublication(state,3*SimulationTime.Year).Seconds==3*SimulationTime.Year,"leaving play can flush empty years in one native publication");
            Check(state.DeepPast.Traditions.All(t=>(t.Knowledge&LithicKnowledge.Pottery)==0),"early foundation does not grant late pottery");
            world.Prehistory=state;var restored=WorldFile.Read(WorldFile.Write(world));var resumed=new Prehistory(restored);
            Check(restored.Prehistory.Hash==state.Hash,"compact native save retains early-history canonical hash");
            Check(restored.Prehistory.DeepPast.RulesVersion==3,"compact native save retains the explicit rule revision");
            Check(resumed.AdvanceYears(restored.Prehistory,1000).Hash==engine.AdvanceYears(state,1000).Hash,"fresh native engine resumes the same populations and traditions");
            var play=new PlaySession{Survival=new SurvivalState()};play.Survival.Corpses=Enumerable.Range(0,8).Select(i=>SurvivalPhysiology.Carcass("deer",(ulong)i+1,new V(1,0,0),70,0)).ToArray();
            foreach(var corpse in play.Survival.Corpses)corpse.SettledPose=Enumerable.Repeat(.12345678f,2048).ToArray();
            string json=WorldJson.Write(play);File.WriteAllText(Path.Combine(output,"play-session.json"),json);
            Check(System.Text.Encoding.UTF8.GetByteCount(json)>65536,"native survival save exceeds the former camera-only limit");
            var loaded=WorldLibrary.ReadPlaySession(Path.Combine(output,"snapshot.world"));
            Check(loaded.Survival.Corpses.Length==8&&loaded.Survival.Corpses.Last().SettledPose.Length==2048,"actual native library loader retains multiple physics poses");
            using(var hash=SHA256.Create())Check(hash.ComputeHash(original).SequenceEqual(hash.ComputeHash(File.ReadAllBytes(input))),"input world file is unchanged");
            File.WriteAllLines(Path.Combine(output,"hominin-persistence-check.txt"),checks);Debug.Log("HOMININ PERSISTENCE PASS "+checks.Count);
        }
        catch(Exception error){Debug.LogError(error.ToString());exit=1;}
        Application.Quit(exit);yield break;
    }
}

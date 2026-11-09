using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class SpokenLine
    {
        public HumanActor Actor;public NpcEventQueue.Event Event;public ChatLine Chat;
        public readonly NpcSpeechPace Pace=new NpcSpeechPace();public bool Final,Delivered;public string Target="player";
        public AudioSource Voice;public double NextMumble;public int LastMumble;
    }
    readonly Dictionary<long,SpokenLine> npcSpeech=new Dictionary<long,SpokenLine>();
    readonly List<long> speechFinished=new List<long>();
    int npcSpeechFrame=-1;
    bool NpcSpeaking(long id)=>npcSpeech.TryGetValue(id,out var line)&&(!line.Final||!line.Pace.Complete);
    SpokenLine SpeechLine(HumanActor actor,NpcEventQueue.Event heard)
    {
        if(npcSpeech.TryGetValue(actor.ResidentId,out var line)&&line.Event==heard)return line;
        line=new SpokenLine{Actor=actor,Event=heard,Target=heard.Speaker>0?"person:"+heard.Speaker:"player",Chat=new ChatLine{Text="",At=Time.realtimeSinceStartupAsDouble}};
        npcSpeech[actor.ResidentId]=line;chatLines.Add(line.Chat);while(chatLines.Count>100)chatLines.RemoveAt(0);return line;
    }
    void UpdateNpcSpeech(bool stopped)
    {
        if(npcSpeechFrame==Time.frameCount)return;npcSpeechFrame=Time.frameCount;
        if(stopped){foreach(var line in npcSpeech.Values)if(line.Voice!=null)line.Voice.Pause();return;}
        if(npcPending!=null&&npcPending!=npcSuppressed&&npcThinking!=null&&livingPeople.TryGetValue(npcPending.Person,out var speaking)&&speaking.Health>0&&HumanDistance(speaking.Planet,Position)<(npcPending.Speaker>0?12:6)&&
            (npcPending.Speaker==0||livingPeople.TryGetValue(npcPending.Speaker,out var listener)&&HumanDistance(listener.Planet,speaking.Planet)<5))
        {
            string text=npcModel?.StreamedSpeech??"";
            if(text.Length>0){var line=SpeechLine(speaking,npcPending);line.Pace.Available=text;}
        }
        speechFinished.Clear();
        foreach(var pair in npcSpeech)
        {
            var line=pair.Value;
            if(line.Actor.Health<=0||!livingPeople.ContainsKey(pair.Key)||HumanDistance(line.Actor.Planet,Position)>22){speechFinished.Add(pair.Key);continue;}
            int previous=line.Pace.Shown;line.Pace.Advance(Math.Min(.1,Time.unscaledDeltaTime),npcModel?.Config.SpeechCharactersPerSecond??24);MumbleSpeech(line,previous);
            if(HumanDistance(line.Actor.Planet,Position)<12){line.Chat.Text="<"+line.Actor.Resident.Name+"> "+line.Pace.Visible;line.Chat.At=Time.realtimeSinceStartupAsDouble;}
            if(line.Final&&line.Pace.Complete&&!line.Delivered){line.Delivered=true;DeliverNpcSpeech(line.Actor,line.Event,line.Pace.Available,line.Target);speechFinished.Add(pair.Key);}
        }
        foreach(long id in speechFinished){RemoveSpeechVoice(npcSpeech[id]);npcSpeech.Remove(id);}
    }
    void FinishNpcSpeech(HumanActor actor,NpcEventQueue.Event heard,string words,string target)
    {
        var line=SpeechLine(actor,heard);line.Pace.Available=words;line.Final=true;line.Target=target;
    }
    void AbortNpcSpeech(NpcEventQueue.Event heard)
    {
        if(heard==null||!npcSpeech.TryGetValue(heard.Person,out var line)||line.Event!=heard)return;
        // Already displayed words were heard, even if the later decision failed.
        string spoken=line.Pace.Visible;if(spoken.Length>0)NpcMemoryFor(heard.Person).Say(NpcSpeakerKey(heard.Speaker),line.Actor.Resident.Name,spoken,heard.DialogueSequence);
        RemoveSpeechVoice(line);npcSpeech.Remove(heard.Person);
    }
}

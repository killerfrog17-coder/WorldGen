using System;
using System.Linq;
using UnityEngine.Playables;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    PlayCommandResult ExecuteSurvivalCommand(string[] words)
    {
        if(words.Length==0)return null;string command=words[0].ToLowerInvariant();
        if(command!="/survival"&&command!="/mode"&&command!="/give"&&command!="/heal")return null;
        if(survival==null)return new PlayCommandResult(false,"Enter the playable world first.");
        if(command=="/mode")
        {
            if(words.Length!=2||!Enum.TryParse(words[1],true,out SurvivalMode mode)||!Enum.IsDefined(typeof(SurvivalMode),mode))return new PlayCommandResult(false,"Usage: /mode survival|creative|spectator");
            survival.Mode=mode;motion.Flying=mode==SurvivalMode.Spectator;return new PlayCommandResult(true,"Mode: "+mode);
        }
        if(command=="/heal")
        {survival.Health=survival.Stamina=100;survival.WaterDeficitL=survival.Fatigue=survival.Bleeding=survival.Illness=0;survival.CoreTemperatureC=37;survival.Dead=false;survival.EnergyKcal=6000;return new PlayCommandResult(true,"Survival condition restored.");}
        if(command=="/give")
        {
            if(words.Length<2||words.Length>3)return new PlayCommandResult(false,"Usage: /give item-id [count], /give list");
            if(words[1]=="list")return new PlayCommandResult(true,string.Join(", ",SurvivalItems.All.Select(i=>i.Id)));
            int count=1;if(words.Length==3&&(!int.TryParse(words[2],out count)||count<1))return new PlayCommandResult(false,"Count must be a positive whole number.");
            if(!SurvivalItems.TryGet(words[1],out var item))return new PlayCommandResult(false,"Unknown item; /give list lists identifiers.");
            if(!survival.Inventory.TryAdd(item.Id,count,out var why))return new PlayCommandResult(false,why);
            return new PlayCommandResult(true,"Added "+count+" "+item.Name.ToLowerInvariant()+".");
        }
        if(words.Length>1&&words[1]=="inventory"){OpenSurvival();return new PlayCommandResult(true,"Equipment opened.");}
        return new PlayCommandResult(true,"Health "+survival.Health.ToString("F0")+"; stamina "+survival.Stamina.ToString("F0")+"; water deficit "+survival.WaterDeficitL.ToString("F1")+" L; energy "+survival.EnergyKcal.ToString("F0")+" kcal.","/survival inventory · /mode survival|creative|spectator · /give item-id [count] · /heal");
    }
    void TriggerHumanSurvivalAction(string role)
    {
        if(humanPlayer==null)return;
        string actual=role=="Attack"?(survival?.Inventory.Held==null?"Punch":"Sword attack"):role=="Sit"?"Sitting":role;
        int index=Array.IndexOf(humanPlayer.Definition.Roles,actual);if(index<0)return;
        humanPlayer.Action=index;humanPlayer.ActionRemaining=role=="Sit"?3600:role=="Dig"?2.5f:humanPlayer.Definition.Clips[index].length;
        humanPlayer.Inputs[index].SetTime(0);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Queue<(Possession item,int count)> portableGiveRequests=new Queue<(Possession,int)>();
    PlayCommandResult TryPortableGive(string[] words,int end,int count)
    {
        var tokens=words.Skip(1).Take(end-1).ToArray();int coat=0;
        foreach(var token in tokens.Where(t=>t.StartsWith("coat=",StringComparison.OrdinalIgnoreCase)))if(!int.TryParse(token.Substring(5),out coat)||coat<0||coat>4095)return new PlayCommandResult(false,"Use coat=0, coat=1, etc.");
        string name=string.Join(" ",tokens.Where(t=>!t.StartsWith("coat=",StringComparison.OrdinalIgnoreCase))),key=PortableTechnology.Key(name);
        if(key=="list")return new PlayCommandResult(true,new[]{"Portable items (/give <name> [count]); /give materials supplies raw test inputs:"}.Concat(PortableTechnology.Items.Select(p=>p.Name)).Concat(new[]{"Animal hides: /give deer hide [count], /give elk hide coat=1 [count]. Stones/branches remain available."}).ToArray());
        if(key=="materials")
        {
            foreach(var pair in new[]{(PossessionKind.RawFibre,12),(PossessionKind.BarkSheet,4),(PossessionKind.ReedBundle,5),(PossessionKind.RawHide,4),(PossessionKind.Bone,8),(PossessionKind.Antler,3),(PossessionKind.Sinew,4),(PossessionKind.Feather,4)})
                portableGiveRequests.Enqueue((PortableTechnology.Create(pair.Item1,Position,pair.Item1==PossessionKind.RawHide?"deer":""),pair.Item2));
            UpdatePortableGive();return new PlayCommandResult(true,"Giving raw crafting materials; overflow drops nearby. No starter bag, tools or completed products added.");
        }
        var definition=PortableTechnology.Named(name);string species="";
        if(definition==null)
        {
            string suffix=new[]{"hide","bone","antler"}.FirstOrDefault(s=>key.EndsWith(s,StringComparison.Ordinal)&&key.Length>s.Length);
            if(suffix==null)return null;string animal=key.Substring(0,key.Length-suffix.Length);
            var profile=animalProfiles.FirstOrDefault(p=>PortableTechnology.Key(p.slug)==animal||PortableTechnology.Key(p.name)==animal||p.aliases.Any(a=>PortableTechnology.Key(a)==animal));
            if(profile==null)return new PlayCommandResult(false,"Unknown animal material source. Try /give deer hide.");
            species=profile.slug;definition=PortableTechnology.Item(suffix=="hide"?PossessionKind.RawHide:suffix=="bone"?PossessionKind.Bone:PossessionKind.Antler);
        }
        if(string.IsNullOrEmpty(species)&&(definition.Form==PortableForm.Hide||definition.Name.StartsWith("Hide ")||definition.Kind==PossessionKind.RawhideStrips))species="deer";
        portableGiveRequests.Enqueue((PortableTechnology.Create(definition.Kind,Position,species,coat),count));UpdatePortableGive();return new PlayCommandResult(true,"Giving "+count+" "+definition.Name+".");
    }
    void UpdatePortableGive()
    {
        if(portableGiveRequests.Count==0)return;long start=System.Diagnostics.Stopwatch.GetTimestamp();InitializeInventory();
        while(portableGiveRequests.Count>0)
        {
            var request=portableGiveRequests.Dequeue();var item=request.item.Copy();item.Id="given-"+Guid.NewGuid().ToString("N");item.Position=Position;Inventory.Items.Add(item);
            var slot=PortableTechnology.Wear(item);
            if(!(slot.HasValue&&Inventory.Move(item.Id,slot.Value,"",0,0,false,out _))&&!Inventory.Hold(item.Id,out _)&&!Inventory.Stow(item.Id,out _))ReleasePossession(item,true);
            if(request.count>1)portableGiveRequests.Enqueue((request.item,request.count-1));
            if((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency>1.5)break;
        }
        inventoryNearbyAt=0;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    PlayCommandResult DescribeAllFlora(string[] words)
    {
        if(!Ready)return new PlayCommandResult(false,"Terrain is not ready yet.");
        var at=terrain.Patch(Position,100).Sample(Position);var r=terrain.FloraRegion(at.Cell);
        int trees=r.Trees.Length,mid=r.MidPlants.Length,total=trees+mid+r.Specialists.Length;
        if(words.Length==1)
        {
            var lines=new List<string>{"Regional flora catalogue - trees, understory, herbs and fungi. Potential species, not a visible census."};int n=0;
            lines.AddRange(r.Trees.Select(s=>(++n)+". "+s.Name+" - "+s.Form));
            lines.AddRange(r.MidPlants.Select(s=>(++n)+". "+s.Name+" - "+s.Form));
            lines.AddRange(r.Specialists.Select(s=>(++n)+". "+s.Name+" - "+s.Form));
            lines.Add("/flora <number> for details.");return new PlayCommandResult(true,lines.ToArray());
        }
        if(words.Length!=2||!int.TryParse(words[1],out int index)||index<1||index>total)return new PlayCommandResult(false,"Usage: /flora [1-"+total+"]");
        if(index<=trees)return DescribeTreeFlora(new[]{"/flora",index.ToString()});
        if(index<=trees+mid)return DescribeMidFlora(new[]{"/flora",(index-trees).ToString()});
        return DescribeSpecialistFlora(new[]{"/flora",(index-trees-mid).ToString()});
    }
    PlayCommandResult DescribeRegionalFauna(string[] words)
    {
        if(!Ready||sourceWorld?.Ecology==null)return new PlayCommandResult(false,"Regional ecology is not ready yet.");
        var e=sourceWorld.Ecology;int cell=terrain.NearestCell(Position),id=e.Region[cell];
        if(id<0)return new PlayCommandResult(true,"No regional terrestrial/freshwater fauna catalogue for open ocean.");
        var taxa=e.Regions[id].Species.Select(p=>e.Taxa[p.TaxonId]).Where(t=>!t.Producer&&!t.BasalResource).ToArray();
        if(words.Length==1)return new PlayCommandResult(true,new[]{"Regional fauna - * suitable at this location; other entries occur elsewhere in the region."}.Concat(taxa.Select((t,i)=>(i+1)+". "+(Biogeography.Present(e,cell,t.Id)?"* ":"")+t.Name)).Concat(new[]{"/fauna <number> for details. Includes audio-only and ecological taxa."}).ToArray());
        if(words.Length!=2||!int.TryParse(words[1],out int index)||index<1||index>taxa.Length)return new PlayCommandResult(false,"Usage: /fauna [1-"+taxa.Length+"]");
        var s=taxa[index-1];return new PlayCommandResult(true,s.Name+" - "+s.ScientificName,"Mass "+s.MassKg.ToString("G3")+" kg; strata "+s.Occupies+"; food "+s.Hunts,"Regional habitat potential, not a count of currently rendered animals.");
    }
    readonly Queue<(PossessionKind kind,int count,int variant,double length)> giveRequests=new Queue<(PossessionKind,int,int,double)>();
    PlayCommandResult ExecuteGive(string[] words)
    {
        if(!Ready||Spectating||DeathVisible)return new PlayCommandResult(false,"Use /give while alive and playing as a character.");
        int count=1,end=words.Length;
        if(end>2&&int.TryParse(words[end-1],out int quantity)){if(quantity<1)return new PlayCommandResult(false,"Count must be positive.");count=quantity;end--;}
        string key=new string(string.Join("",words.Skip(1).Take(end-1)).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        PossessionKind kind;int variant=3;double length=1.9;
        switch(key){case "chert":variant=ToolStone.Encode(ToolStoneMaterial.Chert,0);kind=PossessionKind.SmallRock;break;case "quartzite":variant=ToolStone.Encode(ToolStoneMaterial.Quartzite,0);kind=PossessionKind.SmallRock;break;case "shortbranch":length=.9;kind=PossessionKind.Branch;variant=0;break;case "branch":kind=PossessionKind.Branch;variant=0;break;case "pole":kind=PossessionKind.TrimmedPole;variant=0;break;case "diggingstick":length=.9;kind=PossessionKind.DiggingStick;variant=0;break;case "woodenspear":case "spear":kind=PossessionKind.WoodenSpear;variant=0;break;case "rock":case "stone":case "smallrock":kind=PossessionKind.SmallRock;break;case "largerock":kind=PossessionKind.LargeRock;break;case "flake":case "stoneflake":kind=PossessionKind.StoneFlake;break;case "bag":case "testbag":case "testingbag":kind=PossessionKind.TestBag;break;default:return new PlayCommandResult(false,"Usage: /give <small rock | chert | quartzite | stone flake | branch | short branch | pole | digging stick | wooden spear | bag> [count]");}
        InitializeInventory();giveRequests.Enqueue((kind,count,variant,length));UpdateGiveItems();return new PlayCommandResult(true,"Giving "+count+" "+kind+". Items use free hands/bag space; overflow drops nearby.");
    }
    void UpdateGiveItems()
    {
        if(giveRequests.Count==0)return;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        while(giveRequests.Count>0)
        {
            var request=giveRequests.Dequeue();bool bag=request.kind==PossessionKind.TestBag,large=request.kind==PossessionKind.LargeRock,flake=request.kind==PossessionKind.StoneFlake;
            var p=new Possession{Id="given-"+Guid.NewGuid().ToString("N"),Kind=request.kind,Place=PossessionPlace.Ground,Position=Position,Variant=request.variant,
                WidthM=bag?.6:large?.25:flake?.1:.1,HeightM=bag?.7:large?.16:flake?.01:.07,DepthM=bag?.22:large?.22:flake?.07:.086,
                MassKg=bag?.6:large?4:flake?.07:.7,CellsWide=bag?6:large?3:1,CellsHigh=bag?7:large?2:1,EdgeSharpness=flake?.8:0};
            if(WoodCrafting.Wood(p))
            {
                p.WidthM=p.DepthM=.03;p.HeightM=request.length;p.MassKg=System.Math.PI*.03*.03/4*p.HeightM*620;p.CellsWide=1;p.CellsHigh=(int)System.Math.Ceiling(p.HeightM/.1);p.WoodStraightness=1;p.WoodSoundness=.95;p.Tint=new V(1,1,1);p.GroundRotation=new V(0,heading,90);
            }
            else if(StoneCrafting.Rock(p))p.Tint=ToolStone.Color(p.Variant,p.Tint);
            Inventory.Items.Add(p);
            if(!(bag&&Inventory.Move(p.Id,PossessionPlace.Back,"",0,0,false,out _))&&!Inventory.Hold(p.Id,out _)&&!Inventory.Stow(p.Id,out _))ReleasePossession(p,true);
            if(request.count>1)giveRequests.Enqueue((request.kind,request.count-1,request.variant,request.length));
            if((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency>1.5)break;
        }
        inventoryNearbyAt=0;
    }
}

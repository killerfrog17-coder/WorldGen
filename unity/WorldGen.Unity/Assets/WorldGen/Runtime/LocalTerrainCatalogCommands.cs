using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NaturalLandmark {public string Name;public V Point,Focus;public double FocusM;}
    Task<NaturalLandmark> landmarkSearch;int landmarkGeneration;
    const string LandmarkUsage="/landmark volcano | waterfall | canyon | glacial | fjord | outcrop | dome | karst | shore | mangrove";
    PlayCommandResult ExecuteLandmark(string[] words)
    {
        if(words.Length!=2)return new PlayCommandResult(words.Length==1,LandmarkUsage);
        string kind=words[1].ToLowerInvariant();if(!new[]{"volcano","waterfall","canyon","glacial","fjord","outcrop","dome","karst","shore","mangrove"}.Contains(kind))return new PlayCommandResult(false,LandmarkUsage);
        if(!Ready||sourceWorld==null)return new PlayCommandResult(false,"Terrain is not ready yet.");
        if(landmarkSearch!=null)return new PlayCommandResult(false,"A landmark search is already running.");
        var model=terrain;var world=sourceWorld;double scale=HorizontalScale;landmarkGeneration=generation;
        landmarkSearch=Task.Run(()=>FindNaturalLandmark(model,world,scale,kind));
        return new PlayCommandResult(true,"Finding a generated "+kind+" example...");
    }
    static NaturalLandmark FindNaturalLandmark(LocalTerrain model,World world,double scale,string kind)
    {
        double radius=model.RadiusM*scale;
        NaturalLandmark Site(string name,V point,V focus,double? level=null)
        {
            var ground=model.Patch(point,2000/scale).Sample(point);if(ground.Underwater)return null;
            var target=model.Patch(focus,2000/scale).Sample(focus);
            return new NaturalLandmark{Name=name,Point=point,Focus=focus,FocusM=level??(target.Underwater?target.WaterSurfaceM:target.ElevationM)};
        }
        if(kind=="shore")foreach(var coast in CoastalSites.WavePlatforms(world,model))
        {
            var focus=(coast.Position-coast.Seaward*(70/radius)).Unit;
            var site=Site("Rocky wave-cut shore "+coast.Cell,coast.Position,focus);if(site!=null)return site;
        }
        if(kind=="mangrove")foreach(var coast in TidalForestSites.Nearshore(world,model))
        {
            var focus=(coast.Position+coast.Seaward*(20/radius)).Unit;
            var site=Site("Tidal mangrove forest "+coast.Cell,coast.Position,focus,0);if(site!=null)return site;
        }
        if(kind=="fjord")foreach(var form in model.Amplification.Fjords.Inlets.OrderByDescending(f=>f.LengthM*f.Relief))
        {
            var section=model.Amplification.Fjords.Sections[form.First+form.Count/2];V focus=(section.A+section.B).Unit,side=V.Cross(focus,section.B-section.A).Unit;
            foreach(int sign in new[]{1,-1})for(double r=.55;r<=1.25;r+=.025)
            {
                var point=(focus+side*(sign*form.HalfWidth*r/radius)).Unit;
                var ground=model.Patch(point,2000/scale).Sample(point);if(ground.Underwater||ground.ElevationM<.3||ground.ElevationM>120)continue;
                var site=Site("Glacial fjord "+form.Mouth,point,focus,0);if(site!=null)return site;
            }
        }
        if(kind=="volcano")foreach(var form in model.Amplification.Volcanoes.Forms.OrderBy(f=>f.AgeMyr).ThenByDescending(f=>f.HeightM).Take(128))
        {
            var rim=(form.Center+form.Axis*(form.RadiusM*form.CraterFraction*1.05/radius)).Unit;
            var site=Site(form.Form+" "+form.FeatureId,rim,form.Center);if(site!=null)return site;
        }
        if(kind=="outcrop"||kind=="dome")foreach(var form in model.Amplification.Remnants.Forms.Where(f=>f.Dome==(kind=="dome")).OrderByDescending(f=>f.HeightM).Take(128))
        {
            var point=(form.Center+form.Side*(form.RadiusZ*1.3/radius)).Unit;
            var site=Site((form.Dome?"Granite dome ":"Bedrock outcrop ")+form.Cell,point,form.Center);if(site!=null)return site;
        }
        if(kind=="glacial")foreach(var form in model.Amplification.Glaciers.Reaches.OrderByDescending(f=>f.Strength*f.Relief).Take(80))
        {
            V focus=(form.A+form.B).Unit,side=V.Cross(focus,form.B-form.A).Unit;
            var point=(focus+side*(form.HalfWidth*.35/radius)).Unit;
            var site=Site("Glacial valley "+form.Source,point,focus);if(site!=null)return site;
        }
        if(kind=="canyon")foreach(var form in model.Amplification.Canyons.Reaches.OrderByDescending(f=>f.Strength*f.Relief).Take(80))
        {
            V focus=(form.A+form.B).Unit,side=V.Cross(focus,form.B-form.A).Unit;
            var point=(focus+side*(form.HalfWidth*.9/radius)).Unit;
            var site=Site("Canyon "+form.Source,point,focus);if(site!=null)return site;
        }
        if(kind=="waterfall")foreach(var channel in model.Hydrology.Channels.Where(c=>WaterfallProfiles.CanHost(c.From,world.Recipe.Seed)&&c.WidthM>4&&c.WidthM<600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)>.02).GroupBy(c=>c.From).Select(g=>g.First()).OrderByDescending(c=>c.StartM-c.EndM).Take(128))
        foreach(var fall in model.Amplification.RiverReach(channel.From).Where(f=>f.Primitive==RiverFlowPrimitive.Waterfall))foreach(int sign in new[]{1,-1})
        {
            var point=(fall.B+fall.SideB*(sign*(fall.WidthB*2+10)/radius)).Unit;
            var site=Site("Waterfall "+channel.From,point,(fall.A+fall.B).Unit,(fall.LevelA+fall.LevelB)*.5);if(site!=null)return site;
        }
        if(kind=="karst")foreach(int cell in Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.Surface.Cells[i].Neighbors.All(n=>!world.Hydrology.OceanConnected[n])).Where(i=>model.Profile(i).Landscape.Moisture>.65&&model.Profile(i).Landscape.Glacial<.25).OrderByDescending(i=>model.Profile(i).Landscape.Karst).Take(64))
        {
            var controls=model.Profile(cell).Landscape;if(controls.Karst<.35)continue;
            V center=world.Surface.Cells[cell].Position,east=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit,north=V.Cross(center,east).Unit;
            foreach(int x in new[]{-900,-450,0,450,900})foreach(int z in new[]{-900,-450,0,450,900})
            {
                V at=(center+east*(x/radius)+north*(z/radius)).Unit;
                if(ErodedTerrain.HeightOffset(at*radius,world.Recipe.Seed,controls)<80)continue;
                var site=Site("Tower karst "+cell,(at+north*(400/radius)).Unit,at);if(site!=null)return site;
            }
        }
        return null;
    }
    void UpdateLandmarkSearch()
    {
        if(landmarkSearch==null||!landmarkSearch.IsCompleted)return;
        var search=landmarkSearch;landmarkSearch=null;
        if(landmarkGeneration!=generation||!Ready||search.IsCanceled)return;
        if(search.IsFaulted){PostExplorerChat("Landmark search: "+search.Exception.GetBaseException().Message,true);return;}
        var site=search.Result;if(site==null){PostExplorerChat("No suitable example found by this search.",true);return;}
        Teleport(site.Point);motion.Y=Ground(0,0)+TerrainMotion.EyeHeightM;motion.VerticalSpeed=0;motion.Grounded=!motion.Flying;survivalEnvironmentAt=0;
        TerrainHandoff.Coordinates(frame,site.Focus,out double x,out double z);double y=frame.SurfaceY(site.Focus,site.FocusM);
        heading=(float)(Math.Atan2(x,z)*180/Math.PI);pitch=(float)Math.Max(-65,Math.Min(65,-Math.Atan2(y-motion.Y,Math.Sqrt(x*x+z*z))*180/Math.PI));
        UpdateCamera();PostExplorerChat(site.Name+". Surrounding terrain is loading.");
    }
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
    readonly Queue<(PossessionKind kind,int count,int variant,double sizeM)> giveRequests=new Queue<(PossessionKind,int,int,double)>();
    PlayCommandResult ExecuteGive(string[] words)
    {
        if(!Ready||Spectating||DeathVisible)return new PlayCommandResult(false,"Use /give while alive and playing as a character.");
        int count=1,end=words.Length;
        if(end>2&&int.TryParse(words[end-1],out int quantity)){if(quantity<1)return new PlayCommandResult(false,"Count must be positive.");count=quantity;end--;}
        var portable=TryPortableGive(words,end,count);if(portable!=null)return portable;
        string key=new string(string.Join("",words.Skip(1).Take(end-1)).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        PossessionKind kind;int variant=3;double sizeM=1.9;
        switch(key){case "chert":variant=ToolStone.Encode(ToolStoneMaterial.Chert,0);kind=PossessionKind.SmallRock;break;case "quartzite":variant=ToolStone.Encode(ToolStoneMaterial.Quartzite,0);kind=PossessionKind.SmallRock;break;case "shortbranch":sizeM=.9;kind=PossessionKind.Branch;variant=0;break;case "branch":kind=PossessionKind.Branch;variant=0;break;case "pole":kind=PossessionKind.TrimmedPole;variant=0;break;case "diggingstick":sizeM=.9;kind=PossessionKind.DiggingStick;variant=0;break;case "woodenspear":case "spear":kind=PossessionKind.WoodenSpear;variant=0;break;case "pebble":sizeM=.04;kind=PossessionKind.SmallRock;break;case "rock":case "stone":case "smallrock":kind=PossessionKind.SmallRock;break;case "largerock":kind=PossessionKind.LargeRock;break;case "flake":case "stoneflake":kind=PossessionKind.StoneFlake;break;case "bag":case "testbag":case "testingbag":kind=PossessionKind.TestBag;break;default:return new PlayCommandResult(false,"Usage: /give <small rock | pebble | chert | quartzite | stone flake | branch | short branch | pole | digging stick | wooden spear | bag> [count]");}
        string label=kind==PossessionKind.SmallRock?(sizeM<.1?"pebble":"small rock"):kind==PossessionKind.LargeRock?"large rock":kind.ToString();
        if(count!=1&&(kind==PossessionKind.SmallRock||kind==PossessionKind.LargeRock))label+="s";
        InitializeInventory();giveRequests.Enqueue((kind,count,variant,sizeM));UpdateGiveItems();return new PlayCommandResult(true,"Giving "+count+" "+label+". Items use free hands/bag space; overflow drops nearby.");
    }
    void UpdateGiveItems()
    {
        UpdatePortableGive();if(giveRequests.Count==0)return;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        while(giveRequests.Count>0)
        {
            var request=giveRequests.Dequeue();bool bag=request.kind==PossessionKind.TestBag,large=request.kind==PossessionKind.LargeRock,flake=request.kind==PossessionKind.StoneFlake;
            var p=new Possession{Id="given-"+Guid.NewGuid().ToString("N"),Kind=request.kind,Place=PossessionPlace.Ground,Position=Position,Variant=request.variant,
                WidthM=bag?.6:large?.25:flake?.1:.1,HeightM=bag?.7:large?.16:flake?.01:.07,DepthM=bag?.22:large?.22:flake?.07:.086,
                MassKg=bag?.6:large?4:flake?.07:.7,CellsWide=bag?6:large?3:1,CellsHigh=bag?7:large?2:1,EdgeSharpness=flake?.8:0};
            if(WoodCrafting.Wood(p))
            {
                p.WidthM=p.DepthM=.03;p.HeightM=request.sizeM;p.MassKg=System.Math.PI*.03*.03/4*p.HeightM*620;p.CellsWide=1;p.CellsHigh=(int)System.Math.Ceiling(p.HeightM/.1);p.WoodStraightness=1;p.WoodSoundness=.95;p.Tint=new V(1,1,1);p.GroundRotation=new V(0,heading,90);
            }
            else if(StoneCrafting.Rock(p))
            {
                p.Tint=ToolStone.Color(p.Variant,p.Tint);
                if(request.sizeM<.1){double factor=request.sizeM/p.WidthM;p.WidthM*=factor;p.HeightM*=factor;p.DepthM*=factor;p.MassKg*=factor*factor*factor;}
            }
            Inventory.Items.Add(p);
            if(!(bag&&Inventory.Move(p.Id,PossessionPlace.Back,"",0,0,false,out _))&&!Inventory.Hold(p.Id,out _)&&!Inventory.Stow(p.Id,out _))ReleasePossession(p,true);
            if(request.count>1)giveRequests.Enqueue((request.kind,request.count-1,request.variant,request.sizeM));
            if((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency>1.5)break;
        }
        inventoryNearbyAt=0;
    }
}

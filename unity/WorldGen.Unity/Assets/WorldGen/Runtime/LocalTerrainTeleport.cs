using System;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Rng teleportRandom=new Rng(unchecked((int)DateTime.UtcNow.Ticks),"explorer-navigation-only");
    public PlayCommandResult ExecuteExplorerCommand(string text)
    {
        var words=text.Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        if(words.Length>0&&words[0].Equals("/seed",StringComparison.OrdinalIgnoreCase))
            return words.Length!=1?new PlayCommandResult(false,"Usage: /seed"):sourceWorld==null?new PlayCommandResult(false,"World seed is not available yet."):new PlayCommandResult(true,"World seed: "+sourceWorld.Recipe.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if(words.Length>0&&words[0].Equals("/flora",StringComparison.OrdinalIgnoreCase))return DescribeSpecialistFlora(words);
        if(words.Length>0&&words[0].Equals("/midflora",StringComparison.OrdinalIgnoreCase))return DescribeMidFlora(words);
        if(words.Length>0&&words[0].Equals("/trees",StringComparison.OrdinalIgnoreCase))return DescribeTreeFlora(words);
        if(words.Length==0||(!words[0].Equals("/tp",StringComparison.OrdinalIgnoreCase)&&!words[0].Equals("/teleport",StringComparison.OrdinalIgnoreCase)))return null;
        if(!Ready)return new PlayCommandResult(false,"Terrain is not ready yet.");
        V target=default;double? height=null;
        if(words.Length>=2&&words[1].Equals("random",StringComparison.OrdinalIgnoreCase))
        {
            bool land=words.Length==3&&words[2].Equals("land",StringComparison.OrdinalIgnoreCase);
            if(words.Length!=2&&!land)return new PlayCommandResult(false,ExplorerNavigation.TeleportHelp);
            bool found=false;
            for(int attempt=0;attempt<(land?512:1);attempt++)
            {
                target=ExplorerNavigation.RandomPoint(ref teleportRandom);
                if(land&&sourceWorld!=null&&sourceWorld.Hydrology.OceanConnected[terrain.NearestCell(target)])continue;
                var sample=terrain.Patch(target,2000).Sample(target);
                if(land&&sample.Underwater)continue;found=true;break;
            }
            if(!found)return new PlayCommandResult(false,"No dry land found in 512 attempts; no teleport performed.");
        }
        else if(!ExplorerNavigation.Coordinates(words,playState?.PrimeMeridianLongitude??0,out target,out height))
            return new PlayCommandResult(false,ExplorerNavigation.TeleportHelp,"Latitude -90..90, longitude -180..180; optional height 1.65..1000000 metres above ground/water.");
        double clearance=height??(motion.Flying?Math.Max(40,HeightAboveGround):TerrainMotion.EyeHeightM);
        var at=terrain.Patch(target,2000).Sample(target);Teleport(target);
        double waterRise=at.Underwater?at.WaterSurfaceM-at.ElevationM:0;
        motion.Y=Ground(0,0)+waterRise+clearance;
        if(clearance>TerrainMotion.EyeHeightM||waterRise>0)motion.Flying=true;
        motion.VerticalSpeed=0;UpdateCamera();
        TerrainMapViewport.Degrees(target,out double lat,out double lon);lon=playState?.OfficialLongitude(lon)??lon;
        return new PlayCommandResult(true,"Teleported to "+lat.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+", "+lon.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" (latitude, longitude).");
    }
    PlayCommandResult DescribeSpecialistFlora(string[] words)
    {
        if(!Ready)return new PlayCommandResult(false,"Terrain is not ready yet.");
        var sample=terrain.Patch(Position,100).Sample(Position);var region=terrain.FloraRegion(sample.Cell);var species=region.Specialists;
        if(words.Length==1)
        {
            var lines=new string[species.Length+2];lines[0]="Potential regional herb-layer catalogue · region "+region.RegionId+" · "+sample.Biome+" (not all species fit this exact spot).";
            for(int i=0;i<species.Length;i++)lines[i+1]=(i+1)+". "+species[i].Name+" · "+species[i].Form+" · "+species[i].HeightM.ToString("F2")+"m";
            lines[lines.Length-1]="/flora <1-"+species.Length+"> shows anatomy/niche. Fungi are not plants; these are derived potential taxa, not saved populations.";
            return new PlayCommandResult(true,lines);
        }
        if(words.Length!=2||!int.TryParse(words[1],out int index)||index<1||index>species.Length)return new PlayCommandResult(false,"Usage: /flora [1-"+species.Length+"]");
        var s=species[index-1];var m=s.Morphology;
        return new PlayCommandResult(true,s.Name+" · "+s.Form+" · founder "+s.Founder,
            "Height "+s.HeightM.ToString("F2")+"m; width "+s.WidthM.ToString("F2")+"m; "+(s.Form==SpecialistForm.Fungus?s.FungalRole.ToString():s.Form==SpecialistForm.Fern?m.Fronds+" fronds":m.Petals+" petals, "+m.Heads+" heads"),
            "Warm-season niche "+s.MinWarmC.ToString("F0")+"–"+s.MaxWarmC.ToString("F0")+"°C; aridity-index preference "+s.Moisture.ToString("F2")+"; shade "+(s.Shade*100).ToString("F0")+"%; pH "+s.MinPH.ToString("F1")+"–"+s.MaxPH.ToString("F1")+"; colony scale "+s.ColonyM.ToString("F0")+"m.",
            "Motion phenotype (relative): flexibility "+s.Motion.Flexibility.ToString("F2")+"; response speed "+s.Motion.Response.ToString("F2")+"; damping "+s.Motion.Damping.ToString("F2")+". Actual height also affects leverage/response.");
    }
}

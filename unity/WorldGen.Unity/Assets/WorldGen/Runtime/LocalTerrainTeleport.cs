using System;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Rng teleportRandom=new Rng(unchecked((int)DateTime.UtcNow.Ticks),"explorer-navigation-only");
    public PlayCommandResult ExecuteExplorerCommand(string text)
    {
        var words=text.Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
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
}

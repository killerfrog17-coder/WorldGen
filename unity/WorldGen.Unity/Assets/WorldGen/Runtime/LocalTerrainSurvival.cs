using System;
using System.Globalization;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    PlaySession transientPlayer;
    double survivalEnvironmentAt;
    SurvivalEnvironment survivalEnvironment;
    bool survivalSprinting;
    PlaySession PlayerRules=>playState??(transientPlayer??(transientPlayer=new PlaySession()));
    public GameMode Mode=>PlayerRules.Mode;
    public PlayerSurvival Vitals=>PlayerRules.Survival??(PlayerRules.Survival=new PlayerSurvival());
    public bool UnlimitedResources=>GameModeRules.UnlimitedResources(Mode);
    bool SurvivalActive=>GameModeRules.HasSurvivalNeeds(Mode);
    bool Spectating=>Mode==GameMode.Spectator;
    void ApplyPlayerMode()
    {
        motion.NoClip=GameModeRules.NoClip(Mode);
        if(Spectating)motion.Flying=true;
        else if(SurvivalActive)motion.Flying=false;
        if(Ready&&!Spectating)
        {
            double floor=Ground(motion.X,motion.Z)+TerrainMotion.EyeHeightM;
            if(motion.Y<floor)motion.Y=floor;
            motion.Grounded=!motion.Flying&&motion.Y<=floor+.01;
        }
        if(humanPlayer!=null)humanPlayer.Root.SetActive(GameModeRules.HasBody(Mode));
        survivalEnvironmentAt=0;motion.VerticalSpeed=0;
    }
    PlayCommandResult ExecuteGameMode(string[] words)
    {
        const string usage="Usage: /gamemode survival | creative | spectator";
        if(words.Length==1)
        {
            var s=Vitals;string Number(double n)=>n.ToString("F1",CultureInfo.InvariantCulture);
            return new PlayCommandResult(true,"Game mode: "+Mode.ToString().ToLowerInvariant()+".",
                "Health "+Number(s.Health)+"; stamina "+Number(s.Stamina)+"; energy "+Number(s.Energy)+"; hunger "+Number(s.Hunger)+"; thirst "+Number(s.Thirst)+"; body "+Number(s.CoreTemperatureC)+" C"+(s.Dead?"; dead: "+s.DeathCause:""),usage);
        }
        if(words.Length!=2||!SurvivalRules.TryMode(words[1],out var mode))return new PlayCommandResult(false,usage);
        PlayerRules.Mode=mode;
        if(mode==GameMode.Creative)Vitals.Restore();
        ApplyPlayerMode();if(Ready)UpdateCamera();
        return new PlayCommandResult(true,"Game mode: "+mode.ToString().ToLowerInvariant()+".");
    }
    void UpdatePlayerSurvival(double dx,double dz)
    {
        if(!SurvivalActive||paused||mapOpen||ecologyOpen||chatOpen||!Application.isFocused||!Ready)return;
        double dt=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));
        if(weatherProvider==null||dt<=0)return;
        if(Time.unscaledTimeAsDouble>=survivalEnvironmentAt)
        {
            survivalEnvironmentAt=Time.unscaledTimeAsDouble+.5;
            var at=Position;var sample=walkingPatch.Sample(at);
            // Ecology, not shadow/vegetation quality settings, determines shelter.
            double canopy=TreeFlora.CanopyShade(sample.Forest,at,terrain.SourceSeed,terrain.RadiusM*HorizontalScale);
            double feetAboveGround=motion.Y-TerrainMotion.EyeHeightM-Ground(motion.X,motion.Z);
            double water=sample.WaterSurfaceM;
            if(sample.Underwater&&double.IsNaN(water)&&sourceWorld.Hydrology.OceanConnected[sample.Cell])water=0;
            double daylight=SkyFrame?.Daylight??0;
            survivalEnvironment=new SurvivalEnvironment{
                AirTemperatureC=CurrentWeather.TemperatureC,Humidity=CurrentWeather.Humidity,
                WindMps=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps)*(1-canopy*.65),
                RainMmPerHour=CurrentWeather.RainMmPerHour*(1-canopy*.75),
                Sunlight=daylight*(1-CurrentWeather.CloudCover*.85)*(1-canopy*.8),
                Immersed=!double.IsNaN(water)&&water-(sample.ElevationM+feetAboveGround)>.6};
        }
        double speed=Math.Min(TerrainMotion.RunSpeedMps,Math.Sqrt(dx*dx+dz*dz)/dt);
        SurvivalRules.Advance(Vitals,dt,PlayerRules.Settings.RealMinutesPerDay,survivalEnvironment,speed,survivalSprinting);
    }
}

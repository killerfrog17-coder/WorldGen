using System;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;

static class PlayerWaterTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){n++;if(!ok)throw new Exception(text);}
        var stream=new LocalTerrainSample{WaterKind=LocalWaterKind.River,WaterSpeedMPerS=1.5,WaterSurfaceM=40};
        var fresh=WaterQuality.From(stream);var ocean=WaterQuality.From(new LocalTerrainSample{WaterKind=LocalWaterKind.Ocean});
        var pond=WaterQuality.From(new LocalTerrainSample{WaterKind=LocalWaterKind.Lake});
        Check(fresh.Fresh&&fresh.Contamination>0,"Moving clear freshwater is never guaranteed safe");
        Check(pond.Contamination>fresh.Contamination,"Stagnant freshwater has higher baseline risk");
        Check(!ocean.Fresh&&ocean.SalinityPpt==35,"Ocean water is saline");
        Check(!WaterQuality.From(new LocalTerrainSample{WaterKind=LocalWaterKind.Lake},null,50).Fresh,"Actual saline lake data overrides freshwater defaults");
        var state=new PlayerSurvival{Thirst=60};WaterDrinking.Drink(state,fresh,.25,1,GameMode.Survival);
        Check(state.Thirst<52&&state.WaterSicknessSeconds==0,"A clean sip restores hydration without inevitable sickness");
        double before=state.Thirst;WaterDrinking.Drink(state,ocean,.25,1,GameMode.Survival);Check(state.Thirst>before,"Seawater worsens dehydration");
        before=state.Thirst;WaterDrinking.Drink(state,ocean,.25,0,GameMode.Creative);Check(state.Thirst==before,"Creative water exposure has no survival consequences");
        WaterDrinking.Drink(state,fresh,.25,0,GameMode.Survival);Check(state.WaterSicknessDelaySeconds>3600&&state.WaterSicknessSeconds>0,"Contamination can cause delayed illness");
        var options=new JsonSerializerOptions{IncludeFields=true};var saved=JsonSerializer.Deserialize<PlayerSurvival>(JsonSerializer.Serialize(state,options),options);
        Check(saved.WaterSicknessSeconds==state.WaterSicknessSeconds&&saved.WaterSicknessDelaySeconds==state.WaterSicknessDelaySeconds,"Illness survives ordinary field serialization");
        before=state.Thirst;WaterDrinking.AdvanceIllness(state,3600);Check(state.Thirst==before,"Illness has an incubation period");
        WaterDrinking.AdvanceIllness(state,state.WaterSicknessDelaySeconds+3600);Check(state.Thirst>before&&state.Energy<100,"Active illness costs water and energy");
        foreach(double dt in new[]{1d/240,1d/60,1d/20,.25})
        {
            var m=new TerrainMotion{Y=.5,Water=(x,z)=>new MotionWater(0)};
            for(double t=0;t<8;t+=dt)m.Move(dt,0,0,0,0,0,false,false,(x,z)=>-6);
            Check(m.Swimming&&Math.Abs(m.Y-.12)<.01,"Buoyant eyes settle above the surface at "+dt);
            m.Move(.25,1,0,0,0,0,false,false,(x,z)=>-6);Check(m.Z>.3&&Math.Abs(m.Y-.12)<.01,"Horizontal swimming retains buoyancy");
            for(double t=0;t<3;t+=dt)m.Move(dt,0,0,-1,0,0,false,false,(x,z)=>-6);
            Check(m.Y< -2&&m.Y> -6+TerrainMotion.EyeHeightM-.001,"Dive descends and respects the bed");
            for(double t=0;t<8;t+=dt)m.Move(dt,0,0,1,0,0,false,false,(x,z)=>-6);
            Check(m.Y<.2&&m.Y>0,"Ascending cannot fly out of water");
            m.Place(0,0,0);Check(!m.Swimming,"Teleport clears stale swimming state");
        }
        var current=new TerrainMotion{Y=0,Water=(x,z)=>new MotionWater(0,1,0)};
        for(int i=0;i<60;i++)current.Move(1d/60,0,0,0,0,0,false,false,(x,z)=>-6);
        Check(Math.Abs(current.X-1)<1e-7,"Actual water current carries a passive swimmer");
        var wade=new TerrainMotion{Water=(x,z)=>new MotionWater(.5)};wade.Place(0,0,0);wade.Move(.1,1,0,0,0,0,false,false,(x,z)=>0);
        Check(!wade.Swimming&&wade.Grounded,"Shallow water remains walkable");
        var flight=new TerrainMotion{Flying=true,NoClip=true,Y=-4,Water=(x,z)=>new MotionWater(0)};flight.Move(.1,0,0,1,0,0,false,false,(x,z)=>-6);
        Check(!flight.Swimming&&flight.Y>0,"Spectator flight bypasses buoyancy");
        var block=new TerrainMotion{Y=0,Water=(x,z)=>new MotionWater(0)};
        for(int i=0;i<180;i++)block.Move(1d/60,1,0,0,0,0,false,false,(x,z)=>z>.3?4:-6);
        Check(block.Z<=.301&&block.Swimming,"A swimmer cannot climb an abrupt high bank");
        Check(Math.Abs(WaterSurface.Swell(13,27,0,4,2,0,true))<1e-12,"Swell displacement vanishes at the shoreline");
        Check(Math.Abs(WaterSurface.Swell(13,27,0,4,2,5,true)-WaterSurface.Swell(13,27,1,4,2,5,true))>.001,"Ocean surface evolves continuously with time");
        Console.WriteLine("PASS "+n+" drinking, water quality, saved illness and swimming checks");
    }
}

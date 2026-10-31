using System;
using System.Linq;
using System.Text.Json.Nodes;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class SurvivalModeTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        void Reject(Action action,string message){bool failed=false;try{action();}catch(ArgumentException){failed=true;}Check(failed,message);}
        var session=new PlaySession();session.Validate();
        Check(session.Mode==GameMode.Creative&&session.Survival.Health==100,"New sessions retain previous creative sandbox behavior");
        foreach(var mode in new[]{GameMode.Creative,GameMode.Survival,GameMode.Spectator})
        {
            session.Mode=mode;session.Survival.Thirst=42;session.Survival.Energy=63;
            var restored=WorldJson.Read<PlaySession>(WorldJson.Write(session));restored.Validate();
            Check(restored.Mode==mode&&restored.Survival.Thirst==42&&restored.Survival.Energy==63,"Mode and needs round trip: "+mode);
            var copy=session.Copy();copy.Survival.Health=1;Check(session.Survival.Health==100,"Copied needs are independent: "+mode);
        }
        var legacy=JsonNode.Parse(WorldJson.Write(session));legacy.AsObject().Remove("Mode");legacy.AsObject().Remove("Survival");
        var old=WorldJson.Read<PlaySession>(legacy.ToJsonString());old.Validate();
        Check(old.Mode==GameMode.Creative&&old.Survival.Health==100&&old.Survival.CoreTemperatureC==37,"Missing old-save fields receive healthy creative defaults");
        new PlaySession{Mode=GameMode.Spectator,HeightAboveGround=-100}.Validate();checks++;
        Reject(()=>new PlaySession{Mode=GameMode.Survival,HeightAboveGround=-1}.Validate(),"Only spectators can save below terrain");
        Reject(()=>new PlaySession{Mode=(GameMode)99}.Validate(),"Unknown saved mode rejected");
        foreach(double bad in new[]{double.NaN,double.PositiveInfinity,-1,101})Reject(()=>new PlayerSurvival{Health=bad}.Validate(),"Corrupt health rejected");
        Reject(()=>new PlayerSurvival{CoreTemperatureC=50}.Validate(),"Corrupt body temperature rejected");
        Check(GameModeRules.CanFly(GameMode.Creative)&&GameModeRules.UnlimitedResources(GameMode.Creative)&&!GameModeRules.HasSurvivalNeeds(GameMode.Creative),"Creative capabilities");
        Check(GameModeRules.NoClip(GameMode.Spectator)&&!GameModeRules.HasBody(GameMode.Spectator)&&!GameModeRules.HasSurvivalNeeds(GameMode.Spectator),"Spectator capabilities");
        Check(!GameModeRules.CanFly(GameMode.Survival)&&GameModeRules.HasBody(GameMode.Survival)&&GameModeRules.HasSurvivalNeeds(GameMode.Survival)&&!GameModeRules.UnlimitedResources(GameMode.Survival),"Survival capabilities");
        foreach(string name in new[]{"survival","creative","spectator","SURVIVAL"})Check(SurvivalRules.TryMode(name,out _),"Named modes accepted");
        foreach(string name in new[]{"0","1","2","nonsense","survival extra"})Check(!SurvivalRules.TryMode(name,out _),"Non-command mode values rejected");
        Check(PlayCommands.Complete("/gamemode ").Length==3&&PlayCommands.Complete("/gamemode s").Contains("/gamemode spectator"),"Mode completion is command-only");
        var motion=new TerrainMotion{Flying=true,NoClip=true,Y=10};
        motion.Move(.1,0,0,-1,0,0,false,false,(x,z)=>100);
        Check(motion.Y<10,"Spectator flight passes through terrain without snapping upward");
        motion.NoClip=false;motion.Move(.1,0,0,-1,0,0,false,false,(x,z)=>100);
        Check(motion.Y>=100+TerrainMotion.EyeHeightM,"Creative flight retains ground collision");
        var mild=new SurvivalEnvironment{AirTemperatureC=24,Humidity=.5};
        var resting=new PlayerSurvival();SurvivalRules.Advance(resting,600,60,mild,0,false);
        Check(resting.Health==100&&Math.Abs(resting.CoreTemperatureC-37)<.001,"Temperate rest is safe");
        Check(resting.Hunger>0&&resting.Thirst>0,"Food and water needs advance with local day duration");
        var a=new PlayerSurvival();var b=new PlayerSurvival();
        SurvivalRules.Advance(a,60,60,mild,3.4,false);
        for(int i=0;i<600;i++)SurvivalRules.Advance(b,.1,60,mild,3.4,false);
        Check(Math.Abs(a.Thirst-b.Thirst)<.001&&Math.Abs(a.CoreTemperatureC-b.CoreTemperatureC)<.001&&Math.Abs(a.Energy-b.Energy)<.001,"Frame subdivisions do not materially change needs");
        var sprint=new PlayerSurvival();double duration=0;
        while(!sprint.Exhausted&&duration<600){SurvivalRules.Advance(sprint,.1,60,mild,6,true);duration+=.1;}
        Check(sprint.Exhausted&&sprint.CanSprint&&sprint.SprintPower<.65&&duration>180&&duration<500,"Sustained hard running takes minutes to exhaust and fades to reduced available power");
        SurvivalRules.Advance(sprint,3,60,mild,0,false);
        Check(sprint.CanSprint&&sprint.Stamina<25,"A few seconds of rest cannot reset an exhausted sprint");
        var blocked=new PlayerSurvival();SurvivalRules.Advance(blocked,10,60,mild,0,true);
        Check(blocked.Stamina==100,"Holding sprint while stationary consumes no sprint stamina");
        var tired=new PlayerSurvival{Energy=10,Stamina=0,Exhausted=true};SurvivalRules.Advance(tired,10,60,mild,0,false);
        Check(tired.Stamina<=tired.MaximumStamina&&tired.MaximumStamina<50,"Long-term energy limits available stamina");
        var desert=new SurvivalEnvironment{AirTemperatureC=45,Humidity=.15,Sunlight=1,WindMps=2};
        var idleHot=new PlayerSurvival();var runningHot=new PlayerSurvival();
        SurvivalRules.Advance(idleHot,300,60,desert,0,false);SurvivalRules.Advance(runningHot,300,60,desert,6,true);
        Check(runningHot.Thirst>idleHot.Thirst&&runningHot.CoreTemperatureC>idleHot.CoreTemperatureC,"Desert exertion increases thirst and retained heat");
        var warm=new PlayerSurvival();SurvivalRules.Advance(warm,120,60,new SurvivalEnvironment{AirTemperatureC=30,Humidity=.4},0,false);
        Check(warm.Health==100,"Warm air does not directly drain health");
        var humid=new PlayerSurvival();SurvivalRules.Advance(humid,300,60,new SurvivalEnvironment{AirTemperatureC=45,Humidity=.95,Sunlight=1},6,true);
        Check(humid.CoreTemperatureC>runningHot.CoreTemperatureC,"Humidity hinders evaporative cooling");
        var shaded=new PlayerSurvival();desert.Sunlight=0;SurvivalRules.Advance(shaded,300,60,desert,0,false);
        Check(shaded.CoreTemperatureC<idleHot.CoreTemperatureC,"Shade reduces solar heat load");
        var cold=new SurvivalEnvironment{AirTemperatureC=-25,Humidity=.5,WindMps=8};
        var polar=new PlayerSurvival();SurvivalRules.Advance(polar,600,60,cold,0,false);
        Check(polar.Dead&&polar.DeathCause==PlayerDeathCause.Cold,"Unprotected polar exposure can kill");
        var coldRest=new PlayerSurvival();var coldRun=new PlayerSurvival();
        SurvivalRules.Advance(coldRest,60,60,cold,0,false);SurvivalRules.Advance(coldRun,60,60,cold,6,true);
        Check(coldRun.CoreTemperatureC>coldRest.CoreTemperatureC,"Activity generates protective heat in cold conditions");
        var dry=new PlayerSurvival();var wet=new PlayerSurvival();
        var cool=new SurvivalEnvironment{AirTemperatureC=5,Humidity=.5,WindMps=10};
        SurvivalRules.Advance(dry,120,60,cool,0,false);cool.RainMmPerHour=20;SurvivalRules.Advance(wet,120,60,cool,0,false);
        Check(wet.Wetness>dry.Wetness&&wet.CoreTemperatureC<dry.CoreTemperatureC,"Wind and wetness worsen cold exposure");
        var immersed=new PlayerSurvival();cool.Immersed=true;SurvivalRules.Advance(immersed,120,60,cool,0,false);
        Check(immersed.Wetness==1&&immersed.CoreTemperatureC<wet.CoreTemperatureC,"Cold water immersion is more severe than rain");
        var hungry=new PlayerSurvival{Hunger=100};var thirsty=new PlayerSurvival{Thirst=100};
        SurvivalRules.Advance(hungry,60,60,mild,0,false);SurvivalRules.Advance(thirsty,60,60,mild,0,false);
        Check(hungry.Health==100&&hungry.ReserveKcal>0&&thirsty.WaterDeficitLitres>0,"An empty stomach is not starvation; actual water deficit accumulates independently of thirst sensation");
        var dead=polar.Copy();SurvivalRules.Advance(polar,100,60,mild,0,false);
        Check(polar.Health==0&&polar.Energy==dead.Energy&&polar.CoreTemperatureC==dead.CoreTemperatureC&&polar.MovementScale==0,"Death stops survival updates and voluntary movement");
        polar.Restore();Check(polar.Health==100&&polar.Stamina==100&&polar.DeathCause==PlayerDeathCause.None,"Creative reset can recover a dead prototype player");
        var fastDay=new PlayerSurvival();var normalDay=new PlayerSurvival();
        SurvivalRules.Advance(fastDay,10,30,mild,0,false);SurvivalRules.Advance(normalDay,10,60,mild,0,false);
        Check(Math.Abs(fastDay.Hunger-normalDay.Hunger*2)<.001,"Needs follow solar-day duration without historical calendar multipliers");
        foreach(var s in new[]{resting,a,b,sprint,tired,idleHot,runningHot,humid,shaded,polar,coldRest,coldRun,dry,wet,immersed,hungry,thirsty}){s.Validate();checks++;}
        Console.WriteLine("PASS "+checks+" survival/mode/save/movement assertions; sprint exhaustion "+duration.ToString("F1")+"s.");
    }
}

using System;
using System.Linq;
using System.Text.Json.Nodes;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WaterSourceTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){n++;if(!ok)throw new Exception(text);}
        var stream=new WaterQuality(LocalWaterKind.River,.08,.2,.05);
        var p=new V(.8,.6,0);var environment=new WaterSourceEnvironment(.15,runoff:.2);
        double At(int seed,double day,WaterSourceEnvironment e)=>WaterContamination.Hazard(seed,45,p,637100,day,e);
        var first=At(321,7.2,environment);
        Check(first==At(321,7.2,environment),"The same source and time are deterministic without visitation state");
        Check(Enumerable.Range(0,100).Select(i=>At(i,7.2,environment)).Distinct().Count()>20,"Sources differ while many sites can have no contamination at all");
        for(int seed=0;seed<50;seed++)
        {
            double low=At(seed,7.2,new WaterSourceEnvironment()),busy=At(seed,7.2,new WaterSourceEnvironment(1)),runoff=At(seed,7.2,new WaterSourceEnvironment(1,runoff:1));
            Check(busy>=low&&runoff>=busy,"Animal pressure and runoff affect existing contamination exposure, seed "+seed);
            Check(At(seed,7.2,new WaterSourceEnvironment(1,protection:1))<=busy*.021,"Protected-source pathway suppresses exposure, seed "+seed);
        }
        double waste=At(123,8,new WaterSourceEnvironment(humanWaste:1));
        Check(waste>At(123,8,new WaterSourceEnvironment()),"Actual waste input has a separate strong contamination pathway");
        var catchment=new World{Surface=new Surface{Cells=new[]{new Cell{Neighbors=new[]{1,2}},new Cell{Neighbors=new[]{0}},new Cell{Neighbors=new[]{0}}}},
            Hydrology=new HydrologyResult{Receiver=new[]{-1,0,-1},ContributingAreaM2=new long[]{100,90,1000}},
            Ecology=new EcologyResult{Habitat=new[]{new HabitatCell(),new HabitatCell{ProductivityGCM2Year=2200,Habitat=Habitat.Wetland},new HabitatCell{ProductivityGCM2Year=2200,Habitat=Habitat.Wetland}}}};
        double upstreamUse=WaterContamination.CatchmentAnimalUse(catchment,0);
        Check(upstreamUse>0,"Productive upstream habitat contributes without any rendered animal objects");
        catchment.Ecology.Habitat[2]=new HabitatCell();
        Check(WaterContamination.CatchmentAnimalUse(catchment,0)==upstreamUse,"A neighbouring catchment that does not drain into the source has no upstream effect");
        catchment.Hydrology.Receiver[1]=-1;
        Check(WaterContamination.CatchmentAnimalUse(catchment,0)==0,"Disconnected productive habitat is not falsely upstream of the source");
        double cleanMud=WaterQuality.From(new LocalTerrainSample{WaterKind=LocalWaterKind.River,Sediment=1,WaterTurbulence=1}).Contamination;
        Check(cleanMud==0,"Sediment and turbulence are never themselves infectious material");
        double greatestDelta=0,timeChange=0;
        for(int seed=0;seed<100;seed++)for(int day=1;day<20;day++)
        {
            double a=At(seed,day-1e-6,environment),b=At(seed,day+1e-6,environment);
            greatestDelta=Math.Max(greatestDelta,Math.Abs(a-b));timeChange=Math.Max(timeChange,Math.Abs(a-At(seed,day+.5,environment)));
        }
        Check(greatestDelta<.00001&&timeChange>.0001,"Multi-day contamination evolves smoothly rather than rerolling at midnight");
        double close=WaterContamination.Hazard(321,45,(p+new V(0,0,1e-10)).Unit,637100,7.2,environment);
        Check(Math.Abs(close-first)<.00001,"Tiny aim movements do not replace the source condition");
        var one=new PlayerSurvival();one.Restore();var split=one.Copy();
        WaterDrinking.Drink(one,stream,1,.3,GameMode.Survival);
        for(int i=0;i<4;i++)WaterDrinking.Drink(split,stream,.25,.3+i*.1,GameMode.Survival);
        Check(one.WaterExposureHazard==split.WaterExposureHazard&&one.WaterExposureThreshold==split.WaterExposureThreshold&&one.WaterExposureResponse==split.WaterExposureResponse&&one.WaterExposureSequence==split.WaterExposureSequence,"Subdividing one litre cannot reroll susceptibility or change the dose");
        var saved=WorldJson.Read<PlayerSurvival>(WorldJson.Write(one));saved.Validate();
        WaterDrinking.Drink(saved,stream,1,.99,GameMode.Survival);WaterDrinking.Drink(one,stream,1,0,GameMode.Survival);
        Check(saved.WaterSicknessSeverity==one.WaterSicknessSeverity&&saved.WaterSicknessSeconds==one.WaterSicknessSeconds&&saved.WaterSicknessDelaySeconds==one.WaterSicknessDelaySeconds,"Save/reload and a changed sip random draw cannot reroll an existing exposure");
        var large=new PlayerSurvival();large.Restore();var small=large.Copy();
        WaterDrinking.Drink(large,stream,4,.4,GameMode.Survival);for(int i=0;i<16;i++)WaterDrinking.Drink(small,stream,.25,.4,GameMode.Survival);
        Check(large.WaterSicknessSeconds>0&&large.WaterSicknessSeconds==small.WaterSicknessSeconds&&large.WaterSicknessSeverity==small.WaterSicknessSeverity,"One larger drink and sixteen small drinks yield the same infection and course");
        double delay=large.WaterSicknessDelaySeconds,duration=large.WaterSicknessSeconds,severity=large.WaterSicknessSeverity;
        for(int i=0;i<100;i++)WaterDrinking.Drink(large,stream,.25,0,GameMode.Survival);
        Check(large.WaterSicknessDelaySeconds==delay&&large.WaterSicknessSeconds==duration&&large.WaterSicknessSeverity==severity,"Extra drinking does not stack or restart the same illness");
        Check(SurvivalHudValues.From(large,22).Sickness==0,"Exposure and incubation reveal no hidden source contamination in the HUD");
        WaterDrinking.AdvanceIllness(split,86400);Check(split.WaterExposureThreshold==0&&split.WaterExposureHazard==0,"Unestablished exposure clears after its physiological-day window");
        var old=JsonNode.Parse(WorldJson.Write(new PlayerSurvival())).AsObject();
        foreach(string field in new[]{"WaterExposureHazard","WaterExposureThreshold","WaterExposureResponse","WaterExposureAgeSeconds","WaterExposureSequence","WaterSicknessTotalSeconds"})old.Remove(field);
        var legacy=WorldJson.Read<PlayerSurvival>(old.ToJsonString());legacy.Validate();Check(legacy.WaterExposureHazard==0&&legacy.WaterExposureThreshold==0,"Older saves missing all new optional fields remain valid");
        foreach(double s in new[]{0d,.1,.45,.9})
        {
            var sick=new PlayerSurvival();sick.Restore();sick.WaterSicknessDelaySeconds=12*3600;sick.WaterSicknessSeconds=sick.WaterSicknessTotalSeconds=48*3600;sick.WaterSicknessSeverity=s;
            var divided=sick.Copy();double water=sick.WaterDeficitLitres,energy=sick.Energy;
            WaterDrinking.AdvanceIllness(sick,13*3600);for(int i=0;i<13*60;i++)WaterDrinking.AdvanceIllness(divided,60);
            Check(Math.Abs(sick.WaterDeficitLitres-divided.WaterDeficitLitres)<1e-9&&Math.Abs(sick.Energy-divided.Energy)<1e-8,"Illness costs agree across large and small time steps at severity "+s);
            Check(WaterDrinking.SymptomSeverity(sick)<=s/3+1e-9,"Symptoms develop gradually rather than becoming fully severe at onset");
            if(s==0)Check(water==sick.WaterDeficitLitres&&energy==sick.Energy&&SurvivalHudValues.From(sick,22).Sickness==0,"Asymptomatic infection has no tint or hidden gameplay penalty");
            if(s==.1)Check(sick.WaterDeficitLitres<.01&&SurvivalHudValues.From(sick,22).Sickness<.04,"Mild symptoms cause only a faint tint and negligible early fluid loss");
            var roundtrip=WorldJson.Read<PlaySession>(WorldJson.Write(new PlaySession{Survival=sick.Copy()}));roundtrip.Validate();
            Check(roundtrip.Survival.WaterSicknessTotalSeconds==sick.WaterSicknessTotalSeconds&&roundtrip.Survival.WaterSicknessSeconds==sick.WaterSicknessSeconds,"New illness course survives the actual play-session serializer");
            WaterDrinking.AdvanceIllness(sick,10*86400);Check(sick.WaterSicknessSeconds==0&&sick.WaterSicknessSeverity==0&&SurvivalHudValues.From(sick,22).Sickness==0,"Illness resolves fully, including a large sleep/time step");
        }
        var baseline=new PlayerSurvival();baseline.Restore();baseline.WaterDeficitLitres=2;
        for(int i=0;i<1000;i++)WaterDrinking.Drink(baseline,stream.WithContamination(0),.25,0,GameMode.Survival);
        Check(baseline.WaterSicknessSeconds==0&&baseline.WaterExposureThreshold==0,"A genuinely uncontaminated source never becomes poisonous through repeated drinking");
        var creative=new PlayerSurvival();creative.Restore();WaterDrinking.Drink(creative,stream,4,0,GameMode.Creative);
        Check(creative.WaterExposureSequence==0&&creative.WaterSicknessSeconds==0,"Creative drinking never initializes disease state");
        var dry=new PlayerSurvival();dry.Restore();dry.Health=0;WaterDrinking.Drink(dry,stream,1,0,GameMode.Survival);
        Check(dry.WaterExposureThreshold==0,"Dead players cannot drink or start infection");
        var courses=new int[4];
        for(int i=0;i<1000;i++)
        {
            var s=new PlayerSurvival();s.Restore();s.WaterExposureThreshold=.01;s.WaterExposureResponse=(i+.5)/1000d;
            WaterDrinking.Drink(s,stream,1,.99,GameMode.Survival);
            courses[s.WaterSicknessSeverity==0?0:s.WaterSicknessSeverity<.2?1:s.WaterSicknessSeverity<.6?2:3]++;
        }
        Check(courses.SequenceEqual(new[]{550,300,120,30}),"Explicit gameplay course distribution favours unnoticed and mild infections");
        foreach(double minutes in new[]{15d,60,1440})
        {
            var s=new PlayerSurvival();s.Restore();s.WaterSicknessSeconds=s.WaterSicknessTotalSeconds=48*3600;s.WaterSicknessDelaySeconds=86400;s.WaterSicknessSeverity=.1;
            var env=new SurvivalEnvironment{AirTemperatureC=22,Humidity=.4};
            SurvivalRules.Advance(s,minutes*60/24,minutes,env,0,false);
            Check(Math.Abs(s.WaterSicknessDelaySeconds-23*3600)<.001&&SurvivalHudValues.From(s,22).Sickness==0,"One solar hour advances incubation at day length "+minutes);
        }
        // A fixed distribution sample guards against returning to unavoidable
        // daily disease. These thresholds are gameplay checks, not medical rates.
        int infected=0,noticeable=0;
        for(int seed=0;seed<500;seed++)
        {
            var s=new PlayerSurvival();s.Restore();bool any=false,seen=false;
            for(int day=0;day<30;day++)
            {
                double hazard=At(seed,day+.3,new WaterSourceEnvironment(.15));
                for(int drink=0;drink<8;drink++)WaterDrinking.Drink(s,stream.WithContamination(hazard),.25,Rng.Value(seed,"player-water-exposure",s.WaterExposureSequence),GameMode.Survival);
                any|=s.WaterSicknessSeconds>0;seen|=s.WaterSicknessSeverity>0;WaterDrinking.AdvanceIllness(s,86400);
            }
            if(any)infected++;if(seen)noticeable++;
        }
        Console.WriteLine("Source calibration: 500 varied sites, 30 solar days, 2 L/day: "+infected+" infected, "+noticeable+" with any symptoms (gameplay model).");
        Check(noticeable<125,"Most low-pressure sources support sustained drinking without a symptomatic episode every month");
        Console.WriteLine("PASS "+n+" persistent source, dose, symptom, recovery, save and timescale checks");
    }
}

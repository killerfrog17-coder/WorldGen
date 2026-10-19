using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Reflection;

static class AtmosphereTests
{
    public static void Run(Action<bool,string> check)
    {
        var earth=CelestialSettings.Preset("Earth-like");var moon=earth.Moons[0];
        var tangentSun=PlayableSky.Sample(0,18.1,.218,earth);var curvedSun=PlayableSky.Sample(0,18.1,.218,earth,horizonDipDegrees:4);
        check(curvedSun.Bodies[0].Direction.Equals(tangentSun.Bodies[0].Direction)&&curvedSun.SunAltitudeDegrees==tangentSun.SunAltitudeDegrees,"Curved horizon lighting never moves the astronomical sun");
        check(curvedSun.Bodies[0].Illuminance>tangentSun.Bodies[0].Illuminance&&curvedSun.Daylight>tangentSun.Daylight,"An elevated curved horizon retains light after the sun crosses the local tangent");
        check(moon.OrbitalPeriodDays>27&&moon.OrbitalPeriodDays<28&&moon.SynodicPeriodDays>29&&moon.SynodicPeriodDays<30,"Earth-size/distance moon has Keplerian sidereal and synodic periods, not an arbitrary phase timer");
        var farther=moon.Copy();farther.OrbitRadiusKm*=1.5;check(Math.Abs(farther.OrbitalPeriodDays/moon.OrbitalPeriodDays-Math.Pow(1.5,1.5))<1e-9,"Orbital periods obey Kepler's third law within explicit nanoday quantization");
        foreach(string preset in CelestialSettings.Presets)
        {
            var sky=CelestialSettings.Preset(preset);sky.Validate();var cold=WorldJson.Read<CelestialSettings>(WorldJson.Write(sky));cold.Validate();check(StateHash.Compute(sky)==StateHash.Compute(cold),"Sky preset cold-roundtrips: "+preset);
            for(int day=0;day<365;day+=11)foreach(double hour in new[]{0d,6,12,18})
            {
                var f=PlayableSky.Sample(35,hour,day/365d,sky);check(f.Bodies.Length==sky.Moons.Length+1&&f.NightIlluminanceLux>=0&&double.IsFinite(f.NightIlluminanceLux),"Bounded body/illumination state: "+preset);
                check(f.Bodies.All(b=>Math.Abs(b.Direction.Length-1)<3e-9&&b.IlluminatedFraction>=0&&b.IlluminatedFraction<=1&&b.AngularRadiusDegrees>0),"Directions, phases and sizes remain valid");
            }
        }
        foreach(double phase in new[]{0d,.25,.5,.75})foreach(double start in new[]{0d,31,200000*365d+.3})
        {
            double d=CelestialMechanics.NextPhase(moon,start,phase),error=CelestialMechanics.Fraction(CelestialMechanics.PhaseCycle(moon,d)-phase+.5)-.5;
            check(d>=start&&d-start<moon.SynodicPeriodDays*1.1&&Math.Abs(error)<1e-7,"Next-phase search is bounded and follows actual orbital geometry");
            double hour=phase==0?12:phase==.25?18:phase==.5?0:6;var body=PlayableSky.Sample(0,hour,d/365,earth).Bodies.Single(b=>b.MoonIndex==0);
            double expected=(1-Math.Cos(phase*2*Math.PI))*.5;check(Math.Abs(body.IlluminatedFraction-expected)<.07&&body.Direction.Y>.8,"Phase lighting and rise time agree with the sun, including at large world ages");
        }
        double full=CelestialMechanics.NextPhase(moon,0,.5);var white=PlayableSky.Sample(0,0,full/365,earth);var none=PlayableSky.Sample(0,0,full/365);
        check(white.NightIlluminanceLux>none.NightIlluminanceLux*100&&white.Bodies[1].AngularRadiusDegrees>.24&&white.Bodies[1].AngularRadiusDegrees<.29,"A visible full Earthlike moon supplies appropriate angular size and positive night light");
        var magnified=PlayableSky.Sample(0,0,full/365,earth,moonDiskScale:5,sunDiskScale:3);
        check(magnified.Bodies[1].AngularRadiusDegrees==white.Bodies[1].AngularRadiusDegrees*5&&magnified.Bodies[0].AngularRadiusDegrees==white.Bodies[0].AngularRadiusDegrees*3,"Sun and moon display sizes can be magnified independently");
        check(magnified.NightIlluminanceLux==white.NightIlluminanceLux&&magnified.Bodies[1].Illuminance==white.Bodies[1].Illuminance&&magnified.StarVisibility==white.StarVisibility&&magnified.Ambient.Equals(white.Ambient)&&magnified.NightAirGlow.Equals(white.NightAirGlow),"Bigger visual disks do not brighten nights, wash out stars or change ambient haze");
        check(magnified.Bodies[1].IlluminatedFraction==white.Bodies[1].IlluminatedFraction&&magnified.Bodies[1].PhaseCycle==white.Bodies[1].PhaseCycle&&magnified.Bodies[1].DistanceKm==white.Bodies[1].DistanceKm&&magnified.Bodies[1].Direction.Equals(white.Bodies[1].Direction),"Display magnification does not change physical phase, distance or orbit position");
        var realSun=PlayableSky.Sample(35,12,.218,earth);var largeSun=PlayableSky.Sample(35,12,.218,earth,sunDiskScale:5);check(largeSun.Bodies[0].AngularRadiusDegrees==realSun.Bodies[0].AngularRadiusDegrees*5&&largeSun.Bodies[0].Illuminance==realSun.Bodies[0].Illuminance&&largeSun.Ambient.Equals(realSun.Ambient),"A 5x daytime sun does not brighten daylight or ambient illumination");
        var display=new PlaySettings{MoonVisualExaggeration=5,SunVisualExaggeration=3};var savedDisplay=WorldJson.Read<PlaySettings>(WorldJson.Write(display));savedDisplay.Validate();
        check(savedDisplay.MoonVisualExaggeration==5&&savedDisplay.SunVisualExaggeration==3,"Saved visual exaggeration factors cold-roundtrip");
        var precedingTree=System.Text.Json.Nodes.JsonNode.Parse(WorldJson.Write(new PlaySettings()))!.AsObject();precedingTree.Remove("MoonVisualExaggeration");precedingTree.Remove("SunVisualExaggeration");string precedingDisplay=precedingTree.ToJsonString();var legacyDisplay=WorldJson.Read<PlaySettings>(precedingDisplay);legacyDisplay.Validate();
        check(!precedingDisplay.Contains("VisualExaggeration")&&legacyDisplay.MoonVisualExaggeration==1&&legacyDisplay.SunVisualExaggeration==1,"Older saves retain their exact existing disk size and brightness instead of zeroing missing fields");
        check(PlayableSky.Sample(0,12,full/365,earth).NightIlluminanceLux<.01,"A full moon below the horizon does not illuminate the night sky");
        var red=earth.Copy();red.Moons[0].Tint=new V(1,.3,.12);check(PlayableSky.Sample(0,0,full/365,red).NightIlluminanceLux<white.NightIlluminanceLux*.6,"The same red-reflecting moon is less luminous than a white one");
        var twin=CelestialSettings.Preset("Twin moons");double twinFull=CelestialMechanics.NextPhase(twin.Moons[0],0,.5);var big=PlayableSky.Sample(0,0,twinFull/365,twin);
        check(big.NightIlluminanceLux>white.NightIlluminanceLux*10&&big.Bodies.Any(b=>b.AngularRadiusDegrees>1),"Large nearby bright moons can produce much brighter nights");
        check(white.NightAirGlow.Z>white.NightAirGlow.X,"White moonlight scatters blue preferentially rather than making the sky uniformly gray");
        var high=AirPerspective.Transmission(50000,10000,10000);var low=AirPerspective.Transmission(50000,0,0);check(high.X>low.X&&high.Z>low.Z&&low.Z<low.X,"Air perspective is wavelength-dependent and substantially weaker at altitude");
        foreach(double a in new[]{0d,2000,1000000})foreach(double b in new[]{0d,6000,20000})check(Math.Abs(AirPerspective.Column(50000,a,b,8000)-AirPerspective.Column(50000,b,a,8000))<1e-6,"Atmospheric optical depth is reciprocal and stable for steep high-altitude paths");
        check(AirPerspective.Transmission(0,0,0).Equals(new V(1,1,1)),"Nearby objects are not spuriously fogged");
        check(PlayableSky.Season(.218).Contains("vernal equinox")&&PlayableSky.Season(.468).Contains("summer solstice")&&!PlayableSky.Season(.6).Contains("August"),"Season debug labels use orbital turning points rather than Earth month names");
        foreach(var env in Enum.GetValues<StellarEnvironment>())for(int i=0;i<100;i++)check(StellarField.Direction(81,i,env).Equals(StellarField.Direction(81,i,env))&&Math.Abs(StellarField.Direction(81,i,env).Length-1)<3e-9,"Stellar fields are repeatable unit directions, not GPU/random world decisions");
        check(!StellarField.Direction(81,1,StellarEnvironment.StarCluster).Equals(StellarField.Direction(82,1,StellarEnvironment.StarCluster)),"Different saved seeds produce different skies");
        var inclined=moon.Copy();inclined.InclinationDegrees=60;inclined.OrbitRadiusKm=700000;
        foreach(double start in new[]{0d,100,100000*365d+.1})foreach(double phase in new[]{.01,.25,.5,.99})
        {double next=CelestialMechanics.NextPhase(inclined,start,phase),error=CelestialMechanics.Fraction(CelestialMechanics.PhaseCycle(inclined,next)-phase+.5)-.5;check(Math.Abs(error)<1e-7,"Phase search also converges for the highest supported inclination and longest orbit");}
        var axis=StellarField.ClusterAxis(81);int core=0,tail=0;
        for(int i=0;i<5000;i++){double cosine=V.Dot(StellarField.Direction(81,i,StellarEnvironment.StarCluster),axis);if(cosine>.98)core++;if(cosine>.80&&cosine<.94)tail++;}
        check(core>200&&tail>100,"A clustered field has a concentrated core and a soft surrounding tail");
        int lunar=0,nonlunar=0;for(int i=0;i<300;i++)
        {
            var p=HistoricalSkyContext.Root(earth,i,k=>new Rng(82,"calendar-test-"+k,i).Unit(),i%2==0);if(p.SacredMoon>=0)lunar++;else nonlunar++;
            check(p.CalendarMoon<0||Math.Abs(p.LunationDays-moon.SynodicPeriodDays)<1e-10,"Recorded calendars use the configured moon's actual phase cycle");
            var no=HistoricalSkyContext.Root(CelestialSettings.Preset("No moons"),i,k=>new Rng(82,"calendar-test-"+k,i).Unit(),true);check(no.CalendarMoon<0&&no.SacredMoon<0&&no.Basis==CelestialCalendarBasis.SeasonalSolar,"Moonless worlds cannot invent lunar months or lunar deities");
        }
        check(lunar>0&&nonlunar>0,"Sky traditions are not imposed on every religion");
        var settings=new PlaySettings{Sky=null};string legacy=WorldJson.Write(settings).Replace("\"Sky\":null,","");var old=WorldJson.Read<PlaySettings>(legacy);old.Validate();check(old.Sky==null,"Older gameplay settings keep their preceding moonless sky until a new game is configured");
        var copy=earth.Copy();copy.Moons[0].Albedo=.7;check(earth.Moons[0].Albedo==.12,"Saved celestial settings detach from editable UI copies");
        var commandPlay=new PlaySession();double summer=.468*SimulationTime.Year;commandPlay.CalendarOffsetSeconds=summer;
        check(PlayCommands.Execute(commandPlay,0,"/time set sunrise",0,60).Success&&commandPlay.LocalSolarHour(0)<4,"Sunrise commands follow seasonal day length at the current latitude");
        double priorHour=commandPlay.LocalSolarHour(0);check(!PlayCommands.Execute(commandPlay,0,"/time set sunset",0,85).Success&&commandPlay.LocalSolarHour(0)==priorHour,"Polar dates without a sunset report no event instead of inventing 18:00");
    }
    public static void World(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);var e=new Prehistory(world);var literal=e.Start(e.Prepare());string before=literal.ComputeHash();
        var s=e.ConfigureCelestialHistory(literal,CelestialSettings.Preset("Twin moons"));check(literal.ComputeHash()==before&&literal.History.Sky==null&&s.History.Sky!=null,"Explicit celestial attachment leaves the preceding historical checkpoint literal");
        var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;var q=new SimulationEventQueue(s.Timeline.Events);queueField.SetValue(e,q);
        try{typeof(Prehistory).GetMethod("FoundAggregateCulture",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,new object[]{s,s.Origin,e.Patches[s.Origin].Cell,-1});}
        finally{queueField.SetValue(e,null);}s.Timeline.Events=q.Ordered();s.Seal();e.Validate(s);
        check(s.History.Sky.Faiths.Length==1&&s.History.Sky.Cultures.Length==1,"Actual culture/faith creation appends its saved celestial conventions");
        var branch=s.Copy();var child=branch.History.Religions[0].Copy();child.Id=1;child.ParentId=0;branch.History.Religions=branch.History.Religions.Concat(new[]{child}).ToArray();
        typeof(Prehistory).GetMethod("AppendReligiousDoctrine",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,new object[]{branch,1,ReligiousIssue.SacredImages,-1});
        check(branch.History.Sky.Faiths[1].Parent==0&&branch.History.Sky.Faiths[1].LunationDays==s.History.Sky.Faiths[0].LunationDays&&s.History.Sky.Faiths.Length==1,"An actual doctrinal-append hook inherits sky conventions without mutating the parent publication");
        string unchanged=s.ComputeHash();var religion=HistoryAtlas.Read(s,"religion",0,world);var culture=HistoryAtlas.Read(s,"people",0,world);
        check(religion.Sections.Any(x=>x.Title=="Ritual astronomy and festivals")&&culture.Sections.Any(x=>x.Title=="Calendar and the observed sky")&&s.ComputeHash()==unchanged,"Religious and cultural atlas entries expose actual saved calendars without changing history");
        world.Prehistory=s;string geography=StateHash.Compute(world);var cold=WorldFile.Read(WorldFile.Write(world));check(cold.Prehistory.Hash==s.Hash&&StateHash.Compute(cold)==geography&&cold.Prehistory.History.Sky.Settings.Moons.Length==2,"Complete binary world persistence retains celestial history and unchanged geography");
        var resumed=new Prehistory(cold);var a=e.AdvanceSeconds(s,SimulationTime.Year);var b=resumed.AdvanceSeconds(cold.Prehistory,SimulationTime.Year);
        check(a.Hash==b.Hash,"Celestial-context histories continue identically after a cold world reload");
    }
    public static void Native(string path,Action<bool,string> check)
    {
        var native=WorldJson.Read<CelestialTradition[]>(File.ReadAllText(path));var earth=CelestialSettings.Preset("Earth-like");check(native.Length==32,"Native tradition probe count");
        for(int i=0;i<native.Length;i++){int id=i;var expected=HistoricalSkyContext.Root(earth,id,k=>new Rng(82,"calendar-test-"+k,id).Unit(),id%2==0);check(StateHash.Compute(native[i])==StateHash.Compute(expected),"Native/CLR celestial traditions have exact canonical equality: "+id);}
    }
}

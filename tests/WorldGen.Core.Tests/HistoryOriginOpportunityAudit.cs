using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Read-only diagnostic replay. All state advances use the ordinary calendar;
// coarse actor sampling and three explicit saved checkpoints are not sim turns.
public static class HistoryOriginOpportunityAudit
{
    static object Call(Prehistory e,string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,args);
    sealed class Candidate
    {
        public int Id,Cell,Year,Population,Institution,Farming,Preparation,Stability,Links,LastPoliticalYear,Decline;
        public long Rural,Reserve;
        public double Fiscal,Environment,Staples,Homeland,Core,HazardN,HazardM,Draw;
        public string Name="",Climate="";
        public int[] CropIds=Array.Empty<int>(),CropAdoptedYears=Array.Empty<int>(),CropPractice=Array.Empty<int>();
        public bool Provisioning,Qualified;
        public string[] Failing=Array.Empty<string>();
    }
    public static void Run(string path,string folder,string frozenCore)
    {
        string Digest(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        string loadedCore=typeof(Prehistory).Assembly.Location,expected=Digest(frozenCore),actual=Digest(loadedCore);
        if(expected!=actual)throw new Exception("Audit Core differs from the requested frozen baseline; freeze/rebuild intentionally before replay.");
        Directory.CreateDirectory(folder);var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string originalHash=original.ComputeHash();
        var first=original.History.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();int firstYear=first.FoundedYear,firstTown=first.CapitalId;
        world.Prehistory=null;var engine=new Prehistory(world);var initial=engine.Start(engine.Prepare());var session=new SimulationSession(engine,initial);
        var river=(double[])typeof(Prehistory).GetField("riverAccess",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        var season=(double[])typeof(Prehistory).GetField("landscapeSeason",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        Candidate Read(PrehistoryState s,int id)
        {
            var t=s.Society.Settlements[id];var h=s.History;var a=h.Aggregates;var city=a.Cities[id];var record=h.Towns[id];var hc=h.Cultures[t.CultureId];var life=a.Living.Cities[id];
            double fiscal=(double)Call(engine,"LivingCropLandscape",s,t.CultureId,t.Cell,true),environment=(double)Call(engine,"EnvironmentalEstablishment",s,t.Cell,t.CultureId),staples=(double)Call(engine,"EnvironmentalStaples",s,t.CultureId,t.Cell),homeland=(double)Call(engine,"HomelandOriginFactor",s,t.Cell);
            double core=Math.Min(1,fiscal/.30)*Math.Min(1,t.Population/3500.0)*Math.Min(1,hc.Institution/600.0)*(.60+.25*Math.Min(1,river[t.Cell]*4)+.15*Math.Min(1,city.Neighbors.Count(n=>s.Society.Settlements[n].Population>=500)/4.0))*(.85+.15*(.25+.75*season[t.Cell]));
            int husbandry=h.Agriculture[t.CultureId].HusbandryYears?.Max()??0;var failing=new List<string>();
            if(t.Population<1200)failing.Add("urban<1200");if(fiscal<.10&&(fiscal<.04||husbandry<12000))failing.Add("insufficient-storable-crops");if(t.FoodReserve<t.Population*.3)failing.Add("low-food-reserve");if(life.DeclineYears>0)failing.Add("decline");bool provision=failing.Count==0;
            if(t.Stability<550)failing.Add("unstable");if(record.CivicPreparationYears<120)failing.Add("preparation<120");if(s.Society.Cultures[t.CultureId].Farming<250)failing.Add("farming<250");
            double crop=.01+.99*Math.Clamp(staples,0,1),m=(.000000002+.00025*Math.Pow(Math.Clamp(core,0,1),4))*environment*crop*crop*homeland;
            return new Candidate{Id=id,Cell=t.Cell,Name=t.Name,Year=s.Years,Population=(int)Math.Min(int.MaxValue,t.Population),Rural=city.Rural,Reserve=t.FoodReserve,Institution=hc.Institution,Farming=s.Society.Cultures[t.CultureId].Farming,Preparation=record.CivicPreparationYears,Stability=t.Stability,Links=city.Neighbors.Length,LastPoliticalYear=city.LastPoliticalYear,Decline=life.DeclineYears,Fiscal=fiscal,Environment=environment,Staples=staples,Homeland=homeland,Core=core,HazardN=HistoricalLivingWorldOrigins.IndependentStateHazard(core,environment,staples,homeland),HazardM=Math.Max(.000000000000001,D.Q(m,1000000000000000)),Draw=(double)Call(engine,"EnvironmentalOriginRoll","state",id,s.Years),Climate=world.Climate.Classification[t.Cell].ToString(),Provisioning=provision,Qualified=failing.Count==0,Failing=failing.ToArray(),CropIds=h.Agriculture[t.CultureId].Cultivars.Select(c=>c?.CropId??-1).ToArray(),CropAdoptedYears=(int[])h.Agriculture[t.CultureId].AdoptedYear.Clone(),CropPractice=(int[])h.Agriculture[t.CultureId].Practice.Clone()};
        }
        var periods=new List<object>();var ever=new HashSet<int>();var failures=new Dictionary<string,long>();long activeTownYears=0,qualifiedTownYears=0;double exposureN=0,exposureM=0;int firstQualified=-1,lastYear=180000;
        var options=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};
        void Save(PrehistoryState s)
        {engine.Validate(s);world.Prehistory=s;File.WriteAllBytes(Path.Combine(folder,"year-"+s.Years+".world"),WorldFile.Write(world));world.Prehistory=null;}
        session.AdvanceSeconds(180000*SimulationTime.Year,int.MaxValue);
        while(session.LastSnapshot.Years<firstYear-1)
        {
            int next=Math.Min(firstYear-1,lastYear+50);session.AdvanceSeconds((next-lastYear)*SimulationTime.Year,int.MaxValue);var s=session.LastSnapshot;int dt=s.Years-lastYear;lastYear=s.Years;
            if(s.History.Polities.Length>0)throw new Exception("Replay formed a state before the frozen baseline; audit must not silently compare different histories.");
            var candidates=s.Society.Settlements.Where(t=>s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0&&t.Population>0).Select(t=>Read(s,t.Id)).ToArray();activeTownYears+=(long)candidates.Length*dt;
            foreach(var c in candidates){foreach(string reason in c.Failing)failures[reason]=failures.GetValueOrDefault(reason)+(long)dt;if(!c.Qualified)continue;qualifiedTownYears+=dt;ever.Add(c.Id);if(firstQualified<0)firstQualified=s.Years;exposureN+=c.HazardN*dt;exposureM+=c.HazardM*dt;}
            if(s.Years%5000==0||s.Years==firstYear-1)
            {
                var row=new{Year=s.Years,Active=candidates.Length,Qualified=candidates.Count(c=>c.Qualified),EverQualified=ever.Count,FirstQualified=firstQualified,QualifiedTownYears=qualifiedTownYears,ExposureN=exposureN,ExposureM=exposureM,NoOriginProbabilityN=Math.Exp(-exposureN),Candidates=candidates.OrderByDescending(c=>c.HazardN).Take(8)};periods.Add(row);Console.WriteLine("Origin history "+s.Years+": qualified="+candidates.Count(c=>c.Qualified)+", exposureN="+exposureN.ToString("F4")+", exposureM="+exposureM.ToString("F4"));
            }
            if(s.Years==200000||s.Years==240000||s.Years==280000)Save(s);
        }
        object exact=null;int observedFirst=-1;
        for(int attempt=0;attempt<12&&observedFirst<0;attempt++)
        {
            var previous=session.LastSnapshot;long due=previous.Timeline.Events.First(ev=>ev.Process==21&&ev.Entity==firstTown).Due;
            if(due>session.Seconds+1)session.AdvanceSeconds(due-session.Seconds-1,int.MaxValue);
            var before=session.LastSnapshot;int political=before.History.Aggregates.Cities[firstTown].LastPoliticalYear;var beforeCity=Read(before,firstTown);
            session.AdvanceSeconds(1,1);var after=session.LastSnapshot;if(after.History.Polities.Length==0)continue;
            var founded=after.History.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();observedFirst=founded.FoundedYear;var site=Read(after,firstTown);int elapsed=after.Years-political;
            exact=new{FoundingYear=observedFirst,FoundingTown=founded.CapitalId,DueSeconds=due,ElapsedPoliticalYears=elapsed,Before=beforeCity,AfterCityDemography=site,ProbabilityN=1-Math.Exp(-site.HazardN*elapsed),ProbabilityM=1-Math.Exp(-site.HazardM*elapsed),NDrawPassed=site.Draw<1-Math.Exp(-site.HazardN*elapsed),MDrawAlsoPassed=site.Draw<1-Math.Exp(-site.HazardM*elapsed)};
        }
        if(observedFirst!=firstYear)throw new Exception("Exact founding replay did not match the frozen baseline.");
        File.WriteAllText(Path.Combine(folder,"origin-opportunity-history.json"),JsonSerializer.Serialize(new{FrozenCoreSha256=actual,Seed=world.Recipe.Seed,Rules=initial.History.Version,Sampling="50-year active actor samples; hazard exposure and qualified town-years are approximations, not an exact event census",InitialOrigin=initial.Origin,FirstQualifiedYear=firstQualified,ActiveTownYears=activeTownYears,QualifiedTownYears=qualifiedTownYears,EverQualified=ever.Count,IntegratedHazardN=exposureN,IntegratedHazardM=exposureM,Failures=failures,Periods=periods,ExactFirst=exact,InputPreserved=original.ComputeHash()==originalHash},options));
        Console.WriteLine("Exact first origin="+observedFirst+", input preserved="+(original.ComputeHash()==originalHash));
    }
}

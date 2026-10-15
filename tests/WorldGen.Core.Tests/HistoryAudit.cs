using System.Globalization;
using System.Text;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Diagnostic only: consumes authoritative snapshots, never changes geography or expectations.
public static class HistoryAudit
{
    public static int MedievalCeilingYear(HistoryState history,bool requireNavigation=true)
    {var capstones=requireNavigation?new[]{HistoricalTechnology.Steel,HistoricalTechnology.Navigation,HistoricalTechnology.Scholasticism}:new[]{HistoricalTechnology.Steel,HistoricalTechnology.Scholasticism};return history.Cultures.Where(c=>capstones.All(t=>c.Technology[(int)t]>0)).Select(c=>capstones.Max(t=>c.Technology[(int)t])-1).DefaultIfEmpty(-1).Min();}
    static int TechnologyCeilingYear(HistoryState history,bool oceans)
    {
        if(history.Modern==null||history.Modern.TechnologyCeiling==HistoricalTechnologyCeiling.Medieval)return MedievalCeilingYear(history,oceans);
        var capstones=history.Modern.TechnologyCeiling==HistoricalTechnologyCeiling.EarlyModern?new[]{HistoricalTechnology.Printing,HistoricalTechnology.ScientificMethod,HistoricalTechnology.CropRotation}:history.Modern.TechnologyCeiling==HistoricalTechnologyCeiling.Industrial?new[]{HistoricalTechnology.SteamPower,HistoricalTechnology.Railways,HistoricalTechnology.Electricity,HistoricalTechnology.Sanitation}:new[]{HistoricalTechnology.Computing,HistoricalTechnology.Telecommunications,HistoricalTechnology.Antibiotics,HistoricalTechnology.MechanizedAgriculture};
        return history.Cultures.Where(c=>capstones.All(t=>HistoricalTechnologies.Has(c,t))).Select(c=>capstones.Max(t=>c.Technology[(int)t])-1).DefaultIfEmpty(-1).Min();
    }
    public static int TargetYear(int safetyLimit,int ceiling,int tail)=>tail>=0&&ceiling>=0?Math.Min(safetyLimit,ceiling+tail):safetyLimit;
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--history-audit");string source=args[at+1],folder=Path.GetFullPath(args[at+2]);
        int end=int.Parse(args[at+3]),version=int.Parse(args[at+4]);Directory.CreateDirectory(folder);
        int tailAt=Array.IndexOf(args,"--after-max-tech");int tail=tailAt<0?-1:int.Parse(args[tailAt+1]);
        if(tail>=0&&(tail<500||tail>1000))throw new ArgumentOutOfRangeException("--after-max-tech","Use 500–1000 years after the first culture attains the selected technological ceiling's capstones.");
        var w=WorldFile.Read(File.ReadAllBytes(source));bool resume=args.Contains("--continue-history");
        string historyVersion=version>=21?"history-16":version>=20?"history-15":version>=19?"history-14":version>=18?"history-13":version>=17?"history-12":version>=16?"history-11":version>=15?"history-10":version>=14?"history-9":version>=13?"history-8":version>=12?"history-7":version>=11?"history-6":version>=10?"history-5":version>=9?"history-4":version>=8?"history-3":version>=7?"history-2":"history-1";
        if(!resume)w.Prehistory=new PrehistoryState{Version="prehistory-6",History=new HistoryState{Version=historyVersion}};
        int originAt=Array.IndexOf(args,"--origin-patch"),origin=originAt<0?-1:int.Parse(args[originAt+1]);
        if(resume&&originAt>=0)throw new ArgumentException("A resumed history retains its original founding location.");
        var engine=new Prehistory(w);var s=resume?w.Prehistory:engine.Prepare(origin);
        if(!resume){s.Version="prehistory-6";s.History.Version=historyVersion;s.Seal();s=engine.Start(s);}else engine.Validate(s);
        if((historyVersion=="history-10"||historyVersion=="history-11"||historyVersion=="history-12"||historyVersion=="history-13"||historyVersion=="history-14"||(historyVersion=="history-15"||historyVersion=="history-16"))&&s.Timeline==null)s=engine.AdvanceSeconds(s,1);
        if(resume&&historyVersion=="history-11"&&s.History.Version=="history-10")s=engine.UpgradeToDelegatedHistory(s);
        if(resume&&historyVersion=="history-12"&&s.History.Version!="history-12")s=engine.UpgradeToInstitutionalHistory(s);
        if(resume&&historyVersion=="history-13"&&s.History.Version!="history-13")s=engine.UpgradeToModernHistory(s);
        if(resume&&historyVersion=="history-14"&&s.History.Version!="history-14")throw new ArgumentException("History14 resumes its own checkpoints; use version20 for an explicit current-model upgrade.");
        if(resume&&historyVersion=="history-16"&&s.History.Version!="history-16")s=engine.UpgradeToCurrentHistory(s);
        var lines=new List<string>{"year,population,occupiedPatches,cultures,towns,states,urbanCells,cultureCells,events,seconds"};var watch=System.Diagnostics.Stopwatch.StartNew();
        int firstState=-1,maxTechYear=-1;bool firstSnapshot=false;var milestones=new SortedDictionary<string,int>();
        int intervalAt=Array.IndexOf(args,"--map-interval"),mapInterval=intervalAt<0?1000:int.Parse(args[intervalAt+1]);
        if(mapInterval<25||mapInterval>5000)throw new ArgumentOutOfRangeException("--map-interval");
        int sampleAt=Array.IndexOf(args,"--sample-years"),sampleYears=sampleAt<0?50:int.Parse(args[sampleAt+1]);
        if(sampleYears<1||sampleYears>1000)throw new ArgumentOutOfRangeException("--sample-years");
        int lastMap=-1;var previousOwner=Enumerable.Repeat(-1,w.Surface.Cells.Length).ToArray();
        bool finalMapOnly=args.Contains("--final-map-only");
        int checkpointAt=Array.IndexOf(args,"--checkpoint-years"),checkpointYears=checkpointAt<0?0:int.Parse(args[checkpointAt+1]),lastCheckpoint=s.Years;
        if(checkpointYears!=0&&checkpointYears<100)throw new ArgumentOutOfRangeException("--checkpoint-years");
        var transitions=new List<string>{"year,claimedKm2,releasedKm2,changedOwnerKm2,changedRulingCultureKm2"};
        bool ObserveMilestones()
        {
            bool changed=false;
            for(int k=0;k<HistoricalTechnologies.Count(s.History.Version);k++)
            {
                int year=s.History.Cultures.Where(c=>c.Technology[k]>0).Select(c=>c.Technology[k]-1).DefaultIfEmpty(-1).Min();
                string name=HistoricalTechnologies.Name(s.History.Version,k);
                if(year>=0&&!milestones.ContainsKey(name)){milestones.Add(name,year);changed=true;}
            }
            maxTechYear=TechnologyCeilingYear(s.History,w.Hydrology.OceanConnected.Any(o=>o));
            int observedFirst=s.History.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
            if(firstState<0&&observedFirst>=0)changed=true;
            firstState=observedFirst;
            return changed;
        }
        ObserveMilestones();
        while(s.Years<end)
        {
            int target=TargetYear(end,maxTechYear,tail);
            if(s.Years>=target)break;
            s=engine.AdvanceYears(s,Math.Min(s.Society.Cultures.Length>0&&(tail>=0||sampleAt>=0)?sampleYears:1000,target-s.Years));engine.Validate(s);
            bool milestone=ObserveMilestones();
            string row=$"{s.Years},{SocietyView.Population(s)},{s.Population.Count(p=>p>0)},{s.Society.Cultures.Length},{s.Society.Settlements.Count(t=>t.Population>=100)},{s.History.Polities.Count(p=>p.EndedYear<0)},{s.History.CellStage.Count(x=>x>=2)},{s.Society.CellCulture.Count(x=>x>=0)},{s.History.Archive.Count},{watch.Elapsed.TotalSeconds:F1}";
            lines.Add(row);File.WriteAllLines(Path.Combine(folder,"timeline.csv"),lines);
            if(checkpointYears>0&&s.Years-lastCheckpoint>=checkpointYears)
            {
                var checkpointWorld=new World();foreach(var field in typeof(World).GetFields())field.SetValue(checkpointWorld,field.GetValue(w));checkpointWorld.Prehistory=s;
                string pending=Path.Combine(folder,"checkpoint.world.pending"),checkpoint=Path.Combine(folder,"checkpoint.world");
                File.WriteAllBytes(pending,WorldFile.Write(checkpointWorld));File.Move(pending,checkpoint,true);lastCheckpoint=s.Years;
            }
            if(s.History.CellPolity.Length>0)
            {
                double gained=0,lost=0,changed=0,colorChanged=0;
                for(int cell=0;cell<previousOwner.Length;cell++)
                {
                    int before=previousOwner[cell],after=s.History.CellPolity[cell];if(before==after)continue;
                    double area=w.Hydrology.OceanConnected[cell]?(SubcellGeography.Find(w,cell)?.LargestIslandAreaM2??0)/1e6:w.Surface.Cells[cell].AreaM2*(1-w.Hydrology.LakeCoverageFraction[cell])/1e6;
                    if(before<0)gained+=area;else if(after<0)lost+=area;else{changed+=area;if(s.History.Polities[before].CultureId!=s.History.Polities[after].CultureId)colorChanged+=area;}
                    previousOwner[cell]=after;
                }
                transitions.Add(FormattableString.Invariant($"{s.Years},{gained:F3},{lost:F3},{changed:F3},{colorChanged:F3}"));
                File.WriteAllLines(Path.Combine(folder,"political-transitions.csv"),transitions);
            }
            if(s.Years%10000==0||firstState>=0&&(!firstSnapshot||s.Years-firstState<6000)&&s.Years%1000==0||tail>=0&&milestone)
            { Console.WriteLine(row);if(!finalMapOnly&&(w.Surface.Cells.Length<=80000||s.Years%50000==0||firstState>=0&&!firstSnapshot||tail>=0&&milestone))Snapshot(w,engine,s,folder);if(firstState>=0)firstSnapshot=true; }
            if(!finalMapOnly&&firstState>=0&&(lastMap<0||s.Years-lastMap>=mapInterval)){Snapshot(w,engine,s,folder);lastMap=s.Years;}
        }
Snapshot(w,engine,s,folder);w.Prehistory=s;File.WriteAllBytes(Path.Combine(folder,"final.world"),WorldFile.Write(w));File.WriteAllText(Path.Combine(folder,"history.json"),WorldJson.Write(s));File.WriteAllText(Path.Combine(folder,"chronicle.txt"),SocietyView.CampaignLore(s,w));
        File.WriteAllText(Path.Combine(folder,"resume-hash.txt"),engine.AdvanceYears(s,37).Hash);
        var routeRows=new List<string>{"a,b,distanceKm,shallow,capacityA,capacityB,populationA,populationB,arrivalA,arrivalB,navigationA,navigationB"};
        int Navigation(int patch)=>engine.Patches[patch].Cells.Select(i=>s.Society.CellCulture.Length==0?-1:s.Society.CellCulture[i]).Where(i=>i>=0).Select(i=>s.History.Cultures[i].Technology[(int)HistoricalTechnology.Navigation]>0?2:s.History.Cultures[i].Technology[(int)HistoricalTechnology.Sailing]>0?1:0).DefaultIfEmpty(0).Max();
        foreach(var r in engine.Routes.Where(r=>r.Sea))routeRows.Add($"{r.A},{r.B},{r.DistanceM/1000},{r.Shallow},{engine.Patches[r.A].Capacity},{engine.Patches[r.B].Capacity},{s.Population[r.A]},{s.Population[r.B]},{s.ArrivalYear[r.A]},{s.ArrivalYear[r.B]},{Navigation(r.A)},{Navigation(r.B)}");
        File.WriteAllLines(Path.Combine(folder,"sea-routes.csv"),routeRows);
        var patchRows=new List<string>{"patch,latitude,longitude,capacity,population,arrival,knowledge,island"};
        for(int p=0;p<engine.Patches.Length;p++){var point=w.Surface.Cells[engine.Patches[p].Cell].Position;patchRows.Add(FormattableString.Invariant($"{p},{Math.Asin(point.Z)*180/Math.PI:F3},{Math.Atan2(point.Y,point.X)*180/Math.PI:F3},{engine.Patches[p].Capacity},{s.Population[p]},{s.ArrivalYear[p]},{s.Society.Knowledge[p]},{engine.Patches[p].Island}"));}
        File.WriteAllLines(Path.Combine(folder,"patches.csv"),patchRows);
        var candidateRows=new List<string>{"patch,cell,latitude,longitude,knowledge,patchPopulation,foragerCapacity,localPopulation,siteDensity,storageOpportunity,agriculturalYield,riverFraction,nativeCropMask"};
        for(int p=0;p<engine.Patches.Length;p++)
        {
            var site=engine.Patches[p].Cells.Select(cell=>engine.AssessHistoricalSite(s,cell)).OrderByDescending(x=>x.StorageOpportunity).ThenBy(x=>x.Cell).First();var point=w.Surface.Cells[site.Cell].Position;
            candidateRows.Add(FormattableString.Invariant($"{p},{site.Cell},{Math.Asin(point.Z)*180/Math.PI:F3},{Math.Atan2(point.Y,point.X)*180/Math.PI:F3},{site.Knowledge},{site.PatchPopulation},{site.ForagerCapacity},{site.RuralPopulation},{site.Density:F6},{site.StorageOpportunity:F6},{site.AgriculturalYield:F6},{site.RiverFraction:F6},{site.NativeCropMask}"));
        }
        File.WriteAllLines(Path.Combine(folder,"candidate-sites.csv"),candidateRows);
        HistoryAuditReview.Write(w,engine,s,folder);
        string ceilingLabel=s.History.Modern?.TechnologyCeiling.ToString()??"Medieval";
        var eras=new List<string>{"milestone,firstYear"};eras.Add("First state,"+firstState);foreach(var item in milestones.OrderBy(x=>x.Value).ThenBy(x=>x.Key,StringComparer.Ordinal))eras.Add(item.Key+","+item.Value);eras.Add(ceilingLabel+" capstones in one culture,"+maxTechYear);eras.Add("Audit end,"+s.Years);File.WriteAllLines(Path.Combine(folder,"eras.csv"),eras);
        File.WriteAllText(Path.Combine(folder,"audit-summary.txt"),$"History {s.History.Version}; origin patch {s.Origin}; end {s.Years}; first state {firstState}; {ceilingLabel} capstones {maxTechYear}; post-capstone years {(maxTechYear<0?-1:s.Years-maxTechYear)}.\n"+(tail>=0&&(maxTechYear<0||s.Years<maxTechYear+tail)?"Safety limit reached without the full ceiling/tail interval; do not report a completed era progression.\n":""));
        Console.WriteLine("DONE "+folder+" first state "+firstState+" "+ceilingLabel+" capstones "+maxTechYear+" end "+s.Years);
    }
    public static void Snapshot(World w,Prehistory engine,PrehistoryState s,string folder,IEnumerable<HistoryOverlay> selected=null)
    {
        var text=new StringBuilder("kind,id,name,year,latitude,longitude,biome,flood,aridity,population\n");
        void Entry(string kind,int id,string name,int year,int cell,long population){var p=w.Surface.Cells[cell].Position;var h=w.Ecology.Habitat[cell];text.AppendLine(FormattableString.Invariant($"{kind},{id},{name.Replace(',',' ')},{year},{Math.Asin(p.Z)*180/Math.PI:F2},{Math.Atan2(p.Y,p.X)*180/Math.PI:F2},{h.Biome},{h.Flooding},{h.AridityMilli},{population}"));}
        foreach(var c in s.Society.Cultures)Entry("culture",c.Id,c.Name,c.FoundedYear,c.OriginCell,c.Population);
        foreach(var t in s.Society.Settlements)Entry("town",t.Id,t.Name,t.FoundedYear,t.Cell,t.Population);
        File.WriteAllText(Path.Combine(folder,$"sites-{s.Years}.csv"),text.ToString());
        var colors=GlobeView.CellValues(w,Layer.Climate).Colors;
        var corners=Enumerable.Range(0,w.Surface.Cells.Length).Select(_=>new List<V>()).ToArray();
        for(int t=0;t<w.Surface.Triangles.Length;t+=3)for(int k=0;k<3;k++)corners[w.Surface.Triangles[t+k]].Add(w.Surface.Corners[t/3]);
        var humanCodes=PrehistoryView.Codes(w,engine,s);
        foreach(var overlay in selected??new[]{HistoryOverlay.States,HistoryOverlay.Cultures,HistoryOverlay.Religions,HistoryOverlay.Technology})
        {
            var b=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1800' height='960' viewBox='0 0 1800 960'><rect width='1800' height='960' fill='#8cc7e6'/>");
            b.Append($"<text x='24' y='30' fill='white' font-family='sans-serif' font-size='22'>{overlay} / climate · {SimulationClockView.Year(s,s.Years)} · {s.Society.Cultures.Length} cultures · {s.History.Polities.Count(p=>p.EndedYear<0)} states</text>");
            for(int i=0;i<corners.Length;i++)
            {
                var p=w.Surface.Cells[i].Position;double lon=Math.Atan2(p.Y,p.X),lat=Math.Asin(p.Z);int id=SocietyView.OverlayId(s,i,overlay);
                V color=new V(.55,.78,.9);if(!w.Hydrology.OceanConnected[i]){color=new V(colors[i*3],colors[i*3+1],colors[i*3+2])*.15+new V(.85,.85,.85);if(id>=0)color=SocietyView.CivilizationColor(SocietyView.OverlayColorId(s,i,overlay));else if(humanCodes[i]>=170){double gray=humanCodes[i]>=215?.38:humanCodes[i]>=195?.55:.78;color=new V(gray,gray,gray);}}
                string hex=$"#{(int)(255*color.X):x2}{(int)(255*color.Y):x2}{(int)(255*color.Z):x2}";
                var points=corners[i].Select(v=>{double x=Math.Atan2(v.Y,v.X);while(x-lon>Math.PI)x-=2*Math.PI;while(x-lon< -Math.PI)x+=2*Math.PI;return(x,y:Math.Asin(v.Z));}).OrderBy(v=>Math.Atan2(v.y-lat,(v.x-lon)*Math.Cos(lat))).ToArray();
                foreach(int wrap in new[]{-1,0,1}){if(points.All(v=>v.x+wrap*2*Math.PI< -Math.PI)||points.All(v=>v.x+wrap*2*Math.PI>Math.PI))continue;string pts=string.Join(" ",points.Select(v=>FormattableString.Invariant($"{900+(v.x+wrap*2*Math.PI)*900/Math.PI:F2},{510-v.y*900/Math.PI:F2}")));b.Append($"<polygon points='{pts}' fill='{hex}'/>");}
            }
            if(overlay==HistoryOverlay.States)
            {
                var borders=new HistoryBorders().Segments(w,s);
                for(int at=0;at<borders.Length;at+=6)
                {
                    double x=900+Math.Atan2(borders[at+1],borders[at])*900/Math.PI,y=510-Math.Asin(borders[at+2])*900/Math.PI;
                    double xx=900+Math.Atan2(borders[at+4],borders[at+3])*900/Math.PI,yy=510-Math.Asin(borders[at+5])*900/Math.PI;
                    if(xx-x>900)xx-=1800;if(xx-x< -900)xx+=1800;
                    foreach(int wrap in new[]{-1,0,1})b.Append(FormattableString.Invariant($"<path d='M{x+wrap*1800:F2} {y:F2}L{xx+wrap*1800:F2} {yy:F2}' stroke='#202528' stroke-width='.7'/>") );
                }
            }
            for(int latitude=-60;latitude<=60;latitude+=30)b.Append(FormattableString.Invariant($"<path d='M0 {510-latitude*5}H1800' stroke='#ffffff' stroke-opacity='.25'/><text x='5' y='{505-latitude*5}' fill='#29343e' font-size='13'>{latitude}°</text>"));
            foreach(var t in s.Society.Settlements.Where(t=>t.Population>=800)){var p=w.Surface.Cells[t.Cell].Position;double x=900+Math.Atan2(p.Y,p.X)*900/Math.PI,y=510-Math.Asin(p.Z)*900/Math.PI;b.Append(FormattableString.Invariant($"<circle cx='{x:F2}' cy='{y:F2}' r='{(t.PolityId>=0?3:2)}' fill='white' stroke='black'/>"));}
            foreach(var entry in SocietyView.MapLabels(w,s,overlay,engine).Take(80)){var p=w.Surface.Cells[entry.LabelCell>=0?entry.LabelCell:entry.Cell].Position;double x=900+Math.Atan2(p.Y,p.X)*900/Math.PI,y=510-Math.Asin(p.Z)*900/Math.PI;b.Append(FormattableString.Invariant($"<text x='{x:F2}' y='{y:F2}' text-anchor='middle' fill='#151b20' stroke='#ffffffaa' stroke-width='2' paint-order='stroke' font-family='Georgia' font-size='12'>{System.Security.SecurityElement.Escape(entry.LabelName.Length>0?entry.LabelName:entry.Name)}</text>"));}
            b.Append("</svg>");File.WriteAllText(Path.Combine(folder,$"{overlay}-{s.Years}.svg"),b.ToString());
        }
    }
}

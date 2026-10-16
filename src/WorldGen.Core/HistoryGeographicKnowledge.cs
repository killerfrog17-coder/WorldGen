using System;
using System.Linq;
namespace WorldGen.Core
{
    // A polity's reports, not a live query of the world's hidden resources.
    // Grade 1: hearsay; 2: an observed coast/copied directions; 3: an actual
    // completed outward-and-return survey. Knowledge does not confer ownership.
    [Serializable] public sealed class HistoricalGeographicMap
    {
        public HistoricalLocalBag Places=new HistoricalLocalBag();
        public HistoricalCellTree Grade,ReportedYear,Opportunity,Host,Shore,ReviewAfter;
        public HistoricalSeaMission[] Missions=Array.Empty<HistoricalSeaMission>();
        public long ScheduledDue,NextPlan;
        public int Voyages,Surveys,Landfalls,FailedVoyages;
        public HistoricalGeographicMap(int regions)
        {Grade=new HistoricalCellTree(regions);ReportedYear=new HistoricalCellTree(regions);Opportunity=new HistoricalCellTree(regions);Host=new HistoricalCellTree(regions);Shore=new HistoricalCellTree(regions);ReviewAfter=new HistoricalCellTree(regions);}
        public HistoricalGeographicMap Copy(){var c=(HistoricalGeographicMap)MemberwiseClone();c.Places=Places.Copy();c.Missions=Missions.Select(m=>m.Copy()).ToArray();return c;}
        public bool Record(int region,int grade,int year,int opportunity,int host,int shore)
        {
            if(grade<1||grade>3||region<0||region>=Grade.Length)throw new ArgumentException("Invalid geographical report.");
            int previous=Grade[region];if(previous>grade||previous==grade&&ReportedYear[region]>=year)return false;
            Places.Add(region);Grade=Grade.With(region,grade);ReportedYear=ReportedYear.With(region,year);
            Opportunity=Opportunity.With(region,Math.Max(0,Math.Min(3000,opportunity)));Host=Host.With(region,host);Shore=Shore.With(region,shore);return previous<grade;
        }
    }
    [Serializable] public sealed class HistoricalSeaMission
    {
        // Survey returns to report before the map updates. A settlement convoy
        // arrives outward; its actual household/food transaction is rechecked.
        public const int Survey=0,Convoy=1,Report=2;
        public int Kind,Source,Target,Region,DistanceM,Reporter=-1,Grade,Opportunity,Host=-1,ReportedYear;
        public long Departed,Due,Investment;
        public HistoricalSeaMission Copy()=>(HistoricalSeaMission)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalGeographicKnowledge
    {
        public int Version=1,SinceYear;
        public HistoricalGeographicMap[] Governments=Array.Empty<HistoricalGeographicMap>();
        public HistoricalGeographicKnowledge Copy()=>new HistoricalGeographicKnowledge{Version=Version,SinceYear=SinceYear,Governments=Governments.Select(m=>m.Copy()).ToArray()};
    }
    public static class HistoricalSeaVentureEffects
    {
        // Travel coefficients are game-scale priors, not calendar gates. Ocean
        // capability has to be deployed; an unadopted invention is not a fleet.
        public static long Duration(int distanceM,bool returning,double ocean,double steam,double modern,double roll)
        {
            double kmPerDay=55+65*D.Clamp(ocean)+220*D.Clamp(steam)+240*D.Clamp(modern);
            double days=(returning?90:150)+(returning?2:1)*Math.Max(0,distanceM)/1000.0/kmPerDay;
            return Math.Max(SimulationTime.Day,(long)Math.Ceiling(days*(.8+.4*D.Clamp(roll))*SimulationTime.Day));
        }
        public static int Fleet(long urban,long surplus,int exhaustion)
            =>surplus<=0||exhaustion>=750?0:(int)Math.Max(1,Math.Min(4,1+Math.Sqrt(Math.Max(0,urban)/25000.0)*(1-exhaustion/1000.0)));
        public static bool CanColonize(int knowledgeGrade,bool deployedOcean,int distanceM)
            =>knowledgeGrade>=2&&(deployedOcean||distanceM<=700000);
    }
    public sealed partial class Prehistory
    {
        const int SeaMissionProcess=27;
        bool GeographicHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Geography!=null;
        public PrehistoryState UpgradeGeographicKnowledge(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Geographical knowledge requires history32.");
            if(GeographicHistory(source))return source;var s=source.Copy();InitializeGeographicKnowledge(s);s.Seal();return s;
        }
        void InitializeGeographicKnowledge(PrehistoryState s)
        {
            if(GeographicHistory(s))return;var c=s.History.Aggregates.Empires.Connections;
            c.Geography=new HistoricalGeographicKnowledge{SinceYear=s.Years,Governments=s.History.Polities.Select(_=>new HistoricalGeographicMap(Patches.Length)).ToArray()};
            // Explicit setup/upgrade: only bounded actual owned port records.
            // Existing culture-wide chart archives are NOT personal world maps.
            foreach(var p in s.History.Polities)if(p.EndedYear<0)BootstrapGeographicMap(s,p.Id);
        }
        void AppendGeographicGovernment(PrehistoryState s,int predecessor)
        {
            if(!GeographicHistory(s))return;var g=s.History.Aggregates.Empires.Connections.Geography;
            var next=new HistoricalGeographicMap(Patches.Length);
            int parent=predecessor>=0?predecessor:s.History.Polities[g.Governments.Length].ParentId;
            if(parent>=0&&parent<g.Governments.Length)
            {
                var inherited=g.Governments[parent];next.Places=inherited.Places.Copy();
                next.Grade=inherited.Grade;next.ReportedYear=inherited.ReportedYear;next.Opportunity=inherited.Opportunity;next.Host=inherited.Host;next.Shore=inherited.Shore;
                // Institutions inherit working maps, not duplicate ships,
                // investments, achievements or in-flight expeditions.
            }
            g.Governments=g.Governments.Concat(new[]{next}).ToArray();
        }
        void BootstrapGeographicMap(PrehistoryState s,int polity)
        {
            var p=s.History.Polities[polity];var a=s.History.Aggregates;var map=a.Empires.Connections.Geography.Governments[polity];
            void Offer(int cell){if(cell>=0&&CellPatch[cell]>=0&&DiffusionShore(cell)&&HistoricalCells.Polity(s,cell)==polity)ObserveGeographicCoast(s,map,cell,3,polity);}
            Offer(s.Society.Settlements[p.CapitalId].Cell);
            var cities=a.Polities[polity].Cities;for(int k=0;k<Math.Min(12,cities.Count);k++)Offer(s.Society.Settlements[cities.At(k)].Cell);
            var posts=a.Empires.Governments[polity].AdministrativePosts;for(int k=0;k<Math.Min(8,posts.Count);k++)Offer(a.Empires.Territories.Posts[posts.At(k)].Cell);
        }
        void ObserveGeographicCoast(PrehistoryState s,HistoricalGeographicMap map,int cell,int grade,int observer=-1)
        {
            int region=CellPatch[cell];if(region<0)return;
            int owner=HistoricalCells.Polity(s,cell);double yield=World.Ecology.Habitat[cell].GrowingDays/365.0;
            // A local observation, frozen with its date. Later decisions cannot
            // silently look through that report at current foreign armies/crops.
            double crops=observer>=0?LivingCropLandscape(s,s.History.Polities[observer].CultureId,cell,false):yield;
            double resources=observer>=0?Math.Min(1,LivingStrategicResourceValue(s,observer,cell)/30):0;
            int interest=(int)Math.Round(300+1200*Math.Min(1,crops*3)+500*resources+(World.Ecology.Habitat[cell].Biome==Biome.Ice?-300:200));
            map.Record(region,grade,s.Years,interest,owner,cell);
        }
        void RecordStateVoyageReport(PrehistoryState s,int polity,int cell)
        {
            if(!GeographicHistory(s)||polity<0)return;
            ObserveGeographicCoast(s,s.History.Aggregates.Empires.Connections.Geography.Governments[polity],cell,2,polity);
        }
        void ValidateGeographicKnowledge(PrehistoryState s)
        {
            var g=s.History.Aggregates.Empires.Connections.Geography;if(g==null)return;
            if(!LivingWorldHistory(s)||g.Version!=1||g.SinceYear<0||g.SinceYear>s.Years||g.Governments?.Length!=s.History.Polities.Length)throw new ArgumentException("Invalid geographical knowledge policy.");
            long now=SimulationTime.Seconds(s);
            for(int id=0;id<g.Governments.Length;id++)
            {
                var m=g.Governments[id];if(m?.Places==null||m.Missions==null||m.Missions.Length>8||m.Missions.Count(x=>x.Kind!=HistoricalSeaMission.Report)>4||m.ScheduledDue<0||m.NextPlan<0||m.Voyages<0||m.Surveys<0||m.Landfalls<0||m.FailedVoyages<0)throw new ArgumentException("Invalid geographical map.");
                foreach(var tree in new[]{m.Grade,m.ReportedYear,m.Opportunity,m.Host,m.Shore,m.ReviewAfter})if(tree?.Length!=Patches.Length)throw new ArgumentException("Geographical report alignment.");
                if(!m.Grade.AllPresentValues((r,v)=>v>=1&&v<=3&&m.Places.Positions[r]>=0)||!m.ReviewAfter.AllPresentValues((r,v)=>v>=0))throw new ArgumentException("Unindexed geographical reports.");
                if(m.Places.Count<0||m.Places.Count>Patches.Length)throw new ArgumentException("Invalid known-place index.");
                for(int k=0;k<m.Places.Count;k++)
                {
                    int r=m.Places.At(k);if(r<0||r>=Patches.Length||m.Places.Positions[r]!=k||m.Grade[r]<1||m.Grade[r]>3||m.ReportedYear[r]<g.SinceYear||m.ReportedYear[r]>s.Years||m.Opportunity[r]<0||m.Opportunity[r]>3000||m.Host[r]<-1||m.Host[r]>=s.History.Polities.Length||m.Shore[r]<0||m.Shore[r]>=CellPatch.Length||CellPatch[m.Shore[r]]!=r)throw new ArgumentException("Invalid dated geographical report.");
                }
                foreach(var mission in m.Missions)
                    if(mission==null||mission.Kind<0||mission.Kind>2||mission.Departed<0||mission.Departed>now||mission.Due<=now||mission.Due>SimulationTime.Maximum||mission.Region<0||mission.Region>=Patches.Length||mission.Source<0||mission.Source>=CellPatch.Length||mission.Target<0||mission.Target>=CellPatch.Length||CellPatch[mission.Target]!=mission.Region||mission.DistanceM<0||mission.Investment<0||mission.Reporter<-1||mission.Reporter>=g.Governments.Length||mission.Grade<0||mission.Grade>3||mission.Opportunity<0||mission.Opportunity>3000||mission.Host<-1||mission.Host>=g.Governments.Length||mission.ReportedYear<0||mission.ReportedYear>s.Years||mission.Kind==HistoricalSeaMission.Report&&(mission.Grade<1||mission.Grade>2||mission.Reporter<0))throw new ArgumentException("Invalid scheduled sea mission.");
                var scheduled=s.Timeline.Events.Where(e=>e.Process==SeaMissionProcess&&e.Entity==id).ToArray();
                if(m.ScheduledDue==0?scheduled.Length!=0:scheduled.Length!=1||scheduled[0].Due!=m.ScheduledDue)throw new ArgumentException("Missing dated sea-mission event.");
                if(m.Missions.Length==0&&m.ScheduledDue!=0)throw new ArgumentException("Empty fleet cannot retain a sea-mission event.");
                if(m.Missions.Length>0&&m.ScheduledDue!=m.Missions.Min(x=>x.Due))throw new ArgumentException("Sea missions do not wake at their next actual arrival.");
            }
        }
    }
}

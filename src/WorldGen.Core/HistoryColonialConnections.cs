using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalCoastChart
    {
        public int Region,Year,SourceCulture;
        public HistoricalCoastChart Copy()=>(HistoricalCoastChart)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalCulturalEpisode
    {
        public int Since,Until,WayOfLife;public string Name="",Reason="";
        public HistoricalCulturalEpisode Copy()=>(HistoricalCulturalEpisode)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalCulturalContinuity
    {
        public int PrecursorRegion,LastEvolutionYear,WayOfLife,ColonialSponsor=-1,CivilizationRoot=-1,InstitutionalOriginCulture=-1,DormantSince=-1;
        public HistoricalCoastChart[] Charts=Array.Empty<HistoricalCoastChart>();
        public HistoricalCulturalEpisode[] Episodes=Array.Empty<HistoricalCulturalEpisode>();
        public HistoricalCulturalContinuity Copy(){var c=(HistoricalCulturalContinuity)MemberwiseClone();c.Charts=Charts.Select(x=>x.Copy()).ToArray();c.Episodes=Episodes.Select(x=>x.Copy()).ToArray();return c;}
    }
    [Serializable] public sealed class HistoricalColonialGovernment
    {
        public HistoricalLocalBag Titles=new HistoricalLocalBag();
        public int LastExpeditionYear,Sponsor=-1;
        public HistoricalColonialGovernment Copy(){var c=(HistoricalColonialGovernment)MemberwiseClone();c.Titles=Titles.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalRecognizedTitle
    {
        public int Id,Region,Holder,Recognizer,EstablishedYear,LastTransferYear,PreviousHolder=-1;
        public long AreaKm2,LastPrice;public string Basis="";
        public HistoricalRecognizedTitle Copy()=>(HistoricalRecognizedTitle)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalColonialConnections
    {
        public HistoricalCellTree CellRegions,TitleAt,FamilyRoots;
        public long[] RegionAreas=Array.Empty<long>();
        public HistoricalCulturalContinuity[] Cultures=Array.Empty<HistoricalCulturalContinuity>();
        public HistoricalColonialGovernment[] Governments=Array.Empty<HistoricalColonialGovernment>();
        public HistoricalRecognizedTitle[] Titles=Array.Empty<HistoricalRecognizedTitle>();
        public HistoricalColonialConnections Copy()=>new HistoricalColonialConnections{CellRegions=CellRegions,TitleAt=TitleAt,FamilyRoots=FamilyRoots,RegionAreas=(long[])RegionAreas.Clone(),Cultures=Cultures.Select(c=>c.Copy()).ToArray(),Governments=Governments.Select(g=>g.Copy()).ToArray(),Titles=Titles.Select(t=>t.Copy()).ToArray()};
    }
    public static class HistoricalRecognizedClaims
    {
        public static int TitleAt(PrehistoryState s,int cell)
        {var c=s?.History?.Aggregates?.Empires?.Connections;if(c==null||cell<0||cell>=c.CellRegions.Length)return -1;int r=c.CellRegions[cell];return r<0?-1:c.TitleAt[r];}
        public static int Holder(PrehistoryState s,int cell)
        {int id=TitleAt(s,cell);return id<0?-1:s.History.Aggregates.Resolve(s.History.Aggregates.Empires.Connections.Titles[id].Holder,s.History);}
        public static int Civilization(PrehistoryState s,int culture)
        {var c=s?.History?.Aggregates?.Empires?.Connections;return c!=null&&culture>=0&&culture<c.Cultures.Length&&c.Cultures[culture].CivilizationRoot>=0?c.Cultures[culture].CivilizationRoot:culture;}
    }
    public sealed partial class Prehistory
    {
        public const string ColonialHistoryVersion="history-26";
        static bool ColonialHistory(PrehistoryState s)=>s?.History?.Version==ColonialHistoryVersion;
        public PrehistoryState UpgradeToColonialHistory(PrehistoryState source)
        {
            Validate(source);if(ColonialHistory(source))return source;source=UpgradeToProvincialFrontierHistory(source);
            var s=source.Copy();s.History.Version=ColonialHistoryVersion;var a=s.History.Aggregates;
            var c=new HistoricalColonialConnections{CellRegions=HistoricalCellTree.From(CellPatch.Length,i=>CellPatch[i]),TitleAt=new HistoricalCellTree(Patches.Length),FamilyRoots=new HistoricalCellTree(33554432),RegionAreas=new long[Patches.Length],
                Cultures=s.Society.Cultures.Select(x=>new HistoricalCulturalContinuity{PrecursorRegion=a.Cultures[x.Id].Region,LastEvolutionYear=s.Years,CivilizationRoot=a.Cultures[x.Id].CivilizationSince>=0?x.Id:-1}).ToArray(),
                Governments=s.History.Polities.Select(_=>new HistoricalColonialGovernment()).ToArray()};
            // Explicit setup only. Neither research nor claim events scan terrain.
            for(int i=0;i<CellPatch.Length;i++)if(CellPatch[i]>=0)c.RegionAreas[CellPatch[i]]+=(long)Math.Round(TerritorialPoliticalArea(i));
            a.Empires.Connections=c;foreach(var culture in s.Society.Cultures)if(c.Cultures[culture.Id].CivilizationRoot>=0&&c.FamilyRoots[s.History.Cultures[culture.Id].LanguageId]<0)c.FamilyRoots=c.FamilyRoots.With(s.History.Cultures[culture.Id].LanguageId,culture.Id);s.Seal();return s;
        }
        void AppendColonialCulture(PrehistoryState s,int region)
        {if(ColonialHistory(s)){var c=s.History.Aggregates.Empires.Connections;c.Cultures=c.Cultures.Concat(new[]{new HistoricalCulturalContinuity{PrecursorRegion=region,LastEvolutionYear=s.Years}}).ToArray();}}
        void AppendColonialGovernment(PrehistoryState s,int predecessor=-1)
        {if(ColonialHistory(s)){var c=s.History.Aggregates.Empires.Connections;c.Governments=c.Governments.Concat(new[]{predecessor<0?new HistoricalColonialGovernment():c.Governments[predecessor].Copy()}).ToArray();if(predecessor>=0)c.Governments[predecessor]=new HistoricalColonialGovernment();}}
        void RecordCoastChart(PrehistoryState s,int culture,int region,int source)
        {
            if(!ColonialHistory(s)||culture<0||region<0)return;var c=s.History.Aggregates.Empires.Connections.Cultures[culture];
            if(c.Charts.Any(x=>x.Region==region))return;
            c.Charts=c.Charts.Skip(Math.Max(0,c.Charts.Length-23)).Concat(new[]{new HistoricalCoastChart{Region=region,Year=s.Years,SourceCulture=source}}).ToArray();
            HistoryEvent(s,"charted-coast",s.Society.Cultures[culture].Name+" obtains sailing directions to "+ColonialRegionName(region)+" through "+(source==culture?"an actual voyage.":"maritime exchange with "+s.Society.Cultures[source].Name+"."),Patches[region].Cell,culture,category:ChronicleCategory.Discoveries);
        }
        string ColonialRegionName(int region)=>Patches[region].Region>=0?World.Ecology.Regions[Patches[region].Region].Name:"the outer island shores";
        void EstablishCivilizationalLineage(PrehistoryState s,int culture,int city)
        {
            if(!ColonialHistory(s))return;var connections=s.History.Aggregates.Empires.Connections;var record=connections.Cultures[culture];
            if(record.InstitutionalOriginCulture<0)
            {
                int teacher=s.History.Towns[city].CivicTeacherPolityId;if(teacher<0)teacher=record.ColonialSponsor;
                int source=teacher>=0?s.History.Polities[teacher].CultureId:culture;
                record.InstitutionalOriginCulture=source!=culture&&connections.Cultures[source].InstitutionalOriginCulture>=0?connections.Cultures[source].InstitutionalOriginCulture:source;
            }
            if(record.CivilizationRoot>=0)return;int language=s.History.Cultures[culture].LanguageId,root=connections.FamilyRoots[language];
            record.CivilizationRoot=root>=0?root:culture;if(root<0)connections.FamilyRoots=connections.FamilyRoots.With(language,culture);
        }
        void ShareCoastCharts(PrehistoryState s,int first,int second)
        {
            if(!ColonialHistory(s))return;var records=s.History.Aggregates.Empires.Connections.Cultures;
            foreach(var pair in new[]{(first,second),(second,first)})
            {var charts=records[pair.Item1].Charts;if(charts.Length>0){int k=(int)(HistoryRoll("charted-route-exchange-"+pair.Item2,pair.Item1,s.Years)*charts.Length);RecordCoastChart(s,pair.Item2,charts[k].Region,pair.Item1);}}
        }
        double CulturalRecognitionMaturity(PrehistoryState s,int region)
        {
            // A configurable model horizon for recognizable regional traditions,
            // not a claim that real humans lacked culture or intelligence earlier.
            double center=80000+70000*Rng.Value(World.Recipe.Seed,"regional-tradition-horizon",0)+12000*(Rng.Value(World.Recipe.Seed,"regional-tradition-variation",region)-.5);
            return D.Q(1/(1+Math.Exp((center-s.Years)/4500)),1000000000);
        }
        void AdvanceCulturalContinuity(PrehistoryState s,int id)
        {
            if(!ColonialHistory(s))return;var a=s.History.Aggregates;var c=s.Society.Cultures[id];var record=a.Empires.Connections.Cultures[id];
            int way=HistoricalTechnologies.Has(s.History.Cultures[id],HistoricalTechnology.Radio)?5:HistoricalTechnologies.Has(s.History.Cultures[id],HistoricalTechnology.Printing)?4:a.Cultures[id].CivilizationSince>=0?3:c.Farming>=180?2:c.Fishing>=300?1:0;
            if(c.Population==0&&a.Cultures[id].Cities.Count==0&&c.AreaKm2==0){if(record.DormantSince<0){record.DormantSince=s.Years;HistoryEvent(s,"cultural-disappearance",c.Name+" no longer has recorded surviving communities; its traditions remain in the historical record.",c.OriginCell,id,category:ChronicleCategory.Society);}return;}
            record.DormantSince=-1;
            if(way!=record.WayOfLife&&s.Years-record.LastEvolutionYear>=1200)
            {
                string old=c.Name;record.Episodes=record.Episodes.Concat(new[]{new HistoricalCulturalEpisode{Name=old,Since=record.LastEvolutionYear,Until=s.Years,WayOfLife=record.WayOfLife,Reason=way>=2?"Changed cultivation, settlement and institutions":"Changed regional livelihoods"}}).ToArray();
                c.Name=HistoricalName("cultural-renewal",unchecked(id*7919+s.Years));record.LastEvolutionYear=s.Years;record.WayOfLife=way;
                HistoryEvent(s,"cultural-transformation",old+" develops into the regional tradition now called "+c.Name+" as livelihoods and institutions change. Its communities and ancestry continue rather than appearing independently.",c.OriginCell,id,category:ChronicleCategory.Society);
            }
        }
        void ValidateColonialConnections(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires?.Connections;
            if(!ColonialHistory(s)){if(c!=null)throw new ArgumentException("Legacy colonial extension.");return;}
            if(c?.Cultures?.Length!=s.Society.Cultures.Length||c.Governments?.Length!=s.History.Polities.Length||c.CellRegions?.Length!=CellPatch.Length||c.TitleAt?.Length!=Patches.Length||c.FamilyRoots?.Length!=33554432||c.RegionAreas?.Length!=Patches.Length)throw new ArgumentException("Colonial connection alignment.");
            for(int i=0;i<c.Cultures.Length;i++){var r=c.Cultures[i];if(r.PrecursorRegion<0||r.PrecursorRegion>=Patches.Length||r.CivilizationRoot>=c.Cultures.Length||r.CivilizationRoot<-1||r.ColonialSponsor>=c.Governments.Length||r.ColonialSponsor<-1||r.Charts==null||r.Charts.Length>24||r.Episodes==null||r.LastEvolutionYear>s.Years||r.DormantSince>s.Years)throw new ArgumentException("Cultural continuity record.");foreach(var chart in r.Charts)if(chart.Region<0||chart.Region>=Patches.Length||chart.SourceCulture<0||chart.SourceCulture>=c.Cultures.Length||chart.Year>s.Years)throw new ArgumentException("Invalid coast chart.");}
            for(int i=0;i<c.Titles.Length;i++){var t=c.Titles[i];if(t.Id!=i||t.Region<0||t.Region>=Patches.Length||t.Holder<0||t.Holder>=c.Governments.Length||t.Recognizer<0||t.Recognizer>=c.Governments.Length||t.PreviousHolder<-1||t.PreviousHolder>=c.Governments.Length||t.Holder==t.Recognizer||t.EstablishedYear<0||t.EstablishedYear>s.Years||t.LastTransferYear<t.EstablishedYear||t.LastTransferYear>s.Years||t.AreaKm2!=c.RegionAreas[t.Region]||t.LastPrice<0||string.IsNullOrWhiteSpace(t.Basis)||c.TitleAt[t.Region]!=i)throw new ArgumentException("Invalid recognized title.");}
            var titleOwners=new int[c.Titles.Length];
            for(int i=0;i<c.Governments.Length;i++)
            {
                var g=c.Governments[i];if(g==null||g.Titles==null||g.Titles.Count<0||g.Titles.Count>c.Titles.Length||g.Sponsor<-1||g.Sponsor>=i||g.LastExpeditionYear<0||g.LastExpeditionYear>s.Years)throw new ArgumentException("Invalid colonial government.");
                for(int k=0;k<g.Titles.Count;k++){int id=g.Titles.At(k);if(id<0||id>=c.Titles.Length||g.Titles.Positions[id]!=k||++titleOwners[id]>1)throw new ArgumentException("Invalid recognized title index.");int holder=s.History.Aggregates.Resolve(c.Titles[id].Holder,s.History);if(holder>=0&&holder!=i)throw new ArgumentException("Title indexed under a different living holder.");}
            }
            for(int i=0;i<titleOwners.Length;i++)if(titleOwners[i]!=1)throw new ArgumentException("Missing recognized title index.");
            for(int i=0;i<c.Cultures.Length;i++)
            {
                var r=c.Cultures[i];if(r.InstitutionalOriginCulture<-1||r.InstitutionalOriginCulture>=c.Cultures.Length||r.LastEvolutionYear<0||r.DormantSince<-1||r.WayOfLife<0||r.WayOfLife>5||r.Charts.Select(x=>x.Region).Distinct().Count()!=r.Charts.Length)throw new ArgumentException("Invalid cultural continuity.");
                int until=0;foreach(var e in r.Episodes){if(e==null||string.IsNullOrWhiteSpace(e.Name)||e.Since<until||e.Until<e.Since||e.Until>r.LastEvolutionYear||e.WayOfLife<0||e.WayOfLife>5)throw new ArgumentException("Invalid cultural episode.");until=e.Until;}
                foreach(var chart in r.Charts)if(chart.Year<0)throw new ArgumentException("Negative chart date.");
            }
            var areas=new long[Patches.Length];for(int i=0;i<CellPatch.Length;i++){if(c.CellRegions[i]!=CellPatch[i])throw new ArgumentException("Changed claim geography.");if(CellPatch[i]>=0)areas[CellPatch[i]]+=(long)Math.Round(TerritorialPoliticalArea(i));}
            for(int i=0;i<areas.Length;i++)if(c.RegionAreas[i]!=areas[i]||c.TitleAt[i]<-1||c.TitleAt[i]>=c.Titles.Length||c.TitleAt[i]>=0&&c.Titles[c.TitleAt[i]].Region!=i)throw new ArgumentException("Changed regional title extent.");
        }
    }
}

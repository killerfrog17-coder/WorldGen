using System;
using System.Linq;
namespace WorldGen.Core
{
    // Knowledge is not installed productive capacity. Both are persisted, and
    // research effort is a conserved budget shared by all concurrent projects.
    [Serializable] public sealed class HistoricalTechnicalPractice
    {
        public int[] Levels=new int[37],LevelYear=new int[37],InstalledYear=new int[37],Deployment=new int[37],Allocation=new int[37],Offers=new int[37],OfferYear=new int[37],Sources=Enumerable.Repeat(-1,37).ToArray();
        public long[] Progress=new long[37],Learning=new long[37];
        public long LastResearchBudget,LastLearningBudget,LastResearchSpent,LastLearningSpent;
        public int LastYear;
        public HistoricalTechnicalPractice Copy()
        {
            // Do not allocate ten default arrays only to immediately discard
            // them in an object initializer during every checkpoint copy.
            var c=(HistoricalTechnicalPractice)MemberwiseClone();c.Levels=(int[])Levels.Clone();c.LevelYear=(int[])LevelYear.Clone();c.InstalledYear=(int[])InstalledYear.Clone();c.Deployment=(int[])Deployment.Clone();c.Allocation=(int[])Allocation.Clone();c.Offers=(int[])Offers.Clone();c.OfferYear=(int[])OfferYear.Clone();c.Sources=(int[])Sources.Clone();c.Progress=(long[])Progress.Clone();c.Learning=(long[])Learning.Clone();return c;
        }
    }
    public readonly struct HistoricalTechnicalRequirement
    {
        public readonly int Technology,Level;
        public HistoricalTechnicalRequirement(HistoricalTechnology technology,int level){Technology=(int)technology;Level=level;}
    }
    public static class HistoricalTechnicalGraph
    {
        public const int Count=37;
        public const int MaximumLevel=8;
        public static HistoricalTechnicalRequirement R(HistoricalTechnology t,int level)=>new HistoricalTechnicalRequirement(t,level);
        static readonly HistoricalTechnicalRequirement[][] requirements={
            Array.Empty<HistoricalTechnicalRequirement>(),new[]{R(HistoricalTechnology.Storage,1)},new[]{R(HistoricalTechnology.Storage,1)},new[]{R(HistoricalTechnology.Storage,1)},
            new[]{R(HistoricalTechnology.Cultivation,2),R(HistoricalTechnology.Pottery,1)},new[]{R(HistoricalTechnology.Pottery,2)},new[]{R(HistoricalTechnology.Storage,2),R(HistoricalTechnology.Pottery,2)},
            new[]{R(HistoricalTechnology.Copper,2),R(HistoricalTechnology.Pottery,2)},new[]{R(HistoricalTechnology.Cultivation,2)},new[]{R(HistoricalTechnology.Copper,3),R(HistoricalTechnology.Pottery,3)},
            new[]{R(HistoricalTechnology.Writing,2),R(HistoricalTechnology.Copper,2)},new[]{R(HistoricalTechnology.Roads,2),R(HistoricalTechnology.Iron,2)},new[]{R(HistoricalTechnology.Iron,3),R(HistoricalTechnology.Mills,2)},
            new[]{R(HistoricalTechnology.Sailing,3),R(HistoricalTechnology.Storage,2)},new[]{R(HistoricalTechnology.Writing,3),R(HistoricalTechnology.Mills,2),R(HistoricalTechnology.Coinage,2)},
            new[]{R(HistoricalTechnology.Writing,4),R(HistoricalTechnology.Mills,3),R(HistoricalTechnology.Scholasticism,3)},new[]{R(HistoricalTechnology.Iron,4),R(HistoricalTechnology.Scholasticism,3)},
            new[]{R(HistoricalTechnology.Navigation,4),R(HistoricalTechnology.Sailing,4),R(HistoricalTechnology.Storage,3)},new[]{R(HistoricalTechnology.Scholasticism,4),R(HistoricalTechnology.Printing,2)},new[]{R(HistoricalTechnology.Cultivation,4),R(HistoricalTechnology.Mills,2)},
            new[]{R(HistoricalTechnology.Steel,3),R(HistoricalTechnology.ScientificMethod,2),R(HistoricalTechnology.Mills,4)},new[]{R(HistoricalTechnology.SteamPower,2),R(HistoricalTechnology.Steel,4)},
            new[]{R(HistoricalTechnology.SteamPower,2),R(HistoricalTechnology.IndustrialMetallurgy,2),R(HistoricalTechnology.Roads,3)},new[]{R(HistoricalTechnology.ScientificMethod,3),R(HistoricalTechnology.Printing,3)},
            new[]{R(HistoricalTechnology.GermTheory,1),R(HistoricalTechnology.Roads,3)},new[]{R(HistoricalTechnology.ScientificMethod,3),R(HistoricalTechnology.IndustrialMetallurgy,2)},
            new[]{R(HistoricalTechnology.Electricity,1),R(HistoricalTechnology.Printing,3)},new[]{R(HistoricalTechnology.ScientificMethod,3),R(HistoricalTechnology.IndustrialMetallurgy,2),R(HistoricalTechnology.CropRotation,2)},
            new[]{R(HistoricalTechnology.SteamPower,3),R(HistoricalTechnology.IndustrialMetallurgy,3)},new[]{R(HistoricalTechnology.CombustionEngines,2),R(HistoricalTechnology.ScientificMethod,3)},
            new[]{R(HistoricalTechnology.Electricity,2),R(HistoricalTechnology.Telegraph,2)},new[]{R(HistoricalTechnology.GermTheory,2)},new[]{R(HistoricalTechnology.GermTheory,3),R(HistoricalTechnology.IndustrialMetallurgy,3)},
            new[]{R(HistoricalTechnology.CombustionEngines,2),R(HistoricalTechnology.CropRotation,3)},new[]{R(HistoricalTechnology.Electricity,3),R(HistoricalTechnology.Radio,2)},
            new[]{R(HistoricalTechnology.Electronics,2),R(HistoricalTechnology.Printing,3)},new[]{R(HistoricalTechnology.Computing,2),R(HistoricalTechnology.Radio,3)}
        };
        // Material/experimental work, not a prescribed Earth year. Further
        // levels grow in cost; prerequisites constrain the possible paths.
        static readonly int[] costs={80,140,100,140,230,210,380,370,200,540,270,350,480,430,600,620,650,620,720,430,900,650,600,650,420,700,350,500,650,680,420,420,650,450,550,650,450};
        static readonly int[] useful=BuildUsefulLevels();
        static int[] BuildUsefulLevels(){var result=Enumerable.Repeat(2,Count).ToArray();foreach(var branch in requirements)foreach(var r in branch)result[r.Technology]=Math.Max(result[r.Technology],r.Level);return result;}
        public static int UsefulLevel(int technology)=>useful[technology];
        public static HistoricalTechnicalRequirement[] Requirements(int technology)=>requirements[technology];
        public static long Cost(int technology,int nextLevel,string historyVersion=null)
        {
            long result=costs[technology]*1000000L;for(int level=1;level<nextLevel;level++)result=checked((result*33+10)/20);
            if(historyVersion!=Prehistory.ResearchPacingHistoryVersion&&historyVersion!=Prehistory.LanguageHistoryVersion&&historyVersion!=Prehistory.EnvironmentalHistoryVersion)return result;
            // Material difficulty, not a date lock. Imported learning retains
            // its independent one-eighth cost and real-contact requirements.
            int numerator=technology==(int)HistoricalTechnology.Bronze?65:technology==(int)HistoricalTechnology.Iron?(nextLevel==1?400:nextLevel==2?250:165):
                technology==(int)HistoricalTechnology.Steel?115:technology==(int)HistoricalTechnology.Scholasticism?120:
                technology==(int)HistoricalTechnology.SteamPower||technology==(int)HistoricalTechnology.IndustrialMetallurgy?110:100;
            return checked((result*numerator+50)/100);
        }
        public static bool Prerequisites(int technology,int[] levels)
        {foreach(var r in requirements[technology])if(levels[r.Technology]<r.Level)return false;return true;}
        public static bool Military(int k)=>k==7||k==9||k==12||k==16||k==29;
        public static bool WeaponsOnly(int k)=>k==16;
        public static bool Medical(int k)=>k==23||k==24||k==31||k==32;
        public static bool Agricultural(int k)=>k==1||k==4||k==19||k==27||k==33;
        public static bool Maritime(int k)=>k==3||k==13||k==17;
        // Traditional blue-water navigation predates industrial materials and
        // is possible without literacy. A medieval technology limit must not
        // erase that independent craft tradition.
        public static bool WithinCeiling(int k,HistoricalTechnologyCeiling ceiling)
            =>k==(int)HistoricalTechnology.OceanNavigation||HistoricalModernEffects.Era((HistoricalTechnology)k)<=ceiling;
        public static double Installed(HistoricalCulture culture,int technology)=>culture.Technical==null?0:culture.Technical.Deployment[technology]/1000.0;
    }
    public sealed partial class Prehistory
    {
        public const string TechnicalHistoryVersion="history-24";
        static bool TechnicalHistory(PrehistoryState s)=>s?.History?.Version==TechnicalHistoryVersion||ProvincialFrontierHistory(s);
        public PrehistoryState UpgradeToTechnicalHistory(PrehistoryState source)
        {
            Validate(source);if(TechnicalHistory(source))return source;
            source=UpgradeToAgronomicHistory(source);var s=source.Copy();s.History.Version=TechnicalHistoryVersion;
            foreach(var c in s.History.Cultures)InitializeTechnicalPractice(c,s.Years,true);
            InitializeImperialSystem(s);
            s.Seal();return s;
        }
        static void InitializeTechnicalPractice(HistoricalCulture culture,int year,bool migration=false)
        {
            if(culture.Technical!=null)return;var t=new HistoricalTechnicalPractice{LastYear=year};culture.Technical=t;
            for(int k=0;k<HistoricalTechnicalGraph.Count;k++)if(culture.Technology[k]>0)
            {t.Levels[k]=1;t.LevelYear[k]=culture.Technology[k]-1;t.Deployment[k]=(int)Math.Round(D.Clamp((year-culture.Technology[k]+2)/80.0)*1000);if(t.Deployment[k]>=250)t.InstalledYear[k]=Math.Min(year+1,culture.Technology[k]+19);}
            // Migration does not invent supporting discoveries or rewrite their
            // dates. Further improvements must meet the new prerequisites.
            if(migration)for(int k=0;k<HistoricalTechnicalGraph.Count;k++)if(culture.Technology[k]>0)
                foreach(var r in HistoricalTechnicalGraph.Requirements(k))if(culture.Technology[r.Technology]>0&&t.Levels[r.Technology]<r.Level)
                {t.Levels[r.Technology]=r.Level;t.LevelYear[r.Technology]=year;}
        }
        void ValidateTechnicalPractice(PrehistoryState s,HistoricalCulture c)
        {
            var t=c.Technical;
            if(!TechnicalHistory(s)){if(t!=null)throw new ArgumentException("Legacy technical extension.");return;}
            int n=HistoricalTechnicalGraph.Count;
            if(t==null||t.Levels?.Length!=n||t.LevelYear?.Length!=n||t.InstalledYear?.Length!=n||t.Deployment?.Length!=n||t.Allocation?.Length!=n||t.Offers?.Length!=n||t.OfferYear?.Length!=n||t.Sources?.Length!=n||t.Progress?.Length!=n||t.Learning?.Length!=n||t.LastYear>s.Years||t.LastResearchSpent>t.LastResearchBudget||t.LastLearningSpent>t.LastLearningBudget||t.LastResearchSpent<0||t.LastLearningSpent<0)throw new ArgumentException("Invalid technical accounts.");
            int allocations=0;
            for(int k=0;k<n;k++)
            {
                allocations+=t.Allocation[k];
                if(t.Levels[k]<0||t.Levels[k]>HistoricalTechnicalGraph.MaximumLevel||t.Deployment[k]<0||t.Deployment[k]>1000||t.Allocation[k]<0||t.Allocation[k]>10000||t.Progress[k]<0||t.Learning[k]<0||t.Offers[k]<0||t.Offers[k]>HistoricalTechnicalGraph.MaximumLevel||t.OfferYear[k]<0||t.OfferYear[k]>s.Years||t.LevelYear[k]<0||t.LevelYear[k]>s.Years||t.InstalledYear[k]<0||t.InstalledYear[k]>s.Years+1||t.Sources[k]<-1||t.Sources[k]>=s.History.Cultures.Length||t.Levels[k]>0&&c.Technology[k]==0||t.Levels[k]==0&&t.Deployment[k]>0)throw new ArgumentException("Invalid leveled technical practice "+k+".");
            }
            if(allocations>10000)throw new ArgumentException("Research allocations exceed the available budget.");
        }
    }
}

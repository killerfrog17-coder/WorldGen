using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed class ForagerTechnique
    {
        public readonly int Id;public readonly string Name;
        public readonly int[] Prerequisites;
        public readonly double InnovationYears,TeachingYears,PracticeYears,LossYears;
        public ForagerTechnique(int id,string name,int[] prerequisites,double innovation,double teaching=180,double practice=100,double loss=1400)
        {Id=id;Name=name;Prerequisites=prerequisites;InnovationYears=innovation;TeachingYears=teaching;PracticeYears=practice;LossYears=loss;}
    }
    public static class ForagerTechnology
    {
        // First24 IDs retain the owner's current item-tech correspondence.
        // Bows need good wood/cordage and working skill, not an obligatory
        // pressure-flaked stone arrowhead: bone/wood points are alternatives.
        public static readonly ForagerTechnique[] All={
            new ForagerTechnique(0,"Flake cutting",Array.Empty<int>(),12000,60,35,12000),
            new ForagerTechnique(1,"Prepared cores",new[]{0},95000),
            new ForagerTechnique(2,"Scraping",new[]{0},20000,90,60,4500),
            new ForagerTechnique(3,"Wooden spears",new[]{0},25000,90,70,7000),
            new ForagerTechnique(4,"Spearhead hafting",new[]{0,3,7},180000,220,200,1800),
            new ForagerTechnique(5,"Fire tending",Array.Empty<int>(),60000,80,80,5000),
            new ForagerTechnique(6,"Hide working",new[]{2},45000,140,140,3500),
            new ForagerTechnique(7,"Plant binding",Array.Empty<int>(),35000,100,100,5000),
            new ForagerTechnique(8,"Hide wraps",new[]{6},65000,120,100,2500),
            new ForagerTechnique(9,"Fire making",new[]{3,5},250000,250,220,1600),
            new ForagerTechnique(10,"Compound adhesives",new[]{4,5},420000,300,250,900),
            new ForagerTechnique(11,"Bone awls",new[]{0,6},160000,150,120,1800),
            new ForagerTechnique(12,"Hide sewing",new[]{6,7,11},240000,230,180,1500),
            new ForagerTechnique(13,"Fitted clothing",new[]{8,12},350000,250,200,1500),
            new ForagerTechnique(14,"Insulated clothing",new[]{13},500000,300,240,1600),
            new ForagerTechnique(15,"Bone points",new[]{0,11},220000,180,180,1400),
            new ForagerTechnique(16,"Pressure flaking",new[]{1,11},480000,280,260,1100),
            new ForagerTechnique(17,"Bows",new[]{0,3,7},850000,350,260,1200),
            new ForagerTechnique(18,"Barbed points",new[]{15},350000,250,200,1200),
            new ForagerTechnique(19,"Fishing lines",new[]{7,11},260000,180,140,1000),
            new ForagerTechnique(20,"Rafts",new[]{3,7},350000,250,220,1300),
            new ForagerTechnique(21,"Eyed needles",new[]{11,12},620000,280,220,1300),
            new ForagerTechnique(22,"Spear throwers",new[]{3,7,15},700000,300,250,1200),
            new ForagerTechnique(23,"Food preservation",new[]{0,5},220000,200,180,1600),
            new ForagerTechnique(24,"Fishing nets",new[]{7,19},650000,300,240,900),
            new ForagerTechnique(25,"Snares and traps",new[]{3,7},350000,190,160,1200),
            new ForagerTechnique(26,"Grinding tools",new[]{0},260000,160,120,1600),
            new ForagerTechnique(27,"Plant processing",new[]{0,5},70000,100,80,4500),
            new ForagerTechnique(28,"Baskets and woven carriers",new[]{7},200000,170,130,1900),
            new ForagerTechnique(29,"Water containers",new[]{6,7},260000,200,170,1600),
            new ForagerTechnique(30,"Boats",new[]{3,7,20},850000,400,300,900),
            new ForagerTechnique(31,"Cold-weather shelters",new[]{3,6,7},250000,220,170,1800),
            new ForagerTechnique(32,"Food storage",new[]{23,28},500000,250,220,1300)
        };
        public static int Count=>All.Length;
        public static double Prerequisites(double[] skill,int technique)
        {double value=1;foreach(int p in All[technique].Prerequisites)value=Math.Min(value,skill[p]);return value;}
        public static double Materials(ForagerDistrict d,ForagerEnvironment e,int k)
        {
            double wood=D.Clamp(d.Wood*(.2+.8*D.Clamp((e.SummerC-2)/16))),fiber=D.Clamp(d.Fiber*(.3+.7*e.Moisture)),stone=d.Stone;
            switch(k)
            {
                case 0:case 1:case 2:case 16:return .25+.75*stone;
                case 3:case 9:case 20:case 22:case 25:case 30:return .1+.9*wood;
                case 7:case 17:case 19:case 24:case 28:return .12+.88*fiber;
                case 10:return .1+.9*d.Resin;
                case 6:case 8:case 11:case 12:case 13:case 14:case 15:case 18:case 21:case 29:return .12+.88*D.Clamp(e.Food[1]+e.Food[2]);
                case 26:return .55+.45*stone;
                case 27:return .1+.9*D.Clamp(e.Food[0]);
                default:return .4+.6*D.Clamp(wood+fiber);
            }
        }
        public static double Demand(ForagerDistrict d,ForagerEnvironment e,ForagerCohort c,int k)
        {
            double cold=D.Clamp((12-e.WinterC)/32),aquatic=D.Clamp(e.Food[3]+e.Food[4]),forest=D.Clamp(e.Canopy),season=D.Clamp(d.Seasonality[0]+e.Drought*.6);
            switch(k)
            {
                case 0:return 1;case 1:return .5+.35*d.Stone;case 2:case 3:case 4:return .6+.35*D.Clamp(e.Food[1]+e.Food[2]);
                case 5:return .6+.4*cold;case 6:return .45+.5*cold;case 7:return .75;
                case 8:return .12+.86*cold;case 9:return .45+.5*D.Clamp(e.Moisture);
                case 10:return .15+.65*D.Clamp(c.Use.Length>4?c.Use[4]:0);
                case 11:return .25+.5*cold+.15*aquatic;case 12:case 13:case 14:case 21:case 31:return .025+.95*cold*cold;
                case 15:return .25+.45*aquatic;case 16:return .15+.55*d.Stone;
                case 17:return .15+.6*forest+.15*D.Clamp(e.Food[1]);case 18:case 19:case 24:return .02+.96*aquatic;
                case 20:return .03+.9*D.Clamp(d.Coast+d.River);case 22:return .12+.8*(1-forest)*D.Clamp(e.Food[2]);
                case 23:case 32:return .06+.85*season;case 25:return .16+.65*D.Clamp(e.Food[1]);
                case 26:case 27:return .2+.7*D.Clamp(e.Food[0]);case 28:return .5+.35*D.Clamp(e.Food[0]);
                case 29:return .25+.7*D.Clamp(1-e.Water);case 30:return .01+.95*d.Coast;default:return .5;
            }
        }
        public static void Advance(int seed,ForagerCohort c,ForagerDistrict d,ForagerEnvironment e,ForagerCulture culture,double years,int year,double[] teachers,double connectedAdults)
        {
            for(int k=0;k<Count;k++)
            {
                var t=All[k];double prerequisites=Prerequisites(c.Skill,k),materials=Materials(d,e,k),need=Demand(d,e,c,k);
                double preference=culture.TechniquePreference.Length>k?culture.TechniquePreference[k]:.5;
                double opportunity=D.Clamp((prerequisites-.22)/.48)*(.2+.8*materials)*(.15+.85*need);
                double practitioners=c.Adults*(.1+.9*c.Skill[k]),network=Math.Log(1+Math.Max(0,c.Adults+connectedAdults)/40.0)/4;
                // No inherited discovery clock. Realised innovations are rare
                // opportunities; failing to invent this century does not grant
                // inevitable discovery credit to every descendant everywhere.
                double hazard=(1-Math.Exp(-years/t.InnovationYears))*(.3+1.7*D.Clamp(network,0,2))*opportunity;
                if(Rng.Value(seed,"forager-invention-"+k,unchecked(c.Id*104729+year))<hazard)c.Knowledge[k]=Math.Max(c.Knowledge[k],.7+.2*Rng.Value(seed,"forager-insight-"+k,c.Id^year));
                double teacher=teachers!=null&&teachers.Length>k?teachers[k]:0;
                double localTeaching=D.Clamp(c.Skill[k]*Math.Min(1,practitioners/8));
                double learning=teacher*(.35+.65*culture.ExchangeOpenness)+localTeaching;
                double familiarityTarget=Math.Max(c.Knowledge[k],Math.Max(teacher*.95,c.Skill[k]));
                c.Knowledge[k]=PaleolithicRules.Approach(c.Knowledge[k],familiarityTarget,learning/t.TeachingYears,years);
                if(c.Knowledge[k]>.12&&opportunity>.03)
                {
                    double progress=(.15+.85*c.Knowledge[k])*opportunity*(.12+learning);
                    c.Skill[k]=PaleolithicRules.Approach(c.Skill[k],Math.Min(c.Knowledge[k],prerequisites)*(.72+.28*materials),progress/t.PracticeYears,years);
                }
                double expectedUse=D.Clamp(need*(.65+.7*preference)*materials)*c.Skill[k];
                c.Use[k]=PaleolithicRules.Approach(c.Use[k],expectedUse,1/80.0,years);
                double loss=(1-D.Clamp(c.Use[k]*1.8))*(.25+.75*(1-D.Clamp(learning)))/t.LossYears;
                if(practitioners<3)loss+=1/Math.Max(250,t.LossYears*.35);
                c.Skill[k]*=Math.Exp(-years*loss);c.Knowledge[k]*=Math.Exp(-years*(1-D.Clamp(learning+c.Use[k]))/18000.0);
                c.Knowledge[k]=D.Q(D.Clamp(c.Knowledge[k]),1000000000);c.Skill[k]=D.Q(D.Clamp(Math.Min(c.Skill[k],c.Knowledge[k])),1000000000);c.Use[k]=D.Q(D.Clamp(Math.Min(c.Use[k],c.Skill[k])),1000000000);
                c.Teaching[k]=D.Q(learning,1000000);c.Innovation[k]=D.Q(hazard,1000000000);
            }
        }
        public static bool ValidGraph()
        {
            var marks=new byte[Count];bool Visit(int k)
            {if(k<0||k>=Count||All[k].Id!=k||marks[k]==1)return false;if(marks[k]==2)return true;marks[k]=1;foreach(int p in All[k].Prerequisites)if(!Visit(p))return false;marks[k]=2;return true;}
            for(int k=0;k<Count;k++)if(!Visit(k))return false;return true;
        }
    }
}

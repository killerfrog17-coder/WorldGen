using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed class ForagerTechnique
    {
        public readonly int Id;public readonly string Name;
        public readonly int[] Prerequisites;
        public readonly int[][] AlternativePaths;
        public readonly double InnovationYears,TeachingYears,PracticeYears,LossYears;
        public ForagerTechnique(int id,string name,int[] prerequisites,double innovation,double teaching=180,double practice=100,double loss=1400,int[][] alternatives=null)
        {Id=id;Name=name;Prerequisites=prerequisites;AlternativePaths=alternatives??Array.Empty<int[]>();InnovationYears=innovation;TeachingYears=teaching;PracticeYears=practice;LossYears=loss;}
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
            new ForagerTechnique(3,"Woodworking and wooden spears",new[]{0},25000,90,70,7000),
            new ForagerTechnique(4,"Spearhead hafting",new[]{0,3,7},180000,220,200,1800),
            new ForagerTechnique(5,"Fire tending",Array.Empty<int>(),60000,80,80,5000),
            new ForagerTechnique(6,"Hide working",new[]{2},45000,140,140,3500),
            new ForagerTechnique(7,"Binding and cordage",Array.Empty<int>(),35000,100,100,5000),
            new ForagerTechnique(8,"Hide wraps",new[]{6},65000,120,100,2500),
            new ForagerTechnique(9,"Fire making",new[]{3,5},250000,250,220,1600),
            new ForagerTechnique(10,"Compound adhesives",new[]{0,5,7},420000,300,250,900),
            new ForagerTechnique(11,"Piercing tools and bone awls",new[]{0},160000,150,120,1800),
            new ForagerTechnique(12,"Sewing",new[]{7,11},240000,230,180,1500),
            new ForagerTechnique(13,"Fitted clothing",new[]{12},350000,250,200,1500,new[]{new[]{8},new[]{36}}),
            new ForagerTechnique(14,"Insulated clothing",new[]{13},500000,300,240,1600),
            new ForagerTechnique(15,"Bone points",new[]{0,11},220000,180,180,1400),
            new ForagerTechnique(16,"Pressure flaking",new[]{1,3},480000,280,260,1100),
            new ForagerTechnique(17,"Bows",new[]{0,3,7},850000,350,260,1200),
            new ForagerTechnique(18,"Barbed points",new[]{15},350000,250,200,1200),
            new ForagerTechnique(19,"Fishing lines and gorges",new[]{3,7},260000,180,140,1000),
            new ForagerTechnique(20,"Rafts",new[]{3,7},350000,250,220,1300),
            new ForagerTechnique(21,"Eyed needles",new[]{11,12},620000,280,220,1300),
            new ForagerTechnique(22,"Spear throwers",new[]{0,3},700000,300,250,1200),
            new ForagerTechnique(23,"Food preservation",new[]{0,5},220000,200,180,1600),
            new ForagerTechnique(24,"Fishing nets",new[]{7,19},650000,300,240,900),
            new ForagerTechnique(25,"Snares and traps",new[]{3,7},350000,190,160,1200),
            new ForagerTechnique(26,"Grinding tools",new[]{0},260000,160,120,1600),
            new ForagerTechnique(27,"Plant processing",new[]{0,5},70000,100,80,4500),
            new ForagerTechnique(28,"Baskets and woven carriers",new[]{7},200000,170,130,1900),
            new ForagerTechnique(29,"Water containers",new[]{7},260000,200,170,1600,new[]{new[]{6},new[]{3}}),
            new ForagerTechnique(30,"Boats",new[]{3},850000,400,300,900,new[]{new[]{7,20},new[]{7,29},new[]{5,38}}),
            new ForagerTechnique(31,"Cold-weather shelters",new[]{3,6,7},250000,220,170,1800),
            new ForagerTechnique(32,"Food storage",new[]{23,28},500000,250,220,1300),
            new ForagerTechnique(33,"Bifacial hand tools",new[]{0},60000,110,90,6000),
            new ForagerTechnique(34,"Shelter frames",new[]{0,3},45000,100,90,5000),
            new ForagerTechnique(35,"Hide coverings",new[]{6},70000,110,90,3500),
            new ForagerTechnique(36,"Woven mats",new[]{7},190000,160,140,1900),
            new ForagerTechnique(37,"Fishhooks",new[]{0,19},550000,250,200,1200),
            new ForagerTechnique(38,"Ground-edge axes",new[]{0,4,26},750000,300,260,1800),
            new ForagerTechnique(39,"Fired clay vessels",new[]{0,5,26},1400000,380,300,1200),
            new ForagerTechnique(40,"Slings",new[]{7},650000,280,220,1500),
            new ForagerTechnique(41,"Personal adornment",new[]{0,7},260000,150,120,2500),
            new ForagerTechnique(42,"Pigment preparation",new[]{0},150000,130,110,3000)
        };
        public static int Count=>All.Length;
        public static double Prerequisites(double[] skill,int technique)
        {var t=All[technique];double value=1;foreach(int p in t.Prerequisites)value=Math.Min(value,skill[p]);if(t.AlternativePaths.Length>0){double best=0;foreach(var path in t.AlternativePaths){double available=1;foreach(int p in path)available=Math.Min(available,skill[p]);best=Math.Max(best,available);}value=Math.Min(value,best);}return value;}
        public static string Requirements(int technique)
        {var t=All[technique];string common=string.Join(", ",t.Prerequisites.Select(k=>All[k].Name));string alternatives=string.Join(" OR ",t.AlternativePaths.Select(path=>"("+string.Join(" + ",path.Select(k=>All[k].Name))+")"));return common+(common.Length>0&&alternatives.Length>0?" + ":"")+alternatives;}
        public static double Materials(ForagerDistrict d,ForagerEnvironment e,int k)
        {
            double wood=D.Clamp(Math.Max(d.Wood,e.Canopy*.85+.1*D.Clamp(e.Aridity/.6))*(.2+.8*D.Clamp((e.SummerC-2)/16))),fiber=D.Clamp(Math.Max(d.Fiber,.5*D.Clamp(e.Aridity/.8)+.3*d.River+.1*e.Canopy)*(.3+.7*e.Moisture)),stone=d.Stone;
            switch(k)
            {
                case 0:case 1:case 2:case 16:case 33:case 38:case 42:return .25+.75*stone;
                case 3:case 9:case 20:case 22:case 25:case 30:case 34:return .1+.9*wood;
                case 7:return .12+.88*Math.Max(fiber,D.Clamp(e.Food[1]+e.Food[2])*.6);
                case 17:case 19:case 24:case 28:case 36:case 40:return .12+.88*fiber;
                case 10:return .1+.9*d.Resin;
                case 6:case 8:case 11:case 12:case 13:case 14:case 15:case 18:case 21:case 29:case 35:case 37:return .12+.88*D.Clamp(e.Food[1]+e.Food[2]);
                case 39:return D.Clamp(.12+.7*d.River+.18*D.Clamp((d.Aridity-.5)/1.5));
                case 41:return .25+.75*Math.Max(stone,Math.Max(fiber,d.Coast));
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
                case 11:return .25+.5*cold+.15*aquatic;
                case 12:return D.Clamp(.12+.7*cold*cold+.12*(c.Use[28]+c.Use[29]));
                case 21:return D.Clamp(.025+.75*cold*cold+.18*c.Use[12]);
                case 13:case 14:case 31:return .025+.95*cold*cold;
                case 15:return .25+.45*aquatic;case 16:return .15+.55*d.Stone;
                case 17:return .15+.6*forest+.15*D.Clamp(e.Food[1]);case 18:case 19:case 24:return .02+.96*aquatic;
                case 20:return .03+.9*D.Clamp(d.Coast+d.River);case 22:return .12+.8*(1-forest)*D.Clamp(e.Food[2]);
                case 23:case 32:return .06+.85*season;case 25:return .16+.65*D.Clamp(e.Food[1]);
                case 26:case 27:return .2+.7*D.Clamp(e.Food[0]);case 28:return .5+.35*D.Clamp(e.Food[0]);
                case 29:return .25+.7*D.Clamp(1-e.Water);case 30:return .01+.95*d.Coast;
                case 33:return .35+.45*D.Clamp(e.Food[1]+e.Food[2]);
                case 34:return .25+.6*cold;case 35:return .15+.7*cold;case 36:return .25+.3*e.Moisture+.2*season;
                case 37:return .01+.97*aquatic;case 38:return .1+.7*forest;
                case 39:return .05+.45*season+.35*aquatic;case 40:return .15+.55*D.Clamp(e.Food[1])+.2*(1-forest);
                case 41:case 42:return .15+.5*c.Connectivity;default:return .5;
            }
        }
        public static ulong Advance(int seed,ForagerCohort c,ForagerDistrict d,ForagerEnvironment e,ForagerCulture culture,double years,int year,double[] teachers,double connectedAdults,double contactStrength=1,double explanationPractice=1)
        {
            ulong inventions=0;
            for(int k=0;k<Count;k++)
            {
                var t=All[k];double prerequisites=Prerequisites(c.Skill,k),materials=Materials(d,e,k),need=Demand(d,e,c,k);
                double preference=culture.TechniquePreference.Length>k?culture.TechniquePreference[k]:.5;
                double foundation=D.Clamp((prerequisites-.22)/.48);
                double opportunity=foundation*(.2+.8*materials)*(.15+.85*need);
                double practitioners=c.Adults*c.Use[k],network=Math.Max(0,c.Adults+connectedAdults*.2)/2500.0;
                // No inherited discovery clock. Realised innovations are rare
                // opportunities; failing to invent this century does not grant
                // inevitable discovery credit to every descendant everywhere.
                double hazard=1-Math.Exp(-years/t.InnovationYears*network*opportunity);
                if(Rng.Value(seed,"forager-invention-"+k,unchecked(c.Id*104729+year))<hazard){if(c.Knowledge[k]<.5)inventions|=1UL<<k;c.Knowledge[k]=Math.Max(c.Knowledge[k],.7+.2*Rng.Value(seed,"forager-insight-"+k,c.Id^year));}
                double teacher=teachers!=null&&teachers.Length>k?teachers[k]:0;
                double localTeaching=D.Clamp(c.Skill[k]*Math.Min(1,practitioners/8));
                double learning=teacher*(.35+.65*culture.ExchangeOpenness)*D.Clamp(contactStrength)+localTeaching;
                learning*=.55+.45*D.Clamp(explanationPractice);
                // Repeated successful practice can refine a familiar method.
                // Material scarcity reduces opportunity and use; it does not
                // multiply down every descendant technique's maximum skill.
                double practice=D.Clamp(need*(.65+.7*preference)*materials);
                double familiarityTarget=Math.Max(c.Knowledge[k],Math.Max(teacher*.98,c.Skill[k]));
                if(c.Knowledge[k]>.35&&foundation>.2)familiarityTarget=Math.Max(familiarityTarget,c.Knowledge[k]+.15*practice);
                c.Knowledge[k]=PaleolithicRules.Approach(c.Knowledge[k],familiarityTarget,learning/t.TeachingYears,years);
                if(c.Knowledge[k]>.12&&opportunity>.03)
                {
                    double progress=(.15+.85*c.Knowledge[k])*foundation*practice*(.12+learning);
                    c.Skill[k]=PaleolithicRules.Approach(c.Skill[k],c.Knowledge[k]*foundation,progress/t.PracticeYears,years);
                }
                double expectedUse=practice*c.Skill[k];
                c.Use[k]=PaleolithicRules.Approach(c.Use[k],expectedUse,1/80.0,years);
                double loss=(1-D.Clamp(c.Use[k]*1.8))*(.25+.75*(1-D.Clamp(learning)))/t.LossYears;
                if(practitioners<3)loss+=1/Math.Max(250,t.LossYears*.35);
                c.Skill[k]*=Math.Exp(-years*loss);c.Knowledge[k]*=Math.Exp(-years*(1-D.Clamp(learning+c.Use[k]))/18000.0);
                c.Knowledge[k]=D.Q(D.Clamp(c.Knowledge[k]),1000000000);c.Skill[k]=D.Q(D.Clamp(Math.Min(c.Skill[k],c.Knowledge[k])),1000000000);c.Use[k]=D.Q(D.Clamp(Math.Min(c.Use[k],c.Skill[k])),1000000000);
                c.Teaching[k]=D.Q(learning,1000000);c.Innovation[k]=D.Q(hazard,1000000000);
            }
            return inventions;
        }
        public static bool ValidGraph()
        {
            var marks=new byte[Count];bool Visit(int k)
            {if(k<0||k>=Count||All[k].Id!=k||marks[k]==1)return false;if(marks[k]==2)return true;marks[k]=1;foreach(int p in All[k].Prerequisites.Concat(All[k].AlternativePaths.SelectMany(path=>path)))if(!Visit(p))return false;marks[k]=2;return true;}
            for(int k=0;k<Count;k++)if(!Visit(k))return false;return true;
        }
    }
}

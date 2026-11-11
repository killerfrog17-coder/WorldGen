using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    // The first eight channels retain the regional history model's meanings.
    // Additional axes are inherited variation, not racial categories or climate
    // diagnoses. No appearance axis is an ability/personality statistic.
    public enum HumanFormTrait
    {
        Skin,Hair,Iris,Curl,Compactness,Face,Nose,Stature,
        Undertone,RedHair,IrisHue,FaceWidth,JawWidth,Cheekbones,
        NoseWidth,NoseProjection,LipVolume,EyeOpening,EyeTilt,EyelidFold,
        BrowRidge,ChinProjection,LimbProportion,Build
    }
    public enum HumanHairStyle { Cropped,LooseShort,LooseLong,TiedBack,Braided,Shaved }
    public enum HumanBeardStyle { None,Short,Full }

    [Serializable] public sealed class HumanPhenotype
    {
        public int Version=1,People=-1,Lineage=-1;
        public string PeopleName="",Species="Homo sapiens";
        public double[] Traits=Array.Empty<double>();
        public double AdultStatureM=1.7,AdultMassKg=65;
        public HumanPhenotype Copy(){var p=(HumanPhenotype)MemberwiseClone();p.Traits=(double[])Traits.Clone();return p;}
        public double Trait(HumanFormTrait trait)=>Traits.Length>(int)trait?Traits[(int)trait]:.5;
        public void Validate()
        {
            if(Version!=1||Traits.Length!=HumanPhenotypes.Count||Traits.Any(v=>!double.IsFinite(v)||v<0||v>1)||!double.IsFinite(AdultStatureM)||AdultStatureM<.85||AdultStatureM>2.35||!double.IsFinite(AdultMassKg)||AdultMassKg<18||AdultMassKg>180)
                throw new ArgumentException("Invalid inherited human appearance.");
        }
    }
    [Serializable] public sealed class HumanGrooming
    {
        public HumanHairStyle Hair;public HumanBeardStyle Beard;
        public HumanGrooming Copy()=>(HumanGrooming)MemberwiseClone();
    }
    public static class HumanPhenotypes
    {
        public const int Count=24;
        static double Unit(int seed,string field,long id,int channel=0)=>Rng.Value(seed,field+channel,unchecked((int)id^(int)(id>>32)));
        static double Normal(int seed,long id,int channel)
        {double n=0;for(int k=0;k<6;k++)n+=Unit(seed,"inherited-normal-"+channel+"-",id,k)-.5;return n/Math.Sqrt(.5);}
        public static double Founder(int seed,HomininLineage lineage,int district,int channel)
        {
            double ancestry=lineage.Ancestry.Length>channel%8?lineage.Ancestry[channel%8]:.5;
            double value=.5+(ancestry-.5)*.65+(Unit(seed,"ancestral-form-",lineage.OriginDistrict,channel)-.5)*.45;
            if(channel==(int)HumanFormTrait.JawWidth)value+=lineage.JawBreadth-.5;
            if(channel==(int)HumanFormTrait.NoseProjection)value+=(lineage.FaceProjection-.4)*.5;
            if(channel==(int)HumanFormTrait.BrowRidge)value+=lineage.BrowRidge-.5;
            if(channel==(int)HumanFormTrait.LimbProportion)value+=lineage.LimbRatio-.5;
            // Red pigmentation is a separate low-frequency axis. It is not
            // prescribed by latitude, skin colour or a modern named ethnicity.
            if(channel==(int)HumanFormTrait.RedHair)value=.06+.14*Unit(seed,"ancestral-red-",lineage.OriginDistrict);
            return D.Clamp(value+(Unit(seed,"regional-founder-form-",district,channel)-.5)*.12);
        }
        public static double[] Complete(double[] legacy,int seed,HomininLineage lineage,int district)
        {
            if(legacy?.Length==Count)return legacy;
            var traits=new double[Count];
            for(int k=0;k<Count;k++)traits[k]=legacy!=null&&k<legacy.Length?legacy[k]:Founder(seed,lineage,district,k);
            return traits;
        }
        public static double Drift(double prior,int seed,int cohort,int year,int channel,int elapsed,double population)
        {
            if(elapsed<=0)return prior;
            double effective=D.Clamp(population*.1,60,10000);
            double sigma=.065*Math.Sqrt(elapsed/28.0/(2*effective));
            double change=Normal(seed,((long)cohort<<32)^(uint)year,channel)*sigma;
            return D.Q(D.Clamp(prior+change),1000000);
        }
        public static HumanPhenotype Sample(int seed,ForagerCohort cohort,HomininLineage lineage,ForagerPeopleIdentity people,ForagerPersonRecord record,IReadOnlyDictionary<long,ForagerPersonRecord> relatives)
        {
            var mean=Complete(cohort.Appearance,seed,lineage,cohort.District);
            // Sample biological parents, never foster-household membership.
            // One bounded generation supplies family resemblance without a
            // global person ledger or a recursive genealogy walk.
            double Parent(long id,int channel)
            {
                if(id<0)return mean[channel];
                return D.Clamp(mean[channel]+Normal(seed,id,channel)*Spread(channel));
            }
            var values=new double[Count];
            for(int k=0;k<Count;k++)
            {
                bool hasParents=record.Mother>=0||record.Father>=0;
                values[k]=D.Q(D.Clamp(hasParents?(Parent(record.Mother,k)+Parent(record.Father,k))*.5+Normal(seed,record.Id,k)*Spread(k)*.55:Parent(record.Id,k)),1000000);
            }
            double sizeRatio=lineage.StatureM/1.7,statureMean=lineage.StatureM+(record.Female?-.065:.065)*sizeRatio;
            // Population means vary modestly inside sapiens; species baselines
            // carry the much larger inter-species difference. Nutrition affects
            // realised growth, rather than changing inherited potential.
            double stature=D.Clamp(statureMean+(values[7]-.5)*.50*sizeRatio,lineage.StatureM*.78,lineage.StatureM*1.22);
            double mass=lineage.AdultMassKg*Math.Pow(stature/Math.Max(.85,lineage.StatureM),2)*(record.Female?.87:1.05)*(.84+.32*values[23]);
            return new HumanPhenotype{People=people?.Id??-1,PeopleName=people?.Name??"",Lineage=lineage.Id,Species=lineage.Name,Traits=values,AdultStatureM=D.Q(D.Clamp(stature,.85,2.35),1000000),AdultMassKg=D.Q(D.Clamp(mass,18,180),1000000)};
        }
        static double Spread(int channel)=>channel==0?.045:channel==1||channel==2?.065:channel==7?.13:.11;
        public static HumanGrooming Grooming(int seed,ForagerCulture culture,ForagerPersonRecord person,double age,double curl)
        {
            // Cultural fashion is a separate distribution. Every texture can
            // be worn long, cropped, tied or braided. Gender norms are local,
            // and neither skin tone nor political ownership chooses a style.
            int cultureSeed=culture?.Seed??seed;
            double braid=Unit(cultureSeed,"grooming-braid-preference-",0),longHair=Unit(cultureSeed,"grooming-length-preference-",0),shave=Unit(cultureSeed,"grooming-shave-preference-",0);
            double rank=Unit(seed,"personal-hair-style-",person.Id);
            double[] weights={.22+.5*(1-longHair),.25,.15+.7*longHair,.15+.6*longHair,.1+.9*braid,.015+.18*shave};
            if(age<3){weights[0]+=.8;weights[4]*=.2;}
            double at=rank*weights.Sum();int style=0;for(;style<weights.Length-1;style++){at-=weights[style];if(at<0)break;}
            double beardNorm=Unit(cultureSeed,"grooming-beard-preference-",0),beard=Unit(seed,"personal-beard-style-",person.Id);
            return new HumanGrooming{Hair=(HumanHairStyle)style,Beard=person.Female||age<18?HumanBeardStyle.None:beard<.25+.55*(1-beardNorm)?HumanBeardStyle.None:beard<.8?HumanBeardStyle.Short:HumanBeardStyle.Full};
        }
        public static V SkinColor(HumanPhenotype p)
        {
            double pigment=p.Trait(HumanFormTrait.Skin),warm=p.Trait(HumanFormTrait.Undertone)-.5;
            // sRGB material colour, with a continuous range and bounded
            // undertones. Surface detail remains in the existing skin texture.
            var light=new V(.86,.67,.53);var dark=new V(.19,.105,.072);double x=Math.Pow(pigment,.83);
            return new V(D.Clamp(light.X*(1-x)+dark.X*x+warm*.035),D.Clamp(light.Y*(1-x)+dark.Y*x+warm*.012),D.Clamp(light.Z*(1-x)+dark.Z*x-warm*.012));
        }
        public static V HairColor(HumanPhenotype p,double age)
        {
            double pigment=p.Trait(HumanFormTrait.Hair),red=p.Trait(HumanFormTrait.RedHair);
            double light=Math.Pow(1-pigment,2.0);var color=new V(.027+.7*light+red*.15,.020+.49*light+red*.025,.014+.22*light);
            double grey=D.Clamp((age-35)/55)*(.35+.65*p.Trait(HumanFormTrait.Undertone));
            return color*(1-grey)+new V(.58,.55,.51)*grey;
        }
        public static V IrisColor(HumanPhenotype p)
        {
            double pigment=p.Trait(HumanFormTrait.Iris),hue=p.Trait(HumanFormTrait.IrisHue);
            V low=new V(.20,.39,.51)*(1-hue)+new V(.30,.40,.19)*hue,high=new V(.12,.062,.025);
            double dark=D.Clamp((pigment-.2)/.65);return low*(1-dark)+high*dark;
        }
        public static string VisibleAppearance(HumanPhenotype p,HumanGrooming grooming,double age)
        {
            double pigment=p.Trait(HumanFormTrait.Hair),grey=D.Clamp((age-35)/55)*(.35+.65*p.Trait(HumanFormTrait.Undertone));
            string color=grey>.65?"grey":grey>.28?"greying":p.Trait(HumanFormTrait.RedHair)>.6?"reddish-brown":pigment>.78?"black":pigment>.5?"dark brown":pigment>.28?"brown":"fair";
            string texture=p.Trait(HumanFormTrait.Curl)>.75?"tightly curled":p.Trait(HumanFormTrait.Curl)>.58?"curly":p.Trait(HumanFormTrait.Curl)>.35?"wavy":"straight";
            string style=grooming?.Hair==HumanHairStyle.Cropped?"cropped":grooming?.Hair==HumanHairStyle.LooseLong?"loose and long":grooming?.Hair==HumanHairStyle.TiedBack?"tied back":grooming?.Hair==HumanHairStyle.Braided?"braided":"loose and short";
            string hair=grooming?.Hair==HumanHairStyle.Shaved?"shaved scalp":color+", "+texture+" hair, "+style;
            double iris=p.Trait(HumanFormTrait.Iris);
            string eyes=iris>.7?"brown":iris>.45?"hazel":p.Trait(HumanFormTrait.IrisHue)>.5?"green":"blue";
            return hair+"; "+eyes+" eyes";
        }
    }
}

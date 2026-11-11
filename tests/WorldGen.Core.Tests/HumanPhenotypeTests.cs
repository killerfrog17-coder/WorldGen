using WorldGen.Core;

static class HumanPhenotypeTests
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Human phenotype: "+label);}
        var lineage=new HomininLineage{Id=0,Name="Homo sapiens",OriginDistrict=3,StatureM=1.7,AdultMassKg=65,Ancestry=Enumerable.Repeat(.5,8).ToArray()};
        var cohort=new ForagerCohort{Id=17,District=8,Identity=5,Culture=1,Appearance=new[]{.65,.82,.8,.7,.5,.5,.5,.5}};
        var people=new ForagerPeopleIdentity{Id=5,Name="River people"};
        var mother=new ForagerPersonRecord{Id=701,Female=true,Household=14};var father=new ForagerPersonRecord{Id=702,Household=14};
        var records=new Dictionary<long,ForagerPersonRecord>{{mother.Id,mother},{father.Id,father}};
        HumanPhenotype Sample(ForagerPersonRecord person)=>HumanPhenotypes.Sample(73,cohort,lineage,people,person,records);
        var mum=Sample(mother);var dad=Sample(father);
        double kin=0,unrelated=0;var identities=new HashSet<string>();var heights=new List<double>();
        for(int i=0;i<1200;i++)
        {
            var person=new ForagerPersonRecord{Id=10000+i,Household=i,Mother=mother.Id,Father=father.Id,Female=i%2==0};var p=Sample(person);p.Validate();
            Check(p.Traits.SequenceEqual(Sample(person).Traits),"repeat generation preserves exact inherited values");
            Check(p.People==5&&p.PeopleName=="River people","social identity carried independently");
            var copy=cohort.Copy();copy.Culture=77;var conquered=HumanPhenotypes.Sample(73,copy,lineage,people,person,records);
            Check(p.Traits.SequenceEqual(conquered.Traits),"culture/political changes cannot recolour an individual");
            person.Household=99999;Check(p.Traits.SequenceEqual(Sample(person).Traits),"fostering cannot rewrite biological ancestry");
            identities.Add(string.Join(",",p.Traits.Select(v=>v.ToString("R"))));heights.Add(p.AdultStatureM);
            for(int k=0;k<HumanPhenotypes.Count;k++)
            {
                double mean=(mum.Traits[k]+dad.Traits[k])*.5;kin+=Math.Abs(p.Traits[k]-mean);
                var stranger=Sample(new ForagerPersonRecord{Id=300000+i});unrelated+=Math.Abs(stranger.Traits[k]-mean);
            }
        }
        Check(identities.Count==1200,"not a handful of repeated preset appearances");
        Check(kin<unrelated*.82,"actual sampled parents predict children's inherited traits");
        Check(heights.Max()-heights.Min()>.13&&heights.Min()>1.4&&heights.Max()<2.05,"bounded individual sapiens stature spread");
        var small=new HomininLineage{Id=4,Name="Homo islandensis",OriginDistrict=9,StatureM=1.06,AdultMassKg=30,Ancestry=lineage.Ancestry};
        var smallPerson=HumanPhenotypes.Sample(73,cohort,small,people,father,records);smallPerson.Validate();
        Check(smallPerson.AdultStatureM<1.3&&smallPerson.AdultStatureM>.9,"small human species retains its actual baseline");
        Check(cohort.Appearance.Length==8,"appearance queries never mutate regional history");
        var inherited=mum.Copy();inherited.Traits[0]=0;Check(mum.Traits[0]!=0,"inherited trait copies own their arrays");
        var memory=new HumanLocalState{People={new HumanPersonMemory{Person=701,Phenotype=mum,Grooming=new HumanGrooming()}}};var saved=memory.Copy();saved.People[0].Phenotype.Traits[1]=0;
        Check(mum.Traits[1]!=0,"local save copies retain separate phenotype ownership");
        var light=mum.Copy();light.Traits[0]=.05;var dark=mum.Copy();dark.Traits[0]=.95;
        Check(HumanPhenotypes.SkinColor(light).X>HumanPhenotypes.SkinColor(dark).X,"continuous pigmentation palette");
        light.Traits[2]=dark.Traits[2];Check(HumanPhenotypes.IrisColor(light).X==HumanPhenotypes.IrisColor(dark).X,"iris colour is not prescribed by skin colour");
        var a=new ForagerCulture{Seed=412};var b=new ForagerCulture{Seed=873};int differentStyles=0;var styles=new HashSet<HumanHairStyle>();
        for(int i=0;i<500;i++)
        {
            var person=new ForagerPersonRecord{Id=9000+i};var style=HumanPhenotypes.Grooming(73,a,person,30,.05);styles.Add(style.Hair);
            Check(style.Hair==HumanPhenotypes.Grooming(73,a,person,30,.95).Hair,"hair texture does not prohibit cultural grooming");
            if(style.Hair!=HumanPhenotypes.Grooming(73,b,person,30,.05).Hair)differentStyles++;
            Check(HumanPhenotypes.Grooming(73,a,person,7,.5).Beard==HumanBeardStyle.None,"children have no adult beard");
        }
        Check(styles.Count==6&&differentStyles>40,"cultures have overlapping, distinct hairstyle distributions");
        Console.WriteLine($"PASS {checks} inherited phenotype, family, species, cultural grooming and ownership controls");
    }
}

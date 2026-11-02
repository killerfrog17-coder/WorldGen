using WorldGen.Core;
static class ForagerAffiliationTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var a=ForagerAffiliations.Single(0);var b=ForagerAffiliations.Single(1);var c=ForagerAffiliations.Single(2);
        var mixed=ForagerAffiliations.Blend(ForagerAffiliations.Blend(a,60,b,30),90,c,10);
        Check(mixed.Length==3&&Math.Abs(ForagerAffiliations.Share(mixed,0)-.6)<1e-12&&Math.Abs(ForagerAffiliations.Share(mixed,1)-.3)<1e-12&&Math.Abs(ForagerAffiliations.Share(mixed,2)-.1)<1e-12,"Third arrivals preserve both existing populations in their actual proportions");
        var reversed=ForagerAffiliations.Blend(c,10,ForagerAffiliations.Blend(b,30,a,60),90);
        Check(mixed.Zip(reversed).All(p=>p.First.Id==p.Second.Id&&Math.Abs(p.First.Share-p.Second.Share)<1e-12),"Migrant mixing is independent of arrival order when masses are unchanged");
        var replaced=ForagerAffiliations.Replace(mixed,0,3);
        Check(ForagerAffiliations.Share(replaced,1)==.3&&ForagerAffiliations.Share(replaced,2)==.1&&Math.Abs(replaced.Sum(x=>x.Share)-1)<1e-12,"Local dialect branching retains unrelated residents");
        var branch=ForagerAffiliations.Transfer(mixed,0,3,.02);
        Check(branch.Length==4&&Math.Abs(ForagerAffiliations.Share(branch,3)-.012)<1e-12,"A new ritual tradition starts among households instead of instantly replacing the district");
        var target=ForagerAffiliations.ContactNorm(a,30,new[]{(b,1000d,.8)},.8);
        var step=ForagerAffiliations.Approach(a,target,1-Math.Exp(-250/1400d),30);
        Check(ForagerAffiliations.Share(step,1)>0&&ForagerAffiliations.Share(step,1)<.2,"Contact adoption is gradual rather than a one-tick majority replacement");
        for(int i=0;i<40;i++){target=ForagerAffiliations.ContactNorm(step,30,new[]{(b,1000d,.8)},.8);step=ForagerAffiliations.Approach(step,target,1-Math.Exp(-250/1400d),30);}
        Check(ForagerAffiliations.Share(step,1)>.8,"Repeated local contact can produce genuine language shift rather than permanent decorative minorities");
        var isolated=ForagerAffiliations.ContactNorm(a,30,Array.Empty<(ForagerAffiliation[],double,double)>(),.8);
        Check(isolated.Length==1&&isolated[0].Id==0,"Isolation cannot import a distant language through a global popularity count");
        Check(ForagerAffiliations.Pick(mixed,.59)==0&&ForagerAffiliations.Pick(mixed,.61)==1&&ForagerAffiliations.Pick(mixed,.95)==2,"Stable local band sampling represents every resident affiliation");
        var cohort=new ForagerCohort{Speech=mixed,Traditions=branch};ForagerAffiliations.Refresh(cohort);var copy=cohort.Copy();copy.Speech[0].Share=0;
        Check(cohort.Language==0&&cohort.Belief==0&&cohort.Speech[0].Share==.6,"Derived majority caches and deep-copy isolation retain canonical data");
        var f=new ForagerState{Lineages=new[]{new HomininLineage{Id=0,SeparationYears=300000},new HomininLineage{Id=1,SeparationYears=600000}}};
        Check(ForagerSociality.ReproductiveContact(f,new ForagerCohort{Lineage=0},new ForagerCohort{Lineage=0})==1&&ForagerSociality.ReproductiveContact(f,new ForagerCohort{Lineage=0},new ForagerCohort{Lineage=1})>0&&ForagerSociality.ReproductiveContact(f,new ForagerCohort{Lineage=0},new ForagerCohort{Lineage=1})<.2,"Assortative cross-lineage contact permits rare admixture without treating trade as universal interbreeding");
        Console.WriteLine($"PASS {checks} affiliation, contact-shift and lineage checks");
    }
}

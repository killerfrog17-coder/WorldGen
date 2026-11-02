using WorldGen.Core;
static class ForagerLanguageTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var root=ForagerLanguages.Create(451,0,0,0);var state=new ForagerState{Languages=new[]{root},NextLanguage=1};ForagerLanguages.ReviewFamilies(state,0,null);
        var a=ForagerLanguages.Create(451,1,1,0,root);var b=ForagerLanguages.Create(451,2,2,0,root);state.Languages=new[]{root,a,b};state.NextLanguage=3;
        Check(a.Family==b.Family&&ForagerLanguages.SharedRoots(a,b)==1,"New daughter communities inherit actual cognates and family ancestry");
        Check(ForagerLanguages.Word(a,"water").Text==ForagerLanguages.Word(b,"water").Text,"Inherited words are genuinely shared, not only matching family labels");
        double early=0;
        for(int year=250;year<=40000;year+=250)
        {
            ForagerLanguages.Evolve(451,a,year,250,1,0);ForagerLanguages.Evolve(451,b,year,250,1,0);ForagerLanguages.ReviewFamilies(state,year,null);
            if(year==2000)early=ForagerLanguages.Intelligibility(a,b);
        }
        double late=ForagerLanguages.Intelligibility(a,b);
        Check(early>late+.25&&late<.25,"Long-isolated populations become unintelligible rather than slight perpetual dialects");
        Check(a.Family!=b.Family&&a.Family!=root.Family&&b.Family!=root.Family,"Forty millennia of isolation produce distinct recognisable families");
        Check(state.LanguageFamilies[a.Family].Parent>=0&&state.LanguageFamilies[b.Family].Parent>=0,"Deep genealogical ancestry remains recorded separately from recognisable family membership");
        Check(ForagerLanguages.Concepts.Select(c=>ForagerLanguages.Word(a,c).Text).Zip(ForagerLanguages.Concepts.Select(c=>ForagerLanguages.Word(b,c).Text)).Count(p=>p.First==p.Second)<ForagerLanguages.Concepts.Length/3,"The words themselves diverge, not only map colours or names");
        var isolated=ForagerLanguages.Create(451,3,3,0);var connected=isolated.Copy();connected.Id=4;connected.Sounds.Id=4;
        var donor=ForagerLanguages.Create(451,5,5,0);
        for(int year=250;year<=10000;year+=250)
        {ForagerLanguages.Evolve(451,isolated,year,250,.7,0);ForagerLanguages.Evolve(451,connected,year,250,.2,1,donor);}
        Check(ForagerLanguages.SharedRoots(connected,donor)>ForagerLanguages.SharedRoots(isolated,donor)+.12,"Sustained contact changes real vocabulary and supports bilingual understanding");
        Check(a.Roots.Length==ForagerLanguages.Concepts.Length&&a.Sounds.SoundMap.Length==32,"Deep history has a bounded current lexicon and sound map");
        var copy=a.Copy();copy.Roots[0]=0;Check(a.Roots[0]!=0,"Published language copies isolate inherited lexical ancestry");
        var ancestral=ForagerLanguages.Create(11,0,0,0);var cousins=new ForagerState{Languages=new[]{ancestral}};ForagerLanguages.ReviewFamilies(cousins,0,null);
        var left=ForagerLanguages.Create(11,1,1,500,ancestral);var right=ForagerLanguages.Create(11,2,2,500,ancestral);cousins.Languages=new[]{ancestral,left,right};
        foreach(var speech in cousins.Languages)for(int k=0;k<speech.Roots.Length;k++)speech.Roots[k]=100000+k;
        ForagerLanguages.ReviewFamilies(cousins,8000,null);
        Check(cousins.Languages.Select(x=>x.Family).Distinct().Count()==1&&cousins.LanguageFamilies.Length==2,"Uncontacted cousin and minority varieties share a recognisable younger family instead of each inventing a separate one");
        Check(cousins.LanguageFamilies[cousins.Languages[0].Family].Parent==0,"Corrected classification preserves remote family ancestry rather than deleting it");
        Console.WriteLine($"PASS {checks} forager language-history checks; 2ky intelligibility={early:F3}; 40ky={late:F3}; recognised families={state.LanguageFamilies.Length}; contact cognates={ForagerLanguages.SharedRoots(connected,donor):F3}");
    }
}

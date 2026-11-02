using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class PaleolithicLanguage
    {
        static readonly int[][] shifts={new[]{0,1,9,10},new[]{2,3,11,12,23},new[]{4,5,19,20},new[]{6,7,8},new[]{13,21,22,24},new[]{14,17,18},new[]{15,16},new[]{25,26,27,28,29,30,31}};
        static readonly string[] vocabulary={"water","fire","stone","wood","food","hide","spear","speech","people","deer","go","we"};
        public static LanguageProfile Evolve(LanguageProfile source,int year)
        {
            var p=source.Copy();p.ChangedYear=year;p.Revision++;
            var random=new Rng(p.FamilySeed,"paleo-living-speech-1",unchecked(p.Id*7919+p.Revision*104729));
            // Bounded regular chain shifts: exchanging output phonemes retains
            // contrast, unlike a finite list of one-way mergers that eventually
            // makes all branches identical. No growing ancestry walk or lexicon.
            var group=shifts[random.Next()%(uint)shifts.Length];int a=group[random.Next()%(uint)group.Length],b;
            do{b=group[random.Next()%(uint)group.Length];}while(b==a);
            int Shift(int sound)=>sound==a?b:sound==b?a:sound;
            for(int i=0;i<p.SoundMap.Length;i++)p.SoundMap[i]=Shift(p.SoundMap[i]);
            foreach(var loan in p.Loans)for(int i=0;i<loan.Sounds.Length;i++)loan.Sounds[i]=Shift(loan.Sounds[i]);
            if(random.Next()%24==0)p.WordOrder=new[]{"SOV","SVO","VSO"}[random.Next()%3];
            if(random.Next()%32==0)p.ModifierFirst=!p.ModifierFirst;
            p.CodaPercent=Math.Max(0,Math.Min(65,p.CodaPercent+(random.Next()%2==0?3:-3)));
            if(random.Next()%3==0)
            {
                string concept=vocabulary[random.Next()%(uint)vocabulary.Length];var sounds=new int[6];
                for(int i=0;i<2;i++){sounds[i*3]=-2;sounds[i*3+1]=p.SoundMap[p.Onsets[random.Next()%(uint)p.Onsets.Length]];sounds[i*3+2]=p.SoundMap[p.Vowels[random.Next()%(uint)p.Vowels.Length]];}
                var word=new LanguageLoan{Concept=concept,SourceId=p.Id,Year=year,Sounds=sounds};int slot=Array.FindIndex(p.Loans,l=>l.Concept==concept);
                if(slot>=0)p.Loans[slot]=word;else if(p.Loans.Length<LanguageGenerator.MaximumLoans)p.Loans=p.Loans.Concat(new[]{word}).ToArray();
            }
            return p;
        }
        public static LanguageProfile Branch(int id,int year,LanguageProfile parent)
        {
            var p=parent.Copy();p.Id=id;p.ParentId=parent.Id;p.FoundedYear=p.ChangedYear=year;p.Revision=0;
            p=Evolve(p,year);p.Revision=0;return p;
        }
    }
}

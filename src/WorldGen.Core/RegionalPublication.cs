using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class RegionalChanges<T> where T:class
    {
        public int Length;public int[] Indices;public T[] Values;
        public static RegionalChanges<T> Between(T[] before,T[] after)
        {var ids=Enumerable.Range(0,after.Length).Where(i=>i>=before.Length||!ReferenceEquals(before[i],after[i])).ToArray();return new RegionalChanges<T>{Length=after.Length,Indices=ids,Values=ids.Select(i=>after[i]).ToArray()};}
        public T[] Apply(T[] before)
        {var result=new T[Length];Array.Copy(before,result,Math.Min(before.Length,Length));for(int i=0;i<Indices.Length;i++)result[Indices[i]]=Values[i];return result;}
    }
    [Serializable] public sealed class RegionalBiotaUpdate
    {
        public int District,ReviewedYear;public double ForestCover;public double[] FoodAvailability;
        public static RegionalBiotaUpdate[] Between(ForagerBiotaDistrict[] before,ForagerBiotaDistrict[] after)
            =>Enumerable.Range(0,after.Length).Where(i=>!ReferenceEquals(before[i],after[i])).Select(i=>new RegionalBiotaUpdate{District=i,ReviewedYear=after[i].ReviewedYear,ForestCover=after[i].ForestCover,FoodAvailability=after[i].FoodAvailability}).ToArray();
        public void Apply(ForagerBiotaDistrict[] rows)
        {var old=rows[District];rows[District]=new ForagerBiotaDistrict{District=District,ReviewedYear=ReviewedYear,ForestCover=ForestCover,FoodAvailability=FoodAvailability,Taxa=old.Taxa,Abundance=old.Abundance,BaselineFood=old.BaselineFood};}
    }
    // Transient replication of a committed regional publication. It contains
    // changed regional records, never terrain or procedurally generated camps.
    [Serializable] public sealed class RegionalPublication
    {
        public long From,Seconds,Boundary,NextReview,ProcessedEvents;
        public int NextCohort,NextCulture,NextLanguage,NextBelief,NextFamily,NextPeople;
        public RegionalQueueNode Queue;
        public long[] PopulationAt,ProcessAt;public int[] Residents;public double[] Species;
        public RegionalChanges<ForagerCohort> People;
        public RegionalChanges<ForagerResources> Resources;
        public RegionalBiotaUpdate[] Biota;
        public RegionalChanges<ForagerCulture> Cultures;
        public RegionalChanges<ForagerPeopleIdentity> Identities;
        public RegionalChanges<ForagerLanguage> Languages;
        public RegionalChanges<ForagerLanguageFamily> Families;
        public RegionalChanges<ForagerBelief> Beliefs;
        public ForagerEvent[] Events;public ForagerDiscovery[] Discoveries;
        public static RegionalPublication Between(PrehistoryState before,PrehistoryState after)
        {
            var a=before.Foragers;var b=after.Foragers;
            return new RegionalPublication{From=a.Seconds,Seconds=b.Seconds,Boundary=b.BoundarySeconds,NextReview=b.NextReviewSeconds,ProcessedEvents=b.ProcessedEvents,NextCohort=b.NextCohort,NextCulture=b.NextCulture,NextLanguage=b.NextLanguage,NextBelief=b.NextBelief,NextFamily=b.NextFamily,NextPeople=b.NextPeople,
                Queue=b.PendingEvents,PopulationAt=b.PopulationAt,ProcessAt=b.ProcessAt,Residents=b.RegionResidents,Species=b.SpeciesShares,
                People=RegionalChanges<ForagerCohort>.Between(a.Cohorts,b.Cohorts),Resources=RegionalChanges<ForagerResources>.Between(a.Resources,b.Resources),Biota=RegionalBiotaUpdate.Between(a.Biota,b.Biota),Cultures=RegionalChanges<ForagerCulture>.Between(a.Cultures,b.Cultures),Identities=RegionalChanges<ForagerPeopleIdentity>.Between(a.Peoples,b.Peoples),Languages=RegionalChanges<ForagerLanguage>.Between(a.Languages,b.Languages),Families=RegionalChanges<ForagerLanguageFamily>.Between(a.LanguageFamilies,b.LanguageFamilies),Beliefs=RegionalChanges<ForagerBelief>.Between(a.Beliefs,b.Beliefs),Events=b.Events,Discoveries=b.Discoveries};
        }
        public PrehistoryState Apply(PrehistoryState before)
        {
            if(before.Foragers.Seconds!=From)throw new InvalidOperationException("Regional publication is out of order.");
            var next=before.CopyClockOnly();next.DomainToken=new object();var f=next.Foragers=before.Foragers.ClockCopy();next.Paleolithic=before.Paleolithic.Copy();
            f.Seconds=Seconds;f.BoundarySeconds=Boundary;f.NextReviewSeconds=NextReview;f.ProcessedEvents=ProcessedEvents;f.PendingEvents=Queue;
            f.NextCohort=NextCohort;f.NextCulture=NextCulture;f.NextLanguage=NextLanguage;f.NextBelief=NextBelief;f.NextFamily=NextFamily;f.NextPeople=NextPeople;
            f.PopulationAt=PopulationAt;f.ProcessAt=ProcessAt;f.RegionResidents=Residents;f.SpeciesShares=Species;
            f.Cohorts=People.Apply(f.Cohorts);f.Resources=Resources.Apply(f.Resources);f.Biota=(ForagerBiotaDistrict[])f.Biota.Clone();foreach(var update in Biota)update.Apply(f.Biota);f.Cultures=Cultures.Apply(f.Cultures);f.Peoples=Identities.Apply(f.Peoples);f.Languages=Languages.Apply(f.Languages);f.LanguageFamilies=Families.Apply(f.LanguageFamilies);f.Beliefs=Beliefs.Apply(f.Beliefs);f.Events=Events;f.Discoveries=Discoveries;
            next.Years=(int)(Seconds/SimulationTime.Year);next.Paleolithic.Seconds=Seconds;next.Paleolithic.BoundarySeconds=Boundary;next.InvalidateHash();return next;
        }
    }
}

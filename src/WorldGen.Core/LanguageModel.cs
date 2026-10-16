using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum LanguageScript { Alphabet, Abjad, Abugida, Syllabary, Logosyllabic }
    public enum LanguageNameKind { People, Settlement, State, Person, House, Region, Continent, Ocean, Sea, River, Lake, Religion }

    // These are procedural language priors, not reconstructed real languages.
    // Sound IDs refer to LanguageGenerator's stable phoneme table, never glyphs.
    [Serializable] public sealed class LanguageProfile
    {
        public string Version="language-1";
        public int Id, FamilyId, FamilySeed, ParentId=-1, FoundedYear, ChangedYear, Revision;
        public int[] Onsets=Array.Empty<int>(), Codas=Array.Empty<int>(), Vowels=Array.Empty<int>(), SoundMap=Array.Empty<int>();
        public int CodaPercent, ClusterPercent, LongWordPercent, Spelling;
        public bool ModifierFirst, JoinedNames, InitialStress;
        public string WordOrder="SOV";
        public LanguageScript Script;
        public LanguageLoan[] Loans=Array.Empty<LanguageLoan>();
        public LanguageProfile Copy()
        {
            var c=(LanguageProfile)MemberwiseClone();c.Onsets=(int[])Onsets.Clone();c.Codas=(int[])Codas.Clone();c.Vowels=(int[])Vowels.Clone();c.SoundMap=(int[])SoundMap.Clone();c.Loans=Loans.Select(x=>x.Copy()).ToArray();return c;
        }
    }
    [Serializable] public sealed class LanguageLoan
    {
        public string Concept="";
        public int SourceId, Year;
        // Already adapted to the recipient; no donor lookup/ancestry walk later.
        public int[] Sounds=Array.Empty<int>();
        public LanguageLoan Copy()=>new LanguageLoan{Concept=Concept,SourceId=SourceId,Year=Year,Sounds=(int[])Sounds.Clone()};
    }
    [Serializable] public sealed class HistoricalLanguages
    {
        public LanguageProfile[] Profiles=Array.Empty<LanguageProfile>();
        public HistoricalLanguages Copy()=>new HistoricalLanguages{Profiles=Profiles.Select(x=>x.Copy()).ToArray()};
    }
    public readonly struct LanguageWord
    {
        public readonly string Text, Phonetics;
        public LanguageWord(string text,string phonetics){Text=text;Phonetics=phonetics;}
        public override string ToString()=>Text;
    }
}

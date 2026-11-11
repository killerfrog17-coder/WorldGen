using System;
using System.Linq;
using System.Text;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class NpcPersonContext
    {
        public static string Describe(string role,ForagerPersonSample person,ForagerBandSample band,bool personallyKnown,string appearance,NpcSocialState? knowledge=null)
        {
            var text=new StringBuilder(role+": ");
            text.Append("name=").Append(personallyKnown?person?.Name??"unknown":"unfamiliar visitor");
            text.Append("; age in years=").Append(personallyKnown&&person!=null?Math.Floor(person.AgeYears).ToString("0",System.Globalization.CultureInfo.InvariantCulture):"unknown");
            text.Append("; visible appearance=").Append(string.IsNullOrEmpty(appearance)?"not available":appearance);
            if(personallyKnown&&person?.Phenotype!=null)text.Append("; people identity=").Append(person.Phenotype.PeopleName);
            if(!personallyKnown||person==null||band==null)return text.Append("; partner identity=unknown; children=unknown.").ToString();
            bool KnownDead(long id)=>knowledge?.KnownPeople.Any(p=>p.Person==id&&p.SeenDead)==true;
            var members=band.Members??Array.Empty<ForagerPersonSample>();var records=band.FamilyRecords??Array.Empty<ForagerPersonRecord>();
            text.Append("; mother identity=").Append(members.FirstOrDefault(p=>p.Id==person.Mother)?.Name??"unknown");
            text.Append("; father identity=").Append(members.FirstOrDefault(p=>p.Id==person.Father)?.Name??"unknown");
            double year=person.BirthYear+person.AgeYears;
            var partner=members.FirstOrDefault(p=>p.Id==person.Partner);
            var partnerRecord=records.FirstOrDefault(p=>p.Id==person.Partner);
            text.Append("; partner identity=").Append(person.Partner<=0?"no partner recorded":partner?.Name??"a partner is recorded but their name is unavailable");
            if(KnownDead(person.Partner)||partnerRecord!=null&&partnerRecord.DeathYear<=year)text.Append(" (deceased)");
            var children=records.Length>0?records.Where(p=>(p.Mother==person.Id||p.Father==person.Id)&&p.BirthYear<=year&&p.DeathYear>year).Select(p=>p.Id).ToArray():members.Where(p=>p.Mother==person.Id||p.Father==person.Id).Select(p=>p.Id).ToArray();
            children=children.Where(id=>!KnownDead(id)).ToArray();
            text.Append("; living children in family records=").Append(children.Length);
            var childNames=members.Where(p=>children.Contains(p.Id)).Select(p=>p.Name).ToArray();
            if(childNames.Length>0)text.Append(" (").Append(string.Join(", ",childNames)).Append(')');
            return text.Append('.').ToString();
        }
    }
}

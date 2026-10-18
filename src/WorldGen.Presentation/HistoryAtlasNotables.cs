using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasLink NotableLink(PrehistoryState s,int id)
        {
            var p=Prehistory.NotablePersonAt(s,id);return new AtlasLink{Kind="notable",Id=id,Cell=p.Cell,Name=p.Name,Detail=PersonRoles(p.Roles),Context=(p.House>=0?"House "+p.HouseName+" · ":"")+"recorded "+Y(s,p.RecordedYear),Historical=p.DeathYear>=0,Value=p.Renown};
        }
        static string PersonRoles(HistoricalPersonRoles roles)=>roles.ToString().Replace(", "," · ").Replace("RoyalFamily","Ruling household").Replace("ReligiousTeacher","Religious teacher").Replace("PropheticTeacher","Prophetic teacher");
        static void NotableBiography(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            var p=Prehistory.NotablePersonAt(s,id);d.Title=p.Name;d.Cell=p.Cell;d.Subtitle=PersonRoles(p.Roles)+(p.DeathYear>=0?" · deceased":" · living");
            d.Introduction=p.Name+" is first recorded in "+Y(s,p.RecordedYear)+" among the "+s.Society.Cultures[p.Culture].Name+". "+(p.BirthEstimated?"The birth year is an estimate for a person first encountered as an adult. ":"The birth is recorded in this history. ")+(p.DeathYear>=0?"Died in "+Y(s,p.DeathYear)+". ":"Still living at this date. ")+"Office tenure is separate from lifespan; a former ruler need not be dead. Achievements below come from recorded work and events, not a great-person bonus.";
            var facts=new List<AtlasFact>{F(p.BirthEstimated?"Estimated birth":"Born",Y(s,p.BirthYear)),F("Recorded since",Y(s,p.RecordedYear)),F("Age",N((p.DeathYear>=0?p.DeathYear:s.Years)-p.BirthYear)),F("Reputation",p.Renown+" / 1,000")};
            if(p.House>=0)facts.Add(F("Ruling house",p.HouseName));facts.Add(F("Interests",PersonRoles(p.Interests)));if(p.DeathYear>=0)facts.Add(F("Died",Y(s,p.DeathYear)));d.Facts=facts.ToArray();
            var family=new List<AtlasLink>();void Relative(int other,string relation){if(other<0)return;var l=NotableLink(s,other);l.Detail=relation;family.Add(l);}
            Relative(p.Parent1,"Parent");Relative(p.Parent2,"Parent");Relative(p.Spouse,"Spouse / recorded consort");foreach(int child in p.Children)Relative(child,"Child / dynastic family member");Relative(p.Regent,"Recorded regent during minority");
            if(family.Count>0)sections.Add(Links("Family and household",family,"Only politically relevant family members are tracked. This is not a complete pedigree. A family connection does not guarantee succession or exceptional ability."));
            var ties=new List<AtlasLink>{CultureLink(s.Society.Cultures[p.Culture])};if(p.State>=0)ties.Add(StateLink(s,s.History.Polities[p.State]));if(p.Faith>=0)ties.Add(FaithLink(s.History.Religions[p.Faith]));
            if(p.Patron>=0){var l=NotableLink(s,p.Patron);l.Detail="Recorded patron";ties.Add(l);}if(p.Mentor>=0){var l=NotableLink(s,p.Mentor);l.Detail="Living local mentor at entry into the recorded career";ties.Add(l);}
            sections.Add(Links("Career and affiliations",ties,"These are recorded origins and professional associations, not a claim that every scholar or artist agrees with a patron. Predictions of death are not displayed."));
            if(p.CurrentCampaign>=0)sections.Add(Text("Military appointment",s.History.Dynamics.Campaigns[p.CurrentCampaign].Name+". Command aptitude replaces the former generic command term; manpower, supplies, technology and actual battle outcomes still matter."));
            if(p.Leader>=0){var leader=s.History.Dynamics.Leaders[p.Leader];sections.Add(Links("Recorded public office",new[]{LeaderLink(s,leader)},leader.EndedYear>=0?"The office ended in "+Y(s,leader.EndedYear)+"; that is not a death date.":"The latest recorded reign remains active."));}
            string[] labels={"Military command","Scholarship","Craft and art","Rhetoric","Administration","Spiritual inquiry"};
            if((p.DeathYear>=0?p.DeathYear:s.Years)-p.BirthYear<16)sections.Add(Text("Abilities and interests","This person is still a child in the recorded period. Adult career capacities are not presented as accomplishments or independent governing power; any regency is recorded separately."));
            else sections.Add(new AtlasSection{Title="Abilities and interests",Bars=true,Unit="/ 1,000",Text="Procedural personal capacities, not objective merit or inherited ethnic intelligence. Careers may overlap.",Links=labels.Select((label,k)=>new AtlasLink{Name=label,Value=p.Skills[k],Detail=p.Skills[k]+" / 1,000"}).ToArray()});
            sections.Add(new AtlasSection{Title="Recorded achievements",Text=p.Deeds.Length==0?"No individual achievement has yet been recorded. Family or office can make a person politically visible without making them a great inventor or artist.":"Recent recorded achievements. Earlier named contributions remain in the archive. Research is collective; this identifies a participant, not an unsupported claim of sole invention.",Timeline=p.Deeds.AsEnumerable().Reverse().Select(x=>F(Y(s,x.Year)+" · "+x.Kind,x.Text)).ToArray()});
        }
        static void NotableCourtSection(PrehistoryState s,int state,List<AtlasSection> sections)
        {
            var r=s.History.Notables;if(r==null)return;var ids=new List<int>();for(int k=0;k<4;k++){int id=r.Courts[state*8+k];if(id>=0)ids.Add(id);}
            int city=s.History.Polities[state].CapitalId;for(int k=0;k<6;k++){int id=r.Towns[city*8+k];if(id>=0)ids.Add(id);}
            if(ids.Count>0)sections.Add(Links("Court household and notable people",ids.Distinct().Select(id=>NotableLink(s,id)),"Rulers, selected dynastic relatives and recorded local specialists. Follow a person for family links and actual achievements; offices are not lifespans."));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasLink ReligiousBodyLink(PrehistoryState s,int town,int slot=0)=>new AtlasLink{Kind="religious-body",Id=town*2+slot,Cell=s.Society.Settlements[town].Cell,Name=(slot==0?"Ritual seat at ":"Lay association at ")+s.Society.Settlements[town].Name,Detail="Religious authority, distinct from doctrine and political ownership"};
        public static AtlasSection[] ReligiousDoctrineSections(PrehistoryState s,int faith)
        {
            var p=Prehistory.ReligiousDoctrineAt(s,faith);if(p==null)return Array.Empty<AtlasSection>();
            var h=s.History;var result=new List<AtlasSection>();
            result.Add(new AtlasSection{Title="Beliefs and sources of authority",Facts=new[]{F("Cosmology",ReligiousDoctrines.Name(p.Cosmology)),F("Afterlife and ultimate aim",ReligiousDoctrines.Name(p.Afterlife)),F("Source of teaching",ReligiousDoctrines.Name(p.Revelation)),F("Religious family",h.Religions[p.RootId].Name),F("Branch",ReligiousDoctrines.Name(p.Branch)),F("Recorded departure",p.Issue==ReligiousIssue.None?"No doctrinal departure recorded":ReligiousDoctrines.Name(p.Issue))},Text="A religious family is shared descent, not guaranteed agreement or a single church. Doctrine is inherited; a branch changes a particular teaching rather than rerolling an unrelated religion."});
            result.Add(new AtlasSection{Title="Observance and sacred obligations",CompactGrid=true,Facts=new[]{
                F("Sacred images",ReligiousDoctrines.Position(p.Images,"Images are discouraged or forbidden","Sacred images are objects of veneration")),
                F("Ancestors",ReligiousDoctrines.Position(p.Ancestors,"Ancestor rites are discouraged or forbidden","Ancestor rites are an obligation")),
                F("Sacrificial offerings",ReligiousDoctrines.Position(p.Sacrifice,"Sacrificial offerings are rejected","Sacrificial offerings are an obligation")),
                F("Ritual calendar",p.CalendarDays+"-day cycle; chief festival on day "+p.FestivalDay),F("Assembly rhythm","One observance day per "+p.RestDays+" days")},Text="These are generated normative traditions, not claims that every household follows every rule. Belief and practice can differ."});
            var axes=new[]{("Exclusive truth claims",p.Exclusivity),("Doctrinal conformity",p.Orthodoxy),("Outreach",p.Outreach),("Universal membership",p.Universalism),("Clerical authority",p.ClericalAuthority),("Sacred kingship",p.RulerSanctity),("Ascetic discipline",p.Asceticism),("Mutual aid",p.Charity),("Purity obligations",p.Purity),("Nonviolence",p.Nonviolence),("Intermarriage openness",p.Intermarriage),("Hospitality",p.Hospitality)};
            result.Add(new AtlasSection{Title="Social teachings",Bars=true,Unit="/ 1,000",Links=axes.Select(x=>new AtlasLink{Name=x.Item1,Value=x.Item2}).ToArray(),Text="Independent emphases, not a goodness score or percentages of adherents. Strong conviction does not automatically imply persecution; funded policy and local political circumstances matter."});
            var related=new List<AtlasLink>();
            foreach(var r in h.Religions.Where(r=>r.Id!=faith&&(r.ParentId==faith||r.Id==h.Religions[faith].ParentId||r.Id==p.SecondaryParent)).OrderBy(r=>r.Id).Take(16))
            {
                var other=Prehistory.ReligiousDoctrineAt(s,r.Id);var c=ReligiousDoctrines.Compare(p,other);var link=FaithLink(r);
                link.Detail="Beliefs "+c.BeliefAffinity+" · practices "+c.PracticeAffinity+" · authority "+c.AuthorityAffinity+" / 1,000. "+c.Disagreement+"; doctrinal tension "+c.Tension+" / 1,000.";related.Add(link);
            }
            result.Add(Links("Branches and doctrinal relationships",related,"Comparison is multidimensional. Near relatives can disagree intensely on one practice; unrelated traditions can coexist. This is not an automatic war predictor."));
            return result.ToArray();
        }
        static void ReligiousBody(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            int town=id/2,slot=id%2;int cell=s.Society.Settlements[town].Cell;var body=Prehistory.ReligiousInstitutionAt(s,cell,slot);
            d.Title=(slot==0?"Ritual seat at ":"Lay association at ")+s.Society.Settlements[town].Name;d.Cell=cell;
            if(body==null){d.Introduction="No maintained public religious institution is recorded at this site.";return;}
            d.Subtitle=ReligiousDoctrines.Name(body.Form);d.Introduction="This institution organizes public observance of "+s.History.Religions[body.Faith].Name+". Its authority is distinct from both the tradition's doctrines and the government ruling its town. Local gifts and provisions sustain its activity; conquest does not automatically transfer its religious allegiance.";
            d.Facts=new[]{F("Institution established",Y(s,body.SinceYear)),F("Tradition",s.History.Religions[body.Faith].Name),F("Support",body.Support+" / 1,000"),F("Local autonomy",body.Independence+" / 1,000")};
            sections.Add(Links("Institution, town and tradition",new[]{TownLink(s,s.Society.Settlements[town]),FaithLink(s.History.Religions[body.Faith])}));
            var institutions=new List<AtlasLink>();
            foreach(var t in s.Society.Settlements)
            {
                var local=Prehistory.ReligiousInstitutionAt(s,t.Cell,slot);if(local==null||local.Seat!=town||local.Support<100)continue;
                var link=TownLink(s,t);link.Detail=ReligiousDoctrines.Name(local.Form)+" · support "+local.Support+" · autonomy "+local.Independence+" / 1,000";institutions.Add(link);
            }
            sections.Add(Links("Affiliated ritual bodies",institutions,"Affiliation is sustained by physically delivered contact and compatible authority practices, not shared belief alone."));
            var localSection=ReligiousLocalSection(s,cell);if(localSection!=null)sections.Add(localSection);
            sections.Add(Events(s,e=>e.Cell==cell&&e.Kind.StartsWith("religious-",StringComparison.Ordinal)));
        }
    }
}

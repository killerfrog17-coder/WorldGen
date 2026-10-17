using System;
namespace WorldGen.Core
{
    // Derived views, not an invented census of party membership. Ancient
    // participation is negotiated voice, not a claim of universal suffrage.
    public sealed class HistoricalPoliticalProfile
    {
        public string Form="", Participation="", Succession="";
        public string[] Blocs=Array.Empty<string>();
        public int[] InfluencePermille=Array.Empty<int>();
        public int DefensePermille,WorksPermille,ReliefPermille,LearningPermille;
    }
    public readonly struct HistoricalPublicBudget
    {
        public readonly long Defense,Works,Relief,Learning;
        public long Total=>Defense+Works+Relief+Learning;
        public HistoricalPublicBudget(long defense,long works,long relief,long learning)
        {Defense=defense;Works=works;Relief=relief;Learning=learning;}
    }
    public static class HistoricalPoliticalPrograms
    {
        // Calibrated game priors: neither printing nor prosperity forces one
        // constitutional destination. Each fixed-size view uses actor ledgers.
        public static HistoricalPoliticalProfile At(int representation,int centralization,double urban,double commerce,double war,double religion,double literacy,double eliteCapture)
        {
            double r=D.Clamp(representation/1000.0),c=D.Clamp(centralization/1000.0);
            urban=D.Clamp(urban);commerce=D.Clamp(commerce);war=D.Clamp(war);religion=D.Clamp(religion);literacy=D.Clamp(literacy);eliteCapture=D.Clamp(eliteCapture);
            var p=new HistoricalPoliticalProfile();
            p.Form=r>=.65?(c<.4?"Federated representative government":"Representative government")
                :c<.35?"Confederation of local councils":r>=.35?(commerce>.6?"Merchant and civic oligarchy":"Council of estates")
                :war>.65&&c>.65?"Military court":c>.7?"Central dynastic administration":"Dynastic court and provincial assemblies";
            p.Participation=r>=.65?"Broad representation; the franchise and local privileges remain historically contingent."
                :r>=.35?"Participation is mediated through privileged civic bodies, estates and local councils."
                :"Court and notable families hold most formal power; local communities negotiate privileges.";
            p.Succession=r>=.65?"Selection through representative institutions":c<.35?"Negotiation among constituent communities":"Court succession and bargaining with provincial elites";
            p.Blocs=new[]{"Landholders and provincial notables","Merchants and artisans","Military and court","Ritual institutions","Urban and rural commons"};
            double[] weights={.45+.5*(1-urban)+.5*eliteCapture,.12+.65*commerce+.25*urban,.2+.8*war+.3*c,.15+.65*religion*(1-.5*literacy),.1+.65*r+.25*urban};
            double total=0;for(int i=0;i<5;i++)total+=weights[i];p.InfluencePermille=new int[5];int used=0;
            for(int i=0;i<4;i++){p.InfluencePermille[i]=(int)Math.Floor(1000*weights[i]/total);used+=p.InfluencePermille[i];}p.InfluencePermille[4]=1000-used;
            // Spending is a tradeoff; these four shares always sum to1000.
            p.DefensePermille=(int)Math.Round(160+260*war+70*c-35*r);
            p.ReliefPermille=(int)Math.Round(90+150*r+35*(1-c));
            p.LearningPermille=(int)Math.Round(70+100*literacy+65*commerce+45*r);
            p.WorksPermille=1000-p.DefensePermille-p.ReliefPermille-p.LearningPermille;
            return p;
        }
        public static HistoricalPublicBudget Allocate(long available,HistoricalPoliticalProfile p)
        {
            available=Math.Max(0,available);long Share(int x)=>(long)((decimal)available*x/1000);
            long defense=Share(p.DefensePermille),relief=Share(p.ReliefPermille),learning=Share(p.LearningPermille);
            return new HistoricalPublicBudget(defense,available-defense-relief-learning,relief,learning);
        }
        public static double TaxMultiplier(int representation,int centralization)
            =>.94+.10*D.Clamp(centralization/1000.0)+.04*D.Clamp(representation/1000.0);
        public static double RecruitmentMultiplier(HistoricalPoliticalProfile p)=>.90+.45*p.DefensePermille/1000.0;
        public static double FundedRecruitmentMultiplier(HistoricalPoliticalProfile p,double funding)
            =>.85+(RecruitmentMultiplier(p)-.85)*D.Clamp(funding);
        public static double LearningMultiplier(HistoricalPoliticalProfile p,int fundedWorks)
            =>.90+Math.Min(.25,1.0*p.LearningPermille/1000)*D.Clamp(fundedWorks/600.0);
        // A bargaining prior, not an election forecast. Formal decentralization
        // is deliberately absent: granting voice must not automatically make
        // the next concession easier by weakening the same court again.
        public static double ConcessionChance(HistoricalPoliticalProfile p,int representation,int diplomacy,int administration,int military,int ambition,
            int eliteCapture,int exhaustion,double literacy,bool activeWar)
        {
            double civilian=(p.InfluencePermille[4]+.65*p.InfluencePermille[1]+.15*p.InfluencePermille[0])/1000.0;
            double court=(p.InfluencePermille[2]+.6*p.InfluencePermille[0]+.25*p.InfluencePermille[3])/1000.0;
            double inclination=.20*(diplomacy-500)/500.0+.12*(administration-500)/500.0-.12*(military+ambition-1000)/1000.0;
            double strain=D.Clamp(exhaustion/1000.0);
            double chance=.42+.65*(civilian-court)+inclination+.10*D.Clamp(literacy)-.20*D.Clamp(eliteCapture/1000.0)
                -.12*(activeWar?1-strain:0)+.06*strain-.65*(D.Clamp(representation/1000.0)-.4);
            return D.Q(Math.Max(.04,Math.Min(.96,chance)),1000000000);
        }
        public static double AdministrationTarget(HistoricalPoliticalProfile p,double fiscalCapacity,double geographicalLoad,int administration,int exhaustion,bool activeWar)
        {
            double fatigue=D.Clamp(exhaustion/1000.0);
            double target=.30+.40*D.Clamp(fiscalCapacity)+.15*D.Clamp(administration/1000.0)
                +.20*p.InfluencePermille[2]/1000.0-.20*p.InfluencePermille[0]/1000.0-.40*D.Clamp(geographicalLoad/1.5)
                +(activeWar?.16*(1-fatigue):0)-.18*fatigue;
            return D.Q(Math.Max(.15,Math.Min(.90,target)),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        public HistoricalPoliticalProfile PoliticalProfile(PrehistoryState s,int polity)
        {
            if(!LivingWorldHistory(s)||polity<0||polity>=s.History.Polities.Length)return null;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var policy=a.Communities.Governments[polity];var account=a.Polities[polity];var culture=h.Cultures[p.CultureId];var cycle=h.Dynamics.States[polity].Cycle;
            int cell=s.Society.Settlements[p.CapitalId].Cell;
            double urban=D.Clamp(account.Urban/(double)Math.Max(1,p.Population)*4);
            double commerce=D.Clamp(.5*HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Coinage,s.Years)+.35*HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Sailing,s.Years)+((World.Ecology.Habitat[cell].Habitat&Habitat.Coastal)!=0?.2:0));
            double literacy=HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Printing,s.Years);
            // Institutional patronage is a saved-identity-derived prior, not a
            // religion census or a rule that all literate states become secular.
            double patronage=p.ReligionId<0?.1:.35+.65*HistoryRoll("living-ritual-patronage",p.CultureId,0);
            return HistoricalPoliticalPrograms.At(policy.RepresentationMilli,policy.CentralizationMilli,urban,commerce,p.Exhaustion/500.0,patronage,literacy,(cycle?.EliteCapture??0)/1000.0);
        }
        // No cultural census: callers use their existing bounded sampled owners.
        double LivingPoliticalLearning(PrehistoryState s,int polity)
            =>HistoricalPoliticalPrograms.LearningMultiplier(PoliticalProfile(s,polity),s.History.Dynamics.States[polity].PublicWorks);
        double LivingCulturePoliticalLearning(PrehistoryState s,int culture)
        {
            var a=s.History.Aggregates;var cities=a.Cultures[culture].Cities;if(cities.Count==0)return 1;
            // A stable local sample, not a search through all cultural holdings.
            int owner=a.Resolve(s.Society.Settlements[cities.At(0)].PolityId,s.History);
            return owner<0?1:LivingPoliticalLearning(s,owner);
        }
        void InitializePoliticalProgram(PrehistoryState s,int polity)
        {
            var h=s.History;var p=h.Polities[polity];var policy=h.Aggregates.Communities.Governments[polity];var town=s.Society.Settlements[p.CapitalId];int cell=town.Cell;
            bool port=(World.Ecology.Habitat[cell].Habitat&Habitat.Coastal)!=0&&HistoricalTechnologies.Has(h.Cultures[p.CultureId],HistoricalTechnology.Sailing);
            bool irrigation=riverAccess[cell]>.10&&World.Ecology.Habitat[cell].AridityMilli<650;
            // Wide overlapping priors, not hydraulic determinism: irrigation
            // does not make Sumerian city councils identical to a Nile court.
            policy.RepresentationMilli=150+(port?80:0)+(int)(HistoryRoll("living-founding-council",polity,s.Years)*310);
            policy.CentralizationMilli=250+(irrigation?100:0)+(port?-40:0)+(int)(HistoryRoll("living-founding-centralization",polity,s.Years)*430);
            // A daughter court inherits some practiced constitutional habits,
            // without automatically becoming a copy of its metropolitan ruler.
            if(p.ParentId>=0){var parent=h.Aggregates.Communities.Governments[p.ParentId];policy.RepresentationMilli=(policy.RepresentationMilli+parent.RepresentationMilli)/2;policy.CentralizationMilli=(policy.CentralizationMilli+parent.CentralizationMilli)/2;}
        }
        void RefreshPoliticalDescription(PrehistoryState s,int polity)
        {var p=s.History.Polities[polity];p.Government=PoliticalProfile(s,polity).Form;}
        void AdvancePoliticalProgram(PrehistoryState s,int polity,int elapsed)
        {
            var h=s.History;var p=h.Polities[polity];var ds=h.Dynamics.States[polity];var policy=h.Aggregates.Communities.Governments[polity];var leader=h.Dynamics.Leaders[ds.LeaderId];var profile=PoliticalProfile(s,polity);
            double pressure=.12+.32*p.Exhaustion/1000.0+.28*(ds.Cycle?.EliteCapture??0)/1000.0+.28*profile.InfluencePermille[4]/1000.0;
            pressure+=.35*HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.Printing,s.Years)+.25*HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.Telecommunications,s.Years);
            if(s.Years-policy.LastReformYear<25||HistoryRoll("living-political-bargain",polity,s.Years)>=1-Math.Exp(-.008*pressure*elapsed))return;
            policy.LastReformYear=s.Years;
            bool war=false;var contacts=h.International?.Accounts[polity].Relations;
            int offset=contacts==null||contacts.Count==0?0:(int)(HistoryRoll("living-political-war-sample",polity,s.Years)*contacts.Count);
            for(int k=0;k<Math.Min(4,contacts?.Count??0);k++)
            {var relation=h.Relations.Peek(contacts.At((offset+k)%contacts.Count));int other=relation.A==polity?relation.B:relation.A;if(relation.WarSince>=0&&h.Polities[other].EndedYear<0){war=true;break;}}
            double literacy=HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.Printing,s.Years);
            double chance=HistoricalPoliticalPrograms.ConcessionChance(profile,policy.RepresentationMilli,leader.Diplomacy,leader.Administration,leader.Military,leader.Ambition,ds.Cycle?.EliteCapture??0,p.Exhaustion,literacy,war);
            bool bargain=HistoryRoll("living-political-coalition",polity,s.Years)<chance;
            policy.RepresentationMilli=Math.Max(50,Math.Min(950,policy.RepresentationMilli+(bargain?40:-30)));
            // Capacity, not a second treasury debit or a claim that a floored
            // unpaid bill was serviced. Existing paid works remain separate.
            var account=h.Aggregates.Polities[polity];
            double fiscal=Math.Min(D.Clamp(p.Treasury/Math.Max(1000.0,account.ServiceCost*20.0)),D.Clamp(account.Receipts/Math.Max(1.0,account.ServiceCost)));
            double load=Math.Sqrt(Math.Max(1,p.AreaKm2)/Math.PI)/Math.Max(50,account.ReachKm);
            double target=HistoricalPoliticalPrograms.AdministrationTarget(profile,fiscal,load,leader.Administration,p.Exhaustion,war);
            int administrativeChange=(int)Math.Round(Math.Max(-30,Math.Min(30,(target*1000-policy.CentralizationMilli)*.12)));
            policy.CentralizationMilli=Math.Max(150,Math.Min(950,policy.CentralizationMilli+administrativeChange));
            int acceptance=(int)Math.Round(40*(2*(bargain?chance:1-chance)-1));
            p.Legitimacy=Math.Max(100,Math.Min(950,p.Legitimacy+acceptance));p.Cohesion=Math.Max(50,Math.Min(950,p.Cohesion+acceptance/2));
            if(!bargain)p.Exhaustion=Math.Min(1000,p.Exhaustion+10);
            RefreshPoliticalDescription(s,polity);
            string administrationText=administrativeChange>0?" The settlement also strengthens central administration.":administrativeChange<0?" More administrative responsibility remains with provincial institutions.":"";
            HistoryEvent(s,bargain?"political-reform":"political-repression",p.Name+(bargain?" bargains over taxation and local representation, widening the formal voice of its communities.":" restricts formal representation as court and organized interests contest taxation and authority.")+administrationText,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
        }
        void FundPoliticalProgram(PrehistoryState s,int polity,int elapsed)
        {
            var h=s.History;var p=h.Polities[polity];var account=h.Aggregates.Polities[polity];var ds=h.Dynamics.States[polity];var leader=h.Dynamics.Leaders[ds.LeaderId];var profile=PoliticalProfile(s,polity);
            long available=Math.Min(p.Treasury/8,Math.Max(0,(long)(account.Receipts*.08*elapsed)));
            var budget=HistoricalPoliticalPrograms.Allocate(available,profile);p.Treasury-=budget.Total;
            double defenseFunding=budget.Defense/Math.Max(1,p.Army*.05*elapsed);
            // Army strength is a conserved, paid and trained stock in revised
            // military rules. A budget allocation cannot recreate casualties
            // or multiply troops already committed to another campaign.
            if(!RevisedMilitaryHistory(s))p.Army=(int)Math.Min(int.MaxValue,p.Army*HistoricalPoliticalPrograms.FundedRecruitmentMultiplier(profile,defenseFunding));
            double denominator=Math.Max(1,account.Urban/800.0),effect=Math.Min(elapsed*3,(budget.Works+.5*budget.Learning)/denominator*(.3+.7*leader.Administration/1000.0));
            ds.PublicWorks=Math.Max(0,Math.Min(1000,ds.PublicWorks+(int)Math.Round(effect)-elapsed));
            int relief=(int)Math.Min(elapsed*2,budget.Relief/denominator);
            p.Legitimacy=Math.Min(950,p.Legitimacy+relief);p.Cohesion=Math.Min(950,p.Cohesion+relief/2);
        }
    }
}

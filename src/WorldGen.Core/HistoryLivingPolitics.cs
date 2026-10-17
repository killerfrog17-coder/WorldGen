using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        bool TryLivingRegimeChange(PrehistoryState s,int polity)
        {
            // Current realm rules resolve succession within the continuing
            // state. Actual territorial separation belongs to local actors.
            if(RealmHistory(s))return false;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var ds=h.Dynamics.States[polity];var old=h.Dynamics.Leaders[ds.LeaderId];var cycle=ds.Cycle??new HistoricalPoliticalCycle();var hc=h.Cultures[p.CultureId];
            double accountability=1-.35*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years)-.30*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years)-.20*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telecommunications,s.Years);
            double disputed=.015+.16*cycle.EliteCapture/1000.0+.22*cycle.ConquestDebt/1000.0+.20*(1-p.Cohesion/1000.0)*(1-old.Administration/1000.0)+.15*p.Exhaustion/1000.0;
            if(CommunityHistory(s))
            {
                var policy=a.Communities.Governments[polity];
                // Communications alone cannot make an unrepresentative dynasty
                // almost immune to succession disputes. Legitimate elections,
                // however, need not refound the country on every change of leader.
                accountability=.65+.35*(1-policy.RepresentationMilli/1000.0);
                if(policy.RepresentationMilli>=650)disputed*=.12;
            }
            if(HistoryRoll("living-disputed-succession",polity,s.Years)>=disputed*accountability)return false;
            int id=h.Polities.Length;var successor=p.Copy();successor.Id=id;successor.ParentId=polity;successor.FoundedYear=s.Years;successor.RulerSince=s.Years;successor.EndedYear=-1;successor.Legitimacy=550;successor.Cohesion=Math.Max(450,p.Cohesion);successor.Treasury=p.Treasury*3/4;
            bool literate=HistoricalTechnologies.Has(hc,HistoricalTechnology.Printing);successor.Name=s.Society.Settlements[p.CapitalId].Name+(literate?" Commonwealth":" Kingdom");successor.Government=literate?"Reconstituted representative government":"Successor dynasty and provincial councils";
            if(ConnectedHistory(s))
            {
                // Printing permits wider participation; it does not automatically
                // turn every literate state into the same representative regime.
                bool assembly=PassageHistory(s)?a.Communities.Governments[polity].RepresentationMilli>=650:literate&&cycle.EliteCapture<450&&old.Diplomacy>=500&&HistoryRoll("development-assembly-succession",polity,s.Years)<.35;
                bool imperial=!assembly&&a.Polities[polity].CityCount>=6&&old.Military>=650;
                successor.Name=s.Society.Settlements[p.CapitalId].Name+(assembly?" Commonwealth":imperial?" Empire":" Kingdom");
                successor.Government=assembly?"A negotiated assembly and provincial councils":imperial?"A successor imperial court with provincial administration":"A successor dynasty and negotiated local privileges";
            }
            // A form of government belongs in its profile, not compulsorily in
            // every map name. Regime replacement need not erase the endonym.
            if(LivingWorldHistory(s))successor.Name=p.Name;
            var account=a.Polities[polity].Copy();account.Id=id;account.LastYear=s.Years;account.LastSuccession=s.Years;
            h.Polities=h.Polities.Concat(new[]{successor}).ToArray();a.Polities=a.Polities.Concat(new[]{account}).ToArray();
            if(LivingWorldHistory(s)){AppendNuclearState(s);CloseNuclearState(s,polity,id,true);}
            AppendInternationalState(s,polity,true);
            if(ProvincialHistory(s)){Array.Resize(ref a.Provinces.FarmPotential,h.Polities.Length);a.Provinces.FarmPotential[id]=a.Provinces.FarmPotential[polity];a.Provinces.FarmPotential[polity]=0;}
            if(CommunityHistory(s))a.Communities.Governments=a.Communities.Governments.Concat(new[]{a.Communities.Governments[polity].Copy()}).ToArray();
            AppendImperialGovernment(s,polity);
            var next=ds.Copy();next.Id=id;next.LeaderId=-1;next.LastSuccession=s.Years;next.Cycle=cycle.Copy();next.Cycle.EliteCapture/=2;next.Cycle.ConquestDebt/=2;h.Dynamics.States=h.Dynamics.States.Concat(new[]{next}).ToArray();
            // The polity changes, not its entire population or culture. Cached
            // accounts and persistent territory roots transfer without a census.
            p.EndedYear=s.Years;p.Army=0;a.Successors=a.Successors.With(polity,id);a.Polities[polity].Remote=0;a.Polities[polity].RemoteCapacity=0;
            old.EndedYear=s.Years;old.Legacy="A contested succession replaced the ruling government; communities, institutions and cultural traditions continued.";SealAggregateLeader(s,old.Id);AppointAggregateLeader(s,id);ScheduleAggregate(s,StateProcess,id);
            HistoryEvent(s,"regime-change",successor.Name+" succeeds "+p.Name+" after a disputed transfer of power. The cities and population continue under a new government, rather than disappearing with the former court.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,id,other:polity,category:ChronicleCategory.Politics);
            return true;
        }
        double LivingTravelKm(PrehistoryState s,int from,int to,int culture)
        {
            double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
            bool river=riverAccess[from]>.02&&riverAccess[to]>.02&&World.Hydrology.Watershed[from]>=0&&World.Hydrology.Watershed[from]==World.Hydrology.Watershed[to];
            double relief=(World.Ecology.Habitat[from].ReliefM+World.Ecology.Habitat[to].ReliefM)/3000.0;
            double effort=km*(river?.45:1+relief);
            return PassageHistory(s)?Math.Min(effort,DiffusionSeaEffort(s,from,to,culture)):effort;
        }
        void AdvanceLivingIncorporation(PrehistoryState s,int polity,int city,int elapsed)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var neighbors=a.Cities[city].Neighbors;if(neighbors.Length==0||p.Cohesion<500||p.Treasury<100)return;
            int other=neighbors[(int)(HistoryRoll("living-incorporation-peer",polity,s.Years)*neighbors.Length)];var town=s.Society.Settlements[other];
            if(a.Living.Cities[other].AbandonedYear>=0||a.Resolve(town.PolityId,h)>=0||town.Population<300)return;
            double effort=LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,town.Cell,p.CultureId);
            if(!CourtFrontierWithinCore(s,polity,effort))return;
            if(effort>a.Polities[polity].ReachKm*(1+.15*Math.Log(1+a.Polities[polity].CityCount)))return;
            double familiarity=town.CultureId==p.CultureId?1:.25;
            double benefit=(.3+.7*p.Legitimacy/1000.0)*familiarity;
            if(HistoryRoll("living-incorporation",polity,s.Years)<1-Math.Exp(-.003*benefit*elapsed))
            {ClaimAggregatePolity(s,town.Cell,polity);p.Treasury-=100;HistoryEvent(s,"incorporation",town.Name+" joins the administration of "+p.Name+" through local protection and provisioning agreements.",town.Cell,town.CultureId,polity,town:other,category:ChronicleCategory.Politics);}
        }
    }
}

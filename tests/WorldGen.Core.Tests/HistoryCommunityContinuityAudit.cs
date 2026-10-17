using System.Reflection;
using System.Text.Json;
using WorldGen.Core;

// No handler invocation: compare proposed23 arithmetic against observed22
// actors, while preserving their existing raw autonomy pressure and all stock.
public static class HistoryCommunityContinuityAudit
{
    public static void RunLoaded(World world,string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new community continuity output.");
        var original=world.Prehistory;string before=original.ComputeHash();var s=original.Copy();var h=s.History;var a=h.Aggregates;
        if(a.Empires.Connections.Realms.Version!=22)throw new NotSupportedException("The hypothetical23 continuity audit requires an unchanged marked22 source world.");
        long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;var engine=new Prehistory(world);
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        object Place(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI};
        var rows=new List<object>();int owned=0,eligible=0,changed=0,crossed=0,service=0,modern=0,distanceChanged=0,quietChanged=0;
        double oldTotal=0,newTotal=0;
        foreach(var t in s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0))
        {
            int owner=a.Resolve(t.PolityId,h);if(owner<0)continue;owned++;
            var p=h.Polities[owner];var ds=h.Dynamics.States[owner];var account=a.Polities[owner];var policy=a.Communities.Governments[owner];
            var local=a.Communities.Cities[t.Id];var record=h.Towns[t.Id];
            if(t.Id==p.CapitalId||t.Population<1800||a.Living.Cities[t.Id].DeclineYears>0||s.Years-t.FoundedYear<120)continue;eligible++;
            int capital=s.Society.Settlements[p.CapitalId].Cell;
            double reach=Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(h.Cultures[p.CultureId],s.Years,ds.PublicWorks));
            double oldDistance=(double)Call("LivingTravelKm",s,capital,t.Cell,t.CultureId)/reach;
            double rulerDistance=(double)Call("LivingTravelKm",s,capital,t.Cell,p.CultureId)/reach;
            double bureaucracy=(double)Call("RealmBureaucracy",s,owner),crisis=(double)Call("RealmCrisis",s,owner),claimant=(double)Call("RealmClaimantShock",s,owner,t.Id);
            int physicalSource=a.Empires.Territories.Sources[t.Cell];
            bool node=(bool)Call("TerritorialNodeLive",s,owner,physicalSource),gate=(bool)Call("InstitutionalRouteAllowed",s,capital,t.Cell,p.CultureId),served=node&&gate;
            double demands=record.AutonomyPressure/1000.0,delivered=1/(1+Math.Pow(Math.Max(0,rulerDistance-1),2));
            double credit=served&&bureaucracy>0&&account.Receipts>0?.45*D.Clamp(bureaucracy)*D.Clamp(local.InclusionMilli/750.0)*(.55+.45*D.Clamp(policy.RepresentationMilli/1000.0))*D.Clamp(account.Receipts/(double)Math.Max(1,account.ServiceCost))*delivered:0;
            double secession=Math.Max(0,demands-credit);bool succession=s.Years-ds.LastSuccession<15;
            double Risk(double pressure)=>.08*Math.Pow(Math.Max(0,pressure-.35),2)*(.3+.7*(1-p.Cohesion/1000.0))*(succession?3:1);
            double oldRisk=Risk(demands),newRisk=Risk(secession);oldTotal+=oldRisk;newTotal+=newRisk;
            if(served)service++;if(bureaucracy>0)modern++;if(oldDistance!=rulerDistance)distanceChanged++;
            if(newRisk<oldRisk){changed++;if(crisis<=0&&claimant<=0)quietChanged++;}if(oldRisk>0&&newRisk==0)crossed++;
            double Target(double distance)
            {
                var hc=h.Cultures[t.CultureId];bool foreign=t.CultureId!=p.CultureId,recent=s.Years-local.SinceYear<75;
                double printing=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years),organization=D.Clamp(hc.Institution/650.0)*D.Clamp(Math.Sqrt(t.Population/6000.0));
                double deficit=D.Clamp((account.ServiceCost-account.Receipts)/(double)Math.Max(1,account.ServiceCost)),conquest=(ds.Cycle?.ConquestDebt??0)/1000.0;
                double value=D.Clamp(.18+.18*D.Clamp(distance/4)+(foreign?.25+.25*printing:.05)*organization*(1-local.InclusionMilli/1000.0)+.22*printing*(1-policy.RepresentationMilli/1000.0)+.35*deficit+.22*p.Exhaustion/1000.0+.40*conquest-.22*p.Legitimacy/1000.0-.12*ds.PublicWorks/1000.0);
                value=D.Clamp(value+.4*D.Clamp((distance-1)/4)*policy.CentralizationMilli/1000.0*(1-.85*bureaucracy));
                return recent&&foreign&&succession?D.Clamp(value+.25*(1-local.InclusionMilli/1000.0)*conquest):value;
            }
            var siblings=new List<object>();
            foreach(int peer in a.Cities[t.Id].Neighbors)
            {
                var candidate=s.Society.Settlements[peer];int rival=a.Resolve(candidate.PolityId,h);
                if(rival<0||rival==owner||h.Polities[rival].ParentId!=owner||h.Polities[rival].CultureId!=t.CultureId||s.Years-h.Polities[rival].FoundedYear>50)continue;
                double km=(double)Call("LivingTravelKm",s,candidate.Cell,t.Cell,t.CultureId);
                bool allowed=(bool)Call("InstitutionalRouteAllowed",s,candidate.Cell,t.Cell,t.CultureId);
                if(km<Math.Max(180,a.Polities[rival].ReachKm*2.0)&&allowed)siblings.Add(new{Court=rival,Town=peer,Km=km});
            }
            rows.Add(new{Town=t.Id,t.Name,t.CultureId,t.Population,Age=s.Years-t.FoundedYear,Place=Place(t.Cell),Owner=owner,OwnerName=p.Name,RulerCulture=p.CultureId,OwnerPopulation=p.Population,OwnerTowns=account.CityCount,OwnerAreaKm2=p.AreaKm2,
                p.Cohesion,p.Legitimacy,p.Exhaustion,ds.PublicWorks,ds.Strain,policy.RepresentationMilli,policy.CentralizationMilli,local.InclusionMilli,local.SinceYear,
                RawSavedDemands=demands,OldLocalCultureDistance=oldDistance,CorrectRulerCultureDistance=rulerDistance,OldCurrentTargetDemands=Target(oldDistance),CorrectRulerTargetDemands=Target(rulerDistance),
                Bureaucracy=bureaucracy,PhysicalSource=physicalSource,NodeLive=node,PhysicalRulerRoute=gate,ActualService=served,DeliveredFactor=delivered,account.Receipts,account.ServiceCost,
                Counterfactual23ContinuityCredit=credit,Counterfactual23SecessionPressure=secession,Current22AnnualHazard=oldRisk,Counterfactual23AnnualHazard=newRisk,
                Succession=succession,RealmCrisis=crisis,ClaimantShock=claimant,CurrentEligibleSiblingGovernments=siblings});
        }
        long delta=a.CellReads-reads;a.CellReads=reads;
        if(original.ComputeHash()!=before||s.ComputeHash()!=before||a.CellWrites!=writes||a.ProcessedLocalEvents!=events)throw new Exception("Community continuity probe altered source.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=source,Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,DiagnosticReadDelta=delta,
            OwnedTowns=owned,OrdinaryAgePopulationEligible=eligible,CurrentPhysicallyServed=service,CurrentPositiveBureaucracy=modern,LocalVsRulerDistanceChanged=distanceChanged,
            CounterfactualHazardReduced=changed,QuietCounterfactualHazardReduced=quietChanged,CounterfactualCrossedBelowThreshold=crossed,
            SummedCurrent22AnnualHazards=oldTotal,SummedCounterfactual23AnnualHazards=newTotal,Towns=rows,
            Notes=new[]{"Offline hypothetical23 arithmetic over actual frozen22 saved actors. No new rule is enabled, no migration/politics handler called, no RNG roll, and no stock/country/border altered. Original and detached probe hashes preserved apart from normalized diagnostic read counter.",
                "Hazards compare identical CURRENT SAVED raw autonomy pressure. Corrected ruler-distance raw targets are exported separately, without inventing a subsequent smoothed pressure history.",
                "Town age>=120, population>=1800, noncapital, no current decline; actual crisis/claimant shocks are separately exposed. During crisis a prior real separation handler may run first: summed generic hazards are not realized independence counts or independent-probability forecasts.",
                "Current eligibility does not prove this cause dominated past fragmentation. Future counterfactual trajectory needs a genuine fresh marked23 replay. Same-culture siblings are only current bounded physical candidates; no realignment is invoked."}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Community continuity {s.Years}: {changed}/{eligible} counterfactual hazard reductions, {crossed} threshold crossings; unchanged {before}");
    }
}

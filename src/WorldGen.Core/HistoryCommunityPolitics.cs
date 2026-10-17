using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void TransferCommunityGovernment(PrehistoryState s,int city,int polity)
        {
            var a=s.History.Aggregates;var local=a.Communities.Cities[city];
            RecordIdentityGovernmentTransfer(s,city,local.RulerId,polity);
            // A constitutional or dynastic successor does not erase a town's
            // negotiated privileges, population or cultural identity.
            if(local.RulerId>=0&&a.Resolve(local.RulerId,s.History)==polity){local.RulerId=polity;return;}
            local.RulerId=polity;local.SinceYear=s.Years;
            local.InclusionMilli=polity<0?0:IdentityHistory(s)?(int)Math.Round(650-550*LocalIdentityDifference(s,city,polity)):s.Society.Settlements[city].CultureId==s.History.Polities[polity].CultureId?650:100;
        }
        void AdvanceCommunityPolitics(PrehistoryState s,int city)
        {
            ReviewRealmCenterTitles(s,city);
            var h=s.History;var a=h.Aggregates;var t=s.Society.Settlements[city];int owner=a.Resolve(t.PolityId,h);if(owner<0)return;
            var local=a.Communities.Cities[city];if(local.RulerId!=owner)TransferCommunityGovernment(s,city,owner);
            var p=h.Polities[owner];var ds=h.Dynamics.States[owner];var policy=a.Communities.Governments[owner];var record=h.Towns[city];
            bool recent=s.Years-local.SinceYear<75, succession=s.Years-ds.LastSuccession<15;
            int interval=RealmCrisis(s,owner)>0||RealmClaimantShock(s,owner,city)>0?1:recent&&succession?3:recent?7:20,elapsed=s.Years-record.LastAutonomyActionYear;if(elapsed<interval)return;
            record.LastAutonomyActionYear=s.Years;record.UnrestPolityId=owner;
            var leader=h.Dynamics.Leaders[ds.LeaderId];bool foreign=t.CultureId!=p.CultureId;
            double identityDifference=LocalIdentityDifference(s,city,owner);if(IdentityHistory(s))foreign=identityDifference>=.5;
            double inclusionTarget=D.Clamp(.10+.30*p.Legitimacy/1000.0+.25*ds.PublicWorks/1000.0+.25*leader.Diplomacy/1000.0+.25*policy.RepresentationMilli/1000.0+.15*(1-policy.CentralizationMilli/1000.0)-.25*p.Exhaustion/1000.0);
            local.InclusionMilli=(int)Math.Round(local.InclusionMilli+(1000*inclusionTarget-local.InclusionMilli)*(1-Math.Exp(-elapsed/180.0)));
            AdvanceRealmClaimant(s,owner,city,elapsed);
            if(ReviewDisconnectedCourt(s,owner,city,elapsed))return;
            if(city==p.CapitalId||t.Population<(RealmHistory(s)&&(RealmCrisis(s,owner)>0||RealmClaimantShock(s,owner,city)>0)?600:1800)||a.Living.Cities[city].DeclineYears>0)return;
            var hc=h.Cultures[t.CultureId];var account=a.Polities[owner];bool continuityRules=(a.Empires?.Connections?.Realms?.Version??0)>=23;int serviceCulture=continuityRules?p.CultureId:t.CultureId;
            double distance=LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,t.Cell,serviceCulture)/Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(h.Cultures[p.CultureId],s.Years,ds.PublicWorks));
            if(TryRealmRegionalSeparation(s,owner,city,elapsed,distance))return;
            double organization=D.Clamp(hc.Institution/650.0)*D.Clamp(Math.Sqrt(t.Population/6000.0)),printing=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years);
            double deficit=D.Clamp((account.ServiceCost-account.Receipts)/(double)Math.Max(1,account.ServiceCost));
            double conquest=(ds.Cycle?.ConquestDebt??0)/1000.0;
            double culturalPressure=IdentityHistory(s)?.05+identityDifference*(.20+.25*printing):foreign?.25+.25*printing:.05;
            double pressure=D.Clamp(.18+.18*D.Clamp(distance/4)+culturalPressure*organization*(1-local.InclusionMilli/1000.0)
                +.22*printing*(1-policy.RepresentationMilli/1000.0)+.35*deficit+.22*p.Exhaustion/1000.0+.40*conquest
                -.22*p.Legitimacy/1000.0-.12*ds.PublicWorks/1000.0);
            if(RealmHistory(s))pressure=D.Clamp(pressure+.4*D.Clamp((distance-1)/4)*policy.CentralizationMilli/1000.0*(1-.85*RealmBureaucracy(s,owner)));
            pressure=D.Clamp(pressure+ReligiousLocalPoliticalGrievance(s,t.Cell,owner));
            pressure=D.Clamp(pressure+LocalIdentityPoliticalGrievance(s,city,owner,identityDifference));
            if(recent&&foreign&&succession)pressure=D.Clamp(pressure+.25*(1-local.InclusionMilli/1000.0)*conquest);
            double previous=record.AutonomyPressure/1000.0,tau=recent?12:80;
            pressure=previous+(pressure-previous)*(1-Math.Exp(-elapsed/tau));record.AutonomyPressure=(int)Math.Round(pressure*1000);
            double secessionPressure=CommunityTerritorialSecessionPressure(s,owner,city,pressure,distance);
            double risk=.08*Math.Pow(Math.Max(0,secessionPressure-.35),2)*(.3+.7*(1-p.Cohesion/1000.0));
            if(succession)risk*=3;
            if(s.Years-t.FoundedYear>=120&&HistoryRoll("community-independence",city,s.Years)<1-Math.Exp(-risk*elapsed))
            {if(!continuityRules||!JoinRecentCommunitySuccessor(s,owner,city))FoundAggregateState(s,city,owner);ClaimDevelopmentProvince(s,city,owner);record.UnrestPolityId=t.PolityId;record.AutonomyPressure=100;}
        }
        void AdvanceCommunityPolicy(PrehistoryState s,int polity,int elapsed)
        {
            if(LivingWorldHistory(s)){AdvancePoliticalProgram(s,polity,elapsed);return;}
            var h=s.History;var p=h.Polities[polity];var ds=h.Dynamics.States[polity];var policy=h.Aggregates.Communities.Governments[polity];var hc=h.Cultures[p.CultureId];var leader=h.Dynamics.Leaders[ds.LeaderId];
            double printing=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years),comms=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telecommunications,s.Years);
            double pressure=.4*printing+.3*comms+.3*p.Exhaustion/1000.0;
            pressure+=.20*D.Clamp(h.Aggregates.Polities[polity].Urban/Math.Max(1.0,p.Population*.20));
            if(s.Years-policy.LastReformYear<30||HistoryRoll("community-rights-reform",polity,s.Years)>1-Math.Exp(-.015*pressure*elapsed))return;
            policy.LastReformYear=s.Years;bool concede=leader.Diplomacy+leader.Administration>1000&&p.Legitimacy>350;
            if(concede)
            {
                policy.RepresentationMilli=Math.Min(950,policy.RepresentationMilli+150);policy.CentralizationMilli=Math.Max(100,policy.CentralizationMilli-60);
                p.Legitimacy=Math.Min(950,p.Legitimacy+80);ds.PublicWorks=Math.Max(0,ds.PublicWorks-30);
                if(policy.RepresentationMilli>=650)p.Government="Representative institutions with elected governments and local councils";
                HistoryEvent(s,"political-reform",p.Name+" negotiates wider participation and local privileges; its communities retain their own cultural traditions.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
            }
            else
            {
                policy.CentralizationMilli=Math.Min(950,policy.CentralizationMilli+75);p.Legitimacy=Math.Max(100,p.Legitimacy-70);p.Exhaustion=Math.Min(1000,p.Exhaustion+60);
                if(TechnicalHistory(s))
                {policy.RepresentationMilli=Math.Max(50,policy.RepresentationMilli-(p.Exhaustion>300?150:75));if(policy.RepresentationMilli<650)p.Government="A central executive and negotiated provincial privileges";}
                HistoryEvent(s,"political-repression",p.Name+" strengthens central control amid demands for participation, increasing the risk of a disputed transfer of power.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
            }
        }
    }
}

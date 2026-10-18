using System.Collections.Generic;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        public static AtlasSection? SovereigntySection(PrehistoryState s,int city)
        {
            var p=Prehistory.SovereigntyCaseAt(s,city);var transfer=Prehistory.SovereigntyTransferAt(s,city);if(p==null&&transfer==null)return null;
            var facts=new List<AtlasFact>();var links=new List<AtlasLink>();string text="";
            string State(int id)=>id<0?"Local self-government":s.History.Polities[id].Name;
            if(p!=null)
            {
                string request=p.Request==HistoricalSovereigntyRequest.Protection?"Protection agreement":p.Request==HistoricalSovereigntyRequest.Independence?"Local independence claim":"Loss of administration";
                facts.Add(F("Latest local dispute",request+" · "+p.Outcome));facts.Add(F("Opened",Y(s,p.StartedYear)));
                if(p.ResolvedYear>=0)facts.Add(F("Decided",Y(s,p.ResolvedYear)));
                if(p.Candidate>=0){links.Add(StateLink(s,s.History.Polities[p.Candidate]));facts.Add(F("Proposed protector",State(p.Candidate)));}
                if(p.Request==HistoricalSovereigntyRequest.Independence)facts.Add(F("Organized coalition support",p.Support+" / 1,000"));
                text=p.Outcome==HistoricalSovereigntyOutcome.Pending?"A local political dispute is in progress. A petition does not change allegiance or begin a war. Reachable protection, finance, local acceptance and the incumbent court's ability to govern are tested separately.":"The latest local political dispute has concluded; the dated decision and earlier episodes remain in recorded history.";
            }
            if(transfer!=null)
            {
                string cause=transfer.Cause==HistoricalSovereigntyCause.Conquest?"Military conquest":transfer.Cause==HistoricalSovereigntyCause.NegotiatedProtection?"Negotiated protection":transfer.Cause==HistoricalSovereigntyCause.Secession?"Local secession":transfer.Cause==HistoricalSovereigntyCause.AdministrativeWithdrawal?"Administrative withdrawal":transfer.Cause==HistoricalSovereigntyCause.LocalFoundation?"Local state foundation":transfer.Cause==HistoricalSovereigntyCause.SettlementAbandonment?"Settlement abandonment":transfer.Cause==HistoricalSovereigntyCause.SponsoredSettlement?"Sponsored settlement foundation":transfer.Cause==HistoricalSovereigntyCause.FrontierAdministration?"Local frontier administration":"Administrative reassignment";
                facts.Add(F("Last governing transfer",cause+" · "+Y(s,transfer.Year)));facts.Add(F("Former administration",State(transfer.From)));facts.Add(F("Receiving administration",State(transfer.To)));
                if(transfer.From>=0)links.Add(StateLink(s,s.History.Polities[transfer.From]));
                if(transfer.To>=0)links.Add(StateLink(s,s.History.Polities[transfer.To]));
                if(transfer.Campaign>=0){var c=s.History.Dynamics.Campaigns[transfer.Campaign];facts.Add(F("Actual campaign",c.Name));}
                if(transfer.Cause==HistoricalSovereigntyCause.NegotiatedProtection)facts.Add(F("Rights offered by protector",transfer.Rights+" / 1,000"));
                text+=(text.Length>0?" ":"")+"In "+Y(s,transfer.Year)+", administration passed from "+State(transfer.From)+" to "+State(transfer.To)+" through "+cause.ToLowerInvariant()+". This records government, not replacement of the inhabitants, language or faith. Recognized title and delivered territorial service remain separate accounts.";
            }
            return new AtlasSection{Title="Allegiance and sovereignty",Facts=facts.ToArray(),Links=links.ToArray(),Text=text};
        }
    }
}

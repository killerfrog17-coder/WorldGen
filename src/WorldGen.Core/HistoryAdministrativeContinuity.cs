using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void RetainConqueredAdministration(PrehistoryState s,Settlement town,HistoricalPolity former,HistoricalPolity successor,bool formerCapital,CancellationToken token)
        {
            if(!DelegatedHistory(s))return;var h=s.History;var a=h.Administration??(h.Administration=new HistoricalAdministration());
            var old=a.Offices.LastOrDefault(o=>o.TownId==town.Id&&o.PolityId==former.Id&&o.EndedYear<0);
            if(old!=null)old.EndedYear=s.Years;
            bool established=old!=null||formerCapital&&town.Population>=5000&&h.Towns[town.Id].CivicPreparationYears>=150;
            if(!established||town.Population<3000||!HistoricalTechnologies.Has(h.Cultures[successor.CultureId],HistoricalTechnology.Writing))return;
            long annual=old?.AnnualCost??(50+town.Population/200),setup=500+town.Population/80;
            if(successor.Treasury<setup+annual*10||BuildAdministrativeNetwork(s,token).CommunicationOwner[town.Cell]!=successor.Id)return;
            // Preserve the former record. Retaining local clerks, accounts and
            // officials is paid continuity, not automatically acquired culture.
            successor.Treasury-=setup;a.Offices=a.Offices.Concat(new[]{new HistoricalDistrictOffice{Id=a.Offices.Length,PolityId=successor.Id,TownId=town.Id,FoundedYear=s.Years,AnnualCost=annual,PredecessorOfficeId=old?.Id??-1,PredecessorPolityId=former.Id}}).ToArray();
            HistoryEvent(s,"administration-retained",successor.Name+" retains established accounts and local officials at "+town.Name+" under a funded provincial office after conquest. Their former service to "+former.Name+" remains part of the city's history.",town.Cell,town.CultureId,successor.Id,former.Id,town.Id,category:ChronicleCategory.Society);
        }
    }
}

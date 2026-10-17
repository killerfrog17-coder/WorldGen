namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // A growing culture's hundredth town must not wait a hundred culture
        // draws to feed its own ritual house. Reuse the existing city calendar;
        // this is neither another queue nor a territory/culture membership walk.
        static bool LocalReligiousCityHistory(PrehistoryState s)
            =>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=8&&ReligiousNetworkHistory(s);

        void AdvanceLocalReligiousCity(PrehistoryState s,int city)
        {
            if(!LocalReligiousCityHistory(s)||city<0||city>=s.Society.Settlements.Length)return;
            var town=s.Society.Settlements[city];
            if(town.Population<=0||s.History.Aggregates.Living.Cities[city].AbandonedYear>=0)return;
            AdvanceReligiousCommunity(s,town.CultureId,town.Cell,city);
        }

        void InitializeLocalReligiousCityClocks(PrehistoryState s)
        {
            if(!LocalReligiousCityHistory(s))return;
            // Explicit upgrade ONLY: a bounded actor migration, not recurring
            // work. Missed centuries of the preceding sampled clock are not
            // unpaid invoices. Preserve public/household rites, support, dated
            // visitors, dissent and institutions; start new upkeep prospectively.
            var a=s.History.Aggregates;var r=a.Empires.Connections.Religion;
            foreach(var town in s.Society.Settlements)
            {
                if(town.Population<=0||a.Living.Cities[town.Id].AbandonedYear>=0||HistoricalCells.Religion(s,town.Cell)<0)continue;
                TouchReligiousCommunity(s,town.Cell);r.Write(town.Cell,FaithReviewed,s.Years);
            }
        }
    }
}

using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalExchangeMemory
    {
        public int[] Peers=Array.Empty<int>(),LastYear=Array.Empty<int>(),LastDirectYear=Array.Empty<int>();
        public long[] Capabilities=Array.Empty<long>();
        public HistoricalExchangeMemory Copy()=>new HistoricalExchangeMemory{Peers=(int[])Peers.Clone(),LastYear=(int[])LastYear.Clone(),LastDirectYear=(int[])LastDirectYear.Clone(),Capabilities=(long[])Capabilities.Clone()};
    }
    public sealed partial class Prehistory
    {
        static long ProvincialKnowledge(HistoricalCulture c)
        {long mask=0;for(int k=0;k<c.Technology.Length;k++)if(c.Technology[k]>0)mask|=1L<<k;return mask;}
        static int ProvincialKnowledgeCount(long mask)
        {
            ulong bits=(ulong)mask;bits-=bits>>1&0x5555555555555555UL;bits=(bits&0x3333333333333333UL)+(bits>>2&0x3333333333333333UL);
            return (int)((((bits+(bits>>4))&0x0f0f0f0f0f0f0f0fUL)*0x0101010101010101UL)>>56);
        }
        int ProvincialTeacher(PrehistoryState s,int receiver,int technology)
        {
            var memory=s.History.Aggregates.Provinces.Exchanges[receiver];int best=-1,breadth=-1;
            for(int k=0;k<memory.Peers.Length;k++)
            {
                if((memory.Capabilities[k]&(1L<<technology))==0)continue;int peer=memory.Peers[k];
                int year=s.History.Cultures[peer].Technology[technology];if(year<=0||year-1>=s.Years)continue;
                int score=ProvincialKnowledgeCount(memory.Capabilities[k]);if(score>breadth||score==breadth&&(best<0||peer<best)){best=peer;breadth=score;}
            }
            return best;
        }
        void OfferProvincialKnowledge(PrehistoryState s,int receiver,int sender,long mask,int year,bool direct)
        {
            if(receiver<0||sender<0||receiver==sender)return;var p=s.History.Aggregates.Provinces;var memory=p.Exchanges[receiver];
            OfferTechnicalKnowledge(s,receiver,sender,mask,year);
            int at=Array.IndexOf(memory.Peers,sender);
            if(at<0)
            {
                if(memory.Peers.Length<16){at=memory.Peers.Length;Array.Resize(ref memory.Peers,at+1);Array.Resize(ref memory.LastYear,at+1);Array.Resize(ref memory.LastDirectYear,at+1);Array.Resize(ref memory.Capabilities,at+1);memory.LastDirectYear[at]=-1;}
                else{at=0;for(int k=1;k<16;k++)if(memory.LastYear[k]<memory.LastYear[at])at=k;memory.Capabilities[at]=0;memory.LastDirectYear[at]=-1;}
                memory.Peers[at]=sender;
                s.Society.Cultures[receiver].Contacts=(int[])memory.Peers.Clone();
            }
            memory.Capabilities[at]|=mask;memory.LastYear[at]=Math.Max(memory.LastYear[at],year);if(direct)memory.LastDirectYear[at]=Math.Max(memory.LastDirectYear[at],year);
        }
        bool ProvincialHasOffer(PrehistoryState s,int receiver,int sender,int technology)
        {var memory=s.History.Aggregates.Provinces.Exchanges[receiver];int at=Array.IndexOf(memory.Peers,sender);return at>=0&&(memory.Capabilities[at]&(1L<<technology))!=0;}
        bool ProvincialRecentContact(PrehistoryState s,int receiver,int sender,bool direct)
        {var memory=s.History.Aggregates.Provinces.Exchanges[receiver];int at=Array.IndexOf(memory.Peers,sender);return at>=0&&s.Years-(direct?memory.LastDirectYear[at]:memory.LastYear[at])<=250;}
        long ProvincialRegionalKnowledge(PrehistoryState s,int region,int teacher)
        {
            if(teacher<0)return 0;var a=s.History.Aggregates;
            long known=a.Provinces.RegionalKnowledge[region];
            if(a.Regions[region].Culture==teacher)known|=CurrentProvincialKnowledge(s,teacher);
            int court=ProvincialRegionalCourt(s,region);
            if(court>=0&&s.History.Polities[court].CultureId==teacher)known|=CurrentProvincialKnowledge(s,teacher);
            return known&CurrentProvincialKnowledge(s,teacher);
        }
    }
}

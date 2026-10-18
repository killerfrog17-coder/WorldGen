using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Social recognition is independent of the advanced-culture actor.
        // Pigmentation/body variation already resides in regional Ancestry.
        void InitializeForagerHeritage(PrehistoryState s)
        {
            var r=s.History.Identities;
            if(s.History.Aggregates.Empires.Connections.Realms.Version<36||r==null||r.Version>=2)return;
            r.Version=2;
            // Explicit migration only; no historical ancestry is reconstructed.
            for(int region=0;region<s.Population.Length;region++)EnsureForagerHeritage(s,region);
        }
        int EnsureForagerHeritage(PrehistoryState s,int region)
        {
            var r=s.History.Identities;if(r==null||r.Version<2||region<0)return -1;
            int id=r.Regions[region*12];if(id>=0)return id;
            long residents=s.Population[region]+s.History.Aggregates.Living.Regions[region].SettledPopulation;
            if(residents<=0)return -1;
            int cell=s.History.Aggregates.Regions[region].BestCell,culture=s.History.Aggregates.Regions[region].Culture;
            id=culture>=0?r.CultureDefaults[culture]:-1;
            if(id<0)id=AddPeopleIdentity(s,culture,cell);
            StoreEthnicCommunity(s,cell,new EthnicCommunity{Primary=id,Cohesion=450,HomeAttachment=500,SinceYear=s.Years,ReviewedYear=s.Years},true);
            return id;
        }
        void CarryRegionalHeritage(PrehistoryState s,int from,int to,long migrants,long previousResidents)
        {
            var r=s.History.Identities;if(r?.Version<2||r==null||migrants<=0||from==to)return;
            int donor=EnsureForagerHeritage(s,from);if(donor<0)return;
            var incoming=ReadEthnic(r.Regions,from*12,true);
            var local=r.Regions[to*12]<0?null:ReadEthnic(r.Regions,to*12,true);
            long residents=Math.Max(0,previousResidents)+s.History.Aggregates.Living.Regions[to].SettledPopulation;
            if(local==null||residents==0)
            {local=incoming;local.HomeAttachment=200;local.SinceYear=local.ReviewedYear=s.Years;local.ContactGroup=-1;local.ContactYear=-1;local.MixedYears=local.SeparatedYears=local.ContactStrength=0;}
            else
            {
                double share=SpokenLanguageEffects.MigrantShare(residents,migrants)/1000.0;
                var ids=new[]{local.Primary,local.Minority,incoming.Primary,incoming.Minority};
                var weights=new[]{(1000-local.MinorityPermille)*(1-share),local.MinorityPermille*(1-share),(1000-incoming.MinorityPermille)*share,incoming.MinorityPermille*share};
                var top=ids.Where(x=>x>=0).Distinct().Select(id=>new{Id=id,Weight=ids.Select((x,k)=>x==id?weights[k]:0).Sum()}).OrderByDescending(x=>x.Weight).ThenBy(x=>x.Id).Take(2).ToArray();
                local.Primary=top[0].Id;local.Minority=top.Length>1?top[1].Id:-1;local.MinorityPermille=top.Length>1?(int)Math.Round(top[1].Weight):0;
                local.ContactGroup=donor;local.ContactYear=s.Years;local.ContactStrength=Math.Min(1000,local.ContactStrength+100);
            }
            StoreEthnicCommunity(s,s.History.Aggregates.Regions[to].BestCell,local,true);
        }
    }
}

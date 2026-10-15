using System;
using System.Linq;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        sealed class LocalFiscalCache
        {public int[] Signature;public readonly Dictionary<int,double> Cells=new Dictionary<int,double>();}
        readonly Dictionary<int,LocalFiscalCache> localFiscalCaches=new Dictionary<int,LocalFiscalCache>();
        PrehistoryState localFiscalState;string localFiscalHash;
        int[] FiscalSignature(PrehistoryState s,int culture)
        {
            var c=s.Society.Cultures[culture];var a=s.History.Agriculture[culture];
            var key=new List<int>{c.Farming,c.Fishing,HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation)?1:0};
            for(int k=0;k<6;k++)
            {
                key.Add(a.AdoptedYear[k]>0?1:0);key.Add(a.HusbandryYears?[k]??0);key.Add(a.PracticeCell[k]);var v=a.Cultivars[k];
                if(v==null){key.Add(-1);continue;}
                key.Add(v.CropId);key.Add(v.MaturityDays);key.Add(v.DaylengthLatitudeTenths);key.Add(v.WaterRequirementPermille);key.Add(v.MinimumWarmTenthsC);key.Add(v.YieldPermille);
            }
            return key.ToArray();
        }
        void PrepareFiscalCache(PrehistoryState s)
        {
            if(ReferenceEquals(s,localFiscalState)&&s.Hash==localFiscalHash)return;
            foreach(var culture in s.Society.Cultures)
            {
                var key=FiscalSignature(s,culture.Id);
                if(!localFiscalCaches.TryGetValue(culture.Id,out var old)||!key.SequenceEqual(old.Signature))localFiscalCaches[culture.Id]=new LocalFiscalCache{Signature=key};
            }
            localFiscalState=s;localFiscalHash=s.Hash;
        }
        void InvalidateFiscalCache(int culture)=>localFiscalCaches.Remove(culture);
        void ResetFiscalCaches(){localFiscalCaches.Clear();localFiscalState=null;}
        double LocalFiscalPotential(PrehistoryState s,int culture,int cell)
        {
            if(!ModernHistory(s))return UncachedLocalFiscalPotential(s,culture,cell);
            PrepareFiscalCache(s);
            if(!localFiscalCaches.TryGetValue(culture,out var cache)){cache=new LocalFiscalCache{Signature=FiscalSignature(s,culture)};localFiscalCaches[culture]=cache;}
            if(!cache.Cells.TryGetValue(cell,out double value)){value=UncachedLocalFiscalPotential(s,culture,cell);cache.Cells.Add(cell,value);}
            return value;
        }
    }
}

using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

// Actual immutable inventory controls; opt-in diagnostic, never a model turn.
public static class HistorySeedLandscapeCacheTests
{
    static object Call(Prehistory e,string name,params object[] values)
        =>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,values)!;
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));
        var original=world.Prehistory;string before=original.ComputeHash();
        var query=original.Copy();var cached=new Prehistory(world);var literal=new Prehistory(world){UseSeedLandscapeCache=false};
        Call(cached,"PrepareAggregateEnvironment",query);Call(cached,"PrepareAggregateRelations",query);
        Call(literal,"PrepareAggregateEnvironment",query);Call(literal,"PrepareAggregateRelations",query);
        var towns=original.Society.Settlements.Where(t=>t.Population>0&&original.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0)
            .Where(t=>cached.LocalSeedsAt(original,t.Id)!=null).Take(24).ToArray();
        check(towns.Length>=4,"Actual saved towns provide immutable field inventories, not invented world cells.");
        double Landscape(Prehistory e,int culture,int cell,HistoricalTownSeeds seeds,bool carried,bool stored)
            =>(double)Call(e,"CommunitySeededCropLandscape",query,culture,cell,stored,seeds,carried);
        void Compare(int culture,int cell,HistoricalTownSeeds seeds,bool carried,bool stored)
        {
            double expected=Landscape(literal,culture,cell,seeds,carried,stored);
            double cold=Landscape(cached,culture,cell,seeds,carried,stored),warm=Landscape(cached,culture,cell,seeds,carried,stored);
            check(BitConverter.DoubleToInt64Bits(cold)==BitConverter.DoubleToInt64Bits(expected)&&BitConverter.DoubleToInt64Bits(warm)==BitConverter.DoubleToInt64Bits(expected),
                "Cold and warm whole-landscape results retain literal quantized bytes.");
        }
        int revision=query.History.Aggregates.Empires.Connections.Realms.Version;
        foreach(var town in towns)
        {
            var hc=query.History.Cultures[town.CultureId];
            int crop=(int)HistoricalTechnology.CropRotation,pottery=(int)HistoricalTechnology.Pottery;
            int oldCrop=hc.Technology[crop],oldPottery=hc.Technology[pottery];
            int oldCropDeployment=hc.Technical.Deployment[crop],oldPotteryDeployment=hc.Technical.Deployment[pottery];
            var seeds=cached.LocalSeedsAt(query,town.Id)!;
            foreach(int rules in new[]{14,18,19,21,22,26})
            foreach(bool managed in new[]{false,true})foreach(bool pots in new[]{false,true})
            {
                query.History.Aggregates.Empires.Connections.Realms.Version=rules;
                hc.Technology[crop]=managed?query.Years+1:0;hc.Technology[pottery]=pots?query.Years+1:0;
                hc.Technical.Deployment[crop]=managed?1000:0;hc.Technical.Deployment[pottery]=pots?1000:0;
                check(HistoricalTechnologies.Has(hc,HistoricalTechnology.CropRotation)==managed&&HistoricalTechnologies.Has(hc,HistoricalTechnology.Pottery)==pots,"Management and pottery fixture flags actually reach both live branches.");
                foreach(bool carried in new[]{false,true})foreach(bool stored in new[]{false,true})
                    Compare(town.CultureId,town.Cell,seeds,carried,stored);
            }
            hc.Technology[crop]=oldCrop;hc.Technology[pottery]=oldPottery;
            hc.Technical.Deployment[crop]=oldCropDeployment;hc.Technical.Deployment[pottery]=oldPotteryDeployment;
            query.History.Aggregates.Empires.Connections.Realms.Version=revision;

            var lots=Enumerable.Range(0,6).Select(k=>seeds[k]).ToArray();
            // A colliding holder has DIFFERENT content, so returning the
            // previous slot's value cannot accidentally pass a parity check.
            var collision=new HistoricalTownSeeds(seeds.TownId+16384,seeds.FieldCell,new HistoricalLocalSeedlot[6]);
            foreach(bool stored in new[]{false,true})
            {
                Compare(town.CultureId,town.Cell,seeds,true,stored);
                Compare(town.CultureId,town.Cell,collision,true,stored);
                Compare(town.CultureId,town.Cell,seeds,true,stored);
            }
            int group=Array.FindIndex(lots,p=>p!=null);
            if(group>=0)
            {
                var copy=lots[group].Cultivar;copy.SelectedYears+=13;copy.DaylengthLatitudeTenths+=30;
                check(lots[group].Cultivar.SelectedYears!=copy.SelectedYears,"Public cultivar queries return copies, not writable cached seed profiles.");
                lots[group]=new HistoricalLocalSeedlot(copy,query.Years,town.Id,town.Cell,lots[group].PracticeYears+100,query.Years);
                var replacement=new HistoricalTownSeeds(seeds.TownId,seeds.FieldCell,lots);
                foreach(bool stored in new[]{false,true})Compare(town.CultureId,town.Cell,replacement,true,stored);
            }
            var adoption=query.History.Agriculture[town.CultureId].AdoptedYear;
            var saved=(int[])adoption.Clone();Array.Clear(adoption,0,adoption.Length);
            foreach(bool stored in new[]{false,true})Compare(town.CultureId,town.Cell,seeds,true,stored);
            Array.Copy(saved,adoption,saved.Length);
            foreach(bool stored in new[]{false,true})Compare(town.CultureId,town.Cell,null,true,stored);
            int otherCulture=(town.CultureId+1)%query.History.Cultures.Length;
            foreach(bool stored in new[]{false,true})Compare(otherCulture,town.Cell,seeds,true,stored);
            int other=world.Surface.Cells[town.Cell].Neighbors.Where(n=>cached.CellPatch[n]>=0).DefaultIfEmpty(town.Cell).First();
            foreach(bool stored in new[]{false,true})Compare(town.CultureId,other,seeds,true,stored);
        }
        check(cached.SeedLandscapeCacheHits>0&&cached.SeedLandscapeCacheMisses>0,"Actual town queries exercise both whole-field memo misses and hits.");
        check(literal.SeedLandscapeCacheHits==0&&literal.SeedLandscapeCacheMisses==0,"The explicit literal switch bypasses the entire new memo.");
        var control=new Prehistory(world){UseSeedLandscapeCache=false};var optimized=new Prehistory(world);
        var full=optimized.AdvanceYears(original,12);var old=control.AdvanceYears(original,12);
        check(full.Hash==old.Hash,"Actual12-year population, crops, faith, war, calendar and stock histories are identical with the whole-field memo disabled.");
        var five=optimized.AdvanceYears(original,5);world.Prehistory=five;
        try
        {
            var cold=WorldFile.Read(WorldFile.Write(world));var continuation=new Prehistory(cold);
            var split=continuation.AdvanceYears(cold.Prehistory,7);
            check(split.Hash==full.Hash,"Actual12 years equal5 years, full-world cold save/load and7 further years with an empty runtime memo.");
        }
        finally{world.Prehistory=original;}
        check(original.ComputeHash()==before,"Cache comparisons and cold continuation preserve the original saved checkpoint.");
        Console.WriteLine("Seed landscape exact12/cold5+7 "+full.Hash+"; query hits "+cached.SeedLandscapeCacheHits+", misses "+cached.SeedLandscapeCacheMisses);
    }
}

using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryInstitutionalTransmissionTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(0,0,0)==35,"Primitive dispersal is not a dependable long-range farm-package trade connection");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,0,0)==120,"Sailing alone cannot establish routine transoceanic institutional contact");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,1,0,1)==350,"Initial traditional navigation can link islands and short open-water gaps");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,1,0,8)==800,"Developed pre-ocean navigation remains distinct from ocean-going navigation");
        check(HistoricalInstitutionalTransmission.OpenWaterLimitKm(1,1,1)==14000,"Ocean-going craft can deliver knowledge across oceans without an arbitrary continent ban");
        double full=HistoricalInstitutionalTransmission.RelayExposure(1000,1,100),second=HistoricalInstitutionalTransmission.RelayExposure(full,1,100);
        check(full==550&&second==302.5,"A relay transmits its actual local institutional experience rather than resetting to civilization-global experience");
        check(HistoricalInstitutionalTransmission.RelayExposure(1000,.01,100)==5.5,"Forest, latitude and route resistance constrain civic transmission as well as ordinary contact");
        check(HistoricalInstitutionalTransmission.RelayExposure(1000,1,50)*2==full,"Exposure delivery scales with elapsed opportunities rather than UI publication frequency");
        check(HistoricalInstitutionalTransmission.RegularOceanDelivery(4000,4,0)<.01,"Oral ocean voyaging is not automatically frequent transoceanic institutional trade");
        check(HistoricalInstitutionalTransmission.RegularOceanDelivery(4000,4,2)>.7,"Developed ocean infrastructure can connect the same ocean without a permanent historical blockade");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var before=world.Prehistory;string original=before.ComputeHash();
        var s=engine.UpgradeToLivingWorldHistory(before);engine.Validate(s);
        check(s.History.Version==Prehistory.LivingWorldHistoryVersion&&before.ComputeHash()==original,"Physical civic-delivery rules require explicit upgrade and preserve the old checkpoint");
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values)!;
        int[] shores=Enumerable.Range(0,world.Surface.Cells.Length).Where(c=>!world.Hydrology.OceanConnected[c]&&(bool)Call("DiffusionShore",c)).ToArray();
        int from=-1,to=-1;double best=0;
        // Explicit diagnostics only. Recurring model events never enumerate
        // these cells, shores or holdings.
        for(int i=0;i<Math.Min(64,shores.Length);i++)for(int j=0;j<Math.Min(64,shores.Length);j++)
        {
            int first=shores[i*shores.Length/Math.Min(64,shores.Length)],second=shores[j*shores.Length/Math.Min(64,shores.Length)];
            double km=D.Angle(world.Surface.Cells[first].Position,world.Surface.Cells[second].Position)*world.Recipe.RadiusKm;
            if(km<1800||km>9000||!(bool)Call("DiffusionSeaLane",first,second))continue;
            object[] values={first,second,false};double open=(double)Call("InstitutionalUnsupportedWaterKm",values);
            if(open>best){best=open;from=first;to=second;}
        }
        check(from>=0&&best>1500,"Control contains a genuine unsupported open-ocean passage rather than assuming continents imply isolation");
        int culture=0;var hc=s.History.Cultures[culture];
        foreach(var technology in new[]{HistoricalTechnology.Sailing,HistoricalTechnology.Navigation})
        {int k=(int)technology;hc.Technology[k]=1;hc.Technical.Levels[k]=1;hc.Technical.Deployment[k]=1000;hc.Technical.LevelYear[k]=0;}
        int ocean=(int)HistoricalTechnology.OceanNavigation;hc.Technology[ocean]=0;hc.Technical.Levels[ocean]=0;hc.Technical.Deployment[ocean]=0;
        check(!(bool)Call("InstitutionalRouteAllowed",s,from,to,culture),"An actually unsupported ocean passage cannot teach state institutions with coastal navigation alone");
        hc.Technology[ocean]=1;hc.Technical.Levels[ocean]=8;hc.Technical.Deployment[ocean]=1000;hc.Technical.LevelYear[ocean]=0;
        check((bool)Call("InstitutionalRouteAllowed",s,from,to,culture),"The same passage becomes eligible when ocean-going craft actually exist");
        object[] forward={from,to,false},reverse={to,from,false};double firstOpen=(double)Call("InstitutionalUnsupportedWaterKm",forward),reverseOpen=(double)Call("InstitutionalUnsupportedWaterKm",reverse);
        check(firstOpen==reverseOpen&&(bool)forward[2]==(bool)reverse[2],"Geometry-only route cache has canonical direction and cannot change reverse delivery");
        var cropProbe=engine.UpgradeToLivingWorldHistory(before).Copy();int receiver=-1,donor=-1,group=-1,site=-1;
        for(int r=0;r<Math.Min(12,cropProbe.Society.Cultures.Length)&&donor<0;r++)
        for(int d=0;d<Math.Min(12,cropProbe.Society.Cultures.Length)&&donor<0;d++)
        for(int g=0;g<6;g++)
        {
            if(r==d||cropProbe.History.Agriculture[d].Cultivars[g]==null)continue;int cell=cropProbe.History.Agriculture[r].PracticeCell[g];
            double fit=(double)Call("CommunityTransferFit",cropProbe,cell,cropProbe.History.Agriculture[d].Cultivars[g],false);
            if(fit<=.12)continue;receiver=r;donor=d;group=g;site=cell;break;
        }
        check(donor>=0,"Crop-delivery control has an agronomically suitable donor, not a habitat-induced false rejection");
        cropProbe.Society.Cultures[receiver].Contacts=new[]{donor};
        var memory=cropProbe.History.Aggregates.Provinces.Exchanges[receiver];memory.Peers=new[]{donor};memory.LastYear=new[]{cropProbe.Years-100};memory.LastDirectYear=new[]{cropProbe.Years-100};memory.Capabilities=new[]{long.MaxValue};
        cropProbe.History.Agriculture[donor].AdoptedYear[group]=cropProbe.Years-10;
        check((int)Call("DevelopmentCropDonor",cropProbe,receiver,group,site,donor)==-1,"A remembered donor cannot supply a crop domesticated after the actual delivery");
        memory.LastYear[0]=cropProbe.Years;
        check((int)Call("DevelopmentCropDonor",cropProbe,receiver,group,site,donor)==donor,"A fresh delivery can introduce the same suitable crop rather than blocking diffusion categorically");
        var next=engine.AdvanceYears(engine.UpgradeToLivingWorldHistory(before),20);var split=engine.AdvanceYears(engine.AdvanceYears(engine.UpgradeToLivingWorldHistory(before),7),13);
        check(next.Hash==split.Hash,"Physical transmission and local civic exposure are invariant to publication partitioning");
        Console.WriteLine($"Civic ocean control: {best:F1} km unsupported passage, coastal navigation rejected, installed ocean craft accepted.");
    }
}

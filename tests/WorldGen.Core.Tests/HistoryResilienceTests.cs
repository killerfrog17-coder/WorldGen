using WorldGen.Core;
public static class HistoryResilienceTests
{
    public static void Run(Action<bool,string> check)
    {
        var cargo=HistoricalFoodEffects.Deliver(100,70,800,200);
        check(cargo.Sent==88&&cargo.Received==70&&cargo.Lost==18,"Food shipments account for cargo, delivered food and transit loss");
        check(100-cargo.Sent+cargo.Received+cargo.Lost==100,"Trade moves finite food without creating a harvest bonus");
        cargo=HistoricalFoodEffects.Deliver(100,200,800,50);check(cargo.Sent==50&&cargo.Received==40,"Local cargo capacity limits emergency provisioning");
        cargo=HistoricalFoodEffects.Deliver(20,200,1000,100);check(cargo.Sent==20&&cargo.Received==20,"Missing surplus cannot be promised as food relief");
        check(HistoricalFoodEffects.Deliver(-1,20,800,50).Sent==0&&HistoricalFoodEffects.Deliver(100,20,0,50).Sent==0,"Invalid food supply or retention creates no shipment");
        cargo=HistoricalFoodEffects.Deliver(long.MaxValue,long.MaxValue,999,long.MaxValue);check(cargo.Received<cargo.Sent&&cargo.Sent==long.MaxValue,"Large integer provisioning avoids multiplication overflow");
        check(HistoricalFoodEffects.ResilientLoss(.6,4,800,true,true)<HistoricalFoodEffects.ResilientLoss(.6,1,0,false,true),"Food diversity and supported infrastructure buffer crop shocks");
        check(HistoricalFoodEffects.ResilientLoss(.6,6,1000,true,true)>0,"Infrastructure cannot make crops immune to environmental shocks");
        var p=new V(1,0,0);check(HistoricalFoodEffects.ShockWeight(p,p,1200,6371)==1,"Regional shocks are strongest near their source");
        check(HistoricalFoodEffects.ShockWeight(p,new V(-1,0,0),1200,6371)==0,"A local crop shock cannot span the whole planet");
        double near=HistoricalDiseaseEffects.Transmission(HistoricalDiseaseKind.Respiratory,50,1),far=HistoricalDiseaseEffects.Transmission(HistoricalDiseaseKind.Respiratory,700,1);
        check(near>far&&near>0&&far>0,"Disease contact weakens with physical distance without an arbitrary longitude rule");
        check(HistoricalDiseaseEffects.Transmission(HistoricalDiseaseKind.VectorFever,50,0)==0,"Unsuitable habitat prevents the current vector-fever proxy");
        check(HistoricalDiseaseEffects.CombineExposure(.1,.1)==.19,"Multiple contacts increase exposure without adding probabilities past one");
        check(HistoricalDiseaseEffects.CombineExposure(1,1)==.45,"Annual network exposure is bounded");
        check(!HistoricalDiseaseEffects.Susceptible(100,110,0)&&!HistoricalDiseaseEffects.Susceptible(100,90,120)&&HistoricalDiseaseEffects.Susceptible(130,110,120),"Active disease and community recovery constrain repeated outbreaks");
        check(HistoricalDiseaseEffects.AnnualDeaths(10000,110,1)==1100,"Severe epidemic mortality is explicitly applied to resident population");
        check(HistoricalDiseaseEffects.AnnualDeaths(10000,110,.5)>1100,"Food shortage can worsen the coarse outbreak impact");
        check(HistoricalDiseaseEffects.AnnualDeaths(100,10000,0)==100,"Even extreme mortality cannot remove more than the living population");
        var town=new HistoricalTown{KindImmunity=new[]{20,30,40,50}};var copy=town.Copy();copy.KindImmunity[0]=100;
        check(town.KindImmunity[0]==20&&StateHash.Compute(town)!=StateHash.Compute(copy),"Syndrome recovery checkpoints deep-copy and hash their family-specific state");
    }
}

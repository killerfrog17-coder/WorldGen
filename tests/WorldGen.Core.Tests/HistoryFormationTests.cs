using WorldGen.Core;
public static class HistoryFormationTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalFormationEffects.EmergenceFloor(4500)==.34&&HistoricalFormationEffects.EmergenceFloor(10000)==.2,"Accumulated local practice opens marginal habitats, not an unconditional time unlock");
        check(HistoricalFormationEffects.EmergenceFloor(100000)==.2,"Experience never makes sterile habitat an automatic culture");
        int prepared=0;for(int year=0;year<149;year++)prepared=HistoricalFormationEffects.Preparation(prepared,6000,7000,10000,750,650,.5);
        check(prepared==149,"One good harvest is not a prepared urban institution");
        check(HistoricalFormationEffects.Preparation(prepared,6000,7000,10000,750,650,.5)==150,"Sustained physical support can prepare an independent center");
        check(HistoricalFormationEffects.Preparation(150,6000,3000,10000,750,650,.5)==148,"Insufficient local production erodes preparation despite a culture-wide surplus");
        check(HistoricalFormationEffects.Preparation(150,6000,7000,100,750,650,.5)==148,"Depleted reserves erode preparation");
        check(HistoricalFormationEffects.Preparation(150,6000,7000,10000,750,100,.5)==148,"Food without organized institutions is insufficient");
        check(HistoricalFormationEffects.Preparation(150,6000,7000,10000,750,650,.05)==148,"A large town alone cannot grant a state in an unsupported local economy");
        check(HistoricalFormationEffects.CultivationEffort(Biome.TropicalRainforest,false)>0&&HistoricalFormationEffects.CultivationEffort(Biome.TropicalRainforest,false)<1,"Forest cultivation is slower, never vetoed");
        check(HistoricalFormationEffects.CultivationEffort(Biome.TropicalRainforest,true)>HistoricalFormationEffects.CultivationEffort(Biome.TropicalRainforest,false),"Introduced forest food systems still need adaptation but shorten learning");
        int[][] graph={new[]{1},new[]{0,2},new[]{1,3},new[]{2}};int[] owners={0,-1,-1,1};bool[] occupied={true,true,true,true};
        int[] Reach(int limit)=>HistoricalExchange.NearbyCultures(0,new[]{0},i=>graph[i],i=>occupied[i],i=>owners[i],(a,b)=>100,limit);
        check(Reach(300).SequenceEqual(new[]{1}),"Inhabited forager communities relay contact without sharing either culture");
        check(owners.SequenceEqual(new[]{0,-1,-1,1}),"Exchange does not repaint local identities");
        check(Reach(250).Length==0,"Diffusion is distance bounded, not global");
        occupied[2]=false;check(Reach(1000).Length==0,"Empty corridors cannot relay exchange");occupied[2]=true;
        check(HistoricalExchange.NearbyCultures(0,new[]{0},i=>graph[i],i=>occupied[i],i=>owners[i],(a,b)=>a==1?1000:100,400).Length==0,"Physical travel barriers constrain diffusion");
        using var canceled=new CancellationTokenSource();canceled.Cancel();bool stopped=false;
        try{HistoricalExchange.NearbyCultures(0,new[]{0},i=>graph[i],i=>occupied[i],i=>owners[i],(a,b)=>100,300,canceled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"Exchange search cooperatively cancels");
        var oldTown=new HistoricalTown();string old=StateHash.Compute(oldTown);var changed=oldTown.Copy();changed.CivicPreparationYears=150;
        check(StateHash.Compute(changed)!=old&&StateHash.Compute(oldTown)==old,"Preparation is saved/hashed without mutating prior checkpoints");
    }
}

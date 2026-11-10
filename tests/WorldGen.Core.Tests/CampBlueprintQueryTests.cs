using WorldGen.Core;
using WorldGen.Serialization;

static class CampBlueprintQueryTests
{
    public static void Run()
    {
        int checks=0;
        void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
        void Compare(CampBlueprint plan,string context)
        {
            // Fresh public geometry and direct state reads are independent of
            // the private cached query geometry under test.
            var parts=CampBlueprints.Components(plan);
            bool Done(CampComponent part)
            {
                var state=plan.Parts.Find(s=>s.Key==part.Key);
                return (part.Kg>0?(state?.Materials.Sum(i=>i.MassKg)??0)+1e-8>=part.Kg:state?.Materials.Count==1)&&(state?.WorkSeconds??0)+1e-8>=part.Seconds;
            }
            Check(CampBlueprints.Complete(plan)==parts.All(Done),context+": completion");
            Check(CampBlueprints.Stage(plan)==parts.Where(p=>!Done(p)).Select(p=>p.Stage).DefaultIfEmpty(4).Min(),context+": stage");
        }
        foreach(var design in Enum.GetValues<CampDesign>())foreach(var cover in Enum.GetValues<CampCover>())
        {
            var plan=CampBlueprints.Create(design,2,2,cover);
            foreach(var part in CampBlueprints.Components(plan))plan.Parts.Add(new CampPartState{Key=part.Key,WorkSeconds=part.Seconds,Materials=new(){new Possession{MassKg=part.Kg}}});
            Compare(plan,"Initial "+design);
            var before=WorldJson.Write(plan);for(int i=0;i<8;i++)Compare(plan,"Warm "+design);
            Check(WorldJson.Write(plan)==before,"Query cache must not enter serialized state");
            var exposed=CampBlueprints.Components(plan);exposed.Clear();Compare(plan,"Caller-owned list");
            plan.Parts[0].WorkSeconds=0;Compare(plan,"Work changed without revision");plan.Parts[0].WorkSeconds=1000;
            plan.Parts[0].Materials.Clear();Compare(plan,"Removed material without revision");
            plan.WidthM+=1;Compare(plan,"Width changed");plan.DepthM+=.75;Compare(plan,"Depth changed");
            plan.ScreenHeightM=2;Compare(plan,"Screen height changed");plan.Cover=(CampCover)(((int)cover+1)%4);Compare(plan,"Cover changed");
            plan.FloorHeights=new double[9];Compare(plan,"Floor assigned");plan.FloorHeights[0]=.7;Compare(plan,"Same floor array edited");
            plan.FloorHeights=(double[])plan.FloorHeights.Clone();Compare(plan,"Equal floor array replaced");
            plan.Version=1;Compare(plan,"Version changed");plan.Design=CampDesign.RidgeTent;Compare(plan,"Design changed");
            Compare(plan.Copy(),"Independent copied plan");Compare(WorldJson.Read<CampBlueprint>(WorldJson.Write(plan)),"Reloaded plan");
        }
        var stable=CampBlueprints.Create(CampDesign.LeanTo,3,2);
        for(int i=0;i<100;i++){CampBlueprints.Complete(stable);CampBlueprints.Stage(stable);}
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++){CampBlueprints.Complete(stable);CampBlueprints.Stage(stable);}
        long bytes=GC.GetAllocatedBytesForCurrentThread()-start;
        Check(bytes<1024,"Warm completion/stage queries allocate "+bytes+" bytes");
        Parallel.For(0,32,_=>{if(CampBlueprints.Complete(stable)||CampBlueprints.Stage(stable)!=0)throw new Exception("Concurrent stable-plan queries changed");});
        Console.WriteLine("PASS "+checks+" cached construction query mutation/save/ownership controls; warm 20000 queries allocated "+bytes+" bytes");
    }
}

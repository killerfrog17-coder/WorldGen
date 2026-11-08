using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverAlluviumTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(RiverAlluvium.Strength(1,.001,1,0)==0,"Tiny streams do not acquire large sand bars");
        Check(RiverAlluvium.Strength(50,.025,1,0)==0,"Steep channels do not become depositional lowland reaches");
        Check(RiverAlluvium.Strength(50,.001,.8,.1)>RiverAlluvium.Strength(50,.001,.8,.9),"Resistant confined beds reduce alluvial deposition");
        foreach(double depth in new[]{.3,1,3,8})foreach(double strength in new[]{0d,.2,.6,1})
        {
            bool exposed=false;
            for(int i=-100;i<=100;i++)
            {
                double q=i/100d,original=100-depth*(1-q*q);
                double bed=RiverAlluvium.Bed(original,100,depth,Math.Abs(q),q,.9,strength);
                double mirrored=RiverAlluvium.Bed(original,100,depth,Math.Abs(q),-q,-.9,strength);
                Check(double.IsFinite(bed)&&bed==mirrored,"Opposite bends mirror the same finite cross-section");
                Check(bed<=100+Math.Min(1.2,.15+depth*.25),"Deposits remain within the bounded bank height");
                if(strength==0)Check(bed==original,"Zero alluvium preserves the original bed exactly");
                if(q==0)Check(bed==100-depth,"The central continuous water path remains below its datum");
                if(q<0)Check(bed<=original,"The outside bank is deeper, not a deposited sand bar");
                if(q>0&&bed>100)exposed=true;
                if(Math.Abs(q)==1)Check(bed==100,"Bank-edge height remains on the shared datum");
            }
            if(strength>=.6)Check(exposed,"A sufficiently curved sedimentary channel has an exposed inner point bar");
        }
        Check(RiverAlluvium.Deposit(100,120,2,1,1)==0,"High valley walls are not painted as sand banks");
        Check(RiverAlluvium.Deposit(100,100,2,2,1)==0,"Depositional material has finite lateral support");
        const double radius=637100;
        var points=Enumerable.Range(0,8).Select(i=>new V(1,i*80/radius,Math.Sin(i*.9)*45/radius).Unit).ToArray();
        var sides=Enumerable.Range(0,8).Select(i=>V.Cross(points[i],points[Math.Min(7,i+1)]-points[Math.Max(0,i-1)]).Unit).ToArray();
        var sections=Enumerable.Range(0,7).Select(i=>new RiverscapeSpan{A=points[i],B=points[i+1],SideA=sides[i],SideB=sides[i+1],
            LevelA=100-i*.05,LevelB=100-(i+1)*.05,WidthA=12,WidthB=12,Bend=.04,Primitive=i%2==0?RiverFlowPrimitive.Calm:RiverFlowPrimitive.Turbulent}).ToArray();
        var allCalm=(RiverscapeSpan[])sections.Clone();for(int i=0;i<allCalm.Length;i++)allCalm[i].Primitive=RiverFlowPrimitive.Calm;
        RiverAlluvium.PrepareBanks(sections,radius,.8);RiverAlluvium.PrepareBanks(allCalm,radius,.8);
        for(int i=0;i<sections.Length;i++)
        {
            Check(sections[i].AlluviumA==allCalm[i].AlluviumA&&sections[i].AlluviumB==allCalm[i].AlluviumB,"Changing only flow animation category cannot change the riverbed's bank profile");
            if(i>0)Check(sections[i-1].AlluviumB==sections[i].AlluviumA&&sections[i-1].BankBendB==sections[i].BankBendA,"Adjacent sections share the exact same bank strength and bend");
        }
        Check(sections[3].Alluvium>.5,"Calm/turbulent transitions retain nonzero deposition where the physical grade allows it");
        Check(sections[0].AlluviumA==0&&sections[^1].AlluviumB==0,"Bar profiles retire at the actual drainage endpoints");
        sections[3].Primitive=RiverFlowPrimitive.Waterfall;RiverAlluvium.PrepareBanks(sections,radius,.8);
        Check(sections[2].AlluviumB==0&&sections[3].AlluviumA==0&&sections[3].AlluviumB==0&&sections[4].AlluviumA==0,"Neighbouring banks taper continuously to zero at a real waterfall");
        string unconfined=StateHash.Compute(allCalm);RiverAlluvium.PrepareBanks(allCalm,radius,0);
        Check(StateHash.Compute(allCalm)==unconfined,"An ineligible reach retains its existing profiles exactly");
        var ground=new GroundProfile{Sand=.4,Clay=.3,Silt=.3,Gravel=.1,Organic=.12,DepthM=1,Drainage=.5};
        var cover=new GroundCoverProfile{Grasses=new V(.8,0,0)};var mid=new MidCoverProfile{Coverage=.7};var forest=new ForestCoverProfile{Coverage=.6};
        string before=StateHash.Compute(new object[]{ground,cover,mid,forest});
        RiverAlluvium.Ground(0,0,ref ground,ref cover,ref mid,ref forest);
        Check(before==StateHash.Compute(new object[]{ground,cover,mid,forest}),"Ordinary dry ground and vegetation are unchanged");
        RiverAlluvium.Ground(.55,.35,ref ground,ref cover,ref mid,ref forest);
        Check(Math.Abs(ground.Sand+ground.Clay+ground.Silt-1)<1e-12&&ground.Gravel<=1,"Sediment fractions stay normalized");
        Check(ground.Deposit==ParentDeposit.Alluvium&&ground.Organic<.02&&forest.Coverage<.1,"Active bars carry alluvium with sparse mature vegetation");
        Check(RiverAlluvium.RootedAdmission(0,0)==1&&RiverAlluvium.RootedAdmission(.4,.2)==0,"Rooted scatter admits ordinary ground and excludes active bare deposits");
        Console.WriteLine($"PASS {checks} alluvial profile, eligibility, symmetry, bank, sediment and vegetation checks");
    }
    public static void World(string input,string output)
    {
        var world=WorldFile.ReadFile(input);string original=StateHash.Compute(world);
        var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);double physical=model.RadiusM*.1;
        var candidates=model.Hydrology.Channels.Where(c=>c.WidthM>12&&c.WidthM<180&&c.StartM>15&&c.StartM<1800&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)<.008)
            .OrderBy(c=>Rng.Mix(unchecked((uint)c.From^(uint)world.Recipe.Seed))).Take(48);
        var examples=new List<object>();int spansChecked=0,wetCenters=0,bars=0,bestReach=-1;double best=0;V bestSite=default,vegetationSite=default;
        foreach(var channel in candidates)
        {
            var spans=model.Amplification.RiverReach(channel.From);
            foreach(var span in spans.Where(s=>s.Alluvium>.15&&s.Primitive==RiverFlowPrimitive.Calm).OrderByDescending(s=>Math.Abs(s.BankBendA+s.BankBendB)*s.Alluvium).Take(3))
            {
                V center=(span.A+span.B).Unit,side=(span.SideA+span.SideB).Unit;double width=(span.WidthA+span.WidthB)*.5;
                var patch=model.Patch(center,Math.Max(3000,width*50));var middle=patch.Sample(center);spansChecked++;
                if(middle.Underwater)wetCenters++;
                for(int q=-9;q<=9;q++)
                {
                    V at=(center+side*(width*q/10/physical)).Unit;var sample=patch.Sample(at);
                    if(sample.WaterKind!=LocalWaterKind.River||sample.Underwater||sample.ElevationM<=sample.WaterSurfaceM+.025||sample.WaterFootprintM<.1)continue;
                    if(sample.ElevationM>sample.WaterSurfaceM+.1&&(sample.Ground.Organic>.15||sample.Forest.Coverage>.1))
                        throw new Exception("Exposed active point bar retains peat or mature forest");
                    bars++;double score=(sample.ElevationM-sample.WaterSurfaceM)*Math.Sqrt(width);
                    if(vegetationSite.Length<.1)vegetationSite=at;
                    if(examples.Count<16)examples.Add(new{Reach=channel.From,Position=new{at.X,at.Y,at.Z},WidthM=width*2,Height=sample.ElevationM-sample.WaterSurfaceM,Ground=sample.Ground.TextureName,sample.Forest.Coverage});
                    if(score>best){best=score;bestSite=at;bestReach=channel.From;}
                }
            }
        }
        if(spansChecked<10||wetCenters<spansChecked*.9||bars==0)throw new Exception($"Actual alluvial channels incomplete: {spansChecked} spans, {wetCenters} wet centers, {bars} exposed bars");
        var barSample=model.Patch(bestSite,10000).Sample(bestSite);
        Console.WriteLine($"Selected bar on reach {bestReach}: {bestSite.X:R},{bestSite.Y:R},{bestSite.Z:R}");
        var frame=new LocalTerrainFrame(bestSite,model.RadiusM,barSample.ElevationM,.1);int bankEdges=0;
        foreach(var shape in new[]{(size:102.4,segments:64),(size:204.8,segments:32),(size:3276.8,segments:16)})
        {
            var mesh=LocalTerrainMesh.Build(model,frame,-shape.size/2,-shape.size/2,shape.size,shape.segments);
            if(!mesh.WaterPositions.All(float.IsFinite)||mesh.WaterTriangles.Length==0)throw new Exception("Actual point-bar mesh lacks finite water");
            bankEdges+=WaterCouplingTests.ContainedBoundary(mesh,trace:(x,z,y)=>{
                var at=frame.Position(mesh.EastM+x,mesh.NorthM+z);var edge=model.Patch(at,3000).Sample(at);
                Console.WriteLine($"OPEN ALLUVIAL EDGE {x:R},{z:R},{y:R}; actual {at.X:R},{at.Y:R},{at.Z:R}; {edge.WaterKind}/{edge.WaterBodyKey}, physical ground {edge.ElevationM:R}, water {edge.WaterSurfaceM:R}, footprint {edge.WaterFootprintM:R}, wet {edge.Underwater}");
            });
        }
        var scatter=new LocalScatter(model);var scatterPatch=model.Patch(vegetationSite,20000);int plants=0;
        foreach(bool detail in new[]{false,true})foreach(var plant in scatter.Build(scatter.Address(vegetationSite,detail)).Instances)
        {
            if(plant.Kind is ScatterKind.Stone or ScatterKind.Boulder or ScatterKind.Outcrop or ScatterKind.Branch)continue;
            var s=scatterPatch.Sample(plant.Position);plants++;
            if(RiverAlluvium.RootedAdmission(s.RiverSand,s.RiverGravel)<=0)throw new Exception("Rooted scatter appears on an active bare bar");
        }
        if(plants==0)throw new Exception("Vegetation test needs a nonempty planted river neighbourhood");
        if(StateHash.Compute(world)!=original)throw new Exception("Local deposits changed canonical world/history inputs");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"survey.json"),JsonSerializer.Serialize(new{spansChecked,wetCenters,bars,bankEdges,plants,examples},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"sites.json"),WorldJson.Write(new VegetationBenchmarkSiteSet{HistoryHash=world.Prehistory.Hash,Sites=new[]{new VegetationBenchmarkSite{Name="alluvial-bar",Position=bestSite,Biome="TemperateForest"}}}));
        Console.WriteLine($"PASS actual {spansChecked} alluvial spans, {wetCenters} wet centers, {bars} exposed inner-bar samples; canonical world unchanged.");
    }
}

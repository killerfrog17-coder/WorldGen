using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckLandforms(string output,string sourceHash)
    {
        Application.logMessageReceived+=(message,trace,kind)=>
        {if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        AudioListener.volume=0;play.Settings.HorizontalScale=.1;
        bool earth=world.Recipe.TerrainSource=="Earth";var sites=new List<Tuple<string,V>>();
        if(earth)
        {
            sites.Add(Tuple.Create("himalaya",TerrainMapViewport.FromDegrees(27.98,86.92)));
            sites.Add(Tuple.Create("tibetan-plateau",TerrainMapViewport.FromDegrees(32,88)));
            sites.Add(Tuple.Create("andes",TerrainMapViewport.FromDegrees(-32.65,-70.01)));
            sites.Add(Tuple.Create("appalachians",TerrainMapViewport.FromDegrees(38.5,-79.4)));
            sites.Add(Tuple.Create("texas-hills",TerrainMapViewport.FromDegrees(30.1,-98.4)));
            sites.Add(Tuple.Create("coast",TerrainMapViewport.FromDegrees(38.07,-122.96)));
        }
        else
        {
            foreach(var kind in new[]{FeatureKind.CollisionBelt,FeatureKind.Cordillera,FeatureKind.AncientOrogen})
            {
                var feature=world.Features.Where(f=>f.Kind==kind&&f.Cells.Length>0).OrderByDescending(f=>f.AmplitudeM).FirstOrDefault();
                if(feature!=null){int cell=feature.Cells.OrderByDescending(i=>world.ElevationM[i]).First();sites.Add(Tuple.Create(kind.ToString(),world.Surface.Cells[cell].Position));}
            }
            int flat=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>50&&world.ElevationM[i]<1200).OrderBy(i=>world.Ecology.Habitat[i].ReliefM).First();
            sites.Add(Tuple.Create("plain",world.Surface.Cells[flat].Position));
        }
        selectedCell=0;ExploreTerrain(sites[0].Item2);double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.Ready)throw new Exception("Landform sampler did not load.");
        var explorer=terrainExplorer;var terrain=localTerrainTask.Result;
        if(Environment.GetCommandLineArgs().Contains("--worldgen-hyper-review"))
        {yield return LocalTerrainExplorer.CheckUploadBuffers(output);yield return CheckHyperAmplification(output,sourceHash,explorer,terrain);yield break;}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-incision-timing-only"))
        {
            File.WriteAllLines(Path.Combine(output,"preparation.txt"),new[]{"Native scaffold "+terrain.Scaffold.PreparationMilliseconds.ToString("F0")+"ms; river conditioning "+terrain.Hydrology.ConditioningMilliseconds.ToString("F0")+"ms","PASS source unchanged "+StateHash.Compute(world)});
            var timingProbes=new List<string>{"originX,originY,originZ,eastM,northM,elevationM"};timingProbes.AddRange(explorer.Probes());File.WriteAllLines(Path.Combine(output,"probes.csv"),timingProbes);
            if(StateHash.Compute(world)!=sourceHash)throw new Exception("River preparation changed the source world.");
            explorer.Close();Application.Quit(0);yield break;
        }
        if(Environment.GetCommandLineArgs().Contains("--worldgen-loading-check"))
        {
            var loading=new List<string>();explorer.TestView(2,20,8);
            // No all-horizon wait: measure the actual full-detail neighbourhood,
            // then cross multiple fine-tile boundaries in the running player.
            foreach(double move in new[]{0d,180d,180d,180d})
            {
                if(move>0){explorer.TestAdvanceEast(move);explorer.TestView(2,20,8);}
                double started=Time.realtimeSinceStartupAsDouble;
                yield return null;deadline=started+30;
                while(!explorer.NearReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                if(!explorer.NearReady||explorer.Error!="")throw new Exception("Near terrain loading failed: "+explorer.Error);
                loading.Add("Full-detail near ring after "+move+"m: "+(Time.realtimeSinceStartupAsDouble-started).ToString("F3")+"s; "+explorer.Diagnostics);
            }
            if(StateHash.Compute(world)!=sourceHash)throw new Exception("Loading changed the source world.");
            loading.Add("PASS unchanged source "+sourceHash);File.WriteAllLines(Path.Combine(output,"loading.txt"),loading);
            explorer.Close();Application.Quit(0);yield break;
        }
        var river=terrain.Hydrology.Channels.Where(c=>c.WidthM>40&&c.WidthM<300&&c.StartM>30&&c.StartM<1500).OrderByDescending(c=>c.LengthM).First();
        sites.Add(Tuple.Create("river",river.Position(.5)));
        var lake=terrain.Hydrology.Shores.Where(f=>f.Kind==ShoreKind.Lake&&f.ScaleM*.1>500&&f.ScaleM*.1<10000).OrderByDescending(f=>f.AreaM2).FirstOrDefault();
        if(lake!=null)sites.Add(Tuple.Create("lake",lake.Boundary(0,terrain.RadiusM)));
        bool quickShape=Environment.GetCommandLineArgs().Contains("--worldgen-shape-review");
        if(quickShape)sites.RemoveAll(s=>s.Item1!="himalaya"&&s.Item1!="andes"&&s.Item1!="river");
        bool incisionReview=Environment.GetCommandLineArgs().Contains("--worldgen-incision-review");
        if(incisionReview)
        {
            var worst=terrain.Hydrology.Channels.Where(c=>c.WidthM>5&&c.WidthM<300&&c.Level(.5)>100&&c.StartM<5000)
                .OrderByDescending(c=>c.MaximumLoweringM).First();
            sites.Clear();sites.Add(Tuple.Create("lowered-mountain-river",worst.Position(.5)));sites.Add(Tuple.Create("river",river.Position(.5)));quickShape=true;
        }
        if(Environment.GetCommandLineArgs().Contains("--worldgen-water-review"))sites.RemoveAll(s=>s.Item1!="river"&&s.Item1!="lake");
        var report=new List<string>();var probes=new List<string>{"originX,originY,originZ,eastM,northM,elevationM"};
        if(terrain.Scaffold!=null)report.Add("Geographic scaffold: "+terrain.Scaffold.Positions.Length+" controls; "+terrain.Scaffold.PreparationMilliseconds.ToString("F0")+"ms preparation; "+(terrain.Scaffold.RetainedBytes/1048576.0).ToString("F1")+"MiB retained arrays");
        report.Add("River conditioning: "+terrain.Hydrology.ConditioningMilliseconds.ToString("F0")+"ms; largest old-profile lowering "+terrain.Hydrology.Channels.Max(c=>c.MaximumLoweringM).ToString("F1")+"m");
        foreach(var site in sites)
        {
            explorer.Teleport(site.Item2);explorer.TestView(quickShape?(site.Item1=="river"?12:600):80,site.Item1=="coast"?270:20,incisionReview&&site.Item1!="river"?45:quickShape?20:8);
            if(site.Item1=="coast")explorer.TestNearbyShore();
            PlayCommands.Execute(play,0,"/weather clear",weather:explorer.WeatherControl);
            TerrainMapViewport.Degrees(explorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            double reviewStarted=Time.realtimeSinceStartupAsDouble;
            deadline=reviewStarted+(quickShape?12:180);
            while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if((quickShape?!explorer.NearReady:!explorer.AllReady)||explorer.Error!="")throw new Exception("Landform tiles failed: "+site.Item1+" "+explorer.Error);
            yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,site.Item1+".png"));
            report.Add(site.Item1+" "+lat.ToString("F6")+","+lon.ToString("F6")+": "+explorer.Diagnostics);probes.AddRange(explorer.Probes());
            if(quickShape){File.WriteAllLines(Path.Combine(output,"landforms.txt"),report);continue;}
            explorer.TestView(site.Item1=="lake"?1500:700,20,65);
            deadline=Time.realtimeSinceStartupAsDouble+90;while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,site.Item1+"-above.png"));
            if(site.Item1=="river")
            {
                var sample=terrain.Patch(site.Item2).Sample(0,0);if(!sample.Underwater)throw new Exception("Native river has no water over its bed.");
                explorer.TestView(6,20,4);deadline=Time.realtimeSinceStartupAsDouble+90;while(!explorer.AllReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,"river-near.png"));
                report.Add("River width "+river.WidthM.ToString("F2")+"m; level "+sample.WaterSurfaceM.ToString("F3")+"m; bed "+sample.ElevationM.ToString("F3")+"m");
            }
            File.WriteAllLines(Path.Combine(output,"landforms.txt"),report);
        }
        if(StateHash.Compute(world)!=sourceHash)throw new Exception("Local landforms changed the source world.");
        report.Add("PASS source unchanged "+sourceHash);File.WriteAllLines(Path.Combine(output,"landforms.txt"),report);
        File.WriteAllLines(Path.Combine(output,"probes.csv"),probes);explorer.Close();Application.Quit(0);
    }

    IEnumerator CheckHyperAmplification(string output,string sourceHash,LocalTerrainExplorer explorer,LocalTerrain terrain)
    {
        var sites=new List<Tuple<string,V,double,float,float>>();
        if(world.Recipe.TerrainSource=="Earth")
        {
            sites.Add(Tuple.Create("himalayas",TerrainMapViewport.FromDegrees(27.98,86.92),600d,20f,18f));
            sites.Add(Tuple.Create("appalachians",TerrainMapViewport.FromDegrees(35.6,-83.5),100d,20f,12f));
            sites.Add(Tuple.Create("hill-country",TerrainMapViewport.FromDegrees(30.2,-99),50d,20f,8f));
            sites.Add(Tuple.Create("plateau",TerrainMapViewport.FromDegrees(32,88),150d,20f,12f));
        }
        else
        {
            int high=Enumerable.Range(0,world.ElevationM.Length).OrderByDescending(i=>world.ElevationM[i]).First();
            int flat=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>80&&world.ElevationM[i]<800).OrderBy(i=>world.Ecology.Habitat[i].ReliefM).First();
            sites.Add(Tuple.Create("generated-mountains",world.Surface.Cells[high].Position,500d,20f,18f));
            sites.Add(Tuple.Create("generated-lowlands",world.Surface.Cells[flat].Position,100d,20f,12f));
        }
        var target=world.Recipe.TerrainSource=="Earth"?TerrainMapViewport.FromDegrees(30.5,31.2):sites[sites.Count-1].Item2;
        var river=terrain.Hydrology.Channels.Where(c=>c.WidthM>10&&c.StartM>10&&c.StartM<1500).OrderBy(c=>(c.A-target).Length).First();
        V riverAt=terrain.Amplification.RiverPoint(river.From,.5);
        sites.Insert(0,Tuple.Create("lowland-river",riverAt,350d,20f,50f));
        sites.Insert(1,Tuple.Create("river-ground",riverAt,6d,20f,8f));
        int lake=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.Hydrology.LakeCoverageFraction[i]>.7&&world.ElevationM[i]>0&&world.Hydrology.WaterBody[i]>=0)
            .Where(i=>world.Hydrology.Waters[world.Hydrology.WaterBody[i]].AreaM2>1e8&&world.Hydrology.Waters[world.Hydrology.WaterBody[i]].AreaM2<5e10)
            .OrderBy(i=>Math.Abs(Math.Log(world.Hydrology.Waters[world.Hydrology.WaterBody[i]].AreaM2/3e9))).DefaultIfEmpty(-1).First();
        if(lake>=0)sites.Insert(2,Tuple.Create("lake",world.Surface.Cells[lake].Position,350d,20f,55f));
        if(Environment.GetCommandLineArgs().Contains("--worldgen-ocean-review"))
        {
            int coast=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>20&&!world.Hydrology.OceanConnected[i]
                &&world.Surface.Cells[i].Neighbors.Any(j=>world.Hydrology.OceanConnected[j])).OrderBy(i=>world.ElevationM[i]).First();
            int sea=world.Surface.Cells[coast].Neighbors.Where(i=>world.Hydrology.OceanConnected[i]).OrderBy(i=>world.ElevationM[i]).First();
            V landAt=world.Surface.Cells[coast].Position,seaAt=world.Surface.Cells[sea].Position;
            V wet=seaAt,dry=landAt;
            for(int j=0;j<24;j++)
            {
                V mid=(wet+dry).Unit;
                if(terrain.Patch(mid,2000).Sample(mid).ElevationM<0)wet=mid;else dry=mid;
            }
            var shoreFrame=new LocalTerrainFrame(dry,terrain.RadiusM,0,.1);
            TerrainHandoff.Coordinates(shoreFrame,seaAt,out double dx,out double dz);
            float towardSea=(float)(Math.Atan2(dx,dz)*180/Math.PI);
            sites.Clear();sites.Add(Tuple.Create("ocean-shore",dry,6d,towardSea,5f));
            sites.Add(Tuple.Create("ocean-shelf",seaAt,250d,towardSea+180,12f));
        }
        if(Environment.GetCommandLineArgs().Contains("--worldgen-hyper-loading-review"))
            sites=sites.Where(s=>s.Item1=="river-ground"||s.Item1=="generated-mountains"||s.Item1=="generated-lowlands"||s.Item1=="himalayas"||s.Item1=="hill-country"||s.Item1=="lake").ToList();
        string chosenSites=Argument("--worldgen-hyper-sites","");
        if(chosenSites!="")sites=sites.Where(s=>chosenSites.Split(',').Contains(s.Item1)).ToList();
        var report=new List<string>{"Surface "+LocalTerrain.Version+" / "+HyperAmplification.Version,
            "Core hierarchical preparation "+terrain.Amplification.PreparationMilliseconds.ToString("F1")+"ms"};
        var probes=new List<string>{"originX,originY,originZ,eastM,northM,elevationM"};
        foreach(var site in sites)
        {
            Debug.Log("HYPER REVIEW starting "+site.Item1+" at "+Time.realtimeSinceStartupAsDouble.ToString("F2")+"s");
            var surface=terrain.Patch(site.Item2).Sample(0,0);
            double aboveGround=site.Item3+(surface.Underwater?Math.Max(0,surface.WaterSurfaceM-surface.ElevationM):0);
            if((explorer.Position-site.Item2).Length*terrain.RadiusM>.1)explorer.Teleport(site.Item2);
            explorer.TestView(aboveGround,site.Item4,site.Item5);
            PlayCommands.Execute(play,0,"/weather clear",weather:explorer.WeatherControl);
            TerrainMapViewport.Degrees(site.Item2,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            double started=Time.realtimeSinceStartupAsDouble,deadline=started+30;
            explorer.BeginLoadingProfile();
            yield return null;
            while(!explorer.NearReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!explorer.NearReady||explorer.Error!="")throw new Exception("Hyper terrain near coverage: "+site.Item1+" "+explorer.Error);
            report.Add(site.Item1+": near "+(Time.realtimeSinceStartupAsDouble-started).ToString("F3")+"s; "+explorer.Diagnostics);
            // Near-ready proves input readiness, not that an elevated review
            // camera has any distant land in view. Capture only real coverage.
            deadline=Time.realtimeSinceStartupAsDouble+180;double nextProgress=Time.realtimeSinceStartupAsDouble+10;
            while(!explorer.AllReady&&explorer.Error==""&&Time.realtimeSinceStartupAsDouble<deadline)
            {if(Time.realtimeSinceStartupAsDouble>nextProgress){nextProgress+=10;Debug.Log("HYPER streaming "+site.Item1+": "+explorer.Diagnostics);}yield return null;}
            if(!explorer.AllReady||explorer.Error!="")throw new Exception("Hyper terrain distant coverage: "+site.Item1+" "+explorer.Error);
            report.Add(site.Item1+": full coverage "+(Time.realtimeSinceStartupAsDouble-started).ToString("F3")+"s; "+explorer.Diagnostics);
            report.Add(site.Item1+": "+explorer.EndLoadingProfile());
            double hold=Time.realtimeSinceStartupAsDouble+3;while(Time.realtimeSinceStartupAsDouble<hold)yield return null;
            yield return new WaitForEndOfFrame();explorer.Capture(Path.Combine(output,site.Item1+".png"));
            probes.AddRange(explorer.Probes());
            if(site.Item1=="river-ground"&&!terrain.Patch(site.Item2).Sample(0,0).Underwater)throw new Exception("Actual amplified river point has no water.");
            if(site.Item1=="river-ground")
            {var sample=terrain.Patch(site.Item2).Sample(0,0);report.Add("River "+river.From+": level "+sample.WaterSurfaceM.ToString("F3")+"m, bed "+sample.ElevationM.ToString("F3")+"m");}
            File.WriteAllLines(Path.Combine(output,"hyper-review.txt"),report);File.WriteAllLines(Path.Combine(output,"probes.csv"),probes);
        }
        if(StateHash.Compute(world)!=sourceHash)throw new Exception("Amplification changed source geography/history.");
        report.Add("PASS unchanged source "+sourceHash);File.WriteAllLines(Path.Combine(output,"hyper-review.txt"),report);
        explorer.Close();Application.Quit(0);
    }
}

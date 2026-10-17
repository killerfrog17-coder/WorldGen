using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Controlled handler fixture on accepted actual geometry. This is NOT a new
// historical replay or a save upgrade. Every mutation is on a detached case.
public static class HistorySparseRegionalExchangeAudit
{
    public static void Run(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new sparse fixture output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var original=world.Prehistory;
        string before=original.ComputeHash();var engine=new Prehistory(world);
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,values);
        var baseProbe=original.Copy();Call("PrepareAggregateEnvironment",baseProbe);Call("PrepareAggregateRelations",baseProbe);
        int cell=176034,region=engine.CellPatch[cell];var checks=new List<object>();var rows=new List<object>();
        void Check(bool pass,string message){checks.Add(new{Pass=pass,Message=message});if(!pass)throw new Exception(message);}
        long Residents(PrehistoryState s,int r)=>(long)Call("ProvincialContactResidents",s,r,false);
        Check(original.History.Aggregates.Empires.Connections.Realms.Version==17,"Source remains actual Q17; no upgrade is performed.");
        Check(baseProbe.History.Aggregates.Regions[region].Culture<0&&baseProbe.History.Aggregates.TownAt[cell]<0,"Observed sparse French recipient remains anonymous and has no town.");
        var eligible=new List<(int Route,int Other,int Donor,int From,int To)>();
        foreach(int routeId in baseProbe.History.Aggregates.Regions[region].Routes)
        {
            var route=engine.Routes[routeId];if(route.Sea)continue;int other=route.A==region?route.B:route.A;
            int donor=(int)Call("DevelopmentRegionalTeacher",baseProbe,other);
            if(donor<0||Residents(baseProbe,other)<100)continue;
            int from=(int)Call("RegionalContactCell",baseProbe,route,other),to=(int)Call("RegionalContactCell",baseProbe,route,region);
            if(from<0||to<0||!(bool)Call("InstitutionalRouteAllowed",baseProbe,from,to,donor))continue;
            long package=(long)Call("ProvincialRegionalKnowledge",baseProbe,other,donor);if(package==0)continue;
            eligible.Add((routeId,other,donor,from,to));
        }
        int positiveActualRoutes=0,denseExactRoutes=0,zeroRecipientRoutes=0,zeroSourceRoutes=0;
        foreach(var edge in eligible)
        {
            var routeRows=new List<object>();bool actualPositive=false,denseExact=false,zeroRecipient=false,zeroSource=false;
            string Execute(int version,long? recipient,int trial,bool emptySource,out bool delivered,out long actualSource,out long actualRecipient)
            {
                var s=original.Copy();var a=s.History.Aggregates;var local=a.Living.Regions[region];
                a.Empires.Connections.Realms.Version=version;
                // Explicit fixture control: choose ONE real original boundary,
                // not a fabricated pass, teleport or altered crop/terrain fit.
                a.Regions[region].Routes=new[]{edge.Route};
                if(recipient.HasValue){s.Population[region]=recipient.Value;local.SettledPopulation=0;}
                if(emptySource){s.Population[edge.Other]=0;a.Living.Regions[edge.Other].SettledPopulation=0;}
                Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
                actualSource=Residents(s,edge.Other);actualRecipient=Residents(s,region);
                long reads=a.CellReads;int initialReceipt=local.LastExchangeYear,initialTeacher=local.Teacher,initialExposure=local.Exposure;
                long initialBits=a.Provinces.RegionalKnowledge[region];int cultures=s.Society.Cultures.Length,towns=s.Society.Settlements.Length,states=s.History.Polities.Length;
                Call("TryLivingRegionalExchange",s,region,100,trial);
                delivered=local.LastExchangeYear!=initialReceipt||local.Teacher!=initialTeacher||local.Exposure!=initialExposure||a.Provinces.RegionalKnowledge[region]!=initialBits;
                Check(s.Society.Cultures.Length==cultures&&s.Society.Settlements.Length==towns&&s.History.Polities.Length==states,"Regional lesson never creates a free culture, town or state.");
                routeRows.Add(new{Version=version,RecipientControl=recipient,Trial=trial,Elapsed=100,EmptySourceControl=emptySource,
                    ActualSourceResidents=actualSource,ActualRecipientResidents=actualRecipient,Delivered=delivered,
                    TeacherBefore=initialTeacher,TeacherAfter=local.Teacher,ExposureBefore=initialExposure,ExposureAfter=local.Exposure,
                    ReceiptBefore=initialReceipt,ReceiptAfter=local.LastExchangeYear,KnowledgeBefore=initialBits,KnowledgeAfter=a.Provinces.RegionalKnowledge[region]});
                // Dense-control equivalence excludes ONLY the explicit test
                // marker and instrumented reads; all physical mutations remain.
                a.Empires.Connections.Realms.Version=17;a.CellReads=reads;
                return s.ComputeHash();
            }
            for(int trial=0;trial<12&&!actualPositive;trial++)
            {
                Execute(17,null,trial,false,out bool old,out _,out long recipients);
                Execute(18,null,trial,false,out bool current,out _,out _);
                Check(recipients<100&&!old,"Literal17 cannot deliver to the actual below100 recipient.");
                actualPositive=current;
            }
            string dense17=Execute(17,100,0,false,out _,out _,out long denseResidents);
            string dense18=Execute(18,100,0,false,out _,out _,out _);
            Check(denseResidents==100,"Dense fixture changes only actual recipient to100, not a remote phantom population.");
            denseExact=dense17==dense18;Check(denseExact,"At least100 source and recipient retain exact preceding handler result after marker/read-counter normalization.");
            Execute(18,0,0,false,out bool emptyTarget,out _,out long targetZero);
            zeroRecipient=targetZero==0&&!emptyTarget;Check(zeroRecipient,"An actual zero-recipient endpoint cannot receive a lesson.");
            long sourceResidents=Residents(baseProbe,edge.Other),sourceRaw=baseProbe.Population[edge.Other]+baseProbe.History.Aggregates.Living.Regions[edge.Other].SettledPopulation;
            if(sourceResidents==sourceRaw)
            {
                Execute(18,75,0,true,out bool emptySender,out long sourceZero,out _);
                zeroSource=sourceZero==0&&!emptySender;Check(zeroSource,"A retained teacher ID cannot deliver from a controlled zero-person source.");
            }
            if(actualPositive)positiveActualRoutes++;if(denseExact)denseExactRoutes++;if(zeroRecipient)zeroRecipientRoutes++;if(zeroSource)zeroSourceRoutes++;
            rows.Add(new{edge.Route,SourceRegion=edge.Other,edge.Donor,Departure=edge.From,Arrival=edge.To,ActualSourceResidents=sourceResidents,
                ActualRecipientResidents=Residents(baseProbe,region),ActualPositive=actualPositive,DenseExact=denseExact,ZeroRecipientPass=zeroRecipient,
                ZeroSourcePass=zeroSource,ZeroSourceControlSkippedDueToAdditionalRealCohort=sourceResidents!=sourceRaw,Cases=routeRows});
        }
        Check(original.ComputeHash()==before,"Original published source hash is unchanged by every detached fixture case.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=source,Year=original.Years,Hash=before,Core=typeof(Prehistory).Assembly.Location,
            Cell=cell,Region=region,ActualRecipients=Residents(baseProbe,region),EligibleActualRoutes=eligible.Count,PositiveActualRoutes=positiveActualRoutes,
            DenseExactRoutes=denseExactRoutes,ZeroRecipientRoutes=zeroRecipientRoutes,ZeroSourceRoutes=zeroSourceRoutes,Checks=checks,Routes=rows,
            Notes=new[]{"Only detached case rules marker, chosen real route and explicitly listed population controls differ. Original world is never saved or upgraded.",
                "A lesson is not a crop delivery, genotype inventory, adoption, settlement, state or Europe-realism acceptance.",
                "Actual below100 case retains original population and all physical/crop/source gates; only route selection is controlled. It uses100 elapsed years, not arbitrary millennia to force success.",
                "No eligible actual route or no stochastic success is reported as negative evidence, never replaced by a convenient invented donor."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Sparse actual routes "+eligible.Count+", positives "+positiveActualRoutes+", checks "+checks.Count+", source hash preserved.");
    }
}

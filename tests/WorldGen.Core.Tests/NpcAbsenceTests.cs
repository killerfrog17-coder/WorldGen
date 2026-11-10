using System;
using System.IO;
using System.Runtime.Serialization;
using WorldGen.Core;

static class NpcAbsenceTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
        Check(HumanDailyLife.LocalDay(.99,8.76)==HumanDailyLife.LocalDay(1.01,9.24),"Daily work assignments do not reroll during the morning when the global solar counter crosses an integer");
        Check(HumanDailyLife.LocalDay(.624,23.976)+1==HumanDailyLife.LocalDay(.626,.024),"Daily assignment seeds advance at local midnight");
        var known=new NpcKnownPerson{Person=7,LastSeenSolarDay=10};
        var trip=new HumanActivitySample{DepartureSolarDay=10,ExpectedReturnSolarDay=10.4,OutingPurpose="foraging with a party"};
        NpcAbsence.Expect(known,10,8,12,30,true,trip);
        Check(known.ExpectedReturnSolarDay==10.4&&NpcAbsence.OverdueHours(known,10.3,30)==0,"A normal planned party outing is not missing despite seven hours without a sighting");
        Check(NpcAbsence.OverdueHours(known,10.43,30)==0&&NpcAbsence.OverdueHours(known,10.5,30)>0,"Expected return includes tolerance before genuine overdue concern");
        Check(NpcAbsence.OverdueHours(known,10.5,30,8)<NpcAbsence.OverdueHours(known,10.5,30),"Bad weather allows a longer reasonable delay");
        Check(NpcAbsence.ExpectedBack(10,22,12,30,true)>10.8,"Adults last seen in the evening are not required to check in every few hours overnight");
        Check(NpcAbsence.ExpectedBack(10,22,12,5,true)>10.3&&NpcAbsence.ExpectedBack(10,22,12,5,true)<10.5,"Young children sleeping at camp are expected around the following morning, not during the night");
        Check(NpcAbsence.ExpectedBack(10,10,12,5,false)<10.05,"A small unsupervised child outside camp has a much earlier check-in expectation");
        var young=new NpcKnownPerson{Person=8,LastSeenSolarDay=10};NpcAbsence.Expect(young,10,8,12,10,false,trip);
        Check(NpcAbsence.OverdueHours(young,10.3,10)==0,"A child on a known accompanied outing can stay away for the planned trip");
        Check(NpcAbsence.Response(.5,30,.9,.7,.8,.5,.2,true,false,false,.2)==NpcAbsenceResponse.Worried,"A modest adult delay causes concern without automatically launching a search");
        Check(NpcAbsence.Response(4,30,.9,.7,.8,.5,.2,false,false,false,.2)==NpcAbsenceResponse.WaitForDaylight,"An overdue adult does not automatically send everyone into the dark");
        Check(NpcAbsence.Response(4,30,.9,.8,.8,.3,.1,true,false,false,.1)==NpcAbsenceResponse.Search,"A willing adult can choose to search after a meaningful delay in daylight");
        Check(NpcAbsence.Response(4,30,.9,.1,.1,.3,.9,true,false,false,.99)==NpcAbsenceResponse.AskAround,"Caution and personal variation can favor asking rather than searching");
        Check(NpcAbsence.Response(4,30,.9,.9,.9,.3,.1,true,true,false,.1)==NpcAbsenceResponse.AskAround,"An essential duty is not abandoned just because someone is overdue");
        Check(NpcAbsence.Response(1,5,.9,.7,.8,.3,.2,false,false,false,.1)==NpcAbsenceResponse.Search,"A genuinely overdue small child can justify urgent action at night");
        known.LastSeenSolarDay=10.499;Check(NpcAbsence.OverdueHours(known,10.5,30)==0,"Personally seeing an alive late returner prevents an immediate missing-person alarm");
        var social=new NpcSocialState();social.KnownPeople.Add(known);known.SearchUntilSolarDay=11;known.ConcernSinceSolarDay=10.5;known.ReportedPresence=true;social.See(7,new V(1,0,0),10.6);
        Check(known.SearchUntilSolarDay==0&&known.ConcernSinceSolarDay==0&&!known.ReportedPresence,"An actual sighting clears the accepted search and marks knowledge as personal");
        known.SearchUntilSolarDay=10.8;known.ConcernNextSolarDay=10.7;var copy=social.Copy();copy.KnownPeople[0].ExpectedReason="changed";
        Check(known.ExpectedReason!="changed"&&copy.KnownPeople[0].SearchUntilSolarDay==10.8,"Absence expectations and accepted searches copy independently");
        var serializer=new DataContractSerializer(typeof(NpcSocialState));using(var stream=new MemoryStream())
        {serializer.WriteObject(stream,social);stream.Position=0;var saved=(NpcSocialState)serializer.ReadObject(stream);saved.Validate();Check(saved.KnownPeople[0].ExpectedReturnSolarDay==10.4&&saved.KnownPeople[0].SearchUntilSolarDay==10.8,"Expected returns and accepted searches survive save/reload");}
        Console.WriteLine("NPC absence checks PASS: "+checks);
    }
}

using WorldGen.Core;
using WorldGen.Serialization;

static class BleedingTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){n++;if(!ok)throw new Exception(text);}
        var blunt=new BleedingState();BleedingRules.Wound(blunt,.3,0,1,.2,true);Check(blunt.Rate==0,"A blunt impact is not automatically an open bleeding wound");
        var minor=new BleedingState();BleedingRules.Wound(minor,.2,.8,.9,.04,false);BleedingRules.Advance(minor,1800);
        Check(minor.Remaining>.85&&BleedingRules.HealthLimit(minor,100)==100,"A shallow wound can clot without becoming a guaranteed death timer");
        var deep=new BleedingState();BleedingRules.Wound(deep,.3,.9,.9,.22,true);var copy=deep.Copy();
        BleedingRules.Advance(deep,1);Check(deep.Remaining>.98&&BleedingRules.HealthLimit(deep,100)>99,"A deep puncture does not kill instantly through the bleeding path");
        BleedingRules.Advance(deep,299);Check(BleedingRules.HealthLimit(deep,100)==0,"A sustained severe wound can eventually cause fatal blood loss");
        var grazing=new BleedingState();BleedingRules.Wound(grazing,.3,.9,.12,.22,true);BleedingRules.Advance(grazing,300);
        Check(grazing.Remaining>.9,"A glancing impact is not treated like a square deep torso puncture");
        var one=copy.Copy();var split=copy.Copy();BleedingRules.Advance(one,90,.8);for(int i=0;i<900;i++)BleedingRules.Advance(split,.1,.8);
        Check(Math.Abs(one.Remaining-split.Remaining)<1e-10&&Math.Abs(one.Rate-split.Rate)<1e-10,"Loaded frames and offscreen catch-up integrate the same wound course");
        var resting=copy.Copy();var running=copy.Copy();BleedingRules.Advance(resting,50,0);BleedingRules.Advance(running,50,1);
        Check(running.Remaining<resting.Remaining,"Exertion can increase blood loss");
        Check(BleedingRules.Mobility(new BleedingState{Remaining=.45})<BleedingRules.Mobility(new BleedingState{Remaining=.9}),"Blood loss progressively weakens movement rather than preserving full flight speed");
        var player=new PlayerSurvival();player.Restore();player.Bleeding=copy.Copy();var saved=WorldJson.Read<PlayerSurvival>(WorldJson.Write(player));saved.Validate();
        Check(saved.Bleeding.Rate==player.Bleeding.Rate&&saved.Copy().Bleeding!=saved.Bleeding,"Player wounds save and copy independently");
        player.Restore();Check(player.Bleeding==null,"Respawning/restoring the player clears the previous body's wound state");
        var state=new WildlifeState();state.BloodSigns.Add(new BloodSign{Position=new V(1,0,0),SizeM=.1,CreatedDays=1});var loaded=WorldJson.Read<WildlifeState>(WorldJson.Write(state));loaded.Validate();
        Check(loaded.BloodSigns.Count==1&&loaded.Copy().BloodSigns[0]!=loaded.BloodSigns[0],"Logical ground blood signs survive saves without sharing mutable records");
        var record=new WildlifeIndividual{Id=12,Species="deer",Position=new V(1,0,0),MaximumHealth=100,Health=80,Bleeding=copy.Copy()};record.Validate();
        var restored=WorldJson.Read<WildlifeIndividual>(WorldJson.Write(record));restored.Validate();Check(restored.Bleeding.Rate==copy.Rate,"An injured animal retains bleeding across streaming/save cycles");
        Console.WriteLine("PASS "+n+" bleeding, clotting, persistence and catch-up checks");
    }
}

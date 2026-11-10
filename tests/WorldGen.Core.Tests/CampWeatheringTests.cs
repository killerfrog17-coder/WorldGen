using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;

static class CampWeatheringTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);n++;}
        CampObject Hut(CampCover cover)=>new CampObject{Id="weather-hut",Kind=CampKind.Blueprint,Position=new V(1,0,0),NpcGroup=7,NpcSite=9,NpcHousehold=11,WeatheredThroughCalendarDay=0,Blueprint=CampBlueprints.Create(CampDesign.LeanTo,2,2,cover)};
        var fresh=Hut(CampCover.Brush);HumanCampLayout.FitShelter(fresh,new double[9]);var wet=fresh.Copy();var dry=fresh.Copy();var cold=fresh.Copy();
        CampWeathering.Advance(wet,30,2000,25,.5);CampWeathering.Advance(dry,30,100,25,.5);CampWeathering.Advance(cold,30,2000,-10,.5);
        double Wear(CampObject c)=>c.Blueprint.Parts.Sum(p=>p.Weathering);
        Check(Wear(wet)>Wear(dry)&&Wear(wet)>Wear(cold),"Warm damp conditions age a shelter faster than dry or frozen conditions");
        Check(wet.Blueprint.Parts.Count==fresh.Blueprint.Parts.Count,"A month of ordinary weather does not instantly erase the frame");
        Check(CampBlueprints.CoverFraction(wet.Blueprint)<CampBlueprints.CoverFraction(fresh.Blueprint),"An ageing covering loses weather protection gradually before falling apart");
        var step=fresh.Copy();for(int day=1;day<=90;day++)CampWeathering.Advance(step,day,1200,15,.5);var jump=fresh.Copy();CampWeathering.Advance(jump,90,1200,15,.5);
        Check(Math.Abs(Wear(step)-Wear(jump))<1e-9,"Daily observation and a single unobserved interval accumulate the same weather dose");
        var original=fresh.OwnedMaterials().Select(p=>p.Id).ToHashSet();double originalKg=fresh.MassKg;
        var remains=CampWeathering.Advance(fresh,3000,2400,25,.2);
        Check(remains.Length>0&&fresh.Blueprint.Parts.Count<wet.Blueprint.Parts.Count,"Long abandonment produces missing components and fallen remains");
        Check(remains.All(p=>original.Contains(p.Id)&&p.Place==PossessionPlace.Ground&&p.UseDamage>=.8),"Fallen remains preserve actual identities while becoming worn ground items");
        Check(fresh.MassKg+remains.Sum(p=>p.MassKg)<originalKg,"Rot/decomposition loses mass rather than creating replacement supplies");
        Check(!fresh.OwnedMaterials().Any(p=>remains.Any(r=>r.Id==p.Id)),"A collapsed material is not retained inside the structure too");
        Check(CampWeathering.Advance(fresh,3000,2400,25,.2).Length==0,"The same elapsed interval cannot repeatedly produce debris");
        var saved=WorldJson.Read<CampObject>(WorldJson.Write(wet));new CampState{Objects={saved}}.Validate();
        Check(saved.WeatheredThroughCalendarDay==30&&Math.Abs(Wear(saved)-Wear(wet))<1e-9,"Weather exposure and the applied calendar checkpoint survive save/load");
        var copy=wet.Copy();copy.Blueprint.Parts[0].Weathering=0;Check(Wear(copy)<Wear(wet),"Weathered component state copies independently");
        Console.WriteLine("NPC camp weather checks PASS: "+n);
    }
}

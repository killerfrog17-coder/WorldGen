using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckWaterVesselsAtSource(string output,List<string> report,WaterQuality source)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("VESSEL CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
            Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Actual game content is rendered: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
        }
        var right=Inventory.AtHand(PossessionPlace.RightHand);var left=Inventory.AtHand(PossessionPlace.LeftHand);
        var target=AimedWater(float.PositiveInfinity);Check(target!=null,"Current native water aim is available for the vessel checks");var targetPoint=target.Point;
        var vessel=PortableTechnology.Create(PossessionKind.Waterskin,Position,"deer",1);Inventory.Items.Add(vessel);double dry=vessel.MassKg;
        Check(MovePossession(vessel.Id,PossessionPlace.Shoulder),"Actual new waterskin can be worn independently of both occupied hands");
        Check(FillWaterVessel(vessel)&&vessel.WaterLitres==2,"Live inventory fill uses the reachable rendered sea source");
        Check(Math.Abs(vessel.WaterSalinityPpt-source.SalinityPpt)<1e-8&&vessel.WaterSalinityPpt>30,"A waterskin preserves actual seawater salinity");
        Check(Math.Abs(vessel.MassKg-dry-2)<1e-8&&Inventory.AtHand(PossessionPlace.RightHand)==right&&Inventory.AtHand(PossessionPlace.LeftHand)==left,"Filling adds real water mass without dropping or deleting either held tool");
        double thirst=Vitals.WaterDeficitLitres;
        Check(DrinkWaterVessel(vessel)&&vessel.WaterLitres==1.75&&Vitals.WaterDeficitLitres>thirst,"Live stored-water drink consumes a mouthful and salt water still worsens hydration");
        paused=false;SetInventoryOpen(true);inventoryContext=new NearbyPossession{Item=vessel};inventoryMenuAt=new Vector2(Screen.width*.66f,Screen.height*.15f);yield return Shot("water-vessel-context");inventoryContext=null;SetInventoryOpen(false);paused=true;
        EmptyWaterVessel(vessel);Check(vessel.WaterLitres==0&&Math.Abs(vessel.MassKg-dry)<1e-8,"Live pour action restores the empty vessel mass");
        Check(MovePossession(left.Id,PossessionPlace.Ground)&&MovePossession(vessel.Id,PossessionPlace.LeftHand),"The off hand can hold the vessel while a spear remains in the right");
        view.transform.LookAt(targetPoint);waterSipAt=waterAimAt=0;
        Check(WaterPrompt(float.PositiveInfinity).StartsWith("Fill ")&&TryWaterInteract(float.PositiveInfinity)&&vessel.WaterLitres==2,"Actual E prompt and interaction fill a left-hand vessel instead of drinking");
        Check(Inventory.Find(left.Id)==left&&Inventory.AtHand(PossessionPlace.RightHand)==right,"Off-hand fill retains every original item");
        var save=new PlaySession();StorePlayer(save);var restored=WorldJson.Read<PlaySession>(WorldJson.Write(save));restored.Validate();
        var saved=restored.Inventory.Find(vessel.Id);Check(saved.WaterLitres==2&&saved.WaterSalinityPpt==vessel.WaterSalinityPpt&&saved.Place==PossessionPlace.LeftHand,"Full player capture/reload retains finite water and its equipment location");
        UpdateInventoryVisuals();paused=false;SetInventoryOpen(true);inventorySelected=vessel.Id;yield return Shot("filled-waterskin");SetInventoryOpen(false);paused=true;
        Inventory.Validate();
    }
}

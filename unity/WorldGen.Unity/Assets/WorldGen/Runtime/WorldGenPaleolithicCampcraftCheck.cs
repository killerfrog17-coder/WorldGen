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
    IEnumerator CheckPaleolithicCampcraftFlow(string output,List<string> report)
    {
        Application.targetFrameRate=60;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("CAMPCRAFT CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered native game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        Possession Add(PossessionKind kind,string species="")
        {var p=PortableTechnology.Create(kind,Position,species);Inventory.Items.Add(p);SetCraftWorkArea(p);return p;}
        IEnumerator Finish(string id,Possession p)
        {RefreshInventoryNearby(true);Check(StartPortableCraft(id,p.Id),"Actual contextual action starts: "+id);double limit=Time.unscaledTimeAsDouble+30;while(Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;Check(!Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id),"Timed native work completes: "+id);Inventory.Validate();}
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
        var spear=Add(PossessionKind.StoneTippedSpear);Check(MovePossession(spear.Id,PossessionPlace.RightHand),"Start with a spear in hand");
        var mat=Add(PossessionKind.WeavingStrips);for(int i=0;i<7;i++)Add(PossessionKind.WeavingStrips);Add(PossessionKind.Cordage);
        SetInventoryOpen(true);yield return Finish("weave-mat",mat);Check(mat.Kind==PossessionKind.WovenMat&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Actual strip bundles become one rolled mat and restore the spear hand");
        var bone=Add(PossessionKind.Bone,"deer");Add(PossessionKind.Thread);Add(PossessionKind.StoneBlade);Add(PossessionKind.StoneBorer);yield return Finish("bone-pendant",bone);
        Check(bone.Kind==PossessionKind.BonePendant&&MovePossession(bone.Id,PossessionPlace.Neck),"Actual source-bone pendant equips on its independent neck attachment");
        RenderInventoryPreview();Check(inventoryPreviewGear.Any(g=>g.name==bone.Name),"Actual character portrait renders this worn jewellery");inventoryContext=new NearbyPossession{Item=bone};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("bone-pendant-and-reed-mat");inventoryContext=null;SetInventoryOpen(false);
        // Use actual placement validation instead of assuming one fixed point
        // near a terrain fixture is flat enough for a bed.
        BeginCampPlacement(mat,CampKind.Bedding);bool placed=false;CampObject bed=null;
        for(int i=0;i<12;i++)
        {
            double a=i*Math.PI/6,x=motion.X+Math.Sin(a)*1.6,z=motion.Z+Math.Cos(a)*1.6;
            UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ))));campGroundAt=0;UpdateCampPlacement();
            if(!campPlaceValid)continue;ConfirmCampPlacement();bed=Camp.Objects.Last();placed=true;break;
        }
        Check(placed,"A real woven mat places as bedding through existing placement controls");Check(!Inventory.Items.Contains(mat)&&bed.Materials.Any(p=>p.Id==mat.Id),"Placement transfers the same mat to the actual bed");
        campUpdateAt=0;UpdateCamp();Check(campVisuals[bed.Id].Root.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh!=null&&f.sharedMesh.vertexCount>100),"Bedding renders the actual spread woven surface");var bedTarget=campVisuals[bed.Id].Root.transform.localPosition+Vector3.up*.025f;
        landscapeReviewCamera=view.transform.localPosition;landscapeReviewRotation=Quaternion.LookRotation(bedTarget-landscapeReviewCamera.Value,Vector3.up);UpdateCamera();
        yield return Shot("spread-woven-bedding");landscapeReviewCamera=null;UpdateCamera();
        Check(CampRules.BedComfort(bed)>.38&&CampRules.BedInsulation(bed)>0,"Woven bedding contributes actual comfort and insulation");
        var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full native capture/reload accepts jewellery, woven material and its placed bed");
    }
}

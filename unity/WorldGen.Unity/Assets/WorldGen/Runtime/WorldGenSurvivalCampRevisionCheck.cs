using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckSurvivalCampRevision(string output,List<string> report)
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("CAMP REVISION "+why);report.Add("PASS "+why);Debug.Log("CAMP REVISION PASS "+why);}
        void Aim(double x,double z,double height=0)
        {heading=Mathf.Atan2((float)(x-motion.X),(float)(z-motion.Z))*Mathf.Rad2Deg;UpdateHumans();UpdateCamera();var q=new Vector3((float)(x-originX),(float)CampFloor(x,z)+(float)height,(float)(z-originZ));pitch=-Mathf.Asin((q-view.transform.localPosition).normalized.y)*Mathf.Rad2Deg;UpdateCamera();campGroundAt=0;}
        Color32[] Capture(string name)
        {
            var old=view.targetTexture;var previous=RenderTexture.active;var rt=new RenderTexture(1280,800,24);rt.Create();view.targetTexture=rt;view.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();var pixels=texture.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);view.targetTexture=old;RenderTexture.active=previous;rt.Release();Destroy(rt);return pixels;
        }
        int Changed(Color32[] a,Color32[] b)=>Enumerable.Range(0,a.Length).Count(i=>Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>12);
        double until=Time.unscaledTimeAsDouble+20;while(bodyAudioLoading!=null&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(bodyClips.Count==8&&bodyClips.Values.All(v=>v.Count==3&&v.All(c=>c.channels==1)),"All eight body-foley categories load three real mono variants");
        paused=false;if(bodyVoice!=null)bodyVoice.Stop();
        PlayBodyCue("eat");Check(bodyVoice!=null&&bodyVoice.isPlaying&&bodyVoice.clip.name.StartsWith("eat_"),"Eating foley plays through an actual native AudioSource");
        PlayBodyCue("pain",true);Check(bodyVoice.clip.name.StartsWith("pain_"),"Urgent pain foley interrupts a lower-priority body cue");
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();Vitals.Restore();humanCamera.Restore(0,false);paused=true;
        double fx=motion.X+.9,fz=motion.Z+.7;var fire=new CampObject{Kind=CampKind.Fire,Position=frame.Position(fx,fz),LastSimulatedSeconds=Camp.SimulatedSeconds};Camp.Objects.Add(fire);
        Possession Supply(PossessionKind kind){var p=PortableTechnology.Create(kind,Position);p.Place=PossessionPlace.Ground;Inventory.Items.Add(p);inventoryNearbyAt=0;return p;}
        void Choose(Func<CampChoice,bool> test){RefreshCampChoices(fire,true);int index=campChoices.FindIndex(c=>test(c));Check(index>=0,"A contextual fire choice is available");campChoiceIndex=index;RememberCampChoice();}
        var tinder=Supply(PossessionKind.TinderBundle);var kindling=Supply(PossessionKind.KindlingBundle);
        var wood=WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,Position=Position,WidthM=1.8,HeightM=.045,Variant=0});wood.Id="revision-fuel-"+Guid.NewGuid().ToString("N");wood.NaturalId=0;Inventory.Items.Add(wood);
        double woodMass=wood.MassKg;Aim(fx,fz,.15);
        foreach(var p in new[]{tinder,kindling,wood}){Choose(c=>c.Item==p);QuickFireAction(fire);}
        Check(Inventory.Find(wood.Id)==null&&fire.Materials.Where(WoodCrafting.Wood).Count()>1&&Math.Abs(fire.Materials.Where(WoodCrafting.Wood).Sum(p=>p.MassKg)-woodMass)<1e-8,"Quick feeding consumes the actual branch into mass-conserving short pieces");
        Choose(c=>c.Action==2);QuickFireAction(fire);Check(fire.Burning&&!CampUiOpen,"Lighting from the quick selector opens no campfire menu");
        var food=Supply(PossessionKind.RawMeat);
        int selected=campChoiceIndex;double zoom=humanCamera.Distance;paused=false;CycleCampChoice(120);paused=true;Check(campChoiceIndex!=selected&&humanCamera.Distance==zoom,"Cycling nearby fire choices preserves camera zoom");
        Choose(c=>c.Item==food);
        var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();var background=InputSystem.settings.backgroundBehavior;TestKeyboard(keyboard);keyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        IEnumerator PressE(){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        try
        {
            paused=false;yield return PressE();paused=true;
            Check(fire.CookingItems.Contains(food)&&Inventory.Find(food.Id)==null&&!CampUiOpen,"Actual E input places selected food over the fire without opening a menu");
            CampRules.Advance(fire,1200,0,.3,0,0);Check(food.Cooked&&!food.Burnt,"Food cooks from the fire's accumulated heat exposure");
            Choose(c=>c.Action==1&&c.Item==food);paused=false;yield return PressE();paused=true;
            Check(!fire.CookingItems.Contains(food)&&Inventory.Find(food.Id)==food&&food.Cooked,"Actual E input retrieves the same cooked food into player ownership");
        }
        finally{InputSystem.RemoveDevice(keyboard);TestKeyboard(null);if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=background;}
        campUpdateAt=0;UpdateCamp();Check(campVisuals[fire.Id].FlameRenderers.Length>=3,"The fueled fire has native flame and ember renderers");
        humanCamera.Restore(3,false);heading=210;pitch=23;UpdateHumans();UpdateCamera();Capture("04-short-firewood-cooking");humanCamera.Restore(0,false);
        var preview=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=1,HeightM=.03});preview.Id="revision-preview";preview.NaturalId=0;Inventory.Items.Add(preview);inventoryNearbyAt=0;
        bool available=false;for(int i=0;i<16&&!available;i++){double angle=i*Math.PI/8;Aim(motion.X+Math.Sin(angle)*2,motion.Z+Math.Cos(angle)*2);BeginCampPlacement(preview,CampKind.Stake);available=campPlaceValid;if(!available)CancelCampPlacement();}
        Check(available&&campGhost!=null&&campGhost.activeSelf&&campGhostMaterial.GetColor("_BaseColor").a<.3f,"Placement uses a translucent real object ghost at a valid aimed ground point");Capture("05-placement-ghost");CancelCampPlacement();
        var post=Camp.Objects.First(c=>c.Kind==CampKind.Stake);TerrainHandoff.Coordinates(frame,post.Position,out double px,out double pz);motion.Place(px+.8,pz,CampFloor(px+.8,pz));post.SoilDepthM=Math.Max(.4,post.SoilDepthM);post.EmbedM=.035;InvalidateCampVisual(post);campUpdateAt=0;UpdateCamp();
        Aim(px,pz,(post.Materials[0].HeightM-post.EmbedM)*.75);var rock=new Possession{Id="revision-heavy-hammer",Kind=PossessionKind.LargeRock,MassKg=15,WidthM=.24,HeightM=.18,DepthM=.2};
        foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)){p.Place=PossessionPlace.Ground;p.Position=Position;}
        rock.Place=PossessionPlace.RightHand;Inventory.Items.Add(rock);double embedded=post.EmbedM;combatGlobalReady=campHammerReady=0;combatRightReady=Time.unscaledTimeAsDouble+5;
        PrimaryItemAction(rock,false,0);Check(post.EmbedM>embedded&&combatRightReady-Time.unscaledTimeAsDouble<=.61,"A 15 kg rock drives the aimed post with a short tool cooldown, even after an old heavy-weapon recovery");
        embedded=post.EmbedM;PrimaryItemAction(rock,false,0);Check(post.EmbedM==embedded,"Same-frame hammer spam is rejected");
        yield return new WaitForSecondsRealtime(.7f);Aim(px,pz,(post.Materials[0].HeightM-post.EmbedM)*.75);PrimaryItemAction(rock,false,0);Check(post.EmbedM>embedded,"Hammering is available again within well under a second");
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();motion.Place(motion.X,motion.Z,CampFloor(motion.X,motion.Z));Vitals.Tiredness=80;paused=false;BeginCampSleep(0);Check(campSleeping,"Natural-sleep disturbance fixture lies down on actual dry ground");
        ReportSleepNoise(transform.TransformPoint(PlayerFeet())+Vector3.up*.2f,2);Check(!campSleeping,"A loud close transient wakes a sleeping player");
        BeginCampSleep(0);ReportSleepNoise(transform.TransformPoint(PlayerFeet())+Vector3.right*40,.1f);Check(campSleeping,"Quiet distant sounds do not constantly interrupt sleep");WakeCamp("Check complete.");paused=true;
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();heading=210;pitch=10;UpdateHumans();UpdateCamera();
        if(bodyScreen==null)bodyScreen=view.gameObject.AddComponent<LocalSurvivalScreen>();
        bodyScreen.Set(Vector4.zero,true);var normal=Capture("06-perception-normal");int draws=bodyScreen.DrawCount;
        bodyScreen.Set(new Vector4(1,0,0,0),true);var cold=Capture("07-perception-frost");
        bodyScreen.Set(new Vector4(0,1,0,0),true);var heat=Capture("08-perception-heat");
        bodyScreen.Set(new Vector4(0,0,1,.5f),true);var weak=Capture("09-perception-faintness");bodyScreen.Set(Vector4.zero,true);
        Check(bodyScreen.DrawCount>=draws+3&&Changed(normal,cold)>1000&&Changed(normal,heat)>1000&&Changed(normal,weak)>1000,"Actual native render-graph passes visibly draw frost, heat and faintness effects");
        paused=false;
    }
}

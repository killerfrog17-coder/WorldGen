using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    bool survivalShowStatus=true;Rect survivalWindowRect;long survivalDragged;Vector2 survivalDragOffset;
    bool DrawSurvival()
    {
        if(survival==null||paused||mapOpen||ecologyOpen)return false;
        if(survivalOpen||survival.Dead)
        {
            float width=Math.Min(Screen.width-40,1050),height=Math.Min(Screen.height-40,730);
            survivalWindowRect=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
            Color panelColor=GUI.color;GUI.color=new Color(.045f,.055f,.06f,.97f);GUI.DrawTexture(survivalWindowRect,Texture2D.whiteTexture);GUI.color=panelColor;
            GUI.Box(survivalWindowRect,GUIContent.none);GUILayout.BeginArea(new Rect(survivalWindowRect.x+20,survivalWindowRect.y+16,width-40,height-32));
            GUILayout.BeginHorizontal();GUILayout.Label(survival.Dead?"YOU HAVE DIED":"CAMP & EQUIPMENT",GUILayout.Height(30));GUILayout.FlexibleSpace();
            if(!survival.Dead&&GUILayout.Button("Close",GUILayout.Width(90))){survivalOpen=false;CaptureMouse();}GUILayout.EndHorizontal();
            if(survival.Dead)DrawSurvivalDeath();
            else
            {
                survivalTab=GUILayout.Toolbar(survivalTab,new[]{"Inventory","Craft","Shelters","Nearby","Character & help"});GUILayout.Space(12);
                survivalScroll=GUILayout.BeginScrollView(survivalScroll);
                switch(survivalTab){case 0:DrawSurvivalInventory();break;case 1:DrawSurvivalCraft();break;case 2:DrawSurvivalBuild();break;case 3:DrawSurvivalNearby();break;default:DrawSurvivalHelp();break;}
                GUILayout.EndScrollView();
            }
            if(!string.IsNullOrEmpty(survivalMessage))GUILayout.Label(survivalMessage);
            GUILayout.EndArea();return true;
        }
        if(survivalShowStatus&&survival.Mode==SurvivalMode.Survival)
        {
            float x=24,y=Screen.height-52,w=130;SurvivalBar(new Rect(x,y,w,7),(float)(survival.Health/100),new Color(.66f,.22f,.18f));
            SurvivalBar(new Rect(x+w+12,y,w,7),(float)(survival.Stamina/100),new Color(.65f,.62f,.32f));
            if(survival.WaterDeficitL>2||survival.CoreTemperatureC<35||survival.CoreTemperatureC>39||survival.EnergyKcal<800)
            {string warning=survival.WaterDeficitL>2?"Dehydrated":survival.CoreTemperatureC<35?"Hypothermia":survival.CoreTemperatureC>39?"Overheating":"Starving";GUI.Label(new Rect(x,y-28,280,24),warning);}
        }
        if(survival.Inventory.Held?.Damage>0&&!chatOpen){Color c=GUI.color;GUI.color=new Color(1,1,1,.55f);GUI.DrawTexture(new Rect(Screen.width/2f-1,Screen.height/2f-1,2,2),Texture2D.whiteTexture);GUI.color=c;}
        if(survivalCompletion!=null)
        {
            var area=new Rect(Screen.width/2f-150,Screen.height*.72f,300,26);GUI.Label(area,survivalAction);
            SurvivalBar(new Rect(area.x,area.y+27,area.width,5),(float)(1-survivalActionRemaining/Math.Max(1,survivalActionTotal)),new Color(.75f,.68f,.45f));
        }
        if(survivalSleeping)GUI.Label(new Rect(Screen.width/2f-100,Screen.height*.65f,200,30),"Resting");
        if(Time.realtimeSinceStartupAsDouble<survivalMessageUntil&&!chatOpen)GUI.Label(new Rect(Screen.width/2f-260,Screen.height*.8f,520,28),survivalMessage);
        return false;
    }
    static void SurvivalBar(Rect rect,float value,Color color)
    {Color old=GUI.color;GUI.color=new Color(.08f,.08f,.07f,.8f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=color;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(value),rect.height),Texture2D.whiteTexture);GUI.color=old;}
    void DrawSurvivalInventory()
    {
        var bag=survival.Inventory;var capacity=bag.Capacity;
        GUILayout.BeginHorizontal();GUILayout.Label(bag.Mass.ToString("F1")+" kg · "+bag.Bulk.ToString("F1")+" L · storage "+capacity.kg.ToString("F0")+" kg / "+capacity.litres.ToString("F0")+" L, plus hands");
        GUILayout.FlexibleSpace();if(GUILayout.Button(bag.View==InventoryView.List?"Use grid":"Use list",GUILayout.Width(100)))bag.View=bag.View==InventoryView.List?InventoryView.Grid:InventoryView.List;GUILayout.EndHorizontal();
        if(bag.View==InventoryView.Grid)DrawSurvivalGrid();
        else
        {
            GUILayout.Space(8);
            var rowStyle=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,fixedHeight=28,padding=new RectOffset(10,10,4,4)};
            foreach(var stack in bag.Items.ToArray())
            {
                var item=SurvivalItems.Get(stack.Item);GUILayout.BeginHorizontal();
                bool selected=stack.Id==survivalSelected;string wear=stack.Container==-2?" [worn]":stack.Id==bag.Equipped?" [held]":"";
                if(GUILayout.Toggle(selected,item.Name+" ×"+stack.Count+wear,rowStyle,GUILayout.MinWidth(290))&&!selected)survivalSelected=stack.Id;
                GUILayout.Label((stack.Count*item.Kg*stack.Fraction).ToString("F2")+" kg",GUILayout.Width(85));GUILayout.Label((stack.Count*item.Litres*stack.Fraction).ToString("F1")+" L",GUILayout.Width(75));
                GUILayout.Label(item.ShelfDays>0?(stack.AgeDays>item.ShelfDays?"Spoiled":"Freshness "+Math.Max(0,100*(1-stack.AgeDays/item.ShelfDays)).ToString("F0")+"%"):"",GUILayout.Width(130));GUILayout.EndHorizontal();
            }
        }
        var chosen=bag.Items.FirstOrDefault(i=>i.Id==survivalSelected);if(chosen==null)return;var d=SurvivalItems.Get(chosen.Item);
        GUILayout.Space(16);GUILayout.Label(d.Name);GUILayout.Label(d.Description??"");
        if(d.Kcal>0||d.WaterMl>0)GUILayout.Label((d.Kcal*chosen.Fraction).ToString("F0")+" kcal · "+(d.WaterMl*chosen.Fraction).ToString("F0")+" ml water per portion");
        GUILayout.BeginHorizontal();
        if(d.Kind==SurvivalItemKind.Clothing&&GUILayout.Button(chosen.Worn?"Take off":"Wear")){bag.Wear(chosen.Id,out var why);if(why!="")SurvivalNotice(why);}
        if(d.StorageWidth==0&&d.Kind!=SurvivalItemKind.Clothing&&GUILayout.Button(chosen.Id==bag.Equipped?"Put away":"Hold")){bag.Equip(chosen.Id,out var why);if(why!="")SurvivalNotice(why);}
        if((d.Kcal>0||d.WaterMl>0)&&GUILayout.Button(d.WaterMl>500?"Drink":"Eat")){SurvivalPhysiology.Eat(survival,chosen.Id,out var why);SurvivalNotice(why);TriggerHumanSurvivalAction("Idle");}
        if(d.Kind==SurvivalItemKind.Medicine&&GUILayout.Button("Dress wound")){if(bag.Remove(d.Id,1)){survival.Bleeding=Math.Max(0,survival.Bleeding-.7);SurvivalNotice("Wound dressed.");}}
        if(GUILayout.Button("Drop one"))DropSurvivalItem(chosen);
        GUILayout.EndHorizontal();
    }
    void DrawSurvivalGrid()
    {
        var bag=survival.Inventory;var capacity=bag.Capacity;GUILayout.Label("Hands / worn attachments");GUILayout.BeginHorizontal();
        foreach(var s in bag.Items.Where(i=>i.Container<0))if(GUILayout.Button(SurvivalItems.Get(s.Item).Name+" ×"+s.Count))survivalSelected=s.Id;
        GUILayout.EndHorizontal();
        if(capacity.width==0){GUILayout.Label("No bag or pouch is worn. Only items held in your hands can be carried.");return;}
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Pack bag",GUILayout.Width(100)))bag.Compact(out _);
        var selected=bag.Items.FirstOrDefault(i=>i.Id==survivalSelected);
        if(selected?.Container==0&&GUILayout.Button("Rotate selected",GUILayout.Width(140))){bag.Move(selected.Id,selected.X,selected.Y,!selected.Rotated,out var why);if(why!="")SurvivalNotice(why);}
        GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
        const float cell=48;Rect rect=GUILayoutUtility.GetRect(capacity.width*cell,capacity.height*cell,GUILayout.ExpandWidth(false));
        var e=Event.current;
        for(int y=0;y<capacity.height;y++)for(int x=0;x<capacity.width;x++)GUI.Box(new Rect(rect.x+x*cell,rect.y+y*cell,cell-2,cell-2),GUIContent.none);
        foreach(var s in bag.Items.Where(i=>i.Container==0))
        {
            var d=SurvivalItems.Get(s.Item);int w=s.Rotated?d.Height:d.Width,h=s.Rotated?d.Width:d.Height;
            var area=new Rect(rect.x+s.X*cell+2,rect.y+s.Y*cell+2,w*cell-6,h*cell-6);
            string label=d.Id=="stone-spear"||d.Id=="wood-spear"?"Spear":d.Id=="cooked-meat"?"Roast":d.Id=="raw-meat"?"Meat":d.Id=="waterskin"?"Water":d.Id=="hammer"?"Hammer":d.Id=="fire-drill"?"Drill":d.Id=="knife"?"Knife":d.Id=="dressing"?"Cloth":d.Id=="tinder"?"Tinder":d.Id=="cord"?"Cord":d.Id=="nuts"?"Nuts":d.Name.Split(' ')[0];
            GUI.Box(area,new GUIContent(label+"\nx"+s.Count,d.Name));
            if(e.type==EventType.MouseDown&&e.button==0&&area.Contains(e.mousePosition))
            {survivalSelected=s.Id;survivalDragged=s.Id;survivalDragOffset=e.mousePosition-area.position;e.Use();}
        }
        if(survivalDragged!=0&&e.type==EventType.MouseUp&&e.button==0)
        {
            if(rect.Contains(e.mousePosition))
            {
                int x=(int)Math.Floor((e.mousePosition.x-survivalDragOffset.x-rect.x)/cell+.1),y=(int)Math.Floor((e.mousePosition.y-survivalDragOffset.y-rect.y)/cell+.1);
                var moving=bag.Items.FirstOrDefault(i=>i.Id==survivalDragged);
                if(moving!=null&&!bag.Move(moving.Id,x,y,moving.Rotated,out var why))SurvivalNotice(why);
            }
            survivalDragged=0;e.Use();
        }

    }
    void DropSurvivalItem(SurvivalStack stack)
    {
        if(SurvivalStorage.Drop(survival,stack.Id,1,Position,out _,out var why))RefreshSurvivalScene();else SurvivalNotice(why);
    }
    void DrawSurvivalCraft()
    {
        bool fire=HasSurvivalFire;
        foreach(var recipe in SurvivalCrafting.Recipes)
        {
            SurvivalCrafting.CanCraft(survival,recipe,fire,out var why);GUILayout.BeginVertical(GUI.skin.box);GUILayout.BeginHorizontal();
            GUILayout.Label(recipe.Name,GUILayout.MinWidth(250));GUILayout.FlexibleSpace();GUI.enabled=why=="";
            if(GUILayout.Button("Make",GUILayout.Width(100)))
                BeginSurvivalAction(recipe.Name,recipe.Seconds,()=>{SurvivalCrafting.Craft(survival,recipe,HasSurvivalFire,out var result);SurvivalNotice(result);});
            GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.Label(SurvivalCrafting.MaterialsText(recipe.Materials)+(recipe.Fire?" · burning hearth":""));if(why!="")GUILayout.Label(why);GUILayout.EndVertical();
        }
    }
    void DrawSurvivalBuild()
    {
        foreach(var b in SurvivalCrafting.Shelters)
        {
            GUILayout.BeginVertical(GUI.skin.box);GUILayout.BeginHorizontal();GUILayout.Label(b.Name,GUILayout.MinWidth(260));GUILayout.FlexibleSpace();
            if(GUILayout.Button("Build ahead",GUILayout.Width(120)))
            {
                double x=motion.X+Math.Sin(heading*Math.PI/180)*3.5,z=motion.Z+Math.Cos(heading*Math.PI/180)*3.5;var point=frame.Position(x,z);var sample=walkingPatch.Sample(point);double g=Ground(x,z);
                if(sample.Underwater||Math.Abs(Ground(x+1,z)-g)>.45||Math.Abs(Ground(x,z+1)-g)>.45)SurvivalNotice("Find dry, reasonably level ground.");
                else if(b.Id=="snow-shelter"&&CurrentWeather.TemperatureC>0)SurvivalNotice("A snow shelter requires persistent freezing conditions.");
                else BeginSurvivalAction("Laying out "+b.Name.ToLowerInvariant(),20,()=>{SurvivalCrafting.PlaceBlueprint(survival,b,point,heading);SurvivalNotice("Blueprint placed. Bring materials in several trips and supply it from Nearby.");});
            }
            GUILayout.EndHorizontal();GUILayout.Label(SurvivalCrafting.MaterialsText(b.Materials));GUILayout.Label(b.Evidence);GUILayout.EndVertical();
        }
    }
    void DrawSurvivalNearby()
    {
        FindSurvivalTarget();
        if(survivalCorpseTarget!=null)
        {
            var corpse=survivalCorpseTarget;GUILayout.Label(corpse.Slug+" carcass · "+corpse.MassKg.ToString("F1")+" kg live mass");
            GUILayout.Label("Remaining meat "+corpse.MeatKg.ToString("F1")+" kg · fat "+corpse.FatKg.ToString("F1")+" kg · organs "+corpse.OrganKg.ToString("F1")+" kg · bone "+corpse.BoneKg.ToString("F1")+" kg");
            foreach(var part in new[]{"meat","fat","organs","hide","bone","sinew"})if(GUILayout.Button("Harvest "+part))BeginSurvivalAction("Butchering "+part,part=="hide"?100:35,()=>{SurvivalPhysiology.Harvest(survival,corpse,part,out var why);SurvivalNotice(why);});
            if(corpse.AgeDays>2)GUILayout.Label("The carcass is deteriorating. Fresh meat may no longer be safe.");
        }
        else if(survivalSiteTarget!=null)
        {
            var site=survivalSiteTarget;GUILayout.Label(SurvivalCrafting.Shelters.FirstOrDefault(b=>b.Id==site.Kind)?.Name??"Dropped equipment");
            if(site.Progress<1)
            {
                var blueprint=SurvivalCrafting.Shelters.First(b=>b.Id==site.Kind);GUILayout.Label((site.Progress*100).ToString("F0")+"% complete");
                foreach(var material in blueprint.Materials)GUILayout.Label(SurvivalItems.Get(material.item).Name+": "+site.Contents.Where(i=>i.Item==material.item).Sum(i=>i.Count)+" / "+material.count);
                if(GUILayout.Button("Supply carried materials"))BeginSurvivalAction("Building "+blueprint.Name.ToLowerInvariant(),Math.Max(15,blueprint.Seconds*.15),()=>{SurvivalCrafting.SupplyBlueprint(survival,site,out var why);SurvivalNotice(why);});
                return;
            }
            if(site.Kind=="hearth")
            {
                GUILayout.Label((site.Lit&&site.FuelHours>0?"Burning":"Unlit")+" · "+site.FuelHours.ToString("F1")+" hours of fuel");
                if(GUILayout.Button("Add branch (+30 minutes)")){if(survival.Inventory.Remove("stick",1)){site.FuelHours=Math.Min(10000,site.FuelHours+.5);SurvivalNotice("Added firewood.");}else SurvivalNotice("A branch is needed.");}
                if(!site.Lit&&GUILayout.Button("Light fire"))BeginSurvivalAction("Making fire",75,()=>{if(site.FuelHours>0&&(survival.Inventory.Has("fire-drill")||survival.Inventory.Has("fire-striker"))&&survival.Inventory.Remove("tinder",1)){site.Lit=true;SurvivalNotice("The tinder catches.");}else SurvivalNotice("Fuel, a firemaking kit and dry tinder are needed.");});
            }
            if(site.Kind=="bed"||site.Kind=="hide-bed"||SurvivalCrafting.Shelters.Any(b=>b.Id==site.Kind&&b.Shelter>.5))
                if(GUILayout.Button("Rest / sleep")){survivalSleeping=true;survivalSleepRemaining=8*3600;survivalOpen=false;CaptureMouse();TriggerHumanSurvivalAction("Sit");}
            foreach(var item in site.Contents.ToArray())if(GUILayout.Button("Take "+SurvivalItems.Get(item.Item).Name+" ×"+item.Count))
            {if(SurvivalStorage.Take(survival,site,item,item.Count,out var why))RefreshSurvivalScene();else SurvivalNotice(why);}
            if(site.Kind=="cache")foreach(var item in survival.Inventory.Items.Where(i=>SurvivalItems.Get(i.Item).StorageWidth==0).ToArray())if(GUILayout.Button("Store one "+SurvivalItems.Get(item.Item).Name))
            {if(!SurvivalStorage.Store(survival,site,item.Id,1,out var why))SurvivalNotice(why);}
        }
        else if(survivalNodeTarget!=null){var node=survivalNodeTarget;GUILayout.Label(SurvivalItems.Get(node.Item).Name);if(GUILayout.Button("Gather"))GatherSurvivalNode(node);}
        else GUILayout.Label("No camp, carcass or gatherable object within reach.");
    }
    void DrawSurvivalHelp()
    {
        GUILayout.Label("Health "+survival.Health.ToString("F0")+" / 100 · stamina "+survival.Stamina.ToString("F0")+" / 100");
        GUILayout.Label("Energy reserve "+survival.EnergyKcal.ToString("F0")+" kcal · water deficit "+survival.WaterDeficitL.ToString("F1")+" L · fatigue "+(survival.Fatigue*100).ToString("F0")+"%");
        GUILayout.Label("Core temperature "+survival.CoreTemperatureC.ToString("F1")+" °C · wetness "+(survival.Wetness*100).ToString("F0")+"%");
        survivalShowStatus=GUILayout.Toggle(survivalShowStatus,"Show compact health and stamina bars");
        if(GUILayout.Button("Rest on the ground")){survivalSleeping=true;survivalSleepRemaining=8*3600;survivalOpen=false;CaptureMouse();TriggerHumanSurvivalAction("Sit");}
        GUILayout.Space(12);GUILayout.Label("Mode");int mode=GUILayout.Toolbar((int)survival.Mode,new[]{"Survival","Creative","Spectator"});
        if(mode!=(int)survival.Mode){survival.Mode=(SurvivalMode)mode;motion.Flying=survival.Mode==SurvivalMode.Spectator;}
        GUILayout.Space(16);GUILayout.Label("I / Tab: inventory. B: shelters. E: gather, inspect camp/carcass, or drink nearby fresh water.");
        GUILayout.Label("Left click: strike with held item. Right click: throw a spear/throwing stick or launch a sling stone.");
        GUILayout.Label("WASD: move. C: walk. Shift: sprint. Ctrl: crouch. Space: jump. Mouse wheel: camera distance. Middle click: lock camera distance.");
        GUILayout.Label("Esc: close inventory / cancel action / stop resting, then pause. F: flight in Creative or Spectator. /help: commands.");
        GUILayout.Space(12);GUILayout.Label("Twenty-four playable days advance one seasonal year by default. Food, water, fuel and wounds follow playable time.");
        GUILayout.Label("Gathering and carcasses are finite. Both inventory views use the same carrying rules. Wear clothing to gain insulation; carrying it in a bag does not keep you warm. Visible clothing fitting is still a placeholder.");
    }
    void DrawSurvivalDeath()
    {
        GUILayout.Label("Your equipment will remain where you fell. Camps and the world persist.");
        if(GUILayout.Button("Respawn at nearest bedding / starting point"))
        {
            var old=survival.Inventory;survival.Sites=survival.Sites.Concat(new[]{new SurvivalSite{Id=survival.NextSite++,Kind="drop",Position=Position,Contents=old.Items.Select(i=>i.Copy()).ToArray()}}).ToArray();
            var bed=survival.Sites.Where(s=>s.Kind=="bed"||s.Kind=="hide-bed").OrderBy(s=>(s.Position-Position).Length).FirstOrDefault();
            survival.Inventory=new SurvivalInventory();survival.Health=100;survival.Stamina=100;survival.SprintExhausted=false;survival.EnergyKcal=4000;survival.WaterDeficitL=0;survival.Fatigue=.1;survival.CoreTemperatureC=37;survival.Wetness=survival.Bleeding=survival.Illness=0;survival.Dead=false;
            survivalOpen=false;motion.Flying=false;Teleport(bed?.Position??startingPoint);CaptureMouse();
        }
    }
}

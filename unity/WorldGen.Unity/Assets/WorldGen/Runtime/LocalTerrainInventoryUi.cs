using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class InventoryZone {public Rect Rect,Clip;public PossessionPlace Place;public string Container="";public float Cell;}
    readonly List<InventoryZone> inventoryZones=new List<InventoryZone>();
    NearbyPossession inventoryDrag,inventoryContext;bool inventoryDragRotated;Vector2 inventoryDragStart,inventoryMenuAt;
    float inventoryScroll,inventoryPreviewYaw=160;Rect inventoryPreviewRect;bool inventoryOrbit,inventoryNameEditing;
    HumanActor inventoryPreviewActor;GameObject inventoryPreviewRoot;Camera inventoryPreviewCamera;RenderTexture inventoryPortrait;
    readonly List<Material> inventoryPreviewMaterials=new List<Material>();
    readonly List<GameObject> inventoryPreviewGear=new List<GameObject>();Material inventoryPreviewRockMaterial;
    Texture2D inventoryRockIcon,inventoryBagIcon,inventoryFlakeIcon,inventoryWoodIcon;
    readonly Dictionary<ToolStoneMaterial,Texture2D> inventoryStoneIcons=new Dictionary<ToolStoneMaterial,Texture2D>();string inventoryHover;
    static readonly Color InventoryPanel=new Color(.055f,.075f,.08f,.97f),InventoryLine=new Color(.25f,.32f,.32f),InventoryGold=new Color(.78f,.72f,.53f);
    void InventoryFill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void InventoryOutline(Rect r,Color c,float edge=1){InventoryFill(new Rect(r.x,r.y,r.width,edge),c);InventoryFill(new Rect(r.x,r.yMax-edge,r.width,edge),c);InventoryFill(new Rect(r.x,r.y,edge,r.height),c);InventoryFill(new Rect(r.xMax-edge,r.y,edge,r.height),c);}
    void InventoryLabel(Rect r,string text,int size=16,Color? color=null)
    {var s=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true};s.normal.textColor=color??new Color(.83f,.86f,.83f);GUI.Label(r,text,s);}
    GUIStyle InventoryControlStyle(GUIStyle source,float scale)
    {return new GUIStyle(source){fontSize=Mathf.RoundToInt(16*scale),alignment=TextAnchor.MiddleCenter,padding=new RectOffset(4,4,1,1),fixedHeight=0};}
    bool InventoryButton(Rect rect,string text,float scale){return GUI.Button(rect,text,InventoryControlStyle(GUI.skin.button,scale));}
    void PrepareInventoryIcons()
    {
        if(inventoryRockIcon!=null)return;
        Texture2D Icon(int kind)
        {
            var texture=new Texture2D(96,96,TextureFormat.RGBA32,false);var pixels=new Color32[96*96];
            for(int y=0;y<96;y++)for(int x=0;x<96;x++)
            {
                float dx=(x-47.5f)/44,dy=(y-47.5f)/44,angle=Mathf.Atan2(dy,dx),radius=Mathf.Sqrt(dx*dx+dy*dy);
                bool bag=kind==1;
                float limit=bag?.88f:.82f+.06f*Mathf.Sin(angle*5+.7f)+.05f*Mathf.Cos(angle*3);
                if(kind==3){if(Mathf.Abs(dx-.13f*Mathf.Sin(dy*4))>.12f||Mathf.Abs(dy)>.91f)continue;}
                else if(kind==2){float u=(x-12)/72f,v=(y-10)/76f;if(u<0||v<0||u+v>1)continue;}
                else if(radius>limit)continue;
                float shade=.78f+.18f*(1-radius)+.1f*(-dx+dy)+((x*13+y*29)%11)*.004f;
                var c=bag||kind==3?new Color(.63f,.48f,.27f):new Color(.62f,.64f,.62f);pixels[y*96+x]=c*new Color(shade,shade,shade,1);
            }
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        inventoryRockIcon=Icon(0);inventoryBagIcon=Icon(1);inventoryFlakeIcon=Icon(2);inventoryWoodIcon=Icon(3);
    }
    string InventoryDisplayName(Possession item)=>StoneCrafting.Rock(item)?item.Name+" ("+StoneCrafting.Material(item)+")":item.Name;
    Texture2D InventoryItemIcon(Possession item)
    {
        if(PortableTechnology.Item(item.Kind)!=null)return PortableIcon(item);
        if(item.Kind==PossessionKind.TestBag)return inventoryBagIcon;
        if(WoodCrafting.Wood(item))return inventoryWoodIcon;
        if(item.Kind==PossessionKind.StoneFlake)return inventoryFlakeIcon;
        var material=ToolStone.Material(item.Variant);
        if(!inventoryStoneIcons.TryGetValue(material,out var icon))
        {
            icon=new Texture2D(96,96,TextureFormat.RGBA32,false);var pixels=inventoryRockIcon.GetPixels();var tint=ToolStone.Color(item.Variant,new V(.6,.6,.6));
            for(int i=0;i<pixels.Length;i++){float alpha=pixels[i].a;pixels[i]*=new Color((float)tint.X*1.6f,(float)tint.Y*1.6f,(float)tint.Z*1.6f);pixels[i].a=alpha;}icon.SetPixels(pixels);icon.Apply();inventoryStoneIcons.Add(material,icon);
        }
        return icon;
    }

    void PrepareInventoryPreview()
    {
        if(humanPlayer==null)return;
        if(inventoryPreviewActor!=null&&inventoryPreviewActor.Definition!=humanPlayer.Definition)DisposeInventoryPreview();
        if(inventoryPreviewActor==null)
        {
            inventoryPreviewRoot=new GameObject("Inventory character preview");inventoryPreviewRoot.transform.SetParent(transform,false);
            inventoryPreviewActor=CreateHuman(humanPlayer.Definition.Variant[0]-'A',0);inventoryPreviewActor.Root.transform.SetParent(inventoryPreviewRoot.transform,false);
            foreach(var t in inventoryPreviewActor.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=29;
            var shader=Resources.Load<Shader>("WorldGenInventoryPreview");
            foreach(var renderer in inventoryPreviewActor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials=renderer.sharedMaterials.Select(m=>{var n=new Material(shader);n.SetTexture("_BaseMap",m.GetTexture("_BaseMap"));n.SetFloat("_Cutoff",m.GetFloat("_Cutoff"));inventoryPreviewMaterials.Add(n);return n;}).ToArray();
                renderer.sharedMaterials=materials;renderer.forceMatrixRecalculationPerRender=true;renderer.updateWhenOffscreen=true;
            }
            var cameraRoot=new GameObject("Inventory portrait camera",typeof(Camera),typeof(UniversalAdditionalCameraData));cameraRoot.transform.SetParent(inventoryPreviewRoot.transform,false);
            inventoryPreviewCamera=cameraRoot.GetComponent<Camera>();inventoryPreviewCamera.enabled=false;inventoryPreviewCamera.cullingMask=1<<29;
            inventoryPreviewCamera.clearFlags=CameraClearFlags.SolidColor;inventoryPreviewCamera.backgroundColor=new Color(.075f,.1f,.11f);inventoryPreviewCamera.nearClipPlane=.05f;inventoryPreviewCamera.farClipPlane=10;
            inventoryPreviewCamera.fieldOfView=33;inventoryPreviewCamera.allowHDR=false;
            inventoryPortrait=new RenderTexture(480,800,24);inventoryPortrait.Create();inventoryPreviewCamera.targetTexture=inventoryPortrait;
        }
        inventoryPreviewRoot.SetActive(true);RenderInventoryPreview();
    }
    void RenderInventoryPreview()
    {
        if(inventoryPreviewActor==null)return;
        inventoryPreviewActor.Root.transform.localRotation=Quaternion.Euler(0,inventoryPreviewYaw,0);
        inventoryPreviewActor.Graph.Evaluate(0);
        var bones=inventoryPreviewActor.Root.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());PoseInventoryHands(inventoryPreviewActor,bones);
        foreach(var gear in inventoryPreviewGear){DestroyInventoryVisual(gear);}inventoryPreviewGear.Clear();
        foreach(var item in Inventory.Items.Where(i=>PortableTechnology.Worn(i.Place)||i.Place==PossessionPlace.RightHand||i.Place==PossessionPlace.LeftHand||i.Place==PossessionPlace.Container&&PortableTechnology.Belt(Inventory.Find(i.ContainerId))&&Inventory.Carried(i)))
        {
            var gear=CreatePossessionVisual(item,29);gear.transform.SetParent(inventoryPreviewRoot.transform,true);inventoryPreviewGear.Add(gear);
            if(PortableTechnology.Item(item.Kind)!=null&&(PortableTechnology.Worn(item.Place)||item.Place==PossessionPlace.Container)){PosePortableWear(gear,item,inventoryPreviewActor.Root.transform,bones);}
            else if(item.Place==PossessionPlace.Back){gear.transform.position=bones["spine_02"].position-inventoryPreviewActor.Root.transform.forward*.18f-Vector3.up*.2f;gear.transform.rotation=inventoryPreviewActor.Root.transform.rotation;}
            else {gear.transform.position=(item.Hands==2?(bones["hand_r"].position+bones["hand_l"].position)*.5f:bones["hand_"+(item.Place==PossessionPlace.RightHand?"r":"l")].position)-Vector3.up*(float)item.HeightM*.35f;gear.transform.rotation=inventoryPreviewActor.Root.transform.rotation;}
            if(WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true){gear.transform.rotation=inventoryPreviewActor.Root.transform.rotation*Quaternion.Euler(35,0,0);gear.transform.position=bones["hand_"+(item.Place==PossessionPlace.RightHand?"r":"l")].position-gear.transform.up*(float)item.HeightM*.35f;}
            if(item.Kind!=PossessionKind.TestBag&&PortableTechnology.Item(item.Kind)==null)
            {
                if(inventoryPreviewRockMaterial==null){inventoryPreviewRockMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));inventoryPreviewMaterials.Add(inventoryPreviewRockMaterial);}
                foreach(var r in gear.GetComponentsInChildren<Renderer>()){r.sharedMaterial=inventoryPreviewRockMaterial;var props=new MaterialPropertyBlock();props.SetFloat("_VertexColours",1);props.SetColor("_Color",new Color((float)item.Tint.X,(float)item.Tint.Y,(float)item.Tint.Z));r.SetPropertyBlock(props);}
            }
        }
        float h=inventoryPreviewActor.Definition.EyeHeight+.13f;
        inventoryPreviewCamera.transform.localPosition=new Vector3(0,h*.53f,-h*2.55f);inventoryPreviewCamera.transform.LookAt(inventoryPreviewRoot.transform.position+Vector3.up*h*.5f);
        inventoryPreviewCamera.Render();
    }
    void DisposeInventoryPreview()
    {
        if(inventoryPreviewActor!=null){DestroyHuman(inventoryPreviewActor);inventoryPreviewActor=null;}
        foreach(var gear in inventoryPreviewGear)DestroyInventoryVisual(gear);
        if(inventoryPreviewRoot!=null)Destroy(inventoryPreviewRoot);inventoryPreviewRoot=null;inventoryPreviewCamera=null;
        if(inventoryPortrait!=null){inventoryPortrait.Release();Destroy(inventoryPortrait);inventoryPortrait=null;}
        foreach(var m in inventoryPreviewMaterials)if(m!=null)Destroy(m);inventoryPreviewMaterials.Clear();
        inventoryPreviewGear.Clear();inventoryPreviewRockMaterial=null;
        if(inventoryRockIcon!=null)Destroy(inventoryRockIcon);if(inventoryBagIcon!=null)Destroy(inventoryBagIcon);if(inventoryFlakeIcon!=null)Destroy(inventoryFlakeIcon);if(inventoryWoodIcon!=null)Destroy(inventoryWoodIcon);foreach(var icon in inventoryStoneIcons.Values)Destroy(icon);inventoryStoneIcons.Clear();inventoryRockIcon=inventoryBagIcon=inventoryFlakeIcon=null;inventoryWoodIcon=null;
    }
    readonly List<(Rect rect,string id)> inventoryHitTargets=new List<(Rect,string)>();
    Rect inventoryContextRect;
    Possession HoveredInventoryPossession()
    {
        var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse==null)return null;var raw=mouse.position.ReadValue();var point=new Vector2(raw.x,Screen.height-raw.y);
        if(inventoryContext!=null&&inventoryContextRect.Contains(point))return null;
        for(int i=inventoryHitTargets.Count-1;i>=0;i--)if(inventoryHitTargets[i].rect.Contains(point))return InventoryCandidate(inventoryHitTargets[i].id)?.Item;
        return null;
    }
    void DrawInventoryItem(Rect rect,NearbyPossession candidate,float scale)
    {
        var item=candidate.Item;inventoryHitTargets.Add((rect,item.Id));bool hover=rect.Contains(Event.current.mousePosition);
        InventoryFill(rect,inventorySelected==item.Id?new Color(.25f,.27f,.21f):new Color(.12f,.16f,.16f));InventoryOutline(rect,hover?InventoryGold:InventoryLine);
        var icon=InventoryItemIcon(item);
        GUI.DrawTexture(new Rect(rect.x+5*scale,rect.y+5*scale,rect.width-10*scale,rect.height-10*scale),icon,ScaleMode.ScaleToFit,true);
        if(rect.width>75*scale&&rect.height>45*scale)InventoryLabel(new Rect(rect.x+5*scale,rect.yMax-23*scale,rect.width-8*scale,22*scale),item.Name,Mathf.RoundToInt(12*scale));
        var craft=CraftFor(item.Id);
        if(craft!=null)
        {
            var bar=new Rect(rect.x+2*scale,rect.yMax-8*scale,rect.width-4*scale,6*scale);InventoryFill(bar,new Color(.025f,.04f,.04f));
            InventoryFill(new Rect(bar.x,bar.y,bar.width*(float)craft.Fraction,bar.height),craft.Running?new Color(.36f,.72f,.49f):InventoryGold);
        }
        if(hover)inventoryHover=item.Id;
        if(hover&&inventoryContext==null&&Event.current.type==EventType.MouseDown)
        {
            inventorySelected=item.Id;
            if(Event.current.button==0){inventoryDrag=candidate;inventoryDragRotated=item.Rotated;inventoryDragStart=Event.current.mousePosition;inventoryContext=null;}
            else if(Event.current.button==1){inventoryContext=candidate;inventoryMenuAt=Event.current.mousePosition;}
            Event.current.Use();
        }
    }
    void DrawInventoryGrid(Possession bag,Rect rect,float cell,Rect clip)
    {
        inventoryZones.Add(new InventoryZone{Rect=rect,Clip=clip,Place=PossessionPlace.Container,Container=bag.Id,Cell=cell});
        for(int y=0;y<bag.ContainerHeight;y++)for(int x=0;x<bag.ContainerWidth;x++)
        {var r=new Rect(rect.x+x*cell,rect.y+y*cell,cell,cell);if(r.yMin>=clip.yMin&&r.yMax<=clip.yMax){InventoryFill(r,new Color(.08f,.11f,.12f));InventoryOutline(r,InventoryLine);}}
        foreach(var item in Inventory.Items.Where(i=>i.Place==PossessionPlace.Container&&i.ContainerId==bag.Id))
        {
            var r=new Rect(rect.x+item.X*cell,rect.y+item.Y*cell,item.PackedWidth*cell,item.PackedHeight*cell);
            if(r.yMin>=clip.yMin&&r.yMax<=clip.yMax)DrawInventoryItem(r,new NearbyPossession{Item=item},cell/48);
        }
    }
    void DrawInventory()
    {
        PrepareInventoryIcons();inventoryZones.Clear();inventoryHitTargets.Clear();inventoryHover=null;var e=Event.current;
        float scale=Mathf.Clamp(Screen.height/1080f,.55f,1.8f),pad=24*scale,top=82*scale,bottom=Screen.height-85*scale,gap=18*scale;
        InventoryFill(new Rect(0,0,Screen.width,Screen.height),new Color(.015f,.025f,.03f,.92f));
        InventoryLabel(new Rect(pad,20*scale,500*scale,42*scale),"POSSESSIONS",Mathf.RoundToInt(26*scale),InventoryGold);
        if(InventoryButton(new Rect(Screen.width-pad-100*scale,22*scale,100*scale,34*scale),"Close",scale))SetInventoryOpen(false);
        float usable=Screen.width-2*pad-2*gap,leftWidth=usable*.28f,middleWidth=usable*.37f;
        var left=new Rect(pad,top,leftWidth,bottom-top);var middle=new Rect(left.xMax+gap,top,middleWidth,bottom-top);var right=new Rect(middle.xMax+gap,top,Screen.width-pad-middle.xMax-gap,bottom-top);
        foreach(var r in new[]{left,middle,right}){InventoryFill(r,InventoryPanel);InventoryOutline(r,InventoryLine);}
        float inset=18*scale;
        GUI.SetNextControlName("inventory-player-name");Inventory.PlayerName=GUI.TextField(new Rect(left.x+inset,left.y+inset,left.width-2*inset,34*scale),Inventory.PlayerName,48,InventoryControlStyle(GUI.skin.textField,scale));
        inventoryNameEditing=GUI.GetNameOfFocusedControl()=="inventory-player-name";
        if(inventoryNameEditing&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.Tab)){GUI.FocusControl(null);inventoryNameEditing=false;e.Use();}
        inventoryPreviewRect=new Rect(left.x+inset,left.y+65*scale,left.width-2*inset,left.height-170*scale);
        if(inventoryPortrait!=null)GUI.DrawTexture(inventoryPreviewRect,inventoryPortrait,ScaleMode.ScaleToFit,false);
        if(e.type==EventType.MouseDown&&e.button==0&&inventoryPreviewRect.Contains(e.mousePosition)){inventoryOrbit=true;e.Use();}
        if(e.type==EventType.MouseDrag&&inventoryOrbit){inventoryPreviewYaw+=e.delta.x*.7f;RenderInventoryPreview();e.Use();}
        if(e.type==EventType.MouseUp)inventoryOrbit=false;
        if(InventoryButton(new Rect(left.x+inset,left.yMax-95*scale,(left.width-2*inset-gap)/2,30*scale),"Turn left",scale)){inventoryPreviewYaw-=45;RenderInventoryPreview();}
        if(InventoryButton(new Rect(left.center.x+gap/2,left.yMax-95*scale,(left.width-2*inset-gap)/2,30*scale),"Turn right",scale)){inventoryPreviewYaw+=45;RenderInventoryPreview();}
        InventoryLabel(new Rect(left.x+inset,left.yMax-54*scale,left.width-2*inset,46*scale),Inventory.CarriedMassKg.ToString("F1")+" kg carried  ·  movement "+Mathf.RoundToInt((float)Inventory.LoadSpeedScale*100)+"%",Mathf.RoundToInt(15*scale));
        InventoryLabel(new Rect(middle.x+inset,middle.y+inset,middle.width-2*inset,32*scale),"HELD & WORN",Mathf.RoundToInt(18*scale),InventoryGold);
        float handWidth=(middle.width-2*inset-gap)/2,handY=middle.y+80*scale;
        foreach(var hand in new[]{PossessionPlace.RightHand,PossessionPlace.LeftHand})
        {
            bool isRight=hand==PossessionPlace.RightHand;var r=new Rect(middle.x+inset+(isRight?0:handWidth+gap),handY,handWidth,115*scale);
            InventoryLabel(new Rect(r.x,r.y-28*scale,r.width,25*scale),(isRight?"Right hand":"Left hand"),Mathf.RoundToInt(14*scale));
            InventoryFill(r,new Color(.08f,.11f,.12f));InventoryOutline(r,InventoryLine);
            inventoryZones.Add(new InventoryZone{Rect=r,Clip=r,Place=hand});var item=Inventory.AtHand(hand);
            if(item!=null){if(item.Hands==2&&!isRight)InventoryLabel(new Rect(r.x+8*scale,r.y+30*scale,r.width-16*scale,65*scale),"Both hands occupied\n"+item.Name,Mathf.RoundToInt(14*scale));else DrawInventoryItem(new Rect(r.x+4*scale,r.y+4*scale,r.width-8*scale,r.height-8*scale),new NearbyPossession{Item=item},scale);}

        }
        float cell=48*scale;DrawEquipmentStorage(middle,handY+155*scale,inset,cell,scale);
        InventoryLabel(new Rect(right.x+inset,right.y+inset,right.width-2*inset,32*scale),"NEARBY",Mathf.RoundToInt(18*scale),InventoryGold);
        var nearbyView=new Rect(right.x+inset,right.y+60*scale,right.width-2*inset-15*scale,right.height-78*scale);
        inventoryZones.Add(new InventoryZone{Rect=nearbyView,Clip=nearbyView,Place=PossessionPlace.Ground});
        int columns=Math.Max(1,(int)(nearbyView.width/cell));var occupied=new HashSet<(int,int)>();int rows=0;
        foreach(var n in inventoryNearby)
        {
            // Nearby is a display layout, not a container: long loose shafts must remain visible.
            int w=Math.Min(columns,n.Item.PackedWidth),h=Math.Min(7,n.Item.PackedHeight),x=0,y=0;
            for(;;y++)
            {bool found=false;for(x=0;x<=columns-w;x++){bool fit=true;for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)if(occupied.Contains((x+xx,y+yy)))fit=false;if(fit){found=true;break;}}if(found)break;}
            for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++)occupied.Add((x+xx,y+yy));rows=Math.Max(rows,y+h);
            var r=new Rect(nearbyView.x+x*cell,nearbyView.y+y*cell-inventoryScroll,w*cell,h*cell);
            if(r.yMin>=nearbyView.yMin&&r.yMax<=nearbyView.yMax)DrawInventoryItem(r,n,scale);
        }
        float content=rows*cell;
        foreach(var n in inventoryNearby.Where(n=>n.Item.ContainerWidth>0))
        {
            var p=n.Item;float y=nearbyView.y+content+42*scale-inventoryScroll;
            if(y-30*scale>=nearbyView.y&&y<=nearbyView.yMax)InventoryLabel(new Rect(nearbyView.x,y-30*scale,nearbyView.width,28*scale),p.Name+" contents",Mathf.RoundToInt(15*scale),InventoryGold);
            DrawInventoryGrid(p,new Rect(nearbyView.x,y,p.ContainerWidth*cell,p.ContainerHeight*cell),cell,nearbyView);content+=42*scale+p.ContainerHeight*cell;
        }
        if(inventoryNearby.Count==0)InventoryLabel(new Rect(nearbyView.x,nearbyView.y,nearbyView.width,55*scale),"Nothing within reach.",Mathf.RoundToInt(16*scale));
        inventoryScroll=Mathf.Clamp(inventoryScroll,0,Mathf.Max(0,content-nearbyView.height));
        if(content>nearbyView.height)inventoryScroll=GUI.VerticalScrollbar(new Rect(right.xMax-inset-12*scale,nearbyView.y,12*scale,nearbyView.height),inventoryScroll,nearbyView.height,0,content);
        if(inventoryContext==null&&e.type==EventType.ScrollWheel&&nearbyView.Contains(e.mousePosition)){inventoryScroll=Mathf.Clamp(inventoryScroll+e.delta.y*cell,0,Mathf.Max(0,content-nearbyView.height));e.Use();}
        var selected=InventoryCandidate(inventoryHover??inventorySelected)?.Item;
        string info=selected==null?"":selected.Name+"  ·  "+selected.MassKg.ToString("F2")+" kg  ·  "+Mathf.RoundToInt((float)selected.WidthM*100)+" × "+Mathf.RoundToInt((float)selected.HeightM*100)+" cm";
        if(selected!=null&&WoodCrafting.Wood(selected))info+=" | "+WoodDescription(selected);
        if(selected!=null&&(StoneCrafting.Rock(selected)||selected.Kind==PossessionKind.StoneFlake))info+="  ·  "+StoneCrafting.Material(selected)+(selected.Kind==PossessionKind.StoneFlake?" · cutting edge":"");
        var activeCraft=ActiveItemCraft;if(activeCraft!=null)info=CraftLabel(activeCraft)+"  ·  "+Mathf.RoundToInt((float)activeCraft.Fraction*100)+"%  ·  "+StoneCrafting.RealSecondsLeft(activeCraft,PlayerRules.Settings).ToString("F1")+" s remaining";
        InventoryLabel(new Rect(pad,bottom+8*scale,Screen.width-pad*2,28*scale),Time.unscaledTime<inventoryNoticeUntil?inventoryNotice:info,Mathf.RoundToInt(15*scale),InventoryGold);
        InventoryLabel(new Rect(pad,Screen.height-38*scale,Screen.width-pad*2,30*scale),"E Pick up   ·   R Stow / rotate dragged item   ·   G Drop hovered item   ·   Q Swap hands   ·   B Set down / wear bag   ·   I / Tab Close",Mathf.RoundToInt(13*scale));
        DrawInventoryDrag(scale);DrawInventoryContext(scale);
    }
    void DrawInventoryDrag(float scale)
    {
        if(inventoryDrag==null)return;var e=Event.current;var item=inventoryDrag.Item;
        if(e.type==EventType.MouseUp&&e.button==0)
        {
            if((e.mousePosition-inventoryDragStart).sqrMagnitude>16)
            {
                var zone=inventoryZones.LastOrDefault(z=>z.Rect.Contains(e.mousePosition)&&z.Clip.Contains(e.mousePosition));
                if(zone!=null)
                {
                    int x=zone.Place==PossessionPlace.Container?(int)((e.mousePosition.x-zone.Rect.x)/zone.Cell):0,y=zone.Place==PossessionPlace.Container?(int)((e.mousePosition.y-zone.Rect.y)/zone.Cell):0;
                    RequestPossession(item.Id,zone.Place,zone.Container,x,y,inventoryDragRotated);
                }
            }
            inventoryDrag=null;e.Use();return;
        }
        if((e.mousePosition-inventoryDragStart).sqrMagnitude<=16)return;
        int w=inventoryDragRotated?item.CellsHigh:item.CellsWide,h=inventoryDragRotated?item.CellsWide:item.CellsHigh;
        var over=inventoryZones.LastOrDefault(z=>z.Rect.Contains(e.mousePosition)&&z.Clip.Contains(e.mousePosition));
        if(over!=null&&over.Place==PossessionPlace.Container)
        {
            int x=(int)((e.mousePosition.x-over.Rect.x)/over.Cell),y=(int)((e.mousePosition.y-over.Rect.y)/over.Cell);
            bool temporary=Inventory.Find(item.Id)==null;if(temporary)Inventory.Items.Add(item);
            bool valid;try{valid=Inventory.CanMove(item.Id,PossessionPlace.Container,over.Container,x,y,inventoryDragRotated,out _);}finally{if(temporary)Inventory.Items.Remove(item);}
            var preview=new Rect(over.Rect.x+x*over.Cell,over.Rect.y+y*over.Cell,w*over.Cell,h*over.Cell);
            if(preview.yMin>=over.Clip.yMin&&preview.yMax<=over.Clip.yMax)InventoryOutline(preview,valid?new Color(.5f,.8f,.5f):new Color(.9f,.35f,.3f),3*scale);
        }
        var r=new Rect(e.mousePosition.x+10*scale,e.mousePosition.y+10*scale,w*48*scale,h*48*scale);InventoryFill(r,new Color(.25f,.3f,.26f,.8f));InventoryOutline(r,InventoryGold);GUI.DrawTexture(r,InventoryItemIcon(item),ScaleMode.ScaleToFit,true);
    }
    void DrawInventoryContext(float scale)=>DrawPortableContext(scale);
    string WoodDescription(Possession p)=>p.HeightM.ToString("F2")+" m long, "+(p.WidthM*100).ToString("F1")+" cm thick; "+(p.WoodStraightness>=.7?"straight":"crooked")+", "+(p.WoodSoundness>=.65?"sound wood":"weathered/fragile wood")+(p.BarkRemoved?", bark removed.":", bark intact.");
}

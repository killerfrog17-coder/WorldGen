using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class WorldGenRuntime
{
    readonly List<Texture2D> menuTextures=new List<Texture2D>();Font titleFont,bodyFont;Texture2D menuBackdrop;
    readonly Color ink=new Color(.038f,.061f,.073f),paper=new Color(.9f,.87f,.78f),copper=new Color(.73f,.48f,.27f);
    GUISkin BuildGameTheme()
    {
        if(gameSkin!=null)return gameSkin;gameSkin=Instantiate(GUI.skin);
        var fonts=Font.GetOSInstalledFontNames();if(fonts.Contains("Georgia"))titleFont=Font.CreateDynamicFontFromOSFont("Georgia",32);if(fonts.Contains("Segoe UI"))bodyFont=Font.CreateDynamicFontFromOSFont("Segoe UI",16);if(bodyFont!=null)gameSkin.font=bodyFont;
        Texture2D Surface(Color top,Color bottom,bool border)
        {
            var t=new Texture2D(64,32,TextureFormat.RGBA32,false);for(int y=0;y<32;y++)for(int x=0;x<64;x++)t.SetPixel(x,y,border&&(x==0||x==63||y==0||y==31)?new Color(.27f,.32f,.32f):Color.Lerp(bottom,top,y/31f));t.Apply();menuTextures.Add(t);return t;
        }
        Texture2D panel=Surface(new Color(.072f,.101f,.112f),ink,true),normal=Surface(new Color(.105f,.145f,.155f),new Color(.074f,.111f,.124f),true),hover=Surface(new Color(.23f,.25f,.23f),new Color(.13f,.18f,.18f),true),active=Surface(new Color(.27f,.23f,.16f),new Color(.18f,.16f,.12f),true);
        gameSkin.box.normal.background=panel;gameSkin.box.border=new RectOffset(2,2,2,2);gameSkin.box.padding=new RectOffset(12,12,12,12);
        gameSkin.button.normal.background=normal;gameSkin.button.hover.background=hover;gameSkin.button.focused.background=hover;gameSkin.button.active.background=active;gameSkin.button.onNormal.background=active;gameSkin.button.onHover.background=hover;gameSkin.button.border=new RectOffset(2,2,2,2);
        gameSkin.button.normal.textColor=paper;gameSkin.button.hover.textColor=Color.white;gameSkin.button.onNormal.textColor=new Color(1,.82f,.52f);gameSkin.button.padding=new RectOffset(12,12,9,9);gameSkin.button.margin=new RectOffset(2,2,4,4);
        gameSkin.label.normal.textColor=paper;gameSkin.label.fontSize=14;gameSkin.label.wordWrap=true;
        gameSkin.textField.normal.background=normal;gameSkin.textField.focused.background=hover;gameSkin.textField.normal.textColor=paper;gameSkin.textField.focused.textColor=Color.white;gameSkin.textField.border=new RectOffset(2,2,2,2);gameSkin.textField.padding=new RectOffset(8,8,6,6);gameSkin.textField.fontSize=14;
        gameSkin.toggle.normal.textColor=paper;gameSkin.toggle.onNormal.textColor=paper;
        return gameSkin;
    }
    GUIStyle TitleStyle(int size)=>new GUIStyle(GUI.skin.label){font=titleFont??GUI.skin.font,fontSize=size,wordWrap=true};
    GUIStyle SectionStyle(){var s=new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold};s.normal.textColor=new Color(.77f,.65f,.46f);return s;}
    GUIStyle MutedStyle(){var s=new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=true};s.normal.textColor=new Color(.72f,.77f,.74f);return s;}
    void DisposeGameTheme(){foreach(var texture in menuTextures)if(texture!=null)Destroy(texture);menuTextures.Clear();if(titleFont!=null)Destroy(titleFont);if(bodyFont!=null)Destroy(bodyFont);if(gameSkin!=null)Destroy(gameSkin);}
    bool MenuButton(string text)=>GUILayout.Button(text,new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,fontSize=17},GUILayout.Height(44));
    void MenuCard(Rect r){GUI.Box(r,"");var old=GUI.color;GUI.color=copper;GUI.DrawTexture(new Rect(r.x,r.y,Math.Min(110,r.width),2),Texture2D.whiteTexture);GUI.color=old;}
    void MenuLine(Vector2 a,Vector2 b,Color color,float thickness=1)
    {var matrix=GUI.matrix;var tint=GUI.color;GUI.color=color;float angle=Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg;GUIUtility.RotateAroundPivot(angle,a);GUI.DrawTexture(new Rect(a.x,a.y,(b-a).magnitude,thickness),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=tint;}
    void DrawMenuBackdrop()
    {
        if(menuBackdrop==null)
        {menuBackdrop=new Texture2D(128,128,TextureFormat.RGBA32,false);for(int y=0;y<128;y++)for(int x=0;x<128;x++){float glow=Mathf.Exp(-((x-92)*(x-92)+(y-68)*(y-68))/3500f);menuBackdrop.SetPixel(x,y,Color.Lerp(ink,new Color(.12f,.19f,.21f),glow));}menuBackdrop.Apply();menuTextures.Add(menuBackdrop);}
        GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),menuBackdrop);
        var center=new Vector2(Screen.width*.74f,Screen.height*.5f);float radius=Math.Min(Screen.width*.245f,Screen.height*.36f);var color=new Color(.48f,.57f,.52f,.24f);
        for(int ring=-3;ring<=3;ring++)
        {float lat=ring*Mathf.PI/8;Vector2 last=Vector2.zero;for(int i=0;i<=96;i++){float a=i*Mathf.PI*2/96;var p=center+new Vector2(Mathf.Cos(a)*Mathf.Cos(lat),Mathf.Sin(lat)*.92f+Mathf.Sin(a)*Mathf.Cos(lat)*.25f)*radius;if(i>0)MenuLine(last,p,color);last=p;}}
        for(int line=0;line<8;line++){Vector2 last=Vector2.zero;for(int i=0;i<=80;i++){float a=i*Mathf.PI*2/80;var p=center+new Vector2(Mathf.Cos(a)*Mathf.Sin(line*Mathf.PI/8),Mathf.Sin(a))*radius;if(i>0)MenuLine(last,p,color);last=p;}}
        MenuLine(new Vector2(40,Screen.height-42),new Vector2(Screen.width-40,Screen.height-42),new Color(.43f,.37f,.27f,.5f));
        GUI.Label(new Rect(44,Screen.height-35,450,24),"WORLDGEN  /  A WORLD TO INHABIT",MutedStyle());
    }
    void DrawFrontEnd()
    {
        DrawMenuBackdrop();float width=Math.Min(410,Screen.width*.42f),x=Screen.width*.095f;
        if(screen=="Menu")
        {
            GUILayout.BeginArea(new Rect(x,Math.Max(30,Screen.height*.18f),width,Screen.height*.72f));
            GUILayout.Label("WORLDGEN",TitleStyle(Math.Min(55,(int)(width/7))));GUILayout.Label("Histories unfold. Worlds await.",MutedStyle());GUILayout.Space(28);
            if(worldReady&&world!=null&&MenuButton(play.HasPlayer?"Continue game":"Continue world")){screen="Simulation";if(play.HasPlayer)RequestTerrain();}
            GUI.enabled=!Busy&&humanStep==null;if(MenuButton("New game"))NewGame();GUI.enabled=true;
            if(MenuButton("Load game")){libraryOpen=true;if(library!=null)saves=library.Entries();}
            if(MenuButton("Settings")){screen="Options";optionsReturn="Menu";}
            GUILayout.Space(12);if(MenuButton("Exit"))Application.Quit();GUILayout.EndArea();
            if(discardPrompt)
            {var r=new Rect(Screen.width/2-235,Screen.height/2-100,470,200);MenuCard(r);GUILayout.BeginArea(new Rect(r.x+24,r.y+20,r.width-48,r.height-40));GUILayout.Label("Start another world?",TitleStyle(24));GUILayout.Label("Unsaved progress in the current game will be left behind.");GUILayout.BeginHorizontal();if(GUILayout.Button("Cancel"))discardPrompt=false;if(GUILayout.Button("Start new game"))NewGame();GUILayout.EndHorizontal();GUILayout.EndArea();}
        }
        else
        {
            var r=new Rect(x,Screen.height*.12f,Math.Min(470,Screen.width-x-30),Screen.height*.76f);MenuCard(r);GUILayout.BeginArea(new Rect(r.x+24,r.y+22,r.width-48,r.height-44));GUILayout.Label("SETTINGS",TitleStyle(29));GUILayout.Space(20);DrawAudioOptions();GUILayout.Label("DISPLAY",SectionStyle());
            bool full=GUILayout.Toggle(Screen.fullScreen,"Fullscreen");if(full!=Screen.fullScreen)Screen.fullScreenMode=full?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
            bool sync=GUILayout.Toggle(QualitySettings.vSyncCount>0,"VSync");QualitySettings.vSyncCount=sync?1:0;
            bool smooth=GUILayout.Toggle(naturalTerrain,"Smooth world-map surface");if(smooth!=naturalTerrain){naturalTerrain=smooth;ApplyCachedDisplay();}
            GUILayout.FlexibleSpace();if(MenuButton("Back"))screen=optionsReturn;GUILayout.EndArea();
        }
    }
    void DrawNewGameSetup(float width)
    {
        MenuCard(new Rect(0,0,width,Screen.height));GUILayout.BeginArea(new Rect(18,16,width-36,Screen.height-32));
        GUILayout.Label("NEW GAME",TitleStyle(27));GUILayout.Space(8);saveName=GUILayout.TextField(saveName,80);GUILayout.Space(12);
        GUILayout.BeginHorizontal();if(GUILayout.Toggle(setupCategory=="World","World generation",GUI.skin.button))setupCategory="World";if(GUILayout.Toggle(setupCategory=="Time","Time & calendar",GUI.skin.button))setupCategory="Time";GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();if(GUILayout.Toggle(setupCategory=="Scale","Scale & density",GUI.skin.button))setupCategory="Scale";if(GUILayout.Toggle(setupCategory=="Sapients","Sapients",GUI.skin.button))setupCategory="Sapients";GUILayout.EndHorizontal();GUILayout.Space(16);
        if(GUILayout.Toggle(setupCategory=="Sky","Sky & atmosphere",GUI.skin.button))setupCategory="Sky";
        scroll=GUILayout.BeginScrollView(scroll);
        if(setupCategory=="Time"||setupCategory=="Scale")DrawPlaySettings(section:setupCategory);
        if(setupCategory=="Sapients")DrawHumanSetup();
        if(setupCategory=="Sky")DrawSkySettings();
        if(setupCategory=="World")
        {
            GUI.enabled=!Busy;GUILayout.Label("Map type",SectionStyle());if(GUILayout.Button(preset+"  ▾"))mapTypeMenu=!mapTypeMenu;
            if(mapTypeMenu){mapPresetScroll=GUILayout.BeginScrollView(mapPresetScroll,GUILayout.Height(220));foreach(string choice in WorldPresets.Names)if(GUILayout.Button(choice)){recipe=setup.Preset(choice,recipe);preset=choice;inputText.Clear();dirty=true;mapTypeMenu=false;}if(GUILayout.Button("Custom")){preset="Custom";recipe.TerrainSource="Procedural";recipe.LandShape=null;dirty=true;mapTypeMenu=false;}GUILayout.EndScrollView();}
            void Integer(string key,string label,int current,int min,int max,Action<int> change,bool dice=false)
            {
                GUILayout.BeginHorizontal();GUILayout.Label(label);GUI.SetNextControlName(key);
                if(!inputText.TryGetValue(key,out var text)||GUI.GetNameOfFocusedControl()!=key)text=current.ToString();
                string next=GUILayout.TextField(text,GUILayout.Width(100));inputText[key]=next;
                if(next!=text&&int.TryParse(next,out int value)&&value>=min&&value<=max){change(value);dirty=true;}
                if(dice&&DiceButton())RollSeed();GUILayout.EndHorizontal();
            }
            Integer(nameof(Recipe.Seed),"Seed",recipe.Seed,0,16777215,v=>recipe.Seed=v,true);
            Integer(nameof(Recipe.Detail),"Cells",recipe.Detail,5000,2560000,v=>recipe.Detail=v);
            GUILayout.Space(12);GUI.enabled=true;if(GUILayout.Button((advancedGeneration?"▾  ":"▸  ")+"Advanced generation"))advancedGeneration=!advancedGeneration;
            if(advancedGeneration){
            for(int g=0;g<groups.Length;g++)
            {GUI.enabled=true;if(GUILayout.Button((panels[g]?"▾  ":"▸  ")+(groups[g]=="Terrain sculpting"?"Terrain warp":groups[g])))panels[g]=!panels[g];if(!panels[g])continue;GUI.enabled=!Busy&&(groups[g]!="World"||recipe.TerrainSource!="Earth");foreach(var f in WorldGen.Presentation.WorldSetup.Fields(groups[g]))DrawSetting(f);}
            }
        }
        GUI.enabled=true;GUILayout.EndScrollView();GUILayout.Space(8);
        if(Busy)GUILayout.Label((Generating?generationStage:progress)+" · "+Math.Max(0,Math.Min(100,Generating?generationPercent:percent))+"%",MutedStyle());
        if(error.Length>0)GUILayout.Label(error);if(dirty&&world!=null)GUILayout.Label("Generation settings changed",MutedStyle());
        GUI.enabled=!Busy;if(GUILayout.Button(world==null?"Generate world":"Regenerate world",GUILayout.Height(38)))Generate();
        GUI.enabled=!Busy&&worldReady&&!dirty&&humans!=null;if(GUILayout.Button("Start game",GUILayout.Height(44)))StartHumanWorld();GUI.enabled=true;
        if(Busy){if(GUILayout.Button("Cancel generation"))CancelGeneration();}else if(GUILayout.Button("Back"))screen="Menu";GUILayout.EndArea();
    }
}

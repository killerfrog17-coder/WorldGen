using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class ChatLine {public string Text;public double At;public bool Error;}
    readonly List<ChatLine> chatLines=new List<ChatLine>();readonly List<string> chatHistory=new List<string>();
    Keyboard chatKeyboard;Font chatFont;string chatText="",chatDraft="";int chatCaret,chatOpenedFrame,chatHistoryIndex,chatScroll;bool chatOpen,chatSelectAll;
    Func<string,PlayCommandResult> command;GUIStyle chatStyle;
    public bool ChatOpen=>chatOpen;
    public string ChatText=>chatText;
    void PostExplorerChat(string text,bool error=false)
    {chatLines.Add(new ChatLine{Text=text,At=Time.realtimeSinceStartupAsDouble,Error=error});while(chatLines.Count>100)chatLines.RemoveAt(0);}
    public void SetCommandHandler(Func<string,PlayCommandResult> handler){command=handler;}
    public void OpenChat(string prefix="")
    {
        if(paused||mapOpen||ecologyOpen)return;chatOpen=true;chatText=prefix;chatCaret=prefix.Length;chatOpenedFrame=Time.frameCount;chatHistoryIndex=chatHistory.Count;chatDraft="";chatSelectAll=false;chatScroll=0;GUIUtility.keyboardControl=0;CaptureMouse();
    }
    void CloseChat(){chatOpen=false;chatSelectAll=false;CaptureMouse();}
    void ChatCharacter(char character)
    {
        if(closed)return;
        if(!chatOpen){if(character=='/'&&!paused&&!mapOpen&&!ecologyOpen)OpenChat("/");return;}
        if(Time.frameCount==chatOpenedFrame||char.IsControl(character))return;InsertChat(character.ToString());
    }
    void InsertChat(string text)
    {
        text=new string(text.Where(c=>!char.IsControl(c)).ToArray());if(chatSelectAll){chatText="";chatCaret=0;chatSelectAll=false;}
        if(text.Length>256-chatText.Length)text=text.Substring(0,256-chatText.Length);chatText=chatText.Insert(chatCaret,text);chatCaret+=text.Length;
    }
    public void SubmitChat()
    {
        string message=chatText.Trim();CloseChat();if(message.Length==0)return;
        if(chatHistory.Count==0||chatHistory[chatHistory.Count-1]!=message)chatHistory.Add(message);if(chatHistory.Count>100)chatHistory.RemoveAt(0);
        if(message.StartsWith("/",StringComparison.Ordinal))
        {
            var result=ExecuteExplorerCommand(message)??command?.Invoke(message)??new PlayCommandResult(false,"Commands are not ready yet.");
            foreach(string line in result.Lines)chatLines.Add(new ChatLine{Text=line,At=Time.realtimeSinceStartupAsDouble,Error=!result.Success});
        }
        else chatLines.Add(new ChatLine{Text="<You> "+message,At=Time.realtimeSinceStartupAsDouble});
        while(chatLines.Count>100)chatLines.RemoveAt(0);
    }
    bool HandleChatKeys()
    {
        var keyboard=ActiveKeyboard;
        if(chatKeyboard!=keyboard){if(chatKeyboard!=null)chatKeyboard.onTextInput-=ChatCharacter;chatKeyboard=keyboard;if(chatKeyboard!=null)chatKeyboard.onTextInput+=ChatCharacter;}
        if(keyboard==null)return chatOpen;
        bool enter=keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame;
        if(!chatOpen)
        {
            if(!paused&&!mapOpen&&!ecologyOpen&&(enter||keyboard.slashKey.wasPressedThisFrame)){OpenChat(keyboard.slashKey.wasPressedThisFrame?"/":"");return true;}return false;
        }
        if(Time.frameCount==chatOpenedFrame)return true;
        if(keyboard.escapeKey.wasPressedThisFrame){CloseChat();return true;}if(enter){SubmitChat();return true;}
        bool control=keyboard.ctrlKey.isPressed||keyboard.leftMetaKey.isPressed||keyboard.rightMetaKey.isPressed;
        if(control&&keyboard.aKey.wasPressedThisFrame)chatSelectAll=true;
        if(control&&keyboard.cKey.wasPressedThisFrame&&chatSelectAll)GUIUtility.systemCopyBuffer=chatText;
        if(control&&keyboard.vKey.wasPressedThisFrame)InsertChat(GUIUtility.systemCopyBuffer??"");
        if(keyboard.homeKey.wasPressedThisFrame){chatCaret=0;chatSelectAll=false;}if(keyboard.endKey.wasPressedThisFrame){chatCaret=chatText.Length;chatSelectAll=false;}
        if(keyboard.leftArrowKey.wasPressedThisFrame){chatCaret=Math.Max(0,chatCaret-1);if(chatCaret>0&&chatCaret<chatText.Length&&char.IsLowSurrogate(chatText[chatCaret]))chatCaret--;chatSelectAll=false;}
        if(keyboard.rightArrowKey.wasPressedThisFrame){chatCaret=Math.Min(chatText.Length,chatCaret+1);if(chatCaret<chatText.Length&&char.IsLowSurrogate(chatText[chatCaret]))chatCaret++;chatSelectAll=false;}
        if(keyboard.backspaceKey.wasPressedThisFrame||keyboard.deleteKey.wasPressedThisFrame)
        {
            if(chatSelectAll){chatText="";chatCaret=0;chatSelectAll=false;}
            else if(keyboard.backspaceKey.wasPressedThisFrame&&chatCaret>0){int count=char.IsLowSurrogate(chatText[chatCaret-1])&&chatCaret>1?2:1;chatText=chatText.Remove(chatCaret-count,count);chatCaret-=count;}
            else if(keyboard.deleteKey.wasPressedThisFrame&&chatCaret<chatText.Length){int count=char.IsHighSurrogate(chatText[chatCaret])&&chatCaret+1<chatText.Length?2:1;chatText=chatText.Remove(chatCaret,count);}
        }
        if(keyboard.upArrowKey.wasPressedThisFrame||keyboard.downArrowKey.wasPressedThisFrame)
        {
            if(chatHistoryIndex==chatHistory.Count)chatDraft=chatText;chatHistoryIndex=Math.Max(0,Math.Min(chatHistory.Count,chatHistoryIndex+(keyboard.upArrowKey.wasPressedThisFrame?-1:1)));
            chatText=chatHistoryIndex==chatHistory.Count?chatDraft:chatHistory[chatHistoryIndex];chatCaret=chatText.Length;chatSelectAll=false;
        }
        if(keyboard.tabKey.wasPressedThisFrame)
        {
            var options=chatText.StartsWith("/spawn ",StringComparison.OrdinalIgnoreCase)?FaunaSpawnCommands.Complete(chatText,animalProfiles).Concat(new[]{"/spawn human"}.Where(s=>s.StartsWith(chatText,StringComparison.OrdinalIgnoreCase))).ToArray():PlayCommands.Complete(chatText);if(options.Length==1){chatText=options[0]+" ";chatCaret=chatText.Length;}
            else if(options.Length>1)PostExplorerChat(string.Join("   ",options.Take(8))+(options.Length>8?"   (type more to narrow; /spawn list lists animals)":""));
        }
        return true;
    }
    void DrawChat()
    {
        if(paused||mapOpen||ecologyOpen)return;
        if(!chatOpen&&(chatLines.Count==0||Time.realtimeSinceStartupAsDouble-chatLines[chatLines.Count-1].At>=11))return;
        if(chatFont==null)chatFont=Font.CreateDynamicFontFromOSFont(new[]{"Consolas","Liberation Mono","Courier New"},17);
        if(chatStyle==null)chatStyle=new GUIStyle(GUI.skin.label){font=chatFont,fontSize=17,richText=false,padding=new RectOffset(0,0,0,0),alignment=TextAnchor.UpperLeft};
        var style=chatStyle;style.wordWrap=true;style.normal.textColor=Color.white;
        float width=Math.Min(720,Screen.width-16),bottom=Screen.height-(chatOpen?43:10),top=Math.Max(8,Screen.height*.48f);var e=Event.current;
        if(chatOpen&&e.type==EventType.ScrollWheel){chatScroll=Math.Max(0,Math.Min(Math.Max(0,chatLines.Count-1),chatScroll+(e.delta.y>0?2:-2)));e.Use();}
        for(int i=chatLines.Count-1-chatScroll;i>=0;i--)
        {
            var line=chatLines[i];double age=Time.realtimeSinceStartupAsDouble-line.At;float fade=chatOpen?1:Mathf.Clamp01((float)(11-age));if(fade<=0)break;
            float height=Math.Max(22,style.CalcHeight(new GUIContent(line.Text),width-12));bottom-=height;if(bottom<top)break;
            GUI.color=new Color(0,0,0,.48f*fade);GUI.DrawTexture(new Rect(4,bottom,width,height),Texture2D.whiteTexture);GUI.color=Color.white;
            var r=new Rect(10,bottom+1,width-12,height);style.normal.textColor=new Color(0,0,0,fade);GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),line.Text,style);
            style.normal.textColor=line.Error?new Color(1,.43f,.43f,fade):new Color(1,1,1,fade);GUI.Label(r,line.Text,style);
        }
        if(chatOpen)
        {
            var r=new Rect(4,Screen.height-35,Screen.width-8,30);GUI.color=new Color(0,0,0,.68f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;GUI.BeginGroup(new Rect(r.x+5,r.y+4,r.width-10,r.height-6));
            style.wordWrap=false;float caret=style.CalcSize(new GUIContent(chatText.Substring(0,chatCaret))).x,scroll=Math.Max(0,caret-r.width+30);
            if(chatSelectAll){GUI.color=new Color(.2f,.35f,.7f,.8f);GUI.DrawTexture(new Rect(-scroll,0,style.CalcSize(new GUIContent(chatText)).x,23),Texture2D.whiteTexture);GUI.color=Color.white;}
            style.normal.textColor=Color.white;GUI.Label(new Rect(-scroll,0,Math.Max(r.width,3000),26),chatText,style);
            if((Time.realtimeSinceStartupAsDouble%1)<.6)GUI.DrawTexture(new Rect(caret-scroll,1,1.5f,21),Texture2D.whiteTexture);GUI.EndGroup();
        }
        GUI.color=Color.white;
    }
    void DisposeChat(){if(chatKeyboard!=null)chatKeyboard.onTextInput-=ChatCharacter;if(chatFont!=null)Destroy(chatFont);}
}

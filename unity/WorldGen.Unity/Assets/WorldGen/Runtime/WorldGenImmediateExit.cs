using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Alt+F4 is the owner's explicit discard-and-exit command. Avoid Unity's
// scene/mesh/audio teardown on this path; no save or confirmation is started.
[DefaultExecutionOrder(-32000)]
public sealed class WorldGenImmediateExit : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("kernel32.dll")] static extern void ExitProcess(uint code);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
#endif
    bool nativeCheck,exiting;
    string checkOutput;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var owner=new GameObject("Immediate exit");DontDestroyOnLoad(owner);
        owner.AddComponent<WorldGenImmediateExit>();
    }
    bool ChordHeld
    {
        get
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.altKey.isPressed&&keyboard.f4Key.isPressed)return true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (GetAsyncKeyState(0x12)&0x8000)!=0&&(GetAsyncKeyState(0x73)&0x8000)!=0;
#else
            return false;
#endif
        }
    }
    void Awake(){Application.wantsToQuit+=WantsToQuit;}
    void Update()
    {
        // Do not react to Alt+F4 used in another application while background
        // world simulation is running.
        if((Application.isFocused||nativeCheck)&&ChordHeld)ExitNow();
    }
    bool WantsToQuit()
    {
        // Windows may deliver its close request before the next Update.
        if(ChordHeld)ExitNow();
        return true;
    }
    void ExitNow()
    {
        if(exiting)return;exiting=true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        ExitProcess(0);
#else
        Application.Quit();
#endif
    }
    void OnDestroy()
    {
        Application.wantsToQuit-=WantsToQuit;
        if(nativeCheck)File.WriteAllText(Path.Combine(checkOutput,"unexpected-teardown.txt"),"Immediate exit entered scene teardown.");
    }
    void Start()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("--worldgen-immediate-exit-check"))return;
        int at=Array.IndexOf(args,"--worldgen-output");checkOutput=args[at+1];
        Directory.CreateDirectory(checkOutput);StartCoroutine(CheckInput());
    }
    IEnumerator CheckInput()
    {
        // Feed only this isolated player's input system, never OS/global keys.
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftAlt));yield return null;yield return null;
        File.WriteAllText(Path.Combine(checkOutput,"alt-alone-survived.txt"),"PASS");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F4));yield return null;yield return null;
        File.WriteAllText(Path.Combine(checkOutput,"f4-alone-survived.txt"),"PASS");
        nativeCheck=true;
        File.WriteAllText(Path.Combine(checkOutput,"armed.txt"),DateTime.UtcNow.ToString("O"));
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftAlt,Key.F4));
        yield return new WaitForSecondsRealtime(3);
        File.WriteAllText(Path.Combine(checkOutput,"failure.txt"),"Alt+F4 failed to exit.");Application.Quit(1);
    }
}

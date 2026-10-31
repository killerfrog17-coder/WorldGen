using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Editor-only graph checks: no player build, resource rebuild or asset saves.
public static class SmallCarryRevisionCheck
{
    public static void Run()
    {
        var report=new List<string>();
        var libraries=new[]{"HumanAppearance/Library","Humans/HumanLibrary"}
            .Select(path=>Resources.Load<WorldGenHumanLibrary>(path)).ToArray();
        if(libraries.Any(l=>l==null||l.People.Length!=6))throw new InvalidDataException("Both six-character libraries are required");
        var bank=Resources.Load<WorldGenHumanMotionBank>("HumanKimodo/Library");
        if(bank==null)throw new InvalidDataException("Current Kimodo bank missing");
        var type=typeof(LocalTerrainExplorer).Assembly.GetType("WorldGenSharedHumanMotion",true);
        foreach(var library in libraries)
        foreach(var person in library.People)
        {
            var actor=UnityEngine.Object.Instantiate(person.Prefab);
            actor.SetActive(true);var animator=actor.GetComponent<Animator>();animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
            var graph=PlayableGraph.Create("Small item carry mask check");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            object controller=null;
            try
            {
                controller=Activator.CreateInstance(type,new object[]{graph,actor.transform,bank.Find(person.Variant),true,person.EyeHeight,null});
                var output=AnimationPlayableOutput.Create(graph,"Body",actor.GetComponent<Animator>());
                output.SetSourcePlayable((AnimationLayerMixerPlayable)type.GetField("Output").GetValue(controller));graph.Play();
                var locomotion=type.GetMethod("Locomotion");var action=type.GetMethod("Action");
                var bones=actor.GetComponentsInChildren<Transform>(true);
                bool Finger(Transform t)=>new[]{"thumb_","index_","middle_","ring_","pinky_"}.Any(p=>t.name.StartsWith(p,StringComparison.Ordinal));
                foreach(float speed in new[]{0f,1.5f,3.4f})foreach(bool left in new[]{false,true})
                {
                    for(int i=0;i<12;i++)
                    {
                        locomotion.Invoke(controller,new object[]{speed,false,false,false,.1f,Vector2.up,"Swim"});
                        action.Invoke(controller,new object[]{"Empty",0d,false,false,null,0d,false,.1f,false,false,false});graph.Evaluate(0);
                    }
                    var before=bones.ToDictionary(t=>t,t=>t.localRotation);
                    string role=left?"ToolReady_Left":"ToolReady";
                    for(int i=0;i<12;i++){action.Invoke(controller,new object[]{role,0d,true,false,null,0d,false,.1f,true,false,left});graph.Evaluate(0);}
                    float body=bones.Where(t=>!Finger(t)).Max(t=>Quaternion.Angle(before[t],t.localRotation));
                    var reference=UnityEngine.Object.Instantiate(person.Prefab);
                    float grip;
                    try
                    {
                        bank.Find(person.Variant).Clips.First(c=>c.Role==role).Clip.SampleAnimation(reference,0);
                        var expected=reference.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,reference.transform),t=>t.localRotation);
                        grip=bones.Where(t=>Finger(t)&&t.name.EndsWith(left?"_l":"_r",StringComparison.Ordinal)).Max(t=>Quaternion.Angle(expected[AnimationUtility.CalculateTransformPath(t,actor.transform)],t.localRotation));
                    }
                    finally{UnityEngine.Object.DestroyImmediate(reference);}
                    if(body>.1f||grip>.2f)throw new InvalidDataException($"Carry mask {person.Variant} {speed} {left}: body={body} grip-error={grip}");
                    report.Add($"PASS {library.name}/{person.Variant} speed={speed} left={left}: ordinary body unchanged ({body:F4}deg), held fingers match source grip ({grip:F4}deg)");
                    for(int i=0;i<12;i++){action.Invoke(controller,new object[]{"SpearReady2",0d,true,false,null,0d,false,.1f,false,false,false});graph.Evaluate(0);}
                    float weapon=bones.Where(t=>t.name.StartsWith("upperarm",StringComparison.Ordinal)).Max(t=>Quaternion.Angle(before[t],t.localRotation));
                    if(weapon<5)throw new InvalidDataException("Weapon action stopped applying: "+person.Variant+" "+weapon);
                    report.Add($"PASS {library.name}/{person.Variant} speed={speed} left={left}: weapon upper-body action still applies ({weapon:F2}deg)");
                }
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                if(controller!=null)foreach(var mask in (IEnumerable<AvatarMask>)type.GetField("masks",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(controller))UnityEngine.Object.DestroyImmediate(mask);
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));string folder=Path.Combine(repo,"artifacts/human-small-carry");Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder,"editor-mask-check.txt"),report);Debug.Log("SMALL CARRY EDITOR PASS "+report.Count);
    }
}

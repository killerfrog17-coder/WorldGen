using System;
using System.Linq;
using UnityEngine;

public sealed class WorldGenHumanAgeLibrary : ScriptableObject
{
    public HumanAgeModel[] People=Array.Empty<HumanAgeModel>();
    public HumanAgeModel Find(string variant,double age)=>age>=17?null:People.Where(p=>p.Variant==variant).OrderBy(p=>Math.Abs(p.Years-age)).FirstOrDefault();
}
[Serializable] public sealed class HumanAgeModel
{
    public string Variant;public int Years;public float EyeHeight;public GameObject Prefab;
    public HumanGroundOffset[] Grounding=Array.Empty<HumanGroundOffset>();
    public float Offset(string role)=>Grounding.FirstOrDefault(g=>g.Role==role)?.Metres??0;
}
[Serializable] public sealed class HumanGroundOffset{public string Role;public float Metres;}

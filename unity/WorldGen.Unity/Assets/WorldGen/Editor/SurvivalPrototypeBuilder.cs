using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldGen.Core;

public static class SurvivalPrototypeBuilder
{
    public static void Validate()
    {
        var state=new SurvivalState();state.Validate();
        string path=Path.Combine(Application.streamingAssetsPath,"fauna-rigs");
        if(Directory.GetFiles(path,"*.json").Length!=177)throw new InvalidDataException("Expected 177 original ragdoll skeleton sidecars.");
        foreach(var recipe in SurvivalCrafting.Recipes)foreach(var material in recipe.Materials)SurvivalItems.Get(material.item);
        Debug.Log("SURVIVAL STATIC READY: "+SurvivalItems.All.Count()+" items, "+SurvivalCrafting.Recipes.Length+" recipes, "+SurvivalCrafting.Shelters.Length+" shelter/camp designs, 177 original skeletons.");
    }
    public static void Prepare()
    {HumanRuntimeBuilder.Build();Validate();AssetDatabase.SaveAssets();}
}

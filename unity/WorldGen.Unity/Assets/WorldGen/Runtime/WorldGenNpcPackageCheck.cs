using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public sealed class NpcPackageReceipt{public string AssemblySha256,CoreSha256,WorkerSha256,BuiltUtc;}
    static void RecordNpcCheckPackage(string output)
    {
        string Hash(string path){using(var file=File.OpenRead(path))using(var digest=SHA256.Create())return BitConverter.ToString(digest.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        var receipt=new NpcPackageReceipt{AssemblySha256=Hash(Path.Combine(Application.dataPath,"Managed/Assembly-CSharp.dll")),CoreSha256=Hash(Path.Combine(Application.dataPath,"Managed/WorldGen.Core.dll")),WorkerSha256=Hash(Path.Combine(root,"TerrainWorker/WorldGen.Cli.exe")),BuiltUtc=File.GetLastWriteTimeUtc(Path.Combine(root,"build-inputs.json")).ToString("O")};
        File.WriteAllText(Path.Combine(output,"package.json"),WorldJson.Write(receipt));
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

// Presentation assets only. A generated species keeps its original identity,
// ecological properties and pigments while reusing one prepared tree shape.
public sealed class LocalTreePrototype : ScriptableObject
{
    public const int BankVersion=2;
    public const float StandSpan=6.6f;
    public int Version=BankVersion,Code;
    public Mesh Near,Middle;
    public Texture2DArray Colour,Normal;
    public Vector3 Center,Extents;
    public float Radius;
    public Texture2DArray StandColour,StandNormal;
    public Vector3 StandCenter,StandExtents;
    public float StandRadius;
    public bool Valid=>Version==BankVersion&&Near!=null&&Middle!=null&&Colour!=null&&Normal!=null&&Radius>0;
    public bool StandValid=>Valid&&StandColour!=null&&StandNormal!=null&&StandRadius>0;
    public static string ResourcePath(int code)=>"TreePrototypesV1/"+code.ToString("X8");

    static readonly int[] codes=CreateCodes();
    static readonly Dictionary<int,int> mapping=new Dictionary<int,int>();
    static readonly TreeMorphology[] shapes=CreateShapes();
    static readonly Dictionary<int,LocalTreePrototype> loaded=new Dictionary<int,LocalTreePrototype>();
    public static IReadOnlyList<int> Codes=>codes;
    static int[] CreateCodes()
    {
        var result=new int[88];int at=0;
        // Fixed founder samples are an authored presentation palette, not a
        // lineage catalogue. Keep these codes stable when worlds are generated.
        for(int strategy=0;strategy<4;strategy++)for(int i=0;i<16;i++)
        {
            // Guarantee leaf-form coverage rather than hoping a short random
            // palette happens to contain every form. Mangroves use two forms.
            var leaf=(TreeLeafShape)(i%(strategy==3?2:4));int selected=-1;
            for(int sample=0;sample<128;sample++)
            {
                int code=(strategy<<22)|(((37+i*977+sample)%16384)<<8);
                if(new TreeMorphology(code).LeafShape==leaf){selected=code;break;}
            }
            if(selected<0)throw new InvalidOperationException("Prepared broadleaf palette has no "+leaf);
            result[at++]=selected;
        }
        for(int form=1;form<=5;form++)for(int i=0;i<(form==1?8:4);i++)
        {
            int selected=-1;
            for(int sample=0;sample<128;sample++)
            {
                int code=(form<<28)|(((73+i*2017+sample)%16384)<<8);
                if(form!=2||new TreeMorphology(code).LeafShape==(i%2==0?TreeLeafShape.PinnateFrond:TreeLeafShape.FanFrond)){selected=code;break;}
            }
            if(selected<0)throw new InvalidOperationException("Prepared frond palette is incomplete");
            result[at++]=selected;
        }
        return result;
    }
    static TreeMorphology[] CreateShapes()
    {
        var result=new TreeMorphology[codes.Length];
        for(int i=0;i<result.Length;i++){result[i]=new TreeMorphology(codes[i]);mapping.Add(codes[i],codes[i]);}
        return result;
    }
    public static int PrototypeCode(int sourceCode)
    {
        if(mapping.TryGetValue(sourceCode,out int found))return found;
        var source=new TreeMorphology(sourceCode);double best=double.PositiveInfinity;int selected=sourceCode;
        for(int i=0;i<codes.Length;i++)
        {
            var shape=shapes[i];if(shape.Form!=source.Form||shape.Strategy!=source.Strategy)continue;
            double score=(shape.LeafShape==source.LeafShape?0:16)+Math.Abs(shape.CrownBase-source.CrownBase)*4+
                Math.Abs(shape.CrownAspect-source.CrownAspect)*3+Math.Abs(shape.Openness-source.Openness)*2+
                Math.Abs(shape.BranchLift-source.BranchLift)+(shape.BarkPattern==source.BarkPattern?0:.1);
            if(score<best){best=score;selected=codes[i];}
        }
        if(double.IsPositiveInfinity(best))throw new InvalidOperationException("Unsupported tree growth form: "+source.Form);
        mapping.Add(sourceCode,selected);return selected;
    }
    public static LocalTreePrototype Load(int code)
    {
        if(loaded.TryGetValue(code,out var prototype))return prototype;
        prototype=Resources.Load<LocalTreePrototype>(ResourcePath(code));
        return Remember(code,prototype);
    }
    public static bool IsPrepared(int code)=>loaded.ContainsKey(code);
    public static IEnumerator Prepare(int code)
    {
        if(loaded.ContainsKey(code))yield break;
        var request=Resources.LoadAsync<LocalTreePrototype>(ResourcePath(code));
        // Forest projection has a manual frame-budget iterator, so poll here
        // rather than relying on Unity to interpret a yielded ResourceRequest.
        while(!request.isDone)yield return null;
        if(!loaded.ContainsKey(code))Remember(code,request.asset as LocalTreePrototype);
    }
    static LocalTreePrototype Remember(int code,LocalTreePrototype prototype)
    {
        if(prototype!=null&&(!prototype.Valid||prototype.Code!=code))throw new InvalidOperationException("Invalid prepared tree prototype "+code.ToString("X8"));
        loaded.Add(code,prototype);return prototype;
    }

    // The editor exporter uses the same mesh and billboard builders as runtime
    // fallback. No editor references or authoring work runs when loading assets.
    public sealed class ExportData
    {
        public Mesh Near,Middle;public Vector3 Center,Extents;public float Radius;
        public Color32[][][] Colours,Normals;
        public Vector3 StandCenter,StandExtents;public float StandRadius;
        public Color32[][][] StandColours,StandNormals;
    }
    public static ExportData PrepareExport(int code)
    {
        bool rebuilt=TerrainVideoOptions.TreeRebuild;TerrainVideoOptions.TreeRebuild=true;
        try
        {
            LocalTreeTextures.Create(out _,out _);var morphology=new TreeMorphology(code);
            var near=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,code);
            var middle=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,1,code);
            var result=LocalTreeImpostor.Bake(new LocalTreeImpostor.Source(near,(int)morphology.LeafShape,LocalTreeTextures.BarkLayer(morphology)),default);
            var prepared=LocalTreeTextures.Prepare(result.Colours,result.Normals,LocalTreeImpostor.Size);
            Mesh combined=null;
            try
            {
                combined=StandMesh(middle,code);
                var stand=LocalTreeImpostor.Bake(new LocalTreeImpostor.Source(combined,(int)morphology.LeafShape,LocalTreeTextures.BarkLayer(morphology)),default);
                var standPrepared=LocalTreeTextures.Prepare(stand.Colours,stand.Normals,LocalTreeImpostor.Size);
                return new ExportData{Near=near,Middle=middle,Center=result.Center,Extents=result.Extents,Radius=result.Radius,Colours=prepared.Colours,Normals=prepared.Normals,
                    StandCenter=stand.Center,StandExtents=stand.Extents,StandRadius=stand.Radius,StandColours=standPrepared.Colours,StandNormals=standPrepared.Normals};
            }
            finally{if(combined!=null)UnityEngine.Object.DestroyImmediate(combined);}
        }
        finally{TerrainVideoOptions.TreeRebuild=rebuilt;}
    }
    static Mesh StandMesh(Mesh tree,int code)
    {
        // A far packet represents a grove, not one enlarged tree. Only the
        // atlas survives export; the 36 source trees never become runtime
        // scene objects or additional per-frame draw records.
        var random=new Rng(code,"prepared-tree-stand-1");var parts=new CombineInstance[36];
        for(int z=0;z<6;z++)for(int x=0;x<6;x++)
        {
            float px=(float)(x-2.5+(random.Unit()-.5)*.9)/StandSpan;
            float pz=(float)(z-2.5+(random.Unit()-.5)*.9)/StandSpan;
            float cohort=(float)(.55+.45*random.Unit()),width=cohort*(float)(.93+.14*random.Unit());
            parts[z*6+x]=new CombineInstance{mesh=tree,transform=Matrix4x4.TRS(new Vector3(px,0,pz),Quaternion.Euler(0,(float)(random.Unit()*360),0),new Vector3(width/StandSpan,cohort,width/StandSpan))};
        }
        var mesh=new Mesh{name="Temporary prepared tree stand",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        try{mesh.CombineMeshes(parts,true,true,false);mesh.RecalculateBounds();return mesh;}
        catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
    }
}

using UnityEditor;

// Isolated import policy: no changes to existing terrain, tree or UI textures.
public sealed class GroundFoliageImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.EndsWith("/Resources/GroundFoliageAtlasV1.png"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;
        texture.alphaSource=TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency=true;
        texture.sRGBTexture=true;
        texture.mipmapEnabled=true;
        texture.mipMapsPreserveCoverage=true;
        texture.alphaTestReferenceValue=.35f;
        texture.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        texture.filterMode=UnityEngine.FilterMode.Trilinear;
        texture.anisoLevel=4;
        texture.maxTextureSize=2048;
        texture.textureCompression=TextureImporterCompression.CompressedHQ;
    }
}

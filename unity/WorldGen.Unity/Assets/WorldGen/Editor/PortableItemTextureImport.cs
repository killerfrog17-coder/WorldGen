using UnityEditor;
using UnityEngine;

public sealed class PortableItemTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool surfaces=assetPath.EndsWith("/PortableMaterialAtlasV1.png"),icons=assetPath.EndsWith("/PickupCategoryMasksV1.png");if(!surfaces&&!icons)return;
        var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.mipmapEnabled=surfaces;importer.sRGBTexture=surfaces;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
        importer.alphaSource=icons?TextureImporterAlphaSource.FromGrayScale:TextureImporterAlphaSource.None;importer.alphaIsTransparency=icons;
        importer.textureCompression=icons?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
    }
}

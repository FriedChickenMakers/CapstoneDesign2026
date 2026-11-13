using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Catalog exports are uGUI RawImage textures, so retain their exact PNG dimensions and alpha.
    // The narrow scope deliberately excludes existing Figma Main/Source importer metadata.
    public sealed class FocusMissionCatalogAssetImporter:AssetPostprocessor
    {
        static readonly Regex CatalogPng=new Regex(@"^Assets/Resources/FocusMissions/Catalog/(M(0[1-9]|1[0-7])|P(0[1-9]|1[0-9]|2[0-8]))\.png$",RegexOptions.CultureInvariant);
        void OnPreprocessTexture()
        {
            if(!CatalogPng.IsMatch(assetPath))return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Default;
            texture.alphaSource=TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency=true;
            texture.mipmapEnabled=false;
            texture.wrapMode=TextureWrapMode.Clamp;
            texture.filterMode=FilterMode.Bilinear;
            texture.npotScale=TextureImporterNPOTScale.None;
            texture.textureCompression=TextureImporterCompression.Uncompressed;
            texture.maxTextureSize=2048;
            texture.isReadable=false;
            texture.sRGBTexture=true;
        }
    }
}

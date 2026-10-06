using UnityEditor;

namespace Shadowfall.EditorTools
{
    /// <summary>
    /// Import settings for the UI art in Assets/Resources/UI (crisp, uncompressed, no mipmaps) and the
    /// ground textures in Assets/Resources/Ground (tiling, mipmapped; the water ripple map is linear data).
    /// </summary>
    public class ShadowfallImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.Contains("/Resources/Ground/"))
            {
                var ground = (TextureImporter)assetImporter;
                ground.textureType = TextureImporterType.Default;
                ground.sRGBTexture = !path.EndsWith("water_normal.png");
                ground.alphaSource = TextureImporterAlphaSource.FromInput; // alpha = height for blending
                ground.alphaIsTransparency = false;
                ground.mipmapEnabled = true;
                ground.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                ground.filterMode = UnityEngine.FilterMode.Trilinear;
                ground.anisoLevel = 4;
                ground.maxTextureSize = 512;
                return;
            }
            if (!path.Contains("/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            bool cursor = path.Contains("/Cursors/");
            importer.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.GUI;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = cursor ? UnityEngine.FilterMode.Point : UnityEngine.FilterMode.Bilinear;
        }
    }
}

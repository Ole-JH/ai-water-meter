using UnityEditor;

namespace Shadowfall.EditorTools
{
    /// <summary>Import settings for the UI art in Assets/Resources/UI (crisp, uncompressed, no mipmaps).</summary>
    public class ShadowfallImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
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

using UnityEditor;

namespace Shadowfall.EditorTools
{
    /// <summary>
    /// Import settings for the UI art in Assets/Resources/UI (crisp, uncompressed, no mipmaps) and the
    /// ground textures in Assets/Resources/Ground (tiling, mipmapped; the water ripple map is linear data), and the
    /// music in Assets/Resources/Music (streamed, loaded only when played).
    /// </summary>
    public class ShadowfallImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            var s = importer.defaultSampleSettings;
            s.loadType = UnityEngine.AudioClipLoadType.Streaming;
            s.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
            s.quality = 0.45f;
            s.preloadAudioData = false; // the Music player loads one track at a time
            importer.defaultSampleSettings = s;
        }

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

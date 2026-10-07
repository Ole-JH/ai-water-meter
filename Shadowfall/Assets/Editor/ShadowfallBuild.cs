using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shadowfall.EditorTools
{
    /// <summary>
    /// Menu: Shadowfall > Build WebGL. Produces a browser build in ../server/public, which the
    /// Docker container serves. Also prepares the scene and assets the build needs.
    /// Headless: Unity -batchmode -quit -projectPath . -executeMethod Shadowfall.EditorTools.ShadowfallBuild.BuildWebGL
    /// </summary>
    public static class ShadowfallBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string GlowMaterialPath = "Assets/Resources/ShadowfallVariants.mat";

        [MenuItem("Shadowfall/Build WebGL (into server folder)")]
        public static void BuildWebGL()
        {
            PrepareProject();

            // SF_BUILD_OUT (relative to the project folder) builds somewhere else: auto-deploy builds into server/public-next,
            // checks it in a browser, and only then swaps it into server/public, which the live server serves.
            string outEnv = System.Environment.GetEnvironmentVariable("SF_BUILD_OUT");
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", string.IsNullOrEmpty(outEnv) ? Path.Combine("server", "public") : outEnv));
            Directory.CreateDirectory(outDir);
            // Start from a clean Build folder so stale files from older builds never get served.
            string buildDir = Path.Combine(outDir, "Build");
            if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true);

            PlayerSettings.productName = "Shadowfall";
            PlayerSettings.companyName = "Shadowfall";
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; // server.js sends Content-Encoding headers
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "PROJECT:Shadowfall";
            PlayerSettings.WebGL.nameFilesAsHashes = true;  // new file names per build: browsers can't mix old and new
            // The build stamp: shown on the login screen and sent to the server, which uses it to tell newer games
            // (their map replaces the stored one) from stale browser caches (told to reload). Sorts by time.
            string build = System.DateTime.UtcNow.ToString("yyyy.MM.dd-HHmmss");
            PlayerSettings.bundleVersion = build;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                // The server reads this to know which build it serves (stale clients reload before logging in).
                File.WriteAllText(Path.Combine(outDir, "build.json"), "{\"build\": \"" + build + "\"}\n");
                Debug.Log("Shadowfall WebGL build written to " + outDir + ". Start (or restart) the server: task up");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(outDir);
            }
            else
            {
                Debug.LogError("Shadowfall WebGL build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Shadowfall/Open Main Scene")]
        public static void PrepareProject()
        {
            MakeModelTexturesReadable();

            // Material with emission enabled so the emission shader variant survives build stripping.
            if (AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath) == null)
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var mat = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                Object.DestroyImmediate(tmp);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white);
                AssetDatabase.CreateAsset(mat, GlowMaterialPath);
            }

            if (!File.Exists(Path.Combine(Application.dataPath, "Scenes", "Main.unity")))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }

            // Fog is set at runtime; enabling it in the scene keeps the fog shader variants in the build.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 95f;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Import settings every model needs:
        /// - Legacy animation. glTFast's editor importer defaults new assets to Mecanim, which adds an
        ///   Animator without a controller and no Animation component, so nothing would ever animate.
        /// - Readable textures: in headless builds (-nographics) there is no GPU, and GPU-only textures
        ///   would be saved without pixels.
        /// </summary>
        // Bump to force one re-import of every model (e.g. after fixing something that broke their textures).
        const string ArtImportVersion = "3-legacy-animation";
        const int LegacyAnimation = 1; // GLTFast.AnimationMethod.Legacy

        static void MakeModelTexturesReadable()
        {
            string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "ShadowfallArtImport.txt"));
            bool forceAll = !File.Exists(marker) || File.ReadAllText(marker).Trim() != ArtImportVersion;
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Resources/Art" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".glb") && !path.EndsWith(".gltf")) continue;
                    var importer = AssetImporter.GetAtPath(path);
                    if (importer == null) continue;
                    var so = new SerializedObject(importer);
                    var readable = so.FindProperty("importSettings.texturesReadable");
                    var animation = so.FindProperty("importSettings.animationMethod");
                    bool needsReadable = readable != null && !readable.boolValue;
                    bool needsLegacy = animation != null && animation.intValue != LegacyAnimation;
                    if (!needsReadable && !needsLegacy && !forceAll) continue;
                    if (needsReadable) readable.boolValue = true;
                    if (needsLegacy) animation.intValue = LegacyAnimation;
                    if (needsReadable || needsLegacy) so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            File.WriteAllText(marker, ArtImportVersion);
            if (changed > 0) Debug.Log("[Shadowfall] Re-imported " + changed + " models (legacy animation, readable textures).");
        }
    }
}

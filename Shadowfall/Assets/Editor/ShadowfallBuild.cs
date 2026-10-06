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

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "server", "public"));
            Directory.CreateDirectory(outDir);

            PlayerSettings.productName = "Shadowfall";
            PlayerSettings.companyName = "Shadowfall";
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; // server.js sends Content-Encoding headers
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "PROJECT:Shadowfall";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
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
    }
}

using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Grades the rendered frame toward a darker, grittier palette (the art packs are bright and
    /// cartoony on their own). Gets colder and bleaker at night. Does nothing if the shader is missing.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ColorGrade : MonoBehaviour
    {
        Material mat;
        public static ColorGrade I;

        /// <summary>
        /// On only while grading is wanted: a camera with OnRenderImage renders to a texture and copies it to the
        /// screen even when the effect does nothing, a whole extra full-screen pass (GameSettings.Apply calls this).
        /// </summary>
        public static void Refresh()
        {
            if (I != null) I.enabled = I.mat != null && GameSettings.ColorGrading;
        }

        /// <summary>0 normal .. 1 drained of colour (death); the frame eases toward it.</summary>
        public static float Mood;
        static float shown;
        static readonly int Saturation = Shader.PropertyToID("_Saturation"), Contrast = Shader.PropertyToID("_Contrast"),
            Exposure = Shader.PropertyToID("_Exposure"), Vignette = Shader.PropertyToID("_Vignette"),
            ShadowTint = Shader.PropertyToID("_ShadowTint"), HighlightTint = Shader.PropertyToID("_HighlightTint");

        void Awake()
        {
            var shader = Resources.Load<Shader>("Shaders/ShadowfallGrade");
            if (shader == null) shader = Shader.Find("Hidden/Shadowfall/Grade");
            if (shader != null && shader.isSupported) mat = new Material(shader) { name = "ColorGrade" };
            I = this;
            Refresh();
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null || !GameSettings.ColorGrading) { Graphics.Blit(src, dst); return; }
            float n = DayNight.Night;
            shown = Mathf.MoveTowards(shown, Mood, Time.unscaledDeltaTime / (Mood > shown ? 1.6f : 0.8f));
            mat.SetFloat(Saturation, Mathf.Lerp(0.68f, 0.55f, n) * (1f - 0.88f * shown));
            mat.SetFloat(Contrast, Mathf.Lerp(1.14f, 1.08f, n) + 0.1f * shown);
            mat.SetFloat(Exposure, Mathf.Lerp(0.97f, 1.0f, n) * (1f - 0.18f * shown));
            mat.SetFloat(Vignette, Mathf.Lerp(0.45f, 0.6f, n) + 0.35f * shown);
            mat.SetColor(ShadowTint, Color.Lerp(new Color(0.88f, 0.92f, 1.02f), new Color(0.8f, 0.88f, 1.1f), n));
            mat.SetColor(HighlightTint, Color.Lerp(new Color(1.06f, 1.0f, 0.9f), new Color(0.98f, 0.98f, 1.02f), n));
            Graphics.Blit(src, dst, mat);
        }
    }
}

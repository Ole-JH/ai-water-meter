using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The hero's avatar for the HUD portrait and the character window: a copy of the hero's model wearing the
    /// current loadout (weapon in hand, helm on or off), posed in a small lit "photo studio" far below the world
    /// and rendered by its own camera into a transparent texture. Only that camera sees the studio's layer.
    /// </summary>
    public class Avatar : MonoBehaviour
    {
        public const int Layer = 30;
        const int TexW = 320, TexH = 480;
        static readonly Vector3 StudioPos = new Vector3(-400f, -200f, -400f);

        static Avatar I;
        Camera cam;
        RenderTexture rt;
        CharacterView view;
        string look, weapon;
        bool helm, built;
        float turn;

        /// <summary>The rendered avatar (full body, transparent background), or null when no hero is in the world.</summary>
        public static Texture Texture => I != null && I.view != null && I.rt != null ? I.rt : null;

        /// <summary>Texture coordinates of the head-and-shoulders crop (for the small portrait).</summary>
        public static readonly Rect HeadCrop = new Rect(0.18f, 0.6f, 0.64f, 0.38f);

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("AvatarStudio");
            go.transform.position = StudioPos;
            I = go.AddComponent<Avatar>();
            I.Setup();
        }

        void Setup()
        {
            rt = new RenderTexture(TexW, TexH, 16, RenderTextureFormat.ARGB32) { name = "Avatar", antiAliasing = 1 };
            var camGo = new GameObject("AvatarCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 6.2f);
            camGo.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            cam = camGo.AddComponent<Camera>();
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 21f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 20f;
            cam.targetTexture = rt;
            cam.depth = -10;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            camGo.AddComponent<NoFog>();

            // Keep the studio out of the game camera.
            var main = GameManager.I != null ? GameManager.I.Cam : Camera.main;
            if (main != null) main.cullingMask &= ~(1 << Layer);

            Light L(Vector3 local, Color c, float intensity, LightType type = LightType.Point)
            {
                var l = new GameObject("AvatarLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.localPosition = local;
                l.type = type;
                l.color = c;
                l.intensity = intensity;
                l.range = 9f;
                l.cullingMask = 1 << Layer;
                l.shadows = LightShadows.None;
                return l;
            }
            L(new Vector3(-1.8f, 2.6f, 3f), new Color(1f, 0.85f, 0.65f), 2.2f);   // warm key
            L(new Vector3(2f, 2.2f, 2.4f), new Color(0.6f, 0.7f, 1f), 1.2f);     // cool fill
            L(new Vector3(0f, 2.8f, -2.2f), new Color(1f, 0.75f, 0.45f), 1.8f);  // rim from behind
        }

        void Update()
        {
            var p = Player.I;
            cam.enabled = p != null && view != null;
            if (p == null) return;
            string w = p.WeaponKind ?? "";
            bool h = p.Inventory.GetEquipped(EquipSlot.Helm) != null;
            if (!built || look != p.Look)
            {
                if (view != null) Destroy(view.Root);
                look = p.Look;
                view = CharacterView.Create(transform, CharacterLook.ForHero(look));
                built = true;
                weapon = null;
            }
            if (view == null) return;
            if (w != weapon || h != helm)
            {
                weapon = w;
                helm = h;
                view.Equip(weapon, helm);
                SetLayer(view.Root.transform);
            }
            view.UpdateLocomotion(0f);
            // A slow, gentle sway so the figure feels alive.
            turn += Time.unscaledDeltaTime;
            view.Root.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(turn * 0.6f) * 14f, 0f);
        }

        /// <summary>Dungeons use close fog; the studio camera renders without it.</summary>
        internal class NoFog : MonoBehaviour
        {
            bool fog;
            void OnPreRender() { fog = RenderSettings.fog; RenderSettings.fog = false; }
            void OnPostRender() { RenderSettings.fog = fog; }
        }

        internal static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
            if (I == this) I = null;
        }
    }
}

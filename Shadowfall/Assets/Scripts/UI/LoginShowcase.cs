using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The login screen's live hero preview: the selected hero model stands in the village square, lit by a
    /// warm key light and a cool rim light, while <see cref="CameraRig"/> frames it in a slow cinematic shot.
    /// </summary>
    public class LoginShowcase : MonoBehaviour
    {
        public static readonly Vector3 Spot = new Vector3(144.5f, 0f, 140.5f);

        /// <summary>Where the camera should look (null when no preview is shown).</summary>
        public static Vector3? Focus => I != null && I.view != null ? Spot : (Vector3?)null;
        /// <summary>Horizontal screen position of the hero, -1 (left edge) .. 1 (right edge).</summary>
        public static float ScreenOffset { get; private set; }

        static LoginShowcase I;
        CharacterView view;
        string look;
        ParticleSystem motes;

        static readonly System.Collections.Generic.Dictionary<string, string> weapons = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Knight", "sword" }, { "Barbarian", "axe" }, { "Mage", "staff" }, { "Rogue", "dagger" },
        };

        /// <summary>Call every frame while the login screen is shown.</summary>
        public static void Ensure(string heroLook, float screenOffset)
        {
            if (I == null)
            {
                var go = new GameObject("LoginShowcase");
                go.transform.position = Spot;
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // facing the camera (which looks north)
                I = go.AddComponent<LoginShowcase>();
                I.AddLights();
            }
            ScreenOffset = screenOffset;
            if (I.look != heroLook) I.Show(heroLook);
        }

        /// <summary>Removes the preview (when entering the world).</summary>
        public static void Hide()
        {
            if (I != null) Destroy(I.gameObject);
            I = null;
        }

        void Show(string heroLook)
        {
            bool first = look == null;
            look = heroLook;
            if (view != null) Destroy(view.Root);
            view = CharacterView.Create(transform, CharacterLook.ForHero(heroLook));
            if (view == null) return;
            weapons.TryGetValue(heroLook, out var w);
            view.Equip(w, true);
            if (!first)
            {
                view.Cheer();
                SpellFx.Ring(transform.position, UISkin.Gold, 1.6f, 0.6f);
                Sfx.Play2D("equip", 0.5f);
            }
        }

        void AddLights()
        {
            Light Add(Vector3 local, Color c, float range, float intensity)
            {
                var l = new GameObject("ShowcaseLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.localPosition = local;
                l.type = LightType.Point;
                l.color = c;
                l.range = range;
                l.intensity = intensity;
                l.shadows = LightShadows.None;
                return l;
            }
            // local +Z is toward the camera (the hero faces it)
            Add(new Vector3(-1.4f, 2.4f, 2.2f), new Color(1f, 0.78f, 0.5f), 7f, 1.7f);  // warm key
            Add(new Vector3(1.6f, 2.6f, -1.6f), new Color(0.55f, 0.7f, 1f), 6f, 1.4f);   // cool rim
            motes = SpellFx.Emit(new SpellFx.P
            {
                Rate = 5, Duration = 100000f, Life = new Vector2(3f, 5f), Speed = new Vector2(0.05f, 0.2f),
                Size = new Vector2(0.03f, 0.06f), Start = new Color(1f, 0.8f, 0.45f, 0.8f), End = new Color(1f, 0.5f, 0.2f, 0f),
                Shape = ParticleSystemShapeType.Circle, Radius = 2.2f, Velocity = new Vector3(0f, 0.35f, 0f),
            }, transform.position + Vector3.up * 0.2f, transform);
        }

        void Update()
        {
            view?.UpdateLocomotion(0f);
        }
    }
}

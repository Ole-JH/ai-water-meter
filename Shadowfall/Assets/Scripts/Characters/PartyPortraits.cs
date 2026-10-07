using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Live portraits of the other party members for the party frames: each member's hero model with their weapon and
    /// helm, in their own lit booth of the avatar studio (far below the world, on <see cref="Avatar.Layer"/>), photographed
    /// a few times a second into a small texture. Framed like the hero's own avatar, so <see cref="Avatar.HeadCrop"/> fits.
    /// Booths come and go with the party; works for members far away too (it only needs the party message).
    /// </summary>
    public class PartyPortraits : MonoBehaviour
    {
        const int TexW = 160, TexH = 240;
        const float Spacing = 40f, RenderEvery = 0.12f;
        static readonly Vector3 StudioPos = new Vector3(-400f, -260f, -400f);

        class Booth
        {
            public Transform Root;
            public Camera Cam;
            public RenderTexture Rt;
            public CharacterView View;
            public string Model, Weapon;
            public bool Helm, Dead, Built;
            public float Phase;
        }

        static PartyPortraits I;
        readonly Dictionary<int, Booth> booths = new Dictionary<int, Booth>();
        readonly List<int> gone = new List<int>();
        float nextRender;

        /// <summary>The portrait of party member <paramref name="id"/> (full body; crop with <see cref="Avatar.HeadCrop"/>), or null.</summary>
        public static Texture For(int id)
        {
            if (I == null)
            {
                var go = new GameObject("PartyPortraits");
                go.transform.position = StudioPos;
                I = go.AddComponent<PartyPortraits>();
            }
            return I.booths.TryGetValue(id, out var b) && b.View != null ? b.Rt : null;
        }

        void Update()
        {
            var net = NetClient.I;
            var party = net != null && Player.I != null ? net.Party : new NetPartyMember[0];

            gone.Clear();
            foreach (var id in booths.Keys) gone.Add(id);
            for (int i = 0; i < party.Length; i++)
            {
                var m = party[i];
                if (m.id == net.MyId) continue;
                gone.Remove(m.id);
                if (!booths.TryGetValue(m.id, out var b)) booths[m.id] = b = NewBooth();
                Dress(b, m);
            }
            foreach (var id in gone) { Free(booths[id]); booths.Remove(id); }

            // Idle animation and a gentle sway every frame; the photos only a few times a second.
            float t = Time.unscaledTime;
            foreach (var b in booths.Values)
            {
                if (b.View == null) continue;
                if (!b.Dead) b.View.UpdateLocomotion(0f);
                b.View.Root.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.6f + b.Phase) * 14f, 0f);
            }
            if (Time.unscaledTime < nextRender) return;
            nextRender = Time.unscaledTime + RenderEvery;
            foreach (var b in booths.Values) if (b.View != null) b.Cam.Render();
        }

        /// <summary>A booth in the first free spot along the studio row.</summary>
        Booth NewBooth()
        {
            int slot = 0;
            while (SlotTaken(slot)) slot++;
            var root = new GameObject("Booth" + slot).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(slot * Spacing, 0f, 0f);

            var b = new Booth { Root = root, Phase = slot * 1.7f };
            b.Rt = new RenderTexture(TexW, TexH, 16, RenderTextureFormat.ARGB32) { name = "PartyPortrait", antiAliasing = 1 };
            // Same framing as the hero's avatar (Avatar.Setup), so the same head crop works.
            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(root, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 6.2f);
            camGo.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            b.Cam = camGo.AddComponent<Camera>();
            b.Cam.cullingMask = 1 << Avatar.Layer;
            b.Cam.clearFlags = CameraClearFlags.SolidColor;
            b.Cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            b.Cam.fieldOfView = 21f;
            b.Cam.nearClipPlane = 1f;
            b.Cam.farClipPlane = 12f; // booths are 40 apart: each camera sees only its own sitter
            b.Cam.targetTexture = b.Rt;
            b.Cam.allowHDR = false;
            b.Cam.allowMSAA = false;
            b.Cam.enabled = false;    // rendered by hand, a few times a second
            camGo.AddComponent<Avatar.NoFog>();

            Light(root, new Vector3(-1.8f, 2.6f, 3f), new Color(1f, 0.85f, 0.65f), 2.2f);   // warm key
            Light(root, new Vector3(2f, 2.2f, 2.4f), new Color(0.6f, 0.7f, 1f), 1.2f);     // cool fill
            Light(root, new Vector3(0f, 2.8f, -2.2f), new Color(1f, 0.75f, 0.45f), 1.8f);  // rim from behind
            return b;
        }

        bool SlotTaken(int slot)
        {
            foreach (var b in booths.Values)
                if (Mathf.RoundToInt(b.Root.localPosition.x / Spacing) == slot) return true;
            return false;
        }

        static void Light(Transform parent, Vector3 local, Color c, float intensity)
        {
            var l = new GameObject("PortraitLight").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = local;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = 9f;
            l.cullingMask = 1 << Avatar.Layer;
            l.shadows = LightShadows.None;
        }

        /// <summary>Puts the member's class model in the booth, with their weapon and helm.</summary>
        static void Dress(Booth b, NetPartyMember m)
        {
            string model = string.IsNullOrEmpty(m.mdl) ? CharacterLook.HeroModels[0] : m.mdl;
            string weapon = m.wk ?? "";
            bool helm = !string.IsNullOrEmpty(m.helm);
            if (!b.Built || b.Model != model)
            {
                if (b.View != null) Destroy(b.View.Root);
                b.Model = model;
                b.View = CharacterView.Create(b.Root, CharacterLook.ForHero(model));
                b.Built = true;
                b.Weapon = null;
            }
            if (b.View == null) return;
            if (weapon != b.Weapon || helm != b.Helm)
            {
                b.Weapon = weapon;
                b.Helm = helm;
                b.View.Equip(weapon, helm);
                Avatar.SetLayer(b.View.Root.transform);
            }
            b.Dead = m.dead; // stands still (the frame greys it out): a death pose would fall out of the head crop
        }

        static void Free(Booth b)
        {
            if (b.Rt != null) b.Rt.Release();
            if (b.Root != null) Destroy(b.Root.gameObject);
        }

        void OnDestroy()
        {
            foreach (var b in booths.Values) if (b.Rt != null) b.Rt.Release();
            if (I == this) I = null;
        }
    }
}

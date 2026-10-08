using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The browser check's photo tour (GameCheck, after the walk): every monster, boss, NPC, hero and mount lined up in
    /// rows of five under studio light, named in a caption, and each walled town from above. The pictures land with the
    /// check's screenshots (.autodeploy/check/) and go to Discord after a deploy, so the models can be judged from real
    /// renders. Only ever runs in the check.
    /// </summary>
    public class PhotoTour : MonoBehaviour
    {
        const int PerRow = 5;
        static readonly Vector3 Studio = new Vector3(288f, 0f, -60f); // south of the map: no ground, nothing in the way

        string caption = "";
        GUIStyle style;

        /// <summary>Takes the photos; <paramref name="shot"/> asks the browser for a screenshot and waits for it.</summary>
        public IEnumerator Run(System.Func<string, IEnumerator> shot)
        {
            var cam = GameManager.I.Cam;
            var rig = CameraRig.I;
            var day = DayNight.I;
            if (rig != null) rig.enabled = false;
            if (day != null) day.enabled = false;
            bool fog = RenderSettings.fog;
            var ambient = (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
            var bg = cam.backgroundColor;
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.64f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.22f);
            cam.backgroundColor = new Color(0.22f, 0.24f, 0.28f);
            var key = new GameObject("PhotoKeyLight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.transform.rotation = Quaternion.Euler(38f, 25f, 0f);

            // Characters, in rows of five
            var groups = new List<(string title, List<(string name, CharacterLook look)> items)>();
            void Group(string title, IEnumerable<(string, CharacterLook)> all)
            {
                var row = new List<(string, CharacterLook)>();
                int n = 1;
                foreach (var it in all)
                {
                    if (it.Item2 == null) continue;
                    row.Add(it);
                    if (row.Count == PerRow) { groups.Add((title + " " + n++, row)); row = new List<(string, CharacterLook)>(); }
                }
                if (row.Count > 0) groups.Add((title + " " + n, row));
            }
            var monsters = new List<(string, CharacterLook)>();
            foreach (var m in CharacterLook.MonsterNames) monsters.Add((m, CharacterLook.ForMonster(m)));
            Group("monsters", monsters);
            var npcs = new List<(string, CharacterLook)>();
            foreach (var n in CharacterLook.NpcNames) npcs.Add((n, CharacterLook.ForNpc(n)));
            Group("npcs", npcs);
            var heroes = new List<(string, CharacterLook)>();
            foreach (var h in CharacterLook.HeroModels) heroes.Add((h, CharacterLook.ForHero(h)));
            foreach (var md in MountDef.All) heroes.Add((md.Name, md.Look));
            Group("heroes-mounts", heroes);

            foreach (var (title, items) in groups)
            {
                var stage = new GameObject("PhotoStage").transform;
                stage.position = Studio;
                float x = 0f, maxH = 1f;
                var names = new List<string>();
                foreach (var (name, look) in items)
                {
                    float w = Mathf.Max(2.6f, look.Height * 0.9f);
                    var slot = new GameObject(name).transform;
                    slot.SetParent(stage, false);
                    slot.localPosition = new Vector3(x + w / 2f, 0f, 0f);
                    slot.localRotation = Quaternion.Euler(0f, 160f, 0f); // facing the camera, a little turned
                    var view = CharacterView.Create(slot, look);
                    if (view == null) names.Add(name + " (MISSING MODEL)");
                    else names.Add(name);
                    x += w;
                    maxH = Mathf.Max(maxH, look.Height);
                }
                foreach (Transform c in stage) c.localPosition -= new Vector3(x / 2f, 0f, 0f);
                float dist = Mathf.Max(x * 0.95f, maxH * 2f) + 2f;
                cam.transform.position = Studio + new Vector3(0f, maxH * 0.75f, -dist);
                cam.transform.LookAt(Studio + new Vector3(0f, maxH * 0.42f, 0f));
                caption = title + ":   " + string.Join("   |   ", names) + "   (left to right)";
                yield return shot("models-" + title);
                Destroy(stage.gameObject);
            }

            // The walled towns from above (in daylight)
            foreach (var t in WorldGenerator.Towns)
            {
                if (!t.Walled) continue;
                float size = Mathf.Max(t.Rect.width, t.Rect.height);
                cam.transform.position = t.Center + new Vector3(0f, size * 0.85f, -size * 0.75f);
                cam.transform.LookAt(t.Center + new Vector3(0f, 0f, size * 0.05f));
                caption = t.Name + " from the south";
                yield return shot("town-" + t.Name.Split(' ')[0].ToLowerInvariant());
            }
            // Hollowmere's square up close
            var sq = WorldGenerator.Towns[0].Center;
            cam.transform.position = sq + new Vector3(-14f, 12f, -14f);
            cam.transform.LookAt(sq);
            caption = "Hollowmere's square from the south-west";
            yield return shot("town-square");

            caption = "";
            Destroy(key.gameObject);
            RenderSettings.fog = fog;
            (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor) = ambient;
            cam.backgroundColor = bg;
            if (day != null) day.enabled = true;
            if (rig != null) { rig.enabled = true; rig.SnapToTarget(); }
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(caption)) return;
            GUI.depth = -100; // over the HUD
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.UpperCenter, richText = false };
            var r = new Rect(20, 16, Screen.width - 40, 80);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 8, Screen.width, 60), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, caption, style);
        }
    }
}

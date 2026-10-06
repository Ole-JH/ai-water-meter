using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>Another player in the world, driven by server snapshots.</summary>
    public class RemotePlayer : MonoBehaviour
    {
        public static readonly Dictionary<int, RemotePlayer> ById = new Dictionary<int, RemotePlayer>();

        public int Id;
        public string Name;
        public int Level;
        public float Health, MaxHealth;
        public bool Dead;
        public float LastSeen;

        Vector3 netPos;
        float netRy, attackAnim = -1f, speed01;
        bool moving, wasAttacking;
        string appearance;
        HumanoidModel model;

        public static RemotePlayer Get(NetPlayer p)
        {
            if (ById.TryGetValue(p.id, out var rp) && rp != null) return rp;
            var go = new GameObject("Player " + p.name);
            go.transform.position = new Vector3(p.x, 0, p.z);
            rp = go.AddComponent<RemotePlayer>();
            rp.Id = p.id;
            rp.netPos = go.transform.position;
            rp.model = HumanoidModel.Build(go.transform, 1f, new Color(0.95f, 0.78f, 0.62f), new Color(0.5f, 0.4f, 0.3f),
                new Color(0.3f, 0.25f, 0.2f), new Color(0.75f, 0.75f, 0.8f));
            ById[p.id] = rp;
            return rp;
        }

        public void Apply(NetPlayer p)
        {
            LastSeen = Time.time;
            Name = p.name;
            Level = p.lvl;
            Health = p.hp;
            MaxHealth = Mathf.Max(1f, p.mhp);
            Dead = p.dead;
            moving = p.mv;
            netPos = new Vector3(p.x, 0, p.z);
            netRy = p.ry;
            if (p.atk && !wasAttacking) attackAnim = 0f;
            wasAttacking = p.atk;

            string key = p.body + p.legs + p.weapon + p.helm;
            if (key != appearance)
            {
                appearance = key;
                model.BodyRenderer.sharedMaterial = Mat.Get(Parse(p.body, new Color(0.5f, 0.4f, 0.3f)));
                model.ArmRendererL.sharedMaterial = model.ArmRendererR.sharedMaterial = model.BodyRenderer.sharedMaterial;
                model.LegRendererL.sharedMaterial = model.LegRendererR.sharedMaterial = Mat.Get(Parse(p.legs, new Color(0.3f, 0.25f, 0.2f)));
                if (model.WeaponRenderer != null)
                {
                    model.WeaponRenderer.gameObject.SetActive(!string.IsNullOrEmpty(p.weapon));
                    model.WeaponRenderer.sharedMaterial = Mat.Get(Parse(p.weapon, Color.gray));
                }
                model.Helm.gameObject.SetActive(!string.IsNullOrEmpty(p.helm));
                if (!string.IsNullOrEmpty(p.helm)) model.HelmRenderer.sharedMaterial = Mat.Get(Parse(p.helm, Color.gray));
            }
        }

        static Color Parse(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time - LastSeen > 3f) { Destroy(gameObject); return; }

            Vector3 to = netPos - transform.position;
            if (to.magnitude > 8f) transform.position = netPos;
            else transform.position = Vector3.MoveTowards(transform.position, netPos, Mathf.Max(to.magnitude * 8f, 3f) * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netRy, 0), dt * 12f);

            if (Dead)
            {
                model.Root.localRotation = Quaternion.Slerp(model.Root.localRotation, Quaternion.Euler(-90, 0, 0), dt * 5f);
                return;
            }
            model.Root.localRotation = Quaternion.identity;

            speed01 = Mathf.Lerp(speed01, moving ? 1f : 0f, dt * 10f);
            float atk = -1f;
            if (attackAnim >= 0f)
            {
                attackAnim += dt * 3f;
                atk = attackAnim;
                if (attackAnim >= 1f) attackAnim = -1f;
            }
            model.Animate(speed01, atk, dt);
        }

        void OnDestroy()
        {
            if (ById.TryGetValue(Id, out var rp) && rp == this) ById.Remove(Id);
        }
    }
}

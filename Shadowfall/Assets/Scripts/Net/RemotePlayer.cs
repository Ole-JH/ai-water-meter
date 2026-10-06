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
        float netRy, attackAnim = -1f, speed01, moveSpeed;
        bool moving, wasAttacking, wasDead;
        string appearance, modelName;
        HumanoidModel model;       // primitive fallback
        CharacterView view;        // animated model

        public static RemotePlayer Get(NetPlayer p)
        {
            if (ById.TryGetValue(p.id, out var rp) && rp != null) return rp;
            var go = new GameObject("Player " + p.name);
            go.transform.position = new Vector3(p.x, 0, p.z);
            rp = go.AddComponent<RemotePlayer>();
            rp.Id = p.id;
            rp.netPos = go.transform.position;
            ById[p.id] = rp;
            return rp;
        }

        void BuildModel(string mdl)
        {
            modelName = mdl;
            appearance = null;
            if (view != null) Destroy(view.Root);
            if (model != null) Destroy(model.Root.gameObject);
            view = CharacterView.Create(transform, CharacterLook.ForHero(mdl));
            model = view == null
                ? HumanoidModel.Build(transform, 1f, new Color(0.95f, 0.78f, 0.62f), new Color(0.5f, 0.4f, 0.3f),
                    new Color(0.3f, 0.25f, 0.2f), new Color(0.75f, 0.75f, 0.8f))
                : null;
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

            string mdl = string.IsNullOrEmpty(p.mdl) ? CharacterLook.HeroModels[0] : p.mdl;
            if (mdl != modelName) BuildModel(mdl);

            if (p.atk && !wasAttacking)
            {
                attackAnim = 0f;
                view?.Attack(0.6f);
            }
            wasAttacking = p.atk;

            if (Dead && !wasDead) view?.Die();
            if (!Dead && wasDead) view?.Revive();
            wasDead = Dead;

            if (model == null) return;
            string key = p.body + p.legs + p.weapon + p.helm;
            if (key == appearance) return;
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

        static Color Parse(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time - LastSeen > 3f) { Destroy(gameObject); return; }

            Vector3 before = transform.position;
            Vector3 to = netPos - transform.position;
            if (to.magnitude > 8f) transform.position = netPos;
            else transform.position = Vector3.MoveTowards(transform.position, netPos, Mathf.Max(to.magnitude * 8f, 3f) * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netRy, 0), dt * 12f);
            float moved = Factory.FlatDistance(before, transform.position);
            if (dt > 0f && moved < 3f) moveSpeed = Mathf.Lerp(moveSpeed, moving ? moved / dt : 0f, dt * 10f);

            if (view != null)
            {
                view.UpdateLocomotion(moveSpeed);
                return;
            }
            if (model == null) return;

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

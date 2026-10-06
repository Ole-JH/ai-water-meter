using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>Animation clip names for one family of rigged models.</summary>
    public class AnimSet
    {
        public string Idle, Walk, Run, Hit, Death, DeathPose, Cast, Shoot, Interact, Cheer, Spawn;
        public string[] Attacks;

        public static readonly AnimSet KayKit = new AnimSet
        {
            Idle = "Idle", Walk = "Walking_A", Run = "Running_A", Hit = "Hit_A", Death = "Death_A", DeathPose = "Death_A_Pose",
            Cast = "Spellcast_Shoot", Shoot = "1H_Ranged_Shoot", Interact = "Interact", Cheer = "Cheer",
            Attacks = new[] { "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal" }
        };

        public static readonly AnimSet Skeleton = new AnimSet
        {
            Idle = "Idle", Walk = "Walking_A", Run = "Running_A", Hit = "Hit_A", Death = "Death_A", DeathPose = "Death_A_Pose",
            Cast = "Spellcast_Shoot", Shoot = "1H_Ranged_Shoot", Interact = "Spellcast_Summon", Spawn = "Skeletons_Awaken_Standing",
            Attacks = new[] { "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal" }
        };

        public static readonly AnimSet Blob = new AnimSet
        {
            Idle = "Idle", Walk = "Walk", Run = "Walk", Hit = "HitRecieve", Death = "Death",
            Cast = "Bite_Front", Shoot = "Bite_Front", Attacks = new[] { "Bite_Front" }
        };

        public static readonly AnimSet Big = new AnimSet
        {
            Idle = "Idle", Walk = "Walk", Run = "Run", Hit = "HitReact", Death = "Death",
            Cast = "Weapon", Shoot = "Weapon", Attacks = new[] { "Punch", "Weapon" }
        };

        public static readonly AnimSet Wolf = new AnimSet
        {
            Idle = "Idle", Walk = "Walk", Run = "Gallop", Hit = "Idle_HitReact1", Death = "Death",
            Cast = "Attack", Shoot = "Attack", Attacks = new[] { "Attack" }
        };

        public static readonly AnimSet Kenney = new AnimSet
        {
            Idle = "idle", Walk = "walk", Run = "sprint", Death = "die", Interact = "interact-right", Cheer = "emote-yes",
            Cast = "attack-melee-right", Shoot = "attack-melee-right", Attacks = new[] { "attack-melee-right" }
        };
    }

    /// <summary>How a character type looks: which model, how tall, which animations.</summary>
    public class CharacterLook
    {
        public string Model;
        public float Height = 1.9f;
        public AnimSet Anims = AnimSet.KayKit;
        public Color? Tint;
        public float RunSpeed = 6f;     // world speed at which the run cycle looks right
        public Color? Light;            // optional point light (bosses)

        static readonly Dictionary<string, CharacterLook> monsters = new Dictionary<string, CharacterLook>
        {
            { "Dire Wolf", new CharacterLook { Model = "Monsters/Wolf", Height = 1.25f, Anims = AnimSet.Wolf, RunSpeed = 6f, Tint = new Color(0.75f, 0.75f, 0.8f) } },
            { "Goblin", new CharacterLook { Model = "Monsters/Goblin", Height = 1.3f, Anims = AnimSet.Blob, RunSpeed = 3f } },
            { "Goblin Shaman", new CharacterLook { Model = "Monsters/GoblinShaman", Height = 1.5f, Anims = AnimSet.Blob, RunSpeed = 3f } },
            { "Goblin Warchief", new CharacterLook { Model = "Monsters/Warchief", Height = 2.9f, Anims = AnimSet.Big, RunSpeed = 5f } },
            { "Bandit", new CharacterLook { Model = "Characters/Rogue", Height = 1.9f, Tint = new Color(0.85f, 0.75f, 0.7f) } },
            { "Skeleton", new CharacterLook { Model = "Characters/SkeletonWarrior", Height = 1.9f, Anims = AnimSet.Skeleton } },
            { "Skeleton Archer", new CharacterLook { Model = "Characters/SkeletonRogue", Height = 1.85f, Anims = AnimSet.Skeleton } },
            { "Zombie", new CharacterLook { Model = "Characters/Zombie", Height = 1.9f, Anims = AnimSet.Kenney, RunSpeed = 4f } },
            { "Rock Golem", new CharacterLook { Model = "Monsters/Golem", Height = 2.8f, Anims = AnimSet.Big, RunSpeed = 4f, Tint = new Color(0.72f, 0.68f, 0.62f) } },
            { "Lich King", new CharacterLook { Model = "Characters/SkeletonMage", Height = 3.4f, Anims = AnimSet.Skeleton, Tint = new Color(0.7f, 0.9f, 1.15f), Light = new Color(0.4f, 0.8f, 1f) } },
        };

        public static CharacterLook ForMonster(string name) => monsters.TryGetValue(name, out var l) ? l : null;

        /// <summary>Selectable hero appearances (stored on the character and shown to other players).</summary>
        public static readonly string[] HeroModels = { "Knight", "Barbarian", "Mage", "Rogue" };

        public static CharacterLook ForHero(string model)
        {
            if (System.Array.IndexOf(HeroModels, model) < 0) model = HeroModels[0];
            return new CharacterLook { Model = "Characters/" + model, Height = 1.95f };
        }

        static readonly Dictionary<string, CharacterLook> npcs = new Dictionary<string, CharacterLook>
        {
            { "Captain Aldric", new CharacterLook { Model = "Characters/Knight", Height = 2.05f } },
            { "Forester Wren", new CharacterLook { Model = "Characters/RogueHooded", Height = 1.9f } },
            { "Smith Gorrin", new CharacterLook { Model = "Characters/Barbarian", Height = 2.0f } },
            { "Merchant Lysa", new CharacterLook { Model = "Characters/Mage", Height = 1.9f, Tint = new Color(1f, 0.9f, 1f) } },
            { "Sister Mae", new CharacterLook { Model = "Characters/Mage", Height = 1.9f, Tint = new Color(1.15f, 1.15f, 1.1f) } },
            { "Thomas", new CharacterLook { Model = "Characters/Keeper", Height = 1.85f, Anims = AnimSet.Kenney } },
        };

        public static CharacterLook ForNpc(string name) => npcs.TryGetValue(name, out var l) ? l : null;
    }

    /// <summary>
    /// An animated model built from a <see cref="CharacterLook"/>, driven through the legacy Animation
    /// component that glTFast creates. Locomotion blends automatically from movement speed; actions
    /// (attacks, casts, hits) play once and then return to locomotion.
    /// </summary>
    public class CharacterView
    {
        public GameObject Root { get; private set; }
        public float Height { get; private set; }
        readonly CharacterLook look;
        readonly Animation anim;
        string current;
        float actionUntil;
        bool dead;
        int attackIndex;

        CharacterView(GameObject root, CharacterLook look, Animation anim)
        {
            Root = root;
            this.look = look;
            this.anim = anim;
            Height = look.Height;
        }

        /// <summary>Returns null if the model isn't available (caller should fall back to primitives).</summary>
        public static CharacterView Create(Transform parent, CharacterLook look)
        {
            if (look == null) return null;
            var go = ArtLibrary.Spawn(look.Model, parent, Vector3.zero, look.Height);
            if (go == null) return null;
            if (look.Tint.HasValue) ArtLibrary.Tint(go, look.Tint.Value);
            if (look.Light.HasValue)
            {
                var l = new GameObject("Glow").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0, look.Height * 0.8f, 0.5f);
                l.type = LightType.Point;
                l.color = look.Light.Value;
                l.range = 8f;
                l.intensity = 2f;
            }

            var anim = go.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                anim.cullingType = AnimationCullingType.BasedOnRenderers;
                foreach (var name in new[] { look.Anims.Idle, look.Anims.Walk, look.Anims.Run })
                    if (name != null && anim[name] != null) anim[name].wrapMode = WrapMode.Loop;
                if (look.Anims.DeathPose != null && anim[look.Anims.DeathPose] != null) anim[look.Anims.DeathPose].wrapMode = WrapMode.ClampForever;
                if (look.Anims.Death != null && anim[look.Anims.Death] != null) anim[look.Anims.Death].wrapMode = WrapMode.ClampForever;
            }
            var view = new CharacterView(go, look, anim);
            view.Play(look.Anims.Idle, 0f);
            return view;
        }

        bool Has(string clip) => anim != null && clip != null && anim[clip] != null;

        void Play(string clip, float fade, float speed = 1f)
        {
            if (!Has(clip)) return;
            anim[clip].speed = speed;
            if (current == clip && anim.IsPlaying(clip)) return;
            current = clip;
            if (fade <= 0f) anim.Play(clip);
            else anim.CrossFade(clip, fade);
        }

        /// <summary>Call every frame with the character's current ground speed.</summary>
        public void UpdateLocomotion(float speed)
        {
            if (anim == null || dead || Time.time < actionUntil) return;
            if (speed < 0.2f) Play(look.Anims.Idle, 0.15f);
            else if (speed < look.RunSpeed * 0.45f) Play(look.Anims.Walk, 0.15f, Mathf.Clamp(speed / (look.RunSpeed * 0.3f), 0.6f, 1.5f));
            else Play(look.Anims.Run, 0.15f, Mathf.Clamp(speed / look.RunSpeed, 0.7f, 1.6f));
        }

        /// <summary>Plays a one-shot action, at most <paramref name="maxDuration"/> long (it is sped up to fit).</summary>
        public void Action(string clip, float maxDuration = 0f)
        {
            if (anim == null || dead || !Has(clip)) return;
            float length = anim[clip].length;
            float speed = maxDuration > 0f && length > maxDuration ? length / maxDuration : 1f;
            anim[clip].wrapMode = WrapMode.Once;
            anim[clip].speed = speed;
            anim[clip].time = 0f;
            anim.CrossFade(clip, 0.06f);
            current = clip;
            actionUntil = Time.time + length / speed * 0.92f;
        }

        public void Attack(float maxDuration = 0f)
        {
            var attacks = look.Anims.Attacks;
            if (attacks == null || attacks.Length == 0) return;
            Action(attacks[attackIndex++ % attacks.Length], maxDuration);
        }

        public void Cast() => Action(look.Anims.Cast, 0.7f);
        public void Shoot() => Action(look.Anims.Shoot, 0.8f);
        public void Interact() => Action(look.Anims.Interact ?? (look.Anims.Attacks != null ? look.Anims.Attacks[0] : null), 1.2f);
        public void Cheer() => Action(look.Anims.Cheer, 2f);
        public void Spawned() => Action(look.Anims.Spawn, 1.5f);

        public void Hit()
        {
            if (Time.time < actionUntil) return; // don't interrupt attacks
            Action(look.Anims.Hit, 0.4f);
        }

        public void Die()
        {
            if (anim == null || dead) return;
            dead = true;
            if (Has(look.Anims.Death)) { anim[look.Anims.Death].speed = 1f; anim.CrossFade(look.Anims.Death, 0.1f); }
            else Root.transform.localRotation = Quaternion.Euler(-80f, 0, 0);
        }

        public void Revive()
        {
            dead = false;
            actionUntil = 0f;
            current = null;
            Root.transform.localRotation = Quaternion.identity;
            Play(look.Anims.Idle, 0.1f);
        }

        /// <summary>Attachment point for weapons/effects (KayKit rigs have handslot.r).</summary>
        public Transform Hand => ArtLibrary.FindDeep(Root.transform, "handslot.r") ?? Root.transform;
    }
}

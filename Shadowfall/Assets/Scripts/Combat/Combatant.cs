using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum Faction { Player, Enemy }

    /// <summary>Base class for anything with health that can fight (player and monsters).</summary>
    public abstract class Combatant : MonoBehaviour
    {
        public static readonly List<Combatant> All = new List<Combatant>();

        public string DisplayName = "Unknown";
        public int Level = 1;
        public Faction Faction;
        public float MaxHealth = 100, Health = 100;
        public float MaxMana, Mana;
        public float Radius = 0.5f;
        public float Height = 2f;
        public float LastDamagedTime = -99f;
        public bool IsDead { get; protected set; }

        public virtual float Armor => 0f;
        public Vector3 Center => transform.position + Vector3.up * (Height * 0.55f);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        // The health bar's white "chip": the health just lost, shrinking after a moment (it shows how big each hit was).
        float chipFrom;

        /// <summary>Health as the bar's chip shows it: what it was before the last hits, catching up after a moment.</summary>
        public float ChipHealth
        {
            get
            {
                float since = Time.time - LastDamagedTime - 0.45f;
                return since <= 0f ? Mathf.Max(Health, chipFrom) : Mathf.Max(Health, chipFrom - MaxHealth * 0.9f * since);
            }
        }

        /// <summary>Call before a hit lands: the chip starts from the health it had (or from where the last chip still is).</summary>
        protected void NoteHit() => chipFrom = ChipHealth;

        public virtual void TakeDamage(float amount, Combatant source, bool crit = false)
        {
            if (IsDead) return;
            float mitigated = amount * 100f / (100f + Mathf.Max(0f, Armor));
            int dmg = Mathf.Max(1, Mathf.RoundToInt(mitigated));
            NoteHit();
            Health -= dmg;
            LastDamagedTime = Time.time;

            Color c = Faction == Faction.Player ? new Color(1f, 0.25f, 0.2f) : crit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            GameUI.Damage(transform.position + Vector3.up * (Height + 0.2f), crit ? dmg + "!" : dmg.ToString(), c, DamageSize(dmg, crit), crit || Faction == Faction.Player, crit || Faction == Faction.Player);

            OnDamaged(source, dmg);
            if (Health <= 0f)
            {
                Health = 0f;
                IsDead = true;
                Die(source);
            }
        }

        public void Heal(float amount, bool showText = true)
        {
            if (IsDead || amount <= 0f) return;
            float before = Health;
            Health = Mathf.Min(MaxHealth, Health + amount);
            int healed = Mathf.RoundToInt(Health - before);
            if (showText && healed > 0)
                GameUI.Float(transform.position + Vector3.up * (Height + 0.2f), "+" + healed, new Color(0.3f, 1f, 0.3f));
        }

        public void RestoreMana(float amount)
        {
            if (IsDead) return;
            Mana = Mathf.Min(MaxMana, Mana + amount);
        }

        protected virtual void OnDamaged(Combatant source, int amount) { }

        /// <summary>How big a damage number is: bigger for a bigger share of the target's life, and for crits.</summary>
        protected float DamageSize(int dmg, bool crit) => (crit ? 1.45f : 1f) + Mathf.Clamp(dmg / Mathf.Max(1f, MaxHealth) * 1.5f, 0f, 0.5f);
        protected abstract void Die(Combatant killer);

        /// <summary>Collect living combatants hostile to <paramref name="attacker"/> within radius of pos.</summary>
        public static void Overlap(Vector3 pos, float radius, Faction attacker, List<Combatant> results)
        {
            results.Clear();
            foreach (var c in All)
            {
                if (c.IsDead || c.Faction == attacker) continue;
                if (Factory.FlatDistance(c.transform.position, pos) <= radius + c.Radius) results.Add(c);
            }
        }
    }
}

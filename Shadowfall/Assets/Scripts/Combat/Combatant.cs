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

        public virtual void TakeDamage(float amount, Combatant source, bool crit = false)
        {
            if (IsDead) return;
            float mitigated = amount * 100f / (100f + Mathf.Max(0f, Armor));
            int dmg = Mathf.Max(1, Mathf.RoundToInt(mitigated));
            Health -= dmg;
            LastDamagedTime = Time.time;

            Color c = Faction == Faction.Player ? new Color(1f, 0.25f, 0.2f) : crit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            GameUI.Float(transform.position + Vector3.up * (Height + 0.2f), crit ? dmg + "!" : dmg.ToString(), c, crit ? 1.5f : 1f);

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

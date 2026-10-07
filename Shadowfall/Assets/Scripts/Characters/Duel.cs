using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// The client side of duels (server/duel.js): the challenge, the countdown, the fight and the result. While the fight
    /// is on, the opponent's character carries a <see cref="DuelFoe"/>: a hostile combatant only for us, so every attack
    /// and ability works on them, and their hits on us come from the server. We never die in a duel: at the last hit
    /// point we yield, and the duel is lost.
    /// </summary>
    public static class Duel
    {
        public static readonly Color Color = new Color(1f, 0.55f, 0.35f);
        public const float Flee = 45f; // server/duel.js FLEE

        public static int OpponentId { get; private set; }
        public static string OpponentName { get; private set; }
        public static string Phase { get; private set; } = "";
        public static Vector3 Flag { get; private set; }
        public static bool Fighting => Phase == "fight";
        public static bool Active => Phase == "count" || Phase == "fight";

        // a challenge waiting for our answer
        public static int ChallengerId { get; private set; }
        public static string ChallengerName { get; private set; }
        public static float ChallengeTime { get; private set; }

        static float countFrom;
        static GameObject flag;
        static DuelFoe foe;

        public static void Challenged(int id, string name)
        {
            ChallengerId = id;
            ChallengerName = name;
            ChallengeTime = Time.time;
            GameUI.Log(name + " challenges you to a duel!", Color);
            Sfx.Play2D("ui_open", 0.5f);
        }

        public static void Answer(bool yes)
        {
            if (ChallengerId != 0) NetClient.I?.SendDuel("dans", yes: yes);
            ChallengerId = 0;
        }

        public static void Challenge(RemotePlayer rp) => NetClient.I?.SendDuel("dreq", rp.Id);

        public static void OnState(NetMsg m)
        {
            OpponentId = m.id;
            OpponentName = m.name;
            switch (m.k)
            {
                case "count":
                    Phase = "count";
                    Flag = new Vector3(m.x, 0f, m.z);
                    countFrom = Time.time;
                    PlantFlag();
                    GameUI.Banner("Duel with " + m.name + "!", Color);
                    Sfx.Play2D("gong", 0.5f, 1.2f);
                    break;
                case "fight":
                    Phase = "fight";
                    GameUI.Banner("Fight!", Color);
                    Sfx.Play2D("roar", 0.5f, 1.3f);
                    AttachFoe();
                    break;
                case "end":
                {
                    var p = Player.I;
                    bool won = m.win != 0 && NetClient.I != null && m.win == NetClient.I.MyId;
                    bool draw = m.win == 0;
                    GameUI.Banner(draw ? "The duel is a draw" : won ? "You won the duel!" : m.name + " wins the duel", draw ? UISkin.Cream : won ? new Color(0.55f, 1f, 0.55f) : Color);
                    Sfx.Play2D(won ? "quest_done" : "ui_close", 0.6f);
                    if (p != null)
                    {
                        p.Achievements.Add("duels");
                        if (won) { p.Achievements.Add("duels_won"); p.Achievements.Once("duel_foe", m.name); }
                        if (p.Health < 1f && !p.IsDead) p.Health = 1f;
                    }
                    Stop();
                    break;
                }
            }
        }

        /// <summary>The opponent hit us (relayed by the server). At the last hit point we yield instead of falling.</summary>
        public static void OnHit(int from, int dmg)
        {
            var p = Player.I;
            if (!Fighting || from != OpponentId || p == null || p.IsDead) return;
            float mitigated = dmg * 100f / (100f + Mathf.Max(0f, p.Armor));
            RemotePlayer.ById.TryGetValue(from, out var rp);
            var src = rp != null ? rp.GetComponent<DuelFoe>() : null;
            if (p.Health - mitigated > 1f) { p.TakeDamage(dmg, src); return; }
            p.Health = 1f;
            GameUI.Float(p.transform.position + Vector3.up * 2.4f, "Yield", Color, 1.3f);
            NetClient.I?.SendDuel("dyield");
            Phase = "yielded";
        }

        /// <summary>Seconds left of the countdown.</summary>
        public static int Countdown => Mathf.Max(0, 3 - Mathf.FloorToInt(Time.time - countFrom));

        static void PlantFlag()
        {
            if (flag != null) Object.Destroy(flag);
            flag = new GameObject("DuelFlag");
            flag.transform.position = Flag;
            if (ArtLibrary.Spawn("Town/banner-red", flag.transform, Vector3.zero, 3f, ArtLibrary.Fit.Height) == null)
            {
                Factory.Prim(PrimitiveType.Cylinder, flag.transform, new Vector3(0, 1.5f, 0), new Vector3(0.08f, 1.5f, 0.08f), new Color(0.4f, 0.3f, 0.2f));
                Factory.Prim(PrimitiveType.Cube, flag.transform, new Vector3(0.45f, 2.5f, 0), new Vector3(0.8f, 0.6f, 0.04f), new Color(0.75f, 0.12f, 0.1f));
            }
            SpellFx.Ring(Flag + Vector3.up * 0.05f, Color, 3f, 1.2f);
        }

        static void AttachFoe()
        {
            if (RemotePlayer.ById.TryGetValue(OpponentId, out var rp) && rp != null)
            {
                foe = rp.GetComponent<DuelFoe>() ?? rp.gameObject.AddComponent<DuelFoe>();
                foe.Bind(rp);
            }
        }

        static void Stop()
        {
            Phase = "";
            OpponentId = 0;
            if (foe != null) Object.Destroy(foe);
            foe = null;
            if (flag != null) Object.Destroy(flag, 4f);
            flag = null;
        }

        /// <summary>Leaving the world (or a new connection): forget everything.</summary>
        public static void Reset() { Stop(); ChallengerId = 0; }

        /// <summary>Keeps the opponent's proxy on them (their character may be rebuilt as they come in and out of view).</summary>
        public static void Update()
        {
            if (Fighting && (foe == null || !foe.enabled)) AttachFoe();
        }
    }

    /// <summary>
    /// The duel opponent as something we can fight: hostile to us, with the health their snapshots say. Our hits go to
    /// the server (which passes them on); nothing here can kill them.
    /// </summary>
    public class DuelFoe : Combatant
    {
        RemotePlayer rp;

        public void Bind(RemotePlayer r)
        {
            rp = r;
            Faction = Faction.Enemy;
            DisplayName = r.Name;
            Level = r.Level;
            Radius = 0.5f;
            Height = 2f;
            Sync();
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0);
                col.height = 2f;
                col.radius = 0.5f;
            }
        }

        void Sync()
        {
            if (rp == null) return;
            MaxHealth = Mathf.Max(1f, rp.MaxHealth);
            if (Time.time - LastDamagedTime > 0.4f) Health = Mathf.Max(1f, rp.Health);
        }

        void Update()
        {
            Sync();
            IsDead = rp == null || rp.Dead || !Duel.Fighting;
        }

        public override void TakeDamage(float amount, Combatant source, bool crit = false)
        {
            if (IsDead || !(source is Player)) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(amount)); // their armor is applied on their side
            Health = Mathf.Max(1f, Health - dmg * 0.8f);
            LastDamagedTime = Time.time;
            GameUI.Float(transform.position + Vector3.up * 2.2f, crit ? dmg + "!" : dmg.ToString(), crit ? new Color(1f, 0.85f, 0.2f) : Color.white, crit ? 1.5f : 1f);
            SpellFx.Hit(Center, new Color(0.55f, 0.03f, 0.03f), true, crit ? 14 : 8);
            Sfx.Play(crit ? "hit_heavy" : "hit_flesh", Center, 0.5f, 0.12f);
            NetClient.I?.SendDuelHit(rp != null ? rp.Id : 0, dmg);
        }

        protected override void Die(Combatant killer) { }
    }
}

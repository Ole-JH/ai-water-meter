using System.Collections;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// A longer scripted playtest after the browser check's walk, only with <c>?sfcheck=1&amp;tour=1</c> (never in the
    /// deploy check, which stays short): opens the main windows, walks out of town and fights whatever it finds, then
    /// goes down the nearest dungeon. Screenshots along the way, and the frame rate of each scene reported as "fps:*".
    /// </summary>
    public class PlaytestTour : MonoBehaviour
    {
        public static bool Requested => GameCheck.Requested && Application.absoluteURL.Contains("tour=1");

        System.Func<string, IEnumerator> shot;
        System.Action<string, string> report;

        public IEnumerator Run(System.Func<string, IEnumerator> shot, System.Action<string, string> report)
        {
            this.shot = shot;
            this.report = report;
            var hero = Player.I;
            // on Low, like the slowest machines (and so software rendering stays quick enough for screenshots)
            GameSettings.Quality = 0;
            yield return new WaitForSeconds(1f);
            yield return Fps("town-idle", 5f);

            // the windows
            foreach (var w in new[] { "bags", "char", "talents", "map", "achievements", "comfort" })
            {
                GameUI.CheckShow(w, true);
                yield return shot("window-" + w);
                GameUI.CheckShow(w, false);
            }

            // the nearest dungeon
            DungeonDef best = null;
            float bestD = float.MaxValue;
            foreach (var d in DungeonDef.All)
            {
                float dist = Factory.FlatDistance(d.Entrance, hero.transform.position);
                if (dist < bestD) { bestD = dist; best = d; }
            }
            if (best == null) goto Outside;
            report("tour", "walking to " + best.Name + " (" + bestD.ToString("0") + " m)");
            hero.MoveTo(best.Entrance);
            float walkUntil = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < walkUntil && Factory.FlatDistance(hero.transform.position, best.Entrance) > 4f && !hero.IsDead)
            {
                if (hero.AttackTarget == null && (Time.frameCount % 60) == 0) hero.MoveTo(best.Entrance);
                yield return null;
            }
            yield return Fps("wilds-walk", 3f);
            yield return shot("dungeon-door");
            NetClient.I.EnterDungeon(System.Array.IndexOf(DungeonDef.All, best), 0);
            float inUntil = Time.realtimeSinceStartup + 30f;
            while (!Dungeon.Active && Time.realtimeSinceStartup < inUntil) yield return null;
            if (!Dungeon.Active) { report("tour", "couldn't enter " + best.Name); goto Outside; }
            yield return new WaitForSeconds(4f);
            yield return Fps("dungeon", 5f);
            yield return shot("dungeon");
            GameUI.CheckShow("map", true);
            yield return shot("dungeon-map");
            GameUI.CheckShow("map", false);
            // a look around: walk a little way in
            var e2 = Nearest(hero.transform.position);
            if (e2 != null)
            {
                hero.SetAttackTarget(e2);
                float u = Time.realtimeSinceStartup + 25f;
                while (Time.realtimeSinceStartup < u && e2 != null && !e2.IsDead && !hero.IsDead) { if (hero.AttackTarget != e2) hero.SetAttackTarget(e2); yield return null; }
                yield return Fps("dungeon-fight", 2f);
                yield return shot("dungeon-fight");
            }

            Outside:
            // back up top (out of the dungeon) and a fight in the open
            if (Dungeon.Active) { NetClient.I.LeaveDungeon(false); float outUntil = Time.realtimeSinceStartup + 30f; while (Dungeon.Active && Time.realtimeSinceStartup < outUntil) yield return null; yield return new WaitForSeconds(3f); }
            // out of town to the nearest monsters, and fight
            for (int fight = 0; fight < 3 && hero != null && !hero.IsDead; fight++)
            {
                var e = Nearest(hero.transform.position);
                if (e == null) { report("tour", "no monsters found"); break; }
                report("tour", "fighting " + e.Def.Name + " at " + Fmt(e.transform.position));
                hero.SetAttackTarget(e);
                float until = Time.realtimeSinceStartup + 45f, shotAt = Time.realtimeSinceStartup + 6f;
                bool shotTaken = false;
                while (Time.realtimeSinceStartup < until && e != null && !e.IsDead && !hero.IsDead)
                {
                    if (hero.Health < hero.MaxHealth * 0.6f) { report("tour", "backing off at " + Mathf.RoundToInt(hero.Health) + " hp"); break; }
                    if (hero.AttackTarget != e) hero.SetAttackTarget(e);
                    if (Factory.FlatDistance(hero.transform.position, e.transform.position) < 3f)
                    {
                        hero.CastAbility(0, e.transform.position, true);
                        if (!shotTaken && Time.realtimeSinceStartup > shotAt) { shotTaken = true; yield return Fps("fight", 3f); yield return shot("fight-" + fight); }
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(1.2f);
                yield return shot(hero.IsDead ? "died" : "after-fight-" + fight);
                if (hero.IsDead || hero.Health < hero.MaxHealth * 0.6f) break;
            }
            if (hero != null && hero.IsDead)
            {
                yield return new WaitForSeconds(6f);
                yield return shot("death-screen");
            }

        }

        IEnumerator Fps(string scene, float seconds)
        {
            int frames = 0;
            float t0 = Time.realtimeSinceStartup, worst = 0f;
            while (Time.realtimeSinceStartup - t0 < seconds) { frames++; worst = Mathf.Max(worst, Time.unscaledDeltaTime); yield return null; }
            float fps = frames / (Time.realtimeSinceStartup - t0);
            report("fps:" + scene, fps.ToString("0.0") + " fps, worst frame " + (worst * 1000f).ToString("0") + " ms, " +
                Object.FindObjectsOfType<ParticleSystem>().Length + " particle systems, " + Object.FindObjectsOfType<Light>().Length + " lights");
        }

        static Enemy Nearest(Vector3 from)
        {
            Enemy best = null;
            float bd = 90f;
            foreach (var e in Enemy.ById.Values)
            {
                if (e == null || e.IsDead || e.Def.Boss) continue;
                float d = Factory.FlatDistance(e.transform.position, from);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        static string Fmt(Vector3 p) => "(" + p.x.ToString("0") + ", " + p.z.ToString("0") + ")";
    }
}

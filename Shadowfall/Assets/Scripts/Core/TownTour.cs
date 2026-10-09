using System.Collections;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Town life up close, only with <c>?sfcheck=1&amp;town=1</c> (never in the deploy check): Hollowmere by day under a
    /// pretend invasion (villagers running for their doors, shutters shut), then at night (every front door swung open
    /// in turn, lit windows) and a gate pulled to and swung open as the hero walks up. All on this client only: the
    /// invasion is a made-up state and the time of day is held with <see cref="DayNight.HourOverride"/>.
    /// </summary>
    public class TownTour : MonoBehaviour
    {
        public static bool Requested => GameCheck.Requested && Application.absoluteURL.Contains("town=1");

        public IEnumerator Run(System.Func<string, IEnumerator> shot, System.Action<string, string> report)
        {
            var cam = GameManager.I.Cam;
            var rig = CameraRig.I;
            var hero = Player.I;
            var town = WorldGenerator.Towns[0];
            var square = town.Center;
            void Look(Vector3 from, Vector3 at) { cam.transform.position = from; cam.transform.LookAt(at); }

            // ---- by day, raiders at the south gate
            DayNight.HourOverride = 10.5f;
            yield return new WaitForSeconds(4f);
            if (rig != null) rig.enabled = false;
            Look(square + new Vector3(-16f, 14f, -16f), square);
            yield return shot("day-square");
            var gate = Rampart.SideOf(town, "south").Centre;
            Invasion.Set(new NetInvasion { town = town.Name, gate = "south", phase = "gather", left = 60, hp = 100, gx = gate.x, gz = gate.z - 3f, waves = 3 });
            yield return new WaitForSeconds(1.5f);
            Look(square + new Vector3(-16f, 14f, -16f), square);
            yield return shot("raid-running");
            yield return new WaitForSeconds(8f);
            yield return shot("raid-square-empty");
            int k = 0;
            foreach (var d in HouseDoors.All)
            {
                if (WorldGenerator.TownAt(d.Centre) != town) continue;
                Look(d.Centre + d.Out * 9f + Vector3.up * 5f + Vector3.Cross(Vector3.up, d.Out) * 4f, d.Centre + Vector3.up * 1.5f);
                yield return new WaitForSeconds(0.6f);
                yield return shot("raid-shutters-" + k++);
                if (k >= 2) break;
            }
            Invasion.Set(new NetInvasion { town = town.Name, gate = "south", phase = "none" });
            yield return new WaitForSeconds(4f);

            // ---- at night: every front door in turn, and the windows
            DayNight.HourOverride = 22f;
            yield return new WaitForSeconds(3f);
            Look(square + new Vector3(-16f, 14f, -16f), square);
            yield return shot("night-square");
            k = 0;
            foreach (var d in HouseDoors.All)
            {
                if (WorldGenerator.TownAt(d.Centre) != town) continue;
                report("door " + k, d.Model + " at " + Fmt(d.Centre) + " facing " + Fmt(d.Out));
                Look(d.Centre + d.Out * 7f + Vector3.up * 3.5f + Vector3.Cross(Vector3.up, d.Out) * 2.5f, d.Centre + Vector3.up * 1.2f);
                HouseDoors.Swing(d, 6f);
                yield return new WaitForSeconds(1f);
                yield return shot("night-door-" + k++);
            }

            // ---- a gate: pulled to at night, swung open as the hero walks up
            var side = Rampart.SideOf(town, "west");
            var inside = side.Centre - side.Out * 12f;
            hero.TeleportTo(square);
            yield return new WaitForSeconds(3f);
            Look(inside + Vector3.up * 6f - side.Out * 2f, side.Centre + Vector3.up * 1.5f);
            yield return shot("gate-night-shut");
            hero.TeleportTo(side.Centre - side.Out * 5f);
            yield return new WaitForSeconds(2.5f);
            Look(inside + Vector3.up * 6f - side.Out * 2f, side.Centre + Vector3.up * 1.5f);
            yield return shot("gate-night-open");

            DayNight.HourOverride = null;
            if (rig != null) { rig.enabled = true; rig.SnapToTarget(); }
        }

        static string Fmt(Vector3 p) => "(" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ")";
    }
}

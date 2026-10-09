using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Sounds of the place around the hero, now and then, at a distance: wolves howling at night out in the wilds,
    /// leaves stirring in the forests by day, and in the dungeons far-off rumbles, drips and groans. (The wind and the
    /// crickets are steady loops in Sfx; the music is Music's.)
    /// </summary>
    public class AmbientSounds : MonoBehaviour
    {
        float next;

        public static void Ensure()
        {
            if (FindObjectOfType<AmbientSounds>() == null) new GameObject("AmbientSounds").AddComponent<AmbientSounds>();
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + Random.Range(7f, 16f);
            var p = Player.I;
            if (p == null || p.IsDead) return;
            var at = p.transform.position;
            // somewhere off to the side, not right on top of us
            var off = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(14f, 26f);
            if (Dungeon.Active)
            {
                string[] deep = { "rubble", "undead", "hit_stone", "splash" };
                Sfx.Play(deep[Random.Range(0, deep.Length)], at + off, Random.Range(0.12f, 0.25f), 0.25f, 40f);
                return;
            }
            if (WorldGenerator.InTown(at)) return; // towns have their own life
            string zone = WorldGenerator.ZoneAt(at);
            bool night = DayNight.Night > 0.6f;
            if (night && Random.value < 0.45f) Sfx.Play("wolf_howl", at + off * 1.6f, Random.Range(0.12f, 0.22f), 0.15f, 60f);
            else if (!night && (zone == "Whisperwood" || zone == WorldGenerator.Frostpeak) && Weather.Sky != "storm")
                Sfx.Play("leaves", at + off * 0.6f, Random.Range(0.15f, 0.3f), 0.2f, 30f);
        }
    }
}

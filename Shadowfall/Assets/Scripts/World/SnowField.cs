using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Where the snow has been cleared: a mask over the overworld (two cells per tile), 1 where it was shoveled down to
    /// the ground, about 0.55 where feet have packed it down, 0 where it's untouched. The terrain and grass shaders read
    /// it as _SfSnowMask; snow depth at a point is the weather's snow cover there times (1 - cleared).
    /// Fresh snowfall fills cleared paths back in, so the elves never run out of work, but never past
    /// <see cref="StreetFloor"/> on town streets: the village keeps its streets passable even when the shovelers fall
    /// behind (their work clears the rest). Local to each player.
    /// </summary>
    public static class SnowField
    {
        public const int Res = 2;
        const byte Packed = 140;
        /// <summary>Town streets never get deeper than about a quarter of the snow around them (no caked boots there).</summary>
        const byte StreetFloor = 190;

        static byte[] mask, floor;
        static Texture2D tex;
        static int w, h;
        static bool dirty;
        static float nextUpload, nextRefill;
        static readonly int MaskId = Shader.PropertyToID("_SfSnowMask");

        static void Ensure()
        {
            if (mask != null) return;
            w = WorldGenerator.W * Res;
            h = WorldGenerator.H * Res;
            mask = new byte[w * h];
            floor = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (WorldGenerator.IsTownStreet(x / Res, y / Res)) floor[y * w + x] = mask[y * w + x] = StreetFloor;
            tex = new Texture2D(w, h, TextureFormat.R8, false, true) { name = "SnowMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.LoadRawTextureData(mask);
            tex.Apply(false);
            Shader.SetGlobalTexture(MaskId, tex);
        }

        static bool Cell(Vector3 p, out int x, out int y)
        {
            x = Mathf.FloorToInt(p.x * Res);
            y = Mathf.FloorToInt(p.z * Res);
            return x >= 0 && y >= 0 && x < w && y < h;
        }

        /// <summary>How much of the snow has been cleared here (0 untouched .. 1 shoveled).</summary>
        public static float Cleared(Vector3 p)
        {
            Ensure();
            return Cell(p, out int x, out int y) ? mask[y * w + x] / 255f : 0f;
        }

        /// <summary>Snow depth 0..1 under your feet.</summary>
        public static float DepthAt(Vector3 p)
        {
            float cover = Weather.SnowCoverAt(p);
            return cover <= 0.001f ? 0f : cover * (1f - Cleared(p));
        }

        /// <summary>Shovels a round patch down to the ground (amount 1) or packs it down (amount ~0.55).</summary>
        public static void Clear(Vector3 center, float radius, float amount = 1f)
        {
            Ensure();
            byte target = (byte)Mathf.Clamp(Mathf.RoundToInt(amount * 255f), 0, 255);
            int r = Mathf.CeilToInt(radius * Res);
            if (!Cell(center, out int cx, out int cy)) return;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Mathf.Max(1f, r);
                    if (d > 1f) continue;
                    byte v = (byte)(target * Mathf.Clamp01(1.3f - d * 0.6f));
                    int i = y * w + x;
                    if (v > mask[i]) { mask[i] = v; dirty = true; settled = false; }
                }
        }

        /// <summary>Feet pack the snow down a little at a time, into a trail.</summary>
        public static void Trample(Vector3 p)
        {
            if (Weather.SnowCoverAt(p) < 0.05f) return;
            Clear(p, 0.45f, Packed / 255f);
        }

        static int refillRow, refillAmount;
        // A full pass that changed nothing means every cell is back at its floor: no more passes until something is
        // cleared again (the pass is 1.3 million cells a second, for nothing, most of the year)
        static bool settled, changedThisPass;

        /// <summary>
        /// Called by the weather every frame: fresh snow fills paths in; with no snow around the mask fades. The refill
        /// works through the mask a band of rows per frame (a full pass every second) instead of all 1.3 million cells
        /// in one frame, and the mask goes to the GPU at most twice a second.
        /// </summary>
        public static void Tick()
        {
            Ensure();
            if (Time.time >= nextRefill)
            {
                nextRefill = Time.time + 1f;
                bool snowing = Weather.Precip > 0.15f && Weather.Season != Season.Summer;
                bool bare = Weather.SnowNorth < 0.01f && Weather.SnowSouth < 0.01f && Weather.SnowPerm < 0.01f;
                refillAmount = settled ? 0 : snowing ? Mathf.RoundToInt(Weather.Precip * 255f / 100f) : bare ? 20 : 0;
                refillRow = 0;
                changedThisPass = false;
            }
            if (refillAmount > 0 && refillRow < h)
            {
                int rows = Mathf.Max(1, Mathf.CeilToInt(h * Time.deltaTime * 1.1f)); // the whole mask in about a second
                int end = Mathf.Min(h, refillRow + rows);
                for (int i = refillRow * w, last = end * w; i < last; i++)
                {
                    int v = mask[i], f = floor[i];
                    if (v <= f) continue;
                    mask[i] = (byte)Mathf.Max(f, v - refillAmount);
                    dirty = true;
                    changedThisPass = true;
                }
                refillRow = end;
                if (refillRow >= h && !changedThisPass) settled = true;
            }
            if (dirty && Time.time >= nextUpload)
            {
                nextUpload = Time.time + 0.5f;
                dirty = false;
                tex.LoadRawTextureData(mask);
                tex.Apply(false);
            }
        }
    }
}

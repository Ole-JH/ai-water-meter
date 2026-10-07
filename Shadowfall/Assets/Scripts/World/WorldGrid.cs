using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Tile grid (1 world unit per cell) used for walkability and A* pathfinding.
    /// Cell (x, y) covers world X in [x, x+1) and world Z in [y, y+1).
    /// </summary>
    public class WorldGrid
    {
        public static WorldGrid Instance;

        public readonly int Width, Height;
        /// <summary>World position of cell (0, 0): (0, 0) for the overworld, offset for dungeon grids.</summary>
        public Vector2Int Origin;
        readonly bool[] blocked;

        // A* scratch buffers (reused between searches, invalidated by a stamp counter)
        readonly float[] gScore;
        readonly int[] parent;
        readonly int[] seenStamp;
        readonly int[] closedStamp;
        int stamp;
        readonly MinHeap open;
        readonly List<Vector3> rawPath = new List<Vector3>();

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        const float Diag = 1.41421356f;

        public WorldGrid(int width, int height, bool makeCurrent = true)
        {
            Width = width;
            Height = height;
            int n = width * height;
            blocked = new bool[n];
            gScore = new float[n];
            parent = new int[n];
            seenStamp = new int[n];
            closedStamp = new int[n];
            open = new MinHeap(n);
            if (makeCurrent) Instance = this;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsBlocked(int x, int y) => !InBounds(x, y) || blocked[y * Width + x];
        public bool IsBlocked(Vector2Int c) => IsBlocked(c.x, c.y);
        public void SetBlocked(int x, int y, bool value) { if (InBounds(x, y)) blocked[y * Width + x] = value; }

        public Vector2Int WorldToCell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x) - Origin.x, Mathf.FloorToInt(p.z) - Origin.y);
        public Vector3 CellToWorld(Vector2Int c) => new Vector3(c.x + Origin.x + 0.5f, 0f, c.y + Origin.y + 0.5f);
        public bool IsWalkable(Vector3 p) => !IsBlocked(WorldToCell(p));

        /// <summary>Walkability bitmap (bit set = blocked, LSB first) for uploading to the server.</summary>
        public byte[] Pack()
        {
            var bytes = new byte[(blocked.Length + 7) / 8];
            for (int i = 0; i < blocked.Length; i++)
                if (blocked[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            return bytes;
        }

        /// <summary>Replaces the walkability with a packed bitmap (the server's map; see <see cref="Pack"/>).</summary>
        public void Unpack(byte[] bytes)
        {
            for (int i = 0; i < blocked.Length && (i >> 3) < bytes.Length; i++)
                blocked[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        }

        /// <summary>FNV-1a hash of the packed grid; identical clients produce identical hashes.</summary>
        public string Hash()
        {
            uint h = 2166136261;
            foreach (var b in Pack()) { h ^= b; h *= 16777619; }
            return Width + "x" + Height + "-" + h.ToString("x8");
        }

        public void BlockRect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetBlocked(x, y, true);
        }

        public void BlockCircle(Vector3 center, float radius)
        {
            int r = Mathf.CeilToInt(radius);
            var c = WorldToCell(center);
            for (int y = c.y - r; y <= c.y + r; y++)
                for (int x = c.x - r; x <= c.x + r; x++)
                {
                    var w = CellToWorld(new Vector2Int(x, y));
                    float dx = w.x - center.x, dz = w.z - center.z;
                    if (dx * dx + dz * dz <= radius * radius) SetBlocked(x, y, true);
                }
        }

        public bool NearestWalkable(Vector2Int c, int maxRadius, out Vector2Int result)
        {
            result = c;
            if (!IsBlocked(c)) return true;
            for (int r = 1; r <= maxRadius; r++)
            {
                float best = float.MaxValue;
                bool found = false;
                for (int y = c.y - r; y <= c.y + r; y++)
                    for (int x = c.x - r; x <= c.x + r; x++)
                    {
                        if (Mathf.Abs(x - c.x) != r && Mathf.Abs(y - c.y) != r) continue;
                        if (IsBlocked(x, y)) continue;
                        float d = (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y);
                        if (d < best) { best = d; result = new Vector2Int(x, y); found = true; }
                    }
                if (found) return true;
            }
            return false;
        }

        /// <summary>Straight-line walkability test with a little side clearance.</summary>
        public bool LineOfSight(Vector3 a, Vector3 b, float clearance = 0.3f)
        {
            Vector3 d = Factory.Flat(b - a);
            float dist = d.magnitude;
            if (dist < 0.001f) return IsWalkable(a);
            Vector3 dir = d / dist;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x) * clearance;
            int steps = Mathf.CeilToInt(dist / 0.25f);
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = a + dir * (dist * i / steps);
                if (!IsWalkable(p) || !IsWalkable(p + perp) || !IsWalkable(p - perp)) return false;
            }
            return true;
        }

        /// <summary>
        /// A* search from start to goal. Fills <paramref name="outPath"/> with smoothed world-space waypoints
        /// (excluding the start). Returns false when no path (or no partial progress) is possible.
        /// </summary>
        public bool FindPath(Vector3 start, Vector3 goal, List<Vector3> outPath, int maxExpand = 12000)
        {
            outPath.Clear();
            var s = WorldToCell(start);
            var g = WorldToCell(goal);
            if (IsBlocked(s) && !NearestWalkable(s, 3, out s)) return false;
            if (IsBlocked(g))
            {
                if (!NearestWalkable(g, 10, out g)) return false;
                goal = CellToWorld(g);
            }

            if (s == g || LineOfSight(start, goal))
            {
                outPath.Add(goal);
                return true;
            }

            stamp++;
            open.Clear();
            int si = s.y * Width + s.x;
            int gi = g.y * Width + g.x;
            gScore[si] = 0f;
            parent[si] = -1;
            seenStamp[si] = stamp;
            open.Push(si, Heuristic(s.x, s.y, g.x, g.y));

            int bestIdx = si;
            float bestH = float.MaxValue;
            int expanded = 0;
            bool reached = false;

            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (closedStamp[cur] == stamp) continue;
                closedStamp[cur] = stamp;
                if (cur == gi) { reached = true; break; }
                if (++expanded > maxExpand) break;

                int cx = cur % Width, cy = cur / Width;
                float h = Heuristic(cx, cy, g.x, g.y);
                if (h < bestH) { bestH = h; bestIdx = cur; }

                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + DX[i], ny = cy + DY[i];
                    if (IsBlocked(nx, ny)) continue;
                    if (i >= 4 && (IsBlocked(cx + DX[i], cy) || IsBlocked(cx, cy + DY[i]))) continue; // no corner cutting
                    int ni = ny * Width + nx;
                    if (closedStamp[ni] == stamp) continue;
                    float ng = gScore[cur] + (i >= 4 ? Diag : 1f);
                    if (seenStamp[ni] == stamp && ng >= gScore[ni]) continue;
                    seenStamp[ni] = stamp;
                    gScore[ni] = ng;
                    parent[ni] = cur;
                    open.Push(ni, ng + Heuristic(nx, ny, g.x, g.y));
                }
            }

            int end = reached ? gi : bestIdx;
            if (end == si) return false;

            rawPath.Clear();
            for (int c = end; c != -1 && c != si; c = parent[c])
                rawPath.Add(CellToWorld(new Vector2Int(c % Width, c / Width)));
            rawPath.Reverse();
            if (reached) rawPath[rawPath.Count - 1] = goal;

            // Greedy string-pulling to remove zig-zags.
            Vector3 from = start;
            int idx = 0;
            while (idx < rawPath.Count)
            {
                int furthest = idx;
                for (int j = idx + 1; j < rawPath.Count && j < idx + 30; j++)
                {
                    if (LineOfSight(from, rawPath[j])) furthest = j;
                    else break;
                }
                outPath.Add(rawPath[furthest]);
                from = rawPath[furthest];
                idx = furthest + 1;
            }
            return outPath.Count > 0;
        }

        static float Heuristic(int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x0 - x1), dy = Mathf.Abs(y0 - y1);
            return (dx + dy) + (Diag - 2f) * Mathf.Min(dx, dy);
        }

        class MinHeap
        {
            int[] items;
            float[] keys;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                items = new int[Mathf.Max(16, capacity)];
                keys = new float[items.Length];
            }

            public void Clear() => Count = 0;

            public void Push(int item, float key)
            {
                if (Count == items.Length)
                {
                    System.Array.Resize(ref items, items.Length * 2);
                    System.Array.Resize(ref keys, keys.Length * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= key) break;
                    items[i] = items[p];
                    keys[i] = keys[p];
                    i = p;
                }
                items[i] = item;
                keys[i] = key;
            }

            public int Pop()
            {
                int result = items[0];
                Count--;
                if (Count > 0)
                {
                    int item = items[Count];
                    float key = keys[Count];
                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1;
                        if (l >= Count) break;
                        int r = l + 1;
                        int m = (r < Count && keys[r] < keys[l]) ? r : l;
                        if (keys[m] >= key) break;
                        items[i] = items[m];
                        keys[i] = keys[m];
                        i = m;
                    }
                    items[i] = item;
                    keys[i] = key;
                }
                return result;
            }
        }
    }
}

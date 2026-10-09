using UnityEngine;

namespace Shadowfall
{
    /// <summary>
    /// Paragon levels: past <see cref="MaxLevel"/> experience fills paragon levels instead, each worth a point to put in
    /// Might, Toughness, Precision or Swiftness (at most <see cref="Cap"/> each; free to reassign). Saved with the hero;
    /// other players see the paragon level on the nameplate.
    /// </summary>
    public class ParagonBoard
    {
        public const int MaxLevel = 30, Cap = 50;
        public static readonly string[] Names = { "Might", "Toughness", "Precision", "Swiftness" };
        public static readonly string[] Effects = { "+1% damage", "+1% maximum life", "+0.25% critical hit chance", "+0.3% attack and movement speed" };
        public static readonly Color Color = new Color(0.45f, 0.8f, 1f);

        public int Level, Xp;
        public readonly int[] Points = new int[4];

        public int Spent { get { int n = 0; foreach (var v in Points) n += v; return n; } }
        public int Free => Mathf.Max(0, Mathf.Min(Level, Cap * Points.Length) - Spent);
        public int XpToNext => 12000 + Level * 300;

        public float Damage => 1f + 0.01f * Points[0];
        public float Life => 1f + 0.01f * Points[1];
        public float Crit => 0.25f * Points[2];
        public float Speed => 1f + 0.003f * Points[3];

        public bool Spend(int i)
        {
            if (i < 0 || i >= Points.Length || Free <= 0 || Points[i] >= Cap) return false;
            Points[i]++;
            return true;
        }

        public void Reset() { for (int i = 0; i < Points.Length; i++) Points[i] = 0; }

        /// <summary>Adds experience; returns how many paragon levels were gained.</summary>
        public int Add(int xp)
        {
            int gained = 0;
            Xp += xp;
            while (Xp >= XpToNext) { Xp -= XpToNext; Level++; gained++; }
            return gained;
        }

        public void Load(int level, int xp, int[] points)
        {
            Level = Mathf.Max(0, level);
            Xp = Mathf.Max(0, xp);
            Reset();
            if (points != null) for (int i = 0; i < Points.Length && i < points.Length; i++) Points[i] = Mathf.Clamp(points[i], 0, Cap);
            while (Spent > Level) for (int i = Points.Length - 1; i >= 0 && Spent > Level; i--) if (Points[i] > 0) Points[i]--;
        }
    }
}

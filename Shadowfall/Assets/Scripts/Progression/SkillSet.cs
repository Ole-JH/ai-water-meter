using UnityEngine;

namespace Shadowfall
{
    public enum SkillType { Woodcutting, Mining, Fishing, Smithing, Cooking }

    /// <summary>RuneScape-style gathering / crafting skills (levels 1-99, classic XP curve).</summary>
    public class SkillSet
    {
        public const int MaxLevel = 99;
        public static readonly SkillType[] All =
            { SkillType.Woodcutting, SkillType.Mining, SkillType.Fishing, SkillType.Smithing, SkillType.Cooking };

        static int[] xpTable;
        readonly int[] xp = new int[5];
        readonly int[] level = { 1, 1, 1, 1, 1 };

        public int Level(SkillType s) => level[(int)s];
        public int Xp(SkillType s) => xp[(int)s];
        public int TotalLevel { get { int t = 0; foreach (var l in level) t += l; return t; } }

        /// <summary>Total XP required to reach the given level (RuneScape formula).</summary>
        public static int XpForLevel(int lvl)
        {
            if (xpTable == null)
            {
                xpTable = new int[MaxLevel + 2];
                float points = 0;
                for (int l = 1; l <= MaxLevel + 1; l++)
                {
                    xpTable[l] = Mathf.FloorToInt(points / 4f);
                    points += Mathf.Floor(l + 300f * Mathf.Pow(2f, l / 7f));
                }
            }
            return xpTable[Mathf.Clamp(lvl, 1, MaxLevel + 1)];
        }

        public static string Verb(SkillType s)
        {
            switch (s)
            {
                case SkillType.Woodcutting: return "Chopping";
                case SkillType.Mining: return "Mining";
                case SkillType.Fishing: return "Fishing";
                case SkillType.Smithing: return "Smithing";
                default: return "Cooking";
            }
        }

        public static Color SkillColor(SkillType s)
        {
            switch (s)
            {
                case SkillType.Woodcutting: return new Color(0.4f, 0.75f, 0.3f);
                case SkillType.Mining: return new Color(0.7f, 0.6f, 0.5f);
                case SkillType.Fishing: return new Color(0.3f, 0.6f, 0.95f);
                case SkillType.Smithing: return new Color(0.85f, 0.5f, 0.25f);
                default: return new Color(0.95f, 0.4f, 0.3f);
            }
        }

        public int[] SaveXp() => (int[])xp.Clone();

        public void LoadXp(int[] saved)
        {
            if (saved == null) return;
            for (int i = 0; i < xp.Length && i < saved.Length; i++)
            {
                xp[i] = Mathf.Max(0, saved[i]);
                level[i] = 1;
                while (level[i] < MaxLevel && xp[i] >= XpForLevel(level[i] + 1)) level[i]++;
            }
        }

        public void AddXp(SkillType s, int amount)
        {
            int i = (int)s;
            xp[i] += amount;
            GameUI.Float(Player.I.transform.position + Vector3.up * 2.6f, "+" + amount + " " + s + " xp", SkillColor(s), 0.8f);
            while (level[i] < MaxLevel && xp[i] >= XpForLevel(level[i] + 1))
            {
                level[i]++;
                GameUI.Banner("Your " + s + " level is now " + level[i] + "!", SkillColor(s));
                GameUI.Log("Congratulations, you just advanced a " + s + " level. You are now level " + level[i] + ".", SkillColor(s));
                FxPulse.Ring(Player.I.transform.position, SkillColor(s), 2.5f, 0.6f);
                Player.I.Achievements.Max("skill." + s, level[i]);
                NetClient.I?.SaveNow(); // the server checks skill levels for gathering and crafting
            }
        }
    }
}

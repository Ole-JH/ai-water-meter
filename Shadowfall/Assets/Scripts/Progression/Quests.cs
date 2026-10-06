using System.Collections.Generic;
using UnityEngine;

namespace Shadowfall
{
    public enum QuestType { Kill, Collect }

    public class QuestDef
    {
        public string Id, Title, Description, Objective, CompletionText;
        public QuestType Type;
        public string Target;
        public int Count;
        public int MinLevel = 1;
        public int RewardXp, RewardGold;
        public int RewardItemLevel;     // 0 = no item
        public Rarity RewardRarity = Rarity.Magic;
    }

    public class QuestState
    {
        public QuestDef Def;
        public int Kills;
        public QuestState(QuestDef d) { Def = d; }

        public int Progress(Player p) =>
            Def.Type == QuestType.Kill ? Kills : Mathf.Min(Def.Count, p.Inventory.CountOf(Def.Target));

        public bool IsReady(Player p) => Progress(p) >= Def.Count;
    }

    /// <summary>WoW-style quest log: accept from NPCs, track kills / items, turn in for rewards.</summary>
    public class QuestLog
    {
        public readonly List<QuestState> Active = new List<QuestState>();
        public readonly HashSet<string> Completed = new HashSet<string>();

        public bool IsActive(string id) => Active.Exists(q => q.Def.Id == id);
        public QuestState Get(string id) => Active.Find(q => q.Def.Id == id);

        public void Accept(QuestDef def)
        {
            if (IsActive(def.Id) || Completed.Contains(def.Id)) return;
            Active.Add(new QuestState(def));
            GameUI.Banner("Quest Accepted: " + def.Title, new Color(1f, 0.85f, 0.3f));
            GameUI.Log("Quest accepted: " + def.Title, new Color(1f, 0.85f, 0.3f));
        }

        public void OnKill(string enemyName)
        {
            foreach (var q in Active)
            {
                if (q.Def.Type != QuestType.Kill || q.Def.Target != enemyName || q.Kills >= q.Def.Count) continue;
                q.Kills++;
                GameUI.Log(q.Def.Target + " slain: " + q.Kills + "/" + q.Def.Count, new Color(1f, 0.85f, 0.3f));
                if (q.Kills >= q.Def.Count)
                    GameUI.Banner(q.Def.Title + " - objective complete!", new Color(1f, 0.85f, 0.3f));
            }
        }

        public void TurnIn(QuestState q, Player p)
        {
            if (!q.IsReady(p)) return;
            if (q.Def.Type == QuestType.Collect) p.Inventory.Remove(q.Def.Target, q.Def.Count);
            Active.Remove(q);
            Completed.Add(q.Def.Id);
            p.AddGold(q.Def.RewardGold);
            p.AddXp(q.Def.RewardXp);
            if (q.Def.RewardItemLevel > 0)
            {
                var item = ItemDatabase.RandomEquipment(q.Def.RewardItemLevel, 0f, q.Def.RewardRarity);
                if (!p.Inventory.Add(item)) LootDrop.Spawn(p.transform.position, item, 0);
                GameUI.Log("You receive: " + item.Name, item.NameColor);
            }
            NetClient.I?.SaveNow();
            GameUI.Banner("Quest Complete: " + q.Def.Title, new Color(1f, 0.85f, 0.3f));
            GameUI.Log("Quest complete: " + q.Def.Title + " (+" + q.Def.RewardXp + " xp, +" + q.Def.RewardGold + " gold)",
                new Color(1f, 0.85f, 0.3f));
        }
    }

    public static class QuestDatabase
    {
        public static QuestDef Find(string id)
        {
            foreach (var chain in Chains.Values)
                foreach (var q in chain)
                    if (q.Id == id) return q;
            return null;
        }

        public static readonly Dictionary<string, QuestDef[]> Chains = new Dictionary<string, QuestDef[]>
        {
            {
                "Captain Aldric", new[]
                {
                    new QuestDef
                    {
                        Id = "wolves", Title = "Wolves at the Gate", Type = QuestType.Kill, Target = "Dire Wolf", Count = 6,
                        Description = "Dire wolves have been stalking travellers on the north road through Whisperwood. " +
                                      "Thin the pack before they grow bold enough to test our walls.",
                        Objective = "Slay 6 Dire Wolves in Whisperwood (north).",
                        CompletionText = "Good work. The road north will be safer for it.",
                        RewardXp = 150, RewardGold = 40, RewardItemLevel = 3, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "goblins", Title = "The Goblin Menace", Type = QuestType.Kill, Target = "Goblin", Count = 10, MinLevel = 3,
                        Description = "A goblin warband has made camp to the east. They raid our farms by night. " +
                                      "Take the fight to them.",
                        Objective = "Slay 10 Goblins at the Goblin Encampment (east).",
                        CompletionText = "Ha! That'll teach the little wretches.",
                        RewardXp = 400, RewardGold = 90, RewardItemLevel = 7, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "bones", Title = "Bones in the Graveyard", Type = QuestType.Kill, Target = "Skeleton", Count = 10, MinLevel = 6,
                        Description = "The dead of the Forsaken Graveyard no longer rest. Something beneath the old crypt " +
                                      "is calling them. Put them back in the ground.",
                        Objective = "Destroy 10 Skeletons in the Forsaken Graveyard (south).",
                        CompletionText = "Then it is true... the Lich has returned.",
                        RewardXp = 900, RewardGold = 160, RewardItemLevel = 11, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "lich", Title = "The Lich King", Type = QuestType.Kill, Target = "Lich King", Count = 1, MinLevel = 12,
                        Description = "Deep in the Crypt of the Lich, at the southern end of the graveyard, an ancient evil stirs. " +
                                      "Destroy the Lich King and end this curse. Bring friends. Or potions. Lots of potions.",
                        Objective = "Defeat the Lich King in his crypt (far south).",
                        CompletionText = "You've done it! Hollowmere is in your debt, hero.",
                        RewardXp = 4000, RewardGold = 750, RewardItemLevel = 20, RewardRarity = Rarity.Legendary
                    },
                }
            },
            {
                "Forester Wren", new[]
                {
                    new QuestDef
                    {
                        Id = "timber", Title = "Timber!", Type = QuestType.Collect, Target = "Oak Logs", Count = 5,
                        Description = "Our palisade needs repairs. Take a few swings at the oak trees north of town and " +
                                      "bring me some logs. Click a tree to start chopping.",
                        Objective = "Bring 5 Oak Logs to Forester Wren.",
                        CompletionText = "Fine timber. You've a woodsman's arm.",
                        RewardXp = 120, RewardGold = 30
                    },
                    new QuestDef
                    {
                        Id = "fish", Title = "Supper by the Lake", Type = QuestType.Collect, Target = "Cooked Trout", Count = 4,
                        Description = "Catch trout at the lake in Whisperwood, then cook them on the campfire in the village square. " +
                                      "Try not to burn them.",
                        Objective = "Bring 4 Cooked Trout to Forester Wren.",
                        CompletionText = "Mmm. Almost as good as my mother's.",
                        RewardXp = 220, RewardGold = 45, RewardItemLevel = 5, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "willow", Title = "Bending Willows", Type = QuestType.Collect, Target = "Willow Logs", Count = 6, MinLevel = 4,
                        Description = "Willow makes the finest bows. You'll need Woodcutting level 8 to fell one.",
                        Objective = "Bring 6 Willow Logs to Forester Wren.",
                        CompletionText = "These will make fine bows for the guard.",
                        RewardXp = 600, RewardGold = 120, RewardItemLevel = 10, RewardRarity = Rarity.Rare
                    },
                }
            },
            {
                "Thomas", new[]
                {
                    new QuestDef
                    {
                        Id = "thomas_harvest", Title = "Stolen Harvest", Type = QuestType.Kill, Target = "Bandit", Count = 6, MinLevel = 2,
                        Description = "Bandits have been raiding my fields at night and hauling the sacks off toward the quarry road, " +
                                      "west of the village. I'm a farmer, not a fighter. Could you have a word with them? A firm word.",
                        Objective = "Slay 6 Bandits along the west road to Ironvein Quarry.",
                        CompletionText = "That'll teach 'em! Here, it isn't much, but it's honest coin.",
                        RewardXp = 300, RewardGold = 60, RewardItemLevel = 5, RewardRarity = Rarity.Magic
                    },
                }
            },
            {
                "Jenkins", new[]
                {
                    new QuestDef
                    {
                        Id = "jenkins_build", Title = "The Broken Build", Type = QuestType.Collect, Target = "Copper Ore", Count = 4,
                        Description = "The bell tower's clockwork has seized, and with it the nightly build. Pardon me: the nightly bell. " +
                                      "I shall need copper to replace the stripped gears. The quarry to the west should oblige.",
                        Objective = "Bring 4 Copper Ore to Jenkins.",
                        CompletionText = "Splendid. The build is green once more. I do so prefer it green.",
                        RewardXp = 160, RewardGold = 40
                    },
                    new QuestDef
                    {
                        Id = "jenkins_pipeline", Title = "Pests in the Pipeline", Type = QuestType.Kill, Target = "Goblin Shaman", Count = 4, MinLevel = 4,
                        Description = "Goblin shamans east of the village keep hexing my pipeline. Every job fails at the same stage. " +
                                      "I have retried them all twice. I believe the remedy is now... manual intervention.",
                        Objective = "Slay 4 Goblin Shamans at the Goblin Encampment (east).",
                        CompletionText = "All stages passing. You have my thanks, and this, with my compliments.",
                        RewardXp = 520, RewardGold = 110, RewardItemLevel = 8, RewardRarity = Rarity.Rare
                    },
                }
            },
            {
                "Smith Gorrin", new[]
                {
                    new QuestDef
                    {
                        Id = "copper", Title = "Ore for the Forge", Type = QuestType.Collect, Target = "Copper Ore", Count = 6,
                        Description = "My forge is cold and my ore bin empty. The Ironvein Quarry west of town has copper aplenty. " +
                                      "Use the anvil beside me to smith your own gear once you've learned the trade.",
                        Objective = "Bring 6 Copper Ore to Smith Gorrin.",
                        CompletionText = "Now that's proper ore. The forge sings again.",
                        RewardXp = 150, RewardGold = 40, RewardItemLevel = 4, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "iron", Title = "Iron Will", Type = QuestType.Collect, Target = "Iron Ore", Count = 6, MinLevel = 5,
                        Description = "Iron lies deeper in the quarry, guarded by golems. You'll need Mining level 8.",
                        Objective = "Bring 6 Iron Ore to Smith Gorrin.",
                        CompletionText = "Fine iron. I'll forge the guard new blades.",
                        RewardXp = 700, RewardGold = 150, RewardItemLevel = 12, RewardRarity = Rarity.Rare
                    },
                }
            },
        };
    }
}

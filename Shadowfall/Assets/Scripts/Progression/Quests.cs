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
            Sfx.Play2D("ui_confirm", 0.6f);
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

        /// <summary>Asks the server to pay the reward (it takes collected items and pays each quest once).</summary>
        public void TurnIn(QuestState q, Player p)
        {
            if (!q.IsReady(p)) return;
            NetClient.I?.Op("quest", k: q.Def.Id);
        }

        /// <summary>The server paid the gold (and the item, named here); experience is ours to give.</summary>
        public void TurnedIn(string id, Player p, string item, Rarity rarity)
        {
            var q = Get(id);
            if (q == null) return;
            MarkDone(id);
            p.AddXp(q.Def.RewardXp);
            p.Achievements.Max("quests", Completed.Count);
            if (!string.IsNullOrEmpty(item)) GameUI.Log("You receive: " + item, Item.RarityColor(rarity));
            NetClient.I?.SaveNow();
            Sfx.Play2D("quest_done", 0.8f);
            GameUI.Banner("Quest Complete: " + q.Def.Title, new Color(1f, 0.85f, 0.3f));
            // the fanfare: a ring of gold at the hero's feet, a shower of sparks, and the rewards flying into the bags
            SpellFx.Ring(p.transform.position + Vector3.up * 0.05f, new Color(1f, 0.85f, 0.3f), 3f, 0.7f);
            SpellFx.Hit(p.transform.position + Vector3.up * 1.6f, new Color(1f, 0.85f, 0.35f), false, 24);
            GameUI.RewardsToBags(GameUI.QuestRewardOrigin(p), Mathf.Clamp(q.Def.RewardGold / 15, 4, 12), string.IsNullOrEmpty(item) ? (Color?)null : Item.RarityColor(rarity));
            GameUI.Log("Quest complete: " + q.Def.Title + " (+" + q.Def.RewardXp + " xp, +" + q.Def.RewardGold + " gold)",
                new Color(1f, 0.85f, 0.3f));
        }

        /// <summary>Finished (the server says so): off the active list for good.</summary>
        public void MarkDone(string id)
        {
            Active.RemoveAll(q => q.Def.Id == id);
            Completed.Add(id);
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
                    // Jenkins, Butler of Automation, runs Hollowmere like a production system. Every quest is a DevOps
                    // chore taken painfully literally. Ids "jenkins_build" and "jenkins_pipeline" are kept from the old chain.
                    new QuestDef
                    {
                        Id = "jenkins_logs", Title = "Check the Logs", Type = QuestType.Collect, Target = "Oak Logs", Count = 5,
                        Description = "Good day. Last night the village went down for eleven minutes. The well stopped, the bell rang thirteen, " +
                                      "and a chicken achieved sentience. The incident report contains a single line of advice: " +
                                      "\"Check the logs.\" I have searched the manor top to bottom and I own no logs whatsoever. " +
                                      "Be a dear and fetch some from Whisperwood, so that I may check them.",
                        Objective = "Bring 5 Oak Logs to Jenkins so he can check them.",
                        CompletionText = "*taps each log, holds one to his ear* Oak log one: no errors. Oak log two: no errors. Oak log three: " +
                                         "a beetle, severity WARN. Logs four and five: bark, bark. Verdict: the logs are fine. The incident remains a mystery. " +
                                         "I shall mark it resolved anyway. That is what we do.",
                        RewardXp = 120, RewardGold = 25
                    },
                    new QuestDef
                    {
                        Id = "jenkins_scaling", Title = "Scaling Horizontally", Type = QuestType.Kill, Target = "Dire Wolf", Count = 8, MinLevel = 2,
                        Description = "Monitoring reports a worrying trend: the wolf population is scaling horizontally across the north meadow. " +
                                      "Eight new instances since breakfast, all of them hungry, none of them load balanced. " +
                                      "I would like you to scale them back down to zero. Gracefully, if possible. Ungracefully is also acceptable.",
                        Objective = "Scale down 8 Dire Wolves in Whisperwood (north).",
                        CompletionText = "Wolf replicas: zero. Desired state reached. I have set an alert for when they autoscale again, which will be Tuesday.",
                        RewardXp = 220, RewardGold = 45
                    },
                    new QuestDef
                    {
                        Id = "jenkins_containers", Title = "Containerization", Type = QuestType.Collect, Target = "Raw Trout", Count = 5, MinLevel = 2,
                        Description = "A consultant from the capital insists everything must be containerized. Everything. " +
                                      "I have containerized the cutlery, the cat (under protest), and the vicar. " +
                                      "Only the fish remain in their legacy environment: the lake. Please migrate five trout into my barrels. " +
                                      "It is called a lift and shift. You lift the fish. The fish shifts. Violently.",
                        Objective = "Catch 5 Raw Trout and bring them to Jenkins for containerization.",
                        CompletionText = "Five trout, each in its own barrel, isolated from its neighbours, sharing nothing. " +
                                         "They seem miserable. The consultant calls that 'stateless'. Splendid.",
                        RewardXp = 260, RewardGold = 50
                    },
                    new QuestDef
                    {
                        Id = "jenkins_hotfix", Title = "Hotfix in Production", Type = QuestType.Collect, Target = "Cooked Trout", Count = 3, MinLevel = 3,
                        Description = "Production is down. By 'production' I mean the Captain, and by 'down' I mean hungry, which in a guard captain " +
                                      "is a critical severity incident. There is no time for the proper release process. " +
                                      "We need a hot fix. Something hot. Cook three trout at a campfire and we shall deploy them straight to his face.",
                        Objective = "Cook 3 trout and bring the hot fix to Jenkins.",
                        CompletionText = "Deployed directly to production without testing. The Captain is stable. " +
                                         "Should anyone ask, it went through code review. You reviewed it. You said 'smells fine'.",
                        RewardXp = 300, RewardGold = 60, RewardItemLevel = 4, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "jenkins_pipeline", Title = "Flaky Tests", Type = QuestType.Kill, Target = "Goblin Shaman", Count = 4, MinLevel = 4,
                        Description = "Our tests have become flaky. One moment the bridge holds, the next it does not, and nobody touched it. " +
                                      "I traced the flakiness to goblin shamans east of the village, hexing the pipeline at random. " +
                                      "I have retried every stage twice. The remedy, I'm afraid, is to remove the source of non-determinism. Permanently.",
                        Objective = "Remove 4 Goblin Shamans (sources of flakiness) at the Goblin Encampment (east).",
                        CompletionText = "All tests passing, three runs in a row. I am deeply suspicious. But green is green, and I shall take it.",
                        RewardXp = 520, RewardGold = 110, RewardItemLevel = 8, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "jenkins_bluegreen", Title = "Blue-Green Deployment", Type = QuestType.Collect, Target = "Mana Potion", Count = 3, MinLevel = 4,
                        Description = "The village council has approved a blue-green deployment. I have the green: it is the meadow. " +
                                      "I lack the blue. Three Mana Potions, please, from Merchant Lysa or a goblin's corpse; " +
                                      "procurement is a perfectly valid deployment strategy. Then we switch all traffic from green to blue. " +
                                      "Traffic, in this case, is Thomas's goat.",
                        Objective = "Bring 3 Mana Potions (the blue) to Jenkins.",
                        CompletionText = "The goat has been routed to blue. The goat drank blue. The goat now speaks Elvish. " +
                                         "Rolling back is impossible, so I shall call this a feature.",
                        RewardXp = 360, RewardGold = 80
                    },
                    new QuestDef
                    {
                        Id = "jenkins_cache", Title = "Cache Invalidation", Type = QuestType.Kill, Target = "Bandit", Count = 6, MinLevel = 5,
                        Description = "There are only two hard problems in this village: cache invalidation and naming goats. " +
                                      "The bandits west of here keep a cache of stolen grain, and it is badly out of date: it still lists " +
                                      "Thomas's harvest as theirs. Invalidate the cache. Invalidate the bandits too, while you're there. " +
                                      "I have named the goat 'Goat 2 Final (Copy)'. That problem is solved.",
                        Objective = "Invalidate 6 Bandits (and their cache) near the Ironvein Quarry.",
                        CompletionText = "Cache invalidated. Thomas's grain is fresh again, from the farm's point of view. Eventually consistent, as they say.",
                        RewardXp = 480, RewardGold = 100
                    },
                    new QuestDef
                    {
                        Id = "jenkins_build", Title = "The Broken Build", Type = QuestType.Collect, Target = "Copper Ore", Count = 6, MinLevel = 5,
                        Description = "The nightly build has failed. The nightly build is the bell tower: it is built every night, by me, from scratch, " +
                                      "because I do not trust yesterday's bell tower. Tonight it failed at the 'gears' stage: missing dependency, copper. " +
                                      "Six copper ore from the quarry should satisfy the dependency resolver. And the resolver is me.",
                        Objective = "Bring 6 Copper Ore to Jenkins to fix the build.",
                        CompletionText = "Build #4128: SUCCESS. Do not touch anything. Do not breathe near the bell. Do not, under any circumstances, update the copper.",
                        RewardXp = 420, RewardGold = 90
                    },
                    new QuestDef
                    {
                        Id = "jenkins_kill9", Title = "kill -9", Type = QuestType.Kill, Target = "Zombie", Count = 9, MinLevel = 6,
                        Description = "Some processes refuse to terminate. They shamble about the graveyard, consuming resources, ignoring polite signals. " +
                                      "I asked them to stop, very nicely. That was SIGTERM. They did not stop. " +
                                      "We are now past the point of nicely. The documentation is clear: kill dash nine. Nine kills, precisely. Not eight. Nine.",
                        Objective = "Send SIGKILL to 9 zombie processes in the Forsaken Graveyard (south).",
                        CompletionText = "Nine zombie processes reaped. The graveyard's memory usage has dropped dramatically. " +
                                         "If any come back, that is a 'restart policy' and frankly not my department.",
                        RewardXp = 700, RewardGold = 140, RewardItemLevel = 9, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "jenkins_rotation", Title = "Log Rotation", Type = QuestType.Collect, Target = "Willow Logs", Count = 6, MinLevel = 7,
                        Description = "The oak logs you fetched are now dangerously old. Retention policy says logs older than one week must be rotated: " +
                                      "the old ones archived (in the fireplace), new ones brought in. Policy also says the new ones must be willow, " +
                                      "because willow logs are more verbose. They creak.",
                        Objective = "Bring 6 Willow Logs to Jenkins for the log rotation.",
                        CompletionText = "*places the willow logs in a neat row, throws the oak in the fire* Rotation complete. " +
                                         "Nobody will ever read these either. That is the natural lifecycle of a log.",
                        RewardXp = 760, RewardGold = 150
                    },
                    new QuestDef
                    {
                        Id = "jenkins_postmortem", Title = "Blameless Post-Mortem", Type = QuestType.Kill, Target = "Skeleton", Count = 8, MinLevel = 7,
                        Description = "After every incident we hold a post-mortem. This one is literal: the skeletons in the graveyard caused last night's outage, " +
                                      "and I should like to examine them. The process is blameless. Nobody is at fault. " +
                                      "However, they are all going to be disassembled.",
                        Objective = "Conduct a post-mortem on 8 Skeletons in the Forsaken Graveyard.",
                        CompletionText = "Findings: root cause, insufficient flesh. Contributing factors: the Lich. Action items: bring a bigger hammer. " +
                                         "I have filed the report where all reports go, under a candle, unread.",
                        RewardXp = 900, RewardGold = 170
                    },
                    new QuestDef
                    {
                        Id = "jenkins_monolith", Title = "Breaking Up the Monolith", Type = QuestType.Kill, Target = "Rock Golem", Count = 5, MinLevel = 9,
                        Description = "The architects agree: the golems of the quarry are monoliths. Enormous, tightly coupled, " +
                                      "impossible to deploy and they fall over when you change one rock. " +
                                      "Please decompose five of them into microservices. Pebbles. I mean pebbles.",
                        Objective = "Decompose 5 Rock Golems in the Ironvein Quarry (west) into microservices.",
                        CompletionText = "Five monoliths, now several thousand pebbles, each with a single responsibility: being a pebble. " +
                                         "They communicate only by rolling into each other. Latency is terrible. Everyone is very proud.",
                        RewardXp = 1300, RewardGold = 240, RewardItemLevel = 12, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "jenkins_leastprivilege", Title = "Principle of Least Privilege", Type = QuestType.Kill, Target = "Goblin Warchief", Count = 1, MinLevel = 10,
                        Description = "The security audit found the root cause of our troubles: one goblin with admin rights over the entire east. " +
                                      "He granted himself the role. Nobody reviewed it. He can raid, burn and requisition chickens at will. " +
                                      "Please revoke his privileges. All of them. Including the privilege of standing upright.",
                        Objective = "Revoke the Goblin Warchief's access (east).",
                        CompletionText = "Access revoked. The goblins now require a signed form to raid anything, and I have hidden all the quills. " +
                                         "This is called 'defense in depth'.",
                        RewardXp = 1800, RewardGold = 320, RewardItemLevel = 13, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "jenkins_chaos", Title = "Chaos Engineering", Type = QuestType.Kill, Target = "Crypt Lord", Count = 1, MinLevel = 12,
                        Description = "To be truly resilient, one must inject failure deliberately. I propose we inject you, deliberately, " +
                                      "into the deepest level of the Catacombs, and see whether the Crypt Lord fails gracefully. " +
                                      "My hypothesis: he will not. My runbook: you hit him until he does. " +
                                      "Should you perish, that too is valuable data.",
                        Objective = "Run the chaos experiment on the Crypt Lord, at the bottom of the Catacombs.",
                        CompletionText = "Experiment complete. Result: the Crypt Lord is not fault tolerant. Hollowmere's uptime is now three nines: " +
                                         "nine, nine, and a chicken. Thank you, truly. Please accept this, and a pager. You are on call now. Forever.",
                        RewardXp = 3200, RewardGold = 600, RewardItemLevel = 16, RewardRarity = Rarity.Legendary
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
            // ---- the outer lands
            {
                "Woodsman Garrick", new[]
                {
                    new QuestDef
                    {
                        Id = "pine_wolves", Title = "White Fangs", Type = QuestType.Kill, Target = "Frost Wolf", Count = 10, MinLevel = 12,
                        Description = "Frost wolves have come down from the high snow and taken two of my mules. They'll take a woodsman next. Thin the pack in the pines north of here.",
                        Objective = "Slay 10 Frost Wolves in the Frostpeak Wilds.",
                        CompletionText = "Ten pelts. The mules thank you. The ones that are left.",
                        RewardXp = 1600, RewardGold = 220, RewardItemLevel = 14, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "pine_yew", Title = "Old Growth", Type = QuestType.Collect, Target = "Yew Logs", Count = 6, MinLevel = 12,
                        Description = "The yews up here are older than Hollowmere and twice as stubborn. Fell me a few; Frosthaven pays well for bow staves.",
                        Objective = "Bring 6 Yew Logs to Woodsman Garrick.",
                        CompletionText = "Good straight grain. You've an eye for timber.",
                        RewardXp = 1400, RewardGold = 200, RewardItemLevel = 14, RewardRarity = Rarity.Magic
                    },
                }
            },
            {
                "Jarl Sigrun", new[]
                {
                    new QuestDef
                    {
                        Id = "frost_wraiths", Title = "Cold Light", Type = QuestType.Kill, Target = "Ice Wraith", Count = 8, MinLevel = 14,
                        Description = "Blue lights drift between the pines at night, and whoever follows them is found frozen at dawn. They are wraiths, bound to the ice. Break them.",
                        Objective = "Destroy 8 Ice Wraiths in the Frostpeak Wilds.",
                        CompletionText = "The nights are darker now. That is a kindness, up here.",
                        RewardXp = 2200, RewardGold = 300, RewardItemLevel = 16, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "frost_giants", Title = "Bigger Than My Hall", Type = QuestType.Kill, Target = "Frost Giant", Count = 4, MinLevel = 15,
                        Description = "Giants walk the highlands north and west of here, and they have started walking toward us. Show them the way back.",
                        Objective = "Slay 4 Frost Giants in the Frostpeak highlands.",
                        CompletionText = "Four! The skalds will need a longer song.",
                        RewardXp = 3000, RewardGold = 420, RewardItemLevel = 17, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "frost_jarl", Title = "The Frostborn", Type = QuestType.Kill, Target = "Jarl Frostborn", Count = 1, MinLevel = 17,
                        Description = "Their chieftain calls himself Jarl, as if he had a hall and a people. He has a camp in the far north-west and a hunger that never ends. End it.",
                        Objective = "Defeat Jarl Frostborn at his camp in the far north-west of the Frostpeak Wilds.",
                        CompletionText = "There is only one Jarl in the north now. Frosthaven owes you its spring.",
                        RewardXp = 6000, RewardGold = 900, RewardItemLevel = 20, RewardRarity = Rarity.Legendary
                    },
                }
            },
            {
                "Caravan Master Rahim", new[]
                {
                    new QuestDef
                    {
                        Id = "salt_raiders", Title = "Bandits of the Sun", Type = QuestType.Kill, Target = "Desert Raider", Count = 10, MinLevel = 13,
                        Description = "Raiders ride out from camps deep in the Badlands and fall on every caravan. My drivers won't leave the walls. Give them a reason to.",
                        Objective = "Slay 10 Desert Raiders in the Sunscar Badlands.",
                        CompletionText = "My drivers are packing the camels. They're even smiling.",
                        RewardXp = 2000, RewardGold = 280, RewardItemLevel = 15, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "salt_golems", Title = "Walking Dunes", Type = QuestType.Kill, Target = "Sand Golem", Count = 5, MinLevel = 15,
                        Description = "Some of the dunes get up and walk. Sand golems, the old women call them, and they crush anything on the road. Five of them would make the salt route safe.",
                        Objective = "Destroy 5 Sand Golems in the Sunscar Badlands.",
                        CompletionText = "The road is clear and my salt moves again. You have my gratitude, and a share.",
                        RewardXp = 2800, RewardGold = 400, RewardItemLevel = 17, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "salt_warlord", Title = "The Warlord", Type = QuestType.Kill, Target = "Raider Warlord", Count = 1, MinLevel = 17,
                        Description = "Every raider answers to one man, a warlord who camps in the far south-east of the Badlands. Cut off the head and the body will scatter.",
                        Objective = "Defeat the Raider Warlord at his camp in the south-east of the Sunscar Badlands.",
                        CompletionText = "Saltreach will remember your name. The camels too.",
                        RewardXp = 6000, RewardGold = 900, RewardItemLevel = 20, RewardRarity = Rarity.Legendary
                    },
                }
            },
            {
                "Commander Varek", new[]
                {
                    new QuestDef
                    {
                        Id = "ash_ghouls", Title = "Smouldering Dead", Type = QuestType.Kill, Target = "Ash Ghoul", Count = 12, MinLevel = 17,
                        Description = "The ash dead crawl out of the ruins every night and scratch at our gate until dawn. Every one you put down is one less at the wall.",
                        Objective = "Destroy 12 Ash Ghouls in the Ashen Reach.",
                        CompletionText = "The gate gets to rest tonight. So do my soldiers.",
                        RewardXp = 3200, RewardGold = 450, RewardItemLevel = 19, RewardRarity = Rarity.Magic
                    },
                    new QuestDef
                    {
                        Id = "ash_skeletons", Title = "Embers", Type = QuestType.Kill, Target = "Ember Skeleton", Count = 10, MinLevel = 18,
                        Description = "Skeletons that burn and never burn out. They guard the old ruins as if the kingdom never fell. Show them it did.",
                        Objective = "Destroy 10 Ember Skeletons in the Ashen Reach.",
                        CompletionText = "Good. The ruins are quieter. Not quiet. Quieter.",
                        RewardXp = 3800, RewardGold = 520, RewardItemLevel = 20, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "ash_golems", Title = "Cinders", Type = QuestType.Kill, Target = "Cinder Golem", Count = 5, MinLevel = 19,
                        Description = "Cinder golems carry the heat of whatever burned this land. They're slow. They're also nearly impossible to kill. Nearly.",
                        Objective = "Destroy 5 Cinder Golems in the Ashen Reach.",
                        CompletionText = "Five! I'd have bet on none. I'd have lost.",
                        RewardXp = 4500, RewardGold = 600, RewardItemLevel = 21, RewardRarity = Rarity.Rare
                    },
                    new QuestDef
                    {
                        Id = "ash_king", Title = "The Ashen King", Type = QuestType.Kill, Target = "The Ashen King", Count = 1, MinLevel = 21,
                        Description = "In the heart of the Reach, a king still sits on a throne of ash and gives orders to the dead. As long as he does, the Reach will never cool. Unseat him.",
                        Objective = "Defeat the Ashen King in the heart of the Ashen Reach.",
                        CompletionText = "The ash is settling. For the first time since I came here, I can see the stars. Thank you.",
                        RewardXp = 9000, RewardGold = 1500, RewardItemLevel = 24, RewardRarity = Rarity.Legendary
                    },
                }
            },
        };
    }
}

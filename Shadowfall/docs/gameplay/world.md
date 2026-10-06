# The world

The map is 160 × 160 tiles, with the walled village of **Hollowmere** (45 × 45 tiles) at its centre. Cobbled cross streets lead from the four gates to the central square with the well; around them are grassy yards with the tavern, homes, the smithy, a church with a fountain plaza, a windmill, a market hall, a fenced farm plot and a guards' training yard. A road leaves each gate. Monsters never follow you inside the walls.

| Zone | Direction | Monsters (level) | Resources |
| --- | --- | --- | --- |
| **Hollowmere Village** | Centre | Safe zone | Anvil (Smithing), campfire (Cooking), vendor, healer, quest givers |
| **Whisperwood** | North | Dire Wolf (1–7), Bandit | Oak → Willow → Yew trees further north; trout and salmon lakes |
| **Goblin Encampment** | East | Goblin, Goblin Shaman (3–9), **Goblin Warchief** (10, boss) | Campfire |
| **Ironvein Quarry** | West | Bandit (3–5), Rock Golem (8–15) | Copper → Iron → Mithril rocks further west |
| **Forsaken Graveyard** | South | Skeleton, Skeleton Archer, Zombie (6–11) | — |
| **Crypt of the Lich** | Far south | Skeletons (11–13), **Lich King** (16, raid boss) | — |

## NPCs

| NPC | Role |
| --- | --- |
| Captain Aldric | Main quest line: wolves → goblins → skeletons → the Lich King |
| Forester Wren | Woodcutting and fishing quests |
| Smith Gorrin | Mining quests. Stands next to the anvil |
| Merchant Lysa | Sells potions and buys anything |
| Armorer Brann | Sells armor for your level (restocks every 10 minutes) |
| Weaponsmith Hilda | Sells weapons for your level (restocks every 10 minutes) |
| Innkeeper Rosie | Food and drink: bread, cooked fish, hearty stew, mulled wine (restores mana) |
| Curio Dealer Vex | Magic and rare rings and amulets (restocks every 10 minutes), Chipped gems, gem fusing |
| Beastmaster Orla | Companions for hire, by the east road (see [Companions](progression.md#companions)) |
| Sister Mae | Restores your health and mana for free |
| Thomas | Farmer by the west houses. Wants the quarry-road bandits dealt with |
| Jenkins | The village's butler of automation, dressed like the Jenkins mascot. Quests about his broken build and pipeline |

Every vendor buys your loot: right-click an item in your bags while trading.

NPCs greet you when you walk up and talk among themselves in speech bubbles. Villagers stroll around the square (and go home after dark), two guards patrol between the gates with torches at night, and a hound roams the village.

## Day and night

A full day takes **48 minutes** (2 real minutes per in-game hour) and follows the server's clock, so everyone sees the same sunset. The time is shown under the minimap. Nights are dark and blue: lanterns and windows light up, your torch burns brighter and reaches further, fireflies come out in Whisperwood and bats replace the crows.

## Parties

Click another player's name above their head and choose **Invite to Party**, or type `/invite name`. Up to 5 players per party.

- **Shared kills:** party members within 60 m of a monster when it dies get the kill: XP, quest progress and their own loot roll.
- **Shared quests:** open the quest log (++l++) and click **Share** to offer a quest to your party. A shared quest can be turned in to its giver even if it's later in their quest chain.
- **Chat:** `/p message` talks to your party. `/w name message` whispers, `/r message` replies to the last whisper, `/leave` leaves the party.
- The party leader can remove members with the **x** on their party frame.

## The Catacombs (dungeon)

An old crypt in the south-east of the Forsaken Graveyard, glowing orange, leads into **the Catacombs**: three levels of randomly generated rooms and corridors under the graveyard.

- **Your own copy:** every party (or solo hero) gets its own Catacombs; party members who walk in join the same one. A new layout is generated each time, and an empty dungeon closes two minutes after the last player leaves.
- **Monsters:** skeletons, skeleton archers and zombies scaled to your party's level and the depth, with more elites than outside. Every room holds a pack; some rooms are led by an elite.
- **Treasure:** chests in side rooms hold gold and a magic or rare item (each hero opens their own).
- **Getting around:** a blue portal by the entrance of every level returns you to Hollowmere; the orange stairs in the farthest room lead one level deeper.
- **The Crypt Lord** waits in the farthest room of depth 3: a giant skeleton who hits hard, blasts frost and calls up his guard at half health.
- Dying in the Catacombs sends you back to Hollowmere. Logging out inside puts you back at the entrance next time.

## Elite monsters

About one in twelve monsters spawns as an **elite champion**: a named monster (for example *Gorefang the Cruel*) shown with a blue name, a glowing aura and its affixes under its health bar. Elites are bigger, two levels higher, have about three times the health and hit harder. They give three times the XP or more, and always drop a pile of gold plus two or three magic items, with a good chance of rare and a small chance of legendary.

| Affix | Effect |
| --- | --- |
| Fast | Moves 50% faster |
| Vampiric | Heals itself when it hits you |
| Fire Enchanted | Explodes when it dies: step away from the corpse |
| Teleporter | Blinks next to you when you keep your distance |
| Shielding | Becomes immune to damage for 3 seconds now and then (blue rings, "Immune") |
| Mighty | Hits much harder |
| Extra Health | Much more health |

## Monsters

Monsters live on the server, so every player sees the same ones. They:

- wander near their spawn point, then **aggro** when a player comes close, pulling their nearby friends with them;
- chase using A* pathfinding around trees, walls and water;
- **leash** back home and regenerate if dragged too far, or if their target escapes into the village;
- respawn 30 seconds after death (bosses take 90–180 seconds), but never right on top of a player.

**The Lich King** shoots frost bolts, casts a Frost Nova when you're close, and raises four skeletons at half health.

Everyone who damages a monster gets **full XP and quest credit**. Each player then rolls **their own loot**, which only they can see.

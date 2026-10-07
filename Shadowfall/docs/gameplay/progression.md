# Items & progression

## Character level

Monsters and quests give XP. Each level grants **5 attribute points** (spend them in the character window, ++c++), **1 talent point** (++t++) and refills your health and mana. Each class starts with different attributes (the Barbarian is strongest, the Mage smartest, the Rogue quickest, the Knight toughest).

| Attribute | Effect |
| --- | --- |
| Strength | +2% weapon damage per point for Knights and Barbarians; Knight holy power |
| Dexterity | +0.15% critical hit chance and +0.25 armor per point; +2% weapon damage per point for Rogues |
| Intelligence | +2.5% spell damage and +3 mana per point; +2% weapon damage per point for Mages; Knight holy power |
| Vitality | +6 life per point |

Armor reduces incoming damage by `armor / (100 + armor)`.

## Classes and abilities

Your class is picked on the login screen when the character is **created** and stays with it. Each class has its own five abilities on ++1++–++5++; the right mouse button casts ability 2. Abilities unlock at levels 1, 1, 3, 5 and 8.

| Class | Main attribute | 1 | 2 / RMB | 3 (lv 3) | 4 (lv 5) | 5 (lv 8) |
| --- | --- | --- | --- | --- | --- | --- |
| Knight | Strength + Vitality | Shield Bash: 140% weapon damage, stuns 1.5 s | Holy Bolt: projectile, heals 3% life per hit | Consecration: burning holy ground that heals you | Divine Shield: 50% less damage for 6 s, heals 20% | Judgement: hammer of light, area damage + stun |
| Barbarian | Strength | Cleave: 170% weapon damage in an arc | Throwing Axe: 110% weapon damage | Whirlwind: spin for 2.5 s while moving, 60% weapon damage per tick | Leap: jump up to 10 m, 200% weapon damage slam + slow | War Cry: +40% damage, +30% armor for 10 s |
| Mage | Intelligence | Chain Lightning: jumps to 4 more enemies | Fireball: exploding projectile | Frost Nova: damage + 50% slow | Teleport: blink up to 12 m | Meteor: huge delayed area hit |
| Rogue | Dexterity | Twin Strike: two quick 90% hits, +15% crit | Multishot: fan of 5 arrows, 75% each | Fan of Knives: 120% all around + slow | Smoke Bomb: monsters lose you for 4 s, heal 10% | Rain of Arrows: 3 s of arrows on an area |

Weapon attacks scale with the class's main attribute (+2% per point); spells with Intelligence (and, for the Knight's holy spells, Strength + Intelligence). Stuns are sent to the server (`stun`), which freezes the monster; bosses shrug off 60% of a stun. Smoke Bomb tells the server to hide you (`vanish`): monsters drop you as a target and ignore you until it ends.

## Talents

You get **one talent point per level** from level 2. Open the talent window with ++t++. Each class has six talents with 3–5 ranks: three general passives (life, armor, damage, crit, mana regeneration, movement speed) and three that improve specific abilities (longer stuns, bigger Consecration, extra Multishot arrows, shorter Teleport cooldown and so on). Talents can be reset in Hollowmere for 25 gold per level.

## Buffs

Divine Shield, War Cry and Smoke Bomb show as icons above the action bar with their remaining time; hover for details. While they last, your hero also wears an aura: rising red embers for War Cry, a golden swirl for Divine Shield, wisps of smoke for Smoke Bomb.

## Loot

Equipment drops in eight slots: weapon, helm, chest, gloves, legs, boots, ring and amulet. Every tooltip starts with the item's **type line** in its rarity colour (for example *LEGENDARY WAR AXE* or *MAGIC RING*), then its slot, item level and rarity tier (1 of 5 to 5 of 5). Rare and better items on the ground also show their rarity next to their name.

| Rarity | Tier | Color | Affixes | Notes |
| --- | --- | --- | --- | --- |
| Common | 1 of 5 | White | 0 | Base item |
| Magic | 2 of 5 | Blue | 1–2 | Prefix/suffix name, e.g. *Swift Broadsword of the Bear* |
| Rare | 3 of 5 | Yellow | 3–4 | Random name, e.g. *Doom Bite*, short loot beam |
| Set | 4 of 5 | Green | 4 | One of four class sets (helm, chest, gloves, boots), tall loot beam |
| Legendary | 5 of 5 | Orange | 5 | Unique name, boosted stats, a **legendary power**, tall loot beam |

Possible affixes: Strength, Dexterity, Intelligence, Vitality, Life, Mana, Armor, Critical Chance, Attack Speed, Life per Hit, Movement Speed, Life Regeneration and Mana Regeneration.

Item level equals the monster's level. Higher item levels roll bigger numbers and better base types, and you need roughly the item's level to equip it. Bosses always drop Rare or better, and sometimes a set piece. Elites drop 1–2 magic-or-better items.

### Where the good loot is

Loot is scarce, and where it comes from matters more than how much you kill:

| Source | Gear | Best it can be |
| --- | --- | --- |
| Ordinary monsters in the open world | about 1 kill in 14 | Magic |
| Ordinary monsters in dungeons and the Crypt of the Lich | about 1 kill in 8 (more on harder difficulties) | anything, but legendaries are very rare |
| Elites | 1–2 pieces, magic or better | Rare in the open world; anything in dungeons and the Crypt |
| Dungeon treasure chests | 1–2 pieces, magic or better, often rare | anything |
| Bosses | 2–4 pieces, at least one rare | the best chance at legendaries and set pieces |

Harder dungeon difficulties (Veteran, Nightmare, Hell) raise every roll.

### Legendary powers

Legendaries usually roll a power for the class that found them:

| Power | Class |
| --- | --- |
| Fireball splits into three | Mage |
| Whirlwind slows everything it hits | Barbarian |
| Multishot fires 3 extra arrows | Rogue |
| Divine Shield stuns nearby enemies for 2 s | Knight |
| Killed enemies explode for 60% weapon damage | Any |
| Heal 5% life on kill | Any |
| Every 5th hit unleashes chain lightning | Any |
| +25% damage to elites and bosses | Any |

### Sets

| Set | Class | 2 pieces | 4 pieces |
| --- | --- | --- | --- |
| Lightbringer's Oath | Knight | +15% armor, +10% life | Holy Bolt, Consecration and Judgement +40% damage |
| Wrath of the Ancients | Barbarian | +12% attack speed | Whirlwind +60% damage and 1 s longer |
| Regalia of the Tempest | Mage | +30% mana regeneration | Chain Lightning jumps 3 more times, +30% damage |
| Nightstalker's Garb | Rogue | +8% critical chance | Multishot and Rain of Arrows +50% damage |

Set pieces drop for your own class 75% of the time. The tooltip shows which pieces you wear and which bonuses are active.

### Gems and sockets

Weapons, helms, chests and legs can roll **0–2 sockets** (shown as small diamonds on the slot). Gems drop from elites, bosses and occasionally normal monsters, and Curio Dealer Vex sells Chipped gems.

| Gem | Stat | Chipped / Flawless / Perfect |
| --- | --- | --- |
| Ruby | Strength | +3 / +6 / +10 |
| Emerald | Dexterity | +3 / +6 / +10 |
| Sapphire | Intelligence | +3 / +6 / +10 |
| Topaz | Vitality | +3 / +6 / +10 |
| Diamond | Maximum life | +12 / +24 / +40 |

Click a gem in your bags, then click an item (in the bags or worn) with an empty socket. Vex fuses three gems of the same kind and quality into the next quality (50 gold for Flawless, 250 for Perfect).

## Companions

Beastmaster Orla, by Hollowmere's east road, hires out companions. You pay once; after that you can summon any companion you own from her for free. One follows you at a time, and the active one is saved with your character.

| Companion | Price | Level | Fights with |
| --- | --- | --- | --- |
| War Hound | 300 | 1 | Fast bites that often slow |
| Squire Edric | 800 | 4 | Sword and shield; stuns a foe every 7 s |
| Hedge Witch Nell | 1,400 | 7 | Fireballs that burn around the target |
| Ranger Kestrel | 2,000 | 10 | Two arrows per shot |
| Acolyte Mira | 2,800 | 12 | Holy bolts; heals you for 10% life when you drop below 70% |
| Stone Golem | 5,000 | 15 | Slow ground slams that hit everything nearby |

Companions grow 12% stronger per hero level, follow you into dungeons, attack what you attack (or whatever is nearest) and rest while you are in town. Monsters don't target them, so they never die. Their hits count as yours for kills and loot. Other players see your companion following you.

Each companion carries its own gear (the squire's helm and shield, the witch's hat and staff, the ranger's crossbow, the acolyte's wand and spellbook) and has its own look in a fight: fire in the witch's hand, light motes around the acolyte, glowing runes on the golem, and a different effect for every attack. See [Art & UI → Companions](../development/art.md#companions).

## Stash

The stash chest by the village square holds 40 items and is saved with your character. Click it to open it; right-click items in your bags to store them and click stashed items to take them back.

## Trading

Right-click another player (anywhere on their character, or click their name) and choose **Trade** (you must be within 10 m). Right-click items in your bags to offer them (they stay in your bags, marked **TRADE**, until the trade completes), set an amount of gold, then press **Accept**. Any change to either offer resets both acceptances; the trade completes when both players accept the same offers. Moving apart, entering a dungeon or logging out cancels it.

Gold and items are kept by the server: loot is rolled for you alone (nobody else sees your drops, so there's no fighting over them), and selling, buying, crafting, quest rewards and trades are all carried out there. Dying costs a tenth of your gold.

## Professions

Professions use RuneScape's experience curve: level 2 needs 83 XP, level 99 needs about 13 million.

| Skill | Where | Tiers (level required) |
| --- | --- | --- |
| Woodcutting | Whisperwood trees | Oak (1), Willow (8), Yew (15) |
| Mining | Ironvein Quarry rocks | Copper (1), Iron (8), Mithril (15) |
| Fishing | Whisperwood lakes | Trout (1), Salmon (8) |
| Smithing | Anvil in Hollowmere | 3 ore → a random piece of gear. Higher Smithing level makes better gear |
| Cooking | Campfires | Raw fish → healing food. Higher Cooking level burns less |

Your success chance per attempt grows as your level climbs above the node's requirement. Trees and rocks are used up after a few harvests and grow back after 10–20 seconds.

## Saving

Characters are stored **on the server**. There are no local saves. The client sends a snapshot every 20 seconds, on level-up, on quest completion and when you quit. The server also records your position whenever you disconnect.

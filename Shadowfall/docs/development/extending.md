# Extending the game

## Add a monster

1. **Stats and spawns (server):** add an entry to `MONSTERS` in `server/content.js`, and one or more rows to `SPAWNERS`:

    ```js
    "Cave Troll": { hp: 300, dmg: 24, speed: 3.2, range: 2.2, cd: 1.8, xp: 140, aggro: 9, armor: 25 },
    // [x, z, count, minLevel, maxLevel, [types], radius?, respawnSeconds?]
    [30, 130, 3, 13, 15, ["Cave Troll"]],
    ```

2. **Looks (client):** add an `EnemyDef` with the **same name** to `EnemyDef.All` in `Assets/Scripts/Characters/Enemy.cs`:

    ```csharp
    new EnemyDef { Name = "Cave Troll", Shape = EnemyShape.Golem, Color = new Color(0.4f, 0.5f, 0.35f),
                   Secondary = new Color(0.3f, 0.35f, 0.25f), Scale = 1.5f },
    ```

    Set `Ranged = true` and a `ProjectileColor` for monsters that shoot. Ranged monsters also need `ranged: true` on the server.

    To give it a 3D model instead of the primitive fallback, add an entry with the same name to the `monsters` dictionary of `CharacterLook` in `Assets/Scripts/Characters/CharacterView.cs` (see [Art & UI](art.md#how-models-are-used)).

3. Rebuild the client and restart the server (`task deploy`).

## Add a quest

Quests are defined on the client in `Assets/Scripts/Progression/Quests.cs`, as chains keyed by NPC name. Kill quests count `kill` messages whose monster name matches `Target`. Collect quests count items in your bags and take them when you turn the quest in.

```csharp
new QuestDef
{
    Id = "trolls", Title = "Troll Trouble", Type = QuestType.Kill, Target = "Cave Troll", Count = 5, MinLevel = 12,
    Description = "...", Objective = "Slay 5 Cave Trolls.", CompletionText = "...",
    RewardXp = 1500, RewardGold = 200, RewardItemLevel = 15, RewardRarity = Rarity.Rare
},
```

Then run `task gamedata`: the server pays the gold and item reward from `server/gamedata.json`, extracted from this file (the smoke test fails while it is out of date). Companion prices and levels come from `Companion.cs` the same way.

!!! warning "Quest ids are saved"
    Completed and active quests are stored by `Id`, so don't rename ids that players may already have.

## Add an achievement

Add an `AchievementDef` to `AchievementDatabase.All` in `Assets/Scripts/Progression/Achievements.cs`: it is earned when the
counter `Stat` reaches `Goal`. Use an existing counter (`kills`, `elites`, `boss.<name>`, `level`, `skill.<skill>`, `quests`,
`gold`, `explored`, `zone`, `dungeon`, `trades`...) or count a new one where it happens with `Player.I.Achievements.Add(...)`,
`Max(...)` or `Once(...)`. Give it an `Icon` (an `ach_*` badge from `tools/ui/render_icons.js`) and optionally a `Title`.
Then run `task gamedata` so the server knows its name and title. Don't rename ids: they're saved.

## Add an NPC

Call `Npc.Create(...)` in `WorldGenerator.BuildTown()`. Roles are `QuestGiver`, `Vendor` and `Healer`. A quest giver offers the chain in `QuestDatabase.Chains[npcName]`.

Pass `blocksTile: false` (all of Hollowmere's NPCs do), so adding or moving an NPC doesn't change the walkable map. If you do change the map (buildings, walls, the town size), just rebuild: the server takes the new map from the first client on the new build.

## Add an ability

1. Add an entry to `AbilityId` and `AbilityDef.All` in `Characters/Abilities.cs`, and put it in a class's kit in `ClassKits` (five slots, cast with ++1++–++5++).
2. Implement it in the `switch` in `Player.CastAbility`. Use `Combatant.Overlap` to find enemies, and call `NetClient.I?.SendFx(...)` so other players see it.
3. Put its look and sound in a method in `Combat/AbilityFx.cs` (built from `SpellFx` pieces such as `CastCircle`, `Shockwave` or `Swirl`; see [Art & UI → Building blocks](art.md#building-blocks)), call it from the cast, and add the fx kind to `AbilityFx.Remote` and to the server's `FX_KINDS` whitelist in `server.js`.
4. The kit slots already have hotkeys in `Player.HandleInput`. For an extra key, add it there plus a `GKey` in `GameInput.cs`.

## Add items or affixes

Items are rolled by the server (`server/items.js`, a port of `ItemDatabase` and `ItemPowers`), so change both sides.

- Base types per tier: `weaponBases` and `armorBases` in `Items/ItemDatabase.cs` and `server/items.js`.
- New stat: add it to the `Stat` enum, `RollStat`, the `prefixes`/`suffixes` dictionaries and `Item.StatText`. Then apply it in `Player.RecalculateStats`.
- Legendary names: `legendaryNames`.

## Announce a change

Add an entry at the **top** of `Changelog.Entries` in `Assets/Scripts/Progression/Changelog.cs`, with the next `Id`, a date, a title and a few lines. Heroes see it marked NEW in the *What's New* window until they open it.

## Change the world

Everything is in `World/WorldGenerator.cs` (the original lands) and `World/WorldGenerator.Regions.cs` (the outer lands; towns are listed there and in `TOWNS` in `server/content.js`). The seed is `WorldGenerator.Seed`. Any change to walls, trees, rocks, water or blocking NPC positions changes the walkability map; the first client on the new build hands the server the new map (see [Operations](../deployment/operations.md#updating-the-game)).

!!! warning "Layout randomness"
    Placement that affects walkability must use the layout helpers `LR`, `LRI` and `LV` (a private `System.Random`); purely visual
    variety uses `VR`/`Pick`. Never use `UnityEngine.Random` in world generation. If an art and a primitive code path differ in how
    many layout numbers they draw, make them draw the same count ("keep in step"), or players with and without the art packs
    would build different maps.

To put a burning fire on a new prop, use `PropFire.Add` (see [Art & UI → Fires on props](art.md#fires-on-props)).

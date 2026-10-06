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

!!! warning "Quest ids are saved"
    Completed and active quests are stored by `Id`, so don't rename ids that players may already have.

## Add an NPC

Call `Npc.Create(...)` in `WorldGenerator.BuildTown()`. Roles are `QuestGiver`, `Vendor` and `Healer`. A quest giver offers the chain in `QuestDatabase.Chains[npcName]`.

Pass `blocksTile: false` (all of Hollowmere's NPCs do), so adding or moving an NPC doesn't change the walkable map. If you do change the map (buildings, walls, the town size), bump `WorldGenerator.LayoutVersion`; the server takes the new map from the first updated client.

## Add an ability

1. Add an entry to `AbilityId` and `AbilityDef.All` in `Characters/Abilities.cs`.
2. Implement it in the `switch` in `Player.CastAbility`. Use `Combatant.Overlap` to find enemies, and call `NetClient.I?.SendFx(...)` so other players see it.
3. Handle the new fx kind in `NetClient.HandleFx`, and add it to the server's fx whitelist in `server.js`.
4. Add a hotkey in `Player.HandleInput`, plus a `GKey` in `GameInput.cs` if you need a new key.

## Add items or affixes

- Base types per tier: `weaponBases` and `armorBases` in `Items/ItemDatabase.cs`.
- New stat: add it to the `Stat` enum, `RollStat`, the `prefixes`/`suffixes` dictionaries and `Item.StatText`. Then apply it in `Player.RecalculateStats`.
- Legendary names: `legendaryNames`.

## Change the world

Everything is in `World/WorldGenerator.cs`. The seed is `WorldGenerator.Seed`. Any change to walls, trees, rocks, water or NPC positions changes the walkability map, so run `task world:reset` when you deploy.

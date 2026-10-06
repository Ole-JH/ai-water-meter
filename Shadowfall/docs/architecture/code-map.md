# Code map

## Client (`Assets/Scripts`)

| Folder / file | Responsibility |
| --- | --- |
| `Core/GameManager.cs` | Bootstrap, camera, lighting, world generation, entering and leaving the world |
| `Core/GameInput.cs` | Mouse and keyboard wrapper for both Unity input backends |
| `Core/CameraRig.cs` | High-angle follow camera, zoom, screen shake |
| `Core/Util.cs` | Material cache (`Mat`), primitive builder (`Factory`), pulse/burst effects (`FxPulse`) |
| `World/WorldGenerator.cs` | Seeded world: ground texture, village, zones, trees, rocks, lakes, NPCs |
| `World/WorldGrid.cs` | Tile walkability, A* pathfinding, line of sight, hashing and packing |
| `World/Interactables.cs` | `LootDrop`, `ResourceNode` (gathering), `CraftingStation` + `Recipe`, `Npc` |
| `Characters/Player.cs` | Click-to-move, targeting, melee, abilities, stats, potions, gathering, save and load |
| `Characters/Enemy.cs` | `EnemyDef` (looks) and the `Enemy` network proxy (interpolation, hit prediction, death, personal loot) |
| `Characters/HumanoidModel.cs` | Blocky procedural humanoid with walk and attack animation |
| `Characters/Abilities.cs` | Ability definitions and the meteor effect |
| `Combat/Combatant.cs` | Base class for health, armor, damage numbers and area queries |
| `Combat/Projectile.cs` | Damaging and cosmetic projectiles |
| `Items/*` | `Item` model and tooltips, random gear generator, inventory |
| `Progression/SkillSet.cs` | RuneScape-style professions and XP curve |
| `Progression/Quests.cs` | Quest definitions (chains per NPC) and quest log |
| `Net/NetClient.cs` | Login flow, message dispatch, state and save sending |
| `Net/NetMessages.cs` | All wire message and save-data classes |
| `Net/WebSocketConnection.cs` | Polling WebSocket (`.jslib` in WebGL, `ClientWebSocket` elsewhere) |
| `Net/RemotePlayer.cs` | Other players: interpolation, appearance, animation |
| `UI/GameUI.cs` | Login screen, HUD, windows, dialogs, vendor, crafting, chat, minimap, tooltips |

## Server (`server/`)

| File | Responsibility |
| --- | --- |
| `server.js` | Static file host, WebSocket sessions, accounts, world grid and A*, monster AI, snapshots, persistence |
| `content.js` | Monster stats, spawner table, town safe-zone rectangle |
| `test/smoke.js` | End-to-end test with two fake clients |
| `Dockerfile`, `docker-compose.yml` | Container build and run |

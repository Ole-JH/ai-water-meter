# Code map

## Client (`Assets/Scripts`)

| Folder / file | Responsibility |
| --- | --- |
| `Core/GameManager.cs` | Bootstrap, camera, lighting, world generation, entering and leaving the world |
| `Core/GameInput.cs` | Mouse and keyboard wrapper for both Unity input backends |
| `Core/CameraRig.cs` | High-angle follow camera, zoom, screen shake |
| `Core/ArtLibrary.cs` | Loads the CC0 models (glTFast), scales and places them, tints them |
| `Core/DayNight.cs` | Day/night cycle on the server clock: sun, moon, ambient light, fog, `NightLight` |
| `Core/Sfx.cs` | Sound effects: clip variants, positional playback heard from the hero, ambience loops, volume |
| `Core/ColorGrade.cs` | Full-screen color grade (darker, grittier palette) |
| `Core/Util.cs` | Material cache (`Mat`), primitive builder (`Factory`), pulse/burst effects (`FxPulse`) |
| `World/WorldGenerator.cs` | Seeded world: ground texture, village, zones, trees, rocks, lakes, NPCs |
| `World/GroundSurface.cs` | Splat control maps, curving roads, ground mesh with lake beds, water, grass blades |
| `World/Dungeon.cs` | The Catacombs on the client: builds the server's layout (walls, floors, torches, props), portals, chests, entrance |
| `World/TownLife.cs` | Strolling villagers, patrolling guards, the village hound |
| `World/Ambience.cs` | Crows, bats, fireflies and falling leaves around the hero |
| `World/NpcChatter.cs` | What NPCs and villagers say in speech bubbles |
| `World/WorldGrid.cs` | Tile walkability, A* pathfinding, line of sight, hashing and packing |
| `World/Interactables.cs` | `LootDrop`, `ResourceNode` (gathering), `CraftingStation` + `Recipe`, `Npc` |
| `Characters/Player.cs` | Click-to-move, targeting, melee, abilities, stats, potions, gathering, save and load |
| `Characters/Enemy.cs` | `EnemyDef` (looks) and the `Enemy` network proxy (interpolation, hit prediction, death, personal loot) |
| `Characters/CharacterView.cs` | Animated model wrapper (`AnimSet`, `CharacterLook`) for heroes, NPCs and monsters |
| `Characters/HumanoidModel.cs` | Blocky procedural humanoid with walk and attack animation |
| `Characters/Abilities.cs` | Ability definitions and the meteor effect |
| `Combat/Combatant.cs` | Base class for health, armor, damage numbers and area queries |
| `Combat/SpellFx.cs` | Particle and mesh effects for spells, hits, explosions, level-ups |
| `Combat/Projectile.cs` | Damaging and cosmetic projectiles |
| `Items/VendorStock.cs` | What each vendor sells, prices and restocking |
| `Items/*` | `Item` model and tooltips, random gear generator, inventory |
| `Progression/SkillSet.cs` | RuneScape-style professions and XP curve |
| `Progression/Quests.cs` | Quest definitions (chains per NPC) and quest log |
| `Net/NetClient.cs` | Login flow, message dispatch, state and save sending |
| `Net/NetMessages.cs` | All wire message and save-data classes |
| `Net/WebSocketConnection.cs` | Polling WebSocket (`.jslib` in WebGL, `ClientWebSocket` elsewhere) |
| `Net/RemotePlayer.cs` | Other players: interpolation, appearance, animation |
| `UI/UISkin.cs` | Fantasy UI skin: panels, buttons, bars, fonts, icons, drawing helpers |
| `UI/Speech.cs` | Speech bubbles (chat and NPC chatter) |
| `UI/GameUI.cs` | HUD, windows, dialogs, vendor, crafting, chat, minimap, tooltips |
| `UI/GameUI.Login.cs` | Login screen |
| `UI/LoginShowcase.cs` | Live, lit hero preview in the village square behind the login screen |

## Server (`server/`)

| File | Responsibility |
| --- | --- |
| `server.js` | Static file host, WebSocket sessions, accounts, world grid and A*, monster AI, snapshots, persistence |
| `content.js` | Monster stats, spawner table, town safe-zone rectangle |
| `dungeon.js` | Dungeon level generator (rooms, corridors, start, stairs, boss, chests, packs) |
| `test/smoke.js` | End-to-end test with two fake clients |
| `Dockerfile`, `docker-compose.yml` | Container build and run |

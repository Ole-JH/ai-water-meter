# Code map

## Client (`Assets/Scripts`)

| Folder / file | Responsibility |
| --- | --- |
| `Core/GameManager.cs` | Bootstrap, camera, lighting, world generation, entering and leaving the world |
| `Core/GameInput.cs` | Mouse and keyboard wrapper for both Unity input backends |
| `Core/CameraRig.cs` | High-angle follow camera, zoom, screen shake |
| `Core/ArtLibrary.cs` | Loads the CC0 models (glTFast), scales and places them, tints them |
| `Core/DayNight.cs` | Day/night cycle on the server clock: sun, moon, ambient light, fog, `NightLight` |
| `Core/GameSettings.cs` | Player options saved in the browser: the graphics preset and options (resolution via `Plugins/WebGL/ShadowfallDisplay.jslib`), UI scale, FPS counter, loot filter |
| `Core/Sfx.cs` | Sound effects: clip variants, positional playback heard from the hero, ambience loops, volume |
| `Core/ColorGrade.cs` | Full-screen color grade (darker, grittier palette) |
| `Core/Util.cs` | Material cache (`Mat`), primitive builder (`Factory`), pulse/burst effects (`FxPulse`) |
| `World/WorldGenerator.cs` | Seeded world: ground texture, village, zones, trees, rocks, lakes, NPCs |
| `World/WorldGenerator.Regions.cs` | The outer lands (north, east, north-east of the original world): the towns list, the ridge, Frostpeak, the Sunscar Badlands, the Ashen Reach, their towns and NPCs |
| `World/Waystone.cs` | Town waystones: attuning and fast travel |
| `World/GroundSurface.cs` | Splat control maps, curving roads, ground mesh with lake beds, water, grass blades |
| `World/Dungeon.cs` | Dungeons on the client: builds the server's layout (walls, floors, torches, campfires, boss braziers, props), portals, stairs, chests |
| `World/TownLife.cs` | Strolling villagers, patrolling guards, the village hound |
| `World/Ambience.cs` | Crows, bats, fireflies and falling leaves around the hero |
| `World/NpcChatter.cs` | What NPCs and villagers say in speech bubbles |
| `World/WorldGrid.cs` | Tile walkability, A* pathfinding, line of sight, hashing and packing |
| `World/Interactables.cs` | `LootDrop`, `ResourceNode` (gathering), `CraftingStation` + `Recipe` (anvil sparks, cooking flare and steam), `Npc` |
| `Combat/Gore.cs` | Blood and gore: directional hit sprays, death bursts, gibs, and ground splats/pools drawn as one decal mesh (`Shadowfall/Decal` shader, procedurally generated splat atlas) that dry and fade; per-monster kinds (blood, goblin ichor, rot, bone, stone) and the Gore setting |
| `World/PropFire.cs` | Animated fire on a prop (campfires, gate torches, braziers, dungeon torches): flames, embers, smoke, a wobbling glow core and a flickering light; pauses when the hero is far away |
| `World/Weather.cs` | Seasons and weather from the server: clouds, rain, snow, fog, lightning, wet ground, frozen lakes; snow cover per region; shader globals |
| `World/SnowField.cs` | Where the snow has been shoveled or trodden (a mask the terrain and grass shaders read), snow depth for slow walking; town streets never fill past a light layer |
| `World/SeasonalTown.cs` | The festival decorations for each season, autumn and winter tree colours, the maypole, petals, the summer bonfire |
| `World/SnowElves.cs` | Winterfest's elves who shovel the streets clear, and the snow piles they leave |
| `World/LeafPiles.cs` | Autumn leaf piles that burst when you run through them, and the villagers who rake them (and chase you) |
| `Core/Music.cs` | Background music: picks the context (town, wilds, graveyard, dungeon, combat, boss, login), crossfades, gaps between pieces |
| `Progression/Achievements.cs` | Achievement definitions (`AchievementDatabase`) and each hero's counters, earned achievements and title (`AchievementLog`) |
| `UI/GameUI.Achievements.cs` | The achievements window (++y++), the "Achievement earned" toasts and the title picker |
| `Characters/PartyPortraits.cs` | Live portraits of the other party members (their model, weapon and helm in a lit booth of the avatar studio) |
| `Characters/Player.cs` | Click-to-move, targeting, melee, abilities, stats, potions, recall, gathering (and its effects), save and load |
| `Characters/Enemy.cs` | `EnemyDef` (looks) and the `Enemy` network proxy (interpolation, hit prediction, death, personal loot) |
| `Characters/CharacterView.cs` | Animated model wrapper (`AnimSet`, `CharacterLook`: model, weapon kind, headgear, extra `Parts`) for heroes, NPCs, companions and monsters |
| `Characters/Avatar.cs` | The hero's avatar: a copy of the model in the current loadout, rendered off-screen for the portrait and character window |
| `Characters/Emotes.cs` | `EmoteDef`: the emotes, their clips, chat lines and `/commands` |
| `Characters/Companion.cs` | Companions for hire (`CompanionDef`: look, gear, stats) and the follower AI: pathing after the hero, targeting, melee/ranged/area attacks, heals, plus their arrival, idle and attack effects (cosmetic for other players) |
| `Characters/HumanoidModel.cs` | Blocky procedural humanoid with walk and attack animation |
| `Characters/Abilities.cs` | Ability definitions, class kits and starting stats (`ClassKits`), talents, buffs, meteor/Judgement and ground effects (Consecration, Rain of Arrows) |
| `Combat/Combatant.cs` | Base class for health, armor, damage numbers and area queries |
| `Combat/SpellFx.cs` | Particle and mesh effects for spells, hits, explosions, level-ups, and the building blocks `CastCircle`, `Shockwave`, `CrossSlash`, `Swirl` and `Loop` (looping emitters); `IceSpike` |
| `Combat/Projectile.cs` | Damaging and cosmetic projectiles |
| `Combat/AbilityFx.cs` | Look and sound of each class ability, shared by the hero and other players' relayed casts (`Remote`); `LightningBolt`, `BuffAura` (War Cry, Divine Shield, Vanished) and `StunStars` |
| `Items/ItemPowers.cs` | Legendary powers, the four class sets and their bonuses, gems (stats, colors, fusing), loot hooks |
| `World/StashChest.cs` | The stash chest in Hollowmere |
| `World/DungeonSites.cs` | The four dungeons (`DungeonDef`: entrance, look, depths) and their entrances (`DungeonEntrance`) |
| `World/WorldBoss.cs` | The client side of world bosses: which is up and where, the banner, the slam's numbers (tracker and map markers in `GameUI`) |
| `Characters/BossPresence.cs` | A world boss's entrance, armour plates (broken by the server's "phase" events), rage glow, slam craters and lingering corpse |
| `World/Invasion.cs` | The client side of town invasions: state from the server, banners, the reward and achievements (tracker and map markers in `GameUI`) |
| `World/ForgeStation.cs` | The smiths' anvils and hearths (built after the world, visual only) and the salvage and reforge shows played on them |
| `World/RiftFx.cs` | A rift's progress orb and kill motes, the guardian's tear, and the collapse after a clear (the tier's light and fog are in `DayNight`) |
| `Characters/DuelRing.cs` | A duel as everyone nearby sees it: the ring, the countdown, the crowd (villagers watching via `Walker.Watch`) and the winner's banner |
| `Social/GuildHeraldry.cs` | Guild heraldry: the flag texture, the banner members carry (`GuildBanner`) and the guild board in Hollowmere (`GuildBoard`) |
| `World/AuctionHouse.cs` | The auctioneer and podium by each general merchant (bell on "ausold") and the courier who brings auction mail ("aumail") |
| `World/BountyBoard.cs` | The bounty boards with today's notices (torn off when done) and the Bounty Cache falling from the sky |
| `World/WeatherDetail.cs` | Puddles (and ice) around the hero, breath in the cold, and lightning striking trees (`WorldGenerator.Trees`) |
| `World/DungeonFeatures.cs` | Dungeon traps (spike plates, pendulums) and the boss room's portcullises |
| `World/Rampart.cs` | The attacked gate during an invasion (shuts, shows damage, loses pieces, breaks), the militia who put up ladders and a walkway, and the walkway heroes climb onto (`Player.ClimbWall`); the shut gate blocks cells with `WorldGrid.SetClosed`, never part of the map |
| `Social/Auction.cs`, `UI/GameUI.Auction.cs` | The auction house as the server shows it, and its window |
| `Progression/Bounties.cs` | Today's bounties as the server sends them, and the reward |
| `World/Rift.cs`, `UI/GameUI.Rift.cs` | The Rift Stone, the tier and leaderboard window, the progress tracker inside |
| `Social/Guild.cs`, `UI/GameUI.Guild.cs` | Our guild as the server sends it, the invitation popup, the guild window (O) |
| `Characters/Duel.cs` | The client side of duels, and `DuelFoe`: the opponent as a hostile combatant only for us |
| `Characters/Paragon.cs` | Paragon levels past the level cap: the points and what they give (Player uses them, the character window spends them) |
| `Items/Forge.cs`, `UI/GameUI.Forge.cs` | Salvage & reforge at the blacksmiths: what it costs and gives (the server's `items.js` decides), and the window |
| `Core/ErrorReporter.cs` | Sends the game's exceptions and errors to the server (`Plugins/WebGL/ShadowfallReport.jslib`, the page's `sfReport`) |
| `Core/PhotoTour.cs` | The browser check's photo tour: every model lined up, the towns from above |
| `Core/GameCheck.cs` | With `?sfcheck=1` only: the game plays a scripted session by itself for the [browser check](../development/testing.md#browser-check) |
| `Core/Exploration.cs` | Fog of war (revealed tiles, saved with the character) and the client side of the admin module (`AdminTools`) |
| `UI/Minimap.cs` | Round minimap and fogged world map rendering |
| `UI/GameUI.Admin.cs` | Admin panel (F10) |
| `UI/GameUI.Menu.cs` | Esc game menu: settings (graphics, UI scale, FPS, loot labels, volume), What's New, account, admin, character select, log out |
| `UI/GameUI.News.cs` | *What's New* window; unread entries are marked NEW |
| `UI/GameUI.Emotes.cs` | Emote menu (++g++) and the `/e` list |
| `Items/VendorStock.cs` | The vendor stock the server sent, and prices |
| `Items/Item.cs` | `Item` model, rarity colors and tier, type line and tooltips |
| `Items/ItemDatabase.cs` | Item names, icons and look-ups (base types, affixes, legendary names, materials); the server's `items.js` rolls the real items |
| `Items/Inventory.cs` | Bags, equipment slots, stacking and sorting |
| `Progression/SkillSet.cs` | RuneScape-style professions and XP curve |
| `Progression/Quests.cs` | Quest definitions (chains per NPC) and quest log |
| `Progression/Changelog.cs` | The in-game *What's New* entries (newest first) |
| `Net/NetClient.cs` | Connection and account flow (log in, register, password resets, character select), message dispatch, state and save sending |
| `Net/NetClient.Trade.cs` | Player trading: offers (bag slots and gold), accept/cancel |
| `Net/NetClient.Items.cs` | Item and gold actions sent to the server (`Op`), the server's inventory, loot drops, vendor stock and answers |
| `Net/NetMessages.cs` | All wire message and save-data classes (`AuthMsg` for every account request) |
| `Net/WebSocketConnection.cs` | Polling WebSocket (`.jslib` in WebGL, `ClientWebSocket` elsewhere) |
| `Net/RemotePlayer.cs` | Other players: interpolation, appearance, animation |
| `UI/UISkin.cs` | Fantasy UI skin: panels, buttons, bars, fonts, icons, drawing helpers |
| `UI/Speech.cs` | Speech bubbles (chat and NPC chatter) |
| `UI/GameUI.cs` | HUD, windows, dialogs, vendor, crafting, chat, minimap, tooltips |
| `UI/GameUI.Login.cs` | Login screens: log in, create account, forgot password, reset with a code (also from a `?reset=CODE&user=NAME` link), character select with delete, create hero |
| `UI/GameUI.Account.cs` | Esc → **Account** (change password, email, new recovery code) and the recovery code popup |
| `UI/GameUI.Companions.cs` | Beastmaster Orla's companion shop and the companion frame |
| `UI/GameUI.Items.cs` | Talent window, buff icons, gem sockets, stash and trade windows |
| `UI/LoginShowcase.cs` | Live, lit hero preview in the village square behind the login screen |

## Server (`server/`)

| File | Responsibility |
| --- | --- |
| `server.js` | Static file host, WebSocket sessions, account and character messages, world grid and A*, monster AI (slows, stuns, vanished heroes), elites, parties, the item and gold ledger (`itemOps`), trades, dungeon instances, admin commands, snapshots, saving |
| `items.js` | Items on the server: the gear generator (a port of `ItemDatabase`/`ItemPowers`), loot tables, chests, vendor stock, prices, recipes, gathering levels, and the bag helpers |
| `gamedata.json` | Quest rewards and companion prices, extracted from the C# sources (`task gamedata`) |
| `store.js` | Account and character storage: `PgStore` (PostgreSQL, `DATABASE_URL`, schema migrations) and `FileStore` (JSON files in `DATA_DIR`), with the one-time import of old character files |
| `accounts.js` | Password and recovery code hashing (scrypt), reset codes, rate limiter, optional reset emails (nodemailer, `SMTP_URL`) |
| `admin-cli.js` | Command-line account admin: `reset-code`, `admin on\|off`, `accounts` ([Accounts & passwords](../deployment/accounts.md#admin-command-line)) |
| `content.js` | Monster stats, spawner table, dungeons (`DUNGEONS`), global `BALANCE`, dungeon `DIFFICULTIES`, town safe-zone rectangle, spawn point |
| `dungeon.js` | Dungeon level generators: rooms and corridors (`generate`) and natural caverns (`generateCaves`), with start, stairs, boss, chests and packs |
| `auction.js` | The auction house: listings, buying with the house's cut, mail for offline sellers, expiry (`store.setMeta`) |
| `bounty.js` | Daily bounties: three per hero per day (seeded by name and date), counted from kills, paid at once |
| `rift.js` | Greater rifts: opening one at the Rift Stone, tier scaling, progress, the guardian, the leaderboard (`store.setMeta`) |
| `guild.js` | Guilds: founding, invitations, ranks, guild chat, the tag on nameplates (stored with `store.saveGuild`) |
| `duel.js` | Duels: challenges, the countdown, relaying hits between the two duelists, who wins |
| `worldboss.js` | World bosses: the four lairs, when one rises, its slam, adds and rage, growing with every hero (`WORLD_BOSS_MINUTES`) |
| `invasion.js` | Town invasions: choosing the town and gate, the waves, the siege of the gate, rewards (`INVASION_MINUTES`) |
| `weather.js` | Seasons (from the clock, `SEASON_MINUTES`) and the weather: what fits the season, changing every 6 to 16 minutes |
| `metrics.js` | Dependency-free Prometheus metrics (counters, histograms, scrape-time gauges, process metrics), served on `METRICS_PORT` |
| `test/smoke.js` | End-to-end test with fake clients, against files or PostgreSQL (`PG_TEST_URL`) |
| `Dockerfile`, `docker-compose.yml` | Container build and run, the PostgreSQL database, plus the monitoring stack |
| `monitoring/` | Prometheus (scrape config, alert rules), Alertmanager, Loki, Alloy and Grafana provisioning and dashboards ([Monitoring](../deployment/monitoring.md)) |

## Tools (`tools/`)

| Path | Responsibility |
| --- | --- |
| `compile-check/` | .NET project that compiles `Assets/Scripts` without Unity (`task client:check`) |
| `audio/build_music.py` | Downloads, trims, normalizes and encodes the music, and writes its playlist (`task music:build`) |
| `gamedata/extract.js` | Writes `server/gamedata.json` from `Quests.cs`, `Companion.cs` and `Achievements.cs` (`task gamedata`; the smoke test fails if it is stale) |
| `docker-build-client.sh` | Entry point of the Docker WebGL build (`task client:build`) |
| `browser-check/` | Playwright runner of the pre-deploy browser check (`task check:browser`) |
| `autodeploy.sh` | What the auto-deploy cron job runs: build, browser check, swap the build in, `task up` |
| `port-of.sh` | Prints a Docker stack port and where it came from (environment, `server/.env` or default) |
| `license-helper/` | Unity Hub in a container for `task license:activate` |
| `art/emotes.py` | Authors the Wave, Dance, Bow, Point, Clap and Flex animations for the hero rig (and previews them as stick figures) |
| `monitoring/shadowfall_dashboard.py` | Generates the Grafana "Shadowfall" dashboard (`task monitoring:dashboard`) |
| `art/` | Fetch and repack the 3D models, generate ground textures |
| `audio/` | Fetch and build the sound effects |
| `ui/make_skin.py`, `ui/render_icons.js` | Generate the UI skin and render the icons |

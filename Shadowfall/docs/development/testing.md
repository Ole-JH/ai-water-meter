# Testing

## Server smoke test

```bash
task server:test    # or: cd server && npm test
task server:test:pg # the same against PostgreSQL
```

`server/test/smoke.js` starts a real server on a random port with a temporary data directory, storing accounts in files. It then connects fake clients and checks:

- that the Dockerfile copies every server module and `gamedata.json`, and that `gamedata.json` matches the C# sources;
- the health endpoint and static page;
- the world upload handshake, account registration and character creation;
- snapshots containing monsters and the other player;
- monster attacks;
- shared kill credit and XP, including the damage cap;
- chat relay and sanitising, spell-effect relay and the server clock;
- parties: invitations, what the party frames get (class, mana, weapon, facing, the dungeon a member is in), party chat, quest sharing, shared kill credit, leaving;
- seasons and weather: sent at login, set by admins only and broadcast;
- achievements: announced to the party once by name, unknown ids ignored, titles only shown once earned;
- stuns, vanishing (Smoke Bomb) and companions in snapshots;
- items and gold: the starter kit, loot in kill messages, client saves that can't change gold, equipping, dropping and picking up (once), drinking, merchants only in town, buying (stacks, the current stock only) and selling, admin `give`, quest rewards paid once and collect quests;
- trades: requests, offers by bag slot, gold you don't have, completion only after both accept, and both inventories afterwards;
- the admin module: admin flag at login, refusal for non-admins, spawning elites, `killall`, time of day, player list;
- dungeons: layouts, separate instances, difficulty, returning to the right entrance, admin depth and regeneration;
- logout notifications;
- that saves persist across logins;
- accounts: name and password rules, unique account and character names, several characters per account, character select and back, a helpful error for logging in with a character name, no email resets without SMTP, changing the password, logging out the old session, recovery codes (work once and are replaced), admin reset codes (`resetpw`, by character name too, work once), deleting characters, locking an account after 5 wrong passwords, and importing an old-format character file with its password and progress;
- that wrong passwords are rejected, and world updates: a newer build replaces the map and reloads players on the old one, older and stale clients are told to reload (also by the served `build.json`), and a different map within the same build adopts the server's.

### Against PostgreSQL

`task server:test:pg` runs the same test with accounts in PostgreSQL. It starts a throwaway `postgres:17` container on
`127.0.0.1:55432` (needs Docker) and removes it afterwards. To use an existing server instead, set `PG_TEST_URL` to a user that may
create databases; the test creates its own `shadowfall_test_*` database and drops it at the end:

```bash
PG_TEST_URL=postgres://postgres:secret@localhost:5432/postgres task server:test:pg
# or: cd server && npm run test:pg   (default PG_TEST_URL: postgres://postgres:postgres@localhost:5432/postgres)
```

`task ci` runs only the file-based test.

## C# compile check

```bash
task client:check
```

This builds `Assets/Scripts` with warnings treated as errors, against Unity's reference assemblies, without needing a Unity install.

## Browser check

```bash
task check:browser                      # the build in server/public
CHECK_PUBLIC=public-next task check:browser   # another build folder under server/
```

Plays a short session of the WebGL build in headless Chromium (Playwright, `tools/browser-check`) before players get it.
[Auto-deploy](../deployment/operations.md#auto-deploy) runs it on every deploy. Two throwaway containers (profile
`check` in `server/docker-compose.yml`, never started by `task up`):

- **check-server**: the game server built from the current code, serving the build to check. No database: accounts go to
  JSON files on a tmpfs, so nothing it does reaches the real game. No published port.
- **browser-check**: Chromium with software WebGL (SwiftShader, no GPU needed) opens the game with `?sfcheck=1`.

With `?sfcheck=1` the game drives itself (`GameCheck.cs`): it registers a random `check123456` account, creates a Knight,
waits for the world, walks to the well in the middle of Hollowmere's square and then to the waystone, and reports every
step to the page (`window.sfCheck`, `ShadowfallCheck.jslib`). The browser takes a screenshot at each stop. The check
**fails** when the page shows a loading error, the game doesn't start within 5 minutes, registering or entering the world
fails, the hero can't walk to a stop, or the game logs an exception. The report lists the exceptions first (with the
top of their stack), then models that failed to load, then other logged errors. "Loading FSB failed" audio errors are
ignored: the check's Chromium has no AAC decoder (the audio format of Unity's WebGL builds); players' browsers do.

Results go to `.autodeploy/check/`: `01-spawn.png`, `02-well.png`, `03-waystone.png`, `04-mount.png` (the hero on a horse; and `NN-fail.png` on failure), plus
`result.json` with the verdict, every step, the game's logged errors and the browser console. `CHECK_TIMEOUT_S` (default
480) limits the whole run; software rendering is slow, so the frame rate it reports is no measure of a real machine.

**Longer playtest** (by hand, never in the deploy check): with `?sfcheck=1&tour=1` the game skips the photo tour and
plays on instead (`PlaytestTour.cs`): switches to the Low graphics preset, opens the bags, character, talents, map,
achievements and comfort settings for a screenshot each, walks to the nearest dungeon and goes down (a look around, the
map, a fight), comes back up and fights the nearest monsters outside (backing off below 60% health), and reports the
frame rate of each scene as `fps:<scene>` steps (with the worst frame and how many particle systems and lights there
are). Run it against any server with the check script, e.g. `CHECK_URL='https://your.server/?tour=1' node check.js`.

**Town tour** (by hand): with `?sfcheck=1&town=1` the check then looks at Hollowmere's life up close, on this client
only (`TownTour.cs`): the square by day, a pretend raid at the south gate (villagers running for their doors, the shutters
shut), then at night (the time held with `DayNight.HourOverride`) every front door swung open in turn with the lit windows
around it, and the west gate pulled to and then swung open as the hero walks up. Each door is reported with its model and
position.
It registers one `check######` account there.

With `?sfcheck=1&photos=1` (by hand; the deploy check skips it to save minutes) the check also takes a **photo tour**
(`PhotoTour.cs`) after the walk: every monster, boss, NPC, hero class and mount lined up five at a time under studio
light with their names in a caption (`models-*.png`; a model that failed to load is marked *MISSING MODEL*), and each
walled town and Hollowmere's square from above (`town-*.png`). Missing models are reported by every check anyway.

`tools/layout/hollowmere_audit.py` checks Hollowmere's fixed placements (buildings, NPCs, props, festival decorations,
villagers' spots) for overlaps and things in the streets, and draws a top-down map.

Run the runner without Docker against any server: `cd tools/browser-check && npm install && CHECK_URL=http://localhost:7341/ node check.js`.

## Playtesting multiplayer locally

1. `task server:dev`
2. Make a WebGL build and open <http://localhost:7341> in two browser windows, or press Play in the editor for one of them.
3. Create two accounts (one per window) and a hero on each. You'll see each other, can chat with ++enter++, and can fight the same monsters.

## Everything at once

```bash
task ci
```

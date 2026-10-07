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
- parties: invitations, party chat, quest sharing, shared kill credit, leaving;
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

## Playtesting multiplayer locally

1. `task server:dev`
2. Make a WebGL build and open <http://localhost:7341> in two browser windows, or press Play in the editor for one of them.
3. Create two accounts (one per window) and a hero on each. You'll see each other, can chat with ++enter++, and can fight the same monsters.

## Everything at once

```bash
task ci
```

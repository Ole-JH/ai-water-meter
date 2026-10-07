# Testing

## Server smoke test

```bash
task server:test    # or: cd server && npm test
```

`server/test/smoke.js` starts a real server on a random port with a temporary data directory. It then connects fake clients and checks:

- that the Dockerfile copies every server module;
- the health endpoint and static page;
- the world upload handshake and account creation;
- snapshots containing monsters and the other player;
- monster attacks;
- shared kill credit and XP, including the damage cap;
- chat relay and sanitising, spell-effect relay and the server clock;
- parties: invitations, party chat, quest sharing, shared kill credit, leaving;
- stuns, vanishing (Smoke Bomb) and companions in snapshots;
- trades: requests, offers and completion only after both accept;
- the admin module: admin flag at login, refusal for non-admins, spawning elites, `killall`, time of day, player list;
- dungeons: layouts, separate instances, difficulty, returning to the right entrance, admin depth and regeneration;
- logout notifications;
- that saves persist across logins;
- that wrong passwords are rejected, and world updates: a changed world is refused while others play, an older layout version can't replace it, and a newer one uploads the new map.

## C# compile check

```bash
task client:check
```

This builds `Assets/Scripts` with warnings treated as errors, against Unity's reference assemblies, without needing a Unity install.

## Playtesting multiplayer locally

1. `task server:dev`
2. Make a WebGL build and open <http://localhost:7341> in two browser windows, or press Play in the editor for one of them.
3. Log in with two different names. You'll see each other, can chat with ++enter++, and can fight the same monsters.

## Everything at once

```bash
task ci
```

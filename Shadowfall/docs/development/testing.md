# Testing

## Server smoke test

```bash
task server:test    # or: cd server && npm test
```

`server/test/smoke.js` starts a real server on a random port with a temporary data directory. It then connects two fake clients and checks:

- the health endpoint and static page;
- the world upload handshake and account creation;
- snapshots containing monsters and the other player;
- monster attacks;
- shared kill credit and XP, including the damage cap;
- chat relay and sanitising, and spell-effect relay;
- logout notifications;
- that saves persist across logins;
- that wrong passwords and mismatched client builds are rejected.

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

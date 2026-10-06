# Shadowfall

An **online, browser-playable top-down action RPG** built with Unity: Diablo-style click-to-move combat and loot, a WoW-style shared world with quests, chat and personal loot, and RuneScape-style gathering and crafting professions.

- **Client:** Unity (WebGL). The whole world is generated from code, with no scenes, prefabs or art assets.
- **Server:** Node.js in a single Docker container. It serves the WebGL build and runs the world over WebSocket: accounts, character saves, and server-simulated monsters.

## Quick start

```bash
# 1. Build the browser client in Docker (needs your Unity license file, the free one works):
mkdir -p unity-license && cp /path/to/Unity_lic.ulf unity-license/
task client:build  # or open this folder in Unity 6 and choose Shadowfall > Build WebGL
# 2. Run the server
task up            # or: cd server && docker compose up -d --build
# 3. Play
open http://localhost:7341
```

Run `task` to see every command (dev server, tests, compile check, backups, docs).

## Documentation

The full docs (setup, controls, deployment behind HTTPS, architecture, network protocol, extending the game) are written with mkdocs-material in [`docs/`](docs/index.md):

```bash
task docs:serve    # http://localhost:8000
```

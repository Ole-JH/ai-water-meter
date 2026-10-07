# Shadowfall

An **online, browser-playable top-down action RPG** built with Unity: Diablo-style click-to-move combat and loot, a WoW-style shared world with quests, chat and personal loot, and RuneScape-style gathering and crafting professions.

- **Client:** Unity (WebGL). The whole world is generated from code, with no scenes or prefabs, and dressed with free CC0 low-poly models, sounds and a CC BY icon set (see the Art & UI docs page).
- **Server:** Node.js in Docker, with PostgreSQL for accounts and characters. It serves the WebGL build and runs the world over WebSocket: accounts (several heroes each, recovery codes, password resets), character saves, and server-simulated monsters.

## Quick start

```bash
# 1. Build the browser client in Docker (no Unity install needed)
task license:activate   # once: sign in to Unity Hub in your browser, get a free license
task client:build       # or open this folder in Unity 6 and choose Shadowfall > Build WebGL
# 2. Run the server
task up            # or: cd server && docker compose up -d --build
# 3. Play
open http://localhost:7341
```

Run `task` to see every command (dev server, tests, compile check, backups, account admin, docs).

Set a database password in `server/.env` before the first start (`POSTGRES_PASSWORD=...`, letters and digits). Characters from
versions before accounts (`server/data/characters`) are imported automatically on the first start: players log in with their
character's name and password. Forgotten passwords: `task account:reset -- <name>`. See the *Accounts & passwords* docs page.

## Documentation

The full docs (setup, controls, deployment behind HTTPS, architecture, network protocol, extending the game) are written with mkdocs-material in [`docs/`](docs/index.md):

```bash
task docs:serve    # http://localhost:8000
```

# Operations

## Day-to-day commands

| Task | Command |
| --- | --- |
| Start / update | `task up` |
| Stop | `task down` |
| Logs | `task logs` |
| Status and health | `task ps` |
| Documentation | <http://localhost:8000> (the `docs` container) |
| Dashboards, metrics, logs, alerts | Grafana on <http://localhost:3000>, see [Monitoring](monitoring.md) |
| Back up characters | `task backup` → `backups/shadowfall-data-<timestamp>.tar.gz` |
| Release a new client build | `task deploy` (Unity build + container rebuild) |

## Updating the game

| What changed | What to do |
| --- | --- |
| Client code only (UI, items, abilities, visuals) | Rebuild WebGL; players refresh the page |
| `server.js` or `content.js` | `task up` (rebuilds the image) |
| **World generation** (`WorldGenerator.cs`: tree/wall/water layout, or the seed) | Bump `WorldGenerator.LayoutVersion` and rebuild. The first player to log in once nobody is online uploads the new map automatically |
| Network protocol (`NetMessages.cs` / handlers) | Bump `ProtocolVersion` in `NetClient.cs` **and** `PROTOCOL_VERSION` in `server.js`. Old clients are told to refresh |

!!! info "How world updates work"
    The server stores the uploaded map with its hash and the client's `LayoutVersion`. A client whose map differs is handled like this:

    - **Higher layout version, nobody online:** the server asks it for the new map, replaces the old one and respawns the overworld's monsters on it.
    - **Higher layout version, others still playing on the old map:** refused until they log out (*"still running the previous version of the world"*).
    - **Same or lower layout version** (usually a stale browser cache): refused with *"Your game client is older than this server's world. Refresh the page"*, so an old client can never swap the world back.

    `task world:reset` still works if you want to force a fresh upload.

## Restoring a backup

```bash
task down
tar -xzf backups/shadowfall-data-20260101-120000.tar.gz -C server
task up
```

## Resetting a character's password

Delete the character's file (`server/data/characters/<name>.json`). The next login with that name creates a fresh character with the new password. To keep the progress instead, copy the `save` object from the old file into the new one while the server is stopped.

## Security notes

Shadowfall is a hobby-scale game. Its trust model is:

- **Server-authoritative:** accounts and passwords, monster health, positions, AI, deaths, respawns and kill credit, plus XP amounts from kills.
- **Client-authoritative:** the player's own movement, inventory, loot rolls and quest progress. These are saved by the server but not re-simulated. Reported damage is capped per level, and hits must be within range of the monster.

A modified client could therefore cheat its own character. If you need stronger guarantees, move inventory and loot generation into the server. The [architecture page](../architecture/overview.md) explains where these boundaries sit.

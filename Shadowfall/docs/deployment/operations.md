# Operations

## Day-to-day commands

| Task | Command |
| --- | --- |
| Start / update | `task up` |
| Stop | `task down` |
| Logs | `task logs` |
| Status and health | `task ps` |
| Back up characters | `task backup` → `backups/shadowfall-data-<timestamp>.tar.gz` |
| Release a new client build | `task deploy` (Unity build + container rebuild) |

## Updating the game

| What changed | What to do |
| --- | --- |
| Client code only (UI, items, abilities, visuals) | Rebuild WebGL; players refresh the page |
| `server.js` or `content.js` | `task up` (rebuilds the image) |
| **World generation** (`WorldGenerator.cs`: tree/wall/water layout, or the seed) | Rebuild the client, run `task world:reset`, then restart. The first player to log in uploads the new map |
| Network protocol (`NetMessages.cs` / handlers) | Bump `ProtocolVersion` in `NetClient.cs` **and** `PROTOCOL_VERSION` in `server.js`. Old clients are told to refresh |

!!! info "Why `world:reset`?"
    The server stores a hash of the uploaded map and rejects clients whose map differs, with the message *"Your game client doesn't match this server's world"*. That stops players with a stale cached client from desyncing monster pathing.

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

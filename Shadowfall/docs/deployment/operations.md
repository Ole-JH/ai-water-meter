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
| Back up accounts, characters and the world map | `task backup` → `backups/shadowfall-db-<timestamp>.sql.gz` + `backups/shadowfall-data-<timestamp>.tar.gz` |
| Reset a player's password | `task account:reset -- <account or character>`, or `/a resetpw <name>` in game |
| List accounts | `task accounts` (or `task accounts -- <filter>`) |
| SQL prompt on the account database | `task db:psql` |
| Release a new client build | `task deploy` (Unity build + container rebuild) |

## Updating the game

| What changed | What to do |
| --- | --- |
| Client code only (UI, items, abilities, visuals) | Rebuild WebGL; players refresh the page |
| `server.js`, `content.js` or another server module | `task up` (rebuilds the image). Database schema changes are migrated on startup |
| **World generation** (`WorldGenerator.cs`: tree/wall/water layout, or the seed) | Bump `WorldGenerator.LayoutVersion` and rebuild. The first player to log in once nobody is online uploads the new map automatically |
| Network protocol (`NetMessages.cs` / handlers) | Bump `ProtocolVersion` in `NetClient.cs` **and** `PROTOCOL_VERSION` in `server.js`. Old clients are told to refresh |

!!! info "How world updates work"
    The server stores the uploaded map with its hash and the client's `LayoutVersion`. A client whose map differs is handled like this:

    - **Higher layout version, nobody online:** the server asks it for the new map, replaces the old one and respawns the overworld's monsters on it.
    - **Higher layout version, others still playing on the old map:** refused until they log out (*"still running the previous version of the world"*).
    - **Lower layout version** (usually a stale browser cache): refused with *"Your game client is older than this server's world. Refresh the page"*, so an old client can never swap the world back.
    - **Same layout version but a different map**: a bug (world generation changed without bumping `LayoutVersion`). Refused with
      *"Your game builds a different world map than this server has, at the same version"*, and the server logs both hashes.
      Fix it in the code by bumping `LayoutVersion`, or run `task world:reset` and restart the server.

    `task world:reset` still works if you want to force a fresh upload.

## Restoring a backup

`task backup` writes two files. Restore the account database (stops the game server, replaces every account and character, and
starts it again):

```bash
task db:restore -- backups/shadowfall-db-20260101-120000.sql.gz
```

Restore `server/data` (the world map, and character files from before accounts):

```bash
task down
tar -xzf backups/shadowfall-data-20260101-120000.tar.gz -C server
task up
```

Backups made before accounts only contain `server/data`. Restoring one into a fresh database (an empty `shadowfall-postgres-data`
volume) imports its characters on the next start; see [Accounts → Upgrading](accounts.md#upgrading-from-character-files).

## Resetting a password

Players can reset their own password with their recovery code, or by email if the server can send mail. Otherwise give them a
one-time reset code (valid 24 hours):

```bash
task account:reset -- Alice     # account name, or the name of one of its characters
```

or type `/a resetpw Alice` in game as an admin. The player chooses **Forgot password?** → **I have a code** and enters it with
their account name. See [Accounts & passwords](accounts.md#resetting-a-password).

## Security notes

Shadowfall is a hobby-scale game. Its trust model is:

- **Server-authoritative:** accounts and passwords (see [Accounts → Security notes](accounts.md#security-notes)), monster health, positions, AI, deaths, respawns and kill credit, plus XP amounts from kills.
- **Client-authoritative:** the player's own movement, inventory, loot rolls and quest progress. These are saved by the server but not re-simulated. Reported damage is capped per level, and hits must be within range of the monster.

A modified client could therefore cheat its own character. If you need stronger guarantees, move inventory and loot generation into the server. The [architecture page](../architecture/overview.md) explains where these boundaries sit.

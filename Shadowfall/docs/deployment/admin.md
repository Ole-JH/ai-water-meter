# Admin module

Admins can see and change everything that is normally hidden or random: the whole map, monsters on the map, dungeon entrances, the time of day, elite odds. They can also jump into any dungeon, spawn monsters and manage players.

## Making someone an admin

Either:

- list character names in the `ADMINS` environment variable (comma-separated, case-insensitive). With Docker, put it in `server/.env` (git-ignored):

    ```bash
    ADMINS=Kissmypiss,SomeFriend
    ```

    then `task up` (or `task update`). Or
- add `"admin": true` to the character's file in `server/data/characters/<name>.json` while the server is stopped.

The server tells the client at login. The server checks every server-side command again, so a modified client can't use them.

## In game

Press ++f10++ (or ++esc++ → **Admin**) to open the admin panel:

| Tab | What it does |
| --- | --- |
| **Map & Hero** | Reveal the whole map (no fog) · show enemies on the maps · show dungeon entrances · god mode · run fast · +1000 gold · +1 level · full heal and reset cooldowns · a legendary, a set piece, 5 gems or 10 potions · reset the recall cooldown · save now |
| **Dungeons** | Enter any dungeon at any depth from anywhere · regenerate the current level (new layout and monsters for everyone inside) · get a fresh private copy of the current level |
| **World** | Set the time of day for everyone · elite chance for new spawns (0 / 8 / 50 / 100%) · spawn any monster (level, count, elite) next to you · kill everything nearby · send an announcement |
| **Players** | Everyone online and where they are · go to them · summon them · kick |

On the world map (++m++), admins can **right-click** to teleport there.

## Chat commands

The same server-side commands work from chat with `/a` (or `/admin`):

| Command | Example |
| --- | --- |
| `tp <x> <z>` | `/a tp 144 141` |
| `tpto <name>` / `summon <name>` | `/a tpto Bob` |
| `dungeon <id or 0-3> [depth]` | `/a dungeon warrens 2` (ids: `catacombs`, `hideout`, `warrens`, `mine`) |
| `regen` | `/a regen` |
| `spawn <type> [level] [count] [elite]` | `/a spawn Goblin King 12 1 elite` |
| `killall [radius]` | `/a killall 30` |
| `time dawn\|day\|dusk\|night\|<hour>` | `/a time night` |
| `elites <0-1>` | `/a elites 0.5` |
| `announce <text>` | `/a announce Server restart in 5 minutes` |
| `kick <name>` | `/a kick Bob` |
| `who` | `/a who` |

`/a` on its own lists them. Admin commands are written to the server log.

!!! note "Client-side helpers"
    Map reveal, enemy markers, god mode, fast running and handing yourself gold or items run in the admin's own client. Character saves are trusted from the client in this game anyway; the admin flag only decides who gets the panel.

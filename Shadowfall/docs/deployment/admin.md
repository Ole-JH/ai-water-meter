# Admin module

Admins can see and change everything that is normally hidden or random: the whole map, monsters on the map, dungeon entrances, the time of day, elite odds. They can also jump into any dungeon, spawn monsters and manage players.

## Making someone an admin

Either:

- list **account** names in the `ADMINS` environment variable (comma-separated, case-insensitive); all of the account's heroes
  are admins. Accounts imported from old character files are named like the old character, so an existing `ADMINS` setting
  keeps working. With Docker, put it in `server/.env` (git-ignored):

    ```bash
    ADMINS=YourAccount,SomeFriend
    ```

    then `task up` (or `task update`). Or
- give the account admin rights in the database: `task account:admin -- <account> on` (`off` takes them away). It takes effect
  at the player's next login. Accounts imported from old character files keep an `"admin": true` flag the file had.

!!! note "Only account names count"
    Character names never grant admin rights: account and character names are separate, so anyone could otherwise register an
    account named like an admin's hero.

The server tells the client when the hero enters the world. The server checks every server-side command again, so a modified client can't use them.

## In game

Press ++f10++ (or ++esc++ → **Admin**) to open the admin panel:

| Tab | What it does |
| --- | --- |
| **Map & Hero** | Reveal the whole map (no fog) · show enemies on the maps · show dungeon entrances · god mode · run fast · +1000 gold · +1 level · full heal and reset cooldowns · a legendary, a set piece, 5 gems or 10 potions · reset the recall cooldown · save now |
| **Dungeons** | Enter any dungeon at any depth from anywhere · regenerate the current level (new layout and monsters for everyone inside) · get a fresh private copy of the current level |
| **World** | Set the time of day for everyone · the season and the weather · elite chance for new spawns (0 / 8 / 50 / 100%) · spawn any monster (level, count, elite) next to you · kill everything nearby · send an announcement |
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
| `season spring\|summer\|autumn\|winter` | `/a season winter`: jump to the start of that season, for everyone |
| `weather clear\|cloudy\|rain\|storm\|fog [minutes]` | `/a weather storm 10` (rain and storms fall as snow where it's cold) |
| `elites <0-1>` | `/a elites 0.5` |
| `worldboss [name]` / `worldboss stop` | `/a worldboss hrimgar`: raise a [world boss](../gameplay/world.md#world-bosses) now (by part of its name; without one, the best fit for the heroes online), or put it back to sleep |
| `invasion [town] [north\|south\|east\|west]` / `invasion stop` | `/a invasion frost north`: start a [town invasion](../gameplay/world.md#town-invasions) now (the town by the start of its name; without one, the busiest walled town; without a gate, a random one), or call the current one off |
| `announce <text>` | `/a announce Server restart in 5 minutes` |
| `kick <name>` | `/a kick Bob` |
| `who` | `/a who` |
| `give gold [n]\|legendary\|set\|gems\|potions` | `/a give gold 5000`: gold (1000 by default), a legendary or set item for your level, 5 gems or 10 of each potion |
| `resetpw <account or character>` | `/a resetpw Alice`: a one-time password reset code for that account, valid 24 hours (see [Accounts & passwords](accounts.md#resetting-a-password)) |

`/a` on its own lists them. Admin commands are written to the server log.

!!! note "Client-side helpers"
    Map reveal, enemy markers, god mode and fast running run in the admin's own client; the admin flag only decides who gets the panel. Gold and items are the server's, so the panel's gold and item buttons send the `give` command.

# Network protocol

Every message is a single JSON text frame on the WebSocket at `/ws`, and every message has a `t` (type) field. The C# definitions are in `Assets/Scripts/Net/NetMessages.cs`; the server handlers are the `handlers` object in `server/server.js`.

Protocol version: **1**. It is checked in `hello`.

## Login sequence

```mermaid
sequenceDiagram
  participant C as Client
  participant S as Server
  C->>S: hello {name, pass, hash, ver}
  alt server has no world yet
    S->>C: needworld
    C->>S: world {hash, w, h, cells(base64 bitmap)}
  end
  alt bad password / version / world hash
    S->>C: error {err}
  else ok
    S->>C: welcome {id, hasSave, save}
    S-->>C: sys "X has entered the world."
    loop every 100 ms
      C->>S: state {...}
      S->>C: snap {l, m[], p[]}
    end
  end
```

## Client → server

| `t` | Fields | Purpose |
| --- | --- | --- |
| `hello` | `name`, `pass`, `hash`, `ver` | Log in, or create the account if the name is new |
| `world` | `hash`, `w`, `h`, `cells` | Upload the walkability bitmap (bit set = blocked, LSB first, row-major) |
| `state` | `x`, `z`, `ry`, `hp`, `mhp`, `lvl`, `mv`, `atk`, `dead`, `body`, `legs`, `weapon`, `helm`, `mdl`, `wk` | Own position, health and appearance, 10× per second. `mdl` = hero model, `wk` = weapon in hand (`sword`, `axe`, `mace`, `dagger`, `staff` or empty) |
| `hit` | `mid`, `dmg`, `crit` | Report damage dealt to monster `mid` (after armor) |
| `slow` | `mid`, `dur` | Frost Nova slow |
| `chat` | `msg` | Chat to everyone. Commands handled by the server: `/who`, `/p` (party), `/w name` (whisper), `/invite name`, `/leave` |
| `pinvite` | `name` | Invite a player to your party (leader only once in a party) |
| `paccept` / `pdecline` | — | Answer a pending invitation (they expire after 60 s) |
| `pleave` | — | Leave your party |
| `pkick` | `id` | Leader removes a member |
| `pshare` | `q` | Offer quest `q` (quest id) to the rest of the party |
| `fx` | `k`, `x`, `z`, `tx`, `tz` | Cosmetic spell effect: `fireball`, `nova`, `heal`, `meteor`, `cleave`, `levelup` |
| `save` | `save` | Full character snapshot (`SaveData`) |

## Server → client

| `t` | Fields | Purpose |
| --- | --- | --- |
| `needworld` | — | Ask this client to upload the world map |
| `error` | `err` | Fatal error; the socket is closed afterwards |
| `welcome` | `id`, `hasSave`, `save`, `now` | Login OK: your session id, stored character and the server clock (ms, drives the day/night cycle) |
| `snap` | `l` (online count), `m[]`, `p[]` | Nearby monsters `{id,n,l,x,z,ry,hp,mhp,ar,sl}` (elites also `el` name, `af` comma-separated affixes, `sh` shield up) and players `{id,name,x,z,ry,hp,mhp,lvl,mv,atk,dead,body,legs,weapon,helm,mdl,wk}` |
| `matk` | `mid`, `tid`, `dmg`, `k`, `x`, `z` | Monster attack: `k` = `melee`, `shot`, `nova`, `summon`, `blink` (elite teleports to `x`,`z` from `tx`,`tz`) or `explode` (Fire Enchanted death, area damage at `x`,`z`); `tid` = target session (−1 for area effects) |
| `mdie` | `mid` | Monster died (play the death animation) |
| `kill` | `mid`, `name`, `l`, `xp`, `x`, `z`, `el` | You get credit for a kill (you damaged it, or a party member did within 60 m): award XP, update quests, roll loot |
| `fx` | `id`, `k`, `x`, `z`, `tx`, `tz` | Another player's spell effect |
| `chat` | `id`, `name`, `msg`, `ch` | Chat line. `ch`: empty = everyone, `p` = party, `w` = whisper to you, `wto` = echo of your whisper (`name` = recipient) |
| `party` | `id` (leader), `pm[]` | Your party, sent on every change and once a second: `{id,name,lvl,hp,mhp,mdl,x,z,dead}`. Empty `pm` = not in a party |
| `pinv` | `id`, `name` | Someone invites you to their party |
| `qshare` | `id`, `name`, `k` | A party member shares quest `k` |
| `sys` | `msg` | System message (joins, leaves, boss kills, `/who`) |
| `leave` | `id` | A player logged out |

## Server-side validation

- Names must match `^[A-Za-z][A-Za-z0-9_]{2,15}$`; passwords must be 4–64 characters.
- `hit`: the monster must be within 30 units of the player, and damage is capped at `100 + level × 60`.
- `chat`: limited to 2 per second, 200 characters, with `<` and `>` stripped so it can't inject IMGUI rich-text tags.
- `fx`: limited to 10 per second, and only whitelisted kinds are relayed.
- `save`: at most 256 KB, and `level` must be between 1 and 100.
- Connections that stop answering pings for 20 seconds are dropped.

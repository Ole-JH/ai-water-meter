# Docker

The server is a single container. It serves the WebGL build over HTTP and runs the game on the WebSocket path `/ws`, both on the same port.

## Run it

```bash
# 1. Build the client into server/public (Unity menu: Shadowfall > Build WebGL)
# 2. Start the container
cd server
docker compose up -d --build      # or: task up
```

`server/docker-compose.yml`:

```yaml
services:
  shadowfall:
    build: .
    restart: unless-stopped
    ports:
      - "${SHADOWFALL_PORT:-7341}:7341"
    volumes:
      - ./data:/data              # accounts, characters, world map
      - ./public:/app/public:ro   # the Unity WebGL build
    environment:
      ADMINS: ${ADMINS:-}         # admin character names, e.g. from server/.env
```

(The file also defines the `client-builder` and `license-helper` services, which only run on demand; see [Building the client in Docker](docker-client-build.md).)

The WebGL build is **copied into the image** and also **mounted** by Compose. With Compose, a new client build only needs a browser refresh. A standalone image (`task docker:build`) works on its own anywhere.

## Ports & firewall

Shadowfall needs **one TCP port**: **7341**. The web page, the game files and the live game connection (a WebSocket on `/ws`) all share it. Players don't need any ports open, and there's no UDP.

| Setup | Forward / open |
| --- | --- |
| Plain HTTP, direct | TCP **7341** → players open `http://your-host:7341` |
| Behind an HTTPS reverse proxy | TCP **443** (and **80** for Let's Encrypt) to the proxy. Keep 7341 private to the host |

If 7341 is taken on your machine, change the **host** side without touching the image:

```bash
SHADOWFALL_PORT=9000 task up          # or: SHADOWFALL_PORT=9000 docker compose up -d
```

## Configuration

| Variable | Default (in the image) | Meaning |
| --- | --- | --- |
| `PORT` | `7341` | HTTP + WebSocket port inside the container |
| `DATA_DIR` | `/data` | Where accounts, characters and `world.json` are stored |
| `PUBLIC_DIR` | `/app/public` | Folder with the WebGL build |
| `ADMINS` | empty | Comma-separated character names with admin rights (see [Admin module](admin.md)); passed through by `docker-compose.yml` from `server/.env` |
| `ELITE_CHANCE` | `0.12` | Chance that a new open-world monster spawns as an elite (admins can change it at runtime) |
| `DROP_PRIVILEGES` | `1` | Start as root only to `chown` the data volume, then run as the `node` user (uid/gid from `APP_UID`/`APP_GID`, default 1000) |

## Data volume

```text
data/
├── world.json             # walkability map uploaded by the first client
└── characters/
    ├── alice.json         # { name, salt, hash (scrypt), created, lastLogin, save: {...} }
    └── bob.json
```

Passwords are stored as salted **scrypt** hashes. Back up the folder with `task backup`.

## Health check

`GET /healthz` returns:

```json
{ "ok": true, "players": 3, "monsters": 96, "world": true }
```

The Dockerfile's `HEALTHCHECK` uses this endpoint. `task ps` shows it as well.

## Deploying to your own server

```bash
# On your workstation: build the client
task client:build              # or use the Unity menu

# Copy the server folder (including public/) to the host, e.g.
rsync -av --exclude node_modules --exclude data server/ me@myhost:shadowfall/

# On the host
cd shadowfall && docker compose up -d --build
```

For anything public-facing, put it behind HTTPS: see [HTTPS & reverse proxy](reverse-proxy.md).

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
      - "8080:8080"
    volumes:
      - ./data:/data              # accounts, characters, world map
      - ./public:/app/public:ro   # the Unity WebGL build
```

The WebGL build is **copied into the image** and also **mounted** by Compose. With Compose, a new client build only needs a browser refresh. A standalone image (`task docker:build`) works on its own anywhere.

## Configuration

| Variable | Default (in the image) | Meaning |
| --- | --- | --- |
| `PORT` | `8080` | HTTP + WebSocket port |
| `DATA_DIR` | `/data` | Where accounts, characters and `world.json` are stored |
| `PUBLIC_DIR` | `/app/public` | Folder with the WebGL build |

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

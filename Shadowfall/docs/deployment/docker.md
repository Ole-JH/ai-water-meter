# Docker

The game server is a single container. It serves the WebGL build over HTTP and runs the game on the WebSocket path `/ws`, both on the same port. Accounts and characters are kept in a **PostgreSQL** container next to it. `docker compose up` also starts this documentation site and the [monitoring stack](monitoring.md).

| Service | URL | Port variable (set in `server/.env`) |
| --- | --- | --- |
| Game | <http://localhost:7341> | `SHADOWFALL_PORT` |
| Dashboard | <http://localhost:7342> | `HOMEPAGE_PORT` |
| Grafana | <http://localhost:7343> | `GRAFANA_PORT` |
| Documentation | <http://localhost:7344> | `DOCS_PORT` (and `DOCS_BIND`, e.g. `127.0.0.1`) |
| PostgreSQL (`postgres`) | Not published; `task db:psql` opens a SQL prompt | — |

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
      - ./data:/data              # world map (and character files from older versions)
      - ./public:/app/public:ro   # the Unity WebGL build
    environment:
      ADMINS: ${ADMINS:-}         # admin account names, e.g. from server/.env
      DATABASE_URL: postgres://shadowfall:${POSTGRES_PASSWORD:-shadowfall}@postgres:5432/shadowfall
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17
    environment:
      POSTGRES_DB: shadowfall
      POSTGRES_USER: shadowfall
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-shadowfall}   # only used when the database is first created
    volumes:
      - postgres-data:/var/lib/postgresql/data              # volume shadowfall-postgres-data
```

(Shortened; the real file also sets the metrics port and the optional email and proxy settings below.)

The `docs` service runs the `squidfunk/mkdocs-material` image and serves `docs/` live with only the docs and `mkdocs.yml`
mounted, read-only, so a `git pull` shows up without a restart. To run only the game: `docker compose up -d shadowfall` (it starts `postgres` too, and waits until the database is ready).

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

`task up` starts by printing every port and where it came from, e.g.
`Ports: game 7341 (default), dashboard 7342 (default), grafana 3000 (server/.env), docs 7344 (default)`. If Docker says
*port is already allocated*, that line shows which setting to change. Remove old `GRAFANA_PORT=3000` / `DOCS_PORT=8000`
lines from `server/.env` to use the current defaults (7343, 7344).

## The dashboard

`task up` also starts **Homepage** on <http://your-host:7342>: one page with a card for the game (players online, heroes in
dungeons, monsters, the build), the docs, Grafana (alerts), Prometheus (targets up) and Alertmanager, plus whether every
container is running and the machine's CPU, memory and disk. See [Monitoring → The dashboard](monitoring.md#the-dashboard).

## Configuration

| Variable | Default (in the image) | Meaning |
| --- | --- | --- |
| `PORT` | `7341` | HTTP + WebSocket port inside the container |
| `DATA_DIR` | `/data` | Where `world.json` is stored, and accounts and characters when there is no `DATABASE_URL` |
| `DATABASE_URL` | empty (Compose: the `postgres` service) | PostgreSQL connection URL for accounts and characters. Empty = JSON files in `DATA_DIR` |
| `PUBLIC_DIR` | `/app/public` | Folder with the WebGL build |
| `ADMINS` | empty | Comma-separated account names with admin rights (see [Admin module](admin.md)); passed through by `docker-compose.yml` from `server/.env` |
| `SEASON_MINUTES` | `120` | Real minutes per season; the year (spring, summer, autumn, winter) comes round every 4 × this |
| `WORLD_BOSS_MINUTES` | `90` | About how often a [world boss](../gameplay/world.md#world-bosses) rises (75-125% of this, only with heroes online). `0` = never (admins can still raise one) |
| `AUCTION_HOURS` | `48` | How long an [auction](../gameplay/progression.md#auction-house) listing stays up |
| `RIFT_MINUTES` | `10` | The time limit of a [greater rift](../gameplay/world.md#greater-rifts) |
| `DEPLOY_STATUS_TOKEN` | (made by the auto-deploy) | Lets the auto-deploy tell players about an update's progress in chat ([operations](operations.md)). Empty: no update messages |
| `INVASION_MINUTES` | `45` | About how often a [town invasion](../gameplay/world.md#town-invasions) starts (75-125% of this, only with heroes near a walled town). `0` = never (admins can still start one) |
| `INVASION_WARN_S` | `90` | How long the scouts' warning runs before the first wave (counting only while heroes are near the town) |
| `INVASION_FIRE_S` | `18` | About how often the raiders' archers set a roof alight during a siege's waves |
| `INVASION_FEAST_S` | `300` | How long a victory feast's table stays in the town square |
| `INVASION_CAPTIVE_S` | `600` | How long heroes have to free a sacked town's captives (`0` = no captives) |
| `INVASION_ABANDON_S` | `180` | With nobody near the town, a warning or siege stands still; after this long of it the raiders give up and the town is spared |
| `ELITE_CHANCE` | `0.18` | Chance that a new open-world monster spawns as an elite (admins can change it at runtime) |
| `DROP_PRIVILEGES` | `1` | Start as root only to `chown` the data volume, then run as the `node` user (uid/gid from `APP_UID`/`APP_GID`, default 1000) |
| `SMTP_URL`, `MAIL_FROM`, `PUBLIC_URL` | empty | Optional password reset emails (see [Accounts & passwords](accounts.md#reset-emails-optional)) |
| `TRUST_PROXY` | `0` | `1` behind a reverse proxy: take client addresses (for login rate limits) from `X-Forwarded-For` |
| `METRICS_PORT` | `0` (off; Compose: `9464`) | Private Prometheus metrics port (see [Monitoring](monitoring.md)) |

Compose-only settings in `server/.env`:

| Variable | Default | Meaning |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | `shadowfall` | Password of the database user `shadowfall`, used by the database, the game server and postgres-exporter. Only applied when the database is first created; to change it later see [Accounts → Database password](accounts.md#password). Letters and digits only |

## Data

| Where | What |
| --- | --- |
| Volume `shadowfall-postgres-data` | The PostgreSQL database: accounts, characters, reset codes, account events |
| `server/data` (mounted at `/data`) | `world.json` (the walkability map uploaded by the first client), and `characters/*.json` from versions before accounts, which are imported once and then kept as a backup |

Passwords are stored as salted **scrypt** hashes. `task backup` dumps the database and archives `server/data`; see
[Accounts & passwords](accounts.md#backups-and-restore).

## Health check

`GET /healthz` returns:

```json
{ "ok": true, "players": 3, "online": 2, "dungeons": 1, "monsters": 96, "world": true, "build": "2026.10.07-150000", "uptime": 5400 }
```

`players` counts open connections (including the login screen), `online` heroes in the world and `dungeons` those of them
underground. The [dashboard](monitoring.md#the-dashboard) shows these.

The Dockerfile's `HEALTHCHECK` uses this endpoint. `task ps` shows it as well.

## Deploying to your own server

### Build here, run there

Build the client on your desktop (where Unity is fast) and run the server on another machine, e.g. `docker2`:

```bash
task release                    # git pull, build the WebGL client here, deploy it and that commit to docker2
CLIENT_BUILD=local task release # the same, building with your local Unity editor (UNITY_PATH)
task deploy:remote              # deploy the build already in server/public, without building
task remote -- logs             # run any task on the server host (ps, backup, accounts, data:reset...)
```

`task release` pulls the latest commits (fast-forward only), checks that the server code is committed and pushed and that it can reach the host over SSH, builds the client,
then on the host pulls the same commit into its git checkout, copies `server/public` there with rsync and runs `task up`.
Players get the reload prompt as usual.

Once, on the host: clone the repository, check out your branch, create `server/.env`, and make sure Task is installed
(`./bin/task` or `task` on the PATH). On the desktop you need `ssh` access to the host (keys, no password prompt) and `rsync`.

Settings, per run (`DEPLOY_HOST=myhost task release`) or in a `.deploy.env` file next to `Taskfile.yml` (not in git):

| Setting | Default | Meaning |
| --- | --- | --- |
| `DEPLOY_HOST` | `docker2` | SSH host (an alias from `~/.ssh/config` works) |
| `DEPLOY_DIR` | `~/ai-water-meter/Shadowfall` | The `Shadowfall` folder of the checkout on the host |
| `CLIENT_BUILD` | Docker | `local` to build with a local Unity editor |

### By hand

```bash
# On your workstation: build the client
task client:build              # or use the Unity menu

# Copy the server folder (including public/) to the host, e.g.
rsync -av --exclude node_modules --exclude data server/ me@myhost:shadowfall/

# On the host
cd shadowfall && docker compose up -d --build
```

To move an existing installation, accounts have to come along: they are in the database volume, not in `data/`. Run
`task backup` on the old host, copy `backups/` over, and on the new one run `task up` followed by
`task db:restore -- backups/shadowfall-db-<stamp>.sql.gz`.

For anything public-facing, put it behind HTTPS: see [HTTPS & reverse proxy](reverse-proxy.md). Set a real `POSTGRES_PASSWORD` in `server/.env` before the first `task up`.

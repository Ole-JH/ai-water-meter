# Taskfile commands

Common commands are defined in `Taskfile.yml` and run with [Task](https://taskfile.dev/installation/). Run `task` on its own to list them.

| Command | What it does |
| --- | --- |
| `task setup` / `task server:install` | Install server dependencies |
| `task server:dev` | Run the server locally with auto-reload on <http://localhost:7341> (accounts as JSON files in `server/data`, no database needed) |
| `task server:test` | End-to-end smoke test against a throwaway server (accounts in files) |
| `task server:test:pg` | The same test against PostgreSQL: a throwaway `postgres:17` container, or the server in `PG_TEST_URL` |
| `task music:build` | Download the CC0 music and rebuild `Assets/Resources/Music` and its playlist (needs ffmpeg) |
| `task gamedata` | Extract quest rewards, companion prices and achievements from the C# sources into `server/gamedata.json` (after changing `Quests.cs` or `Companion.cs`) |
| `task monitoring:test-alert` | Send a test alert through Alertmanager to Discord / Pushover |
| `task client:check` | Compile the C# scripts with the .NET SDK (no Unity) |
| `task client:build` | Build the WebGL client **in Docker** into `server/public` (needs a Unity license) |
| `task client:build:local` | Same, with a locally installed Unity editor (needs `UNITY_PATH`) |
| `task client:clean-cache` | Delete the Docker build's Unity import cache |
| `task license:activate` | Get a free Unity Personal license through Unity Hub in your browser (port 6080) |
| `task up` / `task down` / `task restart` | Start the game server, the dashboard (:7342), Grafana and the monitoring stack (:7343) and the docs site (:7344), stop everything, or restart the game server |
| `task logs` / `task ps` | Follow the game server's logs; show status and health |
| `task monitoring:up` / `task monitoring:down` | Start or stop only the [monitoring stack](../deployment/monitoring.md) |
| `task monitoring:reload` | Reload Prometheus and Alertmanager after editing `server/monitoring` |
| `task monitoring:dashboard` | Regenerate the Shadowfall Grafana dashboard |
| `task metrics` | Print the game server's current Prometheus metrics |
| `task docker:build` | Build the server image only |
| `task deploy` | `client:build` (Docker) followed by `up` |
| `task update` | `git pull`, then `deploy`: the one command to update everything when the game and server run on the same machine |
| `task release` | `git pull`, then build the WebGL client **on this machine**, then deploy it and the current commit to the server host (`DEPLOY_HOST`, default `docker2`); see [Docker](../deployment/docker.md#build-here-run-there). `CLIENT_BUILD=local task release` builds with a local Unity editor |
| `task deploy:remote` | Only the deploy half of `release`: ship the build already in `server/public` and restart the server host |
| `task remote -- <task>` | Run any task on the server host, e.g. `task remote -- logs`, `task remote -- backup`, `task remote -- data:reset` |
| `task data:reset` | **Wipe all game data** (every account, character and stash, and the world map) for a fresh start. Asks first and runs `task backup` before deleting anything |
| `task world:reset` | Delete the stored world map (after changing world generation) |
| `task backup` | Dump the account database (`pg_dump`, if the `postgres` container runs) and archive `server/data` into `backups/` |
| `task db:restore -- <file>` | Restore the account database from a `backups/shadowfall-db-*.sql.gz` dump (asks first; stops the game server meanwhile) |
| `task db:psql` | SQL prompt on the account database |
| `task account:reset -- <name>` | One-time password reset code (24 h) for an account, or for the account owning that character |
| `task account:admin -- <account> on\|off` | Give or take admin rights |
| `task accounts` / `task accounts -- <filter>` | List accounts with their email, characters and last login |
| `task docs:serve` / `task docs:build` | Preview or build this documentation (local mkdocs, or Docker as a fallback). `task up` serves it too, on :7344 |
| `task docs:install` | Install mkdocs-material locally with pip (optional) |
| `task art:fetch` / `task art:build` | Download the CC0 art packs into `.art-cache`; repack the models into `Assets/Resources/Art` |
| `task art:ground` | Regenerate the ground textures (needs numpy and Pillow) |
| `task audio:fetch` / `task audio:build` | Download the sound packs; rebuild `Assets/Resources/Audio` (needs numpy and ffmpeg) |
| `task ui:skin` / `task ui:icons` | Regenerate the dark UI skin; re-render the icons (see [Art & UI](art.md)) |
| `task ci` | `server:test` + `client:check` + `docs:build` |

## Variables

| Variable | Default | Used by |
| --- | --- | --- |
| `UNITY_PATH` | `unity` | `client:build:local` |
| `UNITY_LICENSE`, `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` | — | `client:build` (license options) |
| `PORT` | `7341` | `server:dev` |
| `SHADOWFALL_PORT` | `7341` | Host port for `up` and `ps` |
| `LICENSE_HELPER_PORT`, `LICENSE_HELPER_BIND`, `VNC_PASSWORD` | `6080`, `0.0.0.0`, random | `license:activate` |
| `DOCS_PORT` | `8000` | `docs:serve` (the docs container started by `up` uses `DOCS_PORT` from `server/.env`, default 7344) |
| `PG_TEST_URL` | — | `server:test:pg`: use this PostgreSQL server (it creates and drops a throwaway database) instead of starting a container |

```bash
task deploy                     # build the client in Docker, then rebuild and restart the server
UNITY_PATH="/Applications/Unity/Hub/Editor/6000.0.23f1/Unity.app/Contents/MacOS/Unity" task client:build:local
```

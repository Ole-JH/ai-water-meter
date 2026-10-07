# Taskfile commands

Common commands are defined in `Taskfile.yml` and run with [Task](https://taskfile.dev/installation/). Run `task` on its own to list them.

| Command | What it does |
| --- | --- |
| `task setup` / `task server:install` | Install server dependencies |
| `task server:dev` | Run the server locally with auto-reload on <http://localhost:7341> (data in `server/data`) |
| `task server:test` | End-to-end smoke test against a throwaway server |
| `task client:check` | Compile the C# scripts with the .NET SDK (no Unity) |
| `task client:build` | Build the WebGL client **in Docker** into `server/public` (needs a Unity license) |
| `task client:build:local` | Same, with a locally installed Unity editor (needs `UNITY_PATH`) |
| `task client:clean-cache` | Delete the Docker build's Unity import cache |
| `task license:activate` | Get a free Unity Personal license through Unity Hub in your browser (port 6080) |
| `task up` / `task down` / `task restart` | Start the game server, the docs site (:8000) and the monitoring stack, stop everything, or restart the game server |
| `task logs` / `task ps` | Follow the game server's logs; show status and health |
| `task monitoring:up` / `task monitoring:down` | Start or stop only the [monitoring stack](../deployment/monitoring.md) |
| `task monitoring:reload` | Reload Prometheus and Alertmanager after editing `server/monitoring` |
| `task monitoring:dashboard` | Regenerate the Shadowfall Grafana dashboard |
| `task metrics` | Print the game server's current Prometheus metrics |
| `task docker:build` | Build the server image only |
| `task deploy` | `client:build` (Docker) followed by `up` |
| `task update` | `git pull`, then `deploy`: the one command to update everything when the game and server run on the same machine |
| `task world:reset` | Delete the stored world map (after changing world generation) |
| `task backup` | Archive `server/data` into `backups/` |
| `task docs:serve` / `task docs:build` | Preview or build this documentation (local mkdocs, or Docker as a fallback). `task up` already serves it on :8000, so use `DOCS_PORT=8001 task docs:serve` alongside it |
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
| `DOCS_PORT` | `8000` | `docs:serve`, and the docs container started by `up` |

```bash
task deploy                     # build the client in Docker, then rebuild and restart the server
UNITY_PATH="/Applications/Unity/Hub/Editor/6000.0.23f1/Unity.app/Contents/MacOS/Unity" task client:build:local
```

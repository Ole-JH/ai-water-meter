# Taskfile commands

Common commands are defined in `Taskfile.yml` and run with [Task](https://taskfile.dev/installation/). Run `task` on its own to list them.

| Command | What it does |
| --- | --- |
| `task setup` | Install server dependencies |
| `task server:dev` | Run the server locally with auto-reload on <http://localhost:8080> (data in `server/data`) |
| `task server:test` | End-to-end smoke test against a throwaway server |
| `task client:check` | Compile the C# scripts with the .NET SDK (no Unity) |
| `task client:build` | Headless Unity WebGL build into `server/public` (needs `UNITY_PATH`) |
| `task up` / `task down` / `task restart` | Start, stop or restart the Docker container |
| `task logs` / `task ps` | Follow logs; show status and health |
| `task docker:build` | Build the server image only |
| `task deploy` | `client:build` followed by `up` |
| `task world:reset` | Delete the stored world map (after changing world generation) |
| `task backup` | Archive `server/data` into `backups/` |
| `task docs:serve` / `task docs:build` | Preview or build this documentation (local mkdocs, or Docker as a fallback) |
| `task ci` | `server:test` + `client:check` + `docs:build` |

## Variables

| Variable | Default | Used by |
| --- | --- | --- |
| `UNITY_PATH` | `unity` | `client:build`, `deploy` |
| `PORT` | `8080` | `server:dev` |
| `DOCS_PORT` | `8000` | `docs:serve` |

```bash
UNITY_PATH="/Applications/Unity/Hub/Editor/6000.0.23f1/Unity.app/Contents/MacOS/Unity" task deploy
```

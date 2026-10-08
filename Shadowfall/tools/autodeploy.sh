#!/bin/sh
# Auto-deploy: run on the server host every few minutes (task autodeploy:install sets up the cron job).
# When the branch on origin has new commits, move this checkout to them and deploy:
#   - commits touching the Unity project (Assets/, Packages/, ProjectSettings/) -> build the client in Docker into
#     server/public-next (the live server keeps serving server/public meanwhile)
#   - then the browser check (task check:browser): headless Chromium plays the new build against a throwaway copy of
#     the new server code. Skipped with AUTODEPLOY_CHECK=0 (in .deploy.env or the cron line).
#   - only if that passes: the new build is copied into server/public and task up restarts the server.
# If anything fails, the running server is left alone and that commit isn't retried; the next push tries again.
# Every step is also told to the players in chat (new update, building, testing, restart countdown, done or failed): the
# script posts it to the running server (POST /deploy-status with DEPLOY_STATUS_TOKEN, made and put in server/.env the
# first time).
# Runs from the Shadowfall folder. Log: .autodeploy/log, screenshots: .autodeploy/check/. With DISCORD_WEBHOOK_URL in
# server/.env (the same one the alerts use) every deploy and failure is posted there (text only: the check's
# screenshots stay in .autodeploy/check/ for a look by hand).
set -u
cd "$(dirname "$0")/.." || exit 1
state=.autodeploy
mkdir -p "$state"
# The task binary: $TASK (the install passes the one it ran with), else ./bin/task or ../bin/task, else task on the PATH.
case "${TASK:-}" in
  "") TASK=task; for t in ./bin/task ../bin/task; do [ -x "$t" ] && { TASK="$PWD/$t"; break; }; done ;;
  */*) TASK="$(cd "$(dirname "$TASK")" && pwd)/$(basename "$TASK")" ;;
esac

# One run at a time: a client build takes a while, and cron keeps ticking.
exec 9>"$state/lock"
flock -n 9 || exit 0

log() { echo "$(date '+%Y-%m-%d %H:%M:%S') $*"; }

# notify "message": a Discord message (text only).
notify() {
  url="$(cd server && sh ../tools/port-of.sh DISCORD_WEBHOOK_URL "")"
  [ -n "$url" ] || return 0
  msg="$(printf '%s' "Shadowfall on $(hostname): $1" | tr '\n' ' ' | cut -c1-1900 | sed 's/\\/\\\\/g; s/"/\\"/g')"
  printf '{"content":"%s"}' "$msg" > "$state/discord.json"
  curl -fsS -m 30 -H "Content-Type: application/json" --data "@$state/discord.json" "$url" >/dev/null 2>&1 || true
}

# status stage [message] [seconds]: tells the running game server how the update is coming along, and it tells the
# players in chat (POST /deploy-status, see server.js). The token is made the first time and kept in server/.env.
deploy_token() {
  tok="$(cd server && sh ../tools/port-of.sh DEPLOY_STATUS_TOKEN "")"
  if [ -z "$tok" ]; then
    tok="$(head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n')"
    printf '\n# Lets the auto-deploy tell players about updates in chat (tools/autodeploy.sh made it)\nDEPLOY_STATUS_TOKEN=%s\n' "$tok" >> server/.env
  fi
  echo "$tok"
}
status() {
  tok="$(deploy_token)"
  port="$(cd server && sh ../tools/port-of.sh SHADOWFALL_PORT 7341)"
  msg="$(printf '%s' "${2:-}" | tr '\n' ' ' | cut -c1-150 | sed 's/\\/\\\\/g; s/"/\\"/g')"
  printf '{"stage":"%s","msg":"%s","secs":%s}' "$1" "$msg" "${3:-0}" > "$state/status.json"
  curl -fsS -m 5 -H "Content-Type: application/json" -H "X-Deploy-Token: $tok" --data "@$state/status.json" "http://localhost:$port/deploy-status" >/dev/null 2>&1 || true
}

# The browser check's verdict, from its result.json: "stage: detail".
check_reason() {
  f="$state/check/result.json"
  [ -f "$f" ] || { echo "no result (see the log)"; return; }
  stage="$(sed -n 's/^  "stage": "\(.*\)",$/\1/p' "$f" | head -n 1)"
  detail="$(sed -n 's/^  "detail": "\(.*\)",$/\1/p' "$f" | head -n 1)"
  echo "$stage: $detail" | sed 's/\\n/ /g; s/\\"/"/g'
}

# The lines from the client build's output that say what went wrong (compiler errors first), for the failure message.
build_errors() {
  f="$state/build.log"
  [ -f "$f" ] || return 0
  { grep -E "error CS[0-9]+" "$f" | sort -u | head -n 6
    grep -E "\[client-build\]|Build failed|build failed|BuildFailedException|Exception:|Error building|No valid Unity|license|No space left|Killed|exit status" "$f" | grep -v "error CS" | tail -n 8
  } | cut -c1-300 | tr '\n' ' ' | cut -c1-1400
}


branch="${AUTODEPLOY_BRANCH:-$(git rev-parse --abbrev-ref HEAD)}"
git fetch -q origin "$branch" || { log "git fetch failed"; exit 1; }
head="$(git rev-parse HEAD)"
target="$(git rev-parse "origin/$branch")"
[ "$head" = "$target" ] && exit 0
[ "$(cat "$state/failed" 2>/dev/null)" = "$target" ] && exit 0

short="$(git rev-parse --short "$target")"
subject="$(git log -1 --format=%s "$target")"
log "New commits on $branch: $(git rev-parse --short "$head") -> $short ($subject)"

changed="$(git diff --name-only "$head" "$target" -- .)"
if [ -z "$changed" ]; then
  # Only the rest of the repo changed: follow along, nothing to deploy.
  git reset -q --keep "$target" && log "Nothing for Shadowfall in $short; checkout moved, no deploy."
  exit 0
fi
status new "$subject"
build=""
echo "$changed" | grep -qE '^Shadowfall/(Assets|Packages|ProjectSettings)/|^Shadowfall/server/public/index\.html$' && build=1
[ -f server/public/build.json ] || build=1

# Files the client build rewrites here: drop the local copies when the new commits change them, or the move refuses.
for f in server/public/index.html ProjectSettings/ProjectVersion.txt Packages/packages-lock.json; do
  echo "$changed" | grep -qx "Shadowfall/$f" && git checkout -q -- "$f" 2>/dev/null
done

# --keep moves to the new commit but refuses to throw away other local edits.
if ! git reset -q --keep "$target"; then
  log "Could not move the checkout to $short: local changes in the way (see git status). Not deploying."
  echo "$target" > "$state/failed"
  notify "auto-deploy of $short stopped: local changes in the checkout (see git status)."
  exit 1
fi

give_up() { # why, notify-text
  log "$1"
  echo "$target" > "$state/failed"
  notify "$2"
  status failed
  exit 1
}

if [ -n "$build" ]; then
  log "Building the client into server/public-next..."
  status build
  rm -rf server/public-next
  rm -f "$state/build.rc"
  # The build's output goes to the log as before, and to build.log so the failure message can quote it.
  if ! { SF_BUILD_OUT=server/public-next "$TASK" client:build 2>&1; echo "exit $?" > "$state/build.rc"; } | tee "$state/build.log" \
     || ! grep -qx "exit 0" "$state/build.rc"; then
    why="$(build_errors)"
    give_up "Client build failed for $short; the server keeps running the previous version." \
            "client build failed for $short ($subject); still running the previous version. ${why:+Build said: $why}"
  fi
fi

rm -f "$state"/check/*.png "$state"/check/result.json  # no stale screenshots in this deploy's messages

# Play the new build (or, for a server-only change, the current one) against the new server code.
public=public; [ -n "$build" ] && public=public-next
if [ "${AUTODEPLOY_CHECK:-1}" != 0 ] && [ -f "server/$public/build.json" ]; then
  log "Browser check of server/$public..."
  status check
  if ! CHECK_PUBLIC=$public "$TASK" check:browser; then
    reason="$(check_reason)"
    give_up "Browser check failed for $short ($reason); not deployed, the server keeps running the previous version." \
            "browser check failed for $short ($subject): $reason. Not deployed (screenshots in .autodeploy/check/)."
  fi
  log "Browser check passed: $(check_reason)"
fi

if [ -n "$build" ]; then
  # Into the folder the live server serves, contents only (it is bind-mounted, so the folder itself must stay).
  if command -v rsync >/dev/null 2>&1; then rsync -a --delete-after --delay-updates server/public-next/ server/public/
  else rm -rf server/public/Build && cp -a server/public-next/. server/public/; fi \
    || give_up "Could not copy the new build into server/public." "could not copy the new build for $short into server/public."
  rm -rf server/public-next
fi

# Give the players a moment's warning (AUTODEPLOY_RESTART_S, default 20; 0 = restart at once)
wait_s="${AUTODEPLOY_RESTART_S:-20}"
if [ "$wait_s" -gt 0 ] 2>/dev/null; then
  status restart "" "$wait_s"
  sleep "$wait_s"
fi
log "Restarting..."
if ! "$TASK" up; then
  give_up "task up failed for $short." "task up failed for $short ($subject)."
fi
rm -f "$state/failed"
log "Deployed $short${build:+ (with a new client build)}."
# once the new server answers, tell everyone (and those who log back in over the next minutes) it's done
port="$(cd server && sh ../tools/port-of.sh SHADOWFALL_PORT 7341)"
for i in $(seq 1 30); do curl -fsS -m 3 "http://localhost:$port/healthz" >/dev/null 2>&1 && break; sleep 2; done
status done "$subject"
notify "deployed $short: $subject${build:+ (new client build)}"

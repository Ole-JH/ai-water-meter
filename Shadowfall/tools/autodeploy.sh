#!/bin/sh
# Auto-deploy: run on the server host every few minutes (task autodeploy:install sets up the cron job).
# When the branch on origin has new commits, move this checkout to them and deploy:
#   - commits touching the Unity project (Assets/, Packages/, ProjectSettings/) -> build the client in Docker into
#     server/public-next (the live server keeps serving server/public meanwhile)
#   - then the browser check (task check:browser): headless Chromium plays the new build against a throwaway copy of
#     the new server code. Skipped with AUTODEPLOY_CHECK=0 (in .deploy.env or the cron line).
#   - only if that passes: the new build is copied into server/public and task up restarts the server.
# If anything fails, the running server is left alone and that commit isn't retried; the next push tries again.
# Runs from the Shadowfall folder. Log: .autodeploy/log, screenshots: .autodeploy/check/. With DISCORD_WEBHOOK_URL in
# server/.env (the same one the alerts use) every deploy and failure is posted there, with the check's screenshots.
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

# notify "message" [screenshot.png...]: a Discord message, with up to 4 images attached.
notify() {
  url="$(cd server && sh ../tools/port-of.sh DISCORD_WEBHOOK_URL "")"
  [ -n "$url" ] || return 0
  msg="$(printf '%s' "Shadowfall on $(hostname): $1" | tr '\n' ' ' | cut -c1-1900 | sed 's/\\/\\\\/g; s/"/\\"/g')"
  shift
  printf '{"content":"%s"}' "$msg" > "$state/discord.json"
  n=0; files=""
  for f in "$@"; do
    [ -f "$f" ] && [ $n -lt 4 ] || continue
    files="$files -F files[$n]=@$f"; n=$((n + 1))
  done
  set -f # no globbing of files[0]=...
  # shellcheck disable=SC2086
  curl -fsS -m 30 -F "payload_json=<$state/discord.json" $files "$url" >/dev/null 2>&1 || true
  set +f
}

# The browser check's verdict, from its result.json: "stage: detail".
check_reason() {
  f="$state/check/result.json"
  [ -f "$f" ] || { echo "no result (see the log)"; return; }
  stage="$(sed -n 's/^  "stage": "\(.*\)",$/\1/p' "$f" | head -n 1)"
  detail="$(sed -n 's/^  "detail": "\(.*\)",$/\1/p' "$f" | head -n 1)"
  echo "$stage: $detail" | sed 's/\\n/ /g; s/\\"/"/g'
}

shots() { ls "$state"/check/*.png 2>/dev/null | grep -E -- "$1" | head -n 4; }

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

give_up() { # why, notify-text, [screenshots...]
  log "$1"
  echo "$target" > "$state/failed"
  msg="$2"; shift 2
  notify "$msg" "$@"
  exit 1
}

if [ -n "$build" ]; then
  log "Building the client into server/public-next..."
  rm -rf server/public-next
  SF_BUILD_OUT=server/public-next "$TASK" client:build \
    || give_up "Client build failed for $short; the server keeps running the previous version." \
               "client build failed for $short ($subject); still running the previous version."
fi

rm -f "$state"/check/*.png "$state"/check/result.json  # no stale screenshots in this deploy's messages

# Play the new build (or, for a server-only change, the current one) against the new server code.
public=public; [ -n "$build" ] && public=public-next
if [ "${AUTODEPLOY_CHECK:-1}" != 0 ] && [ -f "server/$public/build.json" ]; then
  log "Browser check of server/$public..."
  if ! CHECK_PUBLIC=$public "$TASK" check:browser; then
    reason="$(check_reason)"
    # shellcheck disable=SC2046
    give_up "Browser check failed for $short ($reason); not deployed, the server keeps running the previous version." \
            "browser check failed for $short ($subject): $reason. Not deployed." $(shots 'fail|stuck')
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

log "Restarting..."
if ! "$TASK" up; then
  give_up "task up failed for $short." "task up failed for $short ($subject)."
fi
rm -f "$state/failed"
log "Deployed $short${build:+ (with a new client build)}."
# shellcheck disable=SC2046
notify "deployed $short: $subject${build:+ (new client build)}" $(shots 'well|waystone|mount')

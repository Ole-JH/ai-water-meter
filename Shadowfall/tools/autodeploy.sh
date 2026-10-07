#!/bin/sh
# Auto-deploy: run on the server host every few minutes (task autodeploy:install sets up the cron job).
# When the branch on origin has new commits, move this checkout to them and deploy:
#   - commits touching the Unity project (Assets/, Packages/, ProjectSettings/) -> build the client in Docker, then task up
#   - anything else (server, docs, monitoring...)                               -> task up only
# If a build fails, the running server is left alone and that commit isn't retried; the next push tries again.
# Runs from the Shadowfall folder. Log: .autodeploy/log. Optional Discord message on each deploy if DISCORD_WEBHOOK_URL
# is set in server/.env (the same one the alerts use).
set -u
cd "$(dirname "$0")/.." || exit 1
state=.autodeploy
mkdir -p "$state"
TASK="${TASK:-task}"

# One run at a time: a client build takes a while, and cron keeps ticking.
exec 9>"$state/lock"
flock -n 9 || exit 0

log() { echo "$(date '+%Y-%m-%d %H:%M:%S') $*"; }

notify() {
  url="$(cd server && sh ../tools/port-of.sh DISCORD_WEBHOOK_URL "")"
  [ -n "$url" ] || return 0
  msg="$(printf '%s' "Shadowfall on $(hostname): $1" | sed 's/\\/\\\\/g; s/"/\\"/g')"
  curl -fsS -m 10 -H 'Content-Type: application/json' -d "{\"content\":\"$msg\"}" "$url" >/dev/null 2>&1 || true
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

if [ -n "$build" ]; then
  log "Building the client..."
  if ! "$TASK" client:build; then
    log "Client build failed for $short; the server keeps running the previous version."
    echo "$target" > "$state/failed"
    notify "client build failed for $short ($subject); still running the previous version."
    exit 1
  fi
fi

log "Restarting..."
if ! "$TASK" up; then
  log "task up failed for $short."
  echo "$target" > "$state/failed"
  notify "task up failed for $short ($subject)."
  exit 1
fi
rm -f "$state/failed"
log "Deployed $short${build:+ (with a new client build)}."
notify "deployed $short: $subject${build:+ (new client build)}"

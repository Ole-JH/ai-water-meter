#!/usr/bin/env bash
# Builds the Unity WebGL client inside a GameCI unityci/editor container and writes it to
# server/public. Run through: task client:build   (or: docker compose --profile build run --rm client-builder)
#
# License (Unity requires one, even the free Personal license). Easiest: task license:activate
# Or provide ONE of:
#   1. unity-license/Unity_lic.ulf in the project root (copy it from a machine where Unity Hub is signed in)
#   2. UNITY_LICENSE env var containing the .ulf file contents
#   3. UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD (Unity Pro / Plus serial)
set -euo pipefail

PROJECT=/project
LICENSE_DIR=/root/.local/share/unity3d/Unity
LICENSE_FILE="$LICENSE_DIR/Unity_lic.ulf"
SERIAL_ACTIVATED=0

log() { echo "[client-build] $*"; }

mkdir -p "$LICENSE_DIR"
if [[ -f "$PROJECT/unity-license/Unity_lic.ulf" ]]; then
  log "Using license file unity-license/Unity_lic.ulf"
  cp "$PROJECT/unity-license/Unity_lic.ulf" "$LICENSE_FILE"
elif [[ -n "${UNITY_LICENSE:-}" ]]; then
  log "Using license from UNITY_LICENSE"
  printf '%s' "$UNITY_LICENSE" > "$LICENSE_FILE"
elif [[ -n "${UNITY_SERIAL:-}" && -n "${UNITY_EMAIL:-}" && -n "${UNITY_PASSWORD:-}" ]]; then
  log "Activating with serial for $UNITY_EMAIL"
  unity-editor -batchmode -nographics -quit -logFile /dev/stdout \
    -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD"
  SERIAL_ACTIVATED=1
else
  cat >&2 <<'EOF'
[client-build] No Unity license found. Easiest: run  task license:activate  (Unity Hub in your browser).
Or provide one of:
  - unity-license/Unity_lic.ulf in the Shadowfall folder. Find it on a machine signed in to Unity Hub:
      Windows: C:\ProgramData\Unity\Unity_lic.ulf
      macOS:   /Library/Application Support/Unity/Unity_lic.ulf
      Linux:   ~/.local/share/unity3d/Unity/Unity_lic.ulf
  - UNITY_LICENSE="$(cat Unity_lic.ulf)"
  - UNITY_SERIAL, UNITY_EMAIL and UNITY_PASSWORD (Pro/Plus)
See docs/deployment/docker-client-build.md
EOF
  exit 1
fi

return_license() {
  if [[ $SERIAL_ACTIVATED == 1 ]]; then
    log "Returning serial license"
    unity-editor -batchmode -nographics -quit -logFile /dev/stdout \
      -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -returnlicense || true
  fi
}

fix_ownership() {
  # Files created in the bind-mounted project would otherwise be owned by root on the host.
  local uid="${HOST_UID:-1000}" gid="${HOST_GID:-1000}"
  find "$PROJECT" -path "$PROJECT/Library" -prune -o -path "$PROJECT/server/node_modules" -prune -o \
    -path "$PROJECT/server/data" -prune -o -user 0 -exec chown "$uid:$gid" {} + 2>/dev/null || true
}
trap 'return_license; fix_ownership' EXIT

log "Building WebGL (the first build imports the project and takes a while; later builds reuse the Library cache)"
unity-editor -batchmode -nographics -quit -logFile /dev/stdout \
  -projectPath "$PROJECT" -buildTarget WebGL \
  -executeMethod Shadowfall.EditorTools.ShadowfallBuild.BuildWebGL

if [[ ! -d "$PROJECT/server/public/Build" ]]; then
  log "Build finished but server/public/Build is missing - check the log above."
  exit 1
fi
log "Done. WebGL client written to server/public - refresh the game page."

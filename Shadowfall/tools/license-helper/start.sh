#!/bin/bash
# Runs as root: starts the desktop session as the unprivileged "unity" user, waits for Unity Hub
# to write a license file, copies it to /out (= Shadowfall/unity-license) and exits.
set -u
OUT=/out
HOST_UID="${HOST_UID:-1000}"
HOST_GID="${HOST_GID:-1000}"
PASS="${VNC_PASSWORD:-$(tr -dc 'A-Za-z0-9' </dev/urandom | head -c 12)}"
PORT="${LICENSE_HELPER_PORT:-6080}"

mkdir -p "$OUT"
chown "$HOST_UID:$HOST_GID" "$OUT" 2>/dev/null || true
started=$(date +%s)

runuser -u unity -- env VNC_PASSWORD="$PASS" /usr/local/bin/session.sh &
SESSION=$!
trap 'pkill -u unity; exit 0' INT TERM

cat <<MSG

==========================================================================================
  Unity license helper is running.

  1. Open in your browser:
       http://<this-server>:${PORT}/vnc.html?autoconnect=1&resize=scale&password=${PASS}
     (password: ${PASS})
  2. In Unity Hub: sign in (a browser window opens inside the desktop; when it asks to
     open the "unityhub" link, allow it).
  3. Go to Preferences (gear icon) > Licenses > Add > Get a free personal license.
     You can skip installing an editor.

  The license is saved to unity-license/Unity_lic.ulf automatically and this helper exits.
  Press Ctrl+C to cancel.
==========================================================================================

MSG

while kill -0 "$SESSION" 2>/dev/null; do
  lic=$(find /home/unity -name '*.ulf' -newermt "@$started" 2>/dev/null | head -n 1)
  if [[ -n "$lic" ]]; then
    sleep 3   # let the Hub finish writing
    cp "$lic" "$OUT/Unity_lic.ulf"
    chown "$HOST_UID:$HOST_GID" "$OUT/Unity_lic.ulf" 2>/dev/null || true
    chmod 600 "$OUT/Unity_lic.ulf"
    echo "[license-helper] License saved to unity-license/Unity_lic.ulf - you can now run: task client:build"
    pkill -u unity
    exit 0
  fi
  sleep 2
done
echo "[license-helper] The desktop session ended before a license was created." >&2
exit 1

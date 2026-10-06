#!/bin/bash
# Runs as the "unity" user: virtual X display, window manager, VNC + noVNC, keyring and Unity Hub.
set -u
export HOME=/home/unity DISPLAY=:1 BROWSER=firefox-esr MOZ_DISABLE_CONTENT_SANDBOX=1 NO_AT_BRIDGE=1

mkdir -p "$HOME/.vnc" "$HOME/.local/share/applications"
x11vnc -storepasswd "$VNC_PASSWORD" "$HOME/.vnc/passwd" >/dev/null 2>&1

# Route unityhub:// sign-in links back to the Hub, and web links to Firefox.
xdg-mime default unityhub.desktop x-scheme-handler/unityhub
xdg-mime default firefox-esr.desktop x-scheme-handler/http x-scheme-handler/https text/html

Xvfb :1 -screen 0 1440x900x24 -nolisten tcp >/dev/null 2>&1 &
for _ in $(seq 1 50); do [[ -e /tmp/.X11-unix/X1 ]] && break; sleep 0.1; done
fluxbox >/dev/null 2>&1 &
x11vnc -display :1 -rfbauth "$HOME/.vnc/passwd" -localhost -rfbport 5900 -forever -shared -quiet >/dev/null 2>&1 &
websockify --web /usr/share/novnc 6080 localhost:5900 >/dev/null 2>&1 &

# Session bus + an unlocked, password-less "login" keyring set as default, so Unity Hub can store
# its sign-in token (libsecret) without popping up a "choose keyring password" dialog.
eval "$(dbus-launch --sh-syntax)"
mkdir -p "$HOME/.local/share/keyrings"
printf 'login' > "$HOME/.local/share/keyrings/default"
if [[ ! -f "$HOME/.local/share/keyrings/login.keyring" ]]; then
  # Unencrypted keyring file format (fine for a throwaway container).
  printf '[keyring]\ndisplay-name=login\nctime=0\nmtime=0\nlock-on-idle=false\nlock-after=false\n' \
    > "$HOME/.local/share/keyrings/login.keyring"
  chmod 600 "$HOME/.local/share/keyrings/login.keyring"
fi
eval "$(printf '' | gnome-keyring-daemon --unlock --components=secrets 2>/dev/null)"
eval "$(gnome-keyring-daemon --start --components=secrets 2>/dev/null)"
export GNOME_KEYRING_CONTROL

# Keep Unity Hub open; restart it if the user closes the window.
while true; do
  unityhub >/tmp/unityhub.log 2>&1
  sleep 2
done

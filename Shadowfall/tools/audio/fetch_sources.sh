#!/usr/bin/env bash
# Downloads the CC0 sound packs used by build_audio.py into .art-cache/audio.
set -euo pipefail
DIR="${1:-.art-cache/audio}"
mkdir -p "$DIR"
cd "$DIR"
kenney() { # resolve the current zip link from the asset page
  local url
  url=$(curl -fsSL "https://kenney.nl/assets/$1" | grep -oE "https://kenney.nl/media/pages/assets/$1/[^\"]+\.zip" | head -1)
  curl -fsSL -o "kenney_$1.zip" "$url"
}
kenney rpg-audio
kenney impact-sounds
kenney interface-sounds
for f in 80-CC0-creature-SFX_0.zip 100-CC0-SFX_0.zip sfx_100_v2.zip 25-CC0-bang-sfx.zip; do
  curl -fsSL -o "$f" "https://opengameart.org/sites/default/files/$f"
done
ls -la

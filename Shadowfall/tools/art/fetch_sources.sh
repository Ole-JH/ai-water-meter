#!/usr/bin/env bash
# Downloads the CC0 source art packs used by build_art.py into a cache folder.
#   tools/art/fetch_sources.sh [cache-dir]     (default: .art-cache)
# Then: python3 tools/art/build_art.py .art-cache
# Needs: git, curl, unzip, python3 with gdown (pip install gdown) for the Quaternius packs.
set -euo pipefail
CACHE="${1:-.art-cache}"
mkdir -p "$CACHE"
cd "$CACHE"

echo "== KayKit (GitHub)"
for repo in KayKit-Character-Pack-Adventures-1.0 KayKit-Character-Pack-Skeletons-1.0 \
            KayKit-Medieval-Hexagon-Pack-1.0 KayKit-Dungeon-Remastered-1.0; do
  [[ -d "$repo" ]] || git clone --depth 1 "https://github.com/KayKit-Game-Assets/$repo.git"
done

echo "== Kenney (kenney.nl)"
for kit in nature-kit fantasy-town-kit graveyard-kit; do
  [[ -d "kenney_$kit" ]] && continue
  url=$(curl -fsSL "https://kenney.nl/assets/$kit" | grep -oE "https://kenney.nl/media/pages/assets/$kit/[^\"]+\.zip" | head -n 1)
  curl -fsSL -o "kenney_$kit.zip" "$url"
  mkdir -p "kenney_$kit" && unzip -qo "kenney_$kit.zip" -d "kenney_$kit" && rm "kenney_$kit.zip"
done

echo "== Quaternius (Google Drive via gdown)"
command -v gdown >/dev/null || { echo "gdown not found: pip install gdown"; exit 1; }
[[ -d "Ultimate Monsters" ]] || gdown --folder "https://drive.google.com/drive/folders/18m4KpzpEzhC9wl7jzr6dUc0N8Jozr79C"
[[ -d "Ultimate Animated Animals - July 2021" ]] || gdown --folder "https://drive.google.com/drive/folders/1uJ3N5HfB7jKTseJUNQr3N4YaN0UuEtHk"

echo "== game-icons.net (UI icons, CC BY 3.0)"
[[ -d icons ]] || git clone --depth 1 https://github.com/game-icons/icons.git

echo "Done. Now run: python3 tools/art/build_art.py $CACHE   (and optionally: task ui:icons)"

#!/usr/bin/env bash
# Downloads the CC0 source art packs used by build_art.py into a cache folder.
#   tools/art/fetch_sources.sh [cache-dir]     (default: .art-cache)
# Then: python3 tools/art/build_art.py .art-cache
# Needs: git, curl, unzip, python3 with gdown (pip install gdown) for the Quaternius Drive packs, and Pillow for build_art.py.
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
for kit in nature-kit fantasy-town-kit graveyard-kit holiday-kit; do
  [[ -d "kenney_$kit" ]] && continue
  url=$(curl -fsSL "https://kenney.nl/assets/$kit" | grep -oE "https://kenney.nl/media/pages/assets/$kit/[^\"]+\.zip" | head -n 1)
  curl -fsSL -o "kenney_$kit.zip" "$url"
  mkdir -p "kenney_$kit" && unzip -qo "kenney_$kit.zip" -d "kenney_$kit" && rm "kenney_$kit.zip"
done

echo "== Quaternius (Google Drive via gdown)"
command -v gdown >/dev/null || { echo "gdown not found: pip install gdown"; exit 1; }
[[ -d "Ultimate Monsters" ]] || gdown --folder "https://drive.google.com/drive/folders/18m4KpzpEzhC9wl7jzr6dUc0N8Jozr79C"
[[ -d "Ultimate Animated Animals - July 2021" ]] || gdown --folder "https://drive.google.com/drive/folders/1uJ3N5HfB7jKTseJUNQr3N4YaN0UuEtHk"

echo "== itch.io (free CC0 packs: Quaternius Stylized Nature MegaKit, KayKit Resource Bits)"
# Downloads the first file of a free itch.io page whose name matches a pattern (itch's "download" flow).
itch_download() {
  local page="$1" pattern="$2" out="$3" jar csrf url upload file_url
  jar=$(mktemp)
  csrf=$(curl -fsSL -c "$jar" -b "$jar" "$page" | grep -oE 'name="csrf_token" value="[^"]+"' | head -1 | sed 's/.*value="//;s/"//')
  url=$(curl -fsSL -c "$jar" -b "$jar" -X POST --data-urlencode "csrf_token=$csrf" "$page/download_url" | python3 -c 'import json,sys;print(json.load(sys.stdin)["url"])')
  curl -fsSL -c "$jar" -b "$jar" "$url" > "$jar.html"
  upload=$(python3 -I -c 'import re,sys
h=open(sys.argv[1]).read()
for m in re.finditer(r"data-upload_id=\"(\d+)\".*?class=\"name\"[^>]*>([^<]+)",h,re.S):
    if re.search(sys.argv[2],m.group(2)): print(m.group(1)); break' "$jar.html" "$pattern")
  csrf=$(grep -oE 'name="csrf_token" value="[^"]+"' "$jar.html" | head -1 | sed 's/.*value="//;s/"//')
  file_url=$(curl -fsSL -c "$jar" -b "$jar" -X POST -H "Referer: $url" --data-urlencode "csrf_token=$csrf" \
    "$page/file/$upload?source=game_download&after_download_lightbox=1" | python3 -c 'import json,sys;print(json.load(sys.stdin)["url"])')
  curl -fsSL -o "$out" "$file_url"
  rm -f "$jar" "$jar.html"
}
if [[ ! -d quaternius-nature ]]; then
  itch_download https://quaternius.itch.io/stylized-nature-megakit "Standard" quaternius-nature.zip
  mkdir -p quaternius-nature && unzip -qo quaternius-nature.zip -d quaternius-nature && rm quaternius-nature.zip
fi
if [[ ! -d kaykit-resource-bits ]]; then
  itch_download https://kaylousberg.itch.io/resource-bits "." kaykit-resource-bits.zip
  mkdir -p kaykit-resource-bits && unzip -qo kaykit-resource-bits.zip -d kaykit-resource-bits && rm kaykit-resource-bits.zip
fi

echo "== game-icons.net (UI icons, CC BY 3.0)"
[[ -d icons ]] || git clone --depth 1 https://github.com/game-icons/icons.git

echo "Done. Now run: python3 tools/art/build_art.py $CACHE   (and optionally: task ui:icons)"

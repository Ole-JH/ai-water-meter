#!/usr/bin/env python3
"""
Builds the game's music into Assets/Resources/Music from CC0 tracks on OpenGameArt:

  * downloads each track into .art-cache/music (once)
  * trims long pieces (with a fade-out), normalizes loudness, and encodes stereo 32 kHz OGG
  * writes music.json, the playlist the game's MusicDirector reads: context -> tracks (loop or not, intro)

Contexts: login (the login screen), town (Hollowmere), wilds (the open world), graveyard (the Forsaken Graveyard and
the Crypt of the Lich), dungeon, combat (fighting elites or a crowd), boss.

Needs: ffmpeg, curl.   Run: task music:build   (or: python3 tools/audio/build_music.py .art-cache/music)
"""
import json
import os
import subprocess
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Resources", "Music")
OGA = "https://opengameart.org/sites/default/files/"

# context, name, file on OpenGameArt, page, author, options (max = seconds to keep, loop = seamless loop, lufs = target loudness)
TRACKS = [
    ("login", "dark_intro", "Dark%20Intro_0.ogg", "dark-intro", "Nikke", {}),
    ("town", "a_new_town", "025_A_New_Town.mp3", "a-new-town-rpg-theme", "The Cynic Project (cynicmusic)", {}),
    ("town", "minstrel_dance", "Loop_Minstrel_Dance_0.wav", "medieval-minstrel-dance", "RandomMind", {"lufs": -18}),
    ("wilds", "dark_forest", "GameMusic_ForestTheme_24_0.mp3", "dark-forest-theme", "The Cynic Project (cynicmusic)", {}),
    ("wilds", "la_citadelle", "Komiku_-_05_-_La_Citadelle_0.mp3", "la-citadelle", "Komiku (Loyalty Freak Music)", {}),
    ("graveyard", "death_waltz", "death_waltz.ogg", "death-waltz", "northivanastan", {}),
    ("graveyard", "dark_forest", None, None, None, {}),  # shared with the wilds
    ("dungeon", "dungeon_ambience", "dungeon002_0.ogg", "dungeon-ambience", "yd", {"lufs": -20}),
    ("dungeon", "dark_shrine", "qubodup-yd-DarkShrineLoop-OpenGameArt.ogg", "dark-shrine-loop", "qubodup and yd", {"lufs": -20}),
    ("dungeon", "dark_cavern", "dark_cavern_ambient_002.ogg", "dark-cavern-ambient", "Paul Wortmann", {"lufs": -20}),
    ("dungeon", "cold_silence", "cold_silence.ogg", "cold-silence", "Eponasoft", {"max": 210, "lufs": -20}),
    ("combat", "determined_pursuit", "determined_pursuit_loop.wav", "determined-pursuit-epic-orchestra-loop", "Emma_MA", {"loop": True}),
    ("combat", "battle_theme_a", "battleThemeA.mp3", "battle-theme-a", "The Cynic Project (cynicmusic)", {"loop": True}),
    ("combat", "ghosts_and_heroes", "ghosts_n_heroes_2025_loop.ogg", "ghosts-heroes", "Bobjt", {"loop": True}),
    ("boss", "boss_battle_2", "boss_battle_%232_metal_pack.zip", "boss-battle-2-symphonic-metal", "nene", {"loop": True, "intro": True}),
]


def fetch(cache, remote):
    local = os.path.join(cache, remote.replace("%20", "_").replace("%23", ""))
    if not os.path.exists(local):
        print("  downloading", remote)
        subprocess.run(["curl", "-fsSL", "-o", local, OGA + remote], check=True)
    return local


def encode(src, dst, opts):
    """Loudness-normalized stereo 32 kHz OGG; long pieces trimmed with a fade-out (never loops: that would break the seam)."""
    lufs = opts.get("lufs", -16)
    af = [f"loudnorm=I={lufs}:TP=-1.5:LRA=11"]
    args = ["ffmpeg", "-v", "error", "-y", "-i", src]
    if opts.get("max") and not opts.get("loop"):
        args += ["-t", str(opts["max"])]
        af.insert(0, f"afade=t=out:st={opts['max'] - 6}:d=6")
    args += ["-af", ",".join(af), "-ac", "2", "-ar", "32000", "-c:a", "libvorbis", "-q:a", "1", dst]
    subprocess.run(args, check=True)


def main():
    cache = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, ".art-cache", "music")
    os.makedirs(cache, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".ogg"):
            os.remove(os.path.join(OUT, f))

    playlist, credits, built = {}, [], set()
    for ctx, name, remote, page, author, opts in TRACKS:
        entry = {"clip": name, "loop": bool(opts.get("loop"))}
        if remote and name not in built:
            print(name)
            src = fetch(cache, remote)
            if src.endswith(".zip"):  # an opening plus a loop
                subprocess.run(["unzip", "-oq", src, "-d", cache], check=True)
                base = os.path.join(cache, "boss_battle_#2_metal_")
                encode(base + "opening.wav", os.path.join(OUT, name + "_intro.ogg"), {**opts, "loop": False})
                src = base + "loop.wav"
            encode(src, os.path.join(OUT, name + ".ogg"), opts)
            built.add(name)
            credits.append((name, page, author))
        if opts.get("intro"):
            entry["intro"] = name + "_intro"
        playlist.setdefault(ctx, []).append(entry)

    with open(os.path.join(OUT, "music.json"), "w") as f:
        json.dump({"contexts": [{"name": k, "tracks": v} for k, v in playlist.items()]}, f, indent=1)
    with open(os.path.join(OUT, "CREDITS.md"), "w") as f:
        f.write("# Music credits\n\nAll music is **CC0** (public domain), from OpenGameArt. Trimmed, loudness-normalized and re-encoded by "
                "`tools/audio/build_music.py`.\n\n| Track | Composer | Source |\n| --- | --- | --- |\n")
        for name, page, author in credits:
            f.write(f"| `{name}` | {author} | https://opengameart.org/content/{page} |\n")
    size = sum(os.path.getsize(os.path.join(OUT, x)) for x in os.listdir(OUT))
    print(f"Built {len(built)} tracks into {OUT} ({size / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()

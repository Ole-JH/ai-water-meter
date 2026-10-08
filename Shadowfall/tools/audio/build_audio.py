#!/usr/bin/env python3
"""
Builds the game's sound effects into Assets/Resources/Audio:

  * picks clips from CC0 packs downloaded into .art-cache/audio (task audio:fetch):
      Kenney RPG Audio, Impact Sounds, Interface Sounds; rubberduck's 80 CC0 creature SFX, 100 CC0 SFX (1 and 2),
      25 CC0 bang SFX (OpenGameArt)
  * synthesizes the rest (sword swings, spells, level-up, ambience loops) with numpy

Every clip is written as mono OGG named <key>_<n>.ogg; the game picks a random variant of a key.
Needs: numpy, ffmpeg.   Run: task audio:build   (or: python3 tools/audio/build_audio.py .art-cache/audio)
"""
import io
import os
import shutil
import subprocess
import sys
import tempfile
import wave
import zipfile

import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 44100

# key -> list of (zip, file inside the zip)
PICKS = {
    "step_grass": [("kenney_impact-sounds.zip", f"footstep_grass_00{i}.ogg") for i in range(5)],
    "step_stone": [("kenney_impact-sounds.zip", f"footstep_concrete_00{i}.ogg") for i in range(5)],
    "hit_flesh": [("kenney_impact-sounds.zip", f"impactPunch_medium_00{i}.ogg") for i in range(5)],
    "hit_heavy": [("kenney_impact-sounds.zip", f"impactPunch_heavy_00{i}.ogg") for i in range(3)],
    "hit_bone": [("kenney_impact-sounds.zip", f"impactWood_light_00{i}.ogg") for i in range(4)],
    "hit_stone": [("kenney_impact-sounds.zip", f"impactMining_00{i}.ogg") for i in range(3)],
    "hit_armor": [("kenney_impact-sounds.zip", f"impactPlate_medium_00{i}.ogg") for i in range(4)],
    "mine": [("kenney_impact-sounds.zip", f"impactMining_00{i}.ogg") for i in range(5)],
    "chop": [("kenney_rpg-audio.zip", "chop.ogg")] + [("kenney_impact-sounds.zip", f"impactWood_heavy_00{i}.ogg") for i in range(3)],
    "anvil": [("kenney_impact-sounds.zip", f"impactMetal_heavy_00{i}.ogg") for i in range(4)],
    "coins": [("kenney_rpg-audio.zip", "handleCoins.ogg"), ("kenney_rpg-audio.zip", "handleCoins2.ogg")],
    "loot": [("kenney_rpg-audio.zip", "beltHandle1.ogg"), ("kenney_rpg-audio.zip", "beltHandle2.ogg"), ("kenney_rpg-audio.zip", "handleSmallLeather.ogg")],
    "equip": [("kenney_rpg-audio.zip", "cloth1.ogg"), ("kenney_rpg-audio.zip", "cloth2.ogg"), ("kenney_rpg-audio.zip", "metalLatch.ogg")],
    "drop": [("kenney_rpg-audio.zip", "dropLeather.ogg")],
    "door_open": [("kenney_rpg-audio.zip", "doorOpen_1.ogg"), ("kenney_rpg-audio.zip", "doorOpen_2.ogg")],
    "door_close": [("kenney_rpg-audio.zip", f"doorClose_{i}.ogg") for i in (1, 2, 3, 4)],
    "book": [("kenney_rpg-audio.zip", "bookOpen.ogg"), ("kenney_rpg-audio.zip", "bookFlip1.ogg")],
    "ui_click": [("kenney_interface-sounds.zip", f"click_00{i}.ogg") for i in (1, 2, 3)],
    "ui_open": [("kenney_interface-sounds.zip", "open_001.ogg"), ("kenney_interface-sounds.zip", "open_002.ogg")],
    "ui_close": [("kenney_interface-sounds.zip", "close_001.ogg"), ("kenney_interface-sounds.zip", "close_002.ogg")],
    "ui_error": [("kenney_interface-sounds.zip", "error_004.ogg"), ("kenney_interface-sounds.zip", "error_006.ogg")],
    "ui_confirm": [("kenney_interface-sounds.zip", "confirmation_002.ogg")],
    "splash": [("100-CC0-SFX_0.zip", "splash_01.ogg"), ("100-CC0-SFX_0.zip", "splash_02.ogg")],
    "explosion": [("25-CC0-bang-sfx.zip", f"bang_0{i}.ogg") for i in (1, 3, 5, 7)],
    "boom": [("25-CC0-bang-sfx.zip", f"cannon_0{i}.ogg") for i in (1, 2, 3)] + [("100-CC0-SFX_0.zip", "explosion.ogg")],
    "shatter": [("100-CC0-SFX_0.zip", f"glass_0{i}.ogg") for i in (1, 2, 3)],
    "rubble": [("sfx_100_v2.zip", f"sfx100v2_stones_0{i}.ogg") for i in (1, 2, 3)],
    "gong": [("100-CC0-SFX_0.zip", "gong_01.ogg")],
    "bell": [("100-CC0-SFX_0.zip", "bell_01.ogg"), ("100-CC0-SFX_0.zip", "bell_02.ogg")],
    "water_loop": [("sfx_100_v2.zip", "sfx100v2_loop_water_01.ogg")],
    # creatures
    "wolf_howl": [("80-CC0-creature-SFX_0.zip", "howl.ogg")],
    "wolf_attack": [("80-CC0-creature-SFX_0.zip", "barking_01.ogg"), ("80-CC0-creature-SFX_0.zip", "barking_02.ogg")],
    "goblin": [("80-CC0-creature-SFX_0.zip", f"grunt_0{i}.ogg") for i in range(1, 6)],
    "goblin_die": [("80-CC0-creature-SFX_0.zip", f"hurt_0{i}.ogg") for i in range(1, 6)],
    "brute": [("80-CC0-creature-SFX_0.zip", f"troll_0{i}.ogg") for i in range(1, 4)],
    "roar": [("80-CC0-creature-SFX_0.zip", f"roar_0{i}.ogg") for i in range(1, 4)],
    "undead": [("80-CC0-creature-SFX_0.zip", f"monster_0{i}.ogg") for i in range(1, 8)],
    "undead_die": [("80-CC0-creature-SFX_0.zip", "breath.ogg"), ("80-CC0-creature-SFX_0.zip", "weird_01.ogg"), ("80-CC0-creature-SFX_0.zip", "weird_02.ogg")],
    "scream": [("80-CC0-creature-SFX_0.zip", "scream_01.ogg"), ("80-CC0-creature-SFX_0.zip", "scream_02.ogg")],
}


# ----------------------------------------------------------------------------- synthesis helpers

def t(sec):
    return np.arange(int(SR * sec)) / SR


def env(n, attack, release, curve=2.0):
    """Attack-release envelope over n samples (attack/release in seconds)."""
    e = np.ones(n)
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r) ** curve
    return e


def lowpass(x, cutoff):
    """One-pole low-pass; cutoff may be an array (sweeps)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    s = 0.0
    for i in range(len(x)):
        s = (1 - a[i]) * x[i] + a[i] * s
        y[i] = s
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def bandnoise(n, lo, hi, rng):
    return highpass(lowpass(rng.standard_normal(n), hi), lo)


def bell(freq, sec, decay=1.5, partials=((1, 1), (2.01, 0.5), (2.76, 0.35), (5.4, 0.15), (8.9, 0.06))):
    tt = t(sec)
    out = np.zeros_like(tt)
    for mul, amp in partials:
        out += amp * np.sin(2 * np.pi * freq * mul * tt) * np.exp(-tt * decay * mul ** 0.5)
    return out * env(len(tt), 0.003, 0.05)


def crackle(n, density, rng):
    x = np.zeros(n)
    idx = rng.random(n) < density / SR
    x[idx] = rng.uniform(-1, 1, idx.sum())
    return lowpass(highpass(x, 1500), 7000) * 6


def norm(x, peak=0.85):
    m = np.max(np.abs(x)) or 1.0
    return x / m * peak


def seamless(x, fade=0.5):
    """Crossfade the tail into the head so the clip loops without a click."""
    f = int(fade * SR)
    head, tail = x[:f], x[-f:]
    w = np.linspace(0, 1, f)
    body = x[f:-f] if len(x) > 2 * f else x[f:]
    return np.concatenate([tail * (1 - w) + head * w, body])


def choir(freqs, sec, rng, attack=0.25, release=0.9):
    """Detuned, vibrato'd voices with a few soft harmonics: an angelic pad."""
    tt = t(sec)
    n = len(tt)
    x = np.zeros(n)
    for f in freqs:
        for detune in (-0.004, 0.0, 0.005):
            vib = 1 + 0.004 * np.sin(2 * np.pi * rng.uniform(4.5, 5.5) * tt + rng.uniform(0, 6))
            ph = 2 * np.pi * np.cumsum(f * (1 + detune) * vib) / SR
            x += np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)
    x = lowpass(x, 2600) * env(n, attack, release, 1.5)
    x += bandnoise(n, 5000, 11000, rng) * env(n, 0.2, 0.8) * 0.15 * np.max(np.abs(x)) / 3
    return x


def class_spells(rng):
    """Sounds for the class abilities (appended last so the earlier clips keep their random sequence)."""
    s = {}
    # Holy Bolt: a quick rising shimmer "whoosh" of light.
    for i in range(2):
        sec = 0.5
        tt = t(sec)
        n = len(tt)
        sweep = np.linspace(700, 1500, n) * rng.uniform(0.95, 1.05)
        ph = 2 * np.pi * np.cumsum(sweep) / SR
        tone = (np.sin(ph) + 0.4 * np.sin(2 * ph) + 0.2 * np.sin(3.01 * ph)) * np.exp(-tt * 7)
        air = lowpass(highpass(rng.standard_normal(n), 1500), np.linspace(7000, 2500, n)) * env(n, 0.01, 0.35)
        s.setdefault("holy_bolt", []).append(norm(tone * 0.5 + air * 0.6 + crackle(n, 400, rng) * np.exp(-tt * 9) * 0.2, 0.7))
    # Lightning: dense crackle over a buzzing arc.
    for i in range(2):
        sec = 0.55
        tt = t(sec)
        n = len(tt)
        f = 90 * (1 + 0.3 * lowpass(rng.standard_normal(n), 30))
        buzz = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.4
        x = highpass(buzz, 300) * env(n, 0.002, 0.4) + crackle(n, 2500, rng) * env(n, 0.002, 0.45) * 1.5
        x += bandnoise(n, 2000, 9000, rng) * np.exp(-tt * 10) * 0.8
        s.setdefault("zap", []).append(norm(x, 0.75))
    # Throwing axe: a spinning whoosh (noise chopped by the rotation).
    for i in range(2):
        sec = 0.5
        tt = t(sec)
        n = len(tt)
        spin = 0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(13, 17) * tt) ** 2
        x = lowpass(highpass(rng.standard_normal(n), 400), 2800) * spin * np.sin(np.linspace(0, np.pi, n)) ** 0.8
        s.setdefault("throw", []).append(norm(x, 0.65))
    # Bow: a plucked string (Karplus-Strong) and a short release whoosh.
    for i in range(3):
        sec = 0.45
        n = int(SR * sec)
        period = int(SR / rng.uniform(95, 125))
        buf = rng.uniform(-1, 1, period)
        out = np.zeros(n)
        for k in range(n):
            out[k] = buf[k % period]
            buf[k % period] = 0.5 * (buf[k % period] + buf[(k + 1) % period]) * 0.994
        whoosh = lowpass(highpass(rng.standard_normal(n), 1200), 5000) * env(n, 0.005, 0.25) * 0.5
        s.setdefault("bow", []).append(norm(out + whoosh, 0.6))
    # Smoke bomb: a soft low "poof" and a hiss.
    n = int(SR * 0.7)
    tt = t(0.7)
    x = lowpass(rng.standard_normal(n), 700) * np.exp(-tt * 7) + bandnoise(n, 3000, 8000, rng) * env(n, 0.02, 0.5) * 0.3
    s["poof"] = [norm(x, 0.7)]
    # Teleport / blink: a fast rising sweep with sparkle.
    n = int(SR * 0.4)
    tt = t(0.4)
    ph = 2 * np.pi * np.cumsum(np.linspace(250, 2600, n)) / SR
    x = (np.sin(ph) + 0.3 * np.sin(2 * ph)) * env(n, 0.01, 0.2) + crackle(n, 600, rng) * env(n, 0.05, 0.3) * 0.3
    s["blink"] = [norm(x, 0.6)]
    return s


# ----------------------------------------------------------------------------- synthesized sounds

def synth(rng):
    s = {}
    # Sword swings: band-passed noise sweeping up then down.
    for i in range(3):
        n = int(SR * rng.uniform(0.22, 0.3))
        sweep = np.concatenate([np.linspace(700, 3800, n // 2), np.linspace(3800, 900, n - n // 2)]) * rng.uniform(0.85, 1.15)
        x = lowpass(highpass(rng.standard_normal(n), 400), sweep) * np.sin(np.linspace(0, np.pi, n)) ** 1.5
        s.setdefault("swing", []).append(norm(x, 0.7))
    n = int(SR * 0.5)
    sweep = np.concatenate([np.linspace(300, 2400, n // 2), np.linspace(2400, 500, n - n // 2)])
    s["swing_heavy"] = [norm(lowpass(highpass(rng.standard_normal(n), 150), sweep) * np.sin(np.linspace(0, np.pi, n)) ** 1.2, 0.8)]

    # Fireball launch: whoosh + crackle + low thump.
    for i in range(2):
        n = int(SR * 0.7)
        whoosh = lowpass(highpass(rng.standard_normal(n), 200), np.linspace(2500, 600, n)) * env(n, 0.04, 0.5, 1.5)
        x = whoosh + crackle(n, 120, rng) * env(n, 0.01, 0.6) * 0.5
        x += np.sin(2 * np.pi * 70 * t(0.7)) * np.exp(-t(0.7) * 9) * 0.6
        s.setdefault("fire_cast", []).append(norm(x, 0.8))

    # Frost: shimmering high partials + icy hiss.
    n = int(SR * 1.1)
    tt = t(1.1)
    shimmer = sum(np.sin(2 * np.pi * f * tt + rng.uniform(0, 6)) * rng.uniform(0.3, 1) for f in rng.uniform(2500, 7000, 14))
    shimmer *= (0.5 + 0.5 * np.sin(2 * np.pi * 18 * tt)) * np.exp(-tt * 3)
    hiss = bandnoise(n, 3000, 9000, rng) * env(n, 0.005, 0.8)
    s["frost_cast"] = [norm(shimmer * 0.4 + hiss + crackle(n, 300, rng) * np.exp(-tt * 4) * 0.4, 0.75)]

    # Holy light: a soft, swelling choir-like chord with a high shimmer (no bells: those read as a clock tower).
    s["holy_cast"] = [norm(choir((261.63, 329.63, 392.0, 523.25), 1.4, rng), 0.6)]

    # Level up: ascending arpeggio.
    notes = (392.0, 493.88, 587.33, 783.99, 987.77)
    n = int(SR * 2.2)
    x = np.zeros(n)
    for k, f in enumerate(notes):
        b = bell(f, 1.6, decay=1.6)
        o = int(k * 0.11 * SR)
        x[o:o + len(b)] += b[: n - o]
    s["levelup"] = [norm(x, 0.75)]

    # Quest complete: two-note fanfare chord.
    n = int(SR * 1.6)
    x = np.zeros(n)
    for o, chord in ((0, (440, 554.37)), (int(0.18 * SR), (587.33, 739.99, 880))):
        for f in chord:
            b = bell(f, 1.4, decay=1.8)
            x[o:o + len(b)] += b[: n - o]
    s["quest_done"] = [norm(x, 0.7)]

    # Meteor: falling whistle getting louder.
    n = int(SR * 0.9)
    tt = t(0.9)
    f = np.linspace(1400, 300, n)
    whistle = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.3
    rush = lowpass(highpass(rng.standard_normal(n), 300), np.linspace(1500, 4000, n))
    s["meteor_fall"] = [norm((whistle + rush) * np.linspace(0.1, 1, n) ** 2, 0.8)]

    # Potion: bubbly gulps.
    n = int(SR * 0.6)
    x = np.zeros(n)
    for k in range(4):
        o = int((0.05 + k * 0.12) * SR)
        tt = t(0.09)
        g = np.sin(2 * np.pi * np.cumsum(np.linspace(300, 700, len(tt))) / SR) * np.exp(-tt * 30)
        x[o:o + len(g)] += g
    s["potion"] = [norm(lowpass(x, 3000), 0.6)]

    # Cooking sizzle.
    n = int(SR * 1.2)
    s["sizzle"] = [norm(bandnoise(n, 2500, 9000, rng) * (0.6 + 0.4 * rng.random(n)) * env(n, 0.05, 0.4) + crackle(n, 200, rng) * 0.4, 0.6)]

    # Death toll for the hero.
    s["death"] = [norm(bell(110, 3.0, decay=0.6, partials=((1, 1), (2.0, 0.6), (2.4, 0.4), (3.0, 0.25), (4.2, 0.15))), 0.8)]

    # ---- ambience loops
    n = int(SR * 10)
    tt = t(10)
    brown = np.cumsum(rng.standard_normal(n))
    brown = highpass(brown - np.mean(brown), 40)
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * tt / 10 * 2) * np.sin(2 * np.pi * tt / 10 * 3 + 1)
    wind = lowpass(brown, 300 + 500 * gust) * gust
    s["wind_loop"] = [norm(seamless(wind, 1.0), 0.6)]

    n = int(SR * 8)
    x = np.zeros(n)
    for c in range(6):  # a few crickets, each chirping in trains
        f = rng.uniform(4200, 5200)
        period = rng.uniform(0.6, 1.1)
        start = rng.uniform(0, period)
        for k in np.arange(start, 8, period):
            for p in range(3):
                o = int((k + p * 0.035) * SR)
                tt = t(0.02)
                chirp = np.sin(2 * np.pi * f * tt) * np.sin(np.linspace(0, np.pi, len(tt)))
                if o + len(chirp) < n:
                    x[o:o + len(chirp)] += chirp * rng.uniform(0.3, 1.0) / (1 + c * 0.3)
    s["crickets_loop"] = [norm(seamless(x, 0.3), 0.5)]

    n = int(SR * 6)
    tt = t(6)
    roar = lowpass(rng.standard_normal(n), 900) * (0.7 + 0.3 * np.sin(2 * np.pi * tt * 0.5)) * 0.4
    s["fire_loop"] = [norm(seamless(roar + crackle(n, 60, rng) + crackle(n, 15, rng) * 2, 0.5), 0.6)]
    return s


def weather_sounds(rng):
    """Rain, thunder, snow and the seasonal town work: shovels, rakes, rustling leaf piles."""
    s = {}
    n = int(SR * 8)
    hiss = bandnoise(n, 900, 9000, rng) * 0.35 + lowpass(rng.standard_normal(n), 500) * 0.25
    drops = crackle(n, 900, rng) * 0.5 + crackle(n, 120, rng)
    s["rain_loop"] = [norm(seamless(hiss + drops, 0.6), 0.55)]
    for k in range(3):
        sec = 4.5 + k
        m = int(SR * sec)
        tt = t(sec)
        rumble = lowpass(rng.standard_normal(m), 140 + 60 * k) * (0.6 + 0.4 * np.sin(2 * np.pi * tt * rng.uniform(1.5, 3)))
        crack = bandnoise(m, 400, 5000, rng) * np.exp(-tt * (9 + 4 * k))
        s.setdefault("thunder", []).append(norm((rumble * 3 + crack * 0.6) * env(m, 0.01 + 0.2 * k, sec * 0.7)))
    for k in range(4):
        m = int(SR * 0.22)
        crunch = bandnoise(m, 900, 5500, rng) * env(m, 0.01, 0.15) + crackle(m, 900, rng) * 0.4
        s.setdefault("step_snow", []).append(norm(crunch, 0.5))
    for k in range(3):
        m = int(SR * 0.7)
        tt = t(0.7)
        scrape = bandnoise(m, 1200, 4500, rng) * np.clip(np.sin(np.pi * tt / 0.45), 0, 1) * 0.6
        thump = lowpass(rng.standard_normal(m), 300) * np.exp(-np.maximum(tt - 0.5, 0) * 30) * (tt > 0.5)
        s.setdefault("shovel", []).append(norm(scrape + thump * 2, 0.6))
    for k in range(2):
        m = int(SR * 0.9)
        tt = t(0.9)
        tines = bandnoise(m, 2500, 9000, rng) * (0.5 + 0.5 * np.sin(2 * np.pi * tt * 23)) * np.sin(np.pi * tt / 0.9)
        rustle = crackle(m, 500, rng) * np.sin(np.pi * tt / 0.9) * 0.6
        s.setdefault("rake", []).append(norm(tines * 0.5 + rustle, 0.5))
    for k in range(3):
        m = int(SR * 1.1)
        s.setdefault("leaves", []).append(norm((crackle(m, 2500, rng) + bandnoise(m, 2000, 8000, rng) * 0.4) * env(m, 0.01, 0.9), 0.75))
    return s


# ----------------------------------------------------------------------------- output

def ffmpeg_to_ogg(src_bytes, dst, is_wav):
    with tempfile.NamedTemporaryFile(suffix=".wav" if is_wav else ".ogg", delete=False) as f:
        f.write(src_bytes)
        tmp = f.name
    try:
        subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", tmp, "-ac", "1", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "4", dst], check=True)
    finally:
        os.unlink(tmp)


def wav_bytes(x):
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    return buf.getvalue()


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, ".art-cache", "audio")
    if os.path.isdir(OUT):
        for f in os.listdir(OUT):
            if f.endswith((".ogg", ".ogg.meta")):
                os.remove(os.path.join(OUT, f))
    os.makedirs(OUT, exist_ok=True)

    zips = {}
    count = 0
    for key, picks in PICKS.items():
        for i, (zname, fname) in enumerate(picks):
            z = zips.get(zname) or zips.setdefault(zname, zipfile.ZipFile(os.path.join(src, zname)))
            member = next(n for n in z.namelist() if n.endswith("/" + fname) or n == fname)
            ffmpeg_to_ogg(z.read(member), os.path.join(OUT, f"{key}_{i}.ogg"), False)
            count += 1

    clips_by_key = synth(np.random.default_rng(7))
    clips_by_key.update(class_spells(np.random.default_rng(11)))
    clips_by_key.update(weather_sounds(np.random.default_rng(23)))
    for key, clips in clips_by_key.items():
        for i, x in enumerate(clips):
            ffmpeg_to_ogg(wav_bytes(x), os.path.join(OUT, f"{key}_{i}.ogg"), True)
            count += 1

    credits = os.path.join(os.path.dirname(__file__), "CREDITS.md")
    if os.path.exists(credits):
        shutil.copy(credits, os.path.join(OUT, "CREDITS.md"))
    total = sum(os.path.getsize(os.path.join(OUT, f)) for f in os.listdir(OUT) if f.endswith(".ogg"))
    print(f"Wrote {count} clips ({total / 1024 / 1024:.1f} MB) to {os.path.relpath(OUT, ROOT)}")


if __name__ == "__main__":
    main()

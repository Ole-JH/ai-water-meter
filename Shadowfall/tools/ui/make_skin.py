#!/usr/bin/env python3
"""
Generates the dark "gothic" UI skin used by UISkin.cs into Assets/Resources/UI/Gothic:
panels with bronze frames, gold trim and corner ornaments, title plates, buttons, insets, item slots,
text fields, tooltips, bar frames and fills, ornamental dividers and a close button.

Everything is drawn procedurally (numpy + Pillow) at 4x and downsampled for smooth edges, so the skin is
ours to license (CC0). The 9-slice border sizes used by UISkin are noted next to each texture.
Run: task ui:skin   (or: python3 tools/ui/make_skin.py)
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "Gothic")
SS = 4  # supersampling

GOLD = (214, 168, 92)
GOLD_LIGHT = (246, 214, 140)
GOLD_DARK = (128, 92, 44)
BRONZE = (74, 52, 30)
BRONZE_LIGHT = (122, 88, 52)
BRONZE_DARK = (34, 23, 13)
INK = (13, 10, 8)

rng = np.random.default_rng(3)


def noise(w, h, scale, amp):
    """Smooth value noise in [-amp, amp]."""
    small = rng.standard_normal((max(2, h // scale + 2), max(2, w // scale + 2)))
    img = Image.fromarray(((small - small.min()) / (np.ptp(small) + 1e-9) * 255).astype(np.uint8))
    img = img.resize((w, h), Image.BICUBIC)
    return (np.asarray(img).astype(np.float32) / 255.0 - 0.5) * 2 * amp


def leather(w, h, base=(20, 15, 12), alpha=242):
    """Dark grainy leather/stone fill with darker edges."""
    n = noise(w, h, 18, 7) + noise(w, h, 5, 4) + rng.normal(0, 2.0, (h, w))
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)).astype(np.float32)
    vign = np.clip(edge / (min(w, h) * 0.25), 0, 1) * 6 - 6
    rgb = np.stack([np.clip(base[i] + n + vign, 0, 255) for i in range(3)], -1)
    a = np.full((h, w, 1), alpha, np.float32)
    return Image.fromarray(np.concatenate([rgb, a], -1).astype(np.uint8), "RGBA")


def big(w, h, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (w * SS, h * SS), color)


def down(img):
    return img.resize((img.width // SS, img.height // SS), Image.LANCZOS)


def bevel_frame(d, w, h, inset, thick, light, dark, mid=None):
    """A bevelled rectangular frame (coordinates in final pixels; drawn at SS)."""
    s = SS
    x0, y0, x1, y1 = inset * s, inset * s, (w - inset) * s - 1, (h - inset) * s - 1
    t = thick * s
    if mid:
        d.rectangle([x0, y0, x1, y1], outline=mid, width=t)
    # top/left light, bottom/right dark (as thin lines on top of the mid band)
    d.line([x0, y0, x1, y0], fill=light, width=s)
    d.line([x0, y0, x0, y1], fill=light, width=s)
    d.line([x0, y1, x1, y1], fill=dark, width=s)
    d.line([x1, y0, x1, y1], fill=dark, width=s)


def corner_ornament(d, cx, cy, sx, sy, size, color, glow=None):
    """Gold filigree in a corner: an L bracket with a curl and a diamond. sx/sy = +1/-1 direction into the panel."""
    s = SS
    L = size * s
    lw = max(1, int(2.2 * s))
    # L bracket
    d.line([cx, cy, cx + sx * L, cy], fill=color, width=lw)
    d.line([cx, cy, cx, cy + sy * L], fill=color, width=lw)
    # inner smaller bracket
    o = 4 * s
    d.line([cx + sx * o, cy + sy * o, cx + sx * L * 0.6, cy + sy * o], fill=color, width=max(1, lw // 2))
    d.line([cx + sx * o, cy + sy * o, cx + sx * o, cy + sy * L * 0.6], fill=color, width=max(1, lw // 2))
    # end dots
    r = 1.6 * s
    for px, py in ((cx + sx * L, cy), (cx, cy + sy * L)):
        d.ellipse([px - r, py - r, px + r, py + r], fill=color)
    # corner diamond
    k = 4.5 * s
    d.polygon([(cx, cy - k), (cx + k, cy), (cx, cy + k), (cx - k, cy)], fill=color, outline=GOLD_DARK)
    k2 = 1.6 * s
    d.polygon([(cx, cy - k2), (cx + k2, cy), (cx, cy + k2), (cx - k2, cy)], fill=GOLD_LIGHT)


def panel(w=160, h=160, ornaments=True, base=(20, 15, 12), alpha=242, trim=GOLD):
    img = leather(w, h, base, alpha).resize((w * SS, h * SS), Image.NEAREST)
    d = ImageDraw.Draw(img)
    # outer bronze frame with bevel
    d.rectangle([0, 0, w * SS - 1, h * SS - 1], outline=BRONZE_DARK + (255,), width=SS)
    d.rectangle([SS, SS, w * SS - 1 - SS, h * SS - 1 - SS], outline=BRONZE + (255,), width=3 * SS)
    bevel_frame(d, w, h, 1, 3, BRONZE_LIGHT + (255,), BRONZE_DARK + (255,))
    # inner gold trim
    i = 7
    d.rectangle([i * SS, i * SS, (w - i) * SS - 1, (h - i) * SS - 1], outline=trim + (230,), width=int(1.3 * SS))
    d.rectangle([(i + 2) * SS, (i + 2) * SS, (w - i - 2) * SS - 1, (h - i - 2) * SS - 1], outline=(0, 0, 0, 120), width=SS)
    if ornaments:
        c = 7 * SS
        for (cx, cy, sx, sy) in ((c, c, 1, 1), (w * SS - c, c, -1, 1), (c, h * SS - c, 1, -1), (w * SS - c, h * SS - c, -1, -1)):
            corner_ornament(d, cx, cy, sx, sy, 24, trim + (255,))
    return down(img)


def title_plate(w=320, h=52):
    img = big(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    p = 22  # pointed ends
    poly = [(0, h * s / 2), (p * s, 4 * s), ((w - p) * s, 4 * s), (w * s - 1, h * s / 2), ((w - p) * s, (h - 4) * s), (p * s, (h - 4) * s)]
    # vertical gradient fill
    grad = Image.new("RGBA", (w * s, h * s))
    g = np.linspace(0, 1, h * s)[:, None]
    top, bot = np.array([84, 26, 18]), np.array([30, 9, 7])
    arr = (top * (1 - g) + bot * g)[:, None, :].repeat(w * s, 1)
    grad = Image.fromarray(np.concatenate([arr, np.full((h * s, w * s, 1), 250)], -1).astype(np.uint8), "RGBA")
    mask = Image.new("L", (w * s, h * s), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    img.paste(grad, (0, 0), mask)
    d.polygon(poly, outline=GOLD + (255,), width=int(1.6 * s))
    inner = [(x + (6 * s if x < w * s / 2 else -6 * s) if x not in (0, w * s - 1) else (x + 7 * s if x == 0 else x - 7 * s), y) for x, y in poly]
    d.polygon(inner, outline=GOLD_DARK + (200,), width=s)
    # highlight line
    d.line([(p * s, 7 * s), ((w - p) * s, 7 * s)], fill=(255, 200, 150, 60), width=s)
    return down(img)


def button(w=192, h=52, state="normal"):
    s = SS
    img = big(w, h)
    d = ImageDraw.Draw(img)
    if state == "hover":
        top, bot, rim = np.array([120, 40, 24]), np.array([52, 14, 9]), GOLD_LIGHT
    elif state == "pressed":
        top, bot, rim = np.array([30, 9, 7]), np.array([70, 22, 14]), GOLD_DARK
    else:
        top, bot, rim = np.array([92, 30, 19]), np.array([36, 10, 7]), GOLD
    g = np.linspace(0, 1, h * s)[:, None]
    arr = (top * (1 - g) + bot * g)[:, None, :].repeat(w * s, 1)
    n = noise(w * s, h * s, 24, 4)[..., None]
    arr = np.clip(arr + n, 0, 255)
    fill = Image.fromarray(np.concatenate([arr, np.full((h * s, w * s, 1), 255)], -1).astype(np.uint8), "RGBA")
    r = 5 * s
    mask = Image.new("L", (w * s, h * s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([2 * s, 2 * s, w * s - 2 * s, h * s - 2 * s], r, fill=255)
    img.paste(fill, (0, 0), mask)
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], r + 2 * s, outline=BRONZE_DARK + (255,), width=2 * s)
    d.rounded_rectangle([2 * s, 2 * s, w * s - 2 * s, h * s - 2 * s], r, outline=rim + (255,), width=int(1.4 * s))
    if state != "pressed":
        d.line([(8 * s, 5 * s), (w * s - 8 * s, 5 * s)], fill=(255, 210, 170, 70), width=s)
    # small diamonds at the ends
    for cx in ((9 * s, w * s - 9 * s) if w > 2 * h else ()):
        cy, k = h * s / 2, 3 * s
        d.polygon([(cx, cy - k), (cx + k, cy), (cx, cy + k), (cx - k, cy)], fill=rim + (255,))
    return down(img)


def inset(w=64, h=64, border=BRONZE, glow=None, alpha=225):
    s = SS
    img = big(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w * s - 1, h * s - 1], fill=(13, 11, 9, alpha))
    # inner shadow (top/left)
    for k in range(5):
        a = int(90 * (1 - k / 5))
        d.line([(k * s, k * s), (w * s - 1, k * s)], fill=(0, 0, 0, a), width=s)
        d.line([(k * s, k * s), (k * s, h * s - 1)], fill=(0, 0, 0, a), width=s)
    if glow:
        gl = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
        gd = ImageDraw.Draw(gl)
        gd.rectangle([3 * s, 3 * s, w * s - 3 * s, h * s - 3 * s], outline=glow + (150,), width=4 * s)
        gl = gl.filter(ImageFilter.GaussianBlur(4 * s))
        img = Image.alpha_composite(img, gl)
        d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w * s - 1, h * s - 1], outline=border + (255,), width=int(1.3 * s))
    d.line([(s, h * s - 2 * s), (w * s - s, h * s - 2 * s)], fill=(255, 220, 160, 25), width=s)
    return down(img)


def slot(w=64, h=64, border=BRONZE_LIGHT):
    s = SS
    img = Image.fromarray(np.zeros((h * s, w * s, 4), np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w * s - 1, h * s - 1], fill=BRONZE_DARK + (255,))
    d.rectangle([2 * s, 2 * s, w * s - 2 * s - 1, h * s - 2 * s - 1], fill=(8, 7, 6, 245))
    # radial dark center glow
    yy, xx = np.mgrid[0:h * s, 0:w * s]
    r = np.sqrt((xx - w * s / 2) ** 2 + (yy - h * s / 2) ** 2) / (w * s / 2)
    arr = np.asarray(img).astype(np.float32)
    lift = np.clip(1 - r, 0, 1)[..., None] * np.array([18, 14, 10, 0])
    inner = (xx > 2 * s) & (xx < w * s - 2 * s) & (yy > 2 * s) & (yy < h * s - 2 * s)
    arr[inner] += lift[inner]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    d.rectangle([s, s, w * s - s - 1, h * s - s - 1], outline=border + (255,), width=s)
    d.line([(2 * s, 2 * s), (w * s - 3 * s, 2 * s)], fill=(0, 0, 0, 200), width=2 * s)
    d.line([(2 * s, 2 * s), (2 * s, h * s - 3 * s)], fill=(0, 0, 0, 200), width=2 * s)
    return down(img)


def field(w=96, h=40, focus=False):
    s = SS
    img = big(w, h)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], 4 * s, fill=(5, 4, 3, 215), outline=(GOLD if focus else BRONZE_LIGHT) + (255,), width=int(1.3 * s))
    for k in range(4):
        d.line([(3 * s, (2 + k) * s), (w * s - 3 * s, (2 + k) * s)], fill=(0, 0, 0, 80 - k * 18), width=s)
    if focus:
        gl = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
        ImageDraw.Draw(gl).rounded_rectangle([2 * s, 2 * s, w * s - 2 * s, h * s - 2 * s], 4 * s, outline=GOLD + (120,), width=3 * s)
        img = Image.alpha_composite(img, gl.filter(ImageFilter.GaussianBlur(3 * s)))
    return down(img)


def bar_frame(w=128, h=24):
    s = SS
    img = big(w, h)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], 4 * s, fill=(4, 3, 3, 235), outline=BRONZE_DARK + (255,), width=2 * s)
    d.rounded_rectangle([2 * s, 2 * s, w * s - 2 * s - 1, h * s - 2 * s - 1], 3 * s, outline=BRONZE_LIGHT + (255,), width=s)
    return down(img)


def bar_fill(color, h=32):
    c = np.array(color, np.float32)
    g = np.linspace(0, 1, h)
    rows = []
    for t in g:
        k = 1.25 - 0.55 * t           # darker toward the bottom
        if t < 0.35:
            k += (0.35 - t) * 1.1     # glossy band at the top
        rows.append(np.clip(c * k, 0, 255))
    arr = np.array(rows)[:, None, :].repeat(4, 1)
    a = np.full((h, 4, 1), 255)
    return Image.fromarray(np.concatenate([arr, a], -1).astype(np.uint8), "RGBA")


def divider(w=512, h=24):
    s = SS
    img = big(w, h)
    d = ImageDraw.Draw(img)
    cy = h * s / 2
    # line fading out toward both ends
    for x in range(0, w * s, s):
        t = abs(x / (w * s) - 0.5) * 2
        a = int(255 * max(0.0, 1 - t ** 1.6))
        d.line([(x, cy), (x + s, cy)], fill=GOLD + (a,), width=int(1.4 * s))
    cx = w * s / 2
    for off in (-34, 34):
        r = 2.2 * s
        d.ellipse([cx + off * s - r, cy - r, cx + off * s + r, cy + r], fill=GOLD + (255,))
    k = 9 * s
    d.polygon([(cx, cy - k), (cx + k * 1.6, cy), (cx, cy + k), (cx - k * 1.6, cy)], fill=(40, 12, 8, 255), outline=GOLD + (255,), width=int(1.4 * s))
    k2 = 3.5 * s
    d.polygon([(cx, cy - k2), (cx + k2 * 1.6, cy), (cx, cy + k2), (cx - k2 * 1.6, cy)], fill=GOLD_LIGHT + (255,))
    return down(img)


def close_button(size=32, hover=False):
    s = SS
    img = big(size, size)
    d = ImageDraw.Draw(img)
    c = size * s / 2
    r = c - s
    d.ellipse([c - r, c - r, c + r, c + r], fill=(60, 16, 10, 255) if hover else (34, 10, 7, 255), outline=GOLD + (255,), width=int(1.4 * s))
    k = r * 0.42
    col = GOLD_LIGHT if hover else GOLD
    d.line([(c - k, c - k), (c + k, c + k)], fill=col + (255,), width=int(2.4 * s))
    d.line([(c - k, c + k), (c + k, c - k)], fill=col + (255,), width=int(2.4 * s))
    return down(img)


def orb_layers(size=256, inner=0.84):
    """Health/mana orb: liquid (white, tinted in game), glass highlight and an ornate bronze frame."""
    n = size * SS
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    c = n / 2
    r = np.sqrt((xx - c) ** 2 + (yy - c) ** 2) / c
    ri = inner
    # liquid: brighter in the middle-top, dark rim, a little swirl noise
    swirl = noise(n, n, 40 * SS, 0.08)
    shade = np.clip(1.05 - (r / ri) ** 2 * 0.55 - (yy - c) / n * 0.25 + swirl, 0.25, 1.1)
    a = np.clip((ri - r) * n * 0.5, 0, 1)
    liquid = np.dstack([shade * 255, shade * 255, shade * 255, a * 255])
    # glass: soft elliptical glare top-left + rim shine
    gx = ((xx - c * 0.72) / (n * 0.2)) ** 2 + ((yy - c * 0.55) / (n * 0.12)) ** 2
    glare = np.clip(1 - gx, 0, 1) ** 1.5 * 0.55
    rim = np.clip(1 - np.abs(r - ri * 0.96) * 40, 0, 1) * 0.25 * (yy < c)
    ga = np.clip(glare + rim, 0, 1) * a
    glass = np.dstack([np.full((n, n), 255), np.full((n, n), 250), np.full((n, n), 240), ga * 255])
    # frame: bevelled bronze ring with gold edges and studs
    ring = (r >= ri) & (r <= 0.98)
    t = np.clip((r - ri) / (0.98 - ri), 0, 1)
    light = 0.55 + 0.45 * np.cos((t - 0.35) * np.pi) * (1 - (yy - c) / n * 0.8)
    base = np.array([70, 50, 30], np.float32)
    frame = np.zeros((n, n, 4), np.float32)
    frame[..., :3] = base * light[..., None]
    frame[..., 3] = ring * 255
    for edge in (ri, 0.98, (ri + 0.98) / 2):
        e = np.clip(1 - np.abs(r - edge) * n / (1.6 * SS), 0, 1)
        col = np.array(GOLD if edge != (ri + 0.98) / 2 else GOLD_DARK, np.float32)
        frame[..., :3] = frame[..., :3] * (1 - e[..., None]) + col * e[..., None]
        frame[..., 3] = np.maximum(frame[..., 3], e * 255)
    img = Image.fromarray(np.clip(frame, 0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    mid = (ri + 0.98) / 2 * c
    for k in range(8):  # studs around the ring
        ang = k * np.pi / 4 + np.pi / 8
        px, py = c + np.cos(ang) * mid, c + np.sin(ang) * mid
        rr = 2.2 * SS * size / 128
        d.ellipse([px - rr, py - rr, px + rr, py + rr], fill=GOLD + (255,), outline=GOLD_DARK + (255,), width=SS)
    to = lambda arr: Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    return down(to(liquid)), down(to(glass)), down(img)


def gradient_v(h=256, top=(0, 0, 0, 255), bottom=(0, 0, 0, 0)):
    g = np.linspace(0, 1, h)[:, None]
    arr = np.array(top) * (1 - g) + np.array(bottom) * g
    return Image.fromarray(arr[:, None, :].repeat(4, 1).astype(np.uint8), "RGBA")


def gradient_h(w=256, left=(0, 0, 0, 0), right=(0, 0, 0, 255)):
    g = np.linspace(0, 1, w)[None, :]
    arr = np.array(left)[None, None, :] * (1 - g[..., None]) + np.array(right)[None, None, :] * g[..., None]
    return Image.fromarray(arr.repeat(4, 0).astype(np.uint8), "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    files = {
        "panel": panel(),                                   # border 34
        "panel_plain": panel(ornaments=False),              # border 14
        "tooltip": panel(96, 96, ornaments=False, base=(14, 11, 9), alpha=248),  # border 14
        "title_plate": title_plate(),                       # border 26 x, 0 y
        "button": button(),                                 # border 14
        "button_hover": button(state="hover"),
        "button_pressed": button(state="pressed"),
        "button_square": button(48, 48),                    # border 12
        "button_square_hover": button(48, 48, "hover"),
        "button_square_pressed": button(48, 48, "pressed"),
        "inset": inset(),                                   # border 8
        "inset_selected": inset(border=GOLD, glow=GOLD),    # border 8
        "slot": slot(),                                     # border 6
        "field": field(),                                   # border 8
        "field_focus": field(focus=True),
        "bar_frame": bar_frame(),                           # border 8
        "bar_red": bar_fill((178, 30, 22)),
        "bar_blue": bar_fill((40, 80, 200)),
        "bar_green": bar_fill((48, 150, 52)),
        "bar_yellow": bar_fill((214, 160, 40)),
        "bar_purple": bar_fill((120, 60, 170)),
        "divider": divider(),
        "close": close_button(),
        "close_hover": close_button(hover=True),
        "fade_down": gradient_v(),                          # black at the top, clear at the bottom
        "fade_right": gradient_h(),                         # clear on the left, black on the right
    }
    files["orb_liquid"], files["orb_glass"], files["orb_frame"] = orb_layers()
    for name, img in files.items():
        img.save(os.path.join(OUT, name + ".png"), optimize=True)
    print(f"Wrote {len(files)} textures to {os.path.relpath(OUT)}")


if __name__ == "__main__":
    main()

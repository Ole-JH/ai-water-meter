#!/usr/bin/env python3
"""
Generates the stylized, seamlessly tiling ground textures used by the terrain splat shader:

    Assets/Resources/Ground/<layer>.png   RGB = color, A = height (for height-based blending)
    Assets/Resources/Ground/water_normal.png

Everything is procedural (numpy + Pillow), so the textures are ours to license (CC0, like the
models). Run: task art:ground   (or: python3 tools/art/make_ground.py)
It also prints each layer's average color, which GroundLayers.cs uses to color the grass blades.
"""
import os
import sys

import numpy as np
from PIL import Image

N = 512
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Ground")
LAYERS = ["grass", "forest", "dry", "dead", "dirt", "cobble", "gravel", "sand"]

yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)


# ----------------------------------------------------------------------------- helpers

def periodic_noise(rng, scale, n=N):
    """Band-limited noise that tiles: white noise filtered in the frequency domain. scale = feature size in px."""
    white = rng.standard_normal((n, n))
    f = np.fft.fftfreq(n)
    fx, fy = np.meshgrid(f, f)
    r = np.sqrt(fx * fx + fy * fy)
    sigma = 1.0 / max(scale, 1.0)
    filt = np.exp(-(r / sigma) ** 2)
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * filt))
    out -= out.min()
    out /= out.max() + 1e-9
    return out.astype(np.float32)


def fbm(rng, base_scale, octaves=4, gain=0.5):
    total, amp, norm = np.zeros((N, N), np.float32), 1.0, 0.0
    scale = base_scale
    for _ in range(octaves):
        total += periodic_noise(rng, scale) * amp
        norm += amp
        amp *= gain
        scale /= 2.0
    return total / norm


def voronoi(rng, count, jitter=1.0):
    """Returns (F1, F2, cell id) with toroidal wrap so the result tiles."""
    g = int(np.ceil(np.sqrt(count)))
    pts = []
    for j in range(g):
        for i in range(g):
            pts.append(((i + 0.5 + (rng.random() - 0.5) * jitter) * N / g,
                        (j + 0.5 + (rng.random() - 0.5) * jitter) * N / g))
    pts = np.array(pts, np.float32)
    f1 = np.full((N, N), 1e9, np.float32)
    f2 = np.full((N, N), 1e9, np.float32)
    cid = np.zeros((N, N), np.int32)
    for k, (px, py) in enumerate(pts):
        dx = np.abs(xx - px)
        dy = np.abs(yy - py)
        dx = np.minimum(dx, N - dx)
        dy = np.minimum(dy, N - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        cid = np.where(closer, k, cid)
        f1 = np.where(closer, d, f1)
    return f1, f2, cid, len(pts)


def stamp_ellipses(rng, img, height, count, rx, ry, colors, hval, angle_range=np.pi, shade=0.35):
    """Paints shaded ellipses (pebbles, leaves) with wrap-around. colors: list of RGB tuples."""
    for _ in range(count):
        cx, cy = rng.random() * N, rng.random() * N
        a = rng.uniform(-angle_range, angle_range)
        sx = rng.uniform(*rx)
        sy = rng.uniform(*ry)
        col = np.array(colors[rng.integers(len(colors))], np.float32) * rng.uniform(0.85, 1.15)
        r = int(max(sx, sy)) + 2
        x0, y0 = int(cx) - r, int(cy) - r
        ys = (np.arange(y0, y0 + 2 * r + 1) % N)
        xs = (np.arange(x0, x0 + 2 * r + 1) % N)
        ly, lx = np.mgrid[y0:y0 + 2 * r + 1, x0:x0 + 2 * r + 1].astype(np.float32)
        lx -= cx
        ly -= cy
        ca, sa = np.cos(a), np.sin(a)
        u = (lx * ca + ly * sa) / sx
        v = (-lx * sa + ly * ca) / sy
        d = u * u + v * v
        m = d < 1.0
        if not m.any():
            continue
        # light from the top-left: brighter on that side, darker on the other
        lit = 1.0 + shade * (-(lx * 0.7 + ly * -0.7) / max(sx, sy)) * 0.8
        dome = np.sqrt(np.clip(1.0 - d, 0, 1))
        sub = img[np.ix_(ys, xs)]
        hsub = height[np.ix_(ys, xs)]
        c = col[None, None, :] * (0.75 + 0.25 * dome)[..., None] * lit[..., None]
        sub[m] = c[m]
        hsub[m] = np.maximum(hsub[m], hval * (0.6 + 0.4 * dome[m]))
        img[np.ix_(ys, xs)] = sub
        height[np.ix_(ys, xs)] = hsub
        # soft contact shadow below-right
    return img, height


def blade_strokes(rng, img, height, count, length, width, palette, hval, lean=0.5):
    """Short tapered strokes (painterly grass). Drawn with wrap-around."""
    for _ in range(count):
        cx, cy = rng.random() * N, rng.random() * N
        L = rng.uniform(*length)
        a = -np.pi / 2 + rng.uniform(-lean, lean)  # mostly pointing "up" the texture
        col = np.array(palette[rng.integers(len(palette))], np.float32) * rng.uniform(0.85, 1.15)
        w = rng.uniform(*width)
        r = int(L) + 3
        x0, y0 = int(cx) - r, int(cy) - r
        ys = (np.arange(y0, y0 + 2 * r + 1) % N)
        xs = (np.arange(x0, x0 + 2 * r + 1) % N)
        ly, lx = np.mgrid[y0:y0 + 2 * r + 1, x0:x0 + 2 * r + 1].astype(np.float32)
        lx -= cx
        ly -= cy
        dx, dy = np.cos(a), np.sin(a)
        t = lx * dx + ly * dy           # along the blade
        s = np.abs(-lx * dy + ly * dx)  # across
        tt = t / L
        m = (tt >= 0) & (tt <= 1) & (s < w * (1.0 - tt))
        if not m.any():
            continue
        sub = img[np.ix_(ys, xs)]
        hsub = height[np.ix_(ys, xs)]
        c = col[None, None, :] * (0.7 + 0.5 * tt)[..., None]  # darker root, lighter tip
        sub[m] = c[m]
        hsub[m] = np.maximum(hsub[m], hval * (0.5 + 0.5 * tt[m]))
        img[np.ix_(ys, xs)] = sub
        height[np.ix_(ys, xs)] = hsub
    return img, height


def tint(base, var, low, high):
    """Lerp between two RGB colors by var (N,N) and multiply into base shape."""
    low = np.array(low, np.float32)
    high = np.array(high, np.float32)
    return low[None, None, :] + (high - low)[None, None, :] * var[..., None]


def relief(img, height, strength=0.35):
    """Bakes a little top-left lighting from the height map, so the flat ground reads as bumpy."""
    gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1))
    gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0))
    light = 1.0 - (gx * 0.7 + gy * 0.7) * strength * 4.0
    return img * np.clip(light, 0.6, 1.4)[..., None]


def save(name, img, height):
    img = np.clip(img, 0, 1)
    height = np.clip(height, 0, 1)
    rgba = np.dstack([img, height[..., None]])
    Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"), optimize=True)
    return img.reshape(-1, 3).mean(0)


# ----------------------------------------------------------------------------- layers

def grass(rng, base_lo, base_hi, palette, blades=9000, flecks=None):
    v = fbm(rng, 90, 4)
    img = tint(None, v, base_lo, base_hi)
    img *= (0.9 + 0.2 * fbm(rng, 20, 3))[..., None]
    h = 0.35 + 0.25 * fbm(rng, 30, 3)
    img, h = blade_strokes(rng, img, h, blades, (6, 14), (1.2, 2.4), palette, 0.9)
    if flecks:
        img, h = stamp_ellipses(rng, img, h, flecks[0], (1.5, 3), (1, 2), flecks[1], 0.8)
    return relief(img, h, 0.25), h


def make_grass(rng):
    return grass(rng, (0.20, 0.28, 0.12), (0.31, 0.40, 0.17),
                 [(0.29, 0.40, 0.17), (0.35, 0.46, 0.19), (0.23, 0.33, 0.13), (0.42, 0.48, 0.22)],
                 flecks=(120, [(0.75, 0.72, 0.38), (0.62, 0.66, 0.3)]))


def make_forest(rng):
    img, h = grass(rng, (0.14, 0.20, 0.08), (0.24, 0.30, 0.11),
                   [(0.20, 0.32, 0.12), (0.26, 0.38, 0.14), (0.17, 0.26, 0.10)], blades=5000)
    # fallen leaves and needles
    img, h = stamp_ellipses(rng, img, h, 900, (3, 6), (1.5, 3), [(0.42, 0.28, 0.12), (0.5, 0.36, 0.14), (0.33, 0.22, 0.1), (0.48, 0.42, 0.16)], 0.95)
    moss = fbm(rng, 60, 3)
    img = img * (1 - 0.25 * (moss > 0.6))[..., None] + np.array([0.18, 0.3, 0.1])[None, None, :] * 0.25 * (moss > 0.6)[..., None]
    return relief(img, h, 0.25), h


def make_dry(rng):
    return grass(rng, (0.40, 0.36, 0.17), (0.55, 0.49, 0.24),
                 [(0.62, 0.55, 0.28), (0.52, 0.46, 0.22), (0.68, 0.60, 0.32), (0.45, 0.44, 0.2)], blades=7000,
                 flecks=(250, [(0.45, 0.38, 0.27), (0.5, 0.46, 0.4)]))


def make_dead(rng):
    return grass(rng, (0.22, 0.23, 0.17), (0.33, 0.33, 0.24),
                 [(0.36, 0.36, 0.26), (0.30, 0.31, 0.22), (0.42, 0.40, 0.28), (0.26, 0.28, 0.2)], blades=6000,
                 flecks=(200, [(0.4, 0.38, 0.34), (0.3, 0.28, 0.24)]))


def make_dirt(rng):
    v = fbm(rng, 80, 4)
    img = tint(None, v, (0.30, 0.21, 0.12), (0.47, 0.35, 0.21))
    img *= (0.88 + 0.24 * fbm(rng, 12, 3))[..., None]
    h = 0.25 + 0.3 * fbm(rng, 25, 3)
    # trodden, darker patches and a few puddle-ish dips
    worn = fbm(rng, 50, 3)
    img *= (1 - 0.15 * np.clip((worn - 0.55) * 4, 0, 1))[..., None]
    h -= 0.1 * np.clip((worn - 0.55) * 4, 0, 1)
    img, h = stamp_ellipses(rng, img, h, 170, (2.5, 6), (2, 4.5), [(0.40, 0.35, 0.28), (0.34, 0.29, 0.22), (0.44, 0.39, 0.31), (0.30, 0.24, 0.17)], 0.95, shade=0.2)
    img, h = stamp_ellipses(rng, img, h, 500, (1.2, 2.5), (1, 2), [(0.3, 0.24, 0.16), (0.42, 0.38, 0.32)], 0.7)
    return relief(img, h, 0.4), h


def make_cobble(rng):
    f1, f2, cid, k = voronoi(rng, 64, 0.9)
    edge = f2 - f1
    stone = np.clip(edge / 9.0, 0, 1)
    dome = np.sqrt(stone)
    cols = np.array([(0.50, 0.48, 0.44), (0.45, 0.43, 0.40), (0.55, 0.51, 0.45), (0.42, 0.42, 0.42), (0.52, 0.47, 0.40)], np.float32)
    pick = rng.integers(len(cols), size=k)
    shade = rng.uniform(0.85, 1.12, size=k)
    img = cols[pick][cid] * shade[cid][..., None]
    img *= (0.85 + 0.25 * fbm(rng, 10, 3))[..., None]
    mortar = np.array([0.20, 0.18, 0.15], np.float32)
    m = stone < 0.12
    img = np.where(m[..., None], mortar[None, None, :] * (0.8 + 0.4 * fbm(rng, 8, 2))[..., None], img)
    h = 0.15 + 0.85 * dome * (0.85 + 0.15 * fbm(rng, 16, 2))
    # moss/dirt in the cracks
    crack = (stone < 0.25) & (fbm(rng, 40, 3) > 0.55)
    img = np.where(crack[..., None], np.array([0.22, 0.28, 0.13])[None, None, :] * 1.0, img)
    return relief(img, h, 0.6), h


def make_gravel(rng):
    v = fbm(rng, 70, 4)
    img = tint(None, v, (0.30, 0.29, 0.27), (0.46, 0.44, 0.41))
    h = 0.3 + 0.3 * fbm(rng, 20, 3)
    f1, f2, cid, k = voronoi(rng, 400, 1.0)
    pebble = np.clip((f2 - f1) / 4.0, 0, 1)
    shade = rng.uniform(0.8, 1.2, size=k)
    img = img * np.where(pebble > 0.2, shade[cid], 0.65)[..., None]
    h = np.maximum(h, 0.4 + 0.5 * np.sqrt(pebble))
    img, h = stamp_ellipses(rng, img, h, 120, (5, 10), (4, 8), [(0.48, 0.46, 0.43), (0.40, 0.39, 0.37), (0.55, 0.52, 0.48)], 1.0)
    return relief(img, h, 0.5), h


def make_sand(rng):
    v = fbm(rng, 90, 4)
    img = tint(None, v, (0.50, 0.44, 0.31), (0.64, 0.57, 0.41))
    ripple = 0.5 + 0.5 * np.sin((yy + (periodic_noise(rng, 80) - 0.5) * 60) * (2 * np.pi * 10 / N))
    img *= (0.94 + 0.08 * ripple)[..., None]
    img *= (0.92 + 0.16 * fbm(rng, 6, 2))[..., None]
    h = 0.2 + 0.2 * ripple + 0.1 * fbm(rng, 20, 2)
    img, h = stamp_ellipses(rng, img, h, 80, (1.5, 3), (1, 2.5), [(0.45, 0.42, 0.38), (0.7, 0.66, 0.58)], 0.7)
    return relief(img, h, 0.3), h


def make_water_normal(rng):
    h = fbm(rng, 40, 4)
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 6
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 6
    n = np.dstack([-gx, -gy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    rgb = n * 0.5 + 0.5
    Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(OUT, "water_normal.png"), optimize=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    makers = {"grass": make_grass, "forest": make_forest, "dry": make_dry, "dead": make_dead,
              "dirt": make_dirt, "cobble": make_cobble, "gravel": make_gravel, "sand": make_sand}
    only = set(sys.argv[1:])
    for i, name in enumerate(LAYERS):
        if only and name not in only:
            continue
        rng = np.random.default_rng(1000 + i)
        img, h = makers[name](rng)
        avg = save(name, img, h)
        print(f'{name:7s} new Color({avg[0]:.3f}f, {avg[1]:.3f}f, {avg[2]:.3f}f)')
    if not only or "water" in only:
        make_water_normal(np.random.default_rng(99))
        print("water_normal")


if __name__ == "__main__":
    main()

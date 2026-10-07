"""Texture processing for the Quaternius Stylized Nature MegaKit (CC0) and the KayKit Resource Bits (CC0).

The MegaKit's free edition ships its foliage as white masks (the paid edition tints them in a shader), and every
texture at 2048 px. Here the masks get baked colours (oak green, willow yellow-green, yew blue-green...) with a little
light/dark variation, everything is scaled down for the browser, and opaque textures become JPEGs.
"""
import io
import random
import zlib

from PIL import Image, ImageFilter

# Foliage colours (sRGB): the masks are multiplied by these.
TINTS = {
    "oak": (104, 142, 44),
    "willow": (158, 168, 62),
    "yew": (52, 98, 66),
    "pine": (60, 100, 46),
    "forest": (80, 112, 40),
    "bush": (86, 120, 48),
    "grass": (110, 134, 52),
}

# Texture names that are tintable foliage masks (Leaves.png is a multi-colour atlas: never tinted).
FOLIAGE = ("Leaves_NormalTree", "Leaves_TwistedTree", "Leaf_Pine", "Leaves_GiantPine", "Grass")


def _encode(img, opaque, quality=85):
    out = io.BytesIO()
    if opaque:
        img.convert("RGB").save(out, "JPEG", quality=quality, optimize=True)
        return out.getvalue(), "image/jpeg"
    img.save(out, "PNG", optimize=True)
    return out.getvalue(), "image/png"


def _shrink(img, size):
    if max(img.size) > size:
        img = img.resize((size, size) if img.size[0] == img.size[1] else
                         (size, int(img.size[1] * size / img.size[0])), Image.LANCZOS)
    return img


def _tint(img, rgb, seed):
    """Luminance of the mask times the colour, with soft blotches of light and shade so leaves aren't flat."""
    img = img.convert("RGBA")
    r, g, b, a = img.split()
    lum = Image.merge("RGB", (r, g, b)).convert("L")
    rnd = random.Random(seed)
    w, h = img.size
    noise = Image.new("L", (max(4, w // 32), max(4, h // 32)))
    noise.putdata([rnd.randint(185, 255) for _ in range(noise.size[0] * noise.size[1])])
    noise = noise.resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(radius=max(1, w // 64)))
    # Normalize: some models use the pre-coloured "_C" textures instead of the white masks, so stretch the leaf
    # brightness to full range first (the 90th percentile of the opaque pixels becomes white).
    hist = lum.histogram(mask=a.point(lambda v: 255 if v > 128 else 0))
    total, acc, p90 = sum(hist), 0, 255
    for i, c in enumerate(hist):
        acc += c
        if total and acc >= total * 0.9:
            p90 = max(1, i)
            break
    shade = lum.point(lambda v: min(255, int(v * 255 / p90)))
    px_l, px_n = shade.load(), noise.load()
    out = Image.new("RGB", (w, h))
    px_o = out.load()
    for y in range(h):
        for x in range(w):
            k = (px_l[x, y] / 255.0) ** 0.8 * (px_n[x, y] / 255.0)
            px_o[x, y] = (int(rgb[0] * k), int(rgb[1] * k), int(rgb[2] * k))
    out.putalpha(a)
    return out


def transform(tint=None, size=512, normal_size=256):
    """An image_transform for gltf_pack.repack."""
    def apply(name, raw, used_as):
        img = Image.open(io.BytesIO(raw))
        img.load()
        if used_as == "normal":
            return _encode(_shrink(img.convert("RGB"), normal_size), True, 88)
        img = _shrink(img, size)
        has_alpha = img.mode in ("RGBA", "LA") and img.getchannel("A").getextrema()[0] < 250
        if tint and name.startswith(FOLIAGE):
            return _encode(_tint(img, TINTS[tint], zlib.crc32(name.encode())), False)
        return _encode(img if has_alpha else img.convert("RGB"), not has_alpha)
    return apply

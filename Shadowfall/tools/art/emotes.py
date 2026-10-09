"""Keyframed emote animations for the KayKit hero rig (Wave, Dance, Bow, Point, Clap, Flex).

The KayKit Adventurers pack has no social animations, so these are authored here: each emote starts from the
pack's own Idle (so breathing and the weight shift stay), then poses bones by aiming them at directions in model
space (+Y up, the hero faces +Z, its left is +X) and rotating them about model axes. The result is baked at 30 fps
into ordinary glTF animations that the game plays like any other clip.

    python3 tools/art/emotes.py <Knight.glb> preview.png    # draws stick-figure frames of every emote

build_art.py calls add() for every hero model.
"""
import math
import struct
import sys

FPS = 30
# The bones the skin uses (the rig's IK helpers don't move vertices).
BONES = ["root", "hips", "spine", "chest", "upperarm.l", "lowerarm.l", "wrist.l", "hand.l", "handslot.l",
         "upperarm.r", "lowerarm.r", "wrist.r", "hand.r", "handslot.r", "head",
         "upperleg.l", "lowerleg.l", "foot.l", "toes.l", "upperleg.r", "lowerleg.r", "foot.r", "toes.r"]


# ------------------------------------------------------------------ quaternion helpers ([x, y, z, w], like glTF)

def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return [aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz]


def qconj(q):
    return [-q[0], -q[1], -q[2], q[3]]


def qnorm(q):
    n = math.sqrt(sum(c * c for c in q)) or 1.0
    return [c / n for c in q]


def qrot(q, v):
    p = qmul(qmul(q, [v[0], v[1], v[2], 0.0]), qconj(q))
    return p[:3]


def axis_angle(axis, deg):
    a = math.radians(deg) / 2
    n = math.sqrt(sum(c * c for c in axis)) or 1.0
    s = math.sin(a) / n
    return [axis[0] * s, axis[1] * s, axis[2] * s, math.cos(a)]


def from_to(u, v):
    """Shortest rotation taking direction u onto direction v."""
    u, v = norm(u), norm(v)
    d = sum(a * b for a, b in zip(u, v))
    if d < -0.9999:
        ax = cross(u, [1, 0, 0]) if abs(u[0]) < 0.9 else cross(u, [0, 1, 0])
        return axis_angle(ax, 180)
    c = cross(u, v)
    return qnorm([c[0], c[1], c[2], 1 + d])


def slerp(a, b, t):
    d = sum(x * y for x, y in zip(a, b))
    if d < 0:
        b, d = [-c for c in b], -d
    if d > 0.9995:
        return qnorm([x + (y - x) * t for x, y in zip(a, b)])
    th = math.acos(d)
    s = math.sin(th)
    wa, wb = math.sin((1 - t) * th) / s, math.sin(t * th) / s
    return [wa * x + wb * y for x, y in zip(a, b)]


def norm(v):
    n = math.sqrt(sum(c * c for c in v)) or 1.0
    return [c / n for c in v]


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


# ------------------------------------------------------------------ glTF access

def read_accessor(js, buffers, idx):
    acc = js["accessors"][idx]
    view = js["bufferViews"][acc["bufferView"]]
    n = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[acc["type"]]
    raw = buffers[view["buffer"]]
    off = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = view.get("byteStride", 4 * n)
    out = []
    for i in range(acc["count"]):
        vals = struct.unpack_from("<%df" % n, raw, off + i * stride)
        out.append(vals[0] if n == 1 else list(vals))
    return out


class Rig:
    """The skeleton's rest pose plus the Idle animation, sampled on demand."""

    def __init__(self, js, buffers):
        self.js = js
        self.nodes = js["nodes"]
        self.index = {n.get("name"): i for i, n in enumerate(self.nodes)}
        self.parent = {}
        for i, n in enumerate(self.nodes):
            for c in n.get("children", []):
                self.parent[c] = i
        idle = next(a for a in js["animations"] if a["name"] == "Idle")
        self.tracks = {}
        self.idle_len = 0.0
        for ch in idle["channels"]:
            s = idle["samplers"][ch["sampler"]]
            times = read_accessor(js, buffers, s["input"])
            vals = read_accessor(js, buffers, s["output"])
            self.tracks[(ch["target"]["node"], ch["target"]["path"])] = (times, vals, s.get("interpolation", "LINEAR"))
            self.idle_len = max(self.idle_len, times[-1])

    def idle_pose(self, t):
        """{node: [translation, rotation]} for every node at Idle time t (looped)."""
        t = t % self.idle_len if self.idle_len > 0 else 0
        pose = {}
        for i, n in enumerate(self.nodes):
            pose[i] = [list(n.get("translation", [0, 0, 0])), list(n.get("rotation", [0, 0, 0, 1]))]
        for (node, path), (times, vals, interp) in self.tracks.items():
            if path not in ("translation", "rotation"):
                continue
            k = 0
            while k + 1 < len(times) and times[k + 1] <= t:
                k += 1
            if k + 1 >= len(times) or interp == "STEP":
                v = vals[k]
            else:
                f = (t - times[k]) / max(1e-6, times[k + 1] - times[k])
                a, b = vals[k], vals[k + 1]
                v = slerp(a, b, f) if path == "rotation" else [x + (y - x) * f for x, y in zip(a, b)]
            pose[node][0 if path == "translation" else 1] = list(v)
        return pose

    def world(self, pose, node):
        """Model-space rotation and position of a node (through all its parents)."""
        chain = []
        i = node
        while i is not None:
            chain.append(i)
            i = self.parent.get(i)
        rot, pos = [0, 0, 0, 1], [0, 0, 0]
        for i in reversed(chain):
            t, r = pose[i] if i in pose else (self.nodes[i].get("translation", [0, 0, 0]), self.nodes[i].get("rotation", [0, 0, 0, 1]))
            s = self.nodes[i].get("scale", [1, 1, 1])
            local = [t[0], t[1], t[2]]
            pos = [p + d for p, d in zip(pos, qrot(rot, [local[0] * 1, local[1] * 1, local[2] * 1]))]
            rot = qmul(rot, r)
            _ = s
        return rot, pos


class Poser:
    """Edits one frame's pose in model space."""

    def __init__(self, rig, pose):
        self.rig, self.pose = rig, pose

    def _set_world(self, name, new_world):
        i = self.rig.index[name]
        parent_world, _ = self.rig.world(self.pose, self.rig.parent[i])
        self.pose[i][1] = qnorm(qmul(qconj(parent_world), new_world))

    def aim(self, name, direction, weight=1.0):
        """Turns a bone (which points along its local +Y) toward a model-space direction."""
        if weight <= 0:
            return
        i = self.rig.index[name]
        w, _ = self.rig.world(self.pose, i)
        cur = qrot(w, [0, 1, 0])
        delta = slerp([0, 0, 0, 1], from_to(cur, direction), min(1.0, weight))
        self._set_world(name, qmul(delta, w))

    def turn(self, name, axis, deg):
        """Rotates a bone about a model-space axis (through its own joint)."""
        if deg == 0:
            return
        i = self.rig.index[name]
        w, _ = self.rig.world(self.pose, i)
        self._set_world(name, qmul(axis_angle(axis, deg), w))

    def lift(self, name, dy):
        self.pose[self.rig.index[name]][0][1] += dy


# ------------------------------------------------------------------ the emotes
# Each takes (poser, t, length) and poses one frame. Model space: +X = hero's left, +Y = up, +Z = forward.

def env(t, length, fade=0.3):
    """0 -> 1 -> 0 over a one-shot emote, so it blends in and out of idle."""
    return smooth(t / fade) * smooth((length - t) / fade)


def wave(p, t, length):
    e = env(t, length, 0.35)
    swing = math.sin(t * math.tau * 1.4)
    p.aim("upperarm.r", [-0.55, 0.75, 0.25], e)
    p.aim("lowerarm.r", [-0.15 + 0.45 * swing, 1.0, 0.3], e)
    p.aim("hand.r", [-0.1 + 0.5 * swing, 1.0, 0.15], e)
    p.turn("chest", [0, 0, 1], 4 * e)          # lean into the wave a little
    p.turn("head", [0, 0, 1], -6 * e)
    p.turn("head", [0, 1, 0], -8 * e)


def dance(p, t, length):
    beat = t * math.tau / 0.8                  # one step every 0.4 s
    s = math.sin(beat)
    up = abs(math.sin(beat))
    e = smooth(t / 0.3)
    p.turn("hips", [0, 1, 0], 14 * s * e)
    p.turn("hips", [0, 0, 1], 5 * s * e)
    p.turn("spine", [0, 0, 1], -8 * s * e)
    p.turn("chest", [0, 1, 0], -10 * s * e)
    # step: lift the leg on the side we sway away from
    left = max(0.0, s) * e
    right = max(0.0, -s) * e
    p.turn("upperleg.l", [1, 0, 0], -28 * left)
    p.turn("lowerleg.l", [1, 0, 0], 45 * left)
    p.turn("upperleg.r", [1, 0, 0], -28 * right)
    p.turn("lowerleg.r", [1, 0, 0], 45 * right)
    # arms pump in turn, fists up
    p.aim("upperarm.l", [0.75, 0.25 + 0.7 * left, 0.35], e)
    p.aim("lowerarm.l", [0.2, 0.9, 0.5 - 0.3 * left], e)
    p.aim("upperarm.r", [-0.75, 0.25 + 0.7 * right, 0.35], e)
    p.aim("lowerarm.r", [-0.2, 0.9, 0.5 - 0.3 * right], e)
    p.turn("head", [0, 0, 1], 10 * s * e)
    p.turn("head", [1, 0, 0], -6 * up * e)


def bow(p, t, length):
    k = smooth(t / 0.7) * smooth((length - t) / 0.7)
    p.turn("spine", [1, 0, 0], 22 * k)
    p.turn("chest", [1, 0, 0], 18 * k)
    p.turn("head", [1, 0, 0], 15 * k)
    # right hand to the chest, left arm out behind
    p.aim("upperarm.r", [-0.35, -0.6, 0.6], k)
    p.aim("lowerarm.r", [0.9, 0.25, 0.3], k)
    p.aim("upperarm.l", [0.35, -0.85, -0.35], k)
    p.aim("lowerarm.l", [0.25, -0.8, -0.45], k)


def point(p, t, length):
    e = env(t, length, 0.3)
    jab = 0.06 * math.sin(min(1.0, t / 0.5) * math.pi)
    p.aim("upperarm.r", [-0.2, 0.25 + jab, 1.0], e)
    p.aim("lowerarm.r", [-0.1, 0.25, 1.0], e)
    p.aim("hand.r", [-0.05, 0.2, 1.0], e)
    p.turn("chest", [0, 1, 0], 12 * e)
    p.turn("head", [1, 0, 0], -4 * e)


def clap(p, t, length):
    e = env(t, length, 0.3)
    c = 0.5 + 0.5 * math.cos(t * math.tau * 2.2)   # 1 = hands apart, 0 = together
    p.aim("upperarm.l", [0.45, -0.35, 0.75], e)
    p.aim("upperarm.r", [-0.45, -0.35, 0.75], e)
    p.aim("lowerarm.l", [-0.75 + 0.75 * c, 0.35, 0.65], e)
    p.aim("lowerarm.r", [0.75 - 0.75 * c, 0.35, 0.65], e)
    p.turn("head", [1, 0, 0], 4 * (1 - c) * e)


def flex(p, t, length):
    k = smooth(t / 0.45) * smooth((length - t) / 0.45)
    pulse = 1 + 0.08 * math.sin(t * math.tau * 2.5)
    p.aim("upperarm.l", [1.0, 0.2 * pulse, 0.05], k)
    p.aim("upperarm.r", [-1.0, 0.2 * pulse, 0.05], k)
    p.aim("lowerarm.l", [0.15, 1.0, 0.1], k)
    p.aim("lowerarm.r", [-0.15, 1.0, 0.1], k)
    p.turn("chest", [1, 0, 0], -8 * k)
    p.turn("head", [1, 0, 0], -8 * k)


EMOTES = {
    # name: (function, seconds, loops)
    "Wave": (wave, 2.4, False),
    "Dance": (dance, 3.2, True),
    "Bow": (bow, 2.2, False),
    "Point": (point, 1.8, False),
    "Clap": (clap, 2.4, False),
    "Flex": (flex, 2.0, False),
}


def bake(rig, fn, length):
    frames = int(round(length * FPS)) + 1
    times = [i / FPS for i in range(frames)]
    poses = []
    for t in times:
        pose = rig.idle_pose(t)
        fn(Poser(rig, pose), t, length)
        poses.append(pose)
    return times, poses


def add(js, buffers):
    """Appends the emote animations to a loaded hero model (in place). Returns their names."""
    rig = Rig(js, buffers)
    blob = bytearray()
    js.setdefault("bufferViews", [])
    js.setdefault("accessors", [])
    buf_index = len(buffers)

    def accessor(values, kind):
        n = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[kind]
        flat = values if n == 1 else [c for v in values for c in v]
        raw = struct.pack("<%df" % len(flat), *flat)
        while len(blob) % 4:
            blob.append(0)
        js["bufferViews"].append({"buffer": buf_index, "byteOffset": len(blob), "byteLength": len(raw)})
        blob.extend(raw)
        acc = {"bufferView": len(js["bufferViews"]) - 1, "componentType": 5126, "count": len(values), "type": kind}
        if kind == "SCALAR":
            acc["min"], acc["max"] = [min(values)], [max(values)]
        js["accessors"].append(acc)
        return len(js["accessors"]) - 1

    names = []
    for name, (fn, length, _loops) in EMOTES.items():
        times, poses = bake(rig, fn, length)
        t_acc = accessor(times, "SCALAR")
        anim = {"name": name, "samplers": [], "channels": []}
        for bone in BONES:
            i = rig.index[bone]
            for path, slot, kind in (("rotation", 1, "VEC4"), ("translation", 0, "VEC3")):
                if path == "translation" and bone not in ("root", "hips"):
                    continue
                anim["samplers"].append({"input": t_acc, "output": accessor([p[i][slot] for p in poses], kind), "interpolation": "LINEAR"})
                anim["channels"].append({"sampler": len(anim["samplers"]) - 1, "target": {"node": i, "path": path}})
        js["animations"].append(anim)
        names.append(name)
    buffers.append(bytes(blob))
    js.setdefault("buffers", []).append({"byteLength": len(blob)})
    return names


# ------------------------------------------------------------------ preview

def preview(model_path, out_png):
    """Front and side stick figures of each emote at a few moments, to check the poses."""
    sys.path.insert(0, __import__("os").path.dirname(__file__))
    import gltf_pack
    from PIL import Image, ImageDraw
    js, buffers, _ = gltf_pack.load(model_path)
    rig = Rig(js, buffers)
    bones = [b for b in BONES if not b.startswith("handslot")]
    cols = 6
    cell = 150
    rows = len(EMOTES) * 2
    img = Image.new("RGB", (cell * cols, cell * rows), (24, 22, 28))
    d = ImageDraw.Draw(img)
    for r, (name, (fn, length, _)) in enumerate(EMOTES.items()):
        for c in range(cols):
            t = length * (c + 0.5) / cols
            pose = rig.idle_pose(t)
            fn(Poser(rig, pose), t, length)
            pos = {b: rig.world(pose, rig.index[b])[1] for b in bones}
            for view in range(2):   # 0 = front (x, y), 1 = side (z, y)
                ox, oy = c * cell + cell / 2, (r * 2 + view) * cell + cell - 12
                sc = cell * 0.62

                def P(b):
                    p = pos[b]
                    return (ox - p[0] * sc, oy - p[1] * sc) if view == 0 else (ox + p[2] * sc, oy - p[1] * sc)
                for b in bones:
                    par = rig.parent.get(rig.index[b])
                    if par is None or rig.nodes[par].get("name") not in pos:
                        continue
                    pb = rig.nodes[par]["name"]
                    color = (255, 120, 90) if b.endswith(".r") else (90, 170, 255) if b.endswith(".l") else (230, 230, 230)
                    d.line([P(pb), P(b)], fill=color, width=3)
                hx, hy = P("head")
                d.ellipse([hx - 9, hy - 18, hx + 9, hy], outline=(230, 230, 230), width=2)
                d.text((c * cell + 4, (r * 2 + view) * cell + 3), f"{name} {'front' if view == 0 else 'side'} {t:.1f}s", fill=(200, 190, 160))
    img.save(out_png)
    print("wrote", out_png)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    preview(sys.argv[1], sys.argv[2])

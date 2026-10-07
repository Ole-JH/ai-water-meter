"""Minimal glTF 2.0 repacker (no dependencies).

Loads a .gltf (with external or data-URI buffers/images) or .glb, optionally keeps only some
animations, drops everything no longer referenced, embeds all images and writes a single .glb.
Used by build_art.py to keep the game's model files small.
"""
import base64
import json
import os
import struct

GLB_MAGIC = 0x46546C67
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942


def _read_uri(uri, base_dir):
    if uri.startswith("data:"):
        return base64.b64decode(uri.split(",", 1)[1])
    with open(os.path.join(base_dir, uri.replace("%20", " ")), "rb") as f:
        return f.read()


def load(path):
    """Returns (json_dict, [buffer_bytes...])."""
    base_dir = os.path.dirname(path)
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] == b"glTF":
        _, _, length = struct.unpack_from("<III", data, 0)
        off, js, binchunk = 12, None, b""
        while off < length:
            clen, ctype = struct.unpack_from("<II", data, off)
            chunk = data[off + 8: off + 8 + clen]
            if ctype == CHUNK_JSON:
                js = json.loads(chunk.decode("utf-8"))
            elif ctype == CHUNK_BIN:
                binchunk = chunk
            off += 8 + clen
        buffers = []
        for b in js.get("buffers", []):
            buffers.append(_read_uri(b["uri"], base_dir) if "uri" in b else binchunk)
        return js, buffers, base_dir
    js = json.loads(data.decode("utf-8"))
    return js, [_read_uri(b["uri"], base_dir) for b in js.get("buffers", [])], base_dir


def repack(path, out_path, keep_animations=None, rename_animations=None, synthesize=None):
    """keep_animations: iterable of names to keep (None = keep all).
    synthesize: optional callable(js, buffers) that adds animations before filtering (see emotes.py).
    Returns dict of stats."""
    js, buffers, base_dir = load(path)
    if synthesize:
        synthesize(js, buffers)
    anims = js.get("animations", [])
    if keep_animations is not None:
        keep = list(keep_animations)
        by_name = {a.get("name"): a for a in anims}
        missing = [n for n in keep if n not in by_name]
        if missing:
            raise ValueError(f"{os.path.basename(path)}: animations not found: {missing}; available: {sorted(by_name)}")
        anims = [by_name[n] for n in keep]
    if rename_animations:
        for a in anims:
            a["name"] = rename_animations.get(a.get("name"), a.get("name"))
    js["animations"] = anims
    if not anims:
        js.pop("animations", None)

    accessors = js.get("accessors", [])
    used_acc = set()
    for m in js.get("meshes", []):
        for p in m.get("primitives", []):
            used_acc.update(p.get("attributes", {}).values())
            if "indices" in p:
                used_acc.add(p["indices"])
            for t in p.get("targets", []):
                used_acc.update(t.values())
    for s in js.get("skins", []):
        if "inverseBindMatrices" in s:
            used_acc.add(s["inverseBindMatrices"])
    for a in anims:
        for s in a["samplers"]:
            used_acc.add(s["input"])
            used_acc.add(s["output"])

    acc_map = {old: new for new, old in enumerate(sorted(used_acc))}
    views = js.get("bufferViews", [])
    used_views = set()
    for old in used_acc:
        acc = accessors[old]
        if "bufferView" in acc:
            used_views.add(acc["bufferView"])
        sp = acc.get("sparse")
        if sp:
            used_views.add(sp["indices"]["bufferView"])
            used_views.add(sp["values"]["bufferView"])
    for img in js.get("images", []):
        if "bufferView" in img:
            used_views.add(img["bufferView"])

    # Build the new binary blob: used buffer views first, then embedded images.
    blob = bytearray()
    new_views, view_map = [], {}

    def add_bytes(raw, extra):
        while len(blob) % 4:
            blob.append(0)
        view = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(raw)}
        view.update(extra)
        blob.extend(raw)
        new_views.append(view)
        return len(new_views) - 1

    for old in sorted(used_views):
        v = views[old]
        src = buffers[v["buffer"]]
        start = v.get("byteOffset", 0)
        raw = src[start: start + v["byteLength"]]
        extra = {k: v[k] for k in ("byteStride", "target") if k in v}
        view_map[old] = add_bytes(raw, extra)

    for img in js.get("images", []):
        if "bufferView" in img:
            img["bufferView"] = view_map[img["bufferView"]]
        elif "uri" in img:
            uri = img.pop("uri")
            raw = _read_uri(uri, base_dir)
            if "mimeType" not in img:
                img["mimeType"] = "image/jpeg" if uri.lower().endswith((".jpg", ".jpeg")) or raw[:3] == b"\xff\xd8\xff" else "image/png"
            img["bufferView"] = add_bytes(raw, {})

    new_accessors = []
    for old in sorted(used_acc):
        acc = dict(accessors[old])
        if "bufferView" in acc:
            acc["bufferView"] = view_map[acc["bufferView"]]
        if "sparse" in acc:
            sp = json.loads(json.dumps(acc["sparse"]))
            sp["indices"]["bufferView"] = view_map[sp["indices"]["bufferView"]]
            sp["values"]["bufferView"] = view_map[sp["values"]["bufferView"]]
            acc["sparse"] = sp
        new_accessors.append(acc)

    for m in js.get("meshes", []):
        for p in m.get("primitives", []):
            p["attributes"] = {k: acc_map[v] for k, v in p.get("attributes", {}).items()}
            if "indices" in p:
                p["indices"] = acc_map[p["indices"]]
            if "targets" in p:
                p["targets"] = [{k: acc_map[v] for k, v in t.items()} for t in p["targets"]]
    for s in js.get("skins", []):
        if "inverseBindMatrices" in s:
            s["inverseBindMatrices"] = acc_map[s["inverseBindMatrices"]]
    for a in anims:
        for s in a["samplers"]:
            s["input"] = acc_map[s["input"]]
            s["output"] = acc_map[s["output"]]

    js["accessors"] = new_accessors
    js["bufferViews"] = new_views
    while len(blob) % 4:
        blob.append(0)
    js["buffers"] = [{"byteLength": len(blob)}]

    json_bytes = json.dumps(js, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "
    total = 12 + 8 + len(json_bytes) + 8 + len(blob)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(struct.pack("<III", GLB_MAGIC, 2, total))
        f.write(struct.pack("<II", len(json_bytes), CHUNK_JSON))
        f.write(json_bytes)
        f.write(struct.pack("<II", len(blob), CHUNK_BIN))
        f.write(blob)
    return {"animations": [a.get("name") for a in anims], "bytes": total}

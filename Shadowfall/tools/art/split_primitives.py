#!/usr/bin/env python3
"""Splits every skinned mesh with several primitives into one mesh per primitive, in place.

glTFast (the importer) fails on skinned meshes with many primitives ("The previously scheduled job
SortAndNormalizeBoneWeightsJob writes to ... NativeArray<VBones>"): the model then imports as nothing and the game
shows a box. One primitive per mesh, each on its own node with the same skin (how the hero models are built), imports
fine. Only the JSON chunk changes; the binary data is untouched.

Usage: python3 tools/art/split_primitives.py Assets/Resources/Art/Mounts/*.glb
"""
import json
import struct
import sys

MAX_PRIMITIVES = 1


def split(path, quiet=False):
    data = open(path, "rb").read()
    magic, version, _ = struct.unpack("<4sII", data[:12])
    assert magic == b"glTF" and version == 2, path
    jlen, jtype = struct.unpack("<I4s", data[12:20])
    assert jtype == b"JSON"
    gltf = json.loads(data[20:20 + jlen])
    rest = data[20 + jlen:]  # the BIN chunk, as it is

    nodes, meshes = gltf["nodes"], gltf["meshes"]
    parents = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}
    changed = 0
    for ni in range(len(nodes)):
        node = nodes[ni]
        if "mesh" not in node or "skin" not in node:
            continue
        mesh = meshes[node["mesh"]]
        prims = mesh["primitives"]
        if len(prims) <= MAX_PRIMITIVES:
            continue
        base = mesh.get("name", "Mesh")
        mesh["primitives"] = prims[:1]
        for k, prim in enumerate(prims[1:], start=1):
            meshes.append({"name": f"{base}_{k}", "primitives": [prim]})
            new = {"name": f"{node.get('name', base)}_{k}", "mesh": len(meshes) - 1, "skin": node["skin"]}
            for key in ("translation", "rotation", "scale", "matrix"):
                if key in node:
                    new[key] = node[key]
            nodes.append(new)
            if ni in parents:
                nodes[parents[ni]]["children"].append(len(nodes) - 1)
            else:
                for scene in gltf.get("scenes", []):
                    if ni in scene.get("nodes", []):
                        scene["nodes"].append(len(nodes) - 1)
        changed += len(prims) - 1
    if not changed:
        if not quiet:
            print(f"{path}: nothing to split")
        return
    js = json.dumps(gltf, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    total = 12 + 8 + len(js) + len(rest)
    out = struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<I4s", len(js), b"JSON") + js + rest
    open(path, "wb").write(out)
    if not quiet:
        print(f"{path}: {changed} primitive(s) moved to meshes of their own")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        split(p)

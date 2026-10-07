#!/usr/bin/env python3
"""Builds Assets/Resources/Art/**.glb from the downloaded CC0 source packs.

    python3 tools/art/build_art.py <sources-dir> [Characters/ ...]   (optional: only these outputs)

<sources-dir> is created by tools/art/fetch_sources.sh. Every model is repacked into a single
.glb with embedded textures and only the animations the game uses (see gltf_pack.py).
The game loads these by path (without extension) via ArtLibrary.cs, so keep names stable.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import emotes  # noqa: E402
import gltf_pack  # noqa: E402
import nature  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art")

HERO = ["Idle", "Walking_A", "Running_A", "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal",
        "2H_Melee_Attack_Spin", "1H_Ranged_Shoot", "Spellcast_Shoot", "Spellcast_Raise", "Hit_A",
        "Death_A", "Death_A_Pose", "PickUp", "Interact", "Cheer",
        # emotes: the pack's own clips, plus the ones authored in emotes.py
        "Sit_Floor_Down", "Sit_Floor_Idle", "Lie_Down", "Lie_Idle", "Jump_Full_Short", "Unarmed_Melee_Attack_Kick",
        "Unarmed_Melee_Attack_Punch_A", "Blocking", *emotes.EMOTES]
SKELETON = ["Idle", "Idle_Combat", "Walking_A", "Running_A", "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal",
            "1H_Ranged_Shoot", "Spellcast_Shoot", "Spellcast_Summon", "Hit_A", "Death_A", "Death_A_Pose",
            "Skeletons_Awaken_Standing"]
BLOB = ["Idle", "Walk", "Bite_Front", "HitRecieve", "Death"]
BIG = ["Idle", "Walk", "Run", "Punch", "Weapon", "HitReact", "Death"]
WOLF = ["Idle", "Walk", "Gallop", "Attack", "Death", "Idle_HitReact1"]
MOUNT = ["Idle", "Walk", "Gallop", "Idle_HitReact1", "Eating"]
KENNEY_CHAR = ["idle", "walk", "sprint", "attack-melee-right", "die", "interact-right", "emote-yes"]

QN = "quaternius-nature"   # Quaternius Stylized Nature MegaKit (CC0), textures processed by nature.py
RB = "ResourceBits"        # KayKit Resource Bits (CC0)

# (output path under Art/, source file name, path hint, animations to keep or None = none/all static[, foliage tint])
MODELS = [
    # --- Trees: woodcutting tiers (oak, willow, yew) and forest filler
    *[(f"Trees/Oak_{i + 1}", f"CommonTree_{n}.gltf", QN, None, "oak") for i, n in enumerate([1, 2, 3])],
    *[(f"Trees/Willow_{i + 1}", f"TwistedTree_{n}.gltf", QN, None, "willow") for i, n in enumerate([1, 3])],
    *[(f"Trees/Yew_{i + 1}", f"Pine_{n}.gltf", QN, None, "yew") for i, n in enumerate([3, 1])],
    *[(f"Trees/Pine_{i + 1}", f"Pine_{n}.gltf", QN, None, "pine") for i, n in enumerate([2, 4, 5])],
    *[(f"Trees/Broadleaf_{i + 1}", f"CommonTree_{n}.gltf", QN, None, "forest") for i, n in enumerate([4, 5])],
    *[(f"Trees/Dead_{i + 1}", f"DeadTree_{n}.gltf", QN, None, None) for i, n in enumerate([1, 3, 4])],
    # --- Rocks, ore, plants
    *[(f"Rocks/Boulder_{n}", f"Rock_Medium_{n}.gltf", QN, None, None) for n in [1, 2, 3]],
    *[(f"Rocks/Pebble_{i + 1}", f"Pebble_Round_{n}.gltf", QN, None, None) for i, n in enumerate([1, 3, 5])],
    ("Ores/Copper", "Copper_Nuggets.gltf", RB, None),
    ("Ores/Iron", "Iron_Nuggets.gltf", RB, None),
    ("Ores/Mithril", "Silver_Nuggets.gltf", RB, None),
    ("Plants/Bush", "Bush_Common.gltf", QN, None, "bush"),
    ("Plants/Bush_Flowers", "Bush_Common_Flowers.gltf", QN, None, "bush"),
    ("Plants/Fern", "Fern_1.gltf", QN, None, None),
    ("Plants/Flowers_Yellow", "Flower_4_Group.gltf", QN, None, None),
    ("Plants/Flowers_Purple", "Plant_7_Big.gltf", QN, None, None),
    ("Plants/Plant", "Plant_1_Big.gltf", QN, None, None),
    ("Plants/Mushrooms", "Mushroom_Common.gltf", QN, None, None),
    # --- Characters (KayKit Adventurers / Skeletons, CC0)
    ("Characters/Knight", "Knight.glb", "adventures", HERO),
    ("Characters/Barbarian", "Barbarian.glb", "adventures", HERO),
    ("Characters/Mage", "Mage.glb", "adventures", HERO),
    ("Characters/Rogue", "Rogue.glb", "adventures", HERO),
    ("Characters/RogueHooded", "Rogue_Hooded.glb", "adventures", HERO),
    ("Characters/SkeletonWarrior", "Skeleton_Warrior.glb", "skeletons", SKELETON),
    ("Characters/SkeletonRogue", "Skeleton_Rogue.glb", "skeletons", SKELETON),
    ("Characters/SkeletonMage", "Skeleton_Mage.glb", "skeletons", SKELETON),
    ("Characters/Zombie", "character-zombie.glb", "graveyard", KENNEY_CHAR),
    ("Characters/Keeper", "character-keeper.glb", "graveyard", KENNEY_CHAR),
    # --- Monsters (Quaternius, CC0)
    ("Monsters/Wolf", "Wolf.gltf", "Animated Animals", WOLF),
    ("Monsters/Goblin", "Orc.gltf", "Blob", BLOB),
    ("Monsters/GoblinShaman", "Wizard.gltf", "Blob", BLOB),
    ("Monsters/Warchief", "Orc.gltf", "Big", BIG),
    ("Monsters/Golem", "Yeti.gltf", "Big", BIG),
    # --- Mounts (Quaternius Ultimate Animated Animals, CC0)
    ("Mounts/Horse", "Horse.gltf", "Animated Animals", MOUNT),
    ("Mounts/HorseWhite", "Horse_White.gltf", "Animated Animals", MOUNT),
    ("Mounts/Stag", "Stag.gltf", "Animated Animals", MOUNT),
    # --- Weapons (KayKit)
    ("Weapons/Sword", "sword_1handed.gltf", "adventures", None),
    ("Weapons/Axe", "axe_1handed.gltf", "adventures", None),
    ("Weapons/Dagger", "dagger.gltf", "adventures", None),
    ("Weapons/Greatsword", "sword_2handed.gltf", "adventures", None),
    ("Weapons/Staff", "staff.gltf", "adventures", None),
    ("Weapons/Shield", "shield_round.gltf", "adventures", None),
    ("Weapons/Crossbow", "crossbow_1handed.gltf", "adventures", None),
    ("Weapons/SkeletonBlade", "Skeleton_Blade.gltf", "skeletons", None),
    ("Weapons/SkeletonStaff", "Skeleton_Staff.gltf", "skeletons", None),
    ("Weapons/SkeletonCrossbow", "Skeleton_Crossbow.gltf", "skeletons", None),
    ("Weapons/SkeletonShield", "Skeleton_Shield_Small_A.gltf", "skeletons", None),
    # --- Nature (Kenney Nature Kit, CC0)
    *[(f"Nature/{n}", f"{n}.glb", "nature-kit", None) for n in [
        # (trees, rocks and plants now come from the Quaternius / KayKit packs above)
        "stump_roundDetailed", "stump_old", "grass", "grass_large", "grass_leafsLarge",
        "tent_detailedOpen", "tent_detailedClosed", "tent_smallClosed", "campfire_stones", "campfire_logs",
        "log_stack", "log_large", "fence_planks", "fence_simple"]],
    # --- Buildings (KayKit Medieval Hexagon, CC0)
    *[(f"Buildings/{n}", f"{n}.gltf", "hexagon", None) for n in [
        "building_home_A_blue", "building_home_B_blue", "building_home_A_green", "building_blacksmith_blue",
        "building_market_blue", "building_tavern_blue", "building_church_blue", "building_tower_A_blue",
        "building_well_blue", "building_windmill_blue"]],
    # --- Town props (Kenney Fantasy Town Kit, CC0)
    *[(f"Town/{n}", f"{n}.glb", "fantasy-town", None) for n in [
        "stall-red", "stall-green", "cart", "lantern", "banner-red", "banner-green", "fountain-round",
        "wall-wood", "fence", "hedge", "pillar-wood"]],
    # --- Props (KayKit Dungeon Remastered, CC0)
    *[(f"Props/{n.replace('.gltf', '')}", f"{n}.glb", "dungeon", None) for n in [
        "barrel_large.gltf", "barrel_small_stack.gltf", "crates_stacked.gltf", "box_stacked.gltf",
        "torch_lit.gltf", "torch_mounted.gltf", "banner_red.gltf", "chest"]],
    # --- Graveyard (Kenney Graveyard Kit, CC0)
    *[(f"Graveyard/{n}", f"{n}.glb", "graveyard", None) for n in [
        "gravestone-cross", "gravestone-round", "gravestone-bevel", "gravestone-broken", "gravestone-decorative",
        "grave", "crypt-large", "crypt", "iron-fence", "iron-fence-border", "lightpost-single", "trunk", "candle-multiple", "fire-basket", "altar-stone", "pillar-large", "coffin",
        "stone-wall", "stone-wall-column"]],
    # --- Seasonal decorations: Kenney Holiday Kit and Graveyard Kit (CC0)
    *[(f"Seasonal/{n}", f"{n}.glb", "holiday", None) for n in [
        "snowman", "snowman-hat", "tree-decorated-snow", "tree-snow-a", "snow-pile", "snow-bunker", "sled", "reindeer",
        "present-a-cube", "present-b-round", "present-a-rectangle", "lights-colored", "lantern-hanging", "wreath-decorated",
        "candy-cane-red", "gingerbread-man"]],
    *[(f"Seasonal/{n}", f"{n}.glb", "graveyard", None) for n in [
        "pumpkin", "pumpkin-carved", "pumpkin-tall-carved", "hay-bale", "hay-bale-bundled", "shovel", "lantern-candle"]],
]


def find_source(src_root, name, hint):
    best = None
    for dirpath, _, files in os.walk(src_root, followlinks=True):
        if name in files:
            path = os.path.join(dirpath, name)
            score = (hint.lower() in path.lower()) * 10 + ("glb" in dirpath.lower() or "gltf" in dirpath.lower())
            if best is None or score > best[0]:
                best = (score, path)
    if best is None or best[0] < 10:
        raise FileNotFoundError(f"{name} (hint '{hint}') not found under {src_root}")
    return best[1]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    src = os.path.abspath(sys.argv[1])
    only = sys.argv[2:]   # optional output-path prefixes, e.g. Characters/
    total = 0
    built = 0
    for entry in MODELS:
        out_rel, name, hint, anims = entry[:4]
        tint = entry[4] if len(entry) > 4 else None
        if only and not any(out_rel.startswith(o) for o in only):
            continue
        built += 1
        source = find_source(src, name, hint)
        out = os.path.join(OUT, out_rel + ".glb")
        processed = hint in (QN, RB)
        stats = gltf_pack.repack(source, out, anims if anims is not None else [],
                                 synthesize=emotes.add if anims is HERO else None,
                                 image_transform=nature.transform(tint) if processed else None)
        total += stats["bytes"]
        print(f"{out_rel:40s} {stats['bytes'] / 1024:8.0f} KB  {len(stats['animations'])} anims")
    print(f"\n{built} models, {total / 1024 / 1024:.1f} MB total -> {OUT}")


if __name__ == "__main__":
    main()

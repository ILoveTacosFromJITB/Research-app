"""Bring-your-own-files step 5: Kino's zombie characters from the user's own Black Ops (T5) dump.

Converts the honour-guard zombie bodies and heads named by character/c_ger_honorguard_zt.gsc (through its
xmodelalias scripts) to GLB, and the zombie AI animations to .bytes clips (same XAN2 format as the
viewmodel clips), and writes zombies_manifest.json.

Usage:
  python -I byo_zombies.py --dump F:/ZombiesBYO/dump --dump2 F:/ZombiesBYO/dump2 --out <UnityProject>/Assets/BYO_Kino
"""
import argparse
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # python -I drops the script folder
import byo_convert_kino as conv  # noqa: E402
import byo_weapons as weapons  # noqa: E402

# Role -> clips. Locomotion clips are played at a rate matched to the zombie's ground speed.
CLIPS = {
    "idle": ["ai_zombie_idle_v1", "ai_zombie_idle_base"],
    "walk": ["ai_zombie_walk_v1", "ai_zombie_walk_v2", "ai_zombie_walk_v3", "ai_zombie_walk_v4", "ai_zombie_walk_v6",
             "ai_zombie_walk_v7", "ai_zombie_walk_v8", "ai_zombie_walk_v9"],
    "run": ["ai_zombie_walk_fast_v1", "ai_zombie_walk_fast_v2", "ai_zombie_walk_fast_v3", "ai_zombie_run_v1", "ai_zombie_run_v2"],
    "sprint": ["ai_zombie_sprint_v1", "ai_zombie_sprint_v2", "ai_zombie_run_v3", "ai_zombie_run_v4"],
    "attack": ["ai_zombie_attack_v1", "ai_zombie_attack_v2", "ai_zombie_attack_v4", "ai_zombie_attack_v6",
               "ai_zombie_attack_forward_v1", "ai_zombie_attack_forward_v2"],
    "tear": [f"ai_zombie_boardtear_m_{i}" for i in range(1, 7)],
    "window_attack": ["ai_zombie_window_attack_arm_l_out", "ai_zombie_window_attack_arm_r_out"],
    "climb": ["ai_zombie_traverse_v1", "ai_zombie_traverse_v2", "ai_zombie_traverse_v5", "ai_zombie_traverse_v6"],
    "death": ["ai_zombie_death_v1", "ai_zombie_death_v2"],
}


def alias_models(path):
    return re.findall(r'a\[\d+\]\s*=\s*"([^"]+)"', open(path, encoding="latin-1").read()) if os.path.exists(path) else []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dump", required=True)
    ap.add_argument("--dump2", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--lod", type=int, default=1)
    args = ap.parse_args()
    zones = ["zombie_theater", "common_zombie"]

    xa = os.path.join(args.dump, "zombie_theater", "xmodelalias")
    bodies = sorted(set(alias_models(os.path.join(xa, "c_ger_honorguard_zomb_bodyalias.gsc"))))
    heads = sorted(set(alias_models(os.path.join(xa, "c_ger_honorguard_zomb_headalias.gsc"))))

    textures = conv.TextureBank([os.path.join(args.dump, z, "images") for z in zones], os.path.join(args.out, "Textures"), 1024)
    materials = conv.MaterialBank([os.path.join(args.dump, z, "materials") for z in zones])
    available, missing = conv.convert_props([{"model": m} for m in bodies + heads],
                                            [os.path.join(args.dump, z, "model_export") for z in zones],
                                            os.path.join(args.out, "Characters"), textures, args.lod, materials)

    anim_index = {}
    for z in zones:
        d = os.path.join(args.dump2, z, "xanim")
        for f in os.listdir(d):
            if f.endswith(".json"):
                anim_index.setdefault(f[:-5].lower(), os.path.join(d, f))
    anim_dir = os.path.join(args.out, "Anims")
    os.makedirs(anim_dir, exist_ok=True)
    clips = {}
    for role, names in CLIPS.items():
        clips[role] = []
        for n in names:
            src = anim_index.get(n.lower())
            if not src:
                continue
            dst = os.path.join(anim_dir, n.lower() + ".bytes")
            if not os.path.exists(dst):
                weapons.write_clip(src, dst)
            clips[role].append(n.lower())

    manifest = {"bodies": [conv.safe(b) for b in bodies if b in available], "heads": [conv.safe(h) for h in heads if h in available],
                "clips": clips}
    json.dump(manifest, open(os.path.join(args.out, "zombies_manifest.json"), "w"), indent=1)
    print(f"bodies {manifest['bodies']}, heads {manifest['heads']}, missing models {missing}, "
          f"clips {{{', '.join(f'{k}: {len(v)}' for k, v in clips.items())}}}")


if __name__ == "__main__":
    main()

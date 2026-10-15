"""Bring-your-own-files step 3: first-person weapons from the user's own Black Ops (T5) dump.

For each zombies weapon the port knows, reads its weapon file (models, animation names, timings, ADS FOV,
sound alias names), converts the gun and hands viewmodels to GLB (PNG textures) and the viewmodel
animations to compact .bytes clips, and writes a manifest Unity reads.

Usage:
  python -I byo_weapons.py --dump F:/ZombiesBYO/dump --dump2 F:/ZombiesBYO/dump2 --out <UnityProject>/Assets/BYO_Kino

Clip format (.bytes, little endian), read by ViewModelClip.cs:
  "XAN2" | float fps | int numFrames | byte looped | byte assetType | int boneCount
  per bone: byte nameLen, name | int rotCount, rotCount x (ushort frame, 4 float xyzw) |
            int posCount, posCount x (ushort frame, 3 float xyz)       (CoD space)
  assetType (XAnimParts.assetType): 1 = absolute (viewmodel/player clips: positions replace the bind position),
  2 = relative (AI clips: positions are offsets added to the bind position), 6 = additive.
  int notifyCount, notifyCount x (byte nameLen, name, float time 0..1)
"""
import argparse
import json
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # python -I drops the script folder
import byo_convert_kino as conv  # noqa: E402

WEAPONS = ["m1911_zm", "m14_zm", "rottweil72_zm", "mp5k_zm", "mp40_zm", "mpl_zm", "pm63_zm", "ak74u_zm", "m16_zm",
           "ithaca_zm", "aug_acog_zm", "commando_zm", "cz75_zm", "dragunov_zm", "famas_zm", "fnfal_zm", "g11_lps_zm",
           "galil_zm", "hk21_zm", "hs10_zm", "l96a1_zm", "python_zm", "ray_gun_zm", "rpk_zm", "spas_zm", "spectre_zm",
           "china_lake_zm", "m72_law_zm", "knife_zm"]
ANIM_KEYS = ["idleAnim", "emptyIdleAnim", "fireAnim", "lastShotAnim", "reloadAnim", "reloadEmptyAnim", "raiseAnim",
             "dropAnim", "firstRaiseAnim", "quickRaiseAnim", "quickDropAnim", "sprintInAnim", "sprintLoopAnim",
             "sprintOutAnim", "adsUpAnim", "adsDownAnim", "adsFireAnim", "adsLastShotAnim", "meleeAnim"]
NUM_KEYS = ["fireTime", "reloadTime", "reloadEmptyTime", "raiseTime", "dropTime", "firstRaiseTime", "quickRaiseTime",
            "adsTransInTime", "adsTransOutTime", "adsZoomFov1", "clipSize", "maxAmmo", "sprintLoopTime"]
SOUND_KEYS = ["fireSoundPlayer", "fireSound", "lastShotSoundPlayer", "emptyFireSoundPlayer", "emptyFireSound",
              "raiseSoundPlayer", "raiseSound", "firstRaiseSoundPlayer", "pickupSoundPlayer"]
HANDS = "viewmodel_usa_pow_arms"  # Dempsey's arms on Kino (zombie_theater.gsc precaches four sets)


def read_weapon(path):
    s = open(path, encoding="latin-1").read()
    parts = s.split("\\")[1:]
    return dict(zip(parts[0::2], parts[1::2]))


def write_clip(src_json, dst):
    a = json.load(open(src_json))
    out = bytearray(b"XAN2")
    out += struct.pack("<fiBBi", float(a["frameRate"]), int(a["numFrames"]), 1 if a["looped"] else 0,
                       int(a.get("assetType", 2)), len(a["bones"]))
    for b in a["bones"]:
        name = b["name"].encode("ascii", "replace")[:255]
        out += struct.pack("<B", len(name)) + name
        rot = b["rot"] or []
        out += struct.pack("<i", len(rot))
        for frame, q in rot:
            out += struct.pack("<H4f", int(frame), *q)
        pos = b["pos"] or []
        out += struct.pack("<i", len(pos))
        for k in pos:
            out += struct.pack("<H3f", int(k[0]), k[1], k[2], k[3])
    notes = a.get("notifies") or []
    out += struct.pack("<i", len(notes))
    for n in notes:
        name = n["name"].encode("ascii", "replace")[:255]
        out += struct.pack("<B", len(name)) + name + struct.pack("<f", float(n["time"]))
    with open(dst, "wb") as f:
        f.write(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dump", required=True)
    ap.add_argument("--dump2", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    zones = ["common_zombie", "zombie_theater"]

    anim_index = {}
    for z in zones:
        d = os.path.join(args.dump2, z, "xanim")
        for f in os.listdir(d):
            if f.endswith(".json"):
                anim_index.setdefault(f[:-5].lower(), os.path.join(d, f))

    textures = conv.TextureBank([os.path.join(args.dump, z, "images") for z in zones], os.path.join(args.out, "Textures"), 1024)
    vm_dir = os.path.join(args.out, "Viewmodels")
    anim_dir = os.path.join(args.out, "Anims")
    os.makedirs(anim_dir, exist_ok=True)

    manifest, models, missing_anims = [], {HANDS}, set()
    for wid in WEAPONS:
        path = next((os.path.join(args.dump, z, "weapons", wid) for z in zones if os.path.exists(os.path.join(args.dump, z, "weapons", wid))), None)
        if not path:
            print("no weapon file for", wid)
            continue
        w = read_weapon(path)
        entry = {"id": wid, "gunModel": w.get("gunModel", ""), "handModel": HANDS, "anims": {}, "numbers": {}, "sounds": {},
                 "hideTags": [t.strip() for t in w.get("hideTags", "").splitlines() if t.strip()]}
        models.add(entry["gunModel"])
        for k in ANIM_KEYS:
            name = w.get(k, "")
            if not name:
                continue
            src = anim_index.get(name.lower())
            if src:
                dst_name = name.lower()
                dst = os.path.join(anim_dir, dst_name + ".bytes")
                if not os.path.exists(dst):
                    write_clip(src, dst)
                entry["anims"][k] = dst_name
            else:
                missing_anims.add(name)
        for k in NUM_KEYS:
            try:
                entry["numbers"][k] = float(w.get(k, "0") or 0)
            except ValueError:
                pass
        for k in SOUND_KEYS:
            if w.get(k):
                entry["sounds"][k] = w[k]
        manifest.append(entry)

    textures.png_for("damage_feedback")  # hit marker
    available, missing_models = conv.convert_props([{"model": m} for m in sorted(models) if m], [os.path.join(args.dump, z, "model_export") for z in zones],
                                                   vm_dir, textures, 0,
                                                   conv.MaterialBank([os.path.join(args.dump, z, "materials") for z in zones]))
    for e in manifest:
        e["gunFile"] = conv.safe(e["gunModel"]) if e["gunModel"] in available else ""
        e["handFile"] = conv.safe(HANDS) if HANDS in available else ""
    json.dump({"weapons": manifest}, open(os.path.join(args.out, "weapons_manifest.json"), "w"), indent=1)
    print(f"weapons {len(manifest)}, viewmodels {len(available)} (missing {missing_models}), "
          f"clips {len(os.listdir(anim_dir))}, missing anims {len(missing_anims)}: {sorted(missing_anims)[:12]}")


if __name__ == "__main__":
    main()

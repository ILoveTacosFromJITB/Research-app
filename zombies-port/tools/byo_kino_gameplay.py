"""Bring-your-own-files step 2: Kino der Toten map entities + zone script -> Unity gameplay layout.

Reads the user's own dumped files (map .ents, zombie_theater.gsc, GfxWorld brush-model bounds) and writes
<out>/kino_gameplay.json with everything already in Unity space, plus GLBs for the script_models
(perk machines, Mystery Box, wall guns, power lever) into <out>/Props.

Usage:
  python -I byo_kino_gameplay.py --dump F:/ZombiesBYO/dump --dump2 F:/ZombiesBYO/dump2 --out <UnityProject>/Assets/BYO_Kino
"""
import argparse
import json
import math
import os
import re
from collections import defaultdict

import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # python -I drops the script folder
import byo_convert_kino as conv  # noqa: E402

PERKS = {
    "specialty_quickrevive": "QuickRevive",
    "specialty_armorvest": "Juggernog",
    "specialty_fastreload": "SpeedCola",
    "specialty_rof": "DoubleTap",
}


def parse_ents(path):
    ents, cur = [], None
    for line in open(path, encoding="latin-1"):
        line = line.strip()
        if line == "{":
            cur = {}
        elif line == "}":
            ents.append(cur)
            cur = None
        elif cur is not None:
            m = re.match(r'"([^"]*)" "([^"]*)"', line)
            if m:
                cur[m.group(1)] = m.group(2)
    return ents


def vec(s):
    return np.array([float(x) for x in s.split()], dtype=np.float64)


def u(p):
    """CoD point (inches, Z-up) -> Unity point (metres)."""
    return [round(-p[0] * conv.INCH, 4), round(p[2] * conv.INCH, 4), round(-p[1] * conv.INCH, 4)]


def u_bounds(mins, maxs):
    a, b = np.array(u(mins)), np.array(u(maxs))
    lo, hi = np.minimum(a, b), np.maximum(a, b)
    return {"center": ((lo + hi) / 2).round(4).tolist(), "size": (hi - lo).round(4).tolist()}


def unity_yaw(cod_yaw):
    """CoD yaw (deg, about +Z, 0 = +X) -> Unity Y rotation for an object whose forward (+Z) faces that way."""
    r = math.radians(cod_yaw)
    fx, fz = -math.cos(r), -math.sin(r)  # Unity direction of CoD (cos, sin, 0)
    return round(math.degrees(math.atan2(fx, fz)), 3)


def angles_to_axis(angles):
    """CoD AnglesToAxis: rows forward, left, up."""
    pitch, yaw, roll = [math.radians(a) for a in angles]
    sp, cp = math.sin(pitch), math.cos(pitch)
    sy, cy = math.sin(yaw), math.cos(yaw)
    sr, cr = math.sin(roll), math.cos(roll)
    fwd = [cp * cy, cp * sy, -sp]
    right = [-sr * sp * cy + cr * sy, -sr * sp * sy - cr * cy, -sr * cp]
    up = [cr * sp * cy + sr * sy, cr * sp * sy - sr * cy, cr * cp]
    return [fwd, [-x for x in right], up]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dump", required=True)
    ap.add_argument("--dump2", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    ents = parse_ents(os.path.join(args.dump, "zombie_theater", "maps", "zombie_theater.d3dbsp.ents"))
    world = json.load(open(os.path.join(args.dump2, "zombie_theater", "maps", "zombie_theater_smodels.json")))
    bm = {b["index"]: b for b in world["brushmodels"]}
    gsc = open(os.path.join(args.dump, "zombie_theater", "maps", "zombie_theater.gsc"), encoding="latin-1").read()

    by_name = defaultdict(list)
    for e in ents:
        by_name[e.get("targetname", "")].append(e)

    def model_index(e):
        m = e.get("model", "")
        return int(m[1:]) if m.startswith("*") else None

    def trigger(e):
        i = model_index(e)
        b = bm.get(i) if i is not None else None
        if b and b["surfCount"] == 0 and b["mins"][0] < b["maxs"][0]:
            # Brush-model bounds are local to the entity origin.
            o = vec(e["origin"])
            box = u_bounds(o + np.array(b["mins"]), o + np.array(b["maxs"]))
        else:
            box = {"center": u(vec(e["origin"])), "size": [2.0, 2.0, 2.0]}
        box["size"] = [max(s, 1.6) for s in box["size"]]  # generous on touch screens
        return box

    # ---- script_models: real props for gameplay objects ----
    script_models = []
    for e in ents:
        if e.get("classname") == "script_model" and e.get("model") and not e["model"].startswith("*"):
            entry = {"model": e["model"], "origin": vec(e["origin"]).tolist(),
                     "axis": angles_to_axis(vec(e.get("angles", "0 0 0"))), "scale": float(e.get("modelscale", 1))}
            script_models.append((e, entry))

    textures = conv.TextureBank([os.path.join(args.dump, z, "images") for z in ("zombie_theater", "common_zombie")],
                                os.path.join(args.out, "Textures"), 1024)
    available, missing = conv.convert_props([s for _, s in script_models],
                                            [os.path.join(args.dump, z, "model_export") for z in ("zombie_theater", "common_zombie")],
                                            os.path.join(args.out, "Props"), textures, 1,
                                            conv.MaterialBank([os.path.join(args.dump, z, "materials") for z in ("zombie_theater", "common_zombie")]))
    ent_models = []
    for e, s in script_models:
        if s["model"] in available:
            p = conv.placement_to_unity(s)
            p["file"] = conv.safe(s["model"].lstrip(","))
            p["targetname"] = e.get("targetname", "")
            ent_models.append(p)

    # Brush models with render geometry are modelled around their entity origin: place each world node there.
    brush_placements = []
    for e in ents:
        i = model_index(e)
        if i is not None and bm.get(i, {}).get("surfCount") and "origin" in e:
            p = conv.placement_to_unity({"model": "", "origin": vec(e["origin"]).tolist(),
                                         "axis": angles_to_axis(vec(e.get("angles", "0 0 0"))), "scale": 1.0 / conv.INCH})
            brush_placements.append({"brushmodel": i, "pos": p["pos"], "rot": p["rot"], "targetname": e.get("targetname", "")})

    def piece(e):
        i = model_index(e)
        return {"brushmodel": i, "pivot": u(vec(e["origin"])), "hasGeometry": bool(bm.get(i, {}).get("surfCount")),
                "rotateY": -vec(e["script_angles"])[1] if "script_angles" in e else 0.0,
                "moveBy": u(vec(e["script_vector"])) if "script_vector" in e else [0, 0, 0],
                "clip": e.get("script_noteworthy") == "clip", "bounds": trigger(e) if i is not None else None}

    # ---- doors ----
    doors = []
    for d in by_name["zombie_door"] + by_name["zombie_debris"]:
        doors.append({"trigger": trigger(d), "cost": int(d.get("zombie_cost", 0)), "flag": d.get("script_flag", ""),
                      "electric": d.get("script_noteworthy") == "electric_door", "target": d.get("target", ""),
                      "debris": d.get("targetname") == "zombie_debris",
                      "pieces": [piece(t) for t in by_name[d.get("target", "")] if t.get("classname") == "script_brushmodel"]})

    # ---- windows ----
    windows = []
    for i, g in enumerate(by_name["exterior_goal"]):
        parts = by_name.get(g.get("target", ""), [])
        boards = sorted([t for t in parts if t.get("script_noteworthy", "").isdigit()], key=lambda t: int(t["script_noteworthy"]))
        clip = next((t for t in parts if t.get("script_noteworthy") == "clip"), None)
        if not boards:
            continue
        centre = np.mean([vec(b["origin"]) for b in boards], axis=0)
        windows.append({"index": len(windows), "goal": u(vec(g["origin"])), "centre": u(centre),
                        "yaw": unity_yaw(vec(g.get("angles", "0 0 0"))[1]), "codGoal": vec(g["origin"]).tolist(),
                        "boards": [piece(b) for b in boards], "clip": trigger(clip) if clip else None})

    # ---- spawners ----
    spawners = []
    for e in ents:
        cls = e.get("classname", "")
        if not cls.startswith("actor_zombie") or cls == "actor_zombie_dog":
            continue
        if cls == "actor_zombie_quad":
            continue  # no quad (crawler) AI yet; they only appear after the curtains in Kino anyway
        tn = e.get("targetname", "")
        zone = tn[len("zombie_spawner_"):] + "_zone" if tn.startswith("zombie_spawner_") else tn.replace("_spawners", "")
        o = vec(e["origin"])
        win = None
        if e.get("script_string") not in ("riser", "zombie_chaser"):
            best = min(windows, key=lambda w: np.linalg.norm(np.array(w["codGoal"][:2]) - o[:2]), default=None)
            if best is not None and np.linalg.norm(np.array(best["codGoal"][:2]) - o[:2]) < 900:
                win = best["index"]
        spawners.append({"pos": u(o), "zone": zone, "window": win, "kind": e.get("script_string") or "window"})

    dog_spots = []
    for tn, items in by_name.items():
        if tn.startswith("zombie_spawner_") and tn.endswith("_dog"):
            for e in items:
                if "origin" in e:
                    dog_spots.append({"pos": u(vec(e["origin"])), "zone": tn[len("zombie_spawner_"):-len("_dog")] + "_zone"})
    for e in by_name.get("foyer_zone_spawners_dog", []):
        dog_spots.append({"pos": u(vec(e["origin"])), "zone": "foyer_zone"})

    # ---- zones ----
    zones = defaultdict(list)
    for e in ents:
        if e.get("classname") == "info_volume" and e.get("script_noteworthy") == "player_volume":
            i = model_index(e)
            b = bm.get(i)
            if b:
                o = vec(e["origin"])
                zones[e["targetname"]].append(u_bounds(o + np.array(b["mins"]), o + np.array(b["maxs"])))
    adjacency = [{"a": a, "b": b, "flag": f} for a, b, f in re.findall(r'add_adjacent_zone\(\s*"(\w+)",\s*"(\w+)",\s*"(\w+)"\s*\)', gsc)]
    initial = re.findall(r'init_zones\[\d+\]\s*=\s*"(\w+)"', gsc)

    # ---- wall buys, perks, box, power ----
    def targets(e):
        return [m for m in ent_models if m["targetname"] and m["targetname"] == e.get("target")]

    wall_buys = [{"trigger": trigger(e), "weapon": e["zombie_weapon_upgrade"], "origin": u(vec(e["origin"])),
                  "models": [m["file"] for m in targets(e)], "target": e.get("target", "")}
                 for e in by_name["weapon_upgrade"]]
    perks = [{"trigger": trigger(e), "perk": PERKS.get(e.get("script_noteworthy"), ""), "target": e.get("target", ""),
              "origin": u(vec(e["origin"]))} for e in by_name["zombie_vending"]]
    boxes = []
    for e in by_name["treasure_chest_use"]:
        name = e.get("script_noteworthy", "")
        org = next(iter(by_name.get(name + "_org", [])), None)
        boxes.append({"name": name, "trigger": trigger(e), "cost": int(e.get("zombie_cost", 950)),
                      "excludeAtStart": e.get("start_exclude") == "1",
                      "lid": name + "_lid", "box": name + "_box",
                      "origin": u(vec(org["origin"])) if org else u(vec(e["origin"])),
                      "yaw": unity_yaw(vec(org.get("angles", "0 0 0"))[1]) if org else 0.0})
    power = [{"trigger": trigger(e), "lever": "elec_switch"} for e in by_name["use_elec_switch"]]

    # ---- lighting: the map's placed lights (spot lights) and worldspawn sun / ambient ----
    def cod_dir_to_unity(pitch, yaw):
        f = angles_to_axis([pitch, yaw, 0])[0]
        return [round(-f[0], 4), round(f[2], 4), round(-f[1], 4)]
    lights = []
    for e in ents:
        if e.get("classname") != "light":
            continue
        color = e.get("_color", "0 0 0")
        if sum(float(x) for x in color.split()) < 0.01:
            color = e.get("script_light2_color", "1 1 1")  # the "on" colour the mixer switches to
        ang = vec(e.get("angles", e.get("angle", "90 0 0")) if " " in e.get("angles", e.get("angle", "90 0 0")) else "0 0 0")
        direction = cod_dir_to_unity(ang[0], ang[1])
        tgt = next(iter(by_name.get(e.get("target", ""), [])), None)
        if tgt is not None and "origin" in tgt:  # CoD spot lights aim at their target entity
            dvec = vec(tgt["origin"]) - vec(e["origin"])
            if np.linalg.norm(dvec) > 1e-3:
                dvec /= np.linalg.norm(dvec)
                direction = [round(-dvec[0], 4), round(dvec[2], 4), round(-dvec[1], 4)]
        lights.append({"pos": u(vec(e["origin"])), "dir": direction,
                       "color": [float(x) for x in color.split()], "intensity": float(e.get("intensity", 1)),
                       "radius": float(e.get("radius", 300)) * conv.INCH, "fovOuter": float(e.get("fov_outer", 90)),
                       "fovInner": float(e.get("fov_inner", 60)), "targetname": e.get("targetname", "")})
    # ComWorld primary lights (patched OAT): the compiled lights with real direction, cone and HDR colour.
    primary = []
    pl_path = os.path.join(args.dump2, "zombie_theater", "maps", "zombie_theater_primary_lights.json")
    if os.path.exists(pl_path):
        for l in json.load(open(pl_path))["lights"]:
            if l["type"] in (2, 3) and max(l["color"]) > 0.01:
                d = l["dir"]
                primary.append({"index": l["index"], "type": "spot" if l["type"] == 2 else "point", "pos": u(l["origin"]),
                                "dir": [round(-d[0], 4), round(d[2], 4), round(-d[1], 4)],  # CoD dir points back at the light
                                "color": l["color"], "range": round(l["radius"] * conv.INCH, 3),
                                "cosOuter": l["cosOuter"], "cosInner": l["cosInner"]})
    ws = next(e for e in ents if e.get("classname") == "worldspawn")
    sd = vec(ws.get("sundirection", "-45 0 0"))
    environment = {"sunColor": [float(x) for x in ws.get("suncolor", "1 1 1").split()],
                   "sunIntensity": float(ws.get("sunlight", 1)), "sunDir": cod_dir_to_unity(sd[0], sd[1]),
                   "ambientColor": [float(x) for x in ws.get("_ambientcolor", "0.5 0.5 0.5").split()],
                   "ambientIntensity": float(ws.get("ambientintensity", 1)), "exposure": float(ws.get("exposure", 1))}

    start = next(e for e in ents if e.get("classname") == "info_player_start")
    layout = {
        "player": {"pos": u(vec(start["origin"])), "yaw": unity_yaw(vec(start["angles"])[1])},
        "initialZones": initial, "adjacency": adjacency,
        "zones": [{"name": k, "boxes": v} for k, v in zones.items()],
        "doors": doors, "windows": windows, "spawners": spawners, "dogSpots": dog_spots,
        "brushPlacements": brush_placements, "lights": lights, "primaryLights": primary, "environment": environment, "wallBuys": wall_buys, "perks": perks, "boxes": boxes, "power": power, "entModels": ent_models,
    }
    json.dump(layout, open(os.path.join(args.out, "kino_gameplay.json"), "w"), indent=1)
    print(f"brush placements {len(brush_placements)}, doors {len(doors)}, windows {len(windows)}, spawners {len(spawners)} "
          f"({sum(1 for s in spawners if s['window'] is not None)} via windows), dog spots {len(dog_spots)}, "
          f"zones {len(zones)}, adjacency {len(adjacency)}, wall buys {len(wall_buys)}, perks {len(perks)}, "
          f"boxes {len(boxes)}, power {len(power)}, ent models {len(ent_models)} (missing {missing})")


if __name__ == "__main__":
    main()

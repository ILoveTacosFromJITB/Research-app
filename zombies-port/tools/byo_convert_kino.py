"""Bring-your-own-files converter: Black Ops (T5) Kino der Toten dump -> Unity-ready assets.

Reads ONLY files dumped from the user's own game install (OAT Unlinker output) and writes
derived assets into a Unity project folder. Nothing from the game is stored in this repo.

Usage:
  python -I byo_convert_kino.py --dump F:/ZombiesBYO/dump --dump2 F:/ZombiesBYO/dump2 --out <UnityProject>/Assets/BYO_Kino

Output (inside --out):
  Textures/*.png            colour maps, max 1024 px
  World/kino_world.glb      map geometry, chunked, metres, glTF Y-up
  Lighting/lightmap*.png    the map's baked lighting (BO1 secondary lightmaps), RGBM, range 8
  Lighting/lightgrid.bytes  BO1's light grid (what lights models), mean colour per sample, for entity models
  Props/*.glb               static models re-pointed at the PNGs (normal maps dropped for mobile)
  kino_layout.json          prop placements already in Unity space (glTFast flips X), with per-prop sun and baked light

Coordinate notes (see MODLOG): CoD is right-handed Z-up inches. glTF = (x, z, -y) * 0.0254.
glTFast imports glTF by negating X.
"""
import argparse
import glob
import json
import os
import re
import struct
from collections import defaultdict

import numpy as np
from PIL import Image

INCH = 0.0254
SKIP_MATERIALS = ("sky", "caulk", "clip", "portal", "shadow")
RUNTIME_ONLY_MATERIALS = ("clan_tag", "player_icon", "emblem")
CHUNK = 1024.0  # inches
C = np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=np.float64)  # CoD -> glTF basis
F = np.diag([-1.0, 1.0, 1.0])  # glTF -> Unity (glTFast)


def safe(name):
    return re.sub(r"[^A-Za-z0-9_.-]", "_", name)


class TextureBank:
    """Image name -> PNG. Looks in the dumped DDS folders first, then (BO1_MAIN set) in the game's own IWD
    archives, converting .iwi with OAT's ImageConverter (OAT_IMAGECONVERTER) into IWI_CACHE."""

    def __init__(self, image_dirs, out_dir, max_size):
        self.iwd_main = os.environ.get("BO1_MAIN")
        self.converter = os.environ.get("OAT_IMAGECONVERTER")
        self.cache = os.environ.get("IWI_CACHE", os.path.join(os.path.dirname(out_dir), "_iwi_cache"))
        self._iwd_index = None
        self.index = {}
        for d in image_dirs:
            if os.path.isdir(d):
                for f in os.listdir(d):
                    if f.lower().endswith(".dds"):
                        self.index.setdefault(f[:-4].lower(), os.path.join(d, f))
        self.out_dir = out_dir
        self.max_size = max_size
        self.done = {}
        os.makedirs(out_dir, exist_ok=True)

    def png_for(self, image_name):
        """Convert image to PNG once; return file name or None."""
        key = image_name.lower().lstrip(",")
        if key in self.done:
            return self.done[key]
        src = self.index.get(key) or self._from_iwd(key)
        result = None
        if src:
            out_name = safe(key) + ".png"
            out_path = os.path.join(self.out_dir, out_name)
            if not os.path.exists(out_path):
                try:
                    img = Image.open(src).convert("RGBA")
                    if max(img.size) > self.max_size:
                        img.thumbnail((self.max_size, self.max_size), Image.LANCZOS)
                    img.save(out_path)
                except Exception as e:  # noqa: BLE001 - skip unreadable images
                    print("  texture failed", key, e)
                    out_path = None
            result = out_name if out_path else None
        self.done[key] = result
        return result

    def _from_iwd(self, key):
        if not (self.iwd_main and self.converter):
            return None
        if self._iwd_index is None:
            import zipfile
            self._iwd_index = {}
            for z in sorted(glob.glob(os.path.join(self.iwd_main, "*.iwd"))):
                for n in zipfile.ZipFile(z).namelist():
                    if n.lower().endswith(".iwi"):
                        self._iwd_index.setdefault(os.path.splitext(os.path.basename(n))[0].lower(), (z, n))
        hit = self._iwd_index.get(key)
        if not hit:
            return None
        import subprocess
        import zipfile
        os.makedirs(self.cache, exist_ok=True)
        iwi = os.path.join(self.cache, safe(key) + ".iwi")
        dds = iwi[:-4] + ".dds"
        if not os.path.exists(dds):
            with open(iwi, "wb") as f:
                f.write(zipfile.ZipFile(hit[0]).read(hit[1]))
            subprocess.run([self.converter, "--t5", iwi], capture_output=True)
        return dds if os.path.exists(dds) else None


class MaterialBank:
    """Material name -> colour map image name, from OAT material JSON."""

    def __init__(self, material_dirs):
        self.json_paths = {}
        for d in material_dirs:
            for root, _, files in os.walk(d):
                for f in files:
                    if f.endswith(".json"):
                        rel = os.path.relpath(os.path.join(root, f), d).replace("\\", "/")[:-5]
                        self.json_paths.setdefault(rel.lower(), os.path.join(root, f))

    def color_image(self, mat_name):
        return self.describe(mat_name)[0]

    def describe(self, mat_name):
        """Return (colour image name, glTF alphaMode) for a material."""
        name = mat_name.lstrip(",")  # leading comma = reference to an asset in another zone
        m = re.match(r"^\*[^(]*\(([^:)]+)", name)  # layered "*24n_21(wc/a:wc/b)" -> first (opaque base) layer
        layered = bool(m)
        if m:
            name = m.group(1)
        image, alpha = None, "OPAQUE"
        path = self.json_paths.get(name.lower())
        if path:
            try:
                j = json.load(open(path, encoding="utf-8"))
                for t in j.get("textures", []):
                    if t.get("semantic") == "colorMap" or t.get("name") == "colorMap":
                        image = t["image"]
                bits = j.get("stateBits") or []
                if bits and not layered:
                    b = bits[0]
                    if b.get("srcBlendRgb") not in (None, "one") or b.get("dstBlendRgb") not in (None, "zero"):
                        alpha = "BLEND"
                    elif b.get("alphaTest") not in (None, "disabled", "always"):
                        alpha = "MASK"
            except Exception:  # noqa: BLE001
                pass
        if image is None:
            image = "~-g" + name.split("/")[-1] + "_c"  # common naming fallback; checked against the image index
        if not layered and alpha == "OPAQUE" and re.search(r"(^|[/_])dec(al)?_|_blend$", name):
            alpha = "BLEND"
        return image, alpha


# ---------------------------------------------------------------- GLB writing
class GlbBuilder:
    def __init__(self):
        self.bin = bytearray()
        self.gltf = {"asset": {"version": "2.0", "generator": "byo_convert_kino"}, "scenes": [{"nodes": []}], "scene": 0,
                     "nodes": [], "meshes": [], "accessors": [], "bufferViews": [], "materials": [], "textures": [],
                     "images": [], "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]}
        self.material_index = {}

    def _view(self, data, target):
        while len(self.bin) % 4:
            self.bin.append(0)
        off = len(self.bin)
        self.bin += data
        self.gltf["bufferViews"].append({"buffer": 0, "byteOffset": off, "byteLength": len(data), "target": target})
        return len(self.gltf["bufferViews"]) - 1

    def accessor(self, arr, gl_type, typ, target, minmax=False):
        view = self._view(arr.tobytes(), target)
        acc = {"bufferView": view, "componentType": gl_type, "count": int(arr.shape[0]), "type": typ}
        if minmax:
            acc["min"] = arr.min(axis=0).tolist()
            acc["max"] = arr.max(axis=0).tolist()
        self.gltf["accessors"].append(acc)
        return len(self.gltf["accessors"]) - 1

    def material(self, name, png_uri, double_sided, alpha_mode="OPAQUE"):
        if name in self.material_index:
            return self.material_index[name]
        mat = {"name": name, "pbrMetallicRoughness": {"metallicFactor": 0.0, "roughnessFactor": 0.85}, "doubleSided": double_sided}
        if alpha_mode != "OPAQUE":
            mat["alphaMode"] = alpha_mode
            if alpha_mode == "MASK":
                mat["alphaCutoff"] = 0.5
        if png_uri:
            self.gltf["images"].append({"uri": png_uri})
            self.gltf["textures"].append({"source": len(self.gltf["images"]) - 1, "sampler": 0})
            mat["pbrMetallicRoughness"]["baseColorTexture"] = {"index": len(self.gltf["textures"]) - 1}
        else:
            mat["pbrMetallicRoughness"]["baseColorFactor"] = [0.5, 0.5, 0.5, 1.0]
        self.gltf["materials"].append(mat)
        self.material_index[name] = len(self.gltf["materials"]) - 1
        return self.material_index[name]

    def add_mesh_node(self, node_name, primitives):
        prims = []
        for p in primitives:
            a = {"POSITION": self.accessor(p["pos"], 5126, "VEC3", 34962, True),
                 "NORMAL": self.accessor(p["nrm"], 5126, "VEC3", 34962),
                 "TEXCOORD_0": self.accessor(p["uv"], 5126, "VEC2", 34962)}
            if "uv2" in p:
                a["TEXCOORD_1"] = self.accessor(p["uv2"], 5126, "VEC2", 34962)
            idx = self.accessor(p["idx"], 5125, "SCALAR", 34963)
            prims.append({"attributes": a, "indices": idx, "material": p["material"], "mode": 4})
        self.gltf["meshes"].append({"name": node_name, "primitives": prims})
        self.gltf["nodes"].append({"name": node_name, "mesh": len(self.gltf["meshes"]) - 1})
        self.gltf["scenes"][0]["nodes"].append(len(self.gltf["nodes"]) - 1)

    def write(self, path):
        self.gltf["buffers"] = [{"byteLength": len(self.bin)}]
        write_glb(path, self.gltf, bytes(self.bin))


def write_glb(path, gltf, bin_data):
    js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    bd = bin_data + b"\0" * ((4 - len(bin_data) % 4) % 4)
    total = 12 + 8 + len(js) + (8 + len(bd) if bd else 0)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        if bd:
            f.write(struct.pack("<II", len(bd), 0x004E4942) + bd)


def read_glb(path):
    b = open(path, "rb").read()
    jl = struct.unpack("<I", b[12:16])[0]
    gltf = json.loads(b[20:20 + jl])
    rest = b[20 + jl:]
    bin_data = rest[8:8 + struct.unpack("<I", rest[:4])[0]] if len(rest) >= 8 else b""
    return gltf, bin_data


# ---------------------------------------------------------------- world
def convert_world(obj_path, out_glb, materials, textures, double_sided):
    print("reading", obj_path)
    V, VT, VN = [], [], []
    faces = defaultdict(lambda: defaultdict(list))  # node -> material -> [face]
    obj, mtl, lm = "world", "default", -1
    with open(obj_path, "r") as f:
        for line in f:
            c = line[:2]
            if c == "lm":
                lm = int(line.split()[1])
                continue
            if c == "v ":
                V.append(line[2:].split())
            elif c == "vt":
                VT.append(line[3:].split())
            elif c == "vn":
                VN.append(line[3:].split())
            elif c == "f ":
                if any(k in mtl.lower() for k in SKIP_MATERIALS):
                    continue
                # "#lmN": which lightmap page the face samples (N >= 31 means none); split so each page gets its own material
                faces[obj][f"{mtl}#lm{lm if 0 <= lm < 31 else 'x'}"].append([int(p.split("/")[0]) - 1 for p in line[2:].split()])
            elif line.startswith("usemtl"):
                mtl = line.split()[1]
            elif c == "o ":
                obj = line.split()[1]
    V = np.array(V, dtype=np.float64)
    VT = np.array(VT, dtype=np.float32)
    VN = np.array(VN, dtype=np.float32)
    P = (V @ C.T * INCH).astype(np.float32)
    N = (VN.astype(np.float64) @ C.T).astype(np.float32)
    UV = np.stack([VT[:, 0], 1.0 - VT[:, 1]], axis=1).astype(np.float32)
    UV2 = np.zeros_like(UV)
    lighting_bin = obj_path[:-4] + "_vertex_lighting.bin"
    if os.path.exists(lighting_bin):
        vl = np.frombuffer(open(lighting_bin, "rb").read(), dtype=[("u", "<f4"), ("v", "<f4"), ("c", "<u4")])
        if len(vl) == len(V):
            UV2 = np.stack([vl["u"], vl["v"]], axis=1).astype(np.float32)  # image rows top-down, like glTF
            UV2[(UV2 < 0) | (UV2 > 1)] = 0.0  # 1000 = no lightmap

    g = GlbBuilder()
    stats = {"tris": 0, "nodes": 0, "missing_tex": set(), "alpha": defaultdict(int)}

    def emit(node_name, mat_faces):
        prims = []
        for mname, fl in mat_faces.items():
            Fi = np.array(fl, dtype=np.int64)
            used, inv = np.unique(Fi.ravel(), return_inverse=True)
            img, alpha = materials.describe(mname.split("#lm")[0])
            png = textures.png_for(img) if img else None
            if not png:
                stats["missing_tex"].add(mname)
                if alpha == "BLEND":
                    continue  # an untextured decal would render as an opaque grey sheet
            stats["alpha"][alpha] += 1
            mi = g.material(mname, "../Textures/" + png if png else None, double_sided, alpha)
            prims.append({"pos": P[used], "nrm": N[used], "uv": UV[used], "uv2": UV2[used], "idx": inv.astype(np.uint32), "material": mi})
            stats["tris"] += len(Fi)
        if prims:
            g.add_mesh_node(node_name, prims)
            stats["nodes"] += 1

    # World: chunk by face centroid on a CHUNK grid (CoD XY).
    world = faces.pop("world", {})
    faces_for_sun = [(int(k.split("#lm")[1]) if k.split("#lm")[1].isdigit() else -1, f) for k, fl in world.items() for f in fl]
    chunks = defaultdict(lambda: defaultdict(list))
    for mname, fl in world.items():
        Fi = np.array(fl, dtype=np.int64)
        cen = V[Fi].mean(axis=1)
        keys = np.floor(cen[:, :2] / CHUNK).astype(int)
        for (kx, ky), face in zip(map(tuple, keys), fl):
            chunks[(kx, ky)][mname].append(face)
    for (kx, ky), mf in sorted(chunks.items()):
        emit(f"world_{kx}_{ky}", mf)
    for name, mf in faces.items():
        emit(name, mf)  # brushmodel_N keeps its own node so script ents can find it

    os.makedirs(os.path.dirname(out_glb), exist_ok=True)
    g.write(out_glb)
    write_sun_masks(obj_path, os.path.join(os.path.dirname(os.path.dirname(out_glb)), "Lighting"), textures)
    write_baked_lightmaps(obj_path, os.path.join(os.path.dirname(os.path.dirname(out_glb)), "Lighting"))
    global WORLD_FOR_SUN
    WORLD_FOR_SUN = (V, faces_for_sun, UV2)
    print(f"world: {stats['nodes']} nodes, {stats['tris']} tris, {len(g.gltf['materials'])} materials, "
          f"{len(stats['missing_tex'])} without colour map, alpha modes {dict(stats['alpha'])}")
    return sorted(stats["missing_tex"])


def write_sun_masks(obj_path, out_dir, textures):
    """The primary lightmap pages: per-texel visibility of the map's primary lights (green = the sun)."""
    meta = obj_path[:-4] + "_lightmaps.json"
    if not os.path.exists(meta):
        return
    os.makedirs(out_dir, exist_ok=True)
    for i, page in enumerate(json.load(open(meta))["lightmaps"]):
        name = (page.get("primary") or {}).get("name", "").lstrip("*")
        src = textures.index.get("_" + name.lower()) or textures.index.get(name.lower())
        if src:
            Image.open(src).convert("RGB").save(os.path.join(out_dir, f"sunmask{i}.png"))
    print("sun masks written to", out_dir)


RGBM_RANGE = 8.0


def decode_secondary_lightmap(sec_raw, secb_raw, width, height):
    """BO1's baked (non-primary) light for one lightmap page, linear HDR RGB at width x height.

    Read off the game's layer_lmap pixel shaders (lightmapSamplerSecondary s13 / SecondaryB s14):
      secondary  = D3DFMT_R5G6B5, width x 2*height: two planes stacked vertically (v*0.5 and v*0.5+0.5)
      secondaryB = D3DFMT_G16R16, width x height: one intensity per plane, scaled by 31.875
      colour_k   = I_k * (4r, 2(1-r-b), 4b)   from plane k's red/blue chromaticity
      n          = (g_top, g_bottom) * 4 - 2   (green channels; non-bumped surfaces weight colour 2 by it)
      light      = colour_0 + colour_1 * saturate(0.4 + 0.6 * exp(-dot(n, n)))
    """
    c = np.frombuffer(sec_raw, "<u2").astype(np.int64).reshape(2, height, width)
    rgb = np.stack([((c >> 11) & 31) / 31.0, ((c >> 5) & 63) / 63.0, (c & 31) / 31.0], -1)
    inten = np.frombuffer(secb_raw, "<u2").astype(np.float64).reshape(height, width, 2) / 65535.0 * 31.875

    def colour(x, i):
        return i[..., None] * np.stack([4 * x[..., 0], 2 * (1 - x[..., 0] - x[..., 2]), 4 * x[..., 2]], -1)

    n = np.stack([rgb[0, ..., 1], rgb[1, ..., 1]], -1) * 4 - 2
    w = np.clip(0.4 + 0.6 * np.exp(-(n ** 2).sum(-1)), 0, 1)
    return colour(rgb[0], inten[..., 0]) + colour(rgb[1], inten[..., 1]) * w[..., None]


def encode_rgbm(hdr):
    """Linear HDR -> RGBM (rgb * a * RGBM_RANGE), 8 bit, so the page stays a plain PNG/ASTC texture on mobile."""
    hdr = np.clip(hdr / RGBM_RANGE, 0, 1)
    m = np.clip(np.ceil(hdr.max(-1) * 255) / 255, 1 / 255, 1)
    rgb = hdr / m[..., None]
    return (np.concatenate([rgb, m[..., None]], -1) * 255 + 0.5).astype(np.uint8)


def write_baked_lightmaps(obj_path, out_dir):
    """The secondary lightmap pages decoded to RGBM PNGs (lightmap{i}.png): the map's real baked lighting."""
    meta = obj_path[:-4] + "_lightmaps.json"
    if not os.path.exists(meta):
        return
    os.makedirs(out_dir, exist_ok=True)
    root = os.path.dirname(os.path.dirname(obj_path))
    for i, page in enumerate(json.load(open(meta))["lightmaps"]):
        sec, secb = page.get("secondary"), page.get("secondaryB")
        if not sec or not secb or sec.get("format") != 23 or secb.get("format") != 34:
            print(f"lightmap {i}: unexpected secondary formats, skipped")
            continue
        w, h = secb["width"], secb["height"]
        sec_raw = open(os.path.join(root, sec["file"]), "rb").read()
        secb_raw = open(os.path.join(root, secb["file"]), "rb").read()
        if len(sec_raw) != w * h * 4 or len(secb_raw) != w * h * 4:
            print(f"lightmap {i}: unexpected sizes, skipped")
            continue
        hdr = decode_secondary_lightmap(sec_raw, secb_raw, w, h)
        Image.fromarray(encode_rgbm(hdr), "RGBA").save(os.path.join(out_dir, f"lightmap{i}.png"))
        print(f"lightmap {i}: {w}x{h}, median {np.median(hdr):.2f}, p99 {np.percentile(hdr, 99):.2f}")


WORLD_FOR_SUN = None


def floor_hits(origins_cod):
    """For each point: (lightmap page, lightmap uv) of the highest upward-facing world triangle at most 24 inches
    above it (the floor under a prop), or None."""
    if WORLD_FOR_SUN is None:
        return [None] * len(origins_cod)
    V, faces, UV2 = WORLD_FOR_SUN
    lm = np.array([f[0] for f in faces]); F = np.array([f[1] for f in faces], dtype=np.int64)
    T = V[F]
    nrm = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    up = nrm[:, 2] / (np.linalg.norm(nrm, axis=1) + 1e-9) > 0.5
    keep = up & (lm >= 0)
    T, F, lm = T[keep], F[keep], lm[keep]
    mn, mx = T[:, :, :2].min(1), T[:, :, :2].max(1)
    cell = 128.0
    grid = defaultdict(list)
    for idx in range(len(T)):
        for gx in range(int(mn[idx, 0] // cell), int(mx[idx, 0] // cell) + 1):
            for gy in range(int(mn[idx, 1] // cell), int(mx[idx, 1] // cell) + 1):
                grid[(gx, gy)].append(idx)
    out = []
    for o in origins_cod:
        cand = grid.get((int(o[0] // cell), int(o[1] // cell)), [])
        best, best_z = None, -1e9
        for idx in cand:
            a, b, c = T[idx]
            v0, v1, v2 = b[:2] - a[:2], c[:2] - a[:2], np.array(o[:2]) - a[:2]
            den = v0[0] * v1[1] - v1[0] * v0[1]
            if abs(den) < 1e-9:
                continue
            w1 = (v2[0] * v1[1] - v1[0] * v2[1]) / den
            w2 = (v0[0] * v2[1] - v2[0] * v0[1]) / den
            if w1 < -1e-4 or w2 < -1e-4 or w1 + w2 > 1 + 1e-4:
                continue
            z = a[2] + w1 * (b[2] - a[2]) + w2 * (c[2] - a[2])
            if z <= o[2] + 24 and z > best_z:
                best, best_z = (idx, w1, w2), z
        if best is None:
            out.append(None)
            continue
        idx, w1, w2 = best
        out.append((int(lm[idx]), UV2[F[idx, 0]] * (1 - w1 - w2) + UV2[F[idx, 1]] * w1 + UV2[F[idx, 2]] * w2))
    return out


def sample_pages(hits, pages, default):
    """Nearest-texel lookup of floor_hits() results in {page: HxW[xC] array}."""
    out = []
    for hit in hits:
        if hit is None or hit[0] not in pages:
            out.append(default)
            continue
        m = pages[hit[0]]
        h, w = m.shape[:2]
        out.append(m[min(h - 1, int(hit[1][1] * h)), min(w - 1, int(hit[1][0] * w))])
    return out


def prop_sun_visibility(hits, sunmask_dir):
    """Fallback sun visibility for each prop: the sun mask of the world floor right under it."""
    masks = {}
    for i in (0, 1):
        p = os.path.join(sunmask_dir, f"sunmask{i}.png")
        if os.path.exists(p):
            masks[i] = np.asarray(Image.open(p).convert("RGB"))[:, :, 1].astype(np.float32) / 255.0
    return [float(v) for v in sample_pages(hits, masks, 0.35)]


def prop_floor_light(hits, lighting_dir):
    """Fallback baked light for each prop: the decoded lightmap (RGBM, linear) of the floor right under it."""
    pages = {}
    for i in (0, 1):
        p = os.path.join(lighting_dir, f"lightmap{i}.png")
        if os.path.exists(p):
            q = np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) / 255.0
            pages[i] = q[..., :3] * q[..., 3:] * RGBM_RANGE
    return sample_pages(hits, pages, None)


class LightGrid:
    """BO1's light grid (GfxWorld.lightGrid, dumped by the patched OAT as <map>_lightgrid.bin): what the game
    lights static models, entities and characters with. Decoded from the dumped data:
      samples sit on a 32 x 32 x 64 inch lattice at index * (32, 32, 64) - 131072 (rowAxis x, colAxis y); checked
        against the floor lightmap, that placement fits best (log correlation 0.68, half-cell shifts are worse)
      rowDataStart[row] (0xFFFF = empty row) * 4 -> GfxLightGridRow in rawRowData:
        u16 colStart, colCount, zStart, zCount; u32 firstEntry; then runs of
        u8 numCols, u8 numZ, [u8 zOffset if numZ]  (numZ = 0: empty columns); entries are column-major, z ascending
      entry = u16 colorsIndex, u8 primaryLightIndex (== sunPrimaryLightIndex where the sun reaches), u8 needsTrace
      colours = 56 directional samples, each 24 bits: r chroma (6) | 0 | b chroma (6) | 0 | intensity (11) | 0,
        rgb = I * (4r, 2(1 - r - b), 4b), the same chromaticity scheme as the secondary lightmaps.
    The 56 direction vectors aren't in the dump, so a sample is reduced to the mean over all directions.
    Like the secondary lightmaps it holds no primary lights (the sun is added live from primaryLightIndex)."""

    def __init__(self, path):
        b = open(path, "rb").read()
        o = 0

        def rd(fmt):
            nonlocal o
            v = struct.unpack_from(fmt, b, o)
            o += struct.calcsize(fmt)
            return v
        magic, _ = rd("<II")
        if magic != 0x4452474C:
            raise ValueError("not a light grid dump")
        rd("<4B")
        self.sun = rd("<I")[0]
        self.mins = np.array(rd("<3H")); maxs = np.array(rd("<3H"))
        if rd("<II") != (0, 1):
            raise ValueError("unhandled light grid axes")
        rows = rd("<%dH" % rd("<I")[0])
        n = rd("<I")[0]; raw = b[o:o + n]; o += n
        n = rd("<I")[0]; ent = np.frombuffer(b, "<u2", n * 2, o).reshape(n, 2); o += n * 4
        n = rd("<I")[0]; col = np.frombuffer(b, np.uint8, n * 168, o).reshape(n, 56, 3).astype(np.int64)
        v = col[..., 0] | col[..., 1] << 8 | col[..., 2] << 16
        r, bl, inten = (v & 63) / 63.0, (v >> 6 & 63) / 63.0, (v >> 12 & 2047).astype(np.float64)
        self.colours = (np.stack([4 * r, 2 * (1 - r - bl), 4 * bl], -1) * inten[..., None]).mean(1)  # raw units
        self.cell = -np.ones(tuple(maxs - self.mins + 1), np.int32)
        for ri, start in enumerate(rows):
            if start == 0xFFFF:
                continue
            p = start * 4
            cs, cc, zs, _, e = struct.unpack_from("<HHHHI", raw, p)
            p += 12
            c = cs
            while c < cs + cc:
                ncol, nz = raw[p], raw[p + 1]
                p += 2
                if nz:
                    z0 = zs + raw[p] - self.mins[2]
                    p += 1
                    for ci in range(c - self.mins[1], c - self.mins[1] + ncol):
                        self.cell[ri, ci, z0:z0 + nz] = np.arange(e, e + nz)
                        e += nz
                c += ncol
        self.colour_index = ent[:, 0].astype(np.int64)
        self.sun_lit = (ent[:, 1] & 0xFF) == self.sun
        self._filled = np.argwhere(self.cell >= 0)
        print(f"light grid: {len(self._filled)} cells, {len(self.colours)} colour sets")

    def sample(self, origin):
        """(mean rgb in raw grid units, sun fraction) at a CoD-space point: trilinear over whichever of the 8
        surrounding samples exist, else the nearest existing sample."""
        f = (np.asarray(origin, np.float64) + 131072.0) / (32.0, 32.0, 64.0) - self.mins
        base = np.floor(f).astype(int); t = f - base
        acc, sun, wsum = np.zeros(3), 0.0, 0.0
        for d in np.ndindex(2, 2, 2):
            p = base + d
            if np.any(p < 0) or np.any(p >= self.cell.shape):
                continue
            e = self.cell[tuple(p)]
            if e < 0:
                continue
            w = float(np.prod(np.where(d, t, 1 - t))) + 1e-6
            acc += w * self.colours[self.colour_index[e]]
            sun += w * self.sun_lit[e]
            wsum += w
        if wsum == 0:
            p = self._filled[np.argmin(((self._filled - f) ** 2 * (1, 1, 4)).sum(1))]
            e = self.cell[tuple(p)]
            return self.colours[self.colour_index[e]], float(self.sun_lit[e])
        return acc / wsum, sun / wsum

    def write_volume(self, path, scale):
        """Dense mean-colour volume for Unity (KinoLightingBuilder lights entity models with it). Little endian:
        'LGV1', int32 dims[3] (x, y, z samples), float32 origin[3] (CoD inches of sample 0), float32 step[3],
        then float16 rgb[z][y][x] in lightmap units. Missing samples are filled from their neighbours."""
        vol = np.zeros(self.cell.shape + (3,), np.float32)
        have = self.cell >= 0
        vol[have] = self.colours[self.colour_index[self.cell[have]]] * scale
        for _ in range(max(self.cell.shape)):
            if have.all():
                break
            acc = np.zeros_like(vol); cnt = np.zeros(self.cell.shape, np.float32)
            for ax in range(3):
                for sh in (1, -1):
                    hv = np.roll(have, sh, ax); vv = np.roll(vol, sh, ax)
                    edge = [slice(None)] * 3; edge[ax] = 0 if sh == 1 else -1  # no wrap-around
                    hv[tuple(edge)] = False
                    acc += vv * hv[..., None]; cnt += hv
            grow = ~have & (cnt > 0)
            vol[grow] = acc[grow] / cnt[grow, None]
            have = have | grow
        with open(path, "wb") as f:
            f.write(b"LGV1")
            f.write(struct.pack("<3i", *self.cell.shape))
            f.write(struct.pack("<3f", *(self.mins * (32.0, 32.0, 64.0) - 131072.0)))
            f.write(struct.pack("<3f", 32.0, 32.0, 64.0))
            f.write(np.ascontiguousarray(vol.transpose(2, 1, 0, 3)).astype("<f2").tobytes())


# Raw grid intensity -> lightmap units (Lighting/lightmap*.png). Calibrated, not read from the game: at 3288 props
# the floor lightmap / grid mean ratio has median 0.00085 (the mean includes the darker downward samples), and
# both share the same floor (~0.33 vs ~430). 1/1024 sits between that and the upward-facing irradiance.
GRID_SCALE = 1.0 / 1024.0


# ---------------------------------------------------------------- props
def placement_to_unity(entry):
    A = np.array(entry["axis"], dtype=np.float64).T  # columns = forward, left, up
    origin = np.array(entry["origin"], dtype=np.float64)
    FC = F @ C
    R = FC @ A @ np.linalg.inv(FC)
    pos = FC @ origin * INCH
    scale = entry["scale"] * INCH
    # Re-orthonormalise and convert to quaternion (Unity x,y,z,w).
    u, _, vt = np.linalg.svd(R)
    R = u @ vt
    if np.linalg.det(R) < 0:
        R = -R
        scale = -scale
    w = np.sqrt(max(0.0, 1 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
    x = np.sqrt(max(0.0, 1 + R[0, 0] - R[1, 1] - R[2, 2])) / 2
    y = np.sqrt(max(0.0, 1 - R[0, 0] + R[1, 1] - R[2, 2])) / 2
    z = np.sqrt(max(0.0, 1 - R[0, 0] - R[1, 1] + R[2, 2])) / 2
    x = np.copysign(x, R[2, 1] - R[1, 2])
    y = np.copysign(y, R[0, 2] - R[2, 0])
    z = np.copysign(z, R[1, 0] - R[0, 1])
    return {"model": entry["model"], "pos": pos.round(5).tolist(), "rot": [float(x), float(y), float(z), float(w)], "scale": float(scale)}


def convert_props(smodels, model_dirs, out_dir, textures, prefer_lod, materials=None):
    os.makedirs(out_dir, exist_ok=True)
    names = sorted({s["model"] for s in smodels})
    available = {}
    for name in names:
        for lod in (prefer_lod, 0, 1, 2):
            for d in model_dirs:
                p = os.path.join(d, f"{name.lstrip(',')}_lod{lod}.glb")
                if os.path.exists(p):
                    available[name] = p
                    break
            if name in available:
                break
    for name, src in available.items():
        gltf, bin_data = read_glb(src)
        image_map = {}
        new_images = []
        for i, im in enumerate(gltf.get("images", [])):
            base = os.path.splitext(os.path.basename(im.get("uri", "")))[0]
            png = textures.png_for(base)
            if png:
                image_map[i] = len(new_images)
                new_images.append({"uri": "../Textures/" + png})
        new_textures = []
        tex_map = {}
        for i, t in enumerate(gltf.get("textures", [])):
            if t.get("source") in image_map:
                tex_map[i] = len(new_textures)
                new_textures.append({"source": image_map[t["source"]]})
        for m in gltf.get("materials", []):
            m.pop("normalTexture", None)
            lname = m.get("name", "").lower()
            if any(k in lname for k in RUNTIME_ONLY_MATERIALS):
                # Filled in at runtime by the game (clan tag, player emblem): invisible in zombies.
                m["pbrMetallicRoughness"] = {"baseColorFactor": [0, 0, 0, 0]}
                m["alphaMode"] = "MASK"
                m["alphaCutoff"] = 0.5
                continue
            if "glow" in lname:
                m["alphaMode"] = "BLEND"  # tritium dots / lamp glows are additive in the game
            pbr = m.setdefault("pbrMetallicRoughness", {})
            bct = pbr.get("baseColorTexture")
            if bct is not None:
                if bct["index"] in tex_map:
                    bct["index"] = tex_map[bct["index"]]
                else:
                    pbr.pop("baseColorTexture")
            if "baseColorTexture" not in pbr and materials is not None:
                # OAT sometimes leaves the material unlinked; the material file still names its colour map.
                png = textures.png_for(materials.color_image(m.get("name", "")))
                if png:
                    new_images.append({"uri": "../Textures/" + png})
                    new_textures.append({"source": len(new_images) - 1})
                    pbr["baseColorTexture"] = {"index": len(new_textures) - 1}
        # Materials with no material file at all (e.g. "mtl_zombie_teleporter_2", "lambert1"): reuse the model's own
        # first colour map, which these variants share in the game, rather than rendering them white.
        first = next((x["pbrMetallicRoughness"]["baseColorTexture"]["index"] for x in gltf.get("materials", [])
                      if "baseColorTexture" in x.get("pbrMetallicRoughness", {})), None)
        if first is not None:
            for x in gltf.get("materials", []):
                p = x.setdefault("pbrMetallicRoughness", {})
                if "baseColorTexture" not in p and x.get("alphaMode") != "MASK":
                    p["baseColorTexture"] = {"index": first}
        gltf["images"] = new_images
        gltf["textures"] = new_textures
        if not new_images:
            gltf.pop("images")
            gltf.pop("textures")
        write_glb(os.path.join(out_dir, safe(name.lstrip(",")) + ".glb"), gltf, bin_data)
    missing = [n for n in names if n not in available]
    print(f"props: {len(available)} of {len(names)} unique models converted, {len(missing)} missing")
    return available, missing


def load_world_for_sun(obj_path):
    """Just what floor_hits() needs from the world OBJ (for --relight, which skips the GLB)."""
    global WORLD_FOR_SUN
    V, faces, name, mtl, lm = [], [], "world", "", -1
    with open(obj_path, "r") as f:
        for line in f:
            c = line[:2]
            if c == "lm":
                lm = int(line.split()[1])
            elif c == "v ":
                V.append(line[2:].split())
            elif c == "f ":
                if name == "world" and not any(k in mtl.lower() for k in SKIP_MATERIALS):
                    faces.append((lm if 0 <= lm < 31 else -1, [int(q.split("/")[0]) - 1 for q in line[2:].split()]))
            elif line.startswith("usemtl"):
                mtl = line.split()[1]
            elif c == "o ":
                name = line.split()[1]
    V = np.array(V, dtype=np.float64)
    vl = np.frombuffer(open(obj_path[:-4] + "_vertex_lighting.bin", "rb").read(), dtype=[("u", "<f4"), ("v", "<f4"), ("c", "<u4")])
    UV2 = np.stack([vl["u"], vl["v"]], axis=1).astype(np.float32)
    UV2[(UV2 < 0) | (UV2 > 1)] = 0.0
    WORLD_FOR_SUN = (V, faces, UV2)


def prop_lighting(smodels, dump2, lighting_dir, skip_world=False):
    """Per smodel: sun visibility (the floor sun mask under it) and baked light (BO1's light grid at the smodel's own
    lighting origin, or the floor lightmap under it without a grid dump). Also writes Lighting/lightgrid.bytes."""
    hits = floor_hits([s["origin"] for s in smodels])
    sun = prop_sun_visibility(hits, lighting_dir) if not skip_world else [1.0] * len(smodels)
    light = prop_floor_light(hits, lighting_dir)
    grid_path = os.path.join(dump2, "zombie_theater", "maps", "zombie_theater_lightgrid.bin")
    if os.path.exists(grid_path):
        grid = LightGrid(grid_path)
        light = [grid.sample(s.get("lightingOrigin", s["origin"]))[0] * GRID_SCALE for s in smodels]
        os.makedirs(lighting_dir, exist_ok=True)
        grid.write_volume(os.path.join(lighting_dir, "lightgrid.bytes"), GRID_SCALE)
    else:
        print("no light grid dump; props use the floor lightmap")
    return sun, light


def layout_entry(s, sun, light):
    p = dict(placement_to_unity(s), file=safe(s["model"].lstrip(",")), sun=round(float(sun), 3))
    if light is not None:
        p["light"] = [round(float(x), 4) for x in light]
    return p


def relight(args):
    """--relight: recompute prop sun/light in an existing kino_layout.json without rebuilding any asset."""
    maps = os.path.join(args.dump2, "zombie_theater", "maps")
    load_world_for_sun(os.path.join(maps, "zombie_theater.obj"))
    smodels = json.load(open(os.path.join(maps, "zombie_theater_smodels.json")))["smodels"]
    path = os.path.join(args.out, "kino_layout.json")
    props = json.load(open(path))["props"]
    # The layout is the smodel list minus models that failed to convert, in order.
    picked, k = [], 0
    for s in smodels:
        if k < len(props) and safe(s["model"].lstrip(",")) == props[k]["file"]:
            picked.append(s)
            k += 1
    if k != len(props):
        raise SystemExit(f"layout does not match the smodel list ({k} of {len(props)} matched)")
    sun, light = prop_lighting(picked, args.dump2, os.path.join(args.out, "Lighting"))
    layout = [layout_entry(s, v, c) for s, v, c in zip(picked, sun, light)]
    json.dump({"props": layout}, open(path, "w"), separators=(",", ":"))
    L = np.array([p["light"] for p in layout if "light" in p])
    print(f"relit {len(layout)} props; light median {np.median(L, 0).round(3).tolist()}, p95 {np.percentile(L, 95, 0).round(3).tolist()}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dump", required=True, help="OAT dump with models/images/materials (dump)")
    ap.add_argument("--dump2", required=True, help="patched OAT dump with maps/ (dump2)")
    ap.add_argument("--out", required=True)
    ap.add_argument("--max-texture", type=int, default=1024)
    ap.add_argument("--prop-lod", type=int, default=1)
    ap.add_argument("--double-sided", action="store_true")
    ap.add_argument("--skip-world", action="store_true")
    ap.add_argument("--relight", action="store_true", help="only recompute prop lighting in an existing kino_layout.json")
    args = ap.parse_args()
    if args.relight:
        return relight(args)

    zones = ["zombie_theater", "common_zombie"]
    textures = TextureBank([os.path.join(args.dump, z, "images") for z in zones], os.path.join(args.out, "Textures"), args.max_texture)
    materials = MaterialBank([os.path.join(args.dump, z, "materials") for z in zones])

    report = {}
    if not args.skip_world:
        report["world_missing_colour_map"] = convert_world(
            os.path.join(args.dump2, "zombie_theater", "maps", "zombie_theater.obj"),
            os.path.join(args.out, "World", "kino_world.glb"), materials, textures, args.double_sided)

    smodels = json.load(open(os.path.join(args.dump2, "zombie_theater", "maps", "zombie_theater_smodels.json")))["smodels"]
    available, missing = convert_props(smodels, [os.path.join(args.dump, z, "model_export") for z in zones],
                                       os.path.join(args.out, "Props"), textures, args.prop_lod, materials)
    sun, light = prop_lighting(smodels, args.dump2, os.path.join(args.out, "Lighting"), args.skip_world)
    layout = []
    for s, v, c in zip(smodels, sun, light):
        if s["model"] not in available:
            continue
        layout.append(layout_entry(s, v, c))
    json.dump({"props": layout}, open(os.path.join(args.out, "kino_layout.json"), "w"), separators=(",", ":"))
    report["props_missing"] = missing
    json.dump(report, open(os.path.join(args.out, "convert_report.json"), "w"), indent=1)
    print(f"layout: {len(layout)} placements; textures written: {sum(1 for v in textures.done.values() if v)}")


if __name__ == "__main__":
    main()

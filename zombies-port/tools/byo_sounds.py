"""Bring-your-own-files step 4: sound aliases from the user's own Black Ops (T5) dump.

Inputs:
  --banks   folder with the patched-OAT soundbank dump (<zone>/sound/<bank>.aliases.json + loaded .xwma/.wav files)
  --iwd     folder with sounds extracted from the game's IWD archives (streamed music / VO), e.g. F:/ZombiesBYO/sound
Outputs (inside --out):
  Sounds/**.ogg            every referenced sound, decoded with ffmpeg (xWMA / MS-ADPCM / PCM -> Vorbis)
  sound_aliases.json       alias -> variant clips + volume / pitch / distance / loop, values normalised for Unity

Usage:
  python -I byo_sounds.py --banks F:/ZombiesBYO/dump3 --iwd F:/ZombiesBYO/sound --out <UnityProject>/Assets/BYO_Kino --ffmpeg C:/ffmpeg/bin/ffmpeg.exe
"""
import argparse
import glob
import json
import os
import struct
import subprocess
from concurrent.futures import ThreadPoolExecutor

INCH = 0.0254
XWMA_RATES = [2500, 4000, 6000, 8000, 12000, 20000, 24000]  # bytes/s allowed by xWMA (20-192 kbps)
LOOP_FLAG = 0x1  # snd_alias_t.flags bit 0: looping (zmb_perks_machine_loop vs zmb_cha_ching)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--banks", required=True)
    ap.add_argument("--iwd", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--ffmpeg", default="ffmpeg")
    ap.add_argument("--quality", default="4", help="Vorbis -q:a (0-10)")
    args = ap.parse_args()

    out_dir = os.path.join(args.out, "Sounds")
    aliases = {}
    jobs = {}  # source path -> output relative path
    durations = {}  # source path -> exact length (s) from the game's frame count; decoders pad the last packet
    missing = set()
    for alias_file in glob.glob(os.path.join(args.banks, "*", "sound", "*.aliases.json")):
        zone_root = os.path.dirname(os.path.dirname(alias_file))  # <banks>/<zone>
        for a in json.load(open(alias_file, encoding="utf-8"))["aliases"]:
            src = None
            if a["kind"] == "loaded" and a["file"]:
                src = os.path.join(zone_root, a["file"])
            elif a["kind"] == "streamed" and a["file"]:
                rel = a["file"].replace("\\", "/")
                for cand in (rel, "sound/" + rel, "english/" + rel, "english/sound/" + rel):
                    p = os.path.join(args.iwd, cand)
                    if os.path.exists(p):
                        src = p
                        break
            if not src or not os.path.exists(src):
                if a["kind"] != "none":
                    missing.add(a["name"])
                continue
            src = os.path.normpath(src)
            base = os.path.normpath(args.banks) if src.startswith(os.path.normpath(args.banks)) else os.path.normpath(args.iwd)
            rel_out = os.path.splitext(os.path.relpath(src, base))[0]
            rel_out = rel_out.replace("\\", "/").replace(".all/", "/") + ".ogg"
            jobs[src] = rel_out
            hdr = a.get("hdr")
            if hdr and hdr.get("rate"):
                durations[src] = hdr["frames"] / hdr["rate"]
            e = aliases.setdefault(a["name"], {
                "name": a["name"], "clips": [], "secondary": a.get("secondary", ""),
                "volMin": round(a["volMin"] / 65535.0, 3), "volMax": round(a["volMax"] / 65535.0, 3),
                "pitchMin": round(a["pitchMin"] / 32767.0, 3) or 1.0, "pitchMax": round(a["pitchMax"] / 32767.0, 3) or 1.0,
                "distMin": round(a["distMin"] * INCH, 2), "distMax": round(a["distMax"] * INCH, 2),
                "loop": bool(a["flags"] & LOOP_FLAG), "limit": a.get("limitCount", 0),
            })
            if rel_out not in e["clips"]:
                e["clips"].append(rel_out)

    def decode_clean(path):
        r = subprocess.run([args.ffmpeg, "-v", "error", "-i", path, "-f", "null", "-"], capture_output=True, text=True)
        return r.returncode == 0 and not r.stderr.strip()

    def fix_xwma(src):
        """The game stores no WMA bitrate, but the decoder derives its block layout from it. Try the xWMA
        bitrates nearest the measured one and keep the first that decodes without errors."""
        data = bytearray(open(src, "rb").read())
        if data[8:12] != b"XWMA" or data[12:16] != b"fmt ":
            return src
        measured = struct.unpack_from("<I", data, 28)[0]
        tmp = src + ".fixed.xwma"
        for rate in sorted(XWMA_RATES, key=lambda r: abs(r - measured)):
            struct.pack_into("<I", data, 28, rate)
            open(tmp, "wb").write(data)
            if decode_clean(tmp):
                return tmp
        return None

    def convert(item):
        src, rel = item
        trim = ["-t", f"{durations[src]:.4f}"] if src in durations else []
        dst = os.path.join(out_dir, rel)
        if os.path.exists(dst):
            return True
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        if src.endswith(".xwma"):
            src = fix_xwma(src)
            if src is None:
                return False
        r = subprocess.run([args.ffmpeg, "-v", "error", "-y", "-i", src, *trim, "-c:a", "libvorbis", "-q:a", args.quality, dst],
                           capture_output=True, text=True)
        if src.endswith(".fixed.xwma"):
            os.remove(src)
        return r.returncode == 0

    with ThreadPoolExecutor(max_workers=max(2, (os.cpu_count() or 4) - 1)) as pool:
        results = list(pool.map(convert, jobs.items()))
    failed = [rel for (src, rel), ok in zip(jobs.items(), results) if not ok]

    bad = set(failed)
    for e in aliases.values():
        e["clips"] = [c for c in e["clips"] if c not in bad]
    out = [e for e in aliases.values() if e["clips"]]
    json.dump({"aliases": out}, open(os.path.join(args.out, "sound_aliases.json"), "w"), indent=0)
    print(f"aliases {len(out)}, clips converted {len(jobs) - len(failed)} / {len(jobs)}, failed {len(failed)}, "
          f"aliases without audio {len(missing)}")
    if failed:
        print("first failures:", failed[:5])


if __name__ == "__main__":
    main()

// ViewModelClip.cs
//
// An animation clip in the compact "XAN2" format written by tools/byo_weapons.py.
// Keys are CoD-space bone-local values. Rotations replace the bind rotation. Translations depend on
// the clip's asset type: absolute clips (type 1: viewmodel and player anims) replace the bind
// position, relative clips (type 2: AI anims) are offsets added to it. Apply() converts to Unity's
// space the same way glTFast converted the model (X mirrored).

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ZombiesPort
{
    public sealed class ViewModelClip
    {
        public sealed class Track
        {
            public string Bone;
            public ushort[] RotFrames;
            public Quaternion[] Rot;
            public ushort[] PosFrames;
            public Vector3[] Pos;
        }

        public string Name { get; private set; }
        public float Fps { get; private set; }
        public int NumFrames { get; private set; }
        public bool Looped { get; private set; }
        public int AssetType { get; private set; }
        public bool AbsolutePositions => AssetType == 1;
        public float Length => NumFrames > 0 && Fps > 0f ? NumFrames / Fps : 0f;
        public readonly List<Track> Tracks = new List<Track>();
        public readonly List<(string name, float time)> Notifies = new List<(string, float)>();

        public static ViewModelClip Load(TextAsset asset)
        {
            if (asset == null) return null;
            using var r = new BinaryReader(new MemoryStream(asset.bytes));
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "XAN2")
            {
                Debug.LogWarning("ViewModelClip: not an XAN2 clip (re-run tools/byo_weapons.py): " + asset.name);
                return null;
            }
            var clip = new ViewModelClip { Name = asset.name, Fps = r.ReadSingle(), NumFrames = r.ReadInt32(), Looped = r.ReadByte() != 0, AssetType = r.ReadByte() };
            int bones = r.ReadInt32();
            for (int b = 0; b < bones; b++)
            {
                var t = new Track { Bone = ReadName(r) };
                int rc = r.ReadInt32();
                t.RotFrames = new ushort[rc];
                t.Rot = new Quaternion[rc];
                for (int i = 0; i < rc; i++)
                {
                    t.RotFrames[i] = r.ReadUInt16();
                    float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(), w = r.ReadSingle();
                    t.Rot[i] = new Quaternion(x, -y, -z, w); // CoD -> Unity (glTFast mirrors X)
                }
                int pc = r.ReadInt32();
                t.PosFrames = new ushort[pc];
                t.Pos = new Vector3[pc];
                for (int i = 0; i < pc; i++)
                {
                    t.PosFrames[i] = r.ReadUInt16();
                    float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
                    t.Pos[i] = new Vector3(-x, y, z);
                }
                clip.Tracks.Add(t);
            }
            int notes = r.ReadInt32();
            for (int i = 0; i < notes; i++) clip.Notifies.Add((ReadName(r), r.ReadSingle()));
            return clip;
        }

        static string ReadName(BinaryReader r) => Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));

        /// <summary>Poses the bones at time t (seconds). Bones without a track are left alone.</summary>
        public void Apply(float t, IReadOnlyDictionary<string, ViewModelRig.Bone> bones, float weight = 1f)
        {
            float frame = Fps * t;
            if (Looped && NumFrames > 0) frame %= NumFrames;
            frame = Mathf.Clamp(frame, 0f, NumFrames);
            foreach (var track in Tracks)
            {
                if (!bones.TryGetValue(track.Bone, out var bone)) continue;
                if (track.Rot.Length > 0)
                {
                    var q = Sample(track.RotFrames, track.Rot, frame);
                    bone.Transform.localRotation = weight >= 1f ? q : Quaternion.Slerp(bone.Transform.localRotation, q, weight);
                }
                Vector3 p;
                if (track.Pos.Length == 0) p = bone.BindPosition;
                else if (AbsolutePositions && !bone.Relative) p = Sample(track.PosFrames, track.Pos, frame);
                else p = bone.BindPosition + Sample(track.PosFrames, track.Pos, frame);
                bone.Transform.localPosition = weight >= 1f ? p : Vector3.Lerp(bone.Transform.localPosition, p, weight);
            }
        }

        static int Find(ushort[] frames, float frame)
        {
            int lo = 0, hi = frames.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (frames[mid] <= frame) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        static Quaternion Sample(ushort[] frames, Quaternion[] keys, float frame)
        {
            if (keys.Length == 1) return keys[0];
            int i = Find(frames, frame);
            if (i >= keys.Length - 1) return keys[keys.Length - 1];
            float span = frames[i + 1] - frames[i];
            float k = span > 0f ? (frame - frames[i]) / span : 0f;
            return Quaternion.Slerp(keys[i], keys[i + 1], k);
        }

        static Vector3 Sample(ushort[] frames, Vector3[] keys, float frame)
        {
            if (keys.Length == 1) return keys[0];
            int i = Find(frames, frame);
            if (i >= keys.Length - 1) return keys[keys.Length - 1];
            float span = frames[i + 1] - frames[i];
            float k = span > 0f ? (frame - frames[i]) / span : 0f;
            return Vector3.Lerp(keys[i], keys[i + 1], k);
        }
    }
}

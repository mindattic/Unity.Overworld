using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The art set every hero draws with (Textures.loadTextures, RECONSTRUCTION 4.4): one standing picture per facing, the
    /// two halt pictures and the six-frame walk and run cycles per facing, as paths under the original's folder
    /// (heroes.json "sprites").
    /// </summary>
    public sealed class HeroSprites
    {
        static readonly Facing[] Facings = { Facing.Left, Facing.Right, Facing.Up, Facing.Down };

        readonly Dictionary<Facing, string> stand = new Dictionary<Facing, string>();
        readonly Dictionary<Facing, string> halt = new Dictionary<Facing, string>();
        readonly Dictionary<Facing, IReadOnlyList<string>> walk = new Dictionary<Facing, IReadOnlyList<string>>();
        readonly Dictionary<Facing, IReadOnlyList<string>> run = new Dictionary<Facing, IReadOnlyList<string>>();

        HeroSprites()
        {
        }

        /// <summary>The standing picture (stand\face_*) facing <paramref name="facing"/>.</summary>
        public string Stand(Facing facing) => stand[facing];

        /// <summary>
        /// The halt picture (halt\halt_left|right): the original loaded only these two, so up and down show the left one
        /// (the picture a falling hero shows is always halt_left, checkFall).
        /// </summary>
        public string Halt(Facing facing) => halt[facing == Facing.Right ? Facing.Right : Facing.Left];

        /// <summary>The walk cycle (walk\walk_*1..6) facing <paramref name="facing"/>, in frame order.</summary>
        public IReadOnlyList<string> Walk(Facing facing) => walk[facing];

        /// <summary>The run cycle (run\run_*1..6) facing <paramref name="facing"/>, in frame order.</summary>
        public IReadOnlyList<string> Run(Facing facing) => run[facing];

        /// <summary>Every picture of the set, once each.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var list = new List<string>();
                list.Add(halt[Facing.Left]);
                list.Add(halt[Facing.Right]);
                foreach (var f in Facings)
                {
                    list.Add(stand[f]);
                    list.AddRange(walk[f]);
                    list.AddRange(run[f]);
                }

                return list;
            }
        }

        internal static HeroSprites Read(ContentReader r, int walkFrames, int runFrames)
        {
            var sprites = new HeroSprites();
            var standReader = r.Section("stand");
            foreach (var f in Facings) sprites.stand[f] = Picture(standReader, Key(f));
            standReader.Done();
            var haltReader = r.Section("halt");
            sprites.halt[Facing.Left] = Picture(haltReader, "left");
            sprites.halt[Facing.Right] = Picture(haltReader, "right");
            haltReader.Done();
            ReadCycle(r.Section("walk"), sprites.walk, walkFrames + 1);
            ReadCycle(r.Section("run"), sprites.run, runFrames + 1);
            r.Done();
            return sprites;
        }

        static void ReadCycle(ContentReader r, Dictionary<Facing, IReadOnlyList<string>> into, int frames)
        {
            foreach (var f in Facings)
            {
                var key = Key(f);
                var files = r.Texts(key);
                r.Check(files.Count == frames, key, "must list " + frames + " frames, the hero's frame count (has " + files.Count + ")");
                foreach (var file in files) CheckPath(r, key, file);
                while (files.Count < frames) files.Add(files.Count > 0 ? files[files.Count - 1] : string.Empty);
                into[f] = files;
            }

            r.Done();
        }

        static string Key(Facing f) => f.ToString().ToLowerInvariant();

        static string Picture(ContentReader r, string key)
        {
            var path = r.Text(key);
            CheckPath(r, key, path);
            return path;
        }

        static void CheckPath(ContentReader r, string key, string path)
        {
            if (path.Length == 0 || path.IndexOf('\\') >= 0 || path.StartsWith("/", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
                r.Fail(key, "must hold relative paths with forward slashes and a file name (has \"" + path + "\")");
        }
    }

    /// <summary>
    /// How heroes move (Heroes.aiWalking / aiRunning, Input.actionMove; RECONSTRUCTION 4.4 and 5.4), heroes.json
    /// "locomotion". The original's per-frame figures are per 60 Hz tick.
    /// </summary>
    public sealed class LocomotionTuning
    {
        /// <summary>How much a held arrow moves the velocity toward its walk or run speed each tick (0.001).</summary>
        public float Acceleration { get; internal set; }

        /// <summary>The original's gameSpeed: position += velocity x this each tick (1).</summary>
        public float GameSpeed { get; internal set; }

        /// <summary>Velocity per unit of reach toward the cursor (moveSpd, 0.02).</summary>
        public float FollowSpeed { get; internal set; }

        /// <summary>Each axis of the reach is clamped to this (deltaMax, 0.6).</summary>
        public float MaxReach { get; internal set; }

        /// <summary>A reach this far on either axis runs; a walk clamps each axis to it (deltaMin, 0.2).</summary>
        public float RunReach { get; internal set; }

        /// <summary>Within this of the cursor on both axes the velocity is boosted (0.1).</summary>
        public float NearRange { get; internal set; }

        public float NearBoost { get; internal set; }

        /// <summary>Within this of the cursor on both axes the hero stops and stands (0.01).</summary>
        public float ArriveRange { get; internal set; }

        /// <summary>The point of the cursor the hero aims at, from the cursor's position (0.03, 0.01).</summary>
        public DataPoint CursorOffset { get; internal set; }

        /// <summary>The hero's own point, as fractions of heroSize from its position (1/2 across, 1/3 down).</summary>
        public DataPoint HeroPoint { get; internal set; }

        internal static LocomotionTuning Read(ContentReader r)
        {
            var t = new LocomotionTuning
            {
                Acceleration = r.Positive("acceleration"),
                GameSpeed = r.Number("gameSpeed", 0f),
                FollowSpeed = r.Positive("followSpeed"),
                MaxReach = r.Positive("maxReach"),
                RunReach = r.Positive("runReach"),
                NearRange = r.Positive("nearRange"),
                NearBoost = r.Positive("nearBoost"),
                ArriveRange = r.Positive("arriveRange"),
                CursorOffset = r.Pair("cursorOffset"),
                HeroPoint = r.Pair("heroPoint", -1f, 1f),
            };
            r.Check(t.RunReach < t.MaxReach, "runReach", "must be below maxReach");
            r.Check(t.ArriveRange < t.NearRange, "arriveRange", "must be below nearRange");
            r.Done();
            return t;
        }
    }

    /// <summary>
    /// How a hero falls into place (Heroes.checkFall and recallHero; RECONSTRUCTION 3.7 and 4.5), heroes.json "fall".
    /// </summary>
    public sealed class FallTuning
    {
        /// <summary>How far a hero below z 0 rises each tick (0.04).</summary>
        public float Rise { get; internal set; }

        /// <summary>The depth a recalled hero is placed at (-1): it falls in from there.</summary>
        public float RecallZ { get; internal set; }

        /// <summary>A recalled hero lands within +/- half this of its leader on each axis (0.1).</summary>
        public float RecallSpread { get; internal set; }

        internal static FallTuning Read(ContentReader r)
        {
            var t = new FallTuning
            {
                Rise = r.Positive("rise"),
                RecallZ = r.Number("recallZ", float.MinValue, 0f),
                RecallSpread = r.Number("recallSpread", 0f),
            };
            r.Check(t.RecallZ < 0f, "recallZ", "must be below 0, or a recalled hero would not fall");
            r.Done();
            return t;
        }
    }
}
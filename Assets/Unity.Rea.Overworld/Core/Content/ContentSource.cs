using System;
using System.Collections.Generic;
using System.IO;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// Where Core reads its data files from. Core only ever asks for a file by name ("pickups.json") and never
    /// knows whether it came from disk, StreamingAssets, a test or a server, so the rules stay engine-free.
    /// <see cref="ContentRegistry.Load"/> is the one reader.
    /// </summary>
    public interface IContentSource
    {
        /// <summary>A description of the source for error messages (a folder path, "memory").</summary>
        string Describe { get; }

        /// <summary>The text of the file called <paramref name="name"/>, or null if there is no such file.</summary>
        string Read(string name);
    }

    /// <summary>Data files in a folder on disk (the build's StreamingAssets/Content, or the repo's copy in tests).</summary>
    public sealed class DirectoryContentSource : IContentSource
    {
        public DirectoryContentSource(string directory)
        {
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public string Directory { get; }

        public string Describe => Directory;

        public string Read(string name)
        {
            var path = Path.Combine(Directory, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }

    /// <summary>Data files held in memory: tests, and a source that patches a few files over another.</summary>
    public sealed class MemoryContentSource : IContentSource
    {
        readonly Dictionary<string, string> files = new Dictionary<string, string>();
        readonly IContentSource fallback;

        /// <param name="fallback">Read for any file not set here (null: none).</param>
        public MemoryContentSource(IContentSource fallback = null)
        {
            this.fallback = fallback;
        }

        public string Describe => fallback != null ? "memory over " + fallback.Describe : "memory";

        /// <summary>Sets (or, with null, hides) the file called <paramref name="name"/>.</summary>
        public MemoryContentSource Set(string name, string text)
        {
            files[name] = text;
            return this;
        }

        public string Read(string name)
        {
            if (files.TryGetValue(name, out var text)) return text;
            return fallback?.Read(name);
        }
    }

    /// <summary>
    /// The content failed to load. <see cref="Errors"/> lists every problem found, each naming its file, key and
    /// line, so one run reports everything wrong rather than the first thing.
    /// </summary>
    public sealed class ContentException : Exception
    {
        public ContentException(IReadOnlyList<string> errors, string source)
            : base(Format(errors, source))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }

        static string Format(IReadOnlyList<string> errors, string source)
        {
            var heading = errors.Count == 1
                ? "The content in " + source + " is invalid:"
                : "The content in " + source + " is invalid (" + errors.Count + " problems):";
            return heading + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", errors);
        }
    }

    /// <summary>A position or size in world units, read from a data file as an [x, y] pair.</summary>
    public readonly struct DataPoint : IEquatable<DataPoint>
    {
        public DataPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public bool Equals(DataPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is DataPoint other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}

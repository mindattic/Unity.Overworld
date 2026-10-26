using System.Collections.Generic;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The original's pictures, by the path they have in the original's folder ("gfx/area/A3.PNG"). They are imported
    /// verbatim under Art/Resources at the same path (the import turns the magenta colour key into transparency), so a
    /// data file names a picture once and both the catalogue and the game find it.
    /// </summary>
    public sealed class TextureLibrary
    {
        readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>The picture at <paramref name="file"/>, or null (logged) when it was not imported.</summary>
        public Texture2D Load(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (cache.TryGetValue(file, out var texture)) return texture;
            texture = Resources.Load<Texture2D>(ResourcePath(file));
            if (texture == null) Debug.LogError("Unity.Overworld: the picture " + file + " is not imported (Art/Resources/" + file + ").");
            cache[file] = texture;
            return texture;
        }

        /// <summary>The Resources path of an original file: the same path without its extension.</summary>
        public static string ResourcePath(string file)
        {
            var dot = file.LastIndexOf('.');
            var slash = file.LastIndexOf('/');
            return dot > slash ? file.Substring(0, dot) : file;
        }
    }
}

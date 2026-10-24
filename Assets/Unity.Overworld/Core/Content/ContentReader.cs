using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// Reads one JSON object of a data file and validates it as it goes. Every key must be present, of the right
    /// type and in range; a key nobody asked for is an error too (a typo would otherwise be silently ignored). Keys
    /// that start with an underscore are notes for people ("_source") and are skipped. Problems are collected, not
    /// thrown, so one load reports all of them, each as "file.path: 'key' ... (line n)". A value that fails gives
    /// back a fallback so reading can go on.
    /// </summary>
    public sealed class ContentReader
    {
        readonly JsonValue node;
        readonly string path;
        readonly List<string> errors;
        readonly HashSet<string> read = new HashSet<string>();

        internal ContentReader(JsonValue value, string path, List<string> errors)
        {
            node = value;
            this.path = path;
            this.errors = errors;
        }

        /// <summary>The 1-based line this object starts on (0 when it is missing).</summary>
        public int Line => node?.Line ?? 0;

        /// <summary>Where this object sits, for messages ("pickups.json.pickups[3]").</summary>
        public string Path => path;

        /// <summary>True when the key is present (an optional member).</summary>
        public bool Has(string key) => node != null && node.Get(key) != null;

        /// <summary>Records a problem against <paramref name="key"/> (or the object when null).</summary>
        public void Fail(string key, string message)
        {
            var value = key != null ? node?.Get(key) : node;
            var where = value != null ? " (line " + value.Line + ")" : string.Empty;
            errors.Add(path + ": " + (key != null ? "'" + key + "' " : string.Empty) + message + where);
        }

        /// <summary>A cross-field check: records <paramref name="message"/> against <paramref name="key"/> when <paramref name="ok"/> is false.</summary>
        public void Check(bool ok, string key, string message)
        {
            if (!ok) Fail(key, message);
        }

        /// <summary>The nested object <paramref name="key"/>.</summary>
        public ContentReader Section(string key)
        {
            var value = Take(key, JsonKind.Object, "an object");
            return new ContentReader(value, path + "." + key, errors);
        }

        /// <summary>Every element of the array <paramref name="key"/>, each of which must be an object.</summary>
        public List<ContentReader> Objects(string key)
        {
            var list = new List<ContentReader>();
            var value = Take(key, JsonKind.Array, "an array");
            if (value == null) return list;
            for (var i = 0; i < value.Items.Count; i++)
            {
                var item = value.Items[i];
                var itemPath = path + "." + key + "[" + i + "]";
                if (item.Kind != JsonKind.Object)
                {
                    errors.Add(itemPath + ": must be an object (is " + Describe(item) + ") (line " + item.Line + ")");
                    continue;
                }

                list.Add(new ContentReader(item, itemPath, errors));
            }

            return list;
        }

        /// <summary>A finite number in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public float Number(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a number");
            if (value == null) return 0f;
            var number = value.Number;
            if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > float.MaxValue)
            {
                Fail(key, "must be a finite number (is " + Show(number) + ")");
                return 0f;
            }

            if (number < min || number > max)
            {
                Fail(key, "must be " + Range(min, max) + " (is " + Show(number) + ")");
                return (float)Math.Min(Math.Max(number, min), max);
            }

            return (float)number;
        }

        /// <summary>A number strictly greater than <paramref name="above"/>.</summary>
        public float Positive(string key, float above = 0f)
        {
            var present = Has(key) && node.Get(key).Kind == JsonKind.Number;
            var number = Number(key);
            if (present && !(number > above)) Fail(key, "must be greater than " + Show(above) + " (is " + Show(number) + ")");
            return number;
        }

        /// <summary>A whole number in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public int Integer(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a whole number");
            if (value == null) return min > 0 ? min : 0;
            var number = value.Number;
            if (Math.Floor(number) != number || number < int.MinValue || number > int.MaxValue)
            {
                Fail(key, "must be a whole number (is " + Show(number) + ")");
                return min > 0 ? min : 0;
            }

            if (number < min || number > max)
            {
                Fail(key, "must be " + Range(min, max) + " (is " + Show(number) + ")");
                return (int)Math.Min(Math.Max(number, min), max);
            }

            return (int)number;
        }

        public bool Flag(string key)
        {
            var value = Take(key, JsonKind.Bool, "true or false");
            return value != null && value.Bool;
        }

        /// <summary>An optional true/false: <paramref name="fallback"/> when the key is absent.</summary>
        public bool Flag(string key, bool fallback) => Has(key) ? Flag(key) : fallback;

        /// <summary>A string; empty is allowed only with <paramref name="allowEmpty"/>, null never.</summary>
        public string Text(string key, bool allowEmpty = false)
        {
            var value = Take(key, JsonKind.String, "a string");
            if (value == null) return string.Empty;
            if (!allowEmpty && value.Text.Length == 0) Fail(key, "must not be empty");
            return value.Text;
        }

        /// <summary>A string or null (a database column that may be empty); null comes back as null.</summary>
        public string TextOrNull(string key)
        {
            if (node != null && node.Get(key) != null && node.Get(key).Kind == JsonKind.Null)
            {
                read.Add(key);
                return null;
            }

            return Text(key, allowEmpty: true);
        }

        /// <summary>A two-number array [x, y], each in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public DataPoint Pair(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var value = Take(key, JsonKind.Array, "an [x, y] pair");
            if (value == null) return default;
            if (value.Items.Count != 2 || value.Items[0].Kind != JsonKind.Number || value.Items[1].Kind != JsonKind.Number)
            {
                Fail(key, "must be an [x, y] pair of two numbers");
                return default;
            }

            var x = value.Items[0].Number;
            var y = value.Items[1].Number;
            if (x < min || x > max || y < min || y > max)
                Fail(key, "must have both numbers " + Range(min, max) + " (is [" + Show(x) + ", " + Show(y) + "])");
            return new DataPoint((float)x, (float)y);
        }

        /// <summary>One of <typeparamref name="T"/>'s names, written as in the code ("SingleItem").</summary>
        public T Choice<T>(string key) where T : struct
        {
            var value = Take(key, JsonKind.String, "a name");
            if (value == null) return default;
            // Enum.TryParse also accepts numeric text ("0"), comma lists ("Sword, Axe") and padded names, none of
            // which are "one of T's names, written as in the code"; only an exact, single name matches.
            if (Array.IndexOf(Enum.GetNames(typeof(T)), value.Text) >= 0 && Enum.TryParse<T>(value.Text, false, out var parsed)) return parsed;
            Fail(key, "must be one of " + string.Join(", ", Enum.GetNames(typeof(T))) + " (is \"" + value.Text + "\")");
            return default;
        }

        /// <summary>A list of strings.</summary>
        public List<string> Texts(string key)
        {
            var list = new List<string>();
            var value = Take(key, JsonKind.Array, "an array of strings");
            if (value == null) return list;
            foreach (var item in value.Items)
            {
                if (item.Kind != JsonKind.String)
                {
                    errors.Add(path + ": '" + key + "' must hold only strings (has " + Describe(item) + ") (line " + item.Line + ")");
                    continue;
                }

                list.Add(item.Text);
            }

            return list;
        }

        /// <summary>
        /// A database column read as text that holds a whole number ("1"): the 2007 database kept some ids in text
        /// columns.
        /// </summary>
        public int IntegerText(string key)
        {
            var text = Text(key);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                if (text.Length > 0) Fail(key, "must hold a whole number (is \"" + text + "\")");
                return 0;
            }

            return number;
        }

        /// <summary>Reports every key of this object that nothing read. Call once its keys have all been read.</summary>
        public void Done()
        {
            if (node == null) return;
            foreach (var member in node.Members)
            {
                if (!read.Contains(member.Key) && !member.Key.StartsWith("_", StringComparison.Ordinal))
                    errors.Add(path + ": unknown key '" + member.Key + "' (line " + member.Value.Line + "); check its spelling");
            }
        }

        JsonValue Take(string key, JsonKind kind, string what)
        {
            read.Add(key);
            if (node == null) return null; // the enclosing object is missing, already reported
            var value = node.Get(key);
            if (value == null)
            {
                errors.Add(path + ": '" + key + "' is missing (expected " + what + ") (line " + node.Line + ")");
                return null;
            }

            if (value.Kind != kind)
            {
                Fail(key, "must be " + what + " (is " + Describe(value) + ")");
                return null;
            }

            return value;
        }

        static string Describe(JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return value.Bool ? "true" : "false";
                case JsonKind.Number: return Show(value.Number);
                case JsonKind.String: return "\"" + value.Text + "\"";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }

        static string Range(double min, double max)
        {
            var hasMin = min > float.MinValue && min > int.MinValue;
            var hasMax = max < float.MaxValue && max < int.MaxValue;
            if (hasMin && hasMax) return "between " + Show(min) + " and " + Show(max);
            return hasMin ? "at least " + Show(min) : "at most " + Show(max);
        }

        static string Show(double number) => number.ToString("G6", CultureInfo.InvariantCulture);
    }

    /// <summary>Opens a data file as a <see cref="ContentReader"/> over its top-level object, reporting a missing file or bad JSON.</summary>
    static class ContentFiles
    {
        public static ContentReader Open(IContentSource source, string file, List<string> errors)
        {
            var text = source.Read(file);
            if (text == null)
            {
                errors.Add(file + ": the file is missing from " + source.Describe);
                return null;
            }

            JsonValue root;
            try
            {
                root = Json.Parse(text);
            }
            catch (JsonException e)
            {
                errors.Add(file + ": " + e.Message);
                return null;
            }

            if (root.Kind != JsonKind.Object)
            {
                errors.Add(file + ": the document must be an object (line " + root.Line + ")");
                return null;
            }

            return new ContentReader(root, file, errors);
        }
    }
}

using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>One change a fix makes to a database table: rewrite the one row <see cref="Where"/> matches, or add <see cref="Add"/>.</summary>
    public sealed class WorldChange
    {
        public string Table { get; internal set; }

        /// <summary>Column values that pick exactly one row (with <see cref="Set"/>); null for an added row.</summary>
        internal JsonValue Where { get; set; }

        /// <summary>The columns to overwrite in the matched row.</summary>
        internal JsonValue Set { get; set; }

        /// <summary>A whole new row, every column given.</summary>
        internal JsonValue Add { get; set; }

        public bool IsAdd => Add != null;

        /// <summary>Where the change sits in the fixes file, for messages.</summary>
        public string Path { get; internal set; }

        public int Line { get; internal set; }
    }

    /// <summary>
    /// One defect of the 2007 world fixed for play: an id the BIBLE's fixed-defects table cites, why, and the table
    /// changes (if any; the start, exits and asset sources name their fix by id instead).
    /// </summary>
    public sealed class WorldFix
    {
        public string Id { get; internal set; }
        public string Why { get; internal set; }
        public IReadOnlyList<WorldChange> Changes { get; internal set; }
    }

    /// <summary>Where one hero of the party stands when a new game begins: the point under its feet, on a ground line.</summary>
    public sealed class HeroStart
    {
        public int HeroId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
    }

    /// <summary>The area a new game begins in and where each hero stands.</summary>
    public sealed class WorldStart
    {
        public string Fix { get; internal set; }
        public int AreaId { get; internal set; }
        public IReadOnlyList<HeroStart> Heroes { get; internal set; }
    }

    /// <summary>
    /// A way out of an area: a rectangle placed like a 2007 event (an origin and edges relative to it) that, when a hero
    /// stands in it, moves the party to <see cref="ToAreaId"/> with the leader at (<see cref="ToX"/>, <see cref="ToY"/>).
    /// </summary>
    public sealed class ExitRecord
    {
        public string Fix { get; internal set; }
        public int AreaId { get; internal set; }
        public string Name { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Left { get; internal set; }
        public float Right { get; internal set; }
        public float Top { get; internal set; }
        public float Bottom { get; internal set; }
        public int ToAreaId { get; internal set; }
        public float ToX { get; internal set; }
        public float ToY { get; internal set; }

        public bool Contains(float x, float y) => x >= X + Left && x <= X + Right && y >= Y + Bottom && y <= Y + Top;
    }

    /// <summary>An original picture whose shipped copy is broken, and the intact copy the asset import takes instead.</summary>
    public sealed class AssetSource
    {
        public string Fix { get; internal set; }

        /// <summary>The picture as the world names it, relative to the original's folder ("gfx/area/...").</summary>
        public string Path { get; internal set; }

        /// <summary>The intact copy, relative to the folder that holds the original's folder (its sibling projects).</summary>
        public string From { get; internal set; }

        public string Why { get; internal set; }
    }

    /// <summary>
    /// The corrections that turn a faithful world database export into the world the game plays (U.OVW-T38). The export
    /// itself stays exactly as the original database held it; the fixes are data beside it, each with an id and a
    /// reason, applied when the playable world is read. Adds what the original never had: the start and the exits.
    /// </summary>
    public sealed class WorldFixes
    {
        public string File { get; private set; }
        public IReadOnlyList<WorldFix> Fixes { get; private set; }
        public WorldStart Start { get; private set; }
        public IReadOnlyList<ExitRecord> Exits { get; private set; }
        public IReadOnlyList<AssetSource> AssetSources { get; private set; }

        public WorldFix Find(string id)
        {
            foreach (var f in Fixes) if (f.Id == id) return f;
            return null;
        }

        internal static WorldFixes Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var result = new WorldFixes { File = file };

            var fixes = new List<WorldFix>();
            foreach (var r in root.Objects("fixes"))
            {
                var fix = new WorldFix { Id = r.Text("id"), Why = r.Text("why") };
                if (fixes.Exists(f => f.Id == fix.Id)) r.Fail("id", "\"" + fix.Id + "\" is used twice");
                var changes = new List<WorldChange>();
                var i = 0;
                foreach (var c in r.Objects("changes"))
                {
                    var change = new WorldChange { Table = c.Text("table"), Path = r.Path + ".changes[" + i++ + "]", Line = c.Line };
                    if (c.Has("add"))
                    {
                        change.Add = c.Raw("add", JsonKind.Object, "an object");
                        c.Check(!c.Has("where") && !c.Has("set"), "add", "an added row takes no 'where' or 'set'");
                        if (c.Has("where")) c.Raw("where", JsonKind.Object, "an object");
                        if (c.Has("set")) c.Raw("set", JsonKind.Object, "an object");
                    }
                    else
                    {
                        change.Where = c.Raw("where", JsonKind.Object, "an object");
                        change.Set = c.Raw("set", JsonKind.Object, "an object");
                        if (change.Where != null && change.Where.Members.Count == 0) c.Fail("where", "must name at least one column");
                        if (change.Set != null && change.Set.Members.Count == 0) c.Fail("set", "must name at least one column");
                    }

                    c.Done();
                    changes.Add(change);
                }

                fix.Changes = changes;
                r.Done();
                fixes.Add(fix);
            }

            result.Fixes = fixes;

            var s = root.Section("start");
            var start = new WorldStart { Fix = s.Text("fix"), AreaId = s.Integer("area") };
            var heroes = new List<HeroStart>();
            foreach (var h in s.Objects("heroes"))
            {
                heroes.Add(new HeroStart { HeroId = h.Integer("hero", 0), X = h.Number("x"), Y = h.Number("y") });
                h.Done();
            }

            start.Heroes = heroes;
            s.Done();
            result.Start = start;

            var exits = new List<ExitRecord>();
            foreach (var e in root.Objects("exits"))
            {
                var exit = new ExitRecord
                {
                    Fix = e.Text("fix"),
                    AreaId = e.Integer("area"),
                    Name = e.Text("name"),
                    X = e.Number("x"),
                    Y = e.Number("y"),
                    Left = e.Number("left"),
                    Right = e.Number("right"),
                    Top = e.Number("top"),
                    Bottom = e.Number("bottom"),
                    ToAreaId = e.Integer("toArea"),
                    ToX = e.Number("toX"),
                    ToY = e.Number("toY"),
                };
                e.Check(exit.Left < exit.Right, "right", "must be right of 'left'");
                e.Check(exit.Bottom < exit.Top, "top", "must be above 'bottom'");
                e.Done();
                exits.Add(exit);
            }

            result.Exits = exits;

            var assets = new List<AssetSource>();
            foreach (var a in root.Objects("assetSources"))
            {
                assets.Add(new AssetSource { Fix = a.Text("fix"), Path = a.Text("path"), From = a.Text("from"), Why = a.Text("why") });
                a.Done();
            }

            result.AssetSources = assets;
            root.Done();

            // Every fix id the start, exits and asset sources cite must be one of the listed fixes.
            void Cited(string id, string what)
            {
                if (id.Length > 0 && result.Find(id) == null) errors.Add(file + ": " + what + " cites the fix \"" + id + "\", which is not in 'fixes'");
            }

            Cited(start.Fix, "'start'");
            foreach (var e in exits) Cited(e.Fix, "the exit \"" + e.Name + "\"");
            foreach (var a in assets) Cited(a.Fix, "the asset source for \"" + a.Path + "\"");
            return result;
        }

        /// <summary>
        /// The rows of every table of <paramref name="db"/> with the fixes applied in file order: a matched row is
        /// replaced by a copy with the set columns overwritten (its messages then name the fix), an added row is
        /// appended. A change that names no such table or column, or whose 'where' matches no row or several, is an error.
        /// </summary>
        internal Dictionary<string, List<ContentReader>> Apply(MdbDatabase db, List<string> errors)
        {
            var rows = new Dictionary<string, List<ContentReader>>();
            foreach (var t in db.Tables) rows[t.Name] = new List<ContentReader>(t.RowReaders);

            foreach (var fix in Fixes)
            {
                foreach (var c in fix.Changes)
                {
                    var where = c.Path + " (line " + c.Line + "): fix " + fix.Id;
                    var table = db.Table(c.Table);
                    if (table == null)
                    {
                        errors.Add(where + " changes the table '" + c.Table + "', which " + db.File + " does not have");
                        continue;
                    }

                    var list = rows[c.Table];
                    if (c.IsAdd)
                    {
                        list.Add(new ContentReader(c.Add, c.Path + ".add (added by fix " + fix.Id + ")", errors));
                        continue;
                    }

                    if (c.Where == null || c.Set == null) continue; // already reported
                    var ok = true;
                    foreach (var m in c.Where.Members) ok &= KnownColumn(table, m.Key, "where", where, errors);
                    foreach (var m in c.Set.Members) ok &= KnownColumn(table, m.Key, "set", where, errors);
                    if (!ok) continue;

                    var matches = new List<int>();
                    for (var i = 0; i < list.Count; i++)
                        if (Matches(list[i].Node, c.Where)) matches.Add(i);
                    if (matches.Count != 1)
                    {
                        errors.Add(where + "'s 'where' matches " + matches.Count + " rows of " + c.Table + " (it must match exactly one)");
                        continue;
                    }

                    var row = list[matches[0]];
                    list[matches[0]] = new ContentReader(Merge(row.Node, c.Set), row.Path + " (as fixed by " + fix.Id + ")", errors);
                }
            }

            return rows;
        }

        static bool KnownColumn(MdbTable table, string column, string part, string where, List<string> errors)
        {
            foreach (var c in table.Columns)
                if (c.Key == column) return true;
            errors.Add(where + "'s '" + part + "' names the column '" + column + "', which " + table.Name + " does not have");
            return false;
        }

        static bool Matches(JsonValue row, JsonValue where)
        {
            if (row == null) return false;
            foreach (var m in where.Members)
            {
                var value = row.Get(m.Key);
                if (value == null || !Same(value, m.Value)) return false;
            }

            return true;
        }

        static bool Same(JsonValue a, JsonValue b)
        {
            if (a.Kind != b.Kind) return false;
            switch (a.Kind)
            {
                case JsonKind.Null: return true;
                case JsonKind.Bool: return a.Bool == b.Bool;
                case JsonKind.Number: return a.Number == b.Number;
                case JsonKind.String: return string.Equals(a.Text, b.Text, StringComparison.Ordinal);
                default: return false;
            }
        }

        static JsonValue Merge(JsonValue row, JsonValue set)
        {
            var members = new List<KeyValuePair<string, JsonValue>>();
            foreach (var m in row.Members)
            {
                var replaced = set.Get(m.Key);
                members.Add(new KeyValuePair<string, JsonValue>(m.Key, replaced ?? m.Value));
            }

            return JsonValue.Object(members, row.Line);
        }
    }
}

using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>One table of an exported Access database: its columns as the database declared them and its rows, untouched.</summary>
    public sealed class MdbTable
    {
        internal MdbTable(string name, IReadOnlyList<KeyValuePair<string, string>> columns, List<ContentReader> rows)
        {
            Name = name;
            Columns = columns;
            RowReaders = rows;
        }

        public string Name { get; }

        /// <summary>Column name and its .NET type name ("Int32", "Single", "String"), in table order.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Columns { get; }

        public int RowCount => RowReaders.Count;

        internal List<ContentReader> RowReaders { get; }
    }

    /// <summary>
    /// An Access .mdb as exported by tools/export-mdb.ps1: every table with its columns and rows. The export is
    /// checked against the schema the original database had (table names, column names, column types), so a
    /// re-export that drifted fails loudly. Typed views (<see cref="WorldData"/>, <see cref="HeroDatabase"/>) read
    /// the tables; the raw tables stay available so no column is lost.
    /// </summary>
    public sealed class MdbDatabase
    {
        readonly Dictionary<string, MdbTable> tables = new Dictionary<string, MdbTable>();

        MdbDatabase(string file, string source)
        {
            File = file;
            Source = source;
        }

        /// <summary>The export file's name ("world-2007.json").</summary>
        public string File { get; }

        /// <summary>The "_source" note: which .mdb it was, and when that file was written.</summary>
        public string Source { get; }

        public IReadOnlyCollection<MdbTable> Tables => tables.Values;

        public MdbTable Table(string name) => tables.TryGetValue(name, out var table) ? table : null;

        /// <summary>
        /// Parses and schema-checks an export. <paramref name="schema"/> lists each expected table as
        /// "Name: column Type, column Type"; any table, column or type that differs is an error.
        /// </summary>
        internal static MdbDatabase Read(IContentSource source, string file, string[] schema, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var note = root.Text("_source");
            var db = new MdbDatabase(file, note);
            var expected = new Dictionary<string, string[]>();
            foreach (var line in schema)
            {
                var colon = line.IndexOf(':');
                expected[line.Substring(0, colon)] = line.Substring(colon + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }

            // The tables are the members of the "tables" object; read each by hand so every error names its table.
            var tablesNode = root.Section("tables");
            foreach (var name in expected.Keys)
            {
                if (!tablesNode.Has(name))
                {
                    errors.Add(file + ".tables: the table '" + name + "' is missing");
                    continue;
                }

                var table = tablesNode.Section(name);
                var columnReaders = table.Objects("columns");
                var columns = new List<KeyValuePair<string, string>>();
                foreach (var c in columnReaders)
                {
                    var columnName = c.Text("name");
                    var columnType = c.Text("type");
                    c.Done();
                    columns.Add(new KeyValuePair<string, string>(columnName, columnType));
                }

                var want = expected[name];
                if (columns.Count != want.Length)
                    table.Fail("columns", "has " + columns.Count + " columns, the original database had " + want.Length);
                for (var i = 0; i < Math.Min(columns.Count, want.Length); i++)
                {
                    var parts = want[i].Trim().Split(' ');
                    if (columns[i].Key != parts[0] || columns[i].Value != parts[1])
                        table.Fail("columns", "column " + (i + 1) + " is " + columns[i].Key + " " + columns[i].Value + ", the original had " + parts[0] + " " + parts[1]);
                }

                var rows = table.Objects("rows");
                table.Done();
                db.tables[name] = new MdbTable(name, columns, rows);
            }

            tablesNode.Done();
            root.Done();
            return db;
        }
    }
}

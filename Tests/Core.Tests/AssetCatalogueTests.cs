using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// docs/asset-catalogue.tsv records every one of the original's 895 files with a disposition (RECONSTRUCTION
    /// section 17). These tests check the catalogue is well formed and agrees with section 17, and diff it against the
    /// original's real file list (the built folder, or Overworld.zip beside it) so no asset is silently missing.
    /// The diff against the original is inconclusive, not green, on a machine without the original.
    /// </summary>
    public class AssetCatalogueTests
    {
        const char Bs = (char)92;
        const int OriginalFileCount = 895;
        static readonly string[] Dispositions = { "used", "union", "not-shipped" };
        const string OriginalFolder = @"R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\Overworld";

        sealed class Entry { public string Path; public long Bytes; public string Disposition; public string Note; }

        static string RepoFile(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(relative + " was not found above " + AppContext.BaseDirectory);
        }

        static List<Entry> catalogue;

        static List<Entry> Catalogue => catalogue ??= Parse();

        static List<Entry> Parse()
        {
            var entries = new List<Entry>();
            foreach (var line in File.ReadAllLines(RepoFile("docs/asset-catalogue.tsv")).Skip(1).Where(l => l.Length > 0))
            {
                var c = line.Split('	');
                if (c.Length != 4 || !long.TryParse(c[1], out var bytes))
                    throw new InvalidDataException("Catalogue line must have path, bytes, disposition, note: " + line);
                entries.Add(new Entry { Path = c[0], Bytes = bytes, Disposition = c[2], Note = c[3] });
            }

            return entries;
        }

        static int Count(string prefix) => Catalogue.Count(e => e.Path.StartsWith(prefix, StringComparison.Ordinal));

        [Test]
        public void CatalogueHasAllOriginalFiles() => Assert.AreEqual(OriginalFileCount, Catalogue.Count);

        [Test]
        public void EveryPathIsListedOnce()
        {
            var dupes = Catalogue.GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(dupes.ToList(), "Paths listed twice");
        }

        [Test]
        public void EveryFileHasAKnownDispositionAndANote()
        {
            foreach (var e in Catalogue)
            {
                Assert.Contains(e.Disposition, Dispositions, e.Path + " has an unknown disposition");
                Assert.IsNotEmpty(e.Note, e.Path + " has no note");
                Assert.IsFalse(e.Path.Contains(Bs) || e.Path.StartsWith("/"), e.Path + " must be a relative forward-slash path");
            }
        }

        [Test]
        public void FolderCountsMatchSection17()
        {
            Assert.AreEqual(52, Count("data/books/"));
            Assert.AreEqual(53, Count("gfx/area/") - Count("gfx/area/West of Town/"));
            Assert.AreEqual(14, Count("gfx/area/West of Town/"));
            Assert.AreEqual(4, Count("gfx/clouds/"));
            Assert.AreEqual(19, Count("gfx/cursor/"));
            Assert.AreEqual(30, Count("gfx/enemy/"));
            Assert.AreEqual(51, Count("gfx/gui/"));
            Assert.AreEqual(316, Count("gfx/hero/"));
            Assert.AreEqual(131, Count("gfx/hero/Chrono/"));
            Assert.AreEqual(133, Count("gfx/hero/belmont/"));
            Assert.AreEqual(47, Count("gfx/hero/girl/"));
            Assert.AreEqual(23, Count("gfx/items/"));
            Assert.AreEqual(22, Count("gfx/npc/"));
            Assert.AreEqual(27, Count("gfx/prop/"));
            Assert.AreEqual(6, Count("gfx/ring/"));
            Assert.AreEqual(8, Count("gfx/status/"));
            Assert.AreEqual(9, Count("gfx/tent/"));
            Assert.AreEqual(190, Count("gfx/weapon/"));
            Assert.AreEqual(8, Count("gfx/weather/"));
            Assert.AreEqual(38, Count("sfx/"));
            Assert.AreEqual(14, Catalogue.Count(e => !e.Path.Contains('/') && !e.Path.EndsWith(".mdb")));
        }

        [Test]
        public void RecordedAndUsedExamplesHaveTheExpectedDisposition()
        {
            string D(string path) => Catalogue.Single(e => e.Path == path).Disposition;
            Assert.AreEqual("used", D("data/world.mdb"));
            Assert.AreEqual("union", D("world.mdb"));
            Assert.AreEqual("union", D("hero.mdb"));
            Assert.AreEqual("not-shipped", D("Engine.exe"));
            Assert.AreEqual("used", D("sfx/hero/regen.wav"));
            Assert.AreEqual("union", D("gfx/hero/belmont/" + Catalogue.First(e => e.Path.StartsWith("gfx/hero/belmont/")).Path.Split('/').Last()));
            Assert.AreEqual(3, Catalogue.Select(e => e.Disposition).Distinct().Count());
        }

        static IEnumerable<KeyValuePair<string, long>> OriginalFiles()
        {
            if (Directory.Exists(OriginalFolder))
            {
                var root = OriginalFolder.TrimEnd(Bs);
                return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(f => new KeyValuePair<string, long>(f.Substring(root.Length + 1).Replace(Bs, '/'), new FileInfo(f).Length)).ToList();
            }

            var zip = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(OriginalFolder) ?? "", "overhead 2", "Overworld.zip");
            if (File.Exists(zip))
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
                var files = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name))
                    .Select(e => new KeyValuePair<string, long>(e.FullName.Replace(Bs, '/'), e.Length)).ToList();
                var top = files[0].Key.Substring(0, files[0].Key.IndexOf('/') + 1);
                if (top.Length > 0 && files.All(f => f.Key.StartsWith(top, StringComparison.Ordinal)))
                    files = files.Select(f => new KeyValuePair<string, long>(f.Key.Substring(top.Length), f.Value)).ToList();
                return files;
            }

            return null;
        }

        [Test]
        public void CatalogueMatchesTheOriginalFileListExactly()
        {
            var original = OriginalFiles();
            if (original == null) Assert.Inconclusive("The original Overworld folder (or Overworld.zip) is not available on this machine.");

            var list = original.ToList();
            var catalogued = Catalogue.ToDictionary(e => e.Path, e => e.Bytes, StringComparer.Ordinal);
            var missing = list.Where(o => !catalogued.ContainsKey(o.Key)).Select(o => o.Key).ToList();
            var extra = catalogued.Keys.Where(k => list.All(o => o.Key != k)).ToList();
            Assert.IsEmpty(missing, "Original files with no catalogue entry (silently missing)");
            Assert.IsEmpty(extra, "Catalogue entries with no original file");

            var wrongSize = list.Where(o => catalogued[o.Key] != o.Value).Select(o => o.Key).ToList();
            Assert.IsEmpty(wrongSize, "Original files whose size differs from the catalogue");
            Assert.AreEqual(OriginalFileCount, list.Count, "The original no longer has 895 files");
        }
    }
}

using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>One entry of the manifest's world list.</summary>
    public sealed class WorldEntry
    {
        public string Edition { get; internal set; }
        public string File { get; internal set; }
        public WorldKind Kind { get; internal set; }
        public string Note { get; internal set; }

        /// <summary>The fixes file the world is played with (null: the world is only a record).</summary>
        public string Fixes { get; internal set; }
    }

    /// <summary>
    /// Everything the original kept in data or hard-coded in its source, loaded and validated: both world
    /// databases, hero.mdb, the pickups, quests, weapons, heroes, walls, props, books and how the world is drawn. <see cref="Load"/> is the
    /// one reader; it throws a <see cref="ContentException"/> listing every problem, each with file, key and line.
    /// <para>
    /// The owner chose the 2007 root world.mdb as the game's world. Both databases are loaded; <see cref="PlayableWorld"/>
    /// is the one the manifest names to play, read with its fixes file (U.OVW-T38) and checked by
    /// <see cref="WorldPlayability"/>; <see cref="ReferenceWorld"/> is the one kept as a reference record, and
    /// <see cref="Worlds"/> holds every edition exactly as its database held it.
    /// </para>
    /// </summary>
    public sealed class ContentRegistry
    {
        ContentRegistry()
        {
        }

        public const string ManifestFile = "content.json";

        public IReadOnlyList<WorldEntry> WorldEntries { get; private set; }
        public IReadOnlyList<WorldData> Worlds { get; private set; }
        public WorldData ReferenceWorld { get; private set; }
        public WorldData PlayableWorld { get; private set; }
        public HeroDatabase HeroDatabase { get; private set; }
        public PickupData Pickups { get; private set; }
        public QuestData Quests { get; private set; }
        public WeaponData Weapons { get; private set; }
        public HeroData Heroes { get; private set; }
        public WallData Walls { get; private set; }
        public PropData Props { get; private set; }
        public BookData Books { get; private set; }

        /// <summary>How the world is drawn: the area tile grid, camera, backdrop and object sprites (view.json).</summary>
        public ViewData View { get; private set; }

        /// <summary>The clock, its periods and the calendar (time.json).</summary>
        public TimeData Time { get; private set; }

        /// <summary>The heads-up text: fonts, the clock's lines and how messages show (hud.json).</summary>
        public HudData Hud { get; private set; }

        /// <summary>The original's own dangling references, kept faithfully (not errors): where and what.</summary>
        public IReadOnlyList<string> Warnings { get; private set; }

        public WorldData World(string edition)
        {
            foreach (var w in Worlds) if (w.Edition == edition) return w;
            return null;
        }

        /// <summary>Loads and validates every data file; throws <see cref="ContentException"/> with all problems if any.</summary>
        public static ContentRegistry Load(IContentSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var errors = new List<string>();
            var registry = Read(source, errors);
            if (errors.Count > 0) throw new ContentException(errors, source.Describe);
            return registry;
        }

        /// <summary>Like <see cref="Load"/> but returns the problems instead of throwing (null registry when any).</summary>
        public static bool TryLoad(IContentSource source, out ContentRegistry registry, out IReadOnlyList<string> errors)
        {
            var list = new List<string>();
            var read = Read(source, list);
            errors = list;
            registry = list.Count == 0 ? read : null;
            return list.Count == 0;
        }

        static ContentRegistry Read(IContentSource source, List<string> errors)
        {
            var registry = new ContentRegistry();
            var manifest = ContentFiles.Open(source, ManifestFile, errors);
            if (manifest == null) return registry;

            var referenceEdition = manifest.Text("referenceWorld");
            var playableEdition = manifest.Text("playableWorld");
            var entries = new List<WorldEntry>();
            var worlds = new List<WorldData>();
            foreach (var r in manifest.Objects("worlds"))
            {
                var entry = new WorldEntry
                {
                    Edition = r.Text("edition"),
                    File = r.Text("file"),
                    Kind = r.Choice<WorldKind>("kind"),
                    Note = r.Text("note"),
                    Fixes = r.Has("fixes") ? r.Text("fixes") : null,
                };
                r.Done();
                if (entries.Exists(e => e.Edition == entry.Edition)) r.Fail("edition", "\"" + entry.Edition + "\" is listed twice");
                entries.Add(entry);
                var world = WorldData.Read(source, entry.File, entry.Edition, entry.Kind, errors);
                if (world != null) worlds.Add(world);
            }

            registry.WorldEntries = entries;
            registry.Worlds = worlds;
            registry.ReferenceWorld = registry.World(referenceEdition);
            if (registry.ReferenceWorld == null) errors.Add(ManifestFile + ": 'referenceWorld' \"" + referenceEdition + "\" is not one of the listed worlds");

            // The world played is the chosen edition read again with its fixes; Worlds keeps every edition as the original held it.
            var playableEntry = entries.Find(e => e.Edition == playableEdition);
            if (playableEntry == null)
                errors.Add(ManifestFile + ": 'playableWorld' \"" + playableEdition + "\" is not one of the listed worlds");
            else if (playableEntry.Fixes == null)
                registry.PlayableWorld = registry.World(playableEdition);
            else
            {
                var fixes = WorldFixes.Read(source, playableEntry.Fixes, errors);
                if (fixes != null)
                    registry.PlayableWorld = WorldData.Read(source, playableEntry.File, playableEntry.Edition, playableEntry.Kind, errors, fixes);
            }

            registry.HeroDatabase = HeroDatabase.Read(source, manifest.Text("heroDatabase"), errors);
            var pickupsFile = manifest.Text("pickups");
            registry.Pickups = PickupData.Read(source, pickupsFile, errors);
            registry.Quests = QuestData.Read(source, manifest.Text("quests"), errors);
            registry.Weapons = WeaponData.Read(source, manifest.Text("weapons"), errors);
            registry.Heroes = HeroData.Read(source, manifest.Text("heroes"), registry.Weapons, errors);
            if (registry.PlayableWorld != null) WorldPlayability.Check(registry.PlayableWorld, registry.Heroes, errors);
            registry.Walls = WallData.Read(source, manifest.Text("walls"), errors);
            registry.Props = PropData.Read(source, manifest.Text("props"), errors);
            registry.Books = BookData.Read(source, manifest.Text("books"), errors);
            registry.View = ViewData.Read(source, manifest.Text("view"), registry.Props, errors);
            registry.Time = TimeData.Read(source, manifest.Text("time"), errors);
            registry.Hud = HudData.Read(source, manifest.Text("hud"), errors);
            manifest.Done();

            // A pickup that opens a book by its own name ("CHANGE BOOK TO ITEM") must name a real book.
            if (registry.Pickups != null && registry.Books != null)
            {
                foreach (var p in registry.Pickups.Pickups)
                    if (p.Script.Contains("CHANGE BOOK TO ITEM") && registry.Books.Find(p.Name) == null)
                        errors.Add(pickupsFile + ": pickup " + p.Index + " (" + p.Name + ") opens a book of its own name, and there is no such book");
            }

            // The playable world is the same export read a second time; a problem in a row no fix touches would otherwise be named twice.
            var unique = new HashSet<string>();
            errors.RemoveAll(e => !unique.Add(e));

            var warnings = new List<string>();
            foreach (var w in worlds) warnings.AddRange(w.Warnings);
            if (registry.HeroDatabase != null) warnings.AddRange(registry.HeroDatabase.Warnings);
            registry.Warnings = warnings;
            return registry;
        }
    }
}

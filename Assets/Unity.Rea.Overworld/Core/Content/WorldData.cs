using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>Which of the original's two world databases a <see cref="WorldData"/> came from.</summary>
    public enum WorldKind
    {
        /// <summary>The engine's own data\world.mdb (2006): areas, events, scripts, item textures, items and pickups.</summary>
        Engine,

        /// <summary>The later root world.mdb (2007): areas, scenes, terrain, props, NPCs, enemies, events and scripts.</summary>
        Root,
    }

    public sealed class AreaRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
    }

    /// <summary>A rectangle that runs a script while a hero stands in it (RECONSTRUCTION 9.4).</summary>
    public sealed class EventRecord
    {
        public int AreaId { get; internal set; }
        public int Id { get; internal set; }
        public string Name { get; internal set; }

        /// <summary>The 2007 database positions an event; the 2006 one does not (HasOrigin false).</summary>
        public bool HasOrigin { get; internal set; }

        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Left { get; internal set; }
        public float Right { get; internal set; }
        public float Top { get; internal set; }
        public float Bottom { get; internal set; }
        public int ScriptId { get; internal set; }
    }

    public sealed class ScriptRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public string Text { get; internal set; }

        /// <summary>Seconds between runs while a hero stays inside the event; 0 runs once on entry.</summary>
        public float Rate { get; internal set; }
    }

    /// <summary>A 2006 item texture row: a placeholder path on the author's C: drive, never used.</summary>
    public sealed class ItemTextureRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public string File { get; internal set; }
    }

    /// <summary>A 2006 item row. <see cref="Type"/> is the old vocabulary ("OneTime"), not the engine's ItemType.</summary>
    public sealed class ItemRecord
    {
        public int Id { get; internal set; }
        public int TextureId { get; internal set; }
        public string Name { get; internal set; }
        public string Script { get; internal set; }
        public string Effect { get; internal set; }
        public string Description { get; internal set; }
        public float Weight { get; internal set; }
        public string Type { get; internal set; }
        public int Amount { get; internal set; }
    }

    public sealed class PickupRecord
    {
        public int AreaId { get; internal set; }
        public int ItemId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
    }

    /// <summary>A background picture of the 2007 world, placed in world units (x, y, z is its origin, width and height its size).</summary>
    public sealed class SceneRecord
    {
        public int AreaId { get; internal set; }
        public int Id { get; internal set; }
        public string Name { get; internal set; }

        /// <summary>The picture's path as the database wrote it, backslashes and all.</summary>
        public string Image { get; internal set; }

        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
    }

    /// <summary>A ground line a hero stands on, in a 2007 area.</summary>
    public sealed class TerrainRecord
    {
        public int AreaId { get; internal set; }
        public int Id { get; internal set; }
        public float X1 { get; internal set; }
        public float Y1 { get; internal set; }
        public float X2 { get; internal set; }
        public float Y2 { get; internal set; }
    }

    /// <summary>A kind of prop: its picture and the picture's size in pixels.</summary>
    public sealed class PropArtRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public string Image { get; internal set; }
        public int ImageWidth { get; internal set; }
        public int ImageHeight { get; internal set; }
    }

    /// <summary>A prop placed in a 2007 area, at a size in world units.</summary>
    public sealed class AreaPropRecord
    {
        public int AreaId { get; internal set; }
        public int PropId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
    }

    public sealed class NpcRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }

        /// <summary>The art folder as the database wrote it.</summary>
        public string Directory { get; internal set; }
    }

    public sealed class AreaNpcRecord
    {
        public int AreaId { get; internal set; }
        public int NpcId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }

        /// <summary>"stand" or "pursue".</summary>
        public string State { get; internal set; }
    }

    public sealed class EnemyRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public int MaxHealth { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public float Range { get; internal set; }
        public string State { get; internal set; }
        public string Motion { get; internal set; }
        public string Class { get; internal set; }
    }

    public sealed class AreaEnemyRecord
    {
        public int AreaId { get; internal set; }
        public int EnemyId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
    }

    /// <summary>
    /// One of the original's two world databases as typed records. The raw export stays on <see cref="Database"/>.
    /// Tables a database did not have are empty lists. <see cref="Warnings"/> names the original's own dangling
    /// references, kept faithfully (a scene whose area does not exist) rather than rejected.
    /// </summary>
    public sealed class WorldData
    {
        public string Edition { get; internal set; }
        public WorldKind Kind { get; internal set; }
        public MdbDatabase Database { get; internal set; }

        public List<AreaRecord> Areas { get; } = new List<AreaRecord>();
        public List<EventRecord> Events { get; } = new List<EventRecord>();
        public List<ScriptRecord> Scripts { get; } = new List<ScriptRecord>();

        public List<ItemTextureRecord> ItemTextures { get; } = new List<ItemTextureRecord>();
        public List<ItemRecord> Items { get; } = new List<ItemRecord>();
        public List<PickupRecord> Pickups { get; } = new List<PickupRecord>();

        public List<SceneRecord> Scenes { get; } = new List<SceneRecord>();
        public List<TerrainRecord> Terrain { get; } = new List<TerrainRecord>();
        public List<PropArtRecord> PropArt { get; } = new List<PropArtRecord>();
        public List<AreaPropRecord> AreaProps { get; } = new List<AreaPropRecord>();
        public List<NpcRecord> Npcs { get; } = new List<NpcRecord>();
        public List<AreaNpcRecord> AreaNpcs { get; } = new List<AreaNpcRecord>();
        public List<EnemyRecord> Enemies { get; } = new List<EnemyRecord>();
        public List<string> EnemyClasses { get; } = new List<string>();
        public List<string> EnemyMotions { get; } = new List<string>();
        public List<string> EnemyStates { get; } = new List<string>();
        public List<AreaEnemyRecord> AreaEnemies { get; } = new List<AreaEnemyRecord>();

        public List<string> Warnings { get; } = new List<string>();

        /// <summary>The corrections this world was read with (null for a faithful record of the original).</summary>
        public WorldFixes Fixes { get; internal set; }

        /// <summary>Where a new game begins (only the playable world has one; the original databases never did).</summary>
        public WorldStart Start { get; internal set; }

        /// <summary>The ways between areas (the original databases had none).</summary>
        public List<ExitRecord> Exits { get; } = new List<ExitRecord>();

        public AreaRecord FindArea(int id) => Areas.Find(a => a.Id == id);
        public ScriptRecord FindScript(int id) => Scripts.Find(s => s.Id == id);
        public PropArtRecord FindPropArt(int id) => PropArt.Find(p => p.Id == id);

        internal static readonly string[] EngineSchema =
        {
            "Area: areaID Int32, areaNAME String",
            "Event: areaID Int32, eventID Int32, eventNAME String, eventLEFT Single, eventRIGHT Single, eventTOP Single, eventBOTTOM Single, scriptID Int32",
            "Item_Textures: textureID Int32, textureNAME String, textureFILE String",
            "Items: itemID Int32, texureID Int32, itemName String, itemScript String, itemEffect String, itemDescription String, itemWeight Single, itemType String, itemAmount Int32",
            "Pickup: areaID Int32, itemID Int32, X Single, Y Single, Z Single",
            "Script: scriptID Int32, scriptNAME String, scriptTEXT String, scriptRATE Single",
        };

        internal static readonly string[] RootSchema =
        {
            "Area: areaID Int32, areaNAME String",
            "Area_Enemies: areaID Int32, enemyID Int32, enemyX Single, enemyY Single, enemyZ Single",
            "Area_Npcs: areaID String, npcID Int32, npcX Single, npcY Single, npcSTATE String",
            "Area_Props: areaID Int32, propID Int32, propX Single, propY Single, propZ Single, propWIDTH Single, propHEIGHT Single",
            "Enemy: enemyID String, enemyNAME String, enemyMAXHP String, enemyMOVE_SPD Single, enemyRANGE Single, enemySTATE String, enemyMOTION String, enemyCLASS String",
            "Enemy_Class: enemyCLASS String",
            "Enemy_Images: enemyID String, enemySTAND_LEFT String, enemySTAND_RIGHT String, enemyWALK_LEFT String, enemyWALK_RIGHT String, enemyATTACK_LEFT String, enemyATTACK_RIGHT String, enemyHURT_LEFT String, enemyHURT_RIGHT String",
            "Enemy_Motion: enemyMOTION String",
            "Enemy_State: enemySTATE String",
            "Event: areaID Int32, eventID Int32, eventNAME String, eventX Single, eventY Single, eventLEFT Single, eventRIGHT Single, eventTOP Single, eventBOTTOM Single, scriptID Int32",
            "Npc: npcID Int32, npcNAME String, npcWIDTH Single, npcHEIGHT Single, npcDIR String",
            "Props: propID Int32, propNAME String, propIMAGE String, propIMAGE_WIDTH Int32, propIMAGE_HEIGHT Int32",
            "Scene: areaID Int32, sceneID Int32, sceneNAME String, sceneIMAGE String, sceneX Single, sceneY Single, sceneZ Single, sceneWIDTH Single, sceneHEIGHT Single",
            "Script: scriptID Int32, scriptNAME String, scriptTEXT String, scriptRATE Single",
            "Terrain: areaID Int32, terrainID Int32, terrainX1 Single, terrainY1 Single, terrainX2 Single, terrainY2 Single",
        };

        /// <summary>
        /// Reads, schema-checks and cross-checks one world database export. With <paramref name="fixes"/>, the typed records
        /// are read from the rows as fixed (the export and <see cref="Database"/> stay as the original held them) and the
        /// world carries the fixes' start and exits.
        /// </summary>
        internal static WorldData Read(IContentSource source, string file, string edition, WorldKind kind, List<string> errors, WorldFixes fixes = null)
        {
            var db = MdbDatabase.Read(source, file, kind == WorldKind.Engine ? EngineSchema : RootSchema, errors);
            if (db == null) return null;
            var world = new WorldData { Edition = edition, Kind = kind, Database = db, Fixes = fixes };
            Dictionary<string, List<ContentReader>> rows;
            if (fixes != null)
            {
                rows = fixes.Apply(db, errors);
                world.Start = fixes.Start;
                world.Exits.AddRange(fixes.Exits);
            }
            else
            {
                rows = new Dictionary<string, List<ContentReader>>();
                foreach (var t in db.Tables) rows[t.Name] = t.RowReaders;
            }

            foreach (var r in Rows(rows, "Area"))
            {
                world.Areas.Add(new AreaRecord { Id = r.Integer("areaID"), Name = r.Text("areaNAME") });
                r.Done();
            }

            foreach (var r in Rows(rows, "Script"))
            {
                world.Scripts.Add(new ScriptRecord
                {
                    Id = r.Integer("scriptID"),
                    Name = r.Text("scriptNAME"),
                    Text = r.Text("scriptTEXT"),
                    Rate = r.Number("scriptRATE", 0f),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Event"))
            {
                var e = new EventRecord { AreaId = r.Integer("areaID"), Id = r.Integer("eventID"), Name = r.Text("eventNAME") };
                if (kind == WorldKind.Root)
                {
                    e.HasOrigin = true;
                    e.X = r.Number("eventX");
                    e.Y = r.Number("eventY");
                }

                e.Left = r.Number("eventLEFT");
                e.Right = r.Number("eventRIGHT");
                e.Top = r.Number("eventTOP");
                e.Bottom = r.Number("eventBOTTOM");
                e.ScriptId = r.Integer("scriptID");
                r.Done();
                world.Events.Add(e);
            }

            foreach (var r in Rows(rows, "Item_Textures"))
            {
                world.ItemTextures.Add(new ItemTextureRecord { Id = r.Integer("textureID"), Name = r.Text("textureNAME"), File = r.Text("textureFILE") });
                r.Done();
            }

            foreach (var r in Rows(rows, "Items"))
            {
                world.Items.Add(new ItemRecord
                {
                    Id = r.Integer("itemID"),
                    TextureId = r.Integer("texureID"),
                    Name = r.Text("itemName"),
                    Script = r.Text("itemScript"),
                    Effect = r.Text("itemEffect", allowEmpty: true),
                    Description = r.Text("itemDescription", allowEmpty: true),
                    Weight = r.Number("itemWeight", 0f),
                    Type = r.Text("itemType"),
                    Amount = r.Integer("itemAmount", 0),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Pickup"))
            {
                world.Pickups.Add(new PickupRecord
                {
                    AreaId = r.Integer("areaID"),
                    ItemId = r.Integer("itemID"),
                    X = r.Number("X"),
                    Y = r.Number("Y"),
                    Z = r.Number("Z"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Scene"))
            {
                world.Scenes.Add(new SceneRecord
                {
                    AreaId = r.Integer("areaID"),
                    Id = r.Integer("sceneID"),
                    Name = r.Text("sceneNAME"),
                    Image = r.Text("sceneIMAGE"),
                    X = r.Number("sceneX"),
                    Y = r.Number("sceneY"),
                    Z = r.Number("sceneZ"),
                    Width = r.Positive("sceneWIDTH"),
                    Height = r.Positive("sceneHEIGHT"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Terrain"))
            {
                world.Terrain.Add(new TerrainRecord
                {
                    AreaId = r.Integer("areaID"),
                    Id = r.Integer("terrainID"),
                    X1 = r.Number("terrainX1"),
                    Y1 = r.Number("terrainY1"),
                    X2 = r.Number("terrainX2"),
                    Y2 = r.Number("terrainY2"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Props"))
            {
                world.PropArt.Add(new PropArtRecord
                {
                    Id = r.Integer("propID"),
                    Name = r.Text("propNAME"),
                    Image = r.Text("propIMAGE"),
                    ImageWidth = r.Integer("propIMAGE_WIDTH", 1),
                    ImageHeight = r.Integer("propIMAGE_HEIGHT", 1),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Area_Props"))
            {
                world.AreaProps.Add(new AreaPropRecord
                {
                    AreaId = r.Integer("areaID"),
                    PropId = r.Integer("propID"),
                    X = r.Number("propX"),
                    Y = r.Number("propY"),
                    Z = r.Number("propZ"),
                    Width = r.Positive("propWIDTH"),
                    Height = r.Positive("propHEIGHT"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Npc"))
            {
                world.Npcs.Add(new NpcRecord
                {
                    Id = r.Integer("npcID"),
                    Name = r.Text("npcNAME"),
                    Width = r.Positive("npcWIDTH"),
                    Height = r.Positive("npcHEIGHT"),
                    Directory = r.Text("npcDIR"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Area_Npcs"))
            {
                world.AreaNpcs.Add(new AreaNpcRecord
                {
                    AreaId = r.IntegerText("areaID"),
                    NpcId = r.Integer("npcID"),
                    X = r.Number("npcX"),
                    Y = r.Number("npcY"),
                    State = r.Text("npcSTATE"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Enemy_Class")) { world.EnemyClasses.Add(r.Text("enemyCLASS")); r.Done(); }
            foreach (var r in Rows(rows, "Enemy_Motion")) { world.EnemyMotions.Add(r.Text("enemyMOTION")); r.Done(); }
            foreach (var r in Rows(rows, "Enemy_State")) { world.EnemyStates.Add(r.Text("enemySTATE")); r.Done(); }

            foreach (var r in Rows(rows, "Enemy"))
            {
                world.Enemies.Add(new EnemyRecord
                {
                    Id = r.IntegerText("enemyID"),
                    Name = r.Text("enemyNAME"),
                    MaxHealth = r.IntegerText("enemyMAXHP"),
                    MoveSpeed = r.Positive("enemyMOVE_SPD"),
                    Range = r.Positive("enemyRANGE"),
                    State = r.Text("enemySTATE"),
                    Motion = r.Text("enemyMOTION"),
                    Class = r.Text("enemyCLASS"),
                });
                r.Done();
            }

            foreach (var r in Rows(rows, "Area_Enemies"))
            {
                world.AreaEnemies.Add(new AreaEnemyRecord
                {
                    AreaId = r.Integer("areaID"),
                    EnemyId = r.Integer("enemyID"),
                    X = r.Number("enemyX"),
                    Y = r.Number("enemyY"),
                    Z = r.Number("enemyZ"),
                });
                r.Done();
            }

            // Enemy_Images is read as raw rows only (the table is empty in the original); a row would still be checked
            // against its nine columns by the unknown/missing key rules.
            foreach (var r in Rows(rows, "Enemy_Images"))
            {
                foreach (var c in db.Table("Enemy_Images").Columns) r.Text(c.Key, allowEmpty: true);
                r.Done();
            }

            world.CrossCheck(file, errors);
            return world;
        }

        static List<ContentReader> Rows(Dictionary<string, List<ContentReader>> rows, string table)
        {
            return rows.TryGetValue(table, out var list) ? list : new List<ContentReader>();
        }

        void CrossCheck(string file, List<string> errors)
        {
            UniqueIds(file, "Area", Areas, a => a.Id, errors);
            UniqueIds(file, "Script", Scripts, s => s.Id, errors);
            UniqueIds(file, "Props", PropArt, p => p.Id, errors);
            UniqueIds(file, "Npc", Npcs, n => n.Id, errors);
            UniqueIds(file, "Enemy", Enemies, e => e.Id, errors);
            UniqueIds(file, "Items", Items, i => i.Id, errors);
            UniqueIds(file, "Item_Textures", ItemTextures, t => t.Id, errors);
            UniqueIds(file, "Scene", Scenes, s => s.Id, errors);
            UniqueIds(file, "Terrain", Terrain, t => t.Id, errors);

            foreach (var e in Events)
            {
                if (FindArea(e.AreaId) == null) errors.Add(file + ": Event " + e.Id + " (" + e.Name + ") is in area " + e.AreaId + ", which does not exist");
                if (FindScript(e.ScriptId) == null) errors.Add(file + ": Event " + e.Id + " (" + e.Name + ") runs script " + e.ScriptId + ", which does not exist");
                if (e.Left > e.Right) errors.Add(file + ": Event " + e.Id + " (" + e.Name + ") has its left edge right of its right edge");
                if (e.Bottom > e.Top) errors.Add(file + ": Event " + e.Id + " (" + e.Name + ") has its bottom above its top");
            }

            foreach (var s in Scripts) CheckScript(file, "Script " + s.Id + " (" + s.Name + ")", s.Text, errors);
            foreach (var i in Items)
            {
                CheckScript(file, "Item " + i.Id + " (" + i.Name + ")", i.Script, errors);
                if (ItemTextures.Find(t => t.Id == i.TextureId) == null) errors.Add(file + ": Item " + i.Id + " (" + i.Name + ") uses texture " + i.TextureId + ", which does not exist");
            }

            foreach (var p in Pickups)
            {
                if (FindArea(p.AreaId) == null) errors.Add(file + ": a Pickup is in area " + p.AreaId + ", which does not exist");
                if (Items.Find(i => i.Id == p.ItemId) == null) errors.Add(file + ": a Pickup places item " + p.ItemId + ", which does not exist");
            }

            // The 2007 database has one scene (11) in area 0, which has no Area row; the original is kept as it was.
            foreach (var s in Scenes)
                if (FindArea(s.AreaId) == null)
                    Warnings.Add(file + ": Scene " + s.Id + " (" + s.Name + ") is in area " + s.AreaId + ", which has no Area row");
            foreach (var t in Terrain)
                if (FindArea(t.AreaId) == null) errors.Add(file + ": Terrain " + t.Id + " is in area " + t.AreaId + ", which does not exist");
            foreach (var p in AreaProps)
            {
                if (FindArea(p.AreaId) == null) errors.Add(file + ": an Area_Props row is in area " + p.AreaId + ", which does not exist");
                if (FindPropArt(p.PropId) == null) errors.Add(file + ": an Area_Props row places prop " + p.PropId + ", which does not exist");
            }

            foreach (var n in AreaNpcs)
            {
                if (FindArea(n.AreaId) == null) errors.Add(file + ": an Area_Npcs row is in area " + n.AreaId + ", which does not exist");
                if (Npcs.Find(x => x.Id == n.NpcId) == null) errors.Add(file + ": an Area_Npcs row places npc " + n.NpcId + ", which does not exist");
                if (n.State != "stand" && n.State != "pursue") errors.Add(file + ": an Area_Npcs row has the state \"" + n.State + "\" (stand or pursue)");
            }

            foreach (var e in Enemies)
            {
                if (!EnemyClasses.Contains(e.Class)) errors.Add(file + ": Enemy " + e.Id + " (" + e.Name + ") has the class \"" + e.Class + "\", not in Enemy_Class");
                if (!EnemyMotions.Contains(e.Motion)) errors.Add(file + ": Enemy " + e.Id + " (" + e.Name + ") has the motion \"" + e.Motion + "\", not in Enemy_Motion");
                if (!EnemyStates.Contains(e.State)) errors.Add(file + ": Enemy " + e.Id + " (" + e.Name + ") has the state \"" + e.State + "\", not in Enemy_State");
            }

            foreach (var a in AreaEnemies)
            {
                if (FindArea(a.AreaId) == null) errors.Add(file + ": an Area_Enemies row is in area " + a.AreaId + ", which does not exist");
                if (Enemies.Find(x => x.Id == a.EnemyId) == null) errors.Add(file + ": an Area_Enemies row places enemy " + a.EnemyId + ", which does not exist");
            }
        }

        static void CheckScript(string file, string what, string text, List<string> errors)
        {
            var problem = ScriptSyntax.Check(text);
            if (problem != null) errors.Add(file + ": " + what + " " + problem);
        }

        static void UniqueIds<T>(string file, string table, List<T> rows, System.Func<T, int> id, List<string> errors)
        {
            var seen = new HashSet<int>();
            foreach (var row in rows)
                if (!seen.Add(id(row))) errors.Add(file + ": " + table + " has the id " + id(row) + " twice");
        }
    }
}

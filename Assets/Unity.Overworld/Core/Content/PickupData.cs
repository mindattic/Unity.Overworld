using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>How a pickup is used (the original's ItemType, RECONSTRUCTION 8.1).</summary>
    public enum ItemType
    {
        SingleItem,
        GroupItem,
        Reusable,
        Equipable,
        UseOnPickup,
    }

    /// <summary>The rectangle the original scattered its random pickups over: x = startX + width * r, y = (startY - height * r) + yLift.</summary>
    public sealed class RandomArea
    {
        public float StartX { get; internal set; }
        public float StartY { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float YLift { get; internal set; }

        /// <summary>The position for two draws of a [0, 1) random number, x first, as the original drew them.</summary>
        public DataPoint Place(double drawX, double drawY) =>
            new DataPoint((float)(StartX + Width * drawX), (float)((StartY - Height * drawY) + YLift));
    }

    /// <summary>One pickup of the world: a record the original's Items.loadItem built, keyed by its index.</summary>
    public sealed class PickupDefinition
    {
        public int Index { get; internal set; }
        public string Name { get; internal set; }
        public string Texture { get; internal set; }

        /// <summary>The inventory icon when it differs from the world texture (the potato and turnip); null otherwise.</summary>
        public string InventoryTexture { get; internal set; }

        public string Script { get; internal set; }
        public string Effect { get; internal set; }
        public string Description { get; internal set; }
        public float Weight { get; internal set; }
        public ItemType Type { get; internal set; }
        public int Value { get; internal set; }

        /// <summary>Seconds until a picked-up pickup grows back; 0 means never.</summary>
        public float GrowSeconds { get; internal set; }

        /// <summary>True for a pickup placed at random over the <see cref="PickupData.RandomArea"/> (its <see cref="Position"/> is not meaningful).</summary>
        public bool IsRandom { get; internal set; }

        /// <summary>The fixed position (0 when <see cref="IsRandom"/>).</summary>
        public DataPoint Position { get; internal set; }

        /// <summary>True for a pickup the rebuild restored (the Nitro Bean), not one of the original's 372.</summary>
        public bool IsRestored { get; internal set; }
    }

    /// <summary>
    /// What is in reach of a hero (Items.checkItemsInRange, RECONSTRUCTION 8.2): a pickup's point (its position plus
    /// <see cref="ItemPoint"/> times the item size) within <see cref="Range"/> of the hero's (its position plus
    /// <see cref="HeroPoint"/> times the hero size) on both axes; at most <see cref="Listed"/> are listed.
    /// </summary>
    public sealed class ReachTuning
    {
        public float Range { get; internal set; }
        public DataPoint HeroPoint { get; internal set; }
        public DataPoint ItemPoint { get; internal set; }
        public int Listed { get; internal set; }
    }

    /// <summary>
    /// What is under the world cursor (Cursor.checkCursorOverItem): a pickup's position plus <see cref="ItemOffset"/>
    /// within <see cref="Range"/> of the cursor plus <see cref="CursorOffset"/> on both axes.
    /// </summary>
    public sealed class HoverTuning
    {
        public float Range { get; internal set; }
        public DataPoint ItemOffset { get; internal set; }
        public DataPoint CursorOffset { get; internal set; }
    }

    /// <summary>
    /// The name label over a pickup (Items.showItemName, renderText): a marker and the name, each centred at the pickup's
    /// position plus its offset, in 3D text whose glyphs are <see cref="UnitsPerPixel"/> of their font pixels times the scale.
    /// </summary>
    public sealed class PickupLabelTuning
    {
        public string Marker { get; internal set; }
        public float MarkerScale { get; internal set; }
        public DataPoint MarkerOffset { get; internal set; }
        public float NameScale { get; internal set; }
        public DataPoint NameOffset { get; internal set; }
        public float FontPoints { get; internal set; }
        public IReadOnlyList<string> Families { get; internal set; }
        public float UnitsPerPixel { get; internal set; }

        /// <summary>The label's font (Text.vb's font3d, Arial 24) for presentation.</summary>
        public HudFont Font => font ??= new HudFont { Name = "font3d", Families = Families, Points = FontPoints, Bold = false };

        HudFont font;

        /// <summary>The height of the font's em in world units at <paramref name="scale"/> (a GDI point is 96 / 72 pixels).</summary>
        public float EmHeight(float scale) => FontPoints * 96f / 72f * UnitsPerPixel * scale;
    }

    /// <summary>All pickups: the original's 372 (0 to 371) plus the restored Nitro Bean (372).</summary>
    public sealed class PickupData
    {
        internal PickupData(float itemSize, RandomArea area, List<string> textures, List<string> inventoryTextures, PickupDefinition[] pickups)
        {
            ItemSize = itemSize;
            RandomArea = area;
            Textures = textures;
            InventoryTextures = inventoryTextures;
            Pickups = pickups;
        }

        public float ItemSize { get; }
        public RandomArea RandomArea { get; }
        public IReadOnlyList<string> Textures { get; }
        public IReadOnlyList<string> InventoryTextures { get; }

        /// <summary>The folder the item pictures are in ("gfx/items/") and their extension (".PNG").</summary>
        public string ArtFolder { get; private set; }

        public string ArtExtension { get; private set; }

        public ReachTuning Reach { get; private set; }
        public HoverTuning Hover { get; private set; }
        public PickupLabelTuning Label { get; private set; }

        /// <summary>The share of maximum stamina a pickup costs a hero moving in battle (checkItemPickup's 5 percent).</summary>
        public float BattlePickupStamina { get; private set; }

        /// <summary>The picture file of an item texture name ("egg" is "gfx/items/egg.PNG").</summary>
        public string File(string texture) => texture == null ? null : ArtFolder + texture + ArtExtension;

        /// <summary>The picture a pickup is drawn with in the world (and in the examine box).</summary>
        public string WorldFile(PickupDefinition pickup) => File(pickup.Texture);

        /// <summary>The picture a pickup shows in the inventory: its own icon when it has one (potato, turnip), else its world picture.</summary>
        public string InventoryFile(PickupDefinition pickup) => File(pickup.InventoryTexture ?? pickup.Texture);

        /// <summary>Indexed by pickup index; no gaps.</summary>
        public IReadOnlyList<PickupDefinition> Pickups { get; }

        /// <summary>The original's pickup count (indices 0 to 371).</summary>
        public const int OriginalCount = 372;

        public int OriginalPickupCount
        {
            get
            {
                var n = 0;
                foreach (var p in Pickups) if (!p.IsRestored) n++;
                return n;
            }
        }

        /// <summary>
        /// Every pickup's position: fixed ones as authored, random ones drawn from <paramref name="draw"/> (x then y, in
        /// index order), so a seeded generator gives a reproducible scatter.
        /// </summary>
        public DataPoint[] ResolvePositions(Func<double> draw)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            var result = new DataPoint[Pickups.Count];
            for (var i = 0; i < Pickups.Count; i++)
            {
                if (!Pickups[i].IsRandom)
                {
                    result[i] = Pickups[i].Position;
                    continue;
                }

                var x = draw();
                var y = draw();
                result[i] = RandomArea.Place(x, y);
            }

            return result;
        }

        internal static PickupData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;

            var itemSize = root.Positive("itemSize");
            var areaReader = root.Section("randomArea");
            var area = new RandomArea
            {
                StartX = areaReader.Number("startX"),
                StartY = areaReader.Number("startY"),
                Width = areaReader.Positive("width"),
                Height = areaReader.Positive("height"),
                YLift = areaReader.Number("yLift"),
            };
            areaReader.Done();
            var textures = root.Texts("textures");
            var inventoryTextures = root.Texts("inventoryTextures");

            var art = root.Section("art");
            var artFolder = art.Text("folder");
            var artExtension = art.Text("extension");
            art.Check(artFolder.StartsWith("gfx/", StringComparison.Ordinal) && artFolder.EndsWith("/", StringComparison.Ordinal), "folder", "must be a folder under gfx/ ending in /");
            art.Check(artExtension.StartsWith(".", StringComparison.Ordinal), "extension", "must start with a dot");
            art.Done();

            var reachReader = root.Section("reach");
            var reach = new ReachTuning
            {
                Range = reachReader.Positive("range"),
                HeroPoint = reachReader.Pair("heroPoint", -1f, 1f),
                ItemPoint = reachReader.Pair("itemPoint", -1f, 1f),
                Listed = reachReader.Integer("listed", 1, 1000),
            };
            reachReader.Done();

            var hoverReader = root.Section("hover");
            var hover = new HoverTuning
            {
                Range = hoverReader.Positive("range"),
                ItemOffset = hoverReader.Pair("itemOffset", -1f, 1f),
                CursorOffset = hoverReader.Pair("cursorOffset", -1f, 1f),
            };
            hoverReader.Done();

            var labelReader = root.Section("label");
            var label = new PickupLabelTuning
            {
                Marker = labelReader.Text("marker"),
                MarkerScale = labelReader.Positive("markerScale"),
                MarkerOffset = labelReader.Pair("markerOffset", -1f, 1f),
                NameScale = labelReader.Positive("nameScale"),
                NameOffset = labelReader.Pair("nameOffset", -1f, 1f),
                FontPoints = labelReader.Positive("fontPoints"),
                Families = labelReader.Texts("families"),
                UnitsPerPixel = labelReader.Positive("unitsPerPixel"),
            };
            labelReader.Check(label.Families.Count > 0, "families", "must name at least one face");
            labelReader.Done();

            var battleStamina = root.Number("battlePickupStamina", 0f, 1f);

            var byIndex = new SortedDictionary<int, PickupDefinition>();
            foreach (var r in root.Objects("pickups"))
            {
                var from = r.Integer("from", 0);
                var to = r.Has("to") ? r.Integer("to", from) : from;
                var restored = r.Flag("restored", false);
                var random = r.Flag("random", false);
                var hasAt = r.Has("at");
                var hasGrid = r.Has("grid");
                r.Check((hasAt ? 1 : 0) + (hasGrid ? 1 : 0) + (random ? 1 : 0) == 1, "from", "needs exactly one of at, grid or random");

                var name = r.Text("name");
                var texture = r.Text("texture");
                var inventoryTexture = r.Has("inventoryTexture") ? r.Text("inventoryTexture") : null;
                var script = r.Text("script");
                var effect = r.Text("effect");
                var description = r.Text("description");
                var weight = r.Number("weight", 0f);
                var type = r.Choice<ItemType>("type");
                var value = r.Integer("value", 0);
                var grow = r.Number("growSeconds", 0f);
                var at = hasAt ? r.Pair("at") : default;
                float gx = 0, gy = 0, gdx = 0, gdy = 0;
                var perRow = 1;
                if (hasGrid)
                {
                    var g = r.Section("grid");
                    gx = g.Number("x");
                    gy = g.Number("y");
                    gdx = g.Number("dx");
                    gdy = g.Number("dy");
                    perRow = g.Integer("perRow", 1);
                    g.Done();
                }

                if (!textures.Contains(texture)) r.Fail("texture", "\"" + texture + "\" is not in the textures list");
                if (inventoryTexture != null && !inventoryTextures.Contains(inventoryTexture)) r.Fail("inventoryTexture", "\"" + inventoryTexture + "\" is not in the inventoryTextures list");
                var problem = ScriptSyntax.Check(script);
                if (problem != null) r.Fail("script", problem);
                r.Done();

                for (var index = from; index <= to; index++)
                {
                    var position = at;
                    if (hasGrid)
                    {
                        var n = index - from;
                        position = new DataPoint(gx + gdx * (n % perRow), gy + gdy * (n / perRow));
                    }

                    var def = new PickupDefinition
                    {
                        Index = index,
                        Name = name,
                        Texture = texture,
                        InventoryTexture = inventoryTexture,
                        Script = script,
                        Effect = effect,
                        Description = description,
                        Weight = weight,
                        Type = type,
                        Value = value,
                        GrowSeconds = grow,
                        IsRandom = random,
                        Position = random ? default : position,
                        IsRestored = restored,
                    };
                    if (byIndex.ContainsKey(index)) r.Fail("from", "pickup index " + index + " is defined twice");
                    else byIndex[index] = def;
                }
            }

            // Indices must run 0..n with no gaps: the index is the key.
            var list = new List<PickupDefinition>();
            var expected = 0;
            foreach (var pair in byIndex)
            {
                if (pair.Key != expected)
                {
                    errors.Add(file + ".pickups: indices must run without gaps (expected " + expected + ", found " + pair.Key + ")");
                    expected = pair.Key;
                }

                list.Add(pair.Value);
                expected++;
            }

            root.Done();
            return new PickupData(itemSize, area, textures, inventoryTextures, list.ToArray())
            {
                ArtFolder = artFolder,
                ArtExtension = artExtension,
                Reach = reach,
                Hover = hover,
                Label = label,
                BattlePickupStamina = battleStamina,
            };
        }
    }
}

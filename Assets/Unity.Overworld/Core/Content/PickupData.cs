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
            return new PickupData(itemSize, area, textures, inventoryTextures, list.ToArray());
        }
    }
}

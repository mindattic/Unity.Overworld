using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// A world as data: any number of named areas, each a rectangular tile grid. It is deliberately
    /// general (area count, sizes, tile ids are all data) so the original compiled single area and the
    /// several areas of the 2007 root world.mdb load through the same type.
    /// </summary>
    public sealed class WorldDefinition
    {
        readonly List<AreaDefinition> areas = new List<AreaDefinition>();

        public WorldDefinition(string name, IEnumerable<AreaDefinition> areas)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            if (areas == null) throw new ArgumentNullException(nameof(areas));
            foreach (var a in areas)
            {
                if (a == null) throw new ArgumentException("An area is null.", nameof(areas));
                if (Find(a.Id) != null) throw new ArgumentException("Duplicate area id " + a.Id + ".", nameof(areas));
                this.areas.Add(a);
            }
        }

        public string Name { get; }
        public IReadOnlyList<AreaDefinition> Areas => areas;

        public AreaDefinition Find(int id)
        {
            for (int i = 0; i < areas.Count; i++)
                if (areas[i].Id == id) return areas[i];
            return null;
        }
    }

    /// <summary>One area: an id, a name and a width x height grid of tile ids (row-major, validated on construction).</summary>
    public sealed class AreaDefinition
    {
        readonly int[] tiles;

        public AreaDefinition(int id, string name, int width, int height, int[] tiles)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (tiles.Length != width * height)
                throw new ArgumentException("Expected " + width * height + " tiles for " + width + " x " + height + ", got " + tiles.Length + ".", nameof(tiles));
            Id = id;
            Name = name ?? "";
            Width = width;
            Height = height;
            this.tiles = (int[])tiles.Clone();
        }

        public int Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int TileAt(int x, int y)
        {
            if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Tile " + x + "," + y + " is outside the area.");
            return tiles[y * Width + x];
        }
    }
}

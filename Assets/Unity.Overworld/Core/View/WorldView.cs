using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>What a depth-sorted object is (Objects.checkDrawOrder's three groups, in its list order).</summary>
    public enum DrawableKind
    {
        Hero,
        PropBottom,
        Weapon,

        /// <summary>The tent: drawn on layer 1 (renderTent, after the tiles and before the pickups), not depth sorted.</summary>
        Tent,
    }

    /// <summary>
    /// One quad the world draws: its kind and index within that kind, its position, and its size. The quad spans x to
    /// x + width and y - halfHeight to y + halfHeight at depth z (the original's setupVertex), showing <see cref="File"/>.
    /// </summary>
    public readonly struct Drawable
    {
        public Drawable(DrawableKind kind, int index, float x, float y, float z, float width, float halfHeight, string file)
        {
            Kind = kind;
            Index = index;
            X = x;
            Y = y;
            Z = z;
            Width = width;
            HalfHeight = halfHeight;
            File = file;
        }

        public DrawableKind Kind { get; }
        public int Index { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Width { get; }
        public float HalfHeight { get; }
        public string File { get; }

        public override string ToString() => Kind + " " + Index + " at (" + X + ", " + Y + ", " + Z + ")";
    }

    /// <summary>Where a hero stands and which sprite it shows (its weapon follows it).</summary>
    public readonly struct HeroPose
    {
        public HeroPose(float x, float y, float z, string sprite, string weaponSprite)
        {
            X = x;
            Y = y;
            Z = z;
            Sprite = sprite;
            WeaponSprite = weaponSprite;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public string Sprite { get; }
        public string WeaponSprite { get; }
    }

    /// <summary>
    /// The world as the original drew it (RECONSTRUCTION 2.4 and 3.1 to 3.4): the area tiles, the camera that culls
    /// them, and the heroes, prop bottoms and weapons drawn back to front by Y. Pure rules: presentation asks it what is
    /// visible and in which order, and draws exactly that.
    /// </summary>
    public sealed class WorldView
    {
        /// <summary>
        /// The view of the world through <paramref name="camera"/> (a player's <see cref="Viewpoint"/> camera), or through
        /// a camera of its own at the start position when none is given.
        /// </summary>
        public WorldView(ViewData view, PropData props, HeroData heroes, WorldCamera camera = null)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Props = props ?? throw new ArgumentNullException(nameof(props));
            Heroes = heroes ?? throw new ArgumentNullException(nameof(heroes));
            Camera = camera ?? new WorldCamera(view.Camera);
        }

        public ViewData View { get; }
        public PropData Props { get; }
        public HeroData Heroes { get; }
        public WorldCamera Camera { get; }

        public AreaGridDefinition Grid => View.Grid;

        /// <summary>The hero quad's width (heroSize, 0.144); heroes and weapons are this square.</summary>
        public float HeroSize => Props.HeroSize;

        /// <summary>The background's culling (renderBackground): a tile is drawn when its slot is within range on both axes.</summary>
        public bool IsVisible(AreaTileDefinition tile) => Camera.InRange(tile.X, tile.Y);

        /// <summary>The objects' culling (renderObjects' inRange).</summary>
        public bool IsVisible(in Drawable drawable) => Camera.InRange(drawable.X, drawable.Y);

        /// <summary>
        /// The tent (Tent.loadTent / renderTent, RECONSTRUCTION 3.6): a quad as wide and as high as its size at its place on
        /// the ground, drawn after the tiles and before the pickups when within the render range on x and y.
        /// </summary>
        public Drawable Tent
        {
            get
            {
                var t = Props.Tent;
                return new Drawable(DrawableKind.Tent, 0, t.X, t.Y, 0f, t.Size, t.Size / 2f, View.TextureFile(t.Texture));
            }
        }

        /// <summary>
        /// The weather's and clouds' culling (renderRain, renderCloud): within the render range of the camera on x, y and z.
        /// </summary>
        public bool IsVisible(float x, float y, float z) =>
            Camera.InRange(x, y) && WorldCamera.Near(z, Camera.Z, Camera.RenderRange);

        /// <summary>Every hero at its start, standing facing Right with its weapon idle (Heroes.loadHero).</summary>
        public HeroPose[] StartPoses()
        {
            var poses = new HeroPose[Heroes.Heroes.Count];
            for (var i = 0; i < poses.Length; i++)
            {
                var h = Heroes.Heroes[i];
                poses[i] = new HeroPose(h.Start.X, h.Start.Y, 0f, View.HeroStandRight, View.WeaponIdle);
            }

            return poses;
        }

        /// <summary>
        /// Every hero of <paramref name="party"/> where it is and with the picture it shows, its weapon's frame over it
        /// (texBlank unless it is drawing or in its battle stance);
        /// <paramref name="alpha"/> (0 to 1, how far through the next tick) places it between its last two ticks.
        /// </summary>
        public HeroPose[] Poses(Party party, float alpha = 1f)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (alpha < 0f) alpha = 0f;
            if (alpha > 1f) alpha = 1f;
            var poses = new HeroPose[party.Heroes.Count];
            for (var i = 0; i < poses.Length; i++)
            {
                var h = party.Heroes[i];
                var x = h.PreviousX + (h.X - h.PreviousX) * alpha;
                var y = h.PreviousY + (h.Y - h.PreviousY) * alpha;
                var z = h.PreviousZ + (h.Z - h.PreviousZ) * alpha;
                poses[i] = new HeroPose(x, y, z, h.Sprite, h.WeaponSprite ?? View.WeaponIdle);
            }

            return poses;
        }

        /// <summary>
        /// checkDrawOrder's list, before sorting: every hero, every prop bottom, then every hero's weapon at its hero's
        /// position.
        /// </summary>
        public List<Drawable> Objects(IReadOnlyList<HeroPose> heroes)
        {
            if (heroes == null) throw new ArgumentNullException(nameof(heroes));
            var list = new List<Drawable>(heroes.Count * 2 + Props.Props.Count);
            var half = HeroSize / 2f;
            for (var i = 0; i < heroes.Count; i++)
                list.Add(new Drawable(DrawableKind.Hero, i, heroes[i].X, heroes[i].Y, heroes[i].Z, HeroSize, half, heroes[i].Sprite));
            for (var i = 0; i < Props.Props.Count; i++)
            {
                var p = Props.Props[i];
                list.Add(new Drawable(DrawableKind.PropBottom, i, p.X, p.Y, p.Z, p.Width, p.BottomHeight, View.TextureFile(p.BottomTexture)));
            }

            // The weapon's Z is never set in the original (always 0); it does not follow a falling hero.
            for (var i = 0; i < heroes.Count; i++)
                list.Add(new Drawable(DrawableKind.Weapon, i, heroes[i].X, heroes[i].Y, 0f, HeroSize, half, heroes[i].WeaponSprite));
            return list;
        }

        /// <summary>The objects in the order they are drawn (back to front by Y), every one of them, culled or not.</summary>
        public List<Drawable> BackToFront(IReadOnlyList<HeroPose> heroes)
        {
            var objects = Objects(heroes);
            var ys = new float[objects.Count];
            for (var i = 0; i < ys.Length; i++) ys[i] = objects[i].Y;
            var order = DrawOrder.BackToFront(ys);
            var sorted = new List<Drawable>(objects.Count);
            foreach (var i in order) sorted.Add(objects[i]);
            return sorted;
        }
    }

    /// <summary>The original's draw-order sort (Objects.checkDrawOrder).</summary>
    public static class DrawOrder
    {
        /// <summary>
        /// The indexes of <paramref name="ys"/> from the largest Y (farthest up the screen, drawn first) to the smallest.
        /// The original sorted the Ys, reversed them, then gave each object, in list order, the first unused place with
        /// its Y: equal Ys therefore keep their list order (a hero before its own weapon).
        /// </summary>
        public static int[] BackToFront(IReadOnlyList<float> ys)
        {
            if (ys == null) throw new ArgumentNullException(nameof(ys));
            var order = new int[ys.Count];
            for (var i = 0; i < order.Length; i++) order[i] = i;

            // A stable insertion sort, descending: n is ten in the original, and stability is the rule.
            for (var i = 1; i < order.Length; i++)
            {
                var current = order[i];
                var j = i - 1;
                while (j >= 0 && ys[order[j]] < ys[current])
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = current;
            }

            return order;
        }
    }
}

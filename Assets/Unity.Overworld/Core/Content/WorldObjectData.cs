using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>One wall: a segment heroes cannot cross, with the slope and intercepts the original derived (Walls.loadwall).</summary>
    public sealed class WallDefinition
    {
        public float X1 { get; internal set; }
        public float Y1 { get; internal set; }
        public float X2 { get; internal set; }
        public float Y2 { get; internal set; }

        /// <summary>rise / run in single precision; a vertical wall has an infinite slope, as the original's did.</summary>
        public float Slope { get; internal set; }

        /// <summary>Y1 - slope * X1.</summary>
        public float YIntercept { get; internal set; }

        /// <summary>The original's "Xintercept" is Y1 + slope * X1 (not a true X intercept); kept as it was computed.</summary>
        public float XIntercept { get; internal set; }

        public bool IsVertical => X1 == X2;
    }

    /// <summary>
    /// How the walls push heroes back (Heroes.checkWall, horizontalWall, angledWall, verticalWall; RECONSTRUCTION 3.5),
    /// walls.json "collision".
    /// </summary>
    public sealed class WallCollisionTuning
    {
        /// <summary>The hero's test point, as fractions of heroSize from its position (1/2 across, 1/2 down).</summary>
        public DataPoint Probe { get; internal set; }

        /// <summary>Added to the test point's y after the probe (0.01).</summary>
        public float ProbeLift { get; internal set; }

        /// <summary>A wall is touched within this many times the hero's velocity on the axis it blocks (2).</summary>
        public float Reach { get; internal set; }

        /// <summary>The velocity a touched wall leaves on its axis, away from the wall (0.001).</summary>
        public float Push { get; internal set; }

        /// <summary>Within this of an angled wall's line the hero's x velocity is zeroed (0.03).</summary>
        public float AngledStop { get; internal set; }

        internal static WallCollisionTuning Read(ContentReader r)
        {
            var t = new WallCollisionTuning
            {
                Probe = r.Pair("probe", -1f, 1f),
                ProbeLift = r.Number("probeLift"),
                Reach = r.Positive("reach"),
                Push = r.Positive("push"),
                AngledStop = r.Number("angledStop", 0f),
            };
            r.Done();
            return t;
        }
    }

    /// <summary>The six wall segments and how they push heroes back (RECONSTRUCTION 3.5).</summary>
    public sealed class WallData
    {
        internal WallData(List<WallDefinition> walls, WallCollisionTuning collision)
        {
            Walls = walls;
            Collision = collision;
        }

        public IReadOnlyList<WallDefinition> Walls { get; }
        public WallCollisionTuning Collision { get; }

        internal static WallData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var list = new List<WallDefinition>();
            foreach (var r in root.Objects("walls"))
            {
                var from = r.Pair("from");
                var to = r.Pair("to");
                var rise = to.Y - from.Y;
                var run = to.X - from.X;
                r.Check(!(rise == 0f && run == 0f), "to", "must differ from 'from'");
                var slope = rise / run;
                list.Add(new WallDefinition
                {
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = to.X,
                    Y2 = to.Y,
                    Slope = slope,
                    YIntercept = from.Y - slope * from.X,
                    XIntercept = from.Y + slope * from.X,
                });
                r.Done();
            }

            if (list.Count == 0) errors.Add(file + ".walls: there must be at least one wall");
            var collision = WallCollisionTuning.Read(root.Section("collision"));
            root.Done();
            return new WallData(list, collision);
        }
    }

    /// <summary>A prop: a bottom quad (the trunk) and a top quad (the canopy) at a world position (Prop.loadProp).</summary>
    public sealed class PropDefinition
    {
        public int Id { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }

        /// <summary>The height of the bottom quad (the hero size / 2).</summary>
        public float BottomHeight { get; internal set; }

        public string BottomTexture { get; internal set; }
        public string TopTexture { get; internal set; }

        /// <summary>The top quad sits at x - width, y + height + heroSize.</summary>
        public float TopX { get; internal set; }

        public float TopY { get; internal set; }
    }

    /// <summary>The tent (Tent.loadTent).</summary>
    public sealed class TentDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Size { get; internal set; }
        public string Texture { get; internal set; }
    }

    /// <summary>The four props and the tent (RECONSTRUCTION 3.4 and 3.6).</summary>
    public sealed class PropData
    {
        internal PropData(float heroSize, List<string> textures, List<PropDefinition> props, TentDefinition tent)
        {
            HeroSize = heroSize;
            Textures = textures;
            Props = props;
            Tent = tent;
        }

        public float HeroSize { get; }
        public IReadOnlyList<string> Textures { get; }
        public IReadOnlyList<PropDefinition> Props { get; }
        public TentDefinition Tent { get; }

        internal static PropData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var heroSize = root.Positive("heroSize");
            var textures = root.Texts("textures");
            var props = new List<PropDefinition>();
            foreach (var r in root.Objects("props"))
            {
                var at = r.Pair("at");
                var p = new PropDefinition
                {
                    Id = r.Integer("id", 0),
                    X = at.X,
                    Y = at.Y,
                    Z = r.Number("z"),
                    Width = r.Positive("width"),
                    Height = r.Positive("height"),
                    BottomHeight = r.Positive("bottomHeight"),
                    BottomTexture = r.Text("bottomTexture"),
                    TopTexture = r.Text("topTexture"),
                };
                p.TopX = p.X - p.Width;
                p.TopY = p.Y + p.Height + heroSize;
                r.Check(p.Id == props.Count, "id", "must be " + props.Count + ", the prop's place in the list (is " + p.Id + ")");
                if (!textures.Contains(p.BottomTexture)) r.Fail("bottomTexture", "\"" + p.BottomTexture + "\" is not in the textures list");
                if (!textures.Contains(p.TopTexture)) r.Fail("topTexture", "\"" + p.TopTexture + "\" is not in the textures list");
                r.Done();
                props.Add(p);
            }

            var tentReader = root.Section("tent");
            var tentAt = tentReader.Pair("at");
            var tent = new TentDefinition
            {
                X = tentAt.X,
                Y = tentAt.Y,
                Size = tentReader.Positive("size"),
                Texture = tentReader.Text("texture"),
            };
            if (!textures.Contains(tent.Texture)) tentReader.Fail("texture", "\"" + tent.Texture + "\" is not in the textures list");
            tentReader.Done();
            root.Done();
            return new PropData(heroSize, textures, props, tent);
        }
    }

    /// <summary>One book: a title (the directory name) and its pages, verbatim.</summary>
    public sealed class BookDefinition
    {
        public string Title { get; internal set; }
        public IReadOnlyList<string> Pages { get; internal set; }

        /// <summary>Whitespace-separated words over all pages joined together (RECONSTRUCTION 11's count, taken with wc -w).</summary>
        public int WordCount
        {
            get
            {
                var all = string.Concat(Pages);
                return all.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            }
        }
    }

    /// <summary>The five books, in the folder order the original listed them (the N key's order).</summary>
    public sealed class BookData
    {
        internal BookData(List<BookDefinition> books)
        {
            Books = books;
        }

        public IReadOnlyList<BookDefinition> Books { get; }

        public BookDefinition Find(string title)
        {
            foreach (var b in Books) if (b.Title == title) return b;
            return null;
        }

        internal static BookData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var list = new List<BookDefinition>();
            foreach (var r in root.Objects("books"))
            {
                var title = r.Text("title");
                var pages = r.Texts("pages");
                if (pages.Count == 0) r.Fail("pages", "a book needs at least one page");
                if (list.Exists(b => b.Title == title)) r.Fail("title", "\"" + title + "\" is defined twice");
                r.Done();
                list.Add(new BookDefinition { Title = title, Pages = pages });
            }

            root.Done();
            return new BookData(list);
        }
    }
}

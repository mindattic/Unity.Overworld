using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>One area tile slot that has a picture: its name ("A1"), grid place and file.</summary>
    public sealed class AreaTileDefinition
    {
        /// <summary>The letter and number, as the original computed the file name ("A1").</summary>
        public string Name { get; internal set; }

        /// <summary>0-based letter index: A is 0, the top row.</summary>
        public int Row { get; internal set; }

        /// <summary>1-based number: 1 is the left column.</summary>
        public int Column { get; internal set; }

        /// <summary>The picture, a path under the original's folder exactly as it is on disk ("gfx/area/A3.PNG").</summary>
        public string File { get; internal set; }

        /// <summary>The slot position (the quad's left edge and vertical centre).</summary>
        public float X { get; internal set; }

        public float Y { get; internal set; }
    }

    /// <summary>The area tile grid (Background.loadBackground, RECONSTRUCTION 3.1).</summary>
    public sealed class AreaGridDefinition
    {
        public int Rows { get; internal set; }
        public int Columns { get; internal set; }
        public float StartX { get; internal set; }
        public float StartY { get; internal set; }

        /// <summary>A tile's width; its quad spans x 0 to size and y -size/2 to +size/2 from the slot.</summary>
        public float TileSize { get; internal set; }

        /// <summary>The slots that have a picture, in file order (A1, A2, ... F8).</summary>
        public IReadOnlyList<AreaTileDefinition> Tiles { get; internal set; }

        public AreaTileDefinition Find(string name)
        {
            foreach (var t in Tiles) if (t.Name == name) return t;
            return null;
        }

        /// <summary>Where the original put slot (row, column): x = startX + (column - 1), y = startY + 1 - row.</summary>
        public static void SlotPosition(float startX, float startY, int row, int column, out float x, out float y)
        {
            x = startX + (column - 1);
            y = startY + 1 - row;
        }

        /// <summary>Parses "A1" .. "K11" into a 0-based row and 1-based column; false when it is not a slot name.</summary>
        public static bool TryParseName(string name, out int row, out int column)
        {
            row = -1;
            column = 0;
            if (string.IsNullOrEmpty(name) || name.Length < 2) return false;
            var letter = name[0];
            if (letter < 'A' || letter > 'Z') return false;
            for (var i = 1; i < name.Length; i++)
                if (name[i] < '0' || name[i] > '9') return false;
            if (name[1] == '0') return false;
            if (!int.TryParse(name.Substring(1), out column)) return false;
            row = letter - 'A';
            return true;
        }
    }

    /// <summary>The perspective camera (Camera.initCamera, RECONSTRUCTION 3.2).</summary>
    public sealed class CameraDefinition
    {
        public float StartX { get; internal set; }
        public float StartY { get; internal set; }
        public float StartZ { get; internal set; }

        /// <summary>Vertical field of view in degrees.</summary>
        public float FieldOfView { get; internal set; }

        /// <summary>The original's fixed aspect (4 / 3): the base the view is designed for.</summary>
        public float Aspect { get; internal set; }

        public float Near { get; internal set; }
        public float Far { get; internal set; }

        /// <summary>The zoom limit closest to the world (-1).</summary>
        public float NearestZ { get; internal set; }

        /// <summary>The zoom limit farthest from the world (-3.5).</summary>
        public float FarthestZ { get; internal set; }

        /// <summary>renderRange = base + |cam.Z|.</summary>
        public float RenderRangeBase { get; internal set; }
    }

    /// <summary>The pictures the world cursor shows (Textures.vb's cursor_*; RECONSTRUCTION 5.3).</summary>
    public enum CursorSprite
    {
        Arrow,
        ScrollLeft,
        ScrollRight,
        ScrollUp,
        ScrollDown,
        Zoom,
        Eye,
    }

    /// <summary>
    /// The world cursor (Cursor/Cursor.vb, Input.getMouse; RECONSTRUCTION 5.3): a quad on the ground moved by the mouse's
    /// relative motion, the virtual screen mouse that decides the edges, and its pictures.
    /// </summary>
    public sealed class CursorDefinition
    {
        /// <summary>The quad's width (cursorSize, 0.05); it spans y +/- half that.</summary>
        public float Size { get; internal set; }

        /// <summary>Where the quad is drawn from the cursor's position (+0.02, -0.042).</summary>
        public DataPoint DrawOffset { get; internal set; }

        /// <summary>World units the cursor moves per pixel of mouse motion (0.00172: one pixel at zoom -1).</summary>
        public float WorldPerPixel { get; internal set; }

        /// <summary>The virtual mouse's screen (the original's 640 x 480 window).</summary>
        public float ScreenWidth { get; internal set; }

        public float ScreenHeight { get; internal set; }

        /// <summary>The virtual mouse is held this many pixels in from every edge, and scrolls when it is there (32).</summary>
        public float EdgeMargin { get; internal set; }

        /// <summary>Where checkCursorEdge puts the cursor, from the camera, at each edge.</summary>
        public float EdgeLeft { get; internal set; }

        public float EdgeRight { get; internal set; }
        public float EdgeUp { get; internal set; }
        public float EdgeDown { get; internal set; }

        /// <summary>The picture file of each cursor sprite.</summary>
        public IReadOnlyDictionary<CursorSprite, string> Sprites { get; internal set; }

        public string File(CursorSprite sprite) => Sprites.TryGetValue(sprite, out var f) ? f : null;
    }

    /// <summary>
    /// How the camera is moved (Camera.vb, Cursor.vb checkScroll/checkHeroEdge, Input.getKeyboard/getMouse/mouseZoom;
    /// RECONSTRUCTION 3.2 and 5.3). Offsets are from the leader's position.
    /// </summary>
    public sealed class CameraControlDefinition
    {
        /// <summary>camSPD (0.02): the edge-scroll step, and the base of the centring and drag-zoom rates.</summary>
        public float Speed { get; internal set; }

        /// <summary>Edge scrolling with Shift held is this many times as fast (3).</summary>
        public float FastFactor { get; internal set; }

        /// <summary>One wheel step zooms this far (0.15).</summary>
        public float WheelStep { get; internal set; }

        /// <summary>Drag zoom: Z changes by rate x (centre y - mouse y) per tick (camSPD / 10).</summary>
        public float DragRate { get; internal set; }

        /// <summary>Drag zoom ignores the mouse within this many pixels of the centre line (5).</summary>
        public float DragDeadZone { get; internal set; }

        /// <summary>Centring moves Speed x this x the remaining distance each tick (8).</summary>
        public float CentreRate { get; internal set; }

        /// <summary>The point centring heads for, from the leader: heroSize x (1/2, -1/2) plus this (0, +0.03).</summary>
        public DataPoint CentreOffset { get; internal set; }

        /// <summary>Centring ends within <see cref="CentreArriveRange"/> of the centre point plus this (+0.02, 0).</summary>
        public DataPoint CentreArriveOffset { get; internal set; }

        public float CentreArriveRange { get; internal set; }

        /// <summary>Edge scrolling stops, and the camera is held, this far from the leader (-0.45, +0.6, -0.4, +0.35).</summary>
        public float ScrollLeft { get; internal set; }

        public float ScrollRight { get; internal set; }
        public float ScrollDown { get; internal set; }
        public float ScrollUp { get; internal set; }

        /// <summary>Beyond these offsets the camera is carried by the leader's velocity (-0.3, +0.45, -0.3, +0.25).</summary>
        public float CarryLeft { get; internal set; }

        public float CarryRight { get; internal set; }
        public float CarryDown { get; internal set; }
        public float CarryUp { get; internal set; }

        /// <summary>Numpad 4/6 and 8/2 move the camera this far per tick (0.05, 0.02).</summary>
        public DataPoint Strafe { get; internal set; }

        /// <summary>X and Z move the camera this far on both axes per tick (0.25).</summary>
        public float Isometric { get; internal set; }
    }

    /// <summary>The box drawn behind everything and the colour each frame is cleared to (Cube.vb, Engine.oneFrameRender).</summary>
    public sealed class BackdropDefinition
    {
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float Depth { get; internal set; }
        public string Texture { get; internal set; }

        /// <summary>Red, green, blue, alpha, 0 to 255.</summary>
        public IReadOnlyList<byte> ClearColor { get; internal set; }
    }

    /// <summary>
    /// How the world is drawn: the tile grid, the camera, the backdrop, the sprites of the depth-sorted objects and the
    /// files behind the props' texture names (view.json).
    /// </summary>
    public sealed class ViewData
    {
        internal ViewData()
        {
        }

        public AreaGridDefinition Grid { get; private set; }
        public CameraDefinition Camera { get; private set; }
        public BackdropDefinition Backdrop { get; private set; }
        public CursorDefinition Cursor { get; private set; }
        public CameraControlDefinition Control { get; private set; }

        /// <summary>A standing hero facing Right (every hero's start pose).</summary>
        public string HeroStandRight { get; private set; }

        /// <summary>What a weapon shows when its hero is neither drawing nor in battle (the original's texBlank).</summary>
        public string WeaponIdle { get; private set; }

        /// <summary>The file behind each texture name the props use.</summary>
        public IReadOnlyDictionary<string, string> Textures { get; private set; }

        /// <summary>The file of a named texture, or null.</summary>
        public string TextureFile(string name) => name != null && Textures.TryGetValue(name, out var file) ? file : null;

        /// <summary>Every picture file the view draws, once each.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var list = new List<string>();
                foreach (var t in Grid.Tiles) list.Add(t.File);
                list.Add(Backdrop.Texture);
                list.Add(HeroStandRight);
                list.Add(WeaponIdle);
                foreach (var f in Textures.Values) if (!list.Contains(f)) list.Add(f);
                foreach (var f in Cursor.Sprites.Values) if (!list.Contains(f)) list.Add(f);
                return list;
            }
        }

        internal static ViewData Read(IContentSource source, string file, PropData props, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var view = new ViewData();

            view.Grid = ReadGrid(root.Section("grid"));
            view.Camera = ReadCamera(root.Section("camera"));
            view.Backdrop = ReadBackdrop(root.Section("backdrop"));
            view.Cursor = ReadCursor(root.Section("cursor"));
            view.Control = ReadControl(root.Section("cameraControl"));

            var objects = root.Section("objects");
            view.HeroStandRight = PicturePath(objects, "heroStandRight");
            view.WeaponIdle = PicturePath(objects, "weaponIdle");
            objects.Done();

            var textures = new Dictionary<string, string>();
            foreach (var r in root.Objects("textures"))
            {
                var name = r.Text("name");
                var path = PicturePath(r, "file");
                if (textures.ContainsKey(name)) r.Fail("name", "\"" + name + "\" is listed twice");
                else textures[name] = path;
                r.Done();
            }

            view.Textures = textures;
            if (props != null)
            {
                foreach (var name in props.Textures)
                    if (!textures.ContainsKey(name))
                        root.Fail("textures", "has no file for the props' texture \"" + name + "\"");
            }

            root.Done();
            return view;
        }

        static AreaGridDefinition ReadGrid(ContentReader r)
        {
            var grid = new AreaGridDefinition
            {
                Rows = r.Integer("rows", 1, 26),
                Columns = r.Integer("columns", 1, 99),
                StartX = r.Number("startX"),
                StartY = r.Number("startY"),
                TileSize = r.Positive("tileSize"),
            };

            var tiles = new List<AreaTileDefinition>();
            foreach (var t in r.Objects("tiles"))
            {
                var name = t.Text("name");
                var path = PicturePath(t, "file");
                if (!AreaGridDefinition.TryParseName(name, out var row, out var column))
                    t.Fail("name", "must be a letter and a number, as \"A1\" (is \"" + name + "\")");
                else if (row >= grid.Rows || column > grid.Columns)
                    t.Fail("name", "\"" + name + "\" is outside the " + grid.Rows + " x " + grid.Columns + " grid");
                else if (tiles.Exists(x => x.Name == name))
                    t.Fail("name", "\"" + name + "\" is listed twice");
                else
                {
                    // The original computed the file name from the slot; the file must be that name, in any letter case.
                    var fileName = path.Substring(path.LastIndexOf('/') + 1);
                    var dot = fileName.LastIndexOf('.');
                    var stem = dot < 0 ? fileName : fileName.Substring(0, dot);
                    if (!string.Equals(stem, name, StringComparison.OrdinalIgnoreCase))
                        t.Fail("file", "must be the picture named after its slot \"" + name + "\" (is \"" + path + "\")");
                    AreaGridDefinition.SlotPosition(grid.StartX, grid.StartY, row, column, out var x, out var y);
                    tiles.Add(new AreaTileDefinition { Name = name, Row = row, Column = column, File = path, X = x, Y = y });
                }

                t.Done();
            }

            if (tiles.Count == 0) r.Fail("tiles", "must name at least one tile");
            grid.Tiles = tiles;
            r.Done();
            return grid;
        }

        static CameraDefinition ReadCamera(ContentReader r)
        {
            var start = r.Pair("start");
            var aspect = r.Pair("aspect", 1f);
            var camera = new CameraDefinition
            {
                StartX = start.X,
                StartY = start.Y,
                StartZ = r.Number("startZ"),
                FieldOfView = r.Number("fieldOfView", 1f, 179f),
                Aspect = aspect.Y > 0 ? aspect.X / aspect.Y : 1f,
                Near = r.Positive("near"),
                Far = r.Positive("far"),
                NearestZ = r.Number("nearestZ", max: 0f),
                FarthestZ = r.Number("farthestZ", max: 0f),
                RenderRangeBase = r.Number("renderRangeBase", 0f),
            };
            r.Check(camera.Far > camera.Near, "far", "must be beyond 'near'");
            r.Check(camera.FarthestZ < camera.NearestZ, "farthestZ", "must be farther from the world (more negative) than 'nearestZ'");
            r.Check(camera.StartZ <= camera.NearestZ && camera.StartZ >= camera.FarthestZ, "startZ", "must lie between 'farthestZ' and 'nearestZ'");
            r.Done();
            return camera;
        }

        static CursorDefinition ReadCursor(ContentReader r)
        {
            var screen = r.Pair("screen", 1f);
            var cursor = new CursorDefinition
            {
                Size = r.Positive("size"),
                DrawOffset = r.Pair("drawOffset"),
                WorldPerPixel = r.Positive("worldPerPixel"),
                ScreenWidth = screen.X,
                ScreenHeight = screen.Y,
                EdgeMargin = r.Number("edgeMargin", 0f),
            };
            r.Check(cursor.EdgeMargin * 2 < cursor.ScreenWidth && cursor.EdgeMargin * 2 < cursor.ScreenHeight, "edgeMargin", "must leave room on the screen");

            var edges = r.Section("edgeSnap");
            cursor.EdgeLeft = edges.Number("left", max: 0f);
            cursor.EdgeRight = edges.Number("right", 0f);
            cursor.EdgeUp = edges.Number("up", 0f);
            cursor.EdgeDown = edges.Number("down", max: 0f);
            edges.Done();

            var sprites = new Dictionary<CursorSprite, string>();
            var section = r.Section("sprites");
            foreach (CursorSprite sprite in Enum.GetValues(typeof(CursorSprite)))
            {
                var key = char.ToLowerInvariant(sprite.ToString()[0]) + sprite.ToString().Substring(1);
                sprites[sprite] = PicturePath(section, key);
            }

            section.Done();
            cursor.Sprites = sprites;
            r.Done();
            return cursor;
        }

        static CameraControlDefinition ReadControl(ContentReader r)
        {
            var c = new CameraControlDefinition
            {
                Speed = r.Positive("speed"),
                FastFactor = r.Positive("fastFactor"),
                WheelStep = r.Positive("wheelStep"),
                DragRate = r.Positive("dragRate"),
                DragDeadZone = r.Number("dragDeadZone", 0f),
                CentreRate = r.Positive("centreRate"),
                CentreOffset = r.Pair("centreOffset"),
                CentreArriveOffset = r.Pair("centreArriveOffset"),
                CentreArriveRange = r.Positive("centreArriveRange"),
                Strafe = r.Pair("strafe", 0f),
                Isometric = r.Number("isometric", 0f),
            };
            r.Check(c.Speed * c.CentreRate < 1f, "centreRate", "times 'speed' must be below 1, or centring overshoots");

            var scroll = r.Section("scrollLimits");
            c.ScrollLeft = scroll.Number("left", max: 0f);
            c.ScrollRight = scroll.Number("right", 0f);
            c.ScrollDown = scroll.Number("down", max: 0f);
            c.ScrollUp = scroll.Number("up", 0f);
            scroll.Done();

            var carry = r.Section("carryBeyond");
            c.CarryLeft = carry.Number("left", max: 0f);
            c.CarryRight = carry.Number("right", 0f);
            c.CarryDown = carry.Number("down", max: 0f);
            c.CarryUp = carry.Number("up", 0f);
            carry.Done();

            r.Done();
            return c;
        }

        static BackdropDefinition ReadBackdrop(ContentReader r)
        {
            var size = Numbers(r, "size", 3, float.Epsilon, float.MaxValue);
            var texture = PicturePath(r, "texture");
            var colour = Numbers(r, "clearColor", 4, 0f, 255f);
            var bytes = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                if (colour[i] != (float)Math.Floor(colour[i])) r.Fail("clearColor", "must hold whole numbers");
                bytes[i] = (byte)colour[i];
            }

            r.Done();
            return new BackdropDefinition { Width = size[0], Height = size[1], Depth = size[2], Texture = texture, ClearColor = bytes };
        }

        /// <summary>A picture path: relative, forward slashes, with a file name.</summary>
        static string PicturePath(ContentReader r, string key)
        {
            var path = r.Text(key);
            if (path.Length == 0) return path;
            if (path.IndexOf('\\') >= 0 || path.StartsWith("/", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
                r.Fail(key, "must be a relative path with forward slashes and a file name (is \"" + path + "\")");
            return path;
        }

        /// <summary>An array of exactly <paramref name="count"/> numbers, each in [min, max].</summary>
        static float[] Numbers(ContentReader r, string key, int count, float min, float max)
        {
            var result = new float[count];
            var value = r.Raw(key, JsonKind.Array, "an array of " + count + " numbers");
            if (value == null) return result;
            if (value.Items.Count != count)
            {
                r.Fail(key, "must hold " + count + " numbers (has " + value.Items.Count + ")");
                return result;
            }

            for (var i = 0; i < count; i++)
            {
                var item = value.Items[i];
                if (item.Kind != JsonKind.Number || item.Number < min || item.Number > max)
                {
                    r.Fail(key, "must hold numbers " + (min > 0 ? "above 0" : "between " + min + " and " + max));
                    return result;
                }

                result[i] = (float)item.Number;
            }

            return result;
        }
    }
}

using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>The original's weather modes (Rain.theWeather), in its enum order.</summary>
    public enum WeatherKind
    {
        Snowing,
        Raining,
        Clear,
        FallingLeaves,
        DustStorm,
    }

    /// <summary>
    /// The box the original scattered its weather and clouds over (startX, startY, worldWidth and worldHeight of the area
    /// grid): x from <see cref="Left"/> to <see cref="Left"/> + <see cref="Width"/>, y from <see cref="Top"/> -
    /// <see cref="Height"/> to <see cref="Top"/>.
    /// </summary>
    public sealed class SceneryArea
    {
        public float Left { get; internal set; }
        public float Top { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }

        /// <summary>The right edge (startX + worldWidth), where a cloud wraps.</summary>
        public float Right => Left + Width;
    }

    /// <summary>How one weather mode moves its particles each frame (Rain.updateRain's Select Case) and what it draws.</summary>
    public sealed class WeatherKindDefinition
    {
        public WeatherKind Kind { get; internal set; }

        /// <summary>The picture every particle shows in this mode; null draws nothing (Clear).</summary>
        public string Texture { get; internal set; }

        public bool Drawn => Texture != null;

        /// <summary>Snowing's drift: x and y each gain this times a fresh random 0 to 1 (x drawn first). Zero draws nothing.</summary>
        public DataPoint Jitter { get; internal set; }

        /// <summary>FallingLeaves' wander: one of four diagonals (getRndInt(1, 4)), each axis this times a fresh random. Zero draws nothing.</summary>
        public float Wander { get; internal set; }

        /// <summary>A steady move along x each frame (the dust storm's wind).</summary>
        public float StepX { get; internal set; }

        /// <summary>How far each particle falls toward the ground (z grows) each frame.</summary>
        public float Fall { get; internal set; }

        public override string ToString() => Kind.ToString();
    }

    /// <summary>The particles of the weather (Rain.loadRain and updateRain, RECONSTRUCTION 14.1).</summary>
    public sealed class WeatherDefinition
    {
        /// <summary>maxRains + 1 particles (2,501).</summary>
        public int Count { get; internal set; }

        /// <summary>RainWidth (0.04): the quad spans x to x + width.</summary>
        public float Width { get; internal set; }

        /// <summary>RainHeight (RainWidth / 2): the quad spans y - halfHeight to y + halfHeight.</summary>
        public float HalfHeight { get; internal set; }

        /// <summary>Particle n starts at z = startZ - (n + 1) x startZStep (the step summed in single precision).</summary>
        public float StartZ { get; internal set; }

        public float StartZStep { get; internal set; }

        /// <summary>A particle that reaches the ground (z at or past <see cref="GroundZ"/>) starts again at this z ...</summary>
        public float RespawnZ { get; internal set; }

        /// <summary>... with a new random x and y, y lifted by this (the loader's y had no lift).</summary>
        public float RespawnLift { get; internal set; }

        public float GroundZ { get; internal set; }

        /// <summary>The mode a new game starts in (Snowing).</summary>
        public WeatherKind Start { get; internal set; }

        /// <summary>The order W steps through, wrapping (frmMain's Keys.W).</summary>
        public IReadOnlyList<WeatherKind> Cycle { get; internal set; }

        /// <summary>Every mode, by <see cref="WeatherKind"/>.</summary>
        public IReadOnlyDictionary<WeatherKind, WeatherKindDefinition> Kinds { get; internal set; }

        public WeatherKindDefinition Of(WeatherKind kind) => Kinds[kind];

        /// <summary>The mode W moves on to from <paramref name="kind"/>.</summary>
        public WeatherKind After(WeatherKind kind)
        {
            for (var i = 0; i < Cycle.Count; i++)
                if (Cycle[i] == kind) return Cycle[(i + 1) % Cycle.Count];
            return Cycle[0];
        }
    }

    /// <summary>The clouds and balloons (Clouds.loadCloud and updateCloud, RECONSTRUCTION 14.2).</summary>
    public sealed class CloudDefinition
    {
        /// <summary>maxClouds + 1 clouds (401).</summary>
        public int Count { get; internal set; }

        /// <summary>Cloud n sits at z = startZ - n x zStep (the step summed in single precision): the first farthest from the camera.</summary>
        public float StartZ { get; internal set; }

        public float ZStep { get; internal set; }

        /// <summary>y is lifted by this over the area's random y, at the start and at every wrap.</summary>
        public float Lift { get; internal set; }

        /// <summary>Drift to the right each frame: min + spread x a random 0 to 1.</summary>
        public float SpeedMin { get; internal set; }

        public float SpeedSpread { get; internal set; }

        /// <summary>Width: min + spread x a random 0 to 1; the quad is width wide and width high (half height width / 2).</summary>
        public float WidthMin { get; internal set; }

        public float WidthSpread { get; internal set; }

        /// <summary>The pictures a cloud is given at random (getRndInt(1, count)).</summary>
        public IReadOnlyList<string> Textures { get; internal set; }

        /// <summary>The first this-many clouds are the balloon instead.</summary>
        public int Balloons { get; internal set; }

        public string BalloonTexture { get; internal set; }
    }

    /// <summary>The weather and the clouds (weather.json).</summary>
    public sealed class SceneryData
    {
        public SceneryArea Area { get; internal set; }
        public WeatherDefinition Weather { get; internal set; }
        public CloudDefinition Clouds { get; internal set; }

        /// <summary>Every picture the weather and clouds can show.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var list = new List<string>();
                foreach (var k in Weather.Kinds.Values)
                    if (k.Texture != null && !list.Contains(k.Texture)) list.Add(k.Texture);
                foreach (var t in Clouds.Textures)
                    if (!list.Contains(t)) list.Add(t);
                if (!list.Contains(Clouds.BalloonTexture)) list.Add(Clouds.BalloonTexture);
                return list;
            }
        }

        internal static SceneryData Read(IContentSource source, string file, AreaGridDefinition grid, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var data = new SceneryData();

            var area = root.Section("area");
            data.Area = new SceneryArea
            {
                Left = area.Number("left"),
                Top = area.Number("top"),
                Width = area.Positive("width"),
                Height = area.Positive("height"),
            };
            // The original took the box from the area grid itself: startX, startY and the grid's size less one.
            if (grid != null)
            {
                area.Check(data.Area.Left == grid.StartX, "left", "must be the area grid's startX (" + grid.StartX + ")");
                area.Check(data.Area.Top == grid.StartY, "top", "must be the area grid's startY (" + grid.StartY + ")");
                area.Check(data.Area.Width == grid.Columns - 1, "width", "must be the area grid's columns less one (" + (grid.Columns - 1) + ")");
                area.Check(data.Area.Height == grid.Rows - 1, "height", "must be the area grid's rows less one (" + (grid.Rows - 1) + ")");
            }

            area.Done();

            data.Weather = ReadWeather(root.Section("weather"));
            data.Clouds = ReadClouds(root.Section("clouds"));
            root.Done();
            return data;
        }

        static WeatherDefinition ReadWeather(ContentReader r)
        {
            var w = new WeatherDefinition
            {
                Count = r.Integer("count", 1, 65535 / 4),
                Width = r.Positive("width"),
                HalfHeight = r.Positive("halfHeight"),
                StartZ = r.Number("startZ"),
                StartZStep = r.Number("startZStep", 0f),
                RespawnZ = r.Number("respawnZ"),
                RespawnLift = r.Number("respawnLift"),
                GroundZ = r.Number("groundZ"),
                Start = r.Choice<WeatherKind>("start"),
            };
            r.Check(w.RespawnZ < w.GroundZ, "respawnZ", "must be below 'groundZ' (a particle would start again on the ground)");

            var cycle = new List<WeatherKind>();
            foreach (var name in r.Texts("cycle"))
            {
                if (Array.IndexOf(Enum.GetNames(typeof(WeatherKind)), name) < 0) r.Fail("cycle", "\"" + name + "\" is not one of " + string.Join(", ", Enum.GetNames(typeof(WeatherKind))));
                else if (cycle.Contains((WeatherKind)Enum.Parse(typeof(WeatherKind), name))) r.Fail("cycle", "names \"" + name + "\" twice");
                else cycle.Add((WeatherKind)Enum.Parse(typeof(WeatherKind), name));
            }

            if (cycle.Count == 0) cycle.Add(w.Start);
            r.Check(cycle.Contains(w.Start), "cycle", "must hold the start mode \"" + w.Start + "\"");
            w.Cycle = cycle;

            var kinds = new Dictionary<WeatherKind, WeatherKindDefinition>();
            foreach (var k in r.Objects("kinds"))
            {
                var d = new WeatherKindDefinition
                {
                    Kind = k.Choice<WeatherKind>("kind"),
                    Texture = k.TextOrNull("texture"),
                    Jitter = k.Pair("jitter", 0f),
                    Wander = k.Number("wander", 0f),
                    StepX = k.Number("stepX"),
                    Fall = k.Number("fall", 0f),
                };
                if (d.Texture != null && d.Texture.Length == 0) k.Fail("texture", "must be a picture's path or null (nothing drawn)");
                if (kinds.ContainsKey(d.Kind)) k.Fail("kind", "\"" + d.Kind + "\" is listed twice");
                else kinds[d.Kind] = d;
                k.Done();
            }

            foreach (WeatherKind kind in Enum.GetValues(typeof(WeatherKind)))
                if (!kinds.ContainsKey(kind))
                {
                    r.Fail("kinds", "has no entry for \"" + kind + "\"");
                    kinds[kind] = new WeatherKindDefinition { Kind = kind };
                }

            w.Kinds = kinds;
            r.Done();
            return w;
        }

        static CloudDefinition ReadClouds(ContentReader r)
        {
            var c = new CloudDefinition
            {
                Count = r.Integer("count", 1, 65535 / 4),
                StartZ = r.Number("startZ"),
                ZStep = r.Number("zStep", 0f),
                Lift = r.Number("lift"),
                SpeedMin = r.Number("speedMin", 0f),
                SpeedSpread = r.Number("speedSpread", 0f),
                WidthMin = r.Positive("widthMin"),
                WidthSpread = r.Number("widthSpread", 0f),
                Textures = r.Texts("textures"),
                Balloons = r.Integer("balloons", 0),
                BalloonTexture = r.Text("balloonTexture"),
            };
            r.Check(c.Textures.Count > 0, "textures", "must name at least one cloud picture");
            r.Check(c.Balloons <= c.Count, "balloons", "must not be more than the clouds (" + c.Count + ")");
            r.Check(c.SpeedMin > 0f, "speedMin", "must be greater than 0 (a cloud drifts right and wraps at the right edge)");
            r.Done();
            return c;
        }
    }
}

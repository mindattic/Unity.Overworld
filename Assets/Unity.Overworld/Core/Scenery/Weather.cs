using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>W: the weather moves on to the next mode, wrapping (Snowing, Raining, Clear, FallingLeaves, DustStorm).</summary>
    public sealed class CycleWeather : ICommand
    {
        public CycleWeather(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>Sets the weather's mode outright (scripted runs).</summary>
    public sealed class SetWeather : ICommand
    {
        public SetWeather(PlayerId player, WeatherKind kind) { Player = player; Kind = kind; }
        public PlayerId Player { get; }
        public WeatherKind Kind { get; }
    }

    public sealed class WeatherChanged : IGameEvent
    {
        public WeatherChanged(long tick, PlayerId by, WeatherKind previous, WeatherKind kind)
        {
            Tick = tick;
            By = by;
            Previous = previous;
            Kind = kind;
        }

        public long Tick { get; }
        public PlayerId By { get; }
        public WeatherKind Previous { get; }
        public WeatherKind Kind { get; }
    }

    /// <summary>Where a particle or cloud is now and where it was at the start of the last tick (presentation eases between them).</summary>
    public readonly struct SkyPoint
    {
        public SkyPoint(float x, float y, float z, float previousX, float previousY, float previousZ)
        {
            X = x;
            Y = y;
            Z = z;
            PreviousX = previousX;
            PreviousY = previousY;
            PreviousZ = previousZ;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float PreviousX { get; }
        public float PreviousY { get; }
        public float PreviousZ { get; }
    }

    /// <summary>
    /// The weather (Weather.vb, module Rain; RECONSTRUCTION 14.1): 2,501 particles scattered over the area grid's box,
    /// each starting a hundredth further from the ground than the last, all moved every 60 Hz tick by the mode's rule
    /// (Snowing drifts and falls, Raining falls fast, Clear stops and hides them, FallingLeaves wanders on a random
    /// diagonal, DustStorm blows right and falls fast); one that reaches the ground starts again at z -1.1 with a new random
    /// x and y. One weather for the world, as the original's module was; W (<see cref="CycleWeather"/>) moves it on.
    /// Stepped where updateRain ran: after the input and before the pickups and the party.
    /// </summary>
    public sealed class Weather : ITickSystem
    {
        readonly Random random;
        readonly float[] x, y, z, px, py, pz;

        public Weather(SceneryData data, Random random = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            this.random = random ?? new Random();
            var w = data.Weather;
            var n = w.Count;
            x = new float[n];
            y = new float[n];
            z = new float[n];
            px = new float[n];
            py = new float[n];
            pz = new float[n];
            Kind = w.Start;

            // loadRain: x then y at random over the box (no lift yet), z stepping away from the ground from the first.
            var increment = w.StartZStep;
            for (var i = 0; i < n; i++)
            {
                x[i] = data.Area.Left + data.Area.Width * Percent();
                y[i] = data.Area.Top - data.Area.Height * Percent();
                z[i] = w.StartZ - increment;
                increment += w.StartZStep;
            }

            Settle();
        }

        public SceneryData Data { get; }

        /// <summary>The mode now (weather).</summary>
        public WeatherKind Kind { get; private set; }

        public WeatherKindDefinition Current => Data.Weather.Of(Kind);

        public int Count => x.Length;

        public SkyPoint this[int index] => new SkyPoint(x[index], y[index], z[index], px[index], py[index], pz[index]);

        public Weather Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<CycleWeather>(HandleCycle);
            game.Register<SetWeather>(HandleSet);
            game.AddSystem(this);
            return this;
        }

        CommandResult HandleCycle(Game game, CycleWeather c) => Change(game, c.Player, Data.Weather.After(Kind));

        CommandResult HandleSet(Game game, SetWeather c) => Change(game, c.Player, c.Kind);

        CommandResult Change(Game game, PlayerId by, WeatherKind kind)
        {
            var previous = Kind;
            Kind = kind;
            return CommandResult.Accept(new WeatherChanged(game.Tick, by, previous, kind));
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events) => Step();

        /// <summary>updateRain: every particle moved by the mode's rule in index order, starting again once it reaches the ground.</summary>
        public void Step()
        {
            var k = Current;
            var w = Data.Weather;
            var area = Data.Area;
            var jitter = k.Jitter.X != 0f || k.Jitter.Y != 0f;
            for (var i = 0; i < x.Length; i++)
            {
                px[i] = x[i];
                py[i] = y[i];
                pz[i] = z[i];

                if (jitter)
                {
                    x[i] += k.Jitter.X * Percent();
                    y[i] += k.Jitter.Y * Percent();
                }

                if (k.Wander != 0f)
                {
                    // getRndInt(1, 4): 1 up-right, 2 down-left, 3 up-left, 4 down-right, each axis a fresh random.
                    switch (this.random.Next(1, 5))
                    {
                        case 1:
                            x[i] += k.Wander * Percent();
                            y[i] += k.Wander * Percent();
                            break;
                        case 2:
                            x[i] -= k.Wander * Percent();
                            y[i] -= k.Wander * Percent();
                            break;
                        case 3:
                            x[i] -= k.Wander * Percent();
                            y[i] += k.Wander * Percent();
                            break;
                        default:
                            x[i] += k.Wander * Percent();
                            y[i] -= k.Wander * Percent();
                            break;
                    }
                }

                x[i] += k.StepX;
                z[i] += k.Fall;

                if (z[i] >= w.GroundZ)
                {
                    z[i] = w.RespawnZ;
                    x[i] = area.Left + area.Width * Percent();
                    y[i] = area.Top - area.Height * Percent() + w.RespawnLift;
                    // A particle that starts again appears there; there is nothing to ease from.
                    px[i] = x[i];
                    py[i] = y[i];
                    pz[i] = z[i];
                }
            }
        }

        void Settle()
        {
            Array.Copy(x, px, x.Length);
            Array.Copy(y, py, y.Length);
            Array.Copy(z, pz, z.Length);
        }

        float Percent() => (float)random.NextDouble();
    }

    /// <summary>
    /// The clouds (Clouds.vb; RECONSTRUCTION 14.2): 401 at random over the area grid's box lifted by 1, the first farthest
    /// from the camera and each 0.005 nearer, 0.2 to 0.8 wide, cloud1 or cloud2 at random, the first four the balloon; every
    /// 60 Hz tick each drifts right by its own 0.001 to 0.004 and one past the right edge comes back at the left with a new
    /// random y. Stepped where updateCloud ran: first after the input.
    /// </summary>
    public sealed class Clouds : ITickSystem
    {
        readonly Random random;
        readonly float[] x, y, z, px, py, speed, width;
        readonly string[] files;

        public Clouds(SceneryData data, Random random = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            this.random = random ?? new Random();
            var c = data.Clouds;
            var n = c.Count;
            x = new float[n];
            y = new float[n];
            z = new float[n];
            px = new float[n];
            py = new float[n];
            speed = new float[n];
            width = new float[n];
            files = new string[n];

            // loadCloud's draws, in its order: x, y, the speed, the width, then the picture (drawn for the balloons too).
            var increment = 0f;
            for (var i = 0; i < n; i++)
            {
                x[i] = data.Area.Left + data.Area.Width * Percent();
                y[i] = data.Area.Top - data.Area.Height * Percent() + c.Lift;
                speed[i] = c.SpeedSpread * Percent() + c.SpeedMin;
                z[i] = c.StartZ - increment;
                increment += c.ZStep;
                width[i] = c.WidthSpread * Percent() + c.WidthMin;
                var picture = this.random.Next(1, c.Textures.Count + 1);
                files[i] = i < c.Balloons ? c.BalloonTexture : c.Textures[picture - 1];
            }

            Array.Copy(x, px, n);
            Array.Copy(y, py, n);
        }

        public SceneryData Data { get; }

        public int Count => x.Length;

        public SkyPoint this[int index] => new SkyPoint(x[index], y[index], z[index], px[index], py[index], z[index]);

        /// <summary>The cloud's width (and height: its quad is width wide, width / 2 above and below its y).</summary>
        public float Width(int index) => width[index];

        /// <summary>How far it drifts right each tick.</summary>
        public float Speed(int index) => speed[index];

        /// <summary>The picture it shows (cloud1, cloud2 or the balloon).</summary>
        public string File(int index) => files[index];

        public Clouds Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.AddSystem(this);
            return this;
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events) => Step();

        /// <summary>updateCloud: each drifts right; past the right edge it is put at the left with a new y (and past the left, at the right).</summary>
        public void Step()
        {
            var area = Data.Area;
            var lift = Data.Clouds.Lift;
            for (var i = 0; i < x.Length; i++)
            {
                px[i] = x[i];
                py[i] = y[i];
                x[i] += speed[i];

                var wrapped = false;
                if (x[i] > area.Right)
                {
                    x[i] = area.Left;
                    y[i] = area.Top - area.Height * Percent() + lift;
                    wrapped = true;
                }

                // Never reached while every cloud drifts right; kept as the original wrote it.
                if (x[i] < area.Left)
                {
                    x[i] = area.Right;
                    y[i] = area.Top - area.Height * Percent() + lift;
                    wrapped = true;
                }

                if (wrapped)
                {
                    px[i] = x[i];
                    py[i] = y[i];
                }
            }
        }

        float Percent() => (float)random.NextDouble();
    }
}

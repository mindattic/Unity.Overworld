using System;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A17: the weather, the clouds and the tent (RECONSTRUCTION 14.1, 14.2 and 3.6), from weather.json and
    /// props.json, checked against Weather.vb, Clouds.vb and Tent.vb.
    /// </summary>
    public class WeatherTests
    {
        const float Tolerance = 1e-5f;

        static string FindUp(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(relative + " was not found above " + AppContext.BaseDirectory);
        }

        static string ContentDirectory() => FindUp(Path.Combine("Assets", "StreamingAssets", "Content"));

        static readonly ContentRegistry Registry = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));

        static SceneryData Data => Registry.Scenery;

        static ContentException Rejected(Func<string, string> change)
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var original = disk.Read("weather.json");
            var changed = change(original);
            Assert.AreNotEqual(original, changed, "the patch to weather.json changed nothing");
            return Assert.Throws<ContentException>(() => ContentRegistry.Load(new MemoryContentSource(disk).Set("weather.json", changed)));
        }

        static Game NewGame(out Weather weather, int seed = 1)
        {
            var game = new Game();
            weather = new Weather(Data, new Random(seed)).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            return game;
        }

        // ---- the data ------------------------------------------------------------------------------------------

        [Test]
        public void TheWeatherIs2501ParticlesOfTheOriginalSizeStartingInSnow()
        {
            var w = Data.Weather;
            Assert.AreEqual(2501, w.Count, "maxRains 2500, 0 to 2500");
            Assert.AreEqual(0.04f, w.Width);
            Assert.AreEqual(0.02f, w.HalfHeight);
            Assert.AreEqual(WeatherKind.Snowing, w.Start);
            CollectionAssert.AreEqual(
                new[] { WeatherKind.Snowing, WeatherKind.Raining, WeatherKind.Clear, WeatherKind.FallingLeaves, WeatherKind.DustStorm }, w.Cycle);
            Assert.AreEqual("gfx/weather/snow1.PNG", w.Of(WeatherKind.Snowing).Texture);
            Assert.AreEqual("gfx/weather/rain1.PNG", w.Of(WeatherKind.Raining).Texture);
            Assert.IsFalse(w.Of(WeatherKind.Clear).Drawn);
            Assert.AreEqual("gfx/weather/snow1.PNG", w.Of(WeatherKind.FallingLeaves).Texture, "no leaf picture exists");
            Assert.AreEqual("gfx/weather/dust1.PNG", w.Of(WeatherKind.DustStorm).Texture);
        }

        [Test]
        public void TheBoxIsTheAreaGridsStartAndSizeLessOne()
        {
            Assert.AreEqual(-3f, Data.Area.Left);
            Assert.AreEqual(2f, Data.Area.Top);
            Assert.AreEqual(10f, Data.Area.Width);
            Assert.AreEqual(10f, Data.Area.Height);
            StringAssert.Contains("startX", Rejected(t => t.Replace("\"left\": -3", "\"left\": -2")).Message);
        }

        [Test]
        public void EveryModeMustBeDescribed()
        {
            var e = Rejected(t => t.Replace("{\"kind\": \"DustStorm\"", "{\"kind\": \"Raining\""));
            StringAssert.Contains("DustStorm", e.Message);
        }

        [Test]
        public void TheCloudsAre401WithFourBalloons()
        {
            var c = Data.Clouds;
            Assert.AreEqual(401, c.Count, "maxClouds 400, 0 to 400");
            Assert.AreEqual(4, c.Balloons);
            Assert.AreEqual("gfx/balloon.PNG", c.BalloonTexture);
            CollectionAssert.AreEqual(new[] { "gfx/clouds/cloud1.PNG", "gfx/clouds/cloud2.PNG" }, c.Textures);
        }

        // ---- the particles -------------------------------------------------------------------------------------

        [Test]
        public void ParticlesStartScatteredEachAHundredthFurtherUp()
        {
            var weather = new Weather(Data, new Random(3));
            Assert.AreEqual(2501, weather.Count);
            Assert.AreEqual(-1.51f, weather[0].Z, Tolerance);
            Assert.AreEqual(-1.52f, weather[1].Z, Tolerance);
            Assert.AreEqual(-26.51f, weather[2500].Z, 0.01f, "the single-precision step drifts a little over 2,500 sums");
            for (var i = 0; i < weather.Count; i++)
            {
                var p = weather[i];
                Assert.That(p.X, Is.InRange(-3f, 7f));
                Assert.That(p.Y, Is.InRange(-8f, 2f), "the loader's y has no lift");
                Assert.AreEqual(p.X, p.PreviousX);
            }
        }

        [Test]
        public void SnowDriftsUpAndRightByUpToAThousandthAndFallsAHundredth()
        {
            NewGame(out var weather);
            var before = Enumerable.Range(0, weather.Count).Select(i => weather[i]).ToArray();
            weather.Step();
            for (var i = 0; i < weather.Count; i++)
            {
                Assert.That(weather[i].X - before[i].X, Is.InRange(0f, 0.001f + Tolerance));
                Assert.That(weather[i].Y - before[i].Y, Is.InRange(0f, 0.001f + Tolerance));
                Assert.AreEqual(before[i].Z + 0.01f, weather[i].Z, Tolerance);
                Assert.AreEqual(before[i].X, weather[i].PreviousX);
            }
        }

        [Test]
        public void RainFallsStraightFast()
        {
            var game = NewGame(out var weather);
            Assert.IsTrue(game.Submit(new SetWeather(PlayerId.One, WeatherKind.Raining)).Accepted);
            var before = weather[7];
            weather.Step();
            Assert.AreEqual(before.X, weather[7].X);
            Assert.AreEqual(before.Y, weather[7].Y);
            Assert.AreEqual(before.Z + 0.05f, weather[7].Z, Tolerance);
        }

        [Test]
        public void ClearStopsEverything()
        {
            var game = NewGame(out var weather);
            game.Submit(new SetWeather(PlayerId.One, WeatherKind.Clear));
            var before = Enumerable.Range(0, weather.Count).Select(i => weather[i]).ToArray();
            for (var t = 0; t < 10; t++) weather.Step();
            for (var i = 0; i < weather.Count; i++)
            {
                Assert.AreEqual(before[i].X, weather[i].X);
                Assert.AreEqual(before[i].Y, weather[i].Y);
                Assert.AreEqual(before[i].Z, weather[i].Z);
            }
        }

        [Test]
        public void LeavesWanderOnADiagonalAndFallAHundredth()
        {
            var game = NewGame(out var weather);
            game.Submit(new SetWeather(PlayerId.One, WeatherKind.FallingLeaves));
            var before = Enumerable.Range(0, weather.Count).Select(i => weather[i]).ToArray();
            weather.Step();
            int upRight = 0, downLeft = 0, upLeft = 0, downRight = 0;
            for (var i = 0; i < weather.Count; i++)
            {
                var dx = weather[i].X - before[i].X;
                var dy = weather[i].Y - before[i].Y;
                Assert.That(Math.Abs(dx), Is.LessThanOrEqualTo(0.003f + Tolerance));
                Assert.That(Math.Abs(dy), Is.LessThanOrEqualTo(0.003f + Tolerance));
                Assert.AreEqual(before[i].Z + 0.01f, weather[i].Z, Tolerance);
                if (dx >= 0 && dy >= 0) upRight++;
                else if (dx <= 0 && dy <= 0) downLeft++;
                else if (dx <= 0 && dy >= 0) upLeft++;
                else downRight++;
            }

            Assert.That(new[] { upRight, downLeft, upLeft, downRight }, Is.All.GreaterThan(400), "the four diagonals are equally likely");
        }

        [Test]
        public void DustBlowsRightAndFallsFast()
        {
            var game = NewGame(out var weather);
            game.Submit(new SetWeather(PlayerId.One, WeatherKind.DustStorm));
            var before = weather[11];
            weather.Step();
            Assert.AreEqual(before.X + 0.05f, weather[11].X, Tolerance);
            Assert.AreEqual(before.Y, weather[11].Y);
            Assert.AreEqual(before.Z + 0.05f, weather[11].Z, Tolerance);
        }

        [Test]
        public void AParticleThatReachesTheGroundStartsAgainAt1Point1WithANewPlace()
        {
            NewGame(out var weather);
            // Particle 0 starts at -1.51 and falls 0.01 a tick: about 151 ticks bring it to the ground (single-precision sums).
            var ticks = 0;
            while (weather[0].Z != -1.1f)
            {
                Assert.Less(weather[0].Z, 0f);
                weather.Step();
                ticks++;
                Assert.Less(ticks, 160);
            }

            Assert.That(ticks, Is.InRange(151, 152));
            var p = weather[0];
            Assert.AreEqual(-1.1f, p.Z);
            Assert.That(p.X, Is.InRange(-3f, 7f));
            Assert.That(p.Y, Is.InRange(-7f, 3f), "lifted by 1 when it starts again");
            Assert.AreEqual(p.X, p.PreviousX, "it appears there; nothing eases across the world");
            Assert.AreEqual(p.Z, p.PreviousZ);
        }

        [Test]
        public void WCyclesTheModesAndWraps()
        {
            var game = NewGame(out var weather);
            var seen = new System.Collections.Generic.List<WeatherKind>();
            game.EventRaised += e =>
            {
                if (e is WeatherChanged c) seen.Add(c.Kind);
            };
            for (var i = 0; i < 5; i++) Assert.IsTrue(game.Submit(new CycleWeather(PlayerId.One)).Accepted);
            CollectionAssert.AreEqual(
                new[] { WeatherKind.Raining, WeatherKind.Clear, WeatherKind.FallingLeaves, WeatherKind.DustStorm, WeatherKind.Snowing }, seen);
            Assert.AreEqual(WeatherKind.Snowing, weather.Kind);
        }

        [Test]
        public void TheWeatherStepsWithTheGame()
        {
            var game = NewGame(out var weather);
            var z = weather[0].Z;
            game.Step();
            Assert.AreEqual(z + 0.01f, weather[0].Z, Tolerance);
        }

        [Test]
        public void TheSameSeedScattersTheSame()
        {
            var a = new Weather(Data, new Random(42));
            var b = new Weather(Data, new Random(42));
            for (var t = 0; t < 3; t++)
            {
                a.Step();
                b.Step();
            }

            for (var i = 0; i < a.Count; i += 97) Assert.AreEqual(a[i].X, b[i].X);
        }

        // ---- the clouds ----------------------------------------------------------------------------------------

        [Test]
        public void CloudsStartScatteredTheFirstFourBalloons()
        {
            var clouds = new Clouds(Data, new Random(5));
            Assert.AreEqual(401, clouds.Count);
            for (var i = 0; i < clouds.Count; i++)
            {
                var c = clouds[i];
                Assert.That(c.X, Is.InRange(-3f, 7f));
                Assert.That(c.Y, Is.InRange(-7f, 3f));
                Assert.AreEqual(-1.1f - 0.005f * i, c.Z, 1e-4f);
                Assert.That(clouds.Speed(i), Is.InRange(0.001f, 0.004f));
                Assert.That(clouds.Width(i), Is.InRange(0.2f, 0.8f));
                if (i < 4) Assert.AreEqual("gfx/balloon.PNG", clouds.File(i));
                else Assert.That(clouds.File(i), Is.EqualTo("gfx/clouds/cloud1.PNG").Or.EqualTo("gfx/clouds/cloud2.PNG"));
            }

            Assert.That(Enumerable.Range(4, 397).Count(i => clouds.File(i) == "gfx/clouds/cloud1.PNG"), Is.InRange(120, 277), "either picture at random");
        }

        [Test]
        public void CloudsDriftRightAndWrapToTheLeftWithANewHeight()
        {
            var clouds = new Clouds(Data, new Random(9));
            var game = new Game();
            clouds.Install(game);
            var x0 = clouds[0].X;
            var y0 = clouds[0].Y;
            game.Step();
            Assert.AreEqual(x0 + clouds.Speed(0), clouds[0].X, Tolerance);
            Assert.AreEqual(y0, clouds[0].Y);
            Assert.AreEqual(x0, clouds[0].PreviousX);

            // Long enough for the slowest to cross the whole box.
            var wrapped = false;
            for (var t = 0; t < 10000 && !wrapped; t++)
            {
                var before = clouds[0].X;
                clouds.Step();
                if (clouds[0].X < before)
                {
                    wrapped = true;
                    Assert.AreEqual(-3f, clouds[0].X);
                    Assert.That(clouds[0].Y, Is.InRange(-7f, 3f));
                    Assert.AreEqual(clouds[0].X, clouds[0].PreviousX, "it appears at the left; nothing eases across the sky");
                }
            }

            Assert.IsTrue(wrapped);
            for (var i = 0; i < clouds.Count; i++) Assert.That(clouds[i].X, Is.InRange(-3f, 7f));
        }

        // ---- the tent and the culling --------------------------------------------------------------------------

        [Test]
        public void TheTentStandsAtItsPlaceThreeTenthsSquare()
        {
            var view = new WorldView(Registry.View, Registry.Props, Registry.Heroes);
            var tent = view.Tent;
            Assert.AreEqual(DrawableKind.Tent, tent.Kind);
            Assert.AreEqual(-0.7f, tent.X);
            Assert.AreEqual(-0.04f, tent.Y);
            Assert.AreEqual(0f, tent.Z);
            Assert.AreEqual(0.3f, tent.Width);
            Assert.AreEqual(0.15f, tent.HalfHeight);
            Assert.AreEqual("gfx/tent/Tent_128.PNG", tent.File);
            Assert.IsTrue(view.IsVisible(tent));
            view.Camera.MoveTo(2.5f, 0f, -1f);
            Assert.IsFalse(view.IsVisible(tent), "-0.7 is beyond 2.5 - 2");
        }

        [Test]
        public void WeatherAndCloudsAreCulledOnAllThreeAxes()
        {
            var view = new WorldView(Registry.View, Registry.Props, Registry.Heroes);
            Assert.IsTrue(view.IsVisible(0f, 0f, -0.5f));
            Assert.IsTrue(view.IsVisible(2f, -2f, -3f), "range 2 at z -1, edges included");
            Assert.IsFalse(view.IsVisible(0f, 0f, -3.5f), "z -3.5 is beyond -1 - 2");
            Assert.IsFalse(view.IsVisible(2.1f, 0f, -0.5f));
        }

        // U.OVW-T44: with no seed the random is null; the constructors and the first steps must not touch the parameter.
        [Test]
        public void WeatherAndCloudsWithoutASeedConstructAndStepWithoutThrowing()
        {
            var game = new Game();
            Assert.DoesNotThrow(() =>
            {
                new Weather(Data).Install(game);
                new Clouds(Data).Install(game);
            });
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            Assert.DoesNotThrow(() => { for (var i = 0; i < 5; i++) game.Step(); });
        }
    }
}

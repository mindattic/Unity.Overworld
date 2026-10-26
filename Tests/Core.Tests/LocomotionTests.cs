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
    /// U.OVW-US-A5: hero locomotion and animation (RECONSTRUCTION 4.4, 5.1, 5.4, 5.6). The arrows walk at 0.003 and run at
    /// 0.006 per tick in 0.001 steps, the right button heads for the cursor (running when far, walking when near, slowing
    /// and stopping on arrival), facing follows the input in four ways, and the six-frame walk and run cycles of the
    /// Chrono set advance on the clock.
    /// </summary>
    public class LocomotionTests
    {
        const float Eps = 1e-5f;

        static string ContentDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Content");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Assets/StreamingAssets/Content");
        }

        static readonly ContentRegistry Registry = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));

        Game game;
        Party party;
        Hero chrono;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Leader(PlayerId.One);
        }

        void Hold(MoveInput input) => Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, input)).Accepted);

        void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) game.Step();
        }

        /// <summary>A cursor position that puts the hero's aim point exactly (dx, dy) from the cursor's aim point.</summary>
        MoveInput Cursor(float dx, float dy, bool run = false)
        {
            var t = Registry.Heroes.Locomotion;
            var size = Registry.Heroes.HeroSize;
            var fromX = chrono.X + size * t.HeroPoint.X;
            var fromY = chrono.Y + size * t.HeroPoint.Y;
            return MoveInput.Toward(fromX + dx - t.CursorOffset.X, fromY + dy - t.CursorOffset.Y, run);
        }

        // ---- data ----------------------------------------------------------------------------------------------------

        [Test]
        public void TheChronoSetHasAStandPictureAndSixFrameWalkAndRunCyclesForEachFacing()
        {
            var s = Registry.Heroes.Sprites;
            foreach (Facing f in Enum.GetValues(typeof(Facing)))
            {
                var name = f.ToString().ToLowerInvariant();
                Assert.AreEqual("gfx/hero/Chrono/stand/face_" + name + ".PNG", s.Stand(f));
                CollectionAssert.AreEqual(Enumerable.Range(1, 6).Select(n => "gfx/hero/Chrono/walk/walk_" + name + n + ".PNG"), s.Walk(f));
                CollectionAssert.AreEqual(Enumerable.Range(1, 6).Select(n => "gfx/hero/Chrono/run/run_" + name + n + ".PNG"), s.Run(f));
            }

            Assert.AreEqual(52, s.Files.Count);
            Assert.AreEqual(52, s.Files.Distinct().Count());
        }

        [Test]
        public void EverySpriteIsAHeroFrameTheAssetCatalogueMarksUsed()
        {
            var dir = new DirectoryInfo(ContentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "docs/asset-catalogue.tsv not found");
            var used = File.ReadAllLines(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))
                .Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[2] == "used").Select(c => c[0]).ToHashSet();
            foreach (var file in Registry.Heroes.Sprites.Files) Assert.IsTrue(used.Contains(file), file + " is not a used file of the catalogue");
        }

        [Test]
        public void TheOriginalsSpeedsAndMouseFiguresAreData()
        {
            var c = Registry.Heroes.Heroes[0];
            Assert.AreEqual("Chrono", c.Name);
            Assert.AreEqual(0.003f, c.WalkSpeed);
            Assert.AreEqual(0.006f, c.RunSpeed);
            Assert.AreEqual(0.13f, c.WalkRate);
            Assert.AreEqual(0.1f, c.RunRate);
            Assert.AreEqual(5, c.WalkFrames);
            Assert.AreEqual(5, c.RunFrames);

            var t = Registry.Heroes.Locomotion;
            Assert.AreEqual(0.001f, t.Acceleration);
            Assert.AreEqual(1f, t.GameSpeed);
            Assert.AreEqual(0.02f, t.FollowSpeed);
            Assert.AreEqual(0.6f, t.MaxReach);
            Assert.AreEqual(0.2f, t.RunReach);
            Assert.AreEqual(0.1f, t.NearRange);
            Assert.AreEqual(1.5f, t.NearBoost);
            Assert.AreEqual(0.01f, t.ArriveRange);
            Assert.AreEqual(new DataPoint(0.03f, 0.01f), t.CursorOffset);
            Assert.AreEqual(0.144f / 2f, 0.144f * t.HeroPoint.X, Eps);
            Assert.AreEqual(-0.144f / 3f, 0.144f * t.HeroPoint.Y, Eps);
        }

        [Test]
        public void ABadCycleIsRejectedByName()
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var text = disk.Read("heroes.json");
            var changed = text.Replace(", \"gfx/hero/Chrono/walk/walk_up6.PNG\"", "");
            Assert.AreNotEqual(text, changed);
            var e = Assert.Throws<ContentException>(() => ContentRegistry.Load(new MemoryContentSource(disk).Set("heroes.json", changed)));
            StringAssert.Contains("heroes.json.sprites.walk: 'up' must list 6 frames", e.Message);
        }

        // ---- arrows --------------------------------------------------------------------------------------------------

        [Test]
        public void TheHeroStartsStandingFacingRightAtItsStart()
        {
            Run(1);
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.AreEqual(Facing.Right, chrono.Facing);
            Assert.AreEqual(0f, chrono.X);
            Assert.AreEqual(0f, chrono.Y);
            Assert.AreEqual("gfx/hero/Chrono/stand/face_right.PNG", chrono.Sprite);
        }

        [Test]
        public void AnArrowWalksInStepsOf0001UpTo0003AndHoversThere()
        {
            Hold(MoveInput.Arrows(right: true));
            Run(1);
            Assert.AreEqual(HeroState.Walking, chrono.State);
            Assert.AreEqual(0.001f, chrono.DX, Eps);
            Run(1);
            Assert.AreEqual(0.002f, chrono.DX, Eps);
            Run(1);
            Assert.AreEqual(0.003f, chrono.DX, Eps);
            for (var i = 0; i < 120; i++)
            {
                Run(1);
                Assert.AreEqual(0.003f, chrono.DX, 0.001f + Eps);
            }

            Assert.AreEqual(0f, chrono.DY);
            Assert.AreEqual(0.36f, chrono.X, 0.13f, "about 0.003 a tick for two seconds");
        }

        [Test]
        public void TheRunKeyRunsAt0006WhileStaminaRemains()
        {
            Hold(MoveInput.Arrows(left: true, run: true));
            Run(10);
            Assert.AreEqual(HeroState.Running, chrono.State);
            Assert.AreEqual(Facing.Left, chrono.Facing);
            Assert.AreEqual(-0.006f, chrono.DX, 0.001f + Eps);

            chrono.Stamina = 0;
            Hold(MoveInput.Arrows(left: true));
            Run(1);
            Hold(MoveInput.Arrows(left: true, run: true));
            Run(10);
            Assert.AreEqual(HeroState.Walking, chrono.State, "no stamina, no running");
            Assert.AreEqual(-0.003f, chrono.DX, 0.001f + Eps);
        }

        [Test]
        public void EachArrowFacesItsWayAndTheLastHeldWins()
        {
            foreach (var (input, facing) in new[]
                     {
                         (MoveInput.Arrows(left: true), Facing.Left), (MoveInput.Arrows(right: true), Facing.Right),
                         (MoveInput.Arrows(up: true), Facing.Up), (MoveInput.Arrows(down: true), Facing.Down),
                         (MoveInput.Arrows(left: true, up: true), Facing.Up), (MoveInput.Arrows(right: true, down: true), Facing.Down),
                     })
            {
                Hold(input);
                Run(1);
                Assert.AreEqual(facing, chrono.Facing, input.ToString());
            }
        }

        [Test]
        public void UpAndDownMoveAlongYAndDiagonalsMoveBothAxes()
        {
            Hold(MoveInput.Arrows(up: true));
            Run(5);
            Assert.AreEqual(0.003f, chrono.DY, 0.001f + Eps);
            Assert.AreEqual(0f, chrono.DX);

            Hold(MoveInput.Arrows(down: true, left: true));
            Run(10);
            Assert.AreEqual(-0.003f, chrono.DY, 0.001f + Eps);
            Assert.AreEqual(-0.003f, chrono.DX, 0.001f + Eps);
        }

        [Test]
        public void SlowingFromARunDownToAWalkDownSettlesAtTheWalkSpeed()
        {
            // The original's walk-down branch only ever sped up, so a run down that became a walk kept running speed
            // (RECONSTRUCTION 4.4); fixed per the BIBLE.
            Hold(MoveInput.Arrows(down: true, run: true));
            Run(10);
            Assert.AreEqual(-0.006f, chrono.DY, 0.001f + Eps);
            Hold(MoveInput.Arrows(down: true));
            Run(10);
            Assert.AreEqual(HeroState.Walking, chrono.State);
            Assert.AreEqual(-0.003f, chrono.DY, 0.001f + Eps);
        }

        [Test]
        public void ReleasingTheArrowsStandsAtOnceFacingTheLastWay()
        {
            Hold(MoveInput.Arrows(up: true));
            Run(20);
            var y = chrono.Y;
            Hold(default);
            Run(1);
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.AreEqual(0f, chrono.DY);
            Assert.AreEqual(y, chrono.Y);
            Assert.AreEqual("gfx/hero/Chrono/stand/face_up.PNG", chrono.Sprite);
            Assert.AreEqual(0, chrono.Frame);
        }

        [Test]
        public void OppositeArrowsTogetherFaceDownAndStand()
        {
            Hold(MoveInput.Arrows(left: true, right: true));
            Run(3);
            Assert.AreEqual(Facing.Down, chrono.Facing);
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.AreEqual(0f, chrono.DX);
        }

        [Test]
        public void TheOtherHeroesStandStill()
        {
            Hold(MoveInput.Arrows(right: true, run: true));
            Run(30);
            foreach (var h in party.Heroes.Skip(1))
            {
                Assert.AreEqual(HeroState.Standing, h.State, h.Name);
                Assert.AreEqual(h.Definition.Start.X, h.X, h.Name);
            }
        }

        // ---- the move button -----------------------------------------------------------------------------------------

        [Test]
        public void ACursorNearByWalksAtItsDistanceTimes002()
        {
            Hold(Cursor(0.15f, 0f));
            Run(1);
            Assert.AreEqual(HeroState.Walking, chrono.State);
            Assert.AreEqual(0.15f * 0.02f, chrono.DX, Eps);
            Assert.AreEqual(0f, chrono.DY, Eps);
            Assert.AreEqual(Facing.Right, chrono.Facing);
            Assert.IsFalse(chrono.Running);
        }

        [Test]
        public void ACursorAtLeast02AwayOnEitherAxisRuns()
        {
            Hold(Cursor(0.05f, -0.25f));
            Run(1);
            Assert.AreEqual(HeroState.Running, chrono.State);
            Assert.AreEqual(Facing.Down, chrono.Facing);
            Assert.AreEqual(-0.25f * 0.02f, chrono.DY, Eps);
            Assert.AreEqual(0.05f * 0.02f, chrono.DX, Eps);
        }

        [Test]
        public void TheReachIsClampedTo06AndAWalkTo02()
        {
            Hold(Cursor(-2f, 0f));
            Run(1);
            Assert.AreEqual(HeroState.Running, chrono.State);
            Assert.AreEqual(-0.6f * 0.02f, chrono.DX, Eps);
            Assert.AreEqual(Facing.Left, chrono.Facing);

            chrono.Stamina = 0;
            Hold(Cursor(-2f, 0f));
            Run(1);
            Assert.AreEqual(HeroState.Walking, chrono.State, "out of stamina, a far cursor walks");
            Assert.AreEqual(-0.2f * 0.02f, chrono.DX, Eps);
        }

        [Test]
        public void TheRunKeyRunsTowardANearCursor()
        {
            Hold(Cursor(0.15f, 0.05f, run: true));
            Run(1);
            Assert.AreEqual(HeroState.Running, chrono.State);
            Assert.AreEqual(0.15f * 0.02f, chrono.DX, Eps);
        }

        [Test]
        public void FacingIsTheLargerAxis()
        {
            foreach (var (dx, dy, facing) in new[]
                     {
                         (0.1f, 0.15f, Facing.Up), (0.15f, 0.1f, Facing.Right), (-0.15f, 0.1f, Facing.Left), (0.1f, -0.15f, Facing.Down),
                     })
            {
                Hold(Cursor(dx, dy));
                Run(1);
                Assert.AreEqual(facing, chrono.Facing, dx + ", " + dy);
            }
        }

        [Test]
        public void TheHeroSlowsOnArrivalAndStopsWithin001OfTheCursor()
        {
            Hold(Cursor(0.5f, 0.3f));
            var target = (x: chrono.X + 0.5f, y: chrono.Y + 0.3f);
            var cursor = party.InputOf(PlayerId.One);
            var speeds = new System.Collections.Generic.List<float>();
            for (var i = 0; i < 600; i++)
            {
                Run(1);
                if (chrono.State == HeroState.Standing) break;
                speeds.Add((float)Math.Sqrt(chrono.DX * chrono.DX + chrono.DY * chrono.DY));
            }

            Assert.AreEqual(HeroState.Standing, chrono.State, "arrived and stood");
            Assert.AreEqual(target.x, chrono.X, 0.011f);
            Assert.AreEqual(target.y, chrono.Y, 0.011f);
            Assert.Greater(speeds[0], speeds[speeds.Count / 2], "slower near the cursor than far from it");

            // Still holding the button on the spot: it stays put.
            var x = chrono.X;
            Hold(cursor);
            Run(30);
            Assert.AreEqual(x, chrono.X, 1e-4f);
        }

        [Test]
        public void ReleasingTheMoveButtonStandsAtOnce()
        {
            Hold(Cursor(0.4f, 0f));
            Run(10);
            Assert.AreNotEqual(0f, chrono.DX);
            Hold(default);
            Run(1);
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.AreEqual(0f, chrono.DX);
        }

        // ---- animation -----------------------------------------------------------------------------------------------

        [Test]
        public void TheWalkCycleTakesANewFrameEvery013SecondsAndLoopsAfterSix()
        {
            Hold(MoveInput.Arrows(right: true));
            var frames = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 60 * 2; i++)
            {
                Run(1);
                if (frames.Count == 0 || frames[frames.Count - 1] != chrono.Sprite) frames.Add(chrono.Sprite);
            }

            var cycle = Registry.Heroes.Sprites.Walk(Facing.Right);
            // 0.13 s is 7.8 ticks: a new frame every 8 ticks, so the first frame and 15 more in 2 seconds.
            Assert.AreEqual(16, frames.Count);
            for (var i = 0; i < frames.Count; i++) Assert.AreEqual(cycle[i % 6], frames[i], "frame " + i);
        }

        [Test]
        public void TheRunCycleTakesANewFrameEvery01Seconds()
        {
            Hold(MoveInput.Arrows(up: true, run: true));
            Run(1);
            var changes = 0;
            var last = chrono.Sprite;
            for (var i = 0; i < 60; i++)
            {
                Run(1);
                if (chrono.Sprite != last) changes++;
                last = chrono.Sprite;
                StringAssert.StartsWith("gfx/hero/Chrono/run/run_up", chrono.Sprite);
            }

            Assert.AreEqual(10, changes, "six ticks a frame");
        }

        [Test]
        public void FacingUpOrDownWithASideArrowShowsTheSideCycleAtItsOwnRate()
        {
            Hold(MoveInput.Arrows(left: true, up: true, run: true));
            Run(1);
            var changes = 0;
            var last = chrono.Sprite;
            for (var i = 0; i < 60; i++)
            {
                Run(1);
                Assert.AreEqual(Facing.Up, chrono.Facing);
                StringAssert.StartsWith("gfx/hero/Chrono/run/run_left", chrono.Sprite);
                if (chrono.Sprite != last) changes++;
                last = chrono.Sprite;
            }

            // The original ran this case at the walk rate (BIBLE: fixed to the run rate).
            Assert.AreEqual(10, changes);

            // Letting go of the run key walks from the next tick (the arrows read the run flag before KeysUp clears it).
            Hold(MoveInput.Arrows(right: true, down: true));
            Run(1);
            Assert.AreEqual(HeroState.Running, chrono.State);
            Run(1);
            Assert.AreEqual(HeroState.Walking, chrono.State);
            StringAssert.StartsWith("gfx/hero/Chrono/walk/walk_right", chrono.Sprite);
        }

        [Test]
        public void MovingTowardTheCursorShowsTheFacingsCycle()
        {
            Hold(Cursor(0f, 0.5f));
            Run(1);
            StringAssert.StartsWith("gfx/hero/Chrono/run/run_up", chrono.Sprite);
            Hold(Cursor(-0.1f, 0f));
            Run(1);
            StringAssert.StartsWith("gfx/hero/Chrono/walk/walk_left", chrono.Sprite);
        }

        // ---- the game ------------------------------------------------------------------------------------------------

        [Test]
        public void StartingAndStoppingRaiseHeroStateChanged()
        {
            var seen = new System.Collections.Generic.List<HeroStateChanged>();
            game.EventRaised += e => { if (e is HeroStateChanged c) seen.Add(c); };
            Hold(MoveInput.Arrows(left: true));
            Run(5);
            Hold(default);
            Run(5);
            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual((0, HeroState.Walking, Facing.Left), (seen[0].Hero, seen[0].State, seen[0].Facing));
            Assert.AreEqual((0, HeroState.Standing, Facing.Left), (seen[1].Hero, seen[1].State, seen[1].Facing));
        }

        [Test]
        public void OnlyAPlayerInTheGameCanMove()
        {
            Assert.IsFalse(game.Submit(new SetMoveInput(new PlayerId(2), MoveInput.Arrows(left: true))).Accepted);
            Assert.AreEqual(0, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(1, party.LeaderOf(new PlayerId(2)));
        }

        [Test]
        public void PosesPlaceHeroesBetweenTheirLastTwoTicks()
        {
            var view = new WorldView(Registry.View, Registry.Props, Registry.Heroes);
            Hold(MoveInput.Arrows(right: true));
            Run(10);
            var half = view.Poses(party, 0.5f)[0];
            Assert.AreEqual((chrono.PreviousX + chrono.X) / 2f, half.X, Eps);
            Assert.AreEqual(chrono.Sprite, half.Sprite);
            Assert.AreEqual(chrono.X, view.Poses(party)[0].X);
            Assert.AreEqual(Registry.View.WeaponIdle, half.WeaponSprite);
        }
    }
}

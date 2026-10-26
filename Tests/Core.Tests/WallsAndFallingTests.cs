using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A7: the six hard-coded walls stop the heroes exactly as the original's checkWall pushed them back
    /// (RECONSTRUCTION 3.5), and a hero recalled to the party falls into place from z -1 (checkFall, recallHero; 3.7, 4.5).
    /// </summary>
    public class WallsAndFallingTests
    {
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
        static float HeroSize => Registry.Heroes.HeroSize;
        static WallCollisionTuning Collision => Registry.Walls.Collision;

        Game game;
        Party party;
        Hero chrono;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp() => Start(Registry.Walls, new Random(7));

        void Start(WallData walls, Random random)
        {
            game = new Game();
            party = new Party(Registry.Heroes, walls, random).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Leader(PlayerId.One);
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Hold(MoveInput input) => Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, input)).Accepted);

        /// <summary>Puts <paramref name="hero"/> where its wall test point is (x, y), still.</summary>
        static void PlaceTestPoint(Hero hero, float x, float y)
        {
            hero.X = hero.PreviousX = x - HeroSize * Collision.Probe.X;
            hero.Y = hero.PreviousY = y - HeroSize * Collision.Probe.Y - Collision.ProbeLift;
            var (px, py) = WallCollision.TestPoint(hero, HeroSize, Collision);
            Assert.AreEqual(x, px, 1e-5f);
            Assert.AreEqual(y, py, 1e-5f);
        }

        static (float X, float Y) Point(Hero hero) => WallCollision.TestPoint(hero, HeroSize, Collision);

        /// <summary>Holds the input for <paramref name="ticks"/> ticks, checking the test point after each.</summary>
        void Walk(MoveInput input, int ticks, Action<float, float> check)
        {
            Hold(input);
            for (var i = 0; i < ticks; i++)
            {
                game.Step();
                var (x, y) = Point(chrono);
                check(x, y);
            }
        }

        // ---- data ----------------------------------------------------------------------------------------------------

        [Test]
        public void TheCollisionAndFallFiguresAreTheOriginalsData()
        {
            Assert.AreEqual(0.5f, Collision.Probe.X);
            Assert.AreEqual(-0.5f, Collision.Probe.Y);
            Assert.AreEqual(0.01f, Collision.ProbeLift);
            Assert.AreEqual(2f, Collision.Reach);
            Assert.AreEqual(0.001f, Collision.Push);
            Assert.AreEqual(0.03f, Collision.AngledStop);

            var fall = Registry.Heroes.Fall;
            Assert.AreEqual(0.04f, fall.Rise);
            Assert.AreEqual(-1f, fall.RecallZ);
            Assert.AreEqual(0.1f, fall.RecallSpread);

            Assert.AreEqual("gfx/hero/Chrono/halt/halt_left.PNG", Registry.Heroes.Sprites.Halt(Facing.Left));
            Assert.AreEqual("gfx/hero/Chrono/halt/halt_right.PNG", Registry.Heroes.Sprites.Halt(Facing.Right));
            Assert.AreEqual("gfx/hero/Chrono/halt/halt_left.PNG", Registry.Heroes.Sprites.Halt(Facing.Up));
        }

        [Test]
        public void TheTestPointIsHalfAHeroAcrossAndDownRaisedByTheLift()
        {
            PlaceTestPoint(chrono, 0f, 0f);
            Assert.AreEqual(-0.072f, chrono.X, 1e-6f);
            Assert.AreEqual(0.062f, chrono.Y, 1e-6f);
        }

        // ---- one push, exactly as the original's -------------------------------------------------------------------

        [Test]
        public void AFlatWallWithinTwiceTheVelocitySetsItToPushAway()
        {
            PlaceTestPoint(chrono, 1.5f, 0.995f);
            chrono.DY = 0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(-0.001f, chrono.DY, "below the wall (1,1)-(2,1), heading up: pushed down");

            PlaceTestPoint(chrono, 1.5f, 1.004f);
            chrono.DY = -0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0.001f, chrono.DY, "above it, heading down: pushed up");

            PlaceTestPoint(chrono, 1.5f, 0.99f);
            chrono.DY = 0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0.003f, chrono.DY, "0.01 away is beyond twice 0.003: untouched");

            PlaceTestPoint(chrono, 2.6f, 0.995f);
            chrono.DY = 0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0.003f, chrono.DY, "x 2.6 is past the wall's end: untouched");
        }

        [Test]
        public void AStillHeroBesideAWallIsLeftAlone()
        {
            // The reach is twice the velocity, so a hero that is not moving is never pushed (unless exactly on the line).
            foreach (var y in new[] { 0.9999f, 1.0001f })
            {
                PlaceTestPoint(chrono, 1.5f, y);
                chrono.DX = chrono.DY = 0f;
                WallCollision.Apply(chrono, Registry.Walls, HeroSize);
                Assert.AreEqual((0f, 0f), (chrono.DX, chrono.DY));
            }
        }

        [Test]
        public void TheVerticalWallPushesSidewaysOnlyAlongItsLength()
        {
            PlaceTestPoint(chrono, 0.995f, 0.5f);
            chrono.DX = 0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(-0.001f, chrono.DX, "left of (1,0)-(1,1), heading right: pushed left");

            PlaceTestPoint(chrono, 1.004f, 0.5f);
            chrono.DX = -0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0.001f, chrono.DX, "right of it, heading left: pushed right");

            PlaceTestPoint(chrono, 0.995f, -0.1f);
            chrono.DX = 0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0.003f, chrono.DX, "below its end: untouched");
        }

        [Test]
        public void AnAngledWallPushesOnItsLineAndStopsSidewaysMotionNearIt()
        {
            // (2,1)-(2.5,2): y = 2x - 3, so at x 2.25 the line is at 1.5.
            PlaceTestPoint(chrono, 2.25f, 1.495f);
            chrono.DX = 0.003f;
            chrono.DY = 0.003f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(-0.001f, chrono.DY);
            Assert.AreEqual(0f, chrono.DX, "within 0.03 of an angled wall the x velocity is zeroed");

            PlaceTestPoint(chrono, 2.25f, 1.48f);
            chrono.DX = 0.006f;
            chrono.DY = 0f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0f, chrono.DY, "no vertical velocity, no vertical push");
            Assert.AreEqual(0f, chrono.DX, "0.02 from the line: still within 0.03");

            PlaceTestPoint(chrono, 2.25f, 1.45f);
            chrono.DX = 0.006f;
            WallCollision.Apply(chrono, Registry.Walls, HeroSize);
            Assert.AreEqual(0.006f, chrono.DX, "0.05 from the line: free");
        }

        // ---- walking into the walls ----------------------------------------------------------------------------------

        [Test]
        public void WalkingOrRunningUpIntoTheFlatWallNeverCrossesIt([Values(false, true)] bool run)
        {
            PlaceTestPoint(chrono, 1.5f, 0.8f);
            Walk(MoveInput.Arrows(up: true, run: run), 600, (x, y) => Assert.Less(y, 1f));
            Assert.Greater(Point(chrono).Y, 0.98f, "it walked right up to the wall");
        }

        [Test]
        public void WalkingDownOntoTheFlatWallFromAboveNeverCrossesIt()
        {
            PlaceTestPoint(chrono, 1.5f, 1.3f);
            Walk(MoveInput.Arrows(down: true), 600, (x, y) => Assert.GreaterOrEqual(y, 1f));
            Assert.Less(Point(chrono).Y, 1.02f);
        }

        [Test]
        public void WalkingRightIntoTheVerticalWallNeverCrossesIt()
        {
            PlaceTestPoint(chrono, 0.7f, 0.5f);
            Walk(MoveInput.Arrows(right: true, run: true), 600, (x, y) => Assert.Less(x, 1f));
            Assert.Greater(Point(chrono).X, 0.98f);
        }

        [Test]
        public void HeadingForACursorBeyondTheWallStopsAtIt()
        {
            // The right button aims through the wall (1,1)-(2,1) at a point above it.
            PlaceTestPoint(chrono, 1.5f, 0.5f);
            Walk(MoveInput.Toward(1.5f, 1.6f, run: true), 600, (x, y) => Assert.Less(y, 1f));
            // Running at the cursor is 0.02 x the 0.6 reach = 0.012 a tick, so it hovers within twice that of the wall.
            Assert.Greater(Point(chrono).Y, 1f - 0.024f - 1e-4f);
            Assert.AreNotEqual(HeroState.Standing, chrono.State, "it is still trying to get there");
        }

        [Test]
        public void WalkingUpIntoTheAngledWallNeverCrossesItsLine()
        {
            PlaceTestPoint(chrono, 3.2f, 1.2f);
            // (3,2)-(3.5,1.5): y = -x + 5, so at x 3.2 the line is at 1.8.
            Walk(MoveInput.Arrows(up: true), 600, (x, y) => Assert.Less(y, -x + 5f));
            Assert.Greater(Point(chrono).Y, 1.75f);
        }

        [Test]
        public void WithoutTheWallsTheSameWalkGoesStraightThrough()
        {
            Start(null, new Random(7));
            PlaceTestPoint(chrono, 1.5f, 0.8f);
            Walk(MoveInput.Arrows(up: true), 600, (x, y) => { });
            Assert.Greater(Point(chrono).Y, 1.5f);
        }

        [Test]
        public void TheWallsStopEveryHeroNotOnlyTheLeader()
        {
            var belmont = party.Heroes[1];
            PlaceTestPoint(belmont, 1.5f, 0.995f);
            belmont.DY = 0.003f;
            belmont.State = HeroState.Walking;
            belmont.MoveUp = true;
            for (var i = 0; i < 120; i++)
            {
                game.Step();
                Assert.Less(Point(belmont).Y, 1f);
            }
        }

        // ---- recall and falling --------------------------------------------------------------------------------------

        [Test]
        public void ARecalledHeroLandsBesideItsLeaderAtZMinusOne()
        {
            PlaceTestPoint(chrono, 0.4f, 0.3f);
            var result = game.Submit(new RecallHero(PlayerId.One, 2));
            Assert.IsTrue(result.Accepted, result.Reason);
            var flagg = party.Heroes[2];
            Assert.AreEqual(-1f, flagg.Z);
            Assert.IsTrue(flagg.Falling);
            Assert.That(flagg.X, Is.InRange(chrono.X - 0.05f, chrono.X + 0.05f));
            Assert.That(flagg.Y, Is.InRange(chrono.Y - 0.05f, chrono.Y + 0.05f));
            Assert.AreEqual(flagg.X, flagg.PreviousX, "placed, not eased in from where it was");

            var e = raised.OfType<HeroRecalled>().Single();
            Assert.AreEqual((2, 0, flagg.X, flagg.Y, -1f), (e.Hero, e.Leader, e.X, e.Y, e.Z));
        }

        [Test]
        public void TheSameRandomSeedRecallsToTheSamePlace()
        {
            game.Submit(new RecallHero(PlayerId.One, 1));
            var first = (party.Heroes[1].X, party.Heroes[1].Y);
            Start(Registry.Walls, new Random(7));
            game.Submit(new RecallHero(PlayerId.One, 1));
            Assert.AreEqual(first, (party.Heroes[1].X, party.Heroes[1].Y));
        }

        [Test]
        public void ARecallOfTheLeaderOrOfNoHeroIsRefused()
        {
            Assert.IsFalse(game.Submit(new RecallHero(PlayerId.One, 0)).Accepted);
            Assert.IsFalse(game.Submit(new RecallHero(PlayerId.One, 3)).Accepted);
            Assert.IsFalse(game.Submit(new RecallHero(PlayerId.One, -1)).Accepted);
            Assert.IsFalse(game.Submit(new RecallHero(new PlayerId(9), 1)).Accepted, "a player not in the game");
            Assert.AreEqual(0f, party.Heroes[1].Z);
        }

        [Test]
        public void AFallingHeroRisesFourHundredthsATickShowingHaltLeftThenStandsOnTheGround()
        {
            game.Submit(new RecallHero(PlayerId.One, 1));
            var belmont = party.Heroes[1];
            var halt = Registry.Heroes.Sprites.Halt(Facing.Left);
            var expected = -1f;
            var ticks = 0;
            while (expected < 0f)
            {
                game.Step();
                ticks++;
                expected += 0.04f;
                Assert.AreEqual(expected, belmont.Z, 1e-6f, "tick " + ticks);
                Assert.AreEqual(halt, belmont.Sprite, "tick " + ticks);
            }

            Assert.That(ticks, Is.InRange(25, 26), "one unit at 0.04 a tick");
            Assert.AreEqual(1, raised.OfType<HeroLanded>().Count(e => e.Hero == 1));

            game.Step();
            Assert.AreEqual(0f, belmont.Z, "the next tick sets it exactly on the ground");
            Assert.IsFalse(belmont.Falling);
            Assert.AreEqual(Registry.Heroes.Sprites.Stand(belmont.Facing), belmont.Sprite);

            for (var i = 0; i < 30; i++) game.Step();
            Assert.AreEqual(1, raised.OfType<HeroLanded>().Count(), "it lands once");
        }

        [Test]
        public void AFallingHeroStillMoves()
        {
            // checkFall only changes the depth and the picture: a falling leader (the Rune's CHANGE HERO Z -1) keeps walking.
            chrono.Z = chrono.PreviousZ = -1f;
            var startX = chrono.X;
            Hold(MoveInput.Arrows(right: true));
            for (var i = 0; i < 10; i++) game.Step();
            Assert.Greater(chrono.X, startX);
            Assert.Less(chrono.Z, 0f);
            Assert.AreEqual(Registry.Heroes.Sprites.Halt(Facing.Left), chrono.Sprite);
        }

        [Test]
        public void TheViewDrawsAFallingHeroAtItsDepthEasedBetweenTicks()
        {
            game.Submit(new RecallHero(PlayerId.One, 1));
            game.Step();
            var view = new WorldView(Registry.View, Registry.Props, Registry.Heroes);
            var belmont = party.Heroes[1];
            Assert.AreEqual(belmont.Z, view.Poses(party, 1f)[1].Z, 1e-6f);
            Assert.AreEqual(-1f, view.Poses(party, 0f)[1].Z, 1e-6f);
            Assert.AreEqual(-0.98f, view.Poses(party, 0.5f)[1].Z, 1e-5f);
        }
    }
}

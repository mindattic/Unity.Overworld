using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A8: the party of three follows its leader as the original's checkfollow steered it (run beyond 0.2, walk
    /// within it, stand within 0.05, recalled beside the leader beyond 1), crowding followers slide apart (checkFlocking),
    /// the leader is switched with [ and ] (changeHero) or a left click on a hero (checkCursorOverHero, actionHeroSelect),
    /// and the team / solo toggle (the ' key or the team button) turns following off so the others kneel
    /// (RECONSTRUCTION 4.5, 4.7 and 13).
    /// </summary>
    public class PartyFollowTests
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

        Game game;
        Party party;
        CameraControl control;
        Hero chrono, belmont, flagg;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(11)).Install(game);
            control = new CameraControl(Registry.View, party).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Heroes[0];
            belmont = party.Heroes[1];
            flagg = party.Heroes[2];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        static void Place(Hero hero, float x, float y)
        {
            hero.X = hero.PreviousX = x;
            hero.Y = hero.PreviousY = y;
        }

        void Run(int ticks)
        {
            for (var i = 0; i < ticks; i++) game.Step();
        }

        void Submit(ICommand command) => Assert.IsTrue(game.Submit(command).Accepted, command.GetType().Name);

        // ---- data ----------------------------------------------------------------------------------------------------

        [Test]
        public void TheFollowFlockingSelectionAndButtonFiguresAreTheOriginals()
        {
            var f = Registry.Heroes.Follow;
            Assert.IsTrue(f.TeamAtStart);
            Assert.AreEqual(1f, f.RecallRange);
            Assert.AreEqual(0.03f, f.RunSpeed);
            Assert.AreEqual(0.2f, f.WalkRange);
            Assert.AreEqual(0.02f, f.WalkSpeed);
            Assert.AreEqual(0.05f, f.StandRange);

            Assert.AreEqual(0.05f, Registry.Heroes.Flocking.Range);
            Assert.AreEqual(6f, Registry.Heroes.Flocking.Divisor);

            var s = Registry.Heroes.Selection;
            Assert.AreEqual((0.5f, -0.5f), (s.HeroPoint.X, s.HeroPoint.Y));
            Assert.AreEqual((0f, 0.03f), (s.HeroOffset.X, s.HeroOffset.Y));
            Assert.AreEqual((0.03f, -0.05f), (s.CursorOffset.X, s.CursorOffset.Y));
            Assert.AreEqual(0.03f, s.Range);

            var b = Registry.View.TeamButton;
            Assert.AreEqual((18f, 75f, 28f, 28f), (b.X, b.Y, b.Width, b.Height));
            Assert.AreEqual("gfx/gui/icon_team.PNG", b.Picture(true));
            Assert.AreEqual("gfx/gui/icon_solo.PNG", b.Picture(false));
            CollectionAssert.Contains(Registry.View.Files, b.TeamPicture);
            CollectionAssert.Contains(Registry.View.Files, b.SoloPicture);
        }

        [Test]
        public void ThePartyIsChronoBelmontAndFlaggAllOnChronosSprites()
        {
            CollectionAssert.AreEqual(new[] { "Chrono", "Belmont", "Flagg" }, party.Heroes.Select(h => h.Name));
            foreach (var h in party.Heroes) Assert.AreEqual(Registry.Heroes.Sprites.Stand(Facing.Right), h.Sprite, h.Name);
            Assert.IsTrue(party.Team);
            Assert.IsTrue(party.IsLed(game, 0));
            Assert.IsFalse(party.IsLed(game, 1));
            Assert.IsFalse(party.IsLed(game, 2));
        }

        // ---- following -------------------------------------------------------------------------------------------------

        [Test]
        public void BeyondPointTwoAFollowerRunsAtThreeHundredthsOfTheDistance()
        {
            // Belmont starts at (0.3, -0.05) from Chrono at (0, 0).
            game.Step();
            Assert.AreEqual(HeroState.Running, belmont.State);
            Assert.AreEqual(-0.3f * 0.03f, belmont.DX, 1e-7f);
            Assert.AreEqual(0.05f * 0.03f, belmont.DY, 1e-7f);
            Assert.AreEqual(Facing.Left, belmont.Facing, "the horizontal distance is the larger");
            Assert.AreEqual(0.3f - 0.009f, belmont.X, 1e-6f);

            // Flagg at (-0.3, 0.1) runs right and down.
            Assert.AreEqual(HeroState.Running, flagg.State);
            Assert.AreEqual(0.3f * 0.03f, flagg.DX, 1e-7f);
            Assert.AreEqual(-0.1f * 0.03f, flagg.DY, 1e-7f);
            Assert.AreEqual(Facing.Right, flagg.Facing);
        }

        [Test]
        public void WithinPointTwoAFollowerWalksAtTwoHundredthsOfTheDistance()
        {
            Place(belmont, 0.15f, 0.1f);
            game.Step();
            Assert.AreEqual(HeroState.Walking, belmont.State);
            Assert.AreEqual(-0.15f * 0.02f, belmont.DX, 1e-7f);
            Assert.AreEqual(-0.1f * 0.02f, belmont.DY, 1e-7f);
            Assert.AreEqual(Facing.Left, belmont.Facing);
        }

        [Test]
        public void TheRangesArePerAxisAndInclusive()
        {
            Place(belmont, 0.1f, 0.2f);
            game.Step();
            Assert.AreEqual(HeroState.Walking, belmont.State, "0.2 up is still within 0.2");

            Place(belmont, 0.04f, 0.3f);
            game.Step();
            Assert.AreEqual(HeroState.Running, belmont.State, "near on x but not on y runs");
            Assert.AreEqual(Facing.Down, belmont.Facing, "the vertical distance wins unless the horizontal is larger");
        }

        [Test]
        public void WithinFiveHundredthsAFollowerStandsStill()
        {
            Place(belmont, 0.04f, -0.05f);
            game.Step();
            Assert.AreEqual(HeroState.Standing, belmont.State);
            Assert.AreEqual(0f, belmont.DX);
            Assert.AreEqual(0f, belmont.DY);
            Assert.AreEqual(0.04f, belmont.X, 1e-7f);
            Assert.AreEqual(Facing.Up, belmont.Facing, "it still turns toward the leader (up: the vertical distance is the larger)");
        }

        [Test]
        public void FollowersShowTheWalkAndRunCycles()
        {
            Place(belmont, 0.15f, 0f);
            Run(2);
            StringAssert.StartsWith("gfx/hero/Chrono/walk/walk_left", belmont.Sprite);
            Place(flagg, 0f, -0.6f);
            Run(2);
            StringAssert.StartsWith("gfx/hero/Chrono/run/run_up", flagg.Sprite);
        }

        [Test]
        public void TheFollowersCatchUpWithAMovingLeaderAndSettleBesideIt()
        {
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true)));
            Run(120);
            Submit(new SetMoveInput(PlayerId.One, default));
            Run(600);
            foreach (var h in new[] { belmont, flagg })
            {
                Assert.AreEqual(HeroState.Standing, h.State, h.Name);
                Assert.That(Math.Abs(h.X - chrono.X), Is.LessThanOrEqualTo(0.05f), h.Name);
                Assert.That(Math.Abs(h.Y - chrono.Y), Is.LessThanOrEqualTo(0.05f), h.Name);
            }

            Assert.IsFalse(raised.OfType<HeroRecalled>().Any(), "a walking leader is never left a unit behind");
        }

        [Test]
        public void BeyondOneUnitAFollowerIsRecalledBesideTheLeaderAndFallsIn()
        {
            Place(chrono, 2f, 1f);
            game.Step();
            var recalled = raised.OfType<HeroRecalled>().ToList();
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, recalled.Select(r => r.Hero));
            foreach (var h in new[] { belmont, flagg })
            {
                Assert.That(Math.Abs(h.X - 2f), Is.LessThanOrEqualTo(0.05f), h.Name);
                Assert.That(Math.Abs(h.Y - 1f), Is.LessThanOrEqualTo(0.05f), h.Name);
                Assert.AreEqual(HeroState.Standing, h.State, h.Name + " lands within 0.05, so it stands");
                Assert.That(h.Z, Is.LessThan(0f), h.Name + " falls in from z -1");
            }

            Run(30);
            Assert.AreEqual(2, raised.OfType<HeroLanded>().Count());
            Assert.AreEqual(0f, belmont.Z);
        }

        [Test]
        public void OneAxisBeyondOneUnitIsEnoughToRecall()
        {
            Place(belmont, 0.2f, -1.01f);
            game.Step();
            Assert.AreEqual(1, raised.OfType<HeroRecalled>().Count(r => r.Hero == 1));
        }

        [Test]
        public void TheRecallUsesTheSeededPartyRandom()
        {
            var again = new Game();
            var other = new Party(Registry.Heroes, null, new Random(11)).Install(again);
            Assert.IsTrue(again.Submit(new JoinGame(PlayerId.One)).Accepted);
            Place(chrono, 3f, 0f);
            Place(other.Heroes[0], 3f, 0f);
            game.Step();
            again.Step();
            Assert.AreEqual(belmont.X, other.Heroes[1].X);
            Assert.AreEqual(flagg.Y, other.Heroes[2].Y);
        }

        [Test]
        public void AFollowerInBattleModeIsNotSteered()
        {
            belmont.Mode = HeroMode.Battle;
            game.Step();
            Assert.AreEqual(HeroState.Standing, belmont.State);
            Assert.AreEqual(0.3f, belmont.X, 1e-7f);
        }

        // ---- team / solo ---------------------------------------------------------------------------------------------

        [Test]
        public void SoloMakesTheFollowersKneelWhereTheyAre()
        {
            Submit(new ToggleTeam(PlayerId.One));
            Assert.IsFalse(party.Team);
            var changed = raised.OfType<TeamChanged>().Single();
            Assert.AreEqual((PlayerId.One, false), (changed.Player, changed.Team));

            Run(2);
            foreach (var h in new[] { belmont, flagg })
            {
                Assert.AreEqual(HeroState.Kneeling, h.State, h.Name);
                Assert.IsTrue(h.Kneeling, h.Name);
                Assert.AreEqual(Registry.Heroes.Sprites.Kneel(h.Facing), h.Sprite, h.Name);
                Assert.AreEqual(h.Definition.Start.X, h.X, h.Name);
            }

            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(left: true, run: true)));
            Run(200);
            Assert.AreEqual(belmont.Definition.Start.X, belmont.X, "in solo nobody follows, however far the leader goes");
            Assert.IsFalse(raised.OfType<HeroRecalled>().Any());
        }

        [Test]
        public void TeamAgainAndTheyFollowOnceMore()
        {
            Submit(new ToggleTeam(PlayerId.One));
            Run(5);
            Submit(new ToggleTeam(PlayerId.One));
            Assert.IsTrue(party.Team);
            game.Step();
            Assert.AreEqual(HeroState.Running, belmont.State);
            Assert.AreEqual(-0.009f, belmont.DX, 1e-7f);
        }

        // ---- flocking ------------------------------------------------------------------------------------------------

        [Test]
        public void TwoMovingFollowersWithinFiveHundredthsSlideApart()
        {
            Place(belmont, 0.5f, 0.3f);
            Place(flagg, 0.52f, 0.31f);
            belmont.State = flagg.State = HeroState.Running;
            belmont.DX = -0.01f;
            belmont.DY = -0.006f;
            flagg.DX = -0.012f;
            flagg.DY = -0.004f;

            party.Flock(new[] { true, false, false });

            // Each ordered pair once (Belmont then Flagg, Flagg then Belmont), so each is nudged twice: away from the other
            // along x by its |dY| / 6 and along y by its |dX| / 6.
            Assert.AreEqual(0.5f - 2 * 0.006f / 6, belmont.X, 1e-6f);
            Assert.AreEqual(0.3f - 2 * 0.01f / 6, belmont.Y, 1e-6f);
            Assert.AreEqual(0.52f + 2 * 0.004f / 6, flagg.X, 1e-6f);
            Assert.AreEqual(0.31f + 2 * 0.012f / 6, flagg.Y, 1e-6f);
        }

        [Test]
        public void StandingLeadingOrFallenHeroesDoNotFlock()
        {
            Place(belmont, 0.5f, 0.3f);
            Place(flagg, 0.52f, 0.31f);
            belmont.DX = flagg.DX = -0.01f;
            belmont.DY = flagg.DY = -0.01f;

            belmont.State = HeroState.Standing;
            flagg.State = HeroState.Running;
            party.Flock(new[] { true, false, false });
            Assert.AreEqual(0.5f, belmont.X, "a standing follower is not nudged");

            belmont.State = HeroState.Running;
            belmont.Health = 0;
            party.Flock(new[] { true, false, false });
            Assert.AreEqual(0.5f, belmont.X, "nor one with no health");

            belmont.Health = 10;
            party.Flock(new[] { true, true, false });
            Assert.AreEqual(0.5f, belmont.X, "nor a hero a player leads");

            Place(flagg, 0.56f, 0.31f);
            party.Flock(new[] { true, false, false });
            Assert.AreEqual(0.5f, belmont.X, "nor two more than 0.05 apart");
        }

        // ---- choosing the leader -------------------------------------------------------------------------------------

        [Test]
        public void TheRightBracketLeadsTheNextHeroAndTheLeftThePreviousWrapping()
        {
            Submit(new CycleLeader(PlayerId.One, 1));
            Assert.AreEqual(1, party.LeaderOf(PlayerId.One));
            Submit(new CycleLeader(PlayerId.One, 1));
            Assert.AreEqual(2, party.LeaderOf(PlayerId.One));
            Submit(new CycleLeader(PlayerId.One, 1));
            Assert.AreEqual(0, party.LeaderOf(PlayerId.One));
            Submit(new CycleLeader(PlayerId.One, -1));
            Assert.AreEqual(2, party.LeaderOf(PlayerId.One));

            var changes = raised.OfType<LeaderChanged>().ToList();
            Assert.AreEqual(4, changes.Count);
            Assert.IsTrue(changes.All(c => c.Cycled));
            Assert.AreEqual((0, 1), (changes[0].Previous, changes[0].Hero));
            Assert.IsFalse(game.Submit(new CycleLeader(PlayerId.One, 2)).Accepted);
        }

        [Test]
        public void ChangingHeroStandsBothCentresTheCameraAndSnapsTheCursor()
        {
            chrono.Running = true;
            belmont.Kneeling = true;
            belmont.State = HeroState.Kneeling;
            Submit(new CycleLeader(PlayerId.One, 1));
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.IsFalse(chrono.Running);
            Assert.AreEqual(HeroState.Standing, belmont.State);
            Assert.IsFalse(belmont.Kneeling);

            var view = control.For(PlayerId.One);
            Assert.IsTrue(view.Centring);
            Assert.AreEqual(belmont.X + HeroSize / 2, view.Cursor.X, 1e-6f);
            Assert.AreEqual(belmont.Y + HeroSize / 2, view.Cursor.Y, 1e-6f);
            Assert.AreEqual(view.Cursor.X, view.Cursor.PreviousX, "the cursor jumps, it is not eased");
        }

        [Test]
        public void TheNewLeaderTakesTheInputAndTheOldOneFollowsIt()
        {
            Submit(new CycleLeader(PlayerId.One, 1));
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(up: true)));
            game.Step();
            Assert.AreEqual(HeroState.Walking, belmont.State);
            Assert.AreEqual(Facing.Up, belmont.Facing);
            Assert.IsTrue(party.IsLed(game, 1));
            Assert.IsFalse(party.IsLed(game, 0));
            Assert.AreEqual(HeroState.Running, chrono.State, "Chrono is 0.3 from Belmont, so he runs to him");
            Assert.That(chrono.DX, Is.GreaterThan(0f));
        }

        [Test]
        public void TheCursorIsOverAHeroWithinThreeHundredthsOfItsPoint()
        {
            // The hero's point: x + size/2, y - size/2 + 0.03; the cursor's: x + 0.03, y - 0.05.
            float CursorX(Hero h, float off) => h.X + HeroSize / 2 - 0.03f + off;
            float CursorY(Hero h, float off) => h.Y - HeroSize / 2 + 0.03f + 0.05f + off;
            Assert.AreEqual(1, party.HeroAt(CursorX(belmont, 0f), CursorY(belmont, 0f)));
            Assert.AreEqual(2, party.HeroAt(CursorX(flagg, 0.029f), CursorY(flagg, -0.029f)));
            Assert.AreEqual(0, party.HeroAt(CursorX(chrono, 0f), CursorY(chrono, 0f)), "the leader counts too");
            Assert.AreEqual(-1, party.HeroAt(CursorX(belmont, 0.031f), CursorY(belmont, 0f)));
            Assert.AreEqual(-1, party.HeroAt(5f, 5f));
        }

        [Test]
        public void AClickOnAHeroMakesItTheLeaderAndCentresTheCamera()
        {
            belmont.Kneeling = true;
            Submit(new SelectHero(PlayerId.One, 1));
            Assert.AreEqual(1, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(HeroState.Standing, belmont.State);
            Assert.IsFalse(belmont.Kneeling);
            var changed = raised.OfType<LeaderChanged>().Single();
            Assert.AreEqual((1, 0, false), (changed.Hero, changed.Previous, changed.Cycled));
            Assert.IsTrue(control.For(PlayerId.One).Centring);
            Assert.AreEqual(HeroState.Standing, chrono.State, "in team mode the old leader is left as it was");
        }

        [Test]
        public void InSoloAClickedHeroLeadsAndTheOldLeaderKneels()
        {
            Submit(new ToggleTeam(PlayerId.One));
            Run(2);
            Submit(new SelectHero(PlayerId.One, 2));
            Assert.AreEqual(HeroState.Kneeling, chrono.State);
            Assert.IsTrue(chrono.Kneeling);
            Assert.AreEqual(HeroState.Standing, flagg.State);
            Assert.IsFalse(flagg.Kneeling);
            Run(2);
            Assert.AreEqual(HeroState.Standing, flagg.State);
            Assert.AreEqual(Registry.Heroes.Sprites.Kneel(chrono.Facing), chrono.Sprite);
        }

        [Test]
        public void AHeroInBattleModeIsChosenByAClickOnlyWithAFullGauge()
        {
            flagg.Mode = HeroMode.Battle;
            flagg.Stamina = flagg.MaxStamina - 1;
            Assert.IsFalse(game.Submit(new SelectHero(PlayerId.One, 2)).Accepted, "recharging");
            Assert.IsFalse(game.Submit(new SelectHero(PlayerId.One, 3)).Accepted);
            Assert.AreEqual(0, party.LeaderOf(PlayerId.One));
            flagg.Stamina = flagg.MaxStamina;
            Assert.IsTrue(game.Submit(new SelectHero(PlayerId.One, 2)).Accepted);
            Assert.AreEqual(2, party.LeaderOf(PlayerId.One));
        }

        [Test]
        public void TheTeamButtonsHitBoxIsTheOriginals()
        {
            var b = Registry.View.TeamButton;
            Assert.IsTrue(b.Contains(18f, 65f));
            Assert.IsTrue(b.Contains(46f, 91f));
            Assert.IsTrue(b.Contains(30f, 80f));
            Assert.IsFalse(b.Contains(17.9f, 80f));
            Assert.IsFalse(b.Contains(46.1f, 80f));
            Assert.IsFalse(b.Contains(30f, 64.9f));
            Assert.IsFalse(b.Contains(30f, 91.1f), "the box stops 12 px above the button's bottom");
        }

        // ---- a second player ------------------------------------------------------------------------------------------

        [Test]
        public void ASecondPlayerLeadsItsOwnHeroAndTheFollowersFollowTheFirst()
        {
            var two = new PlayerId(2);
            Submit(new JoinGame(two));
            Assert.AreEqual(1, party.LeaderOf(two));
            Assert.AreEqual(PlayerId.One, party.Captain(game));
            game.Step();
            Assert.AreEqual(HeroState.Standing, belmont.State, "Belmont is led, so he is not steered");
            Assert.AreEqual(HeroState.Running, flagg.State, "Flagg follows Chrono");

            Assert.IsFalse(game.Submit(new SelectHero(PlayerId.One, 1)).Accepted, "another player leads Belmont");
            Submit(new CycleLeader(PlayerId.One, 1));
            Assert.AreEqual(2, party.LeaderOf(PlayerId.One), "the cycle passes over Belmont");
        }
    }
}

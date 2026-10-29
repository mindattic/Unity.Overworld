using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A7 in Unity: the walls stop the drawn hero, and a recalled hero is drawn falling in from z -1 with the
    // imported halt_left picture until it stands on the ground.
    public class WallsAndFallingTests
    {
        ContentRegistry content;
        WorldViewPresenter presenter;
        Game game;
        Party party;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls, new System.Random(3)).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            presenter = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            presenter.Follow(party, game);
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null) Object.DestroyImmediate(presenter.gameObject);
        }

        [Test]
        public void BothHaltPicturesAreImported()
        {
            var library = new TextureLibrary();
            Assert.IsNotNull(library.Load(content.Heroes.Sprites.Halt(Facing.Left)));
            Assert.IsNotNull(library.Load(content.Heroes.Sprites.Halt(Facing.Right)));
        }

        [Test]
        public void ARecalledHeroIsDrawnFallingInWithHaltLeftThenStands()
        {
            Assert.IsTrue(game.Submit(new RecallHero(PlayerId.One, 1)).Accepted);
            var belmont = presenter.ObjectRenderer(DrawableKind.Hero, 1);
            game.Step();
            presenter.Refresh();
            Assert.Less(belmont.transform.position.z, -0.9f);
            Assert.AreEqual(content.Heroes.Sprites.Halt(Facing.Left), presenter.FileOf(belmont));
            Assert.That(belmont.transform.position.x, Is.InRange(party.Heroes[0].X - 0.05f, party.Heroes[0].X + 0.05f));

            for (var i = 0; i < 30; i++) game.Step();
            presenter.Refresh();
            Assert.AreEqual(0f, belmont.transform.position.z);
            Assert.AreEqual(content.Heroes.Sprites.Stand(party.Heroes[1].Facing), presenter.FileOf(belmont));
        }

        [Test]
        public void TheScriptedRecallReadsAHeroAndAnOptionalDelay()
        {
            Assert.IsTrue(OverworldBootstrap.TryRecall("1", out var hero, out var seconds));
            Assert.AreEqual((1, 0f), (hero, seconds));
            Assert.IsTrue(OverworldBootstrap.TryRecall("2,1.5", out hero, out seconds));
            Assert.AreEqual((2, 1.5f), (hero, seconds));
            Assert.IsFalse(OverworldBootstrap.TryRecall("x", out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryRecall("1,-2", out _, out _));
        }

        [Test]
        public void TheBootstrapsPartyWalksIntoTheWallAndStops()
        {
            // Chrono starts at (0, 0); the move button aims him through the vertical wall (1,0)-(1,1) at (1.5, 0.5).
            game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(1.5f, 0.5f, true)));
            for (var i = 0; i < 900; i++) game.Step();
            var chrono = party.Heroes[0];
            var (x, y) = WallCollision.TestPoint(chrono, content.Heroes.HeroSize, content.Walls.Collision);
            Assert.That(y, Is.InRange(0f, 1f), "it reached the wall along its length");
            Assert.That(x, Is.InRange(0.97f, 1f), "and stopped at it, on its left");
            Assert.AreNotEqual(HeroState.Standing, chrono.State, "still trying to reach the cursor");
        }
    }
}

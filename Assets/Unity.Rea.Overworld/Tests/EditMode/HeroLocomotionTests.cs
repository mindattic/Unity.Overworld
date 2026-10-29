using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A5 in Unity: the presenter draws the party where its locomotion puts it, with Chrono's walk and run
    // pictures, every one of them imported; the scripted-run arguments become the input the player would hold.
    public class HeroLocomotionTests
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
            party = new Party(content.Heroes).Install(game);
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
        public void EveryStandWalkAndRunPictureIsImported()
        {
            var library = new TextureLibrary();
            var sprites = content.Heroes.Sprites;
            foreach (var facing in new[] { Facing.Left, Facing.Right, Facing.Up, Facing.Down })
            {
                Assert.IsNotNull(library.Load(sprites.Stand(facing)), sprites.Stand(facing));
                foreach (var file in sprites.Walk(facing)) Assert.IsNotNull(library.Load(file), file);
                foreach (var file in sprites.Run(facing)) Assert.IsNotNull(library.Load(file), file);
                Assert.AreEqual(6, sprites.Walk(facing).Count);
                Assert.AreEqual(6, sprites.Run(facing).Count);
            }
        }

        [Test]
        public void TheDrawnChronoWalksRightWithTheArrow()
        {
            var chrono = presenter.ObjectRenderer(DrawableKind.Hero, 0);
            var startX = chrono.transform.position.x;
            game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true)));
            for (var i = 0; i < 60; i++) game.Step();
            presenter.Refresh();

            Assert.AreEqual(HeroState.Walking, party.Heroes[0].State);
            Assert.Greater(chrono.transform.position.x, startX + 0.1f);
            StringAssert.Contains("walk_right", presenter.FileOf(chrono));
            Assert.IsTrue(chrono.enabled);
        }

        [Test]
        public void TheDrawnChronoRunsToAFarCursorAndStandsOnArrival()
        {
            var chrono = presenter.ObjectRenderer(DrawableKind.Hero, 0);
            game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(-0.8f, 0f)));
            game.Step();
            presenter.Refresh();
            Assert.AreEqual(HeroState.Running, party.Heroes[0].State);
            Assert.AreEqual(Facing.Left, party.Heroes[0].Facing);
            StringAssert.Contains("run_left", presenter.FileOf(chrono));

            for (var i = 0; i < 600; i++) game.Step();
            presenter.Refresh();
            Assert.AreEqual(HeroState.Standing, party.Heroes[0].State);
            Assert.AreEqual(content.Heroes.Sprites.Stand(party.Heroes[0].Facing), presenter.FileOf(chrono));
        }

        [Test]
        public void ScriptedArgumentsBecomeTheHeldInput()
        {
            Assert.IsNull(OverworldBootstrap.ScriptedInput(new[] { "Overworld.exe" }));
            Assert.AreEqual(MoveInput.Arrows(left: true, run: true),
                OverworldBootstrap.ScriptedInput(new[] { "x", OverworldBootstrap.HoldArgument, "left,run" }));
            Assert.AreEqual(MoveInput.Toward(0.5f, -0.25f, true),
                OverworldBootstrap.ScriptedInput(new[] { "x", OverworldBootstrap.MoveToArgument, "0.5,-0.25,run" }));
        }

        [Test]
        public void TheGroundPointIsWhereTheCameraRayMeetsZZero()
        {
            var cam = presenter.Camera;
            var centre = HeroInput.GroundPoint(cam, new Vector2(cam.pixelWidth / 2f, cam.pixelHeight / 2f));
            Assert.AreEqual(cam.transform.position.x, centre.x, 1e-3f);
            Assert.AreEqual(cam.transform.position.y, centre.y, 1e-3f);
        }
    }
}

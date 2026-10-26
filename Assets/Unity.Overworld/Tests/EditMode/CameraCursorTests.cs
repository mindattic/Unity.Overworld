using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A6 in Unity: the presenter's camera is the player's Core camera, and the world cursor is drawn over
    // everything with the original's pictures at its position plus the draw offset; the scripted view keys parse.
    public class CameraCursorTests
    {
        ContentRegistry content;
        WorldViewPresenter presenter;
        Game game;
        Party party;
        Viewpoint view;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes).Install(game);
            var control = new CameraControl(content.View, party).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            view = control.For(PlayerId.One);
            presenter = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes, view.Camera));
            presenter.Follow(party, game);
            presenter.ShowCursor(view.Cursor);
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null) Object.DestroyImmediate(presenter.gameObject);
        }

        [Test]
        public void EveryCursorPictureIsImported()
        {
            var library = new TextureLibrary();
            foreach (var file in content.View.Cursor.Sprites.Values)
                Assert.IsNotNull(library.Load(file), file + " is not imported under Art/Resources");
        }

        [Test]
        public void TheCursorIsDrawnOverEverythingWithTheArrowAtItsPositionPlusTheOffset()
        {
            var r = presenter.CursorRenderer;
            Assert.IsNotNull(r);
            Assert.IsTrue(r.enabled);
            Assert.AreEqual(WorldViewPresenter.CursorOrder, r.sortingOrder);
            Assert.AreEqual("gfx/cursor/arrow.PNG", presenter.FileOf(r));
            Assert.AreEqual(0.02f, r.transform.position.x, 1e-5f);
            Assert.AreEqual(-0.042f, r.transform.position.y, 1e-5f);
            var bounds = r.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(0.05f, bounds.size.x, 1e-5f);
            Assert.AreEqual(0.05f, bounds.size.y, 1e-5f);
        }

        [Test]
        public void ScrollingRightMovesTheDrawnCameraAndShowsTheScrollArrow()
        {
            game.Submit(new SetViewInput(PlayerId.One, ViewInput.Motion(288, 0)));
            for (var i = 0; i < 10; i++) game.Step();
            presenter.Refresh();

            Assert.AreEqual(0.2f, view.Camera.X, 1e-4f);
            Assert.AreEqual(view.Camera.X, presenter.Camera.transform.position.x, 0.03f, "the drawn camera is the player's camera (eased within a tick)");
            Assert.AreEqual("gfx/cursor/scroll_right.PNG", presenter.FileOf(presenter.CursorRenderer));
        }

        [Test]
        public void TheWheelZoomsTheDrawnCamera()
        {
            game.Submit(new SetViewInput(PlayerId.One, new ViewInput(0, 0, -1, false, false)));
            game.Step();
            game.Step();
            presenter.Refresh();
            Assert.AreEqual(-1.15f, presenter.Camera.transform.position.z, 1e-4f);
        }

        [Test]
        public void ExamineShowsTheEye()
        {
            game.Submit(new ToggleExamine(PlayerId.One));
            game.Step();
            presenter.Refresh();
            Assert.AreEqual("gfx/cursor/eye.PNG", presenter.FileOf(presenter.CursorRenderer));
        }

        [Test]
        public void TheScriptedViewKeysParse()
        {
            var keys = OverworldBootstrap.ScriptedViewKeys("zoom, shift,x,num6,num8");
            Assert.IsTrue(keys.Zoom);
            Assert.IsTrue(keys.Fast);
            Assert.IsTrue(keys.Isometric);
            Assert.IsFalse(keys.Deisometric);
            Assert.IsTrue(keys.StrafeRight);
            Assert.IsTrue(keys.StrafeUp);
            Assert.IsFalse(keys.StrafeLeft);
            Assert.IsFalse(keys.StrafeDown);
        }
    }
}

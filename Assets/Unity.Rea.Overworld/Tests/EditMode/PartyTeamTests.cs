using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A8 in Unity: the kneel, laid-down and team / solo pictures are imported, the team button is drawn at its
    // place on the screen showing icon_team or icon_solo, the drawn followers kneel in solo, and the left button chooses
    // the hero under the world cursor or toggles the team on the button.
    public class PartyTeamTests
    {
        ContentRegistry content;
        WorldViewPresenter presenter;
        TeamButtonPresenter button;
        HeroInput input;
        Game game;
        Party party;
        Viewpoint view;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, null, new System.Random(5)).Install(game);
            var control = new CameraControl(content.View, party).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            view = control.For(PlayerId.One);
            presenter = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes, view.Camera));
            presenter.Follow(party, game);
            presenter.ShowCursor(view.Cursor);
            button = TeamButtonPresenter.Create(presenter.Camera, content.View.TeamButton, content.View.Cursor, party);
            input = HeroInput.Create(game, PlayerId.One, presenter.Camera, view, party, content.View.TeamButton);
            input.UseDevices = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (input != null) Object.DestroyImmediate(input.gameObject);
            if (presenter != null) Object.DestroyImmediate(presenter.gameObject);
        }

        /// <summary>Moves the virtual mouse by (dx, dy) pixels and runs a tick, so the world cursor follows it.</summary>
        void MoveMouse(float dx, float dy)
        {
            Assert.IsTrue(game.Submit(new SetViewInput(PlayerId.One, ViewInput.Motion(dx, dy))).Accepted);
            game.Step();
        }

        [Test]
        public void TheKneelSleepAndTeamPicturesAreImported()
        {
            var library = new TextureLibrary();
            foreach (Facing f in System.Enum.GetValues(typeof(Facing)))
            {
                Assert.IsNotNull(library.Load(content.Heroes.Sprites.Kneel(f)), content.Heroes.Sprites.Kneel(f));
                Assert.IsNotNull(library.Load(content.Heroes.Sprites.Sleep(f)), content.Heroes.Sprites.Sleep(f));
            }

            Assert.IsNotNull(library.Load(content.View.TeamButton.TeamPicture));
            Assert.IsNotNull(library.Load(content.View.TeamButton.SoloPicture));
        }

        [Test]
        public void TheTeamButtonSitsAtItsPlaceOnTheScreenUnderTheCursor()
        {
            var r = button.Renderer;
            Assert.IsTrue(r.enabled);
            Assert.Less(r.sortingOrder, WorldViewPresenter.CursorOrder);
            Assert.Greater(r.sortingOrder, WorldViewPresenter.ObjectsOrder + 100);
            Assert.AreEqual("gfx/gui/icon_team.PNG", button.File);

            // The square's centre, (18 + 14, 75 + 14) of 640 x 480 with y down, as a viewport point.
            presenter.Refresh();
            button.Refresh();
            var centre = presenter.Camera.WorldToViewportPoint(r.bounds.center);
            Assert.AreEqual(32f / 640f, centre.x, 1e-3f);
            Assert.AreEqual(1f - 89f / 480f, centre.y, 1e-3f);
            var corner = presenter.Camera.WorldToViewportPoint(r.bounds.max);
            Assert.AreEqual(46f / 640f, corner.x, 1e-3f);
        }

        [Test]
        public void SoloShowsTheSoloIconAndDrawsTheFollowersKneeling()
        {
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            game.Step();
            game.Step();
            button.Refresh();
            presenter.Refresh();
            Assert.AreEqual("gfx/gui/icon_solo.PNG", button.File);
            var belmont = presenter.ObjectRenderer(DrawableKind.Hero, 1);
            Assert.AreEqual(content.Heroes.Sprites.Kneel(party.Heroes[1].Facing), presenter.FileOf(belmont));
        }

        [Test]
        public void ALeftClickOnTheTeamButtonTogglesTheTeam()
        {
            MoveMouse(30f - 320f, 80f - 240f);
            Assert.That(view.Cursor.MouseX, Is.InRange(18f, 46f));
            input.Click(true, true);
            Assert.IsFalse(party.Team);
            input.Click(true, false);
            Assert.IsFalse(party.Team, "once a press");
            input.Click(false, false);
            input.Click(true, true);
            Assert.IsTrue(party.Team);
        }

        [Test]
        public void ALeftClickOnAHeroMakesItTheLeader()
        {
            // Solo, so Belmont kneels where he starts, (0.3, -0.05); the cursor's point (x + 0.03, y - 0.05) onto his
            // point (x + 0.072, y - 0.072 + 0.03).
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            var perPixel = content.View.Cursor.WorldPerPixel;
            MoveMouse((0.3f + 0.072f - 0.03f) / perPixel, -(-0.05f - 0.072f + 0.03f + 0.05f) / perPixel);
            Assert.AreEqual(1, party.HeroAt(view.Cursor.X, view.Cursor.Y));

            input.Click(true, true);
            Assert.AreEqual(1, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(HeroState.Standing, party.Heroes[1].State);
            Assert.AreEqual(HeroState.Kneeling, party.Heroes[0].State, "in solo the old leader kneels");
            Assert.IsTrue(view.Centring);
        }

        [Test]
        public void TheScriptedSelectReadsAHeroOrAStepAndAnOptionalDelay()
        {
            Assert.IsTrue(OverworldBootstrap.TrySelect("2", out var command, out var seconds));
            Assert.AreEqual(2, ((SelectHero)command).Hero);
            Assert.AreEqual(0f, seconds);
            Assert.IsTrue(OverworldBootstrap.TrySelect("next,1.5", out command, out seconds));
            Assert.AreEqual((1, 1.5f), (((CycleLeader)command).Step, seconds));
            Assert.IsTrue(OverworldBootstrap.TrySelect("previous", out command, out _));
            Assert.AreEqual(-1, ((CycleLeader)command).Step);
            Assert.IsFalse(OverworldBootstrap.TrySelect("x", out _, out _));
            Assert.IsFalse(OverworldBootstrap.TrySelect("1,-1", out _, out _));
        }
    }
}

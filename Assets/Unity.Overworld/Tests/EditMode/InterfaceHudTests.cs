using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A20 in Unity: the options menu, the hero screen, the FPS counter, the pause banner, the turn bars, the 2D
    // arrow over the buttons (the world cursor hidden under it), the name over a hero the cursor is on, the hero-select
    // brackets and the loading picture, each drawn from the state and the data where the original drew it.
    public class InterfaceHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        Pickups pickups;
        InventoryControl pack;
        InterfaceControl ui;
        WorldClock clock;
        MessageBoard messages;
        Viewpoint view;
        WorldViewPresenter world;
        HudPresenter hud;

        static bool Shown(VisualElement e) => e != null && e.style.display == DisplayStyle.Flex;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls, new System.Random(3));
            pickups = new Pickups(content.Pickups, party, null, new System.Random(3)).Install(game);
            var control = new CameraControl(content.View, party);
            pack = new InventoryControl(pickups, content.Hud.Inventory, content.Hud.QuickBar, control).Install(game);
            party.Install(game);
            ui = new InterfaceControl(content.Hud, party).Install(game);
            control.Install(game);
            messages = new MessageBoard(content.Hud);
            clock = new WorldClock(content.Time, content.Heroes.Locomotion.GameSpeed, messages).Install(game);
            messages.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            view = control.For(PlayerId.One);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes, view.Camera));
            world.Follow(party, game);
            world.ShowCursor(view.Cursor);
            hud = HudPresenter.Create(content.Hud, clock, messages);
            hud.ShowVitals(party, PlayerId.One);
            hud.ShowPickups(pickups, view, world, PlayerId.One);
            hud.ShowInventory(pack, PlayerId.One);
            hud.ShowInterface(ui, PlayerId.One, content.View.TeamButton);
            world.CursorHidden = () => hud.Cursor2DShown;
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        void Submit(ICommand c) => Assert.IsTrue(game.Submit(c).Accepted, c.GetType().Name);

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
            world.Refresh();
            hud.Refresh();
        }

        [Test]
        public void EveryInterfacePictureIsImported()
        {
            var library = new TextureLibrary();
            var h = content.Hud;
            foreach (var file in h.Loading.Pictures.Concat(new[] { h.TurnBars.Frame, h.TurnBars.Ready, h.TurnBars.Fill, h.Cursor2D.Picture, h.Options.Box.Picture, h.HeroScreen.Box.Picture }))
                Assert.IsNotNull(library.Load(file), file);
        }

        [Test]
        public void TheOptionsMenuDrawsItsPageAndCursor()
        {
            Steps(1);
            Assert.IsFalse(Shown(hud.OptionsPicture.parent));
            Submit(new ToggleOptions(PlayerId.One));
            Steps(1);
            Assert.IsTrue(Shown(hud.OptionsPicture.parent));
            Assert.AreEqual("Options", hud.OptionsTitle.text);
            CollectionAssert.AreEqual(new[] { "Controls", "Video", "Audio", "Exit" }, hud.OptionRows.Where(Shown).Select(l => l.text).ToArray());
            Assert.AreEqual(60f, hud.OptionsCursor.style.top.value.value);

            Submit(new PressOptionsKey(PlayerId.One, OptionsKey.Enter));
            Steps(1);
            CollectionAssert.AreEqual(new[] { "Move Left = LeftArrow", "Move Right = RightArrow", "Move Up = UpArrow", "Move Down = DownArrow" },
                hud.OptionRows.Where(Shown).Select(l => l.text).ToArray());
            Submit(new PressOptionsKey(PlayerId.One, OptionsKey.Up));
            Steps(1);
            Assert.AreEqual(105f, hud.OptionsCursor.style.top.value.value);
        }

        [Test]
        public void TheHeroScreenWritesTheDateTheTimeAndTheExperience()
        {
            Submit(new ToggleHeroScreen(PlayerId.One));
            Steps(1);
            Assert.IsTrue(Shown(hud.HeroScreenPicture.parent));
            Assert.AreEqual(clock.DateText, hud.HeroDateLabel.text);
            Assert.AreEqual(clock.TimeAndPeriodText, hud.HeroTimeLabel.text);
            Assert.AreEqual("Experience: 0", hud.HeroExperienceLabel.text);
            Assert.AreEqual(25f, hud.HeroDateLabel.style.top.value.value);
        }

        [Test]
        public void TheFpsCounterIsRedAndHidesWhenOff()
        {
            Steps(1);
            Assert.IsTrue(Shown(hud.FpsLabel));
            StringAssert.EndsWith(" fps", hud.FpsLabel.text);
            Assert.AreEqual((Color)new Color32(255, 0, 25, 255), hud.FpsLabel.style.color.value);
            Submit(new ToggleFps(PlayerId.One));
            Steps(1);
            Assert.IsFalse(Shown(hud.FpsLabel), "the original wrote the stale rate while off (BIBLE)");
        }

        [Test]
        public void ThePauseBannerShowsWhileTheGameIsHeld()
        {
            Steps(1);
            Assert.IsFalse(Shown(hud.PauseLabel));
            Submit(new TogglePause(PlayerId.One));
            Steps(0);
            Assert.IsTrue(Shown(hud.PauseLabel));
            Assert.AreEqual("- PAUSE -", hud.PauseLabel.text);
            Assert.AreEqual(0, game.Advance(1.0));
        }

        [Test]
        public void TheTurnBarsShowEachGaugeUnderItsFrame()
        {
            Submit(new SetHeroVitals(PlayerId.One, 0, stamina: 30));
            Steps(1);
            var (fill, frame) = hud.TurnBars[0];
            Assert.IsTrue(Shown(frame));
            Assert.AreEqual("gfx/gui/stamina_bar.PNG", hud.TurnBarFiles[0]);
            Assert.AreEqual(content.Hud.TurnBars.FillWidth(party.Heroes[0].Stamina, 60) * hud.HorizontalScale, fill.style.width.value.value, 1e-3f);
            Assert.AreEqual("gfx/gui/ready_bar.PNG", hud.TurnBarFiles[1], "Belmont is full");
            Assert.IsTrue(Shown(hud.TurnBars[2].Frame));

            Submit(new ToggleTeam(PlayerId.One));
            Steps(1);
            Assert.IsFalse(Shown(hud.TurnBars[1].Frame), "solo: the leader's alone");
        }

        [Test]
        public void TheArrowShowsOverTheTeamButtonAndHidesTheWorldCursor()
        {
            Steps(1);
            Assert.IsFalse(hud.Cursor2DShown);
            Assert.IsTrue(world.CursorRenderer.enabled);
            Submit(new SetViewInput(PlayerId.One, ViewInput.Motion(30f - view.Cursor.MouseX, 80f - view.Cursor.MouseY)));
            Steps(1);
            Steps(0);
            Assert.IsTrue(hud.Cursor2DShown);
            Assert.IsTrue(Shown(hud.Cursor2D));
            Assert.AreEqual((view.Cursor.MouseX - 9f) * hud.HorizontalScale, hud.Cursor2D.style.left.value.value, 1e-3f);
            Assert.AreEqual(view.Cursor.MouseY + 4f, hud.Cursor2D.style.top.value.value, 1e-3f);
            Assert.IsFalse(world.CursorRenderer.enabled, "renderCursor left the world cursor out under the arrow");
        }

        [Test]
        public void TheNameShowsOverAHeroTheCursorIsOnButNotTheLeader()
        {
            var s = content.Heroes.Selection;
            var size = content.Heroes.HeroSize;
            void Over(int hero)
            {
                var h = party.Heroes[hero];
                Submit(new PlaceCursor(PlayerId.One, h.X + size * s.HeroPoint.X + s.HeroOffset.X - s.CursorOffset.X, h.Y + size * s.HeroPoint.Y + s.HeroOffset.Y - s.CursorOffset.Y));
                world.Refresh();
                hud.Refresh();
            }

            Over(1);
            Assert.IsTrue(Shown(hud.HeroHoverName));
            Assert.AreEqual(party.Heroes[1].Name, hud.HeroHoverName.text);
            Assert.AreEqual(".", hud.HeroHoverMarker.text);
            Over(0);
            Assert.AreEqual(0, party.HeroAt(view.Cursor.X, view.Cursor.Y));
            Assert.IsFalse(Shown(hud.HeroHoverName), "not over the leader");
        }

        [Test]
        public void TheChosenTargetIsBracketedInTheStatusBox()
        {
            Submit(new GiveItem(PlayerId.One, 0, 1));
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new UseItem(PlayerId.One));
            Assume.That(pack.For(PlayerId.One).Choosing, "the first pickup's item waits for its target in team mode");
            Steps(1);
            var target = party.Heroes[pack.For(PlayerId.One).Target].Name;
            CollectionAssert.Contains(hud.StatusLabels.Where(Shown).Select(l => l.text).ToList(), "|" + target + "|");
        }

        [Test]
        public void TheLoadingPictureCoversTheScreenUntilPlayBegins()
        {
            var file = content.Hud.Loading.Pictures[4];
            hud.ShowLoading(file);
            Assert.IsTrue(Shown(hud.LoadingPicture));
            Assert.AreEqual(file, hud.LoadingFile);
            Assert.AreEqual(480f, hud.LoadingPicture.style.height.value.value);
            hud.HideLoading();
            Assert.IsFalse(Shown(hud.LoadingPicture));
        }
    }
}

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
    /// U.OVW-US-A20: the interface (RECONSTRUCTION 2.3, 2.4, 5.3, 13). The options menu (F11 / Escape: Up and Down wrap
    /// through four rows, Left back to the main page, Enter opens Controls, Video or Audio or quits on Exit; while it is
    /// open the leader's arrows only mark, the move button, rest and pick-up do nothing), the hero screen and FPS toggles,
    /// the pause holding the game, the FPS counter's sampling, the turn bars, the hero-select brackets, the 2D arrow's
    /// hit test, the tutorial and the data of every widget; the three defects of the original, fixed.
    /// </summary>
    public class InterfaceTests
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
        static HudData Hud => Registry.Hud;

        Game game;
        Party party;
        Pickups pickups;
        InterfaceControl ui;
        MessageBoard messages;
        Hero chrono;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            party.Install(game);
            ui = new InterfaceControl(Hud, party).Install(game);
            messages = new MessageBoard(Hud).Install(game);
            Submit(new JoinGame(PlayerId.One));
            chrono = party.Heroes[0];
        }

        CommandResult Submit(ICommand c)
        {
            var r = game.Submit(c);
            Assert.IsTrue(r.Accepted, r.Reason);
            return r;
        }

        void Key(OptionsKey key) => Submit(new PressOptionsKey(PlayerId.One, key));

        InterfaceView View => ui.For(PlayerId.One);

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        [Test]
        public void TheContentHoldsEveryWidgetWhereTheOriginalDrewIt()
        {
            var o = Hud.Options;
            Assert.AreEqual((10f, 10f, 255f, 255f, "gfx/gui/menu.PNG"), (o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height, o.Box.Picture), "loadOptionMenu's quad");
            Assert.AreEqual((28f, 20f, "Options"), (o.Title.X, o.Title.Y, o.TitleText));
            Assert.AreEqual((255, 0, 255, 255), (o.Title.Colour.A, o.Title.Colour.R, o.Title.Colour.G, o.Title.Colour.B), "cyan");
            Assert.AreEqual((50f, 60f, ">", 4), (o.CursorX, o.RowX, o.CursorText, o.Rows));
            CollectionAssert.AreEqual(new[] { "Main", "Controls", "Video", "Audio" }, o.Pages.Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 60f, 75f, 90f, 200f }, o.Pages[0].Rows.Select(r => r.Y).ToArray());
            CollectionAssert.AreEqual(new[] { "Move Left = LeftArrow", "Move Right = RightArrow", "Move Up = UpArrow", "Move Down = DownArrow" },
                o.Pages[1].Rows.Select(r => r.Text).ToArray());
            Assert.IsTrue(o.Pages[0].Rows[3].Quits);

            Assert.AreEqual((590f, 4f, "fontArial"), (Hud.Fps.Text.X, Hud.Fps.Text.Y, Hud.Fps.Text.Font.Name));
            var fps = Hud.Fps.Text.Colour;
            Assert.AreEqual((255, 255, 0, 25), (fps.A, fps.R, fps.G, fps.B), "renderFPS wrote (255, 255, 0, 25): red, not yellow");
            Assert.AreEqual(("60 fps", 100), (Hud.Fps.Line(60), Hud.Fps.SampleMilliseconds));

            Assert.AreEqual((256f, 210f, "- PAUSE -", "fontArialNarrow24"), (Hud.Pause.X, Hud.Pause.Y, Hud.PauseText, Hud.Pause.Font.Name));

            var h = Hud.HeroScreen;
            Assert.AreEqual((20f, 20f, 600f, 430f, "gfx/gui/menu.PNG"), (h.Box.X, h.Box.Y, h.Box.Width, h.Box.Height, h.Box.Picture));
            Assert.AreEqual("Experience: 3500", h.ExperienceLine(3500));

            var t = Hud.TurnBars;
            Assert.AreEqual((60, 13), (t.Width, t.Height));
            CollectionAssert.AreEqual(new[] { (18f, 59f), (96f, 59f), (176f, 59f) }, t.Slots.Select(s => (s.X, s.Y)).ToArray());
            Assert.AreEqual(("gfx/gui/stamina_bar.PNG", "gfx/gui/ready_bar.PNG", "gfx/gui/stamina_fill.PNG"), (t.Frame, t.Ready, t.Fill));

            Assert.AreEqual(("gfx/cursor/arrow.PNG", 28f, -9f, 4f), (Hud.Cursor2D.Picture, Hud.Cursor2D.Size, Hud.Cursor2D.Offset.X, Hud.Cursor2D.Offset.Y));
            Assert.AreEqual(12, Hud.Loading.Pictures.Count);
            for (var i = 0; i < 12; i++) StringAssert.StartsWith("gfx/gui/load/load640_" + (i + 1) + ".", Hud.Loading.Pictures[i]);
        }

        [Test]
        public void TheTutorialIsTheOriginalsEightyEightLines()
        {
            var lines = Hud.Tutorial.Lines;
            Assert.AreEqual(88, lines.Count);
            Assert.AreEqual(("hi!", "This is a tutorial.", "...thanks again!"), (lines[0], lines[5], lines[87]));
            Assert.AreEqual(71, lines.Count(l => l.Length == 0), "the blanks spacing them out");
            CollectionAssert.Contains(lines.ToList(), "DELETE - Toggle INVENTORY", "the original said INSERT, which recalls the console's last script (BIBLE)");
            Assert.AreEqual("fontArial", Hud.Tutorial.Font.Name);

            messages.SayTutorial();
            Assert.AreEqual(88, messages.Waiting);
            Steps(1);
            Assert.AreEqual("hi!", messages.Shown[0].Caption);
            Assert.AreEqual((255, 255, 255), (messages.Shown[0].R, messages.Shown[0].G, messages.Shown[0].B));
        }

        [Test]
        public void TheOptionsMenuStepsWrapsAndOpensItsPages()
        {
            Assert.IsFalse(game.Submit(new PressOptionsKey(PlayerId.One, OptionsKey.Down)).Accepted, "a shut menu takes no keys");
            Submit(new ToggleOptions(PlayerId.One));
            Assert.IsTrue(View.OptionsOpen);
            Assert.AreEqual((0, 0, 60f), (View.OptionsPage, View.OptionsCursor, ui.CursorY(PlayerId.One)));

            Key(OptionsKey.Up);
            Assert.AreEqual(3, View.OptionsCursor, "Up from the top wraps to the last row");
            Assert.AreEqual(200f, ui.CursorY(PlayerId.One), "Exit's row");
            Key(OptionsKey.Down);
            Assert.AreEqual(0, View.OptionsCursor, "Down from the last wraps to the top");

            Key(OptionsKey.Enter);
            Assert.AreEqual("Controls", ui.PageOf(PlayerId.One).Name);
            Key(OptionsKey.Enter);
            Assert.AreEqual("Controls", ui.PageOf(PlayerId.One).Name, "Enter does nothing off the main page");
            Key(OptionsKey.Down);
            Key(OptionsKey.Down);
            Key(OptionsKey.Down);
            Assert.AreEqual((3, 105f), (View.OptionsCursor, ui.CursorY(PlayerId.One)), "the cursor steps the Controls rows");

            Key(OptionsKey.Left);
            Assert.AreEqual(0, View.OptionsPage, "Left goes back to the main page");
            Key(OptionsKey.Up);
            Key(OptionsKey.Up);
            Key(OptionsKey.Enter);
            Assert.AreEqual(("Video", 1, 75f), (ui.PageOf(PlayerId.One).Name, View.OptionsCursor, ui.CursorY(PlayerId.One)),
                "the Video page's cursor is on its rows, not at y 0 (BIBLE)");
            Key(OptionsKey.Left);
            Key(OptionsKey.Down);
            Key(OptionsKey.Enter);
            Assert.AreEqual(("Audio", 90f), (ui.PageOf(PlayerId.One).Name, ui.CursorY(PlayerId.One)),
                "the Audio page draws (the original read past its table and froze the screen; BIBLE)");

            Submit(new ToggleOptions(PlayerId.One));
            Assert.IsFalse(View.OptionsOpen);
            Submit(new ToggleOptions(PlayerId.One));
            Assert.AreEqual(("Audio", 2), (ui.PageOf(PlayerId.One).Name, View.OptionsCursor), "it opens where it was left");
        }

        [Test]
        public void ExitQuits()
        {
            Submit(new ToggleOptions(PlayerId.One));
            Key(OptionsKey.Up);
            var r = Submit(new PressOptionsKey(PlayerId.One, OptionsKey.Enter));
            Assert.IsTrue(r.Events.OfType<QuitRequested>().Any(), "Exit set bRunning = False");
            Key(OptionsKey.Down);
            Assert.IsFalse(Submit(new PressOptionsKey(PlayerId.One, OptionsKey.Enter)).Events.OfType<QuitRequested>().Any());
        }

        [Test]
        public void TheOpenMenuHoldsTheLeader()
        {
            Submit(new ToggleOptions(PlayerId.One));
            var x = chrono.X;
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true)));
            Steps(30);
            Assert.AreEqual(x, chrono.X, "the arrows work the menu, not the leader");
            Assert.IsTrue(chrono.MoveRight, "they still mark their direction");
            Assert.AreNotEqual(HeroState.Walking, chrono.State);

            Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(chrono.X + 0.5f, chrono.Y)));
            Steps(30);
            Assert.AreEqual(x, chrono.X, "actionMove tested bOptions");

            Assert.IsFalse(game.Submit(new Rest(PlayerId.One)).Accepted, "actionRest tested bOptions");

            Submit(new SetMoveInput(PlayerId.One, default(MoveInput).WithPickUp(true)));
            Steps(2);
            Assert.IsFalse(chrono.PickingUp || chrono.Kneeling, "actionPickup tested bOptions: Enter picks the row, the leader does not kneel");

            Submit(new SetMoveInput(PlayerId.One, default));
            Submit(new ToggleOptions(PlayerId.One));
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true)));
            Steps(30);
            Assert.Greater(chrono.X, x, "shut, the arrows walk again");
        }

        [Test]
        public void TheHeroScreenAndTheFpsCounterToggle()
        {
            Assert.IsFalse(View.HeroScreenOpen);
            Assert.IsTrue(View.FpsShown, "bDrawFPS starts on");
            Assert.IsTrue(Submit(new ToggleHeroScreen(PlayerId.One)).Events.OfType<HeroScreenToggled>().Single().Open);
            Assert.IsFalse(Submit(new ToggleFps(PlayerId.One)).Events.OfType<FpsToggled>().Single().Shown);
            Assert.IsTrue(View.HeroScreenOpen);
            Assert.IsFalse(View.FpsShown);
        }

        [Test]
        public void ThePauseHoldsTheWholeGame()
        {
            Assert.Greater(game.Advance(1.0), 0);
            messages.Add("hello", Hud.Font("fontArial"), 255, 255, 255);
            Submit(new TogglePause(PlayerId.One));
            Assert.IsTrue(ui.Paused);
            var tick = game.Tick;
            Assert.AreEqual(0, game.Advance(5.0), "oneFrameRender skipped every update while bPause was on");
            Assert.AreEqual(tick, game.Tick);
            Assert.AreEqual(1, messages.Waiting, "the messages wait too");
            Submit(new TogglePause(PlayerId.One));
            Assert.That(game.Advance(1.0), Is.InRange(59, 60), "the paused time is not made up");
            Assert.AreEqual(0, messages.Waiting);
        }

        [Test]
        public void ResetShutsTheMenusAndSaysTheTutorialAgain()
        {
            Submit(new ToggleOptions(PlayerId.One));
            Submit(new ToggleFps(PlayerId.One));
            Submit(new TogglePause(PlayerId.One));
            ui.Reset(messages);
            Assert.AreEqual((false, true, false), (View.OptionsOpen, View.FpsShown, ui.Paused));
            Assert.AreEqual(88, messages.Waiting);
        }

        [Test]
        public void TheFrameRateIsTheFramesOfEachSampleTimesTen()
        {
            var counter = new FrameRateCounter(100);
            counter.Frame(1000);
            Assert.AreEqual(0, counter.Rate, "the first sample has counted nothing");
            foreach (var ms in new[] { 1016, 1033, 1050, 1066, 1083 }) counter.Frame(ms);
            Assert.AreEqual(0, counter.Rate);
            counter.Frame(1100);
            Assert.AreEqual(60, counter.Rate, "six frames in 100 ms");
            counter.Frame(1150);
            counter.Frame(1199);
            Assert.AreEqual(60, counter.Rate, "held until the next sample");
            counter.Frame(1200);
            Assert.AreEqual(30, counter.Rate);
        }

        [Test]
        public void TheTurnBarsShowTheLeaderThenTheTeam()
        {
            var t = Hud.TurnBars;
            var bars = TurnBarsView.Bars(t, party, 0);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, bars.Select(b => b.Hero).ToArray());
            Assert.IsTrue(bars.All(b => b.FillWidth == 60 && b.Frame == t.Ready), "full: ready_bar over a full fill");

            Submit(new SetHeroVitals(PlayerId.One, 1, stamina: 30));
            Submit(new SelectHero(PlayerId.One, 1));
            bars = TurnBarsView.Bars(t, party, party.LeaderOf(PlayerId.One));
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, bars.Select(b => b.Hero).ToArray(), "the leader first, then the others in party order");
            Assert.AreEqual((18f, 59f, 30, t.Frame), (bars[0].X, bars[0].Y, bars[0].FillWidth, bars[0].Frame));
            Assert.AreEqual((96f, 176f), (bars[1].X, bars[2].X));

            Submit(new ToggleTeam(PlayerId.One));
            Assert.AreEqual(1, TurnBarsView.Bars(t, party, 1).Count, "solo: the leader's alone");

            Assert.AreEqual(8, t.FillWidth(1, 8), "7.5 rounds half to even");
            Assert.AreEqual(22, t.FillWidth(3, 8), "22.5 rounds half to even");
            Assert.AreEqual(0, t.FillWidth(-5, 60), "never below 0");
        }

        [Test]
        public void TheChosenTargetIsWrittenInTheHeroSelectBrackets()
        {
            var box = Hud.StatusBox;
            var light = new LightColour(255, 255, 255);
            var plain = StatusBoxView.Lines(box, party, 0, light);
            Assert.AreEqual("  " + chrono.Name + "  ", plain[0].Text);

            var lines = StatusBoxView.Lines(box, party, 0, light, chosen: 1);
            var belmont = party.Heroes[1].Name;
            var bracketed = lines.Single(l => l.Text == "|" + belmont + "|");
            Assert.AreEqual((255, 255, 255, 0), (bracketed.Colour.A, bracketed.Colour.R, bracketed.Colour.G, bracketed.Colour.B), "yellow");
            Assert.AreEqual(box.X + box.TeamSpacing + box.Name.X, bracketed.X);
            Assert.AreEqual("  " + chrono.Name + "  ", lines[0].Text);

            lines = StatusBoxView.Lines(box, party, 0, light, chosen: 0);
            Assert.AreEqual("|" + chrono.Name + "|", lines[0].Text);
        }

        [Test]
        public void TheLeftEdgeDoesNotScrollWithTheMouseOnAButton()
        {
            var g = new Game();
            var p = new Party(Registry.Heroes, null, new Random(5)).Install(g);
            var control = new CameraControl(Registry.View, p) { OverButton = (x, y) => Registry.View.TeamButton.Contains(x, y) || Hud.InventoryButton.Contains(x, y) }.Install(g);
            Assert.IsTrue(g.Submit(new JoinGame(PlayerId.One)).Accepted);
            var v = control.For(PlayerId.One);
            var start = v.Camera.X;
            Assert.IsTrue(g.Submit(new SetViewInput(PlayerId.One, ViewInput.Motion(-400f, 80f - v.Cursor.MouseY))).Accepted);
            for (var i = 0; i < 10; i++) g.Step();
            Assert.AreEqual((32f, 80f), (v.Cursor.MouseX, v.Cursor.MouseY), "at the left edge, on the team button");
            Assert.AreEqual(start, v.Camera.X, "checkScroll's left edge waited while overTeam");

            Assert.IsTrue(g.Submit(new SetViewInput(PlayerId.One, ViewInput.Motion(0f, 30f))).Accepted);
            for (var i = 0; i < 10; i++) g.Step();
            Assert.AreEqual(start, v.Camera.X, "and while overInventoryBox");

            Assert.IsTrue(g.Submit(new SetViewInput(PlayerId.One, ViewInput.Motion(0f, 100f))).Accepted);
            for (var i = 0; i < 10; i++) g.Step();
            Assert.Less(v.Camera.X, start, "off the buttons the left edge scrolls");
        }

        [Test]
        public void TheTwoDimensionalArrowShowsOverThePanelAndTheButtons()
        {
            var team = Registry.View.TeamButton;
            Assert.IsTrue(Cursor2DDefinition.Shown(Hud, team, false, 30, 70), "the team button (18, 75), its hit box from 65 to 91");
            Assert.IsFalse(Cursor2DDefinition.Shown(Hud, team, false, 30, 92), "between the buttons' hit boxes");
            Assert.IsTrue(Cursor2DDefinition.Shown(Hud, team, false, 30, 100), "the inventory button (18, 104)");
            Assert.IsFalse(Cursor2DDefinition.Shown(Hud, team, false, 400, 200), "over the shut panel's place");
            Assert.IsTrue(Cursor2DDefinition.Shown(Hud, team, true, 400, 200), "over the open panel");
            Assert.IsFalse(Cursor2DDefinition.Shown(Hud, team, true, 100, 300));
        }
    }
}

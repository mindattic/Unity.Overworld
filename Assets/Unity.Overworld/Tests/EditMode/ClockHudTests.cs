using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A9 in Unity: the world is drawn in the clock's light, the HUD writes the period and the time where the
    // original's renderStatus did, and a period's announcement is drawn as a message at the screen centre.
    public class ClockHudTests
    {
        ContentRegistry content;
        Game game;
        WorldClock clock;
        MessageBoard board;
        WorldViewPresenter world;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            board = new MessageBoard(content.Hud);
            clock = new WorldClock(content.Time, content.Heroes.Locomotion.GameSpeed, board).Install(game);
            board.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            world.LightBy(clock);
            hud = HudPresenter.Create(content.Hud, clock, board);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        [Test]
        public void TheHudWritesThePeriodAndTimeWhereTheOriginalDid()
        {
            Assert.AreEqual("Day", hud.PeriodLabel.text);
            Assert.AreEqual("9 : 00 AM", hud.TimeLabel.text);
            Assert.AreEqual(5f * hud.HorizontalScale, hud.TimeLabel.style.left.value.value, 1e-4f);
            Assert.AreEqual(423f, hud.TimeLabel.style.top.value.value, 1e-4f);
            Assert.AreEqual(410f, hud.PeriodLabel.style.top.value.value, 1e-4f);
            var colour = hud.TimeLabel.style.color.value;
            Assert.AreEqual(new Color(1f, 1f, 0f, 128f / 255f), colour, "the original's half-transparent yellow");
            Assert.AreEqual(FontStyle.Bold, hud.TimeLabel.style.unityFontStyleAndWeight.value);
            Assert.IsNotNull(hud.FaceOf(content.Hud.Time.Font), "a face is found for Calisto MT or its stand-ins");

            for (var i = 0; i < 61; i++) game.Step();
            hud.Refresh();
            Assert.AreEqual("9 : 01 AM", hud.TimeLabel.text);
        }

        [Test]
        public void TheWorldIsDrawnInTheClocksLight()
        {
            Assert.AreEqual((Color)new Color32(255, 255, 255, 255), world.Tint);
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 23)).Accepted);
            world.Refresh();
            Assert.AreEqual((Color)new Color32(128, 128, 255, 255), world.Tint, "night's blue");
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 19)).Accepted);
            world.Refresh();
            Assert.AreEqual((Color)new Color32(255, 128, 128, 255), world.Tint, "evening's red");
        }

        [Test]
        public void APeriodsAnnouncementIsDrawnAtTheCentre()
        {
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 20, 59)).Accepted);
            Assert.IsTrue(game.Submit(new SetTimeRatio(PlayerId.One, 0f)).Accepted);
            game.Step();
            hud.Refresh();
            Assert.AreEqual("9 : 00 PM", hud.TimeLabel.text);
            Assert.AreEqual("Night", hud.PeriodLabel.text);
            Assert.AreEqual(1, board.Shown.Count);
            var label = hud.MessageLabels[0];
            Assert.AreEqual("Night", label.text);
            Assert.AreEqual(DisplayStyle.Flex, label.style.display.value);
            Assert.AreEqual((320 - 5 * 3) * hud.HorizontalScale, label.style.left.value.value, 1e-4f);
            Assert.AreEqual(241f, label.style.top.value.value, 1e-4f);
            Assert.AreEqual(24f * 96f / 72f, label.style.fontSize.value.value, 1e-4f);

            for (var i = 0; i < 128; i++) game.Step();
            hud.Refresh();
            Assert.AreEqual(0, board.Shown.Count);
            Assert.AreEqual(DisplayStyle.None, label.style.display.value, "gone once faded");
        }
    }
}

using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A11 in Unity: while the leader lies down the HUD asks "Rest for n hours?" at the screen centre, a sleep
    // dims the world (not the HUD) to black and back, and the scripted rest arguments read as the bootstrap takes them.
    public class RestHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        WorldClock clock;
        SleepCycle sleep;
        WorldViewPresenter world;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls).Install(game);
            clock = new WorldClock(content.Time, content.Heroes.Locomotion.GameSpeed);
            sleep = new SleepCycle(party, clock).Install(game);
            clock.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            world.Follow(party, game);
            world.LightBy(clock);
            world.DimBy(sleep);
            hud = HudPresenter.Create(content.Hud, clock, null);
            hud.ShowVitals(party, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        [Test]
        public void TheRestPromptShowsWhileTheLeaderLiesDown()
        {
            Assert.AreEqual(DisplayStyle.None, hud.RestLabel.style.display.value, "standing: no prompt");
            Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.RestLabel.style.display.value, "kneeling: no prompt");

            Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.RestLabel.style.display.value);
            Assert.AreEqual("Rest for 1 hour?", hud.RestLabel.text);
            Assert.AreEqual(220f, hud.RestLabel.style.top.value.value, 1e-4f);
            Assert.AreEqual(220f * hud.HorizontalScale, hud.RestLabel.style.left.value.value, 1e-4f);
            Assert.AreEqual(Color.white, hud.RestLabel.style.color.value);

            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(up: true))).Accepted);
            for (var i = 0; i < 6; i++) game.Step();
            hud.Refresh();
            Assert.AreEqual(2, party.Heroes[0].SleepHours);
            Assert.AreEqual("Rest for 2 hours?", hud.RestLabel.text);
        }

        [Test]
        public void ASleepDimsTheWorldToBlackAndBack()
        {
            world.Refresh();
            Assert.AreEqual(Color.white, world.Tint);
            Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);
            Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);
            Assert.IsTrue(game.Submit(new Sleep(PlayerId.One, 2)).Accepted);
            for (var i = 0; i < 25; i++) game.Step();
            world.Refresh();
            Assert.Less(world.Tint.r, 0.6f);
            Assert.Greater(world.Tint.r, 0.4f);
            for (var i = 0; i < 26; i++) game.Step();
            world.Refresh();
            Assert.AreEqual(Color.black.r, world.Tint.r, 1e-4f, "black when the party sleeps");
            Assert.AreEqual(11, clock.Now.Hour);
            for (var i = 0; i < 51; i++) game.Step();
            world.Refresh();
            Assert.AreEqual(1f, world.Tint.r, 1e-4f, "faded back in");
        }

        [Test]
        public void TheScriptedRestArgumentsRead()
        {
            Assert.IsTrue(OverworldBootstrap.TryRest("kneel", out var presses, out var hours, out var seconds));
            Assert.AreEqual(1, presses);
            Assert.IsNull(hours);
            Assert.IsTrue(OverworldBootstrap.TryRest("lay,1.5", out presses, out hours, out seconds));
            Assert.AreEqual(2, presses);
            Assert.AreEqual(1.5f, seconds);
            Assert.IsTrue(OverworldBootstrap.TryRest("8", out presses, out hours, out _));
            Assert.AreEqual(8, hours);
            Assert.IsFalse(OverworldBootstrap.TryRest("25", out _, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryRest("nap", out _, out _, out _));

            Assert.IsTrue(OverworldBootstrap.TrySleepUntil("22", out var hour, out _));
            Assert.AreEqual(22, hour);
            Assert.IsTrue(OverworldBootstrap.TrySleepUntil("5,2", out hour, out seconds));
            Assert.AreEqual(5, hour);
            Assert.AreEqual(2f, seconds);
            Assert.IsFalse(OverworldBootstrap.TrySleepUntil("24", out _, out _));
        }
    }
}

using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A10 in Unity: the status box writes the leader's and the team's vitals, the three bottom-left bars fill by
    // the leader's health, mana and stamina, and each hero's status animation is drawn above it (tinted when poisoned).
    public class VitalsHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        WorldClock clock;
        WorldViewPresenter world;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls).Install(game);
            clock = new WorldClock(content.Time, content.Heroes.Locomotion.GameSpeed).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            world.Follow(party, game);
            world.LightBy(clock);
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
        public void EveryVitalsPictureIsImported()
        {
            var library = new TextureLibrary();
            foreach (var file in content.Heroes.Vitals.Files)
                Assert.IsNotNull(library.Load(file), file + " is not imported under Art/Resources");
            foreach (var file in content.Hud.Bars.Files)
                Assert.IsNotNull(library.Load(file), file + " is not imported under Art/Resources");
        }

        [Test]
        public void TheStatusBoxWritesTheLeaderAndTheTeam()
        {
            var labels = hud.StatusLabels;
            Assert.AreEqual(21, labels.Count);
            Assert.AreEqual("  Chrono  ", labels[0].text);
            Assert.AreEqual("HP 31", labels[1].text);
            Assert.AreEqual("  / 50", labels[2].text);
            Assert.AreEqual("AP 60", labels[5].text);
            Assert.AreEqual(16f, labels[1].style.top.value.value, 1e-4f);
            Assert.AreEqual(19f * hud.HorizontalScale, labels[1].style.left.value.value, 1e-4f);
            Assert.AreEqual(new Color(0f, 1f, 0f, 1f), labels[1].style.color.value, "green in the day's white light");
            Assert.AreEqual("  Belmont  ", labels[7].text);

            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 0, stamina: 7)).Accepted);
            hud.Refresh();
            Assert.AreEqual("AP 7", labels[5].text);
            Assert.AreEqual(DisplayStyle.None, labels[7].style.display.value, "solo shows only the leader");
        }

        [Test]
        public void TheBarsFillByTheLeadersVitals()
        {
            var bars = content.Hud.Bars;
            var scale = hud.HorizontalScale;
            Assert.AreEqual(46f * scale, hud.BarFill(bars.Health).style.width.value.value, 1e-3f, "31 of 50");
            Assert.AreEqual(45f * scale, hud.BarFill(bars.Mana).style.width.value.value, 1e-3f, "12 of 20");
            Assert.AreEqual(75f * scale, hud.BarFill(bars.Stamina).style.width.value.value, 1e-3f, "60 of 60");
            Assert.AreEqual(75f * scale, hud.BarFrame(bars.Health).style.width.value.value, 1e-3f);
            Assert.AreEqual(436f, hud.BarFrame(bars.Health).style.top.value.value, 1e-4f);
            Assert.AreEqual(464f, hud.BarFill(bars.Stamina).style.top.value.value, 1e-4f);
            Assert.IsNotNull(hud.BarFill(bars.Health).style.backgroundImage.value.texture);

            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 0, health: 0, stamina: 30)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.BarFill(bars.Health).style.display.value, "no fill at 0");
            Assert.AreEqual(38f * scale, hud.BarFill(bars.Stamina).style.width.value.value, 1e-3f, "37.5 rounds to even");
        }

        [Test]
        public void EachHerosStatusAnimationIsDrawnAboveIt()
        {
            var v = content.Heroes.Vitals;
            Assert.AreEqual(3, world.StatusIcons.Count);
            game.Step();
            world.Refresh();
            Assert.IsFalse(world.StatusIcons[0].enabled, "Fine shows nothing");

            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 0, stamina: 1)).Accepted);
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 2, status: HeroStatus.Poisoned)).Accepted);
            game.Step();
            world.Refresh();
            var chrono = party.Heroes[0];
            var icon = world.StatusIcons[0];
            Assert.IsTrue(icon.enabled);
            Assert.AreEqual(v.ExhaustedIcon[0], world.FileOf(icon));
            Assert.AreEqual(chrono.X + 0.045f, icon.transform.localPosition.x, 1e-4f);
            Assert.AreEqual(chrono.Y + 0.03f, icon.transform.localPosition.y, 1e-4f);
            Assert.AreEqual(WorldViewPresenter.StatusIconOrder, icon.sortingOrder);
            Assert.AreEqual(Color.white, icon.sharedMaterial.color);

            var poisoned = world.StatusIcons[2];
            Assert.IsTrue(poisoned.enabled);
            Assert.AreEqual(v.PoisonedIcon[0], world.FileOf(poisoned));
            Assert.AreEqual((Color)new Color32(v.PoisonedTint.R, v.PoisonedTint.G, v.PoisonedTint.B, 255), poisoned.sharedMaterial.color, "tinted apart from anger");
        }
    }
}

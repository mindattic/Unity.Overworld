using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A18 in Unity: End draws the team's weapons, the hero quads show the draw and battle-stance frames and the
    // weapon quads over them show the weapon's frames in step (blank out of battle), and the status box writes each
    // battle hero's "NN %" gauge or "Ready!".
    public class BattleHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        WorldViewPresenter world;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls, new System.Random(3)).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            world.Follow(party, game);
            hud = HudPresenter.Create(content.Hud, null, null);
            hud.ShowVitals(party, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
            world.Refresh();
            hud.Refresh();
        }

        [Test]
        public void EveryDrawBattleAndWeaponPictureIsImported()
        {
            var library = new TextureLibrary();
            foreach (var side in new[] { Facing.Left, Facing.Right })
            {
                foreach (var file in content.Heroes.Sprites.Draw(side)) Assert.IsNotNull(library.Load(file), file);
                foreach (var file in content.Heroes.Sprites.Battle(side)) Assert.IsNotNull(library.Load(file), file);
            }

            foreach (var w in content.Weapons.Weapons)
            foreach (var file in w.Files)
                Assert.IsNotNull(library.Load(file), file);
        }

        [Test]
        public void TheWeaponIsBlankOutOfBattleAndDrawnInStepInIt()
        {
            // A second of play first, so the animation and refill clocks are past their first interval.
            Steps(60);
            var axe = world.ObjectRenderer(DrawableKind.Weapon, 0);
            Assert.AreEqual(content.View.WeaponIdle, world.FileOf(axe));

            Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);
            Steps(1);
            var chrono = party.Heroes[0];
            Assert.AreEqual(HeroState.Drawing, chrono.State);
            StringAssert.Contains("combat/draw/draw_right_2", world.FileOf(world.ObjectRenderer(DrawableKind.Hero, 0)));
            Assert.AreEqual("gfx/weapon/axe/draw/weapon_draw_right_2.PNG", world.FileOf(axe));
            StringAssert.Contains("gfx/weapon/steelsword/draw/", world.FileOf(world.ObjectRenderer(DrawableKind.Weapon, 1)));

            Steps(30);
            Assert.AreEqual(HeroState.Battle, chrono.State);
            Assert.AreEqual(content.Heroes.Sprites.Battle(Facing.Right)[chrono.Frame], world.FileOf(world.ObjectRenderer(DrawableKind.Hero, 0)));
            Assert.AreEqual(chrono.Weapon.Right[chrono.Frame], world.FileOf(axe));
            Assert.IsTrue(axe.enabled);
            Assert.Greater(axe.sortingOrder, world.ObjectRenderer(DrawableKind.Hero, 0).sortingOrder, "the weapon is drawn over its hero");

            Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);
            Steps(1);
            Assert.AreEqual(content.View.WeaponIdle, world.FileOf(axe));
        }

        [Test]
        public void TheStatusBoxWritesTheGaugeAndReady()
        {
            Steps(60);
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 0, stamina: 30)).Accepted);
            Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);
            Steps(1);
            var shown = hud.StatusLabels.Where(l => l.style.display == DisplayStyle.Flex).Select(l => l.text).ToList();
            CollectionAssert.Contains(shown, "52 %");
            Assert.AreEqual(2, shown.Count(t => t == "Ready!"), "Belmont and Flagg are full");
        }
    }
}

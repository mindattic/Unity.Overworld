using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A12 in Unity: every pickup is a quad of its item picture behind the heroes, hidden once picked up; the
    // selected pickup in reach and the one under the cursor show their names over the world in its light; examine mode
    // with the cursor over a pickup opens the examine box; and the scripted pickup arguments read as the bootstrap takes them.
    public class PickupHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        Pickups pickups;
        CameraControl control;
        WorldViewPresenter world;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls);
            pickups = new Pickups(content.Pickups, party, null, new System.Random(3)).Install(game);
            party.Install(game);
            control = new CameraControl(content.View, party).Install(game);
            control.WheelFirst = pickups.UseWheel;
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            var viewpoint = control.For(PlayerId.One);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes, viewpoint.Camera));
            world.Follow(party, game);
            world.ShowPickups(pickups);
            hud = HudPresenter.Create(content.Hud, null, null);
            hud.ShowVitals(party, PlayerId.One);
            hud.ShowPickups(pickups, viewpoint, world, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        void ReachFor(int index)
        {
            var p = pickups.All[index];
            var reach = content.Pickups.Reach;
            var item = content.Pickups.ItemSize;
            var size = party.Data.HeroSize;
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, p.X + item * reach.ItemPoint.X - size * reach.HeroPoint.X,
                p.Y + item * reach.ItemPoint.Y - size * reach.HeroPoint.Y)).Accepted);
            game.Step();
            for (var i = 0; i < 13 && pickups.Selected(PlayerId.One) != index; i++) game.Submit(new CyclePickup(PlayerId.One, 1));
            Assert.AreEqual(index, pickups.Selected(PlayerId.One));
        }

        [Test]
        public void EveryPickupIsAQuadOfItsPictureBehindTheHeroes()
        {
            Assert.AreEqual(373, world.PickupRenderers.Count);
            var root = world.PickupRenderers[0];
            Assert.AreEqual("gfx/items/health_root.PNG", world.FileOf(root));
            Assert.AreEqual(new Vector3(0f, -0.1f, 0f), root.transform.localPosition);
            Assert.AreEqual(WorldViewPresenter.PickupsOrder, root.sortingOrder);
            Assert.Less(world.PickupRenderers[372].sortingOrder, WorldViewPresenter.ObjectsOrder);
            Assert.Greater(root.sortingOrder, WorldViewPresenter.TilesOrder);
            Assert.IsTrue(root.enabled, "near the start, so drawn");
            Assert.AreEqual("gfx/items/plant2.PNG", world.FileOf(world.PickupRenderers[52]), "a turnip plant in the world");

            ReachFor(0);
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            world.Refresh();
            Assert.IsFalse(root.enabled, "picked up: not drawn");
        }

        [Test]
        public void TheSelectedPickupShowsItsNameOverTheWorld()
        {
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.SelectedName.style.display.value, "nothing in reach at the start");
            ReachFor(105);
            world.Refresh();
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.SelectedName.style.display.value);
            Assert.AreEqual("Grimoire de'Alchemiste", hud.SelectedName.text);
            Assert.AreEqual(".", hud.SelectedMarker.text);
            Assert.Greater(hud.SelectedMarker.style.fontSize.value.value, hud.SelectedName.style.fontSize.value.value, "the marker is drawn larger");
            Assert.Less(hud.SelectedName.style.top.value.value, hud.SelectedMarker.style.top.value.value, "the name sits above the marker");
            Assert.AreEqual(world.Tint.r, hud.SelectedName.style.color.value.r, 1e-4f, "in the world's light");
        }

        [Test]
        public void ExamineModeOverAPickupOpensTheExamineBox()
        {
            var p = pickups.All[51]; // the Rusty Sword
            var h = content.Pickups.Hover;
            Assert.IsTrue(game.Submit(new PlaceCursor(PlayerId.One, p.X + h.ItemOffset.X - h.CursorOffset.X, p.Y + h.ItemOffset.Y - h.CursorOffset.Y)).Accepted);
            hud.Refresh();
            Assert.AreEqual(-1, hud.Examined, "not in examine mode");
            Assert.AreEqual("Rusty Sword", hud.HoverName.text, "the name shows over the pickup under the cursor anyway");

            Assert.IsTrue(game.Submit(new ToggleExamine(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(51, hud.Examined);
            Assert.AreEqual(DisplayStyle.Flex, hud.ExamineBox.parent.style.display.value);
            Assert.AreEqual(112f, hud.ExamineBox.style.top.value.value, 1e-4f);
            Assert.AreEqual(292f * hud.HorizontalScale, hud.ExamineBox.style.left.value.value, 1e-4f);
            Assert.AreEqual("Rusty Sword", hud.ExamineLines[0].text);
            Assert.AreEqual("Weight: 5.3", hud.ExamineLines[3].text);
            Assert.AreEqual("Value: $50", hud.ExamineLines[4].text);
            Assert.AreEqual(132f, hud.ExamineLines[0].style.top.value.value, 1e-4f);

            Assert.IsTrue(game.Submit(new PlaceCursor(PlayerId.One, 60f, 60f)).Accepted);
            hud.Refresh();
            Assert.AreEqual(-1, hud.Examined);
            Assert.AreEqual(DisplayStyle.None, hud.ExamineBox.parent.style.display.value);
        }

        [Test]
        public void TheScriptedPickupArgumentsRead()
        {
            Assert.IsTrue(OverworldBootstrap.TryPickup("105", 373, out var index, out var seconds));
            Assert.AreEqual((105, 0f), (index, seconds));
            Assert.IsTrue(OverworldBootstrap.TryPickup("372,1.5", 373, out index, out seconds));
            Assert.AreEqual((372, 1.5f), (index, seconds));
            Assert.IsFalse(OverworldBootstrap.TryPickup("373", 373, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryPickup("-1", 373, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryPickup("egg", 373, out _, out _));
        }
    }
}

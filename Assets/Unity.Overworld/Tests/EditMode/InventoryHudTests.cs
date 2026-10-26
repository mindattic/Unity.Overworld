using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A13 in Unity: the inventory button shows the panel shut or open; the open panel draws its picture, each
    // stack's icon ten to a row with its count, the item and swap cursors and the selected stack's lines; the quick bar
    // shows the first ten stacks; a dropped item is a quad of its icon in the world until it is picked up again; the
    // chosen target is marked; "You are overburdened" shows while the leader is; and the scripted arguments read.
    public class InventoryHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        Pickups pickups;
        InventoryControl inventory;
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
            control = new CameraControl(content.View, party);
            inventory = new InventoryControl(pickups, content.Hud.Inventory, content.Hud.QuickBar, control).Install(game);
            party.Install(game);
            control.Install(game);
            var scripts = new ItemScripts(party, pickups.Inventory) { Control = inventory };
            inventory.Scripts = scripts;
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            var viewpoint = control.For(PlayerId.One);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes, viewpoint.Camera));
            world.Follow(party, game);
            world.ShowPickups(pickups);
            world.ShowTarget(inventory, PlayerId.One, content.Hud.Target);
            hud = HudPresenter.Create(content.Hud, null, null);
            hud.ShowVitals(party, PlayerId.One);
            hud.ShowPickups(pickups, viewpoint, world, PlayerId.One);
            hud.ShowInventory(inventory, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        void Submit(ICommand c) => Assert.IsTrue(game.Submit(c).Accepted);

        void Refresh()
        {
            world.Refresh();
            hud.Refresh();
        }

        [Test]
        public void TheOpenPanelShowsTheStacksTenToARowWithTheSelectedOnesLines()
        {
            Refresh();
            Assert.AreEqual("gfx/gui/inventory_Closed.PNG", hud.InventoryButtonFile);
            Assert.AreEqual(104f, hud.InventoryButton.style.top.value.value, 1e-4f);
            Assert.AreEqual(DisplayStyle.None, hud.PackPanel.parent.style.display.value, "shut at the start");

            Submit(new GiveItem(PlayerId.One, 6, 3)); // three potatoes
            foreach (var i in new[] { 0, 1, 2, 3, 4, 5, 51, 52, 101 }) Submit(new GiveItem(PlayerId.One, i));
            Submit(new GiveItem(PlayerId.One, 201, 12));
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new MoveInventoryCursor(PlayerId.One, 0));
            Refresh();

            Assert.AreEqual("gfx/gui/inventory_Open.PNG", hud.InventoryButtonFile);
            Assert.AreEqual(DisplayStyle.Flex, hud.PackPanel.parent.style.display.value);
            Assert.AreEqual(30f, hud.PackPanel.style.top.value.value, 1e-4f);
            Assert.AreEqual(283f * hud.HorizontalScale, hud.PackPanel.style.left.value.value, 1e-4f);
            Assert.AreEqual(400f, hud.PackPanel.style.height.value.value, 1e-4f);

            Assert.AreEqual(11, pickups.Inventory.Items.Count);
            Assert.AreEqual(DisplayStyle.Flex, hud.PackIcons[10].style.display.value);
            Assert.AreEqual(DisplayStyle.None, hud.PackIcons[11].style.display.value);
            Assert.AreEqual(38f, hud.PackIcons[0].style.top.value.value, 1e-4f);
            Assert.AreEqual(38f, hud.PackIcons[9].style.top.value.value, 1e-4f, "ten to a row");
            Assert.AreEqual(78f, hud.PackIcons[10].style.top.value.value, 1e-4f, "the eleventh starts the next row");
            Assert.AreEqual(291f * hud.HorizontalScale, hud.PackIcons[10].style.left.value.value, 1e-4f);
            Assert.AreEqual("3", hud.PackCounts[0].text, "three potatoes");
            Assert.AreEqual(DisplayStyle.None, hud.PackCounts[1].style.display.value, "one health root shows no count");
            Assert.AreEqual("12", hud.PackCounts[10].text);
            Assert.AreEqual(78f + 24f, hud.PackCounts[10].style.top.value.value, 1e-4f);
            Assert.AreEqual((291f + 12f) * hud.HorizontalScale, hud.PackCounts[10].style.left.value.value, 1e-4f, "past 9 the count starts 2 px left");

            Assert.AreEqual("Potato x 3", hud.ItemNameLabel.text);
            Assert.AreEqual("+1 Health, +3 Stamina", hud.EffectLabel.text);
            Assert.AreEqual("0.1", hud.ItemWeightLabel.text);
            Assert.AreEqual("$2", hud.ValueLabel.text);
            Assert.AreEqual((283f + 321f - 6f) * hud.HorizontalScale, hud.ValueLabel.style.left.value.value, 1e-4f);
            StringAssert.StartsWith("Weight: ", hud.WeightLabel.text);
            StringAssert.EndsWith(" / 28.5", hud.WeightLabel.text);
            Assert.AreEqual("Gold: 0", hud.GoldLabel.text);
            Assert.AreEqual(38f, hud.ItemCursor.style.top.value.value, 1e-4f);
            Assert.AreEqual(DisplayStyle.None, hud.SwapCursor.style.display.value);

            Submit(new SwapItem(PlayerId.One));
            Submit(new MoveInventoryCursor(PlayerId.One, 10));
            Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.SwapCursor.style.display.value, "the swap mark stays on the first stack");
            Assert.AreEqual(38f, hud.SwapCursor.style.top.value.value, 1e-4f);
            Assert.AreEqual(78f, hud.ItemCursor.style.top.value.value, 1e-4f);
            Assert.AreEqual("Satchel x 12", hud.ItemNameLabel.text);
        }

        [Test]
        public void TheQuickBarShowsTheFirstTenStacks()
        {
            Submit(new GiveItem(PlayerId.One, 0, 2));
            Submit(new GiveItem(PlayerId.One, 3));
            Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.QuickBar.parent.style.display.value);
            Submit(new ToggleQuickBar(PlayerId.One));
            Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.QuickBar.parent.style.display.value);
            Assert.AreEqual(442f, hud.QuickBar.style.top.value.value, 1e-4f);
            Assert.AreEqual(DisplayStyle.Flex, hud.QuickIcons[1].style.display.value);
            Assert.AreEqual(DisplayStyle.None, hud.QuickIcons[2].style.display.value);
            Assert.AreEqual((238f + 1f + 40f) * hud.HorizontalScale, hud.QuickIcons[1].style.left.value.value, 1e-4f);
            Assert.AreEqual("2", hud.QuickCounts[0].text);
            Assert.Less(hud.QuickIcons[0].parent.IndexOf(hud.QuickIcons[0]), hud.QuickIcons[0].parent.IndexOf(hud.QuickBar), "the icons are drawn before the bar");
        }

        [Test]
        public void ADroppedItemLiesInTheWorldUntilItIsPickedUpAgain()
        {
            Submit(new PlaceHero(PlayerId.One, 0, 1f, 1f));
            Submit(new GiveItem(PlayerId.One, 6));
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new DropItem(PlayerId.One));
            Refresh();
            Assert.AreEqual(1, world.DropRenderers.Count);
            var r = world.DropRenderers[0];
            Assert.AreEqual("gfx/items/potato.PNG", world.FileOf(r), "it lies as its inventory icon");
            Assert.IsTrue(r.enabled);
            Assert.Less(r.sortingOrder, WorldViewPresenter.ObjectsOrder, "behind the heroes");
            Assert.Greater(r.sortingOrder, world.PickupRenderers[372].sortingOrder, "after the pickups");

            // Shut the pack, stand on it, select it and pick it up: its name showed, and it is gone from the world.
            Submit(new ToggleInventory(PlayerId.One));
            var d = pickups.Drops[0];
            var reach = content.Pickups.Reach;
            var item = content.Pickups.ItemSize;
            var size = party.Data.HeroSize;
            Submit(new PlaceHero(PlayerId.One, 0, d.X + item * reach.ItemPoint.X - size * reach.HeroPoint.X, d.Y + item * reach.ItemPoint.Y - size * reach.HeroPoint.Y));
            game.Step();
            Refresh();
            Assert.AreEqual(0, pickups.SelectedDrop(PlayerId.One));
            Assert.AreEqual(-1, pickups.Selected(PlayerId.One), "no pickup in reach to take first");
            Assert.AreEqual("Potato", hud.DropName.text);
            Assert.AreEqual(DisplayStyle.Flex, hud.DropName.style.display.value);
            Submit(new PickUp(PlayerId.One));
            game.Step();
            Refresh();
            Assert.IsFalse(r.enabled);
            Assert.AreEqual("Potato", pickups.Inventory.Items[0].Name);
        }

        [Test]
        public void TheChosenTargetIsMarkedOnItsHero()
        {
            Submit(new GiveItem(PlayerId.One, 0));
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new UseItem(PlayerId.One));
            Submit(new ChooseTarget(PlayerId.One, 1));
            Refresh();
            Assert.IsTrue(world.TargetRenderer.enabled);
            Assert.AreEqual("gfx/gui/select_hero.PNG", world.FileOf(world.TargetRenderer));
            var belmont = party.Heroes[1];
            Assert.AreEqual(new Vector3(belmont.X, belmont.Y, 0f), world.TargetRenderer.transform.localPosition);
            Assert.AreEqual("Belmont", hud.TargetName.text);
            Assert.AreEqual(DisplayStyle.Flex, hud.TargetName.style.display.value);

            Submit(new UseItem(PlayerId.One));
            Refresh();
            Assert.IsFalse(world.TargetRenderer.enabled);
            Assert.AreEqual(DisplayStyle.None, hud.TargetName.style.display.value);
            Assert.AreEqual(41, belmont.Health);
        }

        [Test]
        public void OverburdenedIsWrittenWhileTheLeaderIs()
        {
            Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.BurdenedLabel.style.display.value);
            Submit(new GiveItem(PlayerId.One, 51, 6)); // 31.8 of Rusty Swords
            game.Step();
            Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.BurdenedLabel.style.display.value);
            Assert.AreEqual("You are overburdened", hud.BurdenedLabel.text);
            Assert.AreEqual(240f, hud.BurdenedLabel.style.top.value.value, 1e-4f);
        }

        [Test]
        public void TheScriptedInventoryArgumentsRead()
        {
            Assert.IsTrue(OverworldBootstrap.TryGive("0*3,201*2,105", 373, out var gifts));
            CollectionAssert.AreEqual(new[] { (0, 3), (201, 2), (105, 1) }, gifts);
            Assert.IsFalse(OverworldBootstrap.TryGive("373", 373, out _));
            Assert.IsFalse(OverworldBootstrap.TryGive("1*0", 373, out _));

            Assert.IsTrue(OverworldBootstrap.TryInventoryStep("2:1,0.5", 'u', out var a, out var b, out var s));
            Assert.AreEqual((2, 1, 0.5f), (a, b, s));
            Assert.IsTrue(OverworldBootstrap.TryInventoryStep("3", 'u', out a, out b, out s));
            Assert.AreEqual((3, -1, 0f), (a, b, s));
            Assert.IsTrue(OverworldBootstrap.TryInventoryStep("0*2", 'd', out a, out b, out s));
            Assert.AreEqual((0, 2), (a, b));
            Assert.IsTrue(OverworldBootstrap.TryInventoryStep("1", 'd', out a, out b, out s));
            Assert.AreEqual(1, b, "one drop by default");
            Assert.IsTrue(OverworldBootstrap.TryInventoryStep("0,3,2", 's', out a, out b, out s));
            Assert.AreEqual((0, 3, 2f), (a, b, s));
            Assert.IsFalse(OverworldBootstrap.TryInventoryStep("12", 'q', out _, out _, out _), "the number keys are 0 to 9");
            Assert.IsFalse(OverworldBootstrap.TryInventoryStep("x", 'o', out _, out _, out _));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A13: the inventory panel over the party's pack (RECONSTRUCTION 4.6, 4.7, 8.3 and 8.5): Delete opens and
    /// closes it; the arrows move its cursor ten to a row; the run key marks and swaps; Backspace (or the move button)
    /// drops into the world, where the drop can be picked up again; Enter uses on the leader (solo), at once (a group or
    /// reusable item) or on a chosen target (team); reusable items stay; the 1 to 0 keys use the quick slots; and a pack
    /// over the party's 28.5 burdens every hero, who kneels.
    /// </summary>
    public class InventoryTests
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

        // Pickup indices of the items the tests carry.
        const int HealthRoot = 0, MoonBerry = 1, Mandrake = 2, AngelVine = 5, Potato = 6, RustySword = 51, BrambleNut = 104, Grimoire = 105, XimsScrub = 107, Satchel = 201, Gold = 112;

        Game game;
        Party party;
        Pickups pickups;
        InventoryControl control;
        SleepCycle sleep;
        WorldClock clock;
        ItemScripts scripts;
        List<IGameEvent> raised;
        PackView view;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            control = new InventoryControl(pickups, Registry.Hud.Inventory, Registry.Hud.QuickBar).Install(game);
            party.Install(game);
            var messages = new MessageBoard(Registry.Hud);
            clock = new WorldClock(Registry.Time, Registry.Heroes.Locomotion.GameSpeed, messages);
            sleep = new SleepCycle(party, clock).Install(game);
            clock.Install(game);
            scripts = new ItemScripts(party, pickups.Inventory, sleep) { Control = control };
            control.Scripts = scripts;
            pickups.Scripts = scripts;
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            view = control.For(PlayerId.One);
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        Inventory Pack => pickups.Inventory;

        void Give(int pickup, int count = 1) => Assert.IsTrue(game.Submit(new GiveItem(PlayerId.One, pickup, count)).Accepted);

        void Submit(ICommand c)
        {
            var r = game.Submit(c);
            Assert.IsTrue(r.Accepted, r.Reason);
        }

        void Hold(MoveInput input) => Submit(new SetMoveInput(PlayerId.One, input));

        /// <summary>One press of the held keys: held for a tick, then let go for a tick.</summary>
        void Press(MoveInput input)
        {
            Hold(input);
            game.Step();
            Hold(default);
            game.Step();
        }

        static MoveInput Enter => default(MoveInput).WithPickUp(true);

        void Open()
        {
            if (!view.Open) Submit(new ToggleInventory(PlayerId.One));
            Assert.IsTrue(view.Open);
        }

        string[] Names => Pack.Items.Select(i => i.Name).ToArray();

        [Test]
        public void DeleteOpensAndClosesThePanelButNotInBattle()
        {
            Assert.AreEqual(InputFocus.World, control.FocusOf(PlayerId.One));
            Submit(new ToggleInventory(PlayerId.One));
            Assert.IsTrue(view.Open);
            Assert.AreEqual(InputFocus.Inventory, control.FocusOf(PlayerId.One));
            Assert.IsTrue(raised.OfType<InventoryToggled>().Single().Open);
            Submit(new ToggleInventory(PlayerId.One));
            Assert.IsFalse(view.Open);

            Submit(new ToggleInventory(PlayerId.One));
            Submit(new Rest(PlayerId.One));
            Assert.IsFalse(view.Open, "resting shuts the pack (actionRest)");

            party.Heroes[0].Mode = HeroMode.Battle;
            Assert.IsFalse(game.Submit(new ToggleInventory(PlayerId.One)).Accepted, "the ring's Item entry opens it in battle");
            Assert.IsFalse(view.Open);
        }

        [Test]
        public void StacksAreByNameWithTheirWeightAndTheLimitIsTheParty()
        {
            Give(Potato, 3);
            Give(HealthRoot);
            Give(Potato, 2);
            CollectionAssert.AreEqual(new[] { "Potato", "Health Root" }, Names);
            Assert.AreEqual(5, Pack.Items[0].Amount);
            Assert.AreEqual(0.6f, Pack.Weight, 1e-5f);
            Assert.AreEqual(28.5f, control.WeightLimit, "Strength 7 + Agility 5 / 2 for each of three");
            Assert.AreEqual("Weight: 0.6 / 28.5", Registry.Hud.Inventory.WeightLine(Pack.Weight, control.WeightLimit));
            Assert.AreEqual("gfx/items/potato.PNG", Pack.Items[0].Texture, "the potato's own inventory icon");
            Assert.IsFalse(game.Submit(new GiveItem(PlayerId.One, Gold)).Accepted, "gold is never carried");
        }

        [Test]
        public void TheArrowsMoveTheCursorTenToARowLeftAndRightWrapping()
        {
            foreach (var i in new[] { 0, 1, 2, 3, 4, 5, 6, 51, 52, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 201, 371, 372 }) Give(i);
            Assert.AreEqual(23, Pack.Items.Count);
            Open();
            Press(MoveInput.Arrows(left: true));
            Assert.AreEqual(22, view.Cursor, "left from the first wraps to the last");
            Press(MoveInput.Arrows(right: true));
            Assert.AreEqual(0, view.Cursor, "right from the last wraps to the first");
            Press(MoveInput.Arrows(down: true));
            Assert.AreEqual(10, view.Cursor);
            Press(MoveInput.Arrows(down: true));
            Assert.AreEqual(20, view.Cursor);
            Press(MoveInput.Arrows(down: true));
            Assert.AreEqual(22, view.Cursor, "down past the end stops at the last");
            Press(MoveInput.Arrows(up: true));
            Assert.AreEqual(12, view.Cursor);
            Press(MoveInput.Arrows(up: true));
            Press(MoveInput.Arrows(up: true));
            Assert.AreEqual(0, view.Cursor, "up past the top stops at the first");

            // A held arrow moves once a press.
            Hold(MoveInput.Arrows(right: true));
            for (var i = 0; i < 30; i++) game.Step();
            Assert.AreEqual(1, view.Cursor);
            Hold(default);
            game.Step();

            // The leader neither turns nor walks while the panel takes the arrows.
            Assert.AreEqual(HeroState.Standing, party.Heroes[0].State);
            Assert.AreEqual(0f, party.Heroes[0].DX);
        }

        [Test]
        public void AnEmptyPackKeepsTheCursorOnTheFirstSlot()
        {
            Open();
            Press(MoveInput.Arrows(left: true));
            Press(MoveInput.Arrows(down: true));
            Assert.IsTrue(control.UseWheel(PlayerId.One, -1));
            Assert.AreEqual(0, view.Cursor, "the original's cursor went to -1 here and the next pickup broke the panel (BIBLE)");
        }

        [Test]
        public void TheWheelMovesTheCursorBeforeItZooms()
        {
            Give(HealthRoot);
            Give(Potato);
            Give(Mandrake);
            Assert.IsFalse(control.UseWheel(PlayerId.One, 1), "shut, the wheel is the camera's (or the pickups')");
            Open();
            Assert.IsTrue(control.UseWheel(PlayerId.One, -1));
            Assert.AreEqual(1, view.Cursor, "a turn away moves right");
            Assert.IsTrue(control.UseWheel(PlayerId.One, 2));
            Assert.AreEqual(2, view.Cursor, "two turns toward move left twice, wrapping");
        }

        [Test]
        public void TheRunKeyMarksAStackAndASecondPressSwapsItWithTheCursors()
        {
            Give(HealthRoot);
            Give(Potato);
            Give(Mandrake);
            Open();
            Press(MoveInput.Arrows(run: true));
            Assert.IsTrue(view.Swapping);
            Assert.AreEqual(0, view.SwapFrom);
            Assert.IsFalse(party.Heroes[0].Running, "the run key swaps; the leader does not run");
            Press(MoveInput.Arrows(right: true));
            Press(MoveInput.Arrows(right: true));
            Press(MoveInput.Arrows(run: true));
            Assert.IsFalse(view.Swapping);
            CollectionAssert.AreEqual(new[] { "Mandrake", "Potato", "Health Root" }, Names);
            Assert.AreEqual(2, view.Cursor, "the cursor stays on its slot");

            // Enter with a swap marked swaps too (actionPickup's Else SwapItem).
            Press(MoveInput.Arrows(run: true));
            Press(MoveInput.Arrows(left: true));
            Press(Enter);
            CollectionAssert.AreEqual(new[] { "Mandrake", "Health Root", "Potato" }, Names);

            // Delete clears a mark.
            Press(MoveInput.Arrows(run: true));
            Submit(new ToggleInventory(PlayerId.One));
            Assert.IsFalse(view.Swapping);
        }

        [Test]
        public void BackspaceDropsOneByTheLeaderAndItCanBePickedUpAgain()
        {
            var chrono = party.Heroes[0];
            Submit(new PlaceHero(PlayerId.One, 0, 2f, 2f));
            Give(Potato, 2);
            Give(RustySword);
            var weight = Pack.Weight;
            Open();
            Submit(new DropItem(PlayerId.One));
            Assert.AreEqual(1, Pack.Items[0].Amount);
            Assert.AreEqual(weight - 0.1f, Pack.Weight, 1e-5f);
            var drop = pickups.Drops.Single();
            Assert.IsFalse(drop.PickedUp);
            Assert.AreEqual("Potato", drop.Name);
            Assert.AreEqual("gfx/items/potato.PNG", drop.File, "it lies as its inventory picture");
            Assert.That(drop.X, Is.InRange(chrono.X, chrono.X + 0.05f));
            Assert.That(drop.Y, Is.InRange(chrono.Y, chrono.Y + 0.05f));
            var dropped = raised.OfType<ItemDropped>().Single();
            Assert.AreEqual(("Potato", 0), (dropped.Name, dropped.Hero));

            // The move button drops too, with the pack open (once a press), and the leader does not head for the cursor.
            Press(MoveInput.Toward(5f, 5f));
            CollectionAssert.AreEqual(new[] { "Rusty Sword" }, Names, "the last potato's stack was removed and the list closed up");
            Assert.AreEqual(0, view.Cursor);
            Assert.AreEqual(2, pickups.Drops.Count);
            Assert.AreEqual(2f, chrono.X, 1e-6f);

            // Shut the pack, stand on the first drop and pick it up again: it goes back into the pack by name.
            Submit(new ToggleInventory(PlayerId.One));
            var reach = Registry.Pickups.Reach;
            var item = Registry.Pickups.ItemSize;
            var size = party.Data.HeroSize;
            Submit(new PlaceHero(PlayerId.One, 0, drop.X + item * reach.ItemPoint.X - size * reach.HeroPoint.X, drop.Y + item * reach.ItemPoint.Y - size * reach.HeroPoint.Y));
            game.Step();
            CollectionAssert.Contains(pickups.DropsInReach(PlayerId.One), drop.Index);
            for (var i = 0; i < 13 && pickups.SelectedDrop(PlayerId.One) != drop.Index; i++) Submit(new CyclePickup(PlayerId.One, 1));
            Hold(Enter);
            game.Step();
            Hold(default);
            game.Step();
            Assert.IsTrue(drop.PickedUp);
            CollectionAssert.AreEqual(new[] { "Rusty Sword", "Potato" }, Names);
            Assert.AreEqual("gfx/items/potato.PNG", Pack.Items[1].Texture);
            Assert.AreEqual("Potato", raised.OfType<DroppedItemCollected>().Single().Name);

            // A picked-up drop's slot takes the next drop (the original's 372 slots ran out; BIBLE).
            Open();
            Submit(new MoveInventoryCursor(PlayerId.One, 1));
            Submit(new DropItem(PlayerId.One));
            Assert.AreEqual(2, pickups.Drops.Count);
            Assert.AreEqual(drop.Index, raised.OfType<ItemDropped>().Last().Drop);
        }

        [Test]
        public void DroppingFromAnEmptyPackClosesItAndBackspaceStopsChoosingATarget()
        {
            Open();
            Submit(new DropItem(PlayerId.One));
            Assert.IsFalse(view.Open);
            Assert.IsEmpty(pickups.Drops);

            Give(HealthRoot);
            Submit(new QuickUse(PlayerId.One, 0));
            Assert.IsTrue(view.Choosing);
            Submit(new DropItem(PlayerId.One));
            Assert.IsFalse(view.Choosing);
            Assert.AreEqual(1, Pack.Items.Count, "nothing was used or dropped");
        }

        [Test]
        public void ARemovalClearsTheSwapMarkSoNoStackIsLostPastTheEnd()
        {
            Give(HealthRoot);
            Give(Potato);
            Open();
            Submit(new MoveInventoryCursor(PlayerId.One, 1));
            Submit(new SwapItem(PlayerId.One));
            Submit(new DropItem(PlayerId.One));
            Assert.IsFalse(view.Swapping, "the original kept the mark on the removed slot and the next swap lost a stack (BIBLE)");
            Submit(new SwapItem(PlayerId.One));
            Submit(new SwapItem(PlayerId.One));
            CollectionAssert.AreEqual(new[] { "Health Root" }, Names);
        }

        [Test]
        public void SoloEnterUsesTheItemOnTheLeaderAndItLeavesThePack()
        {
            Submit(new ToggleTeam(PlayerId.One));
            var chrono = party.Heroes[0];
            Give(HealthRoot, 2);
            Open();
            Press(Enter);
            Assert.AreEqual(41, chrono.Health, "31 + 10");
            Assert.AreEqual(1, Pack.Items[0].Amount);
            Assert.AreEqual(0.1f, Pack.Weight, 1e-5f);
            var used = raised.OfType<ItemUsed>().Single();
            Assert.AreEqual((0, "Health Root", true), (used.Target, used.Name, used.Consumed));
            Assert.IsTrue(view.Open, "a health root leaves the panel open");
            Assert.IsFalse(chrono.PickingUp, "Enter used; it did not kneel to pick up");
            Assert.AreEqual(HeroState.Standing, chrono.State);

            Press(Enter);
            Assert.AreEqual(50, chrono.Health, "an addition stops at the maximum");
            Assert.IsEmpty(Pack.Items);
        }

        [Test]
        public void InTeamModeASingleItemWaitsForItsTargetChosenWithLeftAndRight()
        {
            var belmont = party.Heroes[1];
            Give(HealthRoot);
            Open();
            Press(Enter);
            Assert.IsFalse(view.Open, "the panel closes while the target is chosen");
            Assert.IsTrue(view.Choosing);
            Assert.AreEqual(InputFocus.Target, control.FocusOf(PlayerId.One));
            Assert.AreEqual(0, view.Target, "the leader to begin with");
            Assert.AreEqual(1, Pack.Items.Count, "nothing used yet");

            Press(MoveInput.Arrows(right: true));
            Assert.AreEqual(1, view.Target);
            Press(MoveInput.Arrows(left: true));
            Press(MoveInput.Arrows(left: true));
            Assert.AreEqual(2, view.Target, "wrapping");
            Assert.IsTrue(control.UseWheel(PlayerId.One, -1));
            Assert.AreEqual(1, view.Target, "a turn away chooses the previous hero");
            Assert.AreEqual(0f, party.Heroes[0].DX, "left and right chose; the leader did not walk");

            Press(Enter);
            Assert.IsFalse(view.Choosing);
            Assert.AreEqual(41, belmont.Health);
            Assert.AreEqual(31, party.Heroes[0].Health);
            Assert.IsEmpty(Pack.Items);
            Assert.AreEqual(1, raised.OfType<ItemUsed>().Single().Target);
        }

        [Test]
        public void ASatchelIsAGroupItemOpenedAtOnceForItsTwentyFiveGold()
        {
            Give(Satchel, 2);
            Assert.AreEqual(0f, Pack.Weight);
            Open();
            Press(Enter);
            Assert.IsFalse(view.Choosing, "a group item asks for no target");
            Assert.AreEqual(25, Pack.Gold);
            Assert.AreEqual(1, Pack.Items[0].Amount);
            Assert.AreEqual(25, raised.OfType<GoldChanged>().Single().Gold);
        }

        [Test]
        public void TheMoonBerrySleepsThePartyUntilTenAndClosesThePanel()
        {
            Give(MoonBerry);
            Open();
            Press(Enter);
            Assert.IsFalse(view.Open, "CHANGE INVENTORY OPEN FALSE");
            Assert.IsEmpty(Pack.Items);
            var started = raised.OfType<SleepStarted>().Single();
            Assert.AreEqual(22, started.WakeHour);
            Assert.IsTrue(party.Heroes[0].Laying);
            for (var i = 0; i < 200 && sleep.Sleeping; i++) game.Step();
            Assert.AreEqual(22, clock.Now.Hour);
        }

        [Test]
        public void AReusableItemSurvivesItsUseAndAScriptThatCannotRunYetLeavesTheItem()
        {
            Give(Grimoire);
            Give(RustySword);
            Open();
            Press(Enter);
            Assert.AreEqual(2, Pack.Items.Count, "the book's commands come with the book story; it is not used up");
            Assert.AreEqual(1, raised.OfType<ScriptFailed>().Count());
            Assert.IsEmpty(raised.OfType<ItemUsed>());

            // With a runner that runs every script, the Reusable book stays and the Equipable sword is used up.
            control.Scripts = new Everything();
            Press(Enter);
            Assert.AreEqual(1, Pack.Items[0].Amount);
            Assert.IsFalse(raised.OfType<ItemUsed>().Single().Consumed);
            Submit(new ToggleTeam(PlayerId.One));
            Submit(new MoveInventoryCursor(PlayerId.One, 1));
            Press(Enter);
            CollectionAssert.AreEqual(new[] { "Grimoire de'Alchemiste" }, Names);
        }

        sealed class Everything : IScriptRunner
        {
            public bool Run(Game g, PlayerId p, int hero, string script, long tick, ICollection<IGameEvent> events) => true;
        }

        [Test]
        public void TheNumberKeysUseTheQuickSlots()
        {
            Give(HealthRoot);
            Give(Satchel);
            Submit(new ToggleQuickBar(PlayerId.One));
            Assert.IsTrue(view.QuickBar);
            Submit(new QuickUse(PlayerId.One, 5));
            Assert.IsFalse(view.Choosing, "no stack in slot 6: nothing happens");

            // Team: even a group item waits for its target from the quick bar (actionQuickItem).
            Submit(new QuickUse(PlayerId.One, 1));
            Assert.AreEqual(1, view.Cursor);
            Assert.IsTrue(view.Choosing);
            Submit(new ChooseTarget(PlayerId.One, 1));
            Submit(new UseItem(PlayerId.One));
            Assert.AreEqual(25, Pack.Gold);

            // Solo: on the leader at once (the original used the last chosen hero; BIBLE).
            Submit(new ToggleTeam(PlayerId.One));
            Submit(new QuickUse(PlayerId.One, 0));
            Assert.AreEqual(41, party.Heroes[0].Health);
            Assert.AreEqual(31, party.Heroes[1].Health);
            Assert.IsEmpty(Pack.Items);
            Assert.IsFalse(game.Submit(new QuickUse(PlayerId.One, 10)).Accepted);
        }

        [Test]
        public void AChangeOfLeaderMakesItTheTarget()
        {
            Assert.AreEqual(0, view.Target);
            Submit(new CycleLeader(PlayerId.One, 1));
            game.Step();
            Assert.AreEqual(1, view.Target);
        }

        [Test]
        public void APackOverThePartysLimitBurdensEveryHeroWhoKneels()
        {
            Give(RustySword, 5);
            game.Step();
            Assert.IsTrue(party.Heroes.All(h => !h.Burdened), "26.5 is within 28.5");
            Give(RustySword);
            game.Step();
            Assert.IsTrue(party.Heroes.All(h => h.Burdened));
            Assert.IsTrue(party.Heroes[0].Kneeling && party.Heroes[0].State == HeroState.Kneeling, "the leader kneels (the followers' checkfollow still steers them after it, as it did)");
            Assert.AreEqual(3, raised.OfType<HeroBurdened>().Count(e => e.Burdened));

            var chrono = party.Heroes[0];
            var x = chrono.X;
            Hold(MoveInput.Arrows(right: true));
            for (var i = 0; i < 30; i++) game.Step();
            Hold(MoveInput.Toward(chrono.X + 0.5f, chrono.Y));
            for (var i = 0; i < 30; i++) game.Step();
            Assert.AreEqual(x, chrono.X, 1e-6f, "a burdened hero cannot move");
            Hold(default);

            Open();
            Submit(new DropItem(PlayerId.One));
            game.Step();
            Assert.IsTrue(party.Heroes.All(h => !h.Burdened));
            Submit(new ToggleInventory(PlayerId.One));
            Hold(MoveInput.Arrows(right: true));
            for (var i = 0; i < 30; i++) game.Step();
            Assert.Greater(chrono.X, x, "unburdened, it walks again");
        }

        [Test]
        public void EnterWithThePackOpenDoesNotKneelToPickUp()
        {
            var chrono = party.Heroes[0];
            Open();
            Hold(Enter);
            for (var i = 0; i < 5; i++) game.Step();
            Assert.IsFalse(chrono.PickingUp);
            Assert.AreNotEqual(HeroState.Kneeling, chrono.State);
        }

        [Test]
        public void AClickOnAnotherIconOnlyMovesTheCursorThere()
        {
            var panel = Registry.Hud.Inventory;
            Assert.AreEqual(new DataPoint(291, 38), panel.IconAt(0));
            Assert.AreEqual(new DataPoint(291 + 32 * 9, 38), panel.IconAt(9));
            Assert.AreEqual(new DataPoint(291, 78), panel.IconAt(10));
            Assert.AreEqual(0, panel.SlotUnder(300, 50, 23));
            Assert.AreEqual(1, panel.SlotUnder(330, 50, 23));
            Assert.AreEqual(10, panel.SlotUnder(300, 90, 23));
            Assert.AreEqual(-1, panel.SlotUnder(300, 114, 23), "between rows (the original's band reached up to the row above; BIBLE)");
            Assert.AreEqual(-1, panel.SlotUnder(300, 90, 10), "no stack there");
            Assert.AreEqual(-1, panel.SlotUnder(100, 50, 23), "off the panel");

            // Through the game: a press with the virtual mouse over another icon moves the cursor; over the cursor's, it uses.
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            var camera = new CameraControl(Registry.View, party);
            control = new InventoryControl(pickups, panel, Registry.Hud.QuickBar, camera).Install(game);
            party.Install(game);
            camera.Install(game);
            control.Scripts = new ItemScripts(party, pickups.Inventory) { Control = control };
            Submit(new JoinGame(PlayerId.One));
            Submit(new ToggleTeam(PlayerId.One));
            view = control.For(PlayerId.One);
            Give(HealthRoot);
            Give(Potato);
            Open();
            var mouse = camera.For(PlayerId.One).Cursor;
            mouse.MouseX = 330;
            mouse.MouseY = 50;
            Press(Enter);
            Assert.AreEqual(1, view.Cursor);
            Assert.AreEqual(2, Pack.Items.Count, "the press only moved the cursor");
            Press(Enter);
            CollectionAssert.AreEqual(new[] { "Health Root" }, Names, "over the cursor's own icon the press uses it");
        }

        [Test]
        public void TheItemScriptsDoWhatTheirEffectsSay()
        {
            Submit(new ToggleTeam(PlayerId.One));
            var chrono = party.Heroes[0];
            Give(Mandrake);
            Give(XimsScrub);
            Give(BrambleNut);
            Give(AngelVine);
            Open();
            Press(Enter);
            Assert.AreEqual(HeroStatus.Poisoned, chrono.Status);
            Press(Enter);
            Assert.AreEqual(HeroStatus.Fine, chrono.Status);
            Submit(new SetHeroVitals(PlayerId.One, 0, health: 3, stamina: 10));
            Press(Enter);
            Assert.AreEqual((0, 25), (chrono.Health, chrono.Stamina), "-5 health stops at 0, +15 stamina");
            Press(Enter);
            Assert.AreEqual((50, 20, 60), (chrono.Health, chrono.Mana, chrono.Stamina), "the Angel Vine restores everything");
            Assert.IsEmpty(Pack.Items);
        }
    }
}

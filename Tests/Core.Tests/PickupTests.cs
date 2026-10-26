using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A12: the 372 pickups and the restored Nitro Bean lie at their places; the unpicked ones within 0.05 of the
    /// leader are in reach (at most 13, in index order) and one of them is selected (, and . or the wheel cycle them);
    /// holding the pick-up button kneels the leader and takes the selected one: gold pays out at once, anything else joins
    /// the pack by name; picked herbs and crops grow back after their growth time; the cursor finds the pickup under it
    /// for the name and the examine box (RECONSTRUCTION 8.1, 8.2 and 8.4).
    /// </summary>
    public class PickupTests
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

        Game game;
        Party party;
        Pickups pickups;
        Hero chrono;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            // checkItemsInRange and checkItemPickup ran after getInput and before updateHero: the pickups step first.
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            party.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Heroes[0];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void Hold(MoveInput input) => Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, input)).Accepted);

        static MoveInput PickUpHeld => default(MoveInput).WithPickUp(true);

        /// <summary>Places Chrono so pickup <paramref name="index"/> is in reach, and selects it.</summary>
        void ReachFor(int index)
        {
            var p = pickups.All[index];
            var reach = Registry.Pickups.Reach;
            var item = Registry.Pickups.ItemSize;
            var size = party.Data.HeroSize;
            var x = p.X + item * reach.ItemPoint.X - size * reach.HeroPoint.X;
            var y = p.Y + item * reach.ItemPoint.Y - size * reach.HeroPoint.Y;
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, x, y)).Accepted);
            game.Step();
            NUnit.Framework.Legacy.CollectionAssert.Contains(pickups.InReach(PlayerId.One), index);
            for (var i = 0; i < 13 && pickups.Selected(PlayerId.One) != index; i++)
                Assert.IsTrue(game.Submit(new CyclePickup(PlayerId.One, 1)).Accepted);
            Assert.AreEqual(index, pickups.Selected(PlayerId.One));
        }

        [Test]
        public void EveryPickupLiesAtItsPlaceAndTheScatterFollowsTheSeed()
        {
            Assert.AreEqual(373, pickups.All.Count);
            Assert.AreEqual((0f, -0.1f), (pickups.All[0].X, pickups.All[0].Y), "Health Root");
            Assert.AreEqual((-0.4f + 0.05f * 3, -0.5f - 0.05f * 1), (pickups.All[6 + 11].X, pickups.All[6 + 11].Y), "the potato field, 8 to a row");
            Assert.AreEqual((pickups.All[101].X, pickups.All[101].Y), (pickups.All[372].X, pickups.All[372].Y), "the Nitro Bean at the Coffee Bean's place");
            Assert.AreEqual("gfx/items/coffee_bean.PNG", pickups.All[372].File);
            Assert.AreEqual("gfx/items/plant1.PNG", pickups.All[6].File, "a potato plant in the world");
            Assert.IsTrue(pickups.All.All(p => !p.PickedUp));

            var again = new Pickups(Registry.Pickups, party, null, new Random(7));
            for (var i = 112; i <= 370; i++)
            {
                Assert.AreEqual(pickups.All[i].X, again.All[i].X);
                Assert.AreEqual(pickups.All[i].Y, again.All[i].Y);
                Assert.That(pickups.All[i].X, Is.InRange(-3f, 7f));
                Assert.That(pickups.All[i].Y, Is.InRange(-7f, 3f));
            }
        }

        [Test]
        public void ThePickupsWithinReachAreListedInIndexOrderAtMostThirteen()
        {
            // In the middle of the potato field the neighbours 0.05 apart are in reach, edges included.
            ReachFor(6 + 8 + 3);
            var listed = pickups.InReach(PlayerId.One);
            Assert.That(listed.Count, Is.InRange(2, 13));
            NUnit.Framework.Legacy.CollectionAssert.IsOrdered(listed);
            var reach = Registry.Pickups.Reach;
            var hx = chrono.X + party.Data.HeroSize * reach.HeroPoint.X;
            var hy = chrono.Y + party.Data.HeroSize * reach.HeroPoint.Y;
            foreach (var i in listed)
            {
                var p = pickups.All[i];
                Assert.IsTrue(WorldCamera.Near(p.X + 0.025f, hx, 0.05f) && WorldCamera.Near(p.Y - 0.025f, hy, 0.05f), p.ToString());
            }

            // Far from everything nothing is in reach and nothing is selected.
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, 50f, 50f)).Accepted);
            game.Step();
            Assert.IsEmpty(pickups.InReach(PlayerId.One));
            Assert.AreEqual(-1, pickups.Selected(PlayerId.One));
        }

        [Test]
        public void CommaAndPeriodCycleTheItemsInReachWrapping()
        {
            ReachFor(6 + 8 + 3);
            var listed = pickups.InReach(PlayerId.One).ToList();
            var first = pickups.Selected(PlayerId.One);
            var slot = listed.IndexOf(first);
            Assert.IsTrue(game.Submit(new CyclePickup(PlayerId.One, 1)).Accepted);
            Assert.AreEqual(listed[(slot + 1) % listed.Count], pickups.Selected(PlayerId.One));
            Assert.IsTrue(game.Submit(new CyclePickup(PlayerId.One, -1)).Accepted);
            Assert.AreEqual(first, pickups.Selected(PlayerId.One));
            for (var i = 0; i <= slot; i++) Assert.IsTrue(game.Submit(new CyclePickup(PlayerId.One, -1)).Accepted);
            Assert.AreEqual(listed[listed.Count - 1], pickups.Selected(PlayerId.One), "before the first is the last");
            Assert.IsInstanceOf<PickupSelected>(raised.Last());
            Assert.IsFalse(game.Submit(new CyclePickup(PlayerId.One, 2)).Accepted);
        }

        [Test]
        public void HoldingThePickUpButtonKneelsTheLeaderAndTakesTheSelectedOneIntoThePack()
        {
            ReachFor(0);
            Hold(PickUpHeld);
            game.Step();
            Assert.IsTrue(pickups.All[0].PickedUp);
            Assert.IsFalse(chrono.PickingUp, "the pickup completes in the tick it began");
            Assert.AreEqual(HeroState.Kneeling, chrono.State, "kneels while the button is held");
            Assert.AreEqual(party.Data.Sprites.Kneel(chrono.Facing), chrono.Sprite);
            var stack = pickups.Inventory.Find("Health Root");
            Assert.IsNotNull(stack);
            Assert.AreEqual(1, stack.Amount);
            Assert.AreEqual("gfx/items/health_root.PNG", stack.Texture);
            Assert.AreEqual(0.1f, pickups.Inventory.Weight, 1e-6f);
            var collected = raised.OfType<PickupCollected>().Single();
            Assert.AreEqual((0, "Health Root", false), (collected.Pickup, collected.Name, collected.Used));
            Assert.AreEqual(-1, pickups.Selected(PlayerId.One), "a picked pickup is no longer selected");

            // Held on, nothing more is taken (one pickup a press); let go, the hero stands.
            Steps(5);
            Assert.AreEqual(1, raised.OfType<PickupCollected>().Count());
            Hold(default);
            game.Step();
            Assert.AreEqual(HeroState.Standing, chrono.State);
        }

        [Test]
        public void ThePackStacksByNameAndCarriesTheWeight()
        {
            foreach (var i in new[] { 6, 7 })
            {
                ReachFor(i);
                Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
                game.Step();
                Assert.IsTrue(pickups.All[i].PickedUp);
            }

            var potato = pickups.Inventory.Find("Potato");
            Assert.AreEqual(2, potato.Amount);
            Assert.AreEqual("gfx/items/potato.PNG", potato.Texture, "the potato's own inventory icon");
            Assert.AreEqual(1, pickups.Inventory.Items.Count);
            Assert.AreEqual(0.2f, pickups.Inventory.Weight, 1e-6f);
        }

        [Test]
        public void GoldPaysOutAtOnceAndASatchelGoesIntoThePack()
        {
            ReachFor(112);
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            Assert.AreEqual(5, pickups.Inventory.Gold, "Gold +5 at once");
            Assert.IsNull(pickups.Inventory.Find("Gold"), "gold is never carried");
            Assert.IsTrue(raised.OfType<PickupCollected>().Single().Used);
            Assert.AreEqual((5, 0), raised.OfType<GoldChanged>().Select(g => (g.Gold, g.Previous)).Single());

            ReachFor(251);
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            Assert.AreEqual(6, pickups.Inventory.Gold, "Gold +1");

            // A satchel is a GroupItem, as the original made it: carried, paying its 25 when used (story A13).
            ReachFor(201);
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            Assert.AreEqual(6, pickups.Inventory.Gold);
            Assert.AreEqual(1, pickups.Inventory.Find("Satchel").Amount);
        }

        [Test]
        public void TheCurrencyScriptAddsOrAssigns()
        {
            var events = new List<IGameEvent>();
            var scripts = new CurrencyScripts(pickups.Inventory);
            Assert.IsTrue(scripts.Run(game, PlayerId.One, 0, "CHANGE HERO GOLD +25", 0, events));
            Assert.IsTrue(scripts.Run(game, PlayerId.One, 0, "change hero money -5", 0, events));
            Assert.AreEqual(20, pickups.Inventory.Gold);
            Assert.IsTrue(scripts.Run(game, PlayerId.One, 0, "CHANGE HERO GOLD =7 | CHANGE HERO GOLD +1", 0, events));
            Assert.AreEqual(8, pickups.Inventory.Gold);
            Assert.IsFalse(scripts.Run(game, PlayerId.One, 0, "CHANGE HERO HP +10", 0, events));
            Assert.AreEqual(8, pickups.Inventory.Gold);
        }

        [Test]
        public void PickedHerbsAndCropsGrowBackAfterTheirGrowthTime()
        {
            ReachFor(0); // Health Root, 12 s
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            Assert.IsTrue(pickups.All[0].PickedUp);
            Steps(12 * 60 - 1);
            Assert.IsTrue(pickups.All[0].PickedUp, "not yet");
            game.Step();
            Assert.IsFalse(pickups.All[0].PickedUp, "grown back after 12 s");
            Assert.AreEqual(0, raised.OfType<PickupGrew>().Single().Pickup);

            ReachFor(101); // Coffee Bean: never grows back
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            Assert.IsTrue(pickups.All[101].PickedUp);
            Steps(60 * 60);
            Assert.IsTrue(pickups.All[101].PickedUp);
        }

        [Test]
        public void KneelingToPickUpGivesNoStaminaAndTheArrowsWait()
        {
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 0, stamina: 40)).Accepted);
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, 50f, 50f)).Accepted);
            Hold(PickUpHeld);
            Steps(5 * 60);
            Assert.IsTrue(chrono.PickingUp, "nothing in reach: still picking up");
            Assert.AreEqual(HeroState.Kneeling, chrono.State);
            Assert.AreEqual(40, chrono.Stamina, "no stamina while picking up");

            Hold(new MoveInput(false, true, false, false, false, false, 0f, 0f, true));
            var x = chrono.X;
            Steps(10);
            Assert.AreEqual(x, chrono.X, "the arrows do not move a hero picking up");
            Assert.AreEqual(HeroState.Kneeling, chrono.State);

            Hold(MoveInput.Arrows(right: true));
            Steps(10);
            Assert.IsFalse(chrono.PickingUp);
            Assert.Greater(chrono.X, x, "let go, the hero walks");
        }

        [Test]
        public void ALaidHeroDoesNotPickUp()
        {
            ReachFor(0);
            Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);
            Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);
            Hold(PickUpHeld);
            Steps(3);
            Assert.IsFalse(pickups.All[0].PickedUp);
            Assert.IsTrue(chrono.Laying);
        }

        [Test]
        public void TheWheelCyclesTheItemsInReachInsteadOfZooming()
        {
            var control = new CameraControl(Registry.View, party).Install(game);
            control.WheelFirst = pickups.UseWheel;
            var cam = control.For(PlayerId.One).Camera;

            ReachFor(6 + 8 + 3);
            var z = cam.Z;
            var selected = pickups.Selected(PlayerId.One);
            Assert.IsTrue(game.Submit(new SetViewInput(PlayerId.One, new ViewInput(0, 0, 1, false, false))).Accepted);
            game.Step();
            Assert.AreEqual(z, cam.Z, "no zoom with items in reach");
            Assert.AreNotEqual(selected, pickups.Selected(PlayerId.One));

            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, 50f, 50f)).Accepted);
            game.Step();
            Assert.IsTrue(game.Submit(new SetViewInput(PlayerId.One, new ViewInput(0, 0, -1, false, false))).Accepted);
            game.Step();
            Assert.AreNotEqual(z, cam.Z, "nothing in reach: the wheel zooms");
        }

        [Test]
        public void TheCursorFindsThePickupUnderIt()
        {
            var h = Registry.Pickups.Hover;
            var p = pickups.All[105]; // the Grimoire at (0.1, -0.1)
            var cx = p.X + h.ItemOffset.X - h.CursorOffset.X;
            var cy = p.Y + h.ItemOffset.Y - h.CursorOffset.Y;
            Assert.AreEqual(105, pickups.PickupAt(cx, cy));
            Assert.AreEqual(105, pickups.PickupAt(cx + 0.03f, cy - 0.03f), "edges included");
            Assert.AreEqual(-1, pickups.PickupAt(cx + 0.031f, cy));
            Assert.AreEqual(-1, pickups.PickupAt(60f, 60f));

            ReachFor(105);
            Assert.IsTrue(game.Submit(new PickUp(PlayerId.One)).Accepted);
            game.Step();
            Assert.AreEqual(-1, pickups.PickupAt(cx, cy), "a picked pickup is not under the cursor");
        }

        [Test]
        public void TheExamineBoxWritesTheOriginalsLines()
        {
            var box = Registry.Hud.Examine;
            Assert.AreEqual((292f, 112f, 328f, 175f), (box.X, box.Y, box.Width, box.Height));
            Assert.AreEqual("gfx/gui/examine.PNG", box.Picture);
            var lines = box.Lines(Registry.Pickups.Pickups[51]);
            NUnit.Framework.Legacy.CollectionAssert.AreEqual(new[]
            {
                "Rusty Sword", "ATK: 5 DEF: 1 DUR: 2/10", "Description:\nPoorly forged iron short sword\nwith a rusty blade", "Weight: 5.3", "Value: $50",
            }, lines);
            Assert.AreEqual("Weight: 0.1", box.Lines(Registry.Pickups.Pickups[0])[3]);
            Assert.AreEqual("Weight: 0", box.Lines(Registry.Pickups.Pickups[112])[3]);
        }

        [Test]
        public void TheReachHoverAndLabelFiguresAreTheOriginals()
        {
            var d = Registry.Pickups;
            Assert.AreEqual((0.05f, 0.5f, -0.4f, 0.5f, -0.5f, 13), (d.Reach.Range, d.Reach.HeroPoint.X, d.Reach.HeroPoint.Y, d.Reach.ItemPoint.X, d.Reach.ItemPoint.Y, d.Reach.Listed));
            Assert.AreEqual((0.03f, 0.025f, 0f, 0.03f, -0.04f), (d.Hover.Range, d.Hover.ItemOffset.X, d.Hover.ItemOffset.Y, d.Hover.CursorOffset.X, d.Hover.CursorOffset.Y));
            Assert.AreEqual((".", 0.02f, 0.007f), (d.Label.Marker, d.Label.MarkerScale, d.Label.NameScale));
            Assert.AreEqual(24f * 96f / 72f * 0.1f * 0.007f, d.Label.EmHeight(d.Label.NameScale), 1e-6f);
            Assert.AreEqual(0.05f, d.BattlePickupStamina);
            Assert.AreEqual("gfx/items/turnip.PNG", d.InventoryFile(d.Pickups[52]));
            Assert.AreEqual("gfx/items/plant2.PNG", d.WorldFile(d.Pickups[52]));
        }
    }
}

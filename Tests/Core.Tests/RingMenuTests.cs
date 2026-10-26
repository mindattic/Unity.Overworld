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
    /// U.OVW-US-A19: the battle ring menu around the leader (Move, End, Skill, Item, Attack), turned with the arrows or the
    /// wheel and pulsed with Up; Move's budget (agility x 0.05, shown as a dotted circle) and Attack's range circle; Item's
    /// pack at 10 percent of the gauge; End or a spent budget passing the turn to the next ready hero (RECONSTRUCTION 12.2,
    /// 12.3).
    /// </summary>
    public class RingMenuTests
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
        InventoryControl inventory;
        Hero chrono, belmont, flagg;
        RingMenu ring;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            inventory = new InventoryControl(pickups, Registry.Hud.Inventory, Registry.Hud.QuickBar).Install(game);
            party.Install(game);
            var scripts = new ScriptInterpreter(Registry.Scripts, party, pickups.Inventory) { Pack = inventory };
            pickups.Scripts = scripts;
            inventory.Scripts = scripts;
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Heroes[0];
            belmont = party.Heroes[1];
            flagg = party.Heroes[2];
            ring = party.RingOf(PlayerId.One);
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
            Steps(120);
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void End() => Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);

        void Hold(MoveInput input) => Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, input)).Accepted);

        void Choose() => Assert.IsTrue(game.Submit(new ChooseFromRing(PlayerId.One)).Accepted);

        /// <summary>One turn of the ring (a single command), stepped until it settles; returns the ticks it took.</summary>
        int Turn(int step)
        {
            Assert.IsTrue(game.Submit(new TurnRing(PlayerId.One, step)).Accepted);
            var ticks = 0;
            while (ring.Mode != RingMode.Select && ticks < 100)
            {
                game.Step();
                ticks++;
            }

            Assert.Less(ticks, 100, "the turn settles");
            return ticks;
        }

        [Test]
        public void TheRingIsTheOriginals()
        {
            var t = Registry.Heroes.Battle.Ring;
            CollectionAssert.AreEqual(new[] { RingEntry.Move, RingEntry.End, RingEntry.Skill, RingEntry.Item, RingEntry.Attack }, t.Entries.Select(e => e.Entry));
            Assert.AreEqual("gfx/ring/ring_Move.PNG", t.Entries[0].Picture);
            Assert.AreEqual("gfx/gui/ring_select.PNG", t.SelectPicture);
            Assert.AreEqual(0.1f, t.Radius);
            Assert.AreEqual(1.256f, t.SpinStep);
            Assert.AreEqual(0.1f, Registry.Heroes.Battle.ItemStamina);
            Assert.AreEqual(21, Registry.Heroes.Battle.Circle.Dots);

            var dir = new DirectoryInfo(ContentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))) dir = dir.Parent;
            var used = File.ReadAllLines(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))
                .Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[2] == "used").Select(c => c[0]).ToHashSet();
            var c2 = Registry.Heroes.Battle.Circle;
            foreach (var file in t.Entries.Select(e => e.Picture).Concat(new[] { t.SelectPicture, c2.GreenPicture, c2.BluePicture, c2.RedPicture }))
                Assert.IsTrue(used.Contains(file), file + " is not a used file of the catalogue");
        }

        [Test]
        public void EndOpensTheRingAtMoveWithTheMoveCircle()
        {
            Assert.IsFalse(ring.Open);
            End();
            Assert.IsTrue(ring.Open);
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry);
            Assert.IsTrue(ring.ShowMove);
            Assert.IsFalse(ring.ShowAttack);
            for (var i = 0; i < ring.Count; i++) Assert.AreEqual(1.57f + 1.256f * i, ring.Angle(i), 1e-5f);

            game.Step();
            Assert.AreEqual(0.25f, ring.CircleRadius, 1e-6f, "agility 5 x 0.05 at a full gauge");
            var up = chrono.Facing == Facing.Left || chrono.Facing == Facing.Down;
            Assert.AreEqual(up ? 0.01f : -0.01f, ring.DotAngle(0), 1e-6f, "the dots orbit by the leader's facing");
            // The dots at the circle's radius from the leader's middle, the far ones green.
            ring.DotOffset(10, party.Data.HeroSize, out var x, out var y);
            Assert.AreEqual(0.25f, Math.Sqrt(Math.Pow(x - (0.072 - 0.011), 2) + Math.Pow(y - (-0.072 + 0.02), 2)), 1e-4);
            Assert.That(Enumerable.Range(0, ring.DotCount).Select(ring.DotColour), Has.Some.EqualTo(DotColour.Green));

            End();
            Assert.IsFalse(ring.Open);
            Assert.IsFalse(ring.CircleShown);
        }

        [Test]
        public void TurningRightBringsAttackUpWithItsRedRangeCircle()
        {
            End();
            Steps(30);
            var ticks = Turn(1);
            Assert.That(ticks, Is.InRange(3, 10), "an eased spin of a few ticks");
            Assert.AreEqual(RingEntry.Attack, ring.SelectedEntry);
            Assert.AreEqual(1.57f + 1.256f, ring.Angle(0), 1e-4f, "every entry moved one step");
            Assert.IsTrue(ring.ShowAttack);
            Assert.IsFalse(ring.ShowMove);
            Assert.IsTrue(raised.OfType<RingTurned>().Any(e => e.Entry == RingEntry.Attack));
            game.Step();
            Assert.AreEqual(chrono.Weapon.Range, ring.CircleRadius, "the Fine Axe's 0.08");
            Assert.IsTrue(Enumerable.Range(0, ring.DotCount).All(i => ring.DotColour(i) == DotColour.Red));
        }

        [Test]
        public void TurningLeftWalksTheRingAndEachEntryHasItsEffect()
        {
            End();
            Steps(30);
            Turn(-1);
            Assert.AreEqual(RingEntry.End, ring.SelectedEntry);
            Assert.IsFalse(ring.CircleShown);
            Assert.AreEqual(HeroState.Kneeling, chrono.State, "End kneels the leader");
            Assert.IsTrue(chrono.Kneeling);

            Turn(-1);
            Assert.AreEqual(RingEntry.Skill, ring.SelectedEntry);
            Assert.AreEqual(HeroState.Drawing, chrono.State, "back up, it draws again");
            Assert.IsFalse(inventory.For(PlayerId.One).QuickBar);

            Turn(-1);
            Assert.AreEqual(RingEntry.Item, ring.SelectedEntry);
            Assert.IsTrue(inventory.For(PlayerId.One).QuickBar, "Item shows the quick bar");
            Turn(-1);
            Assert.AreEqual(RingEntry.Attack, ring.SelectedEntry);
            Assert.IsFalse(inventory.For(PlayerId.One).QuickBar);
            Turn(-1);
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry, "it wraps");
        }

        [Test]
        public void TheHeldArrowsAndTheWheelTurnTheRing()
        {
            End();
            Steps(30);
            Hold(MoveInput.Arrows(right: true));
            game.Step();
            Assert.AreEqual(RingMode.Right, ring.Mode);
            Hold(default);
            while (ring.Mode != RingMode.Select) game.Step();
            Assert.AreEqual(RingEntry.Attack, ring.SelectedEntry);

            Assert.IsTrue(party.UseRingWheel(PlayerId.One, -1));
            Assert.AreEqual(RingMode.Left, ring.Mode);
            while (ring.Mode != RingMode.Select) game.Step();
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry);
            End();
            Assert.IsFalse(party.UseRingWheel(PlayerId.One, 1), "shut, the wheel zooms");
        }

        [Test]
        public void UpPulsesTheRingOutAndBack()
        {
            End();
            Steps(30);
            Hold(MoveInput.Arrows(up: true));
            game.Step();
            Hold(default);
            var widest = 0f;
            for (var i = 0; i < 40 && ring.Mode != RingMode.Select; i++)
            {
                game.Step();
                widest = Math.Max(widest, ring.Radius);
            }

            Assert.GreaterOrEqual(widest, 1f);
            Assert.AreEqual(RingMode.Select, ring.Mode);
            Assert.AreEqual(0.1f, ring.Radius);
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry, "a pulse does not turn it");
        }

        [Test]
        public void MoveWalksTheBudgetThenTheTurnPassesToTheNextReadyHero()
        {
            End();
            Steps(30);
            var x = chrono.X;
            Choose();
            Assert.IsFalse(ring.Open, "Move shuts the ring");
            Assert.AreEqual(BattleAction.Moving, chrono.BattleAction);
            Assert.IsTrue(ring.ShowMove, "the circle stays to show what is left");
            Assert.IsTrue(raised.OfType<RingEntryChosen>().Single().Taken);

            Hold(MoveInput.Toward(chrono.X + 2f, chrono.Y));
            game.Step();
            Assert.Greater(chrono.X, x);
            Assert.Less(chrono.MoveDistance, 0.25f);
            Assert.Less(chrono.Stamina, 60, "the gauge falls with the distance");

            var ticks = 0;
            while (party.LeaderOf(PlayerId.One) == 0 && ticks++ < 600) game.Step();
            Assert.AreEqual(1, party.LeaderOf(PlayerId.One), "Belmont, next in order and Ready, leads");
            Assert.AreEqual(0, chrono.Stamina);
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction);
            Assert.IsTrue(chrono.Kneeling);
            Assert.Less(chrono.X - x, 0.26f, "it walked no farther than its budget");
            Assert.IsTrue(ring.Open);
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry);
            var passed = raised.OfType<TurnPassed>().Single();
            Assert.AreEqual((0, 1), (passed.From, passed.To));
            Assert.IsTrue(raised.OfType<LeaderChanged>().Any(e => e.Hero == 1), "the camera follows");
        }

        [Test]
        public void EndPassesTheTurnRoundTheReadyHeroes()
        {
            End();
            Steps(30);
            Turn(-1);
            Choose();
            Assert.AreEqual(1, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction);
            Assert.IsTrue(ring.Open, "End leaves the ring open, reset");
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry);
            Steps(2);

            Turn(-1);
            Choose();
            Assert.AreEqual(2, party.LeaderOf(PlayerId.One));

            // Chrono spent nothing: its full gauge makes it Ready again at its next check, so the turn wraps round to it.
            Steps(12);
            Assert.AreEqual(BattleAction.Ready, chrono.BattleAction);
            Turn(-1);
            Choose();
            Assert.AreEqual(0, party.LeaderOf(PlayerId.One));

        }

        [Test]
        public void EndWithNoOneElseReadyGivesTheTurnBackRecharging()
        {
            // Belmont and Flagg enter below full, recharging: no one else is ready.
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 1, stamina: 30)).Accepted);
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 2, stamina: 30)).Accepted);
            End();
            Steps(30);
            Turn(-1);
            Choose();
            Assert.AreEqual(0, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction);
            Assert.AreEqual(HeroState.Drawing, chrono.State, "NextHeroInBattle stood it up again");
            Assert.IsTrue(ring.Open);
            Assert.AreEqual(-1, raised.OfType<TurnPassed>().Last().To);
        }

        [Test]
        public void WithNoOneReadyTheRingWaitsForTheFirstFullGauge()
        {
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            End();
            Steps(30);
            Choose();
            Hold(MoveInput.Toward(chrono.X + 2f, chrono.Y));
            var ticks = 0;
            while (!ring.NoOneReady && ticks++ < 600) game.Step();
            Assert.IsTrue(ring.NoOneReady, "alone in battle, its budget spent");
            Assert.IsFalse(ring.Open);
            Hold(default);

            // 60 points at one every 0.18 s, then the first check at full.
            ticks = 0;
            while (!ring.Open && ticks++ < 1000) game.Step();
            Assert.IsTrue(ring.Open);
            Assert.IsFalse(ring.NoOneReady);
            Assert.AreEqual(0, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(60, chrono.Stamina);
            Assert.AreEqual(HeroMode.Adventure, belmont.Mode, "a full hero out of battle is never taken");
        }

        [Test]
        public void LeavingBattleForgetsThatNoOneWasReady()
        {
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            End();
            Steps(30);
            Choose();
            Hold(MoveInput.Toward(chrono.X + 2f, chrono.Y));
            var ticks = 0;
            while (!ring.NoOneReady && ticks++ < 600) game.Step();
            Hold(default);
            End();
            Assert.IsFalse(ring.NoOneReady);
            Steps(60);
            Assert.IsFalse(ring.Open);
            Assert.AreEqual(HeroMode.Adventure, chrono.Mode);
        }

        [Test]
        public void ItemOpensThePackAndAUseCostsTenPercentAndBringsTheRingBack()
        {
            End();
            Steps(30);
            Turn(-1);
            Turn(-1);
            Turn(-1);
            Assert.AreEqual(RingEntry.Item, ring.SelectedEntry);
            Choose();
            Assert.IsTrue(ring.Open, "an empty pack does not open");
            Assert.IsFalse(raised.OfType<RingEntryChosen>().Last().Taken);

            // Violet Caper (AP +5), in solo mode used on the leader.
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            Assert.IsTrue(game.Submit(new GiveItem(PlayerId.One, 4, 1)).Accepted);
            Choose();
            Assert.IsFalse(ring.Open);
            Assert.IsTrue(inventory.For(PlayerId.One).Open);
            // The Enter that chose is still held: it does not use the item.
            Hold(default(MoveInput).WithPickUp(true));
            game.Step();
            Assert.AreEqual(1, inventory.Inventory.Items.Count);
            Hold(default);
            game.Step();
            Hold(default(MoveInput).WithPickUp(true));
            game.Step();
            Hold(default);
            Assert.AreEqual(0, inventory.Inventory.Items.Count);
            Assert.AreEqual(54, chrono.Stamina, "60 (+5 capped) less CInt(60 x 0.1)");
            Assert.AreEqual(0.225f, chrono.MoveDistance, 1e-6f);
            Assert.IsFalse(inventory.For(PlayerId.One).Open);
            Assert.IsTrue(ring.Open, "the ring comes back as it was");
            Assert.AreEqual(RingEntry.Item, ring.SelectedEntry);
        }

        [Test]
        public void ChoosingDoesNotKneelToPickUp()
        {
            End();
            Steps(30);
            Choose();
            Hold(default(MoveInput).WithPickUp(true));
            Steps(3);
            Assert.IsFalse(chrono.PickingUp);
        }

        [Test]
        public void AClickHandsTheTurnToAFullHeroOnly()
        {
            End();
            Steps(30);
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 2, stamina: 30)).Accepted);
            Assert.IsFalse(game.Submit(new SelectHero(PlayerId.One, 2)).Accepted, "Flagg recharges");
            Assert.IsTrue(game.Submit(new SelectHero(PlayerId.One, 1)).Accepted);
            Assert.AreEqual(1, party.LeaderOf(PlayerId.One));
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction);
            Assert.IsTrue(ring.Open);
            Assert.IsTrue(raised.OfType<LeaderChanged>().Last().Hero == 1);
        }

        [Test]
        public void BackspaceInBattleBringsTheRingBackReset()
        {
            End();
            Steps(30);
            Choose();
            Assert.IsFalse(ring.Open);
            Assert.IsTrue(game.Submit(new DropItem(PlayerId.One)).Accepted);
            Assert.IsTrue(ring.Open);
            Assert.AreEqual(RingEntry.Move, ring.SelectedEntry);
        }

        [Test]
        public void APickupWhileMovingCostsTheBudgetToo()
        {
            End();
            Steps(30);
            Choose();
            var p = pickups.All[0];
            var reach = Registry.Pickups.Reach;
            var size = party.Data.HeroSize;
            var x = p.X + Registry.Pickups.ItemSize * reach.ItemPoint.X - size * reach.HeroPoint.X;
            var y = p.Y + Registry.Pickups.ItemSize * reach.ItemPoint.Y - size * reach.HeroPoint.Y;
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, x, y)).Accepted);
            Hold(default);
            game.Step();
            Hold(default(MoveInput).WithPickUp(true));
            game.Step();
            Assert.IsTrue(p.PickedUp);
            Assert.AreEqual(57, chrono.Stamina);
            Assert.AreEqual(0.25f * 57 / 60, chrono.MoveDistance, 1e-6f, "the cost is not given back as it walks on");
        }
    }
}

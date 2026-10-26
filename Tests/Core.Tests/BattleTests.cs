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
    /// U.OVW-US-A18: End toggles battle mode for the team; a hero whose turn gauge (stamina) is full draws its weapon with
    /// the original's draw and battle-stance animations and its weapon's frames over it in step, one below full recharges
    /// kneeling a point every 0.18 s and draws once full and Ready; leaving returns everyone to adventure mode
    /// (RECONSTRUCTION 7, 12.1, 12.4 and 13).
    /// </summary>
    public class BattleTests
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

        /// <summary>0.18 s on the 60 Hz tick clock: the first tick more than 0.18 s after the last check is the 11th.</summary>
        const int RefillTicks = 11;

        /// <summary>The draw rate (0.05 s) is three ticks; five advances take the draw from frame 0 to its last.</summary>
        const int DrawTicks = 13;

        Game game;
        Party party;
        Pickups pickups;
        InventoryControl inventory;
        Hero chrono, belmont, flagg;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            inventory = new InventoryControl(pickups, Registry.Hud.Inventory, Registry.Hud.QuickBar).Install(game);
            party.Install(game);
            pickups.Scripts = new ScriptInterpreter(Registry.Scripts, party, pickups.Inventory);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Heroes[0];
            belmont = party.Heroes[1];
            flagg = party.Heroes[2];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
            // Settle the followers beside the leader first, as they are at the start of play.
            Steps(120);
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void End() => Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);

        void Stamina(int hero, int value) => Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, hero, stamina: value)).Accepted);

        [Test]
        public void TheWeaponsAndTheBattleFiguresAreTheOriginals()
        {
            Assert.AreEqual("Fine Axe", chrono.Weapon.Name);
            Assert.AreEqual("Steel Sword", belmont.Weapon.Name);
            Assert.AreEqual("Steel Sword", flagg.Weapon.Name);
            Assert.AreEqual(0.18f, Registry.Heroes.Battle.RefillInterval);
            Assert.AreEqual(1, Registry.Heroes.Battle.RefillStamina);
            Assert.AreEqual(0.25f, chrono.MoveDistanceMax, 1e-6f);
            Assert.AreEqual(0.25f, chrono.MoveDistance, 1e-6f);

            var axe = chrono.Weapon;
            Assert.AreEqual("gfx/weapon/axe/weapon_left_1.PNG", axe.Left[0]);
            Assert.AreEqual("gfx/weapon/axe/weapon_right_6.PNG", axe.Right[5]);
            Assert.AreEqual("gfx/weapon/axe/draw/weapon_draw_left_3.PNG", axe.DrawLeft[2]);
            Assert.AreEqual("gfx/weapon/steelsword/draw/weapon_draw_right_6.PNG", belmont.Weapon.DrawRight[5]);
            Assert.AreEqual(24, axe.Files.Distinct().Count());

            var s = Registry.Heroes.Sprites;
            Assert.AreEqual("gfx/hero/Chrono/combat/draw/draw_left_1.PNG", s.Draw(Facing.Down)[0]);
            Assert.AreEqual("gfx/hero/Chrono/combat/draw/draw_right_6.PNG", s.Draw(Facing.Up)[5]);
            Assert.AreEqual("gfx/hero/Chrono/combat/battle_left_2.PNG", s.Battle(Facing.Left)[1]);
            Assert.AreEqual("gfx/hero/Chrono/combat/battle_right_4.PNG", s.Battle(Facing.Right)[3]);
        }

        [Test]
        public void EveryWeaponFrameIsAFileTheAssetCatalogueMarksUsed()
        {
            var dir = new DirectoryInfo(ContentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "docs/asset-catalogue.tsv not found");
            var used = File.ReadAllLines(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))
                .Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[2] == "used").Select(c => c[0]).ToHashSet();
            foreach (var w in Registry.Weapons.Weapons)
            foreach (var file in w.Files)
                Assert.IsTrue(used.Contains(file), file + " is not a used file of the catalogue");
        }

        [Test]
        public void OutOfBattleTheWeaponIsBlank()
        {
            foreach (var h in party.Heroes)
            {
                Assert.AreEqual(HeroMode.Adventure, h.Mode);
                Assert.AreEqual(BattleAction.Ready, h.BattleAction);
                Assert.IsNull(h.WeaponSprite, h.Name);
            }

            Assert.IsFalse(party.RingMenuOpen(PlayerId.One));
        }

        [Test]
        public void EndWithAFullGaugeDrawsTheWholeTeamsWeapons()
        {
            End();
            Assert.IsTrue(party.RingMenuOpen(PlayerId.One));
            var toggled = raised.OfType<BattleToggled>().Single();
            Assert.IsTrue(toggled.Battle);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, toggled.Heroes);
            foreach (var h in party.Heroes)
            {
                Assert.AreEqual(HeroMode.Battle, h.Mode, h.Name);
                Assert.AreEqual(HeroState.Drawing, h.State, h.Name);
                Assert.AreEqual(BattleAction.Ready, h.BattleAction, h.Name);
                Assert.AreEqual(0, h.Frame, h.Name);
            }

            // The first tick advances the draw a frame (its clock is stale), with the weapon's draw frame over it.
            game.Step();
            Assert.AreEqual(HeroState.Drawing, chrono.State);
            Assert.AreEqual(1, chrono.Frame);
            Assert.AreEqual(chrono.Facing == Facing.Left || chrono.Facing == Facing.Down ? Facing.Left : Facing.Right, HeroSprites.Side(chrono.Facing));
            var side = HeroSprites.Side(chrono.Facing);
            Assert.AreEqual(Registry.Heroes.Sprites.Draw(side)[1], chrono.Sprite);
            Assert.AreEqual(side == Facing.Left ? chrono.Weapon.DrawLeft[1] : chrono.Weapon.DrawRight[1], chrono.WeaponSprite);
            var bSide = HeroSprites.Side(belmont.Facing);
            Assert.AreEqual(bSide == Facing.Left ? belmont.Weapon.DrawLeft[1] : belmont.Weapon.DrawRight[1], belmont.WeaponSprite);
            StringAssert.Contains("steelsword", belmont.WeaponSprite);
            StringAssert.Contains("steelsword", flagg.WeaponSprite);
            StringAssert.Contains("/axe/", chrono.WeaponSprite);

            // Every 0.05 s the next frame; on the last the stance is taken at a random frame of it.
            Steps(3);
            Assert.AreEqual(2, chrono.Frame);
            Steps(6);
            Assert.AreEqual(HeroState.Drawing, chrono.State);
            Assert.AreEqual(4, chrono.Frame);
            Steps(3);
            Assert.AreEqual(HeroState.Battle, chrono.State);
            Assert.That(chrono.Frame, Is.InRange(0, 5));
            Assert.AreEqual(Registry.Heroes.Sprites.Draw(side)[5], chrono.Sprite, "the last draw frame shows on the tick the stance is taken");

            // The battle stance loops at 0.1 s, the weapon's stance frame over it.
            for (var i = 0; i < 40; i++)
            {
                game.Step();
                foreach (var h in party.Heroes)
                {
                    Assert.AreEqual(HeroState.Battle, h.State, h.Name);
                    Assert.AreEqual(0f, h.DX, h.Name);
                    var hs = HeroSprites.Side(h.Facing);
                    Assert.AreEqual(Registry.Heroes.Sprites.Battle(hs)[h.Frame], h.Sprite, h.Name);
                    Assert.AreEqual(hs == Facing.Left ? h.Weapon.Left[h.Frame] : h.Weapon.Right[h.Frame], h.WeaponSprite, h.Name);
                }
            }
        }

        [Test]
        public void FacingLeftOrDownShowsTheLeftSets()
        {
            chrono.Facing = Facing.Down;
            End();
            game.Step();
            Assert.AreEqual(Registry.Heroes.Sprites.Draw(Facing.Left)[1], chrono.Sprite);
            Assert.AreEqual(chrono.Weapon.DrawLeft[1], chrono.WeaponSprite);
        }

        [Test]
        public void BelowFullTheGaugeRefillsAPointEveryEighteenHundredthsKneeling()
        {
            Stamina(0, 30);
            End();
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction);
            Assert.AreEqual(HeroState.Drawing, belmont.State, "a full follower draws");
            Assert.AreEqual(BattleAction.Ready, belmont.BattleAction, "and is Ready on its own gauge, not the leader's");

            // The refill's clock is stale: the first check comes at once, then one every 11 ticks (0.18 s, VB's '>').
            game.Step();
            Assert.AreEqual(31, chrono.Stamina);
            Assert.AreEqual(HeroState.Kneeling, chrono.State);
            Assert.IsTrue(chrono.Kneeling);
            Assert.AreEqual(Registry.Heroes.Sprites.Kneel(chrono.Facing), chrono.Sprite);
            Assert.IsNull(chrono.WeaponSprite, "a kneeling hero holds no weapon");
            Steps(RefillTicks - 1);
            Assert.AreEqual(31, chrono.Stamina);
            game.Step();
            Assert.AreEqual(32, chrono.Stamina);
            Steps(RefillTicks * 28);
            Assert.AreEqual(60, chrono.Stamina);
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction, "full, it is Ready only at the next check");
            Assert.AreEqual(HeroState.Kneeling, chrono.State);

            Steps(RefillTicks);
            Assert.AreEqual(BattleAction.Ready, chrono.BattleAction);
            Assert.IsFalse(chrono.Kneeling);
            Assert.AreEqual(HeroState.Drawing, chrono.State);
            Assert.AreEqual(0.25f, chrono.MoveDistance, 1e-6f);
            Assert.IsTrue(raised.OfType<HeroBattleActionChanged>().Any(e => e.Hero == 0 && e.Action == BattleAction.Ready));
            Steps(DrawTicks + 3);
            Assert.AreEqual(HeroState.Battle, chrono.State);
        }

        [Test]
        public void AdventureRegenerationDoesNotRunInBattle()
        {
            End();
            Stamina(1, 59);
            // A follower set below full while Ready is not recharging: it neither refills nor regenerates.
            Steps(600);
            Assert.AreEqual(59, belmont.Stamina);
            Assert.AreEqual(60, chrono.Stamina);
        }

        [Test]
        public void TeamOffOnlyTheLeaderEntersBattle()
        {
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            End();
            Assert.AreEqual(HeroMode.Battle, chrono.Mode);
            Assert.AreEqual(HeroMode.Adventure, belmont.Mode);
            Assert.AreEqual(HeroMode.Adventure, flagg.Mode);
        }

        [Test]
        public void EndAgainReturnsEveryoneToAdventureStandingAndReady()
        {
            Stamina(0, 20);
            Stamina(2, 40);
            Assert.IsTrue(game.Submit(new ToggleQuickBar(PlayerId.One)).Accepted);
            End();
            Steps(30);
            Assert.AreEqual(HeroState.Kneeling, chrono.State);
            End();
            Assert.IsFalse(party.RingMenuOpen(PlayerId.One));
            Assert.IsFalse(inventory.For(PlayerId.One).QuickBar, "leaving hides the quick bar");
            Assert.IsFalse(raised.OfType<BattleToggled>().Last().Battle);
            foreach (var h in party.Heroes)
            {
                Assert.AreEqual(HeroMode.Adventure, h.Mode, h.Name);
                Assert.AreEqual(HeroState.Standing, h.State, h.Name);
                Assert.AreEqual(BattleAction.Ready, h.BattleAction, h.Name);
                Assert.IsFalse(h.Kneeling, h.Name);
            }

            game.Step();
            foreach (var h in party.Heroes) Assert.IsNull(h.WeaponSprite, h.Name);

            // The leader heads for the cursor again at once (the original left it kneeling until an arrow stood it up).
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(chrono.X + 0.5f, chrono.Y))).Accepted);
            game.Step();
            Assert.AreEqual(HeroState.Running, chrono.State);
            Assert.Greater(chrono.DX, 0f);
        }

        [Test]
        public void WithTheRingMenuOpenTheLeaderNeitherWalksNorHeadsForTheCursorNorKneelsToPickUp()
        {
            End();
            Steps(30);
            var x = chrono.X;
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(left: true))).Accepted);
            Steps(20);
            Assert.AreEqual(x, chrono.X);
            Assert.AreEqual(HeroState.Battle, chrono.State);
            Assert.IsTrue(chrono.MoveLeft, "the arrow is marked (it turns the ring)");
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(chrono.X - 0.5f, chrono.Y))).Accepted);
            Steps(20);
            Assert.AreEqual(x, chrono.X);
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, default(MoveInput).WithPickUp(true))).Accepted);
            Steps(5);
            Assert.IsFalse(chrono.PickingUp);
            Assert.AreEqual(HeroState.Battle, chrono.State);
        }

        [Test]
        public void ARechargingLeaderWithNothingHeldKneelsAtOnce()
        {
            Stamina(0, 10);
            End();
            game.Step();
            // Leave and enter again before the next refill check is due: KeysUp kneels it, not the refill.
            End();
            End();
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.AreEqual(BattleAction.Recharging, chrono.BattleAction);
            game.Step();
            Assert.AreEqual(11, chrono.Stamina, "no refill yet");
            Assert.AreEqual(HeroState.Kneeling, chrono.State);
            Assert.IsTrue(chrono.Kneeling);
        }

        [Test]
        public void TheFollowersHoldTheirPlacesInBattle()
        {
            End();
            var bx = belmont.X;
            var by = belmont.Y;
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, chrono.X + 0.5f, chrono.Y)).Accepted);
            Steps(60);
            Assert.AreEqual(bx, belmont.X);
            Assert.AreEqual(by, belmont.Y);
        }

        [Test]
        public void RestAndThePackAreRefusedInBattle()
        {
            End();
            Assert.IsFalse(game.Submit(new Rest(PlayerId.One)).Accepted);
            Assert.IsFalse(game.Submit(new ToggleInventory(PlayerId.One)).Accepted);
        }

        [Test]
        public void TheUKeyFlipsOnlyTheLeadersMode()
        {
            Assert.IsTrue(game.Submit(new ToggleHeroMode(PlayerId.One)).Accepted);
            Assert.AreEqual(HeroMode.Battle, chrono.Mode);
            Assert.AreEqual(HeroState.Standing, chrono.State);
            Assert.AreEqual(BattleAction.Ready, chrono.BattleAction);
            Assert.AreEqual(HeroMode.Adventure, belmont.Mode);
            Assert.IsFalse(party.RingMenuOpen(PlayerId.One));
            Assert.AreEqual(HeroMode.Battle, raised.OfType<HeroModeChanged>().Single().Mode);
            Assert.IsTrue(game.Submit(new ToggleHeroMode(PlayerId.One)).Accepted);
            Assert.AreEqual(HeroMode.Adventure, chrono.Mode);
        }

        [Test]
        public void APickupCostsAHeroMovingInBattleFivePercentOfItsStamina()
        {
            End();
            Steps(30);
            // The ring's Move entry (A19) shuts the ring and sets the hero Moving.
            party.SetRingMenu(PlayerId.One, false);
            chrono.BattleAction = BattleAction.Moving;
            var p = pickups.All[0];
            var reach = Registry.Pickups.Reach;
            var size = party.Data.HeroSize;
            var x = p.X + Registry.Pickups.ItemSize * reach.ItemPoint.X - size * reach.HeroPoint.X;
            var y = p.Y + Registry.Pickups.ItemSize * reach.ItemPoint.Y - size * reach.HeroPoint.Y;
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, x, y)).Accepted);
            game.Step();
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, default(MoveInput).WithPickUp(true))).Accepted);
            game.Step();
            Assert.IsTrue(p.PickedUp);
            Assert.AreEqual(57, chrono.Stamina);
        }

        [Test]
        public void APickupOutOfBattleCostsNoStamina()
        {
            var p = pickups.All[0];
            var reach = Registry.Pickups.Reach;
            var size = party.Data.HeroSize;
            var x = p.X + Registry.Pickups.ItemSize * reach.ItemPoint.X - size * reach.HeroPoint.X;
            var y = p.Y + Registry.Pickups.ItemSize * reach.ItemPoint.Y - size * reach.HeroPoint.Y;
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, x, y)).Accepted);
            game.Step();
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, default(MoveInput).WithPickUp(true))).Accepted);
            game.Step();
            Assert.IsTrue(p.PickedUp);
            Assert.AreEqual(60, chrono.Stamina);
        }

        [Test]
        public void TheStatusBoxWritesEachBattleHerosGaugeOrReady()
        {
            var box = Registry.Hud.StatusBox;
            var day = new LightColour(255, 255, 255);
            Assert.IsFalse(StatusBoxView.Lines(box, party, 0, day).Any(l => l.Text.Contains("%") || l.Text == "Ready!"), "no battle line out of battle");

            Stamina(0, 30);
            Stamina(2, 45);
            End();
            var lines = StatusBoxView.Lines(box, party, 0, day);
            var leader = lines.Single(l => l.Text == "50 %");
            Assert.AreEqual((5f + 14f, 1f + 67f), (leader.X, leader.Y));
            Assert.AreEqual("fontArialNarrow14", leader.Font.Name);
            Assert.AreEqual(14, leader.Font.Points);
            Assert.AreEqual((128, 0, 255, 255), (leader.Colour.A, leader.Colour.R, leader.Colour.G, leader.Colour.B), "alpha CByte(127.5) is 128, half to even");

            var belmontLine = lines.Single(l => l.Text == "Ready!");
            Assert.AreEqual(5f + 80f + 14f, belmontLine.X);
            Assert.AreEqual((255, 0, 255, 255), (belmontLine.Colour.A, belmontLine.Colour.R, belmontLine.Colour.G, belmontLine.Colour.B));
            var flaggLine = lines.Single(l => l.Text == "75%");
            Assert.AreEqual(5f + 160f + 14f, flaggLine.X);
            Assert.AreEqual(191, flaggLine.Colour.A);

            // At night (128, 128, 255) the leader's Ready is (255, 255, 255, b).
            Steps(RefillTicks * 32);
            var night = new LightColour(128, 128, 255);
            var ready = StatusBoxView.Lines(box, party, 0, night).First(l => l.Text == "Ready!");
            Assert.AreEqual(19f, ready.X);
            Assert.AreEqual((255, 255, 255, 255), (ready.Colour.A, ready.Colour.R, ready.Colour.G, ready.Colour.B));
            Assert.AreEqual(17, StatusBoxView.Percent(17, 100));
            Assert.AreEqual(2, StatusBoxView.Percent(1, 60), "1.67 rounds to 2");
        }
    }
}

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
    /// U.OVW-US-A10: each hero has health, mana and stamina with the original's maximums, regenerates and spends stamina
    /// every 2 seconds as checkRegen did, is Exhausted, Angry or Poisoned as checkStatus decided with the status animation
    /// above it, loses health while poisoned, and the status box and bars read it (RECONSTRUCTION 7 and 13).
    /// </summary>
    public class VitalsTests
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
        const int RegenTicks = 120;

        Game game;
        Party party;
        Hero chrono, belmont, flagg;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5)).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Heroes[0];
            belmont = party.Heroes[1];
            flagg = party.Heroes[2];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void Set(int hero, int? health = null, int? mana = null, int? stamina = null, HeroStatus? status = null) =>
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, hero, health, mana, stamina, status)).Accepted);

        [Test]
        public void EveryHeroStartsWithTheOriginalsVitalsAndMaximums()
        {
            foreach (var h in party.Heroes)
            {
                Assert.AreEqual(31, h.Health, h.Name);
                Assert.AreEqual(50, h.MaxHealth, h.Name);
                Assert.AreEqual(12, h.Mana, h.Name);
                Assert.AreEqual(20, h.MaxMana, h.Name);
                Assert.AreEqual(60, h.Stamina, h.Name);
                Assert.AreEqual(60, h.MaxStamina, h.Name);
                Assert.AreEqual(HeroStatus.Fine, h.Status, h.Name);
            }

            var v = Registry.Heroes.Vitals;
            Assert.AreEqual(0.15f, v.ExhaustedBelow);
            Assert.AreEqual(0.05f, v.AngryBelow);
            Assert.AreEqual(2f, chrono.Definition.RegenRate);
        }

        [Test]
        public void AStandingHeroGainsOneStaminaEveryTwoSeconds()
        {
            Set(0, stamina: 40);
            Steps(RegenTicks - 1);
            Assert.AreEqual(40, chrono.Stamina, "nothing before 2 s");
            Steps(1);
            Assert.AreEqual(41, chrono.Stamina, "+1 at 2 s");
            Steps(RegenTicks);
            Assert.AreEqual(42, chrono.Stamina, "+1 at 4 s");
            Set(0, stamina: 60);
            Steps(RegenTicks);
            Assert.AreEqual(60, chrono.Stamina, "never above the maximum");
        }

        [Test]
        public void AKneelingHeroGainsTwoAndNeverPassesItsMaximum()
        {
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            Set(1, stamina: 57);
            Steps(RegenTicks);
            Assert.AreEqual(HeroState.Kneeling, belmont.State);
            Assert.AreEqual(59, belmont.Stamina, "+2 kneeling");
            Steps(RegenTicks);
            Assert.AreEqual(60, belmont.Stamina, "59 + 2 stops at 60 (the original showed 61 for one step; BIBLE)");
        }

        [Test]
        public void AKneelingHeroPickingSomethingUpGainsNothing()
        {
            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            Set(1, stamina: 40);
            belmont.PickingUp = true;
            Steps(RegenTicks);
            Assert.AreEqual(40, belmont.Stamina);
        }

        [Test]
        public void ARunningHeroLosesOneStaminaEveryTwoSecondsAndStopsRunningWhenItIsGone()
        {
            Set(0, stamina: 2);
            Assert.IsTrue(game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true, run: true))).Accepted);
            Steps(RegenTicks);
            Assert.AreEqual(HeroState.Running, chrono.State);
            Assert.AreEqual(1, chrono.Stamina);
            Steps(RegenTicks);
            Assert.AreEqual(0, chrono.Stamina);
            Steps(1);
            Assert.AreEqual(HeroState.Walking, chrono.State, "with no stamina left the run key only walks");
            Steps(RegenTicks * 2);
            Assert.AreEqual(0, chrono.Stamina, "walking neither spends nor regains");
        }

        [Test]
        public void APoisonedHeroLosesAHealthPointEveryTwoSecondsAndGainsNoStamina()
        {
            Set(0, health: 3, stamina: 30, status: HeroStatus.Poisoned);
            Steps(RegenTicks);
            Assert.AreEqual(2, chrono.Health);
            Assert.AreEqual(30, chrono.Stamina, "a poisoned hero does not regenerate");
            Assert.AreEqual(HeroStatus.Poisoned, chrono.Status, "poison stays");
            Steps(RegenTicks * 3);
            Assert.AreEqual(0, chrono.Health, "to 0, never below");
            Set(0, status: HeroStatus.Fine);
            Steps(1);
            Assert.AreEqual(HeroStatus.Angry, chrono.Status, "cured, at 0 health it is Angry");
        }

        [Test]
        public void AHeroIsExhaustedBelowFifteenPercentStaminaAndAngryBelowFivePercentHealth()
        {
            Set(0, stamina: 9);
            Steps(1);
            Assert.AreEqual(HeroStatus.Fine, chrono.Status, "9 is not below 0.15 x 60");
            Set(0, stamina: 8);
            Steps(1);
            Assert.AreEqual(HeroStatus.Exhausted, chrono.Status);
            Set(0, health: 3);
            Steps(1);
            Assert.AreEqual(HeroStatus.Exhausted, chrono.Status, "3 is not below 0.05 x 50");
            Set(0, health: 2);
            Steps(1);
            Assert.AreEqual(HeroStatus.Angry, chrono.Status, "Angry wins over Exhausted");
            Set(0, health: 50, stamina: 60);
            Steps(1);
            Assert.AreEqual(HeroStatus.Fine, chrono.Status);
            Assert.IsTrue(raised.OfType<HeroStatusChanged>().Any(e => e.Hero == 0 && e.Status == HeroStatus.Exhausted));
        }

        [Test]
        public void TheStatusAnimationShowsAboveEachHeroAndEachKeepsItsOwnTime()
        {
            var v = Registry.Heroes.Vitals;
            Steps(1);
            Assert.IsNull(chrono.StatusIcon, "a Fine hero shows nothing");

            Set(0, stamina: 1);
            Set(1, health: 1);
            Set(2, status: HeroStatus.Poisoned);
            Steps(1);
            Assert.AreEqual(v.ExhaustedIcon[0], chrono.StatusIcon);
            Assert.AreEqual(v.AngryIcon[0], belmont.StatusIcon);
            Assert.AreEqual(v.PoisonedIcon[0], flagg.StatusIcon);

            Steps(15);
            Assert.AreEqual(0, chrono.StatusFrame, "tick 17: not yet 0.3 s");
            Steps(1);
            Assert.AreEqual(1, chrono.StatusFrame, "0.3 s a frame");
            Assert.AreEqual(1, belmont.StatusFrame, "every animated hero advances, not only the first (BIBLE)");
            Assert.AreEqual(1, flagg.StatusFrame);
            Assert.AreEqual(v.AngryIcon[1], belmont.StatusIcon);
            Steps(18);
            Assert.AreEqual(2, chrono.StatusFrame);
            Steps(36);
            Assert.AreEqual(0, chrono.StatusFrame, "four frames, looping");
            CollectionAssert.AreEqual(v.AngryIcon, v.PoisonedIcon, "the original's Poisoned folder is empty: it showed the Angry frames");
            Assert.AreEqual(0.05f, v.IconSize);
            Assert.AreEqual(0.045f, v.IconOffset.X);
            Assert.AreEqual(0.03f, v.IconOffset.Y);
        }

        [Test]
        public void SetHeroVitalsClampsToZeroAndTheMaximum()
        {
            Set(1, health: 999, mana: -5, stamina: 61);
            Assert.AreEqual(50, belmont.Health);
            Assert.AreEqual(0, belmont.Mana);
            Assert.AreEqual(60, belmont.Stamina);
            Assert.IsFalse(game.Submit(new SetHeroVitals(PlayerId.One, 3, health: 1)).Accepted);
            var e = raised.OfType<HeroVitalsChanged>().Last();
            Assert.AreEqual((1, 50, 0, 60), (e.Hero, e.Health, e.Mana, e.Stamina));
        }

        [Test]
        public void TheStatusBoxWritesTheLeaderThenTheTeamInTheInverseOfTheLight()
        {
            var box = Registry.Hud.StatusBox;
            var day = new LightColour(255, 255, 255);
            var lines = StatusBoxView.Lines(box, party, party.LeaderOf(PlayerId.One), day);
            Assert.AreEqual(3 * 7, lines.Count, "three columns of a name and three rows of two");
            Assert.AreEqual("  Chrono  ", lines[0].Text);
            Assert.AreEqual((17f, 1f), (lines[0].X, lines[0].Y));
            Assert.AreEqual((255, 255, 255, 255), (lines[0].Colour.A, lines[0].Colour.R, lines[0].Colour.G, lines[0].Colour.B), "the leader's name in white");
            Assert.AreEqual("HP 31", lines[1].Text);
            Assert.AreEqual((19f, 16f), (lines[1].X, lines[1].Y));
            Assert.AreEqual("  / 50", lines[2].Text);
            Assert.AreEqual((50f, 16f), (lines[2].X, lines[2].Y));
            Assert.AreEqual("MP 12", lines[3].Text);
            Assert.AreEqual(31f, lines[3].Y);
            Assert.AreEqual("  / 20", lines[4].Text);
            Assert.AreEqual("AP 60", lines[5].Text);
            Assert.AreEqual(46f, lines[5].Y);
            Assert.AreEqual("  / 60", lines[6].Text);
            Assert.AreEqual((255, 0, 255, 0), (lines[1].Colour.A, lines[1].Colour.R, lines[1].Colour.G, lines[1].Colour.B), "green by day");
            Assert.AreEqual("  Belmont  ", lines[7].Text);
            Assert.AreEqual(97f, lines[7].X);
            Assert.AreEqual((0, 255, 255), (lines[7].Colour.R, lines[7].Colour.G, lines[7].Colour.B), "the others' names in (255 - r, 255, b)");
            Assert.AreEqual("  Flagg  ", lines[14].Text);
            Assert.AreEqual(177f, lines[14].X);

            var night = StatusBoxView.Lines(box, party, 0, new LightColour(128, 128, 255));
            Assert.AreEqual((127, 255, 0), (night[1].Colour.R, night[1].Colour.G, night[1].Colour.B));

            Assert.IsTrue(game.Submit(new ToggleTeam(PlayerId.One)).Accepted);
            Assert.AreEqual(7, StatusBoxView.Lines(box, party, 0, day).Count, "solo: only the leader");
            Assert.IsTrue(game.Submit(new SelectHero(PlayerId.One, 2)).Accepted);
            Assert.AreEqual("  Flagg  ", StatusBoxView.Lines(box, party, party.LeaderOf(PlayerId.One), day)[0].Text);
        }

        [Test]
        public void TheBarsAreTheOriginalsAndTheirFillsRoundAsVbDid()
        {
            var bars = Registry.Hud.Bars;
            Assert.AreEqual((75, 14), (bars.Width, bars.Height));
            Assert.AreEqual((2f, 436f), (bars.Health.X, bars.Health.Y));
            Assert.AreEqual((2f, 450f), (bars.Mana.X, bars.Mana.Y));
            Assert.AreEqual((2f, 464f), (bars.Stamina.X, bars.Stamina.Y));
            Assert.AreEqual("gfx/gui/health_bar.PNG", bars.Mana.Frame, "the mana bar reuses health_bar.png");
            Assert.AreEqual(46, bars.FillWidth(31, 50), "46.5 rounds to even");
            Assert.AreEqual(45, bars.FillWidth(12, 20));
            Assert.AreEqual(75, bars.FillWidth(60, 60));
            Assert.AreEqual(0, bars.FillWidth(0, 60));
            Assert.AreEqual(0, bars.FillWidth(-3, 60));
        }

        [Test]
        public void EveryVitalsPictureIsACataloguedUsedFile()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "docs/asset-catalogue.tsv");
            var catalogue = File.ReadAllLines(Path.Combine(dir.FullName, "docs", "asset-catalogue.tsv")).Skip(1)
                .Select(l => l.Split('\t')).Where(c => c.Length == 4).ToDictionary(c => c[0], c => c[2], StringComparer.Ordinal);
            foreach (var file in Registry.Heroes.Vitals.Files.Concat(Registry.Hud.Bars.Files))
            {
                Assert.IsTrue(catalogue.TryGetValue(file, out var disposition), file + " is not in the asset catalogue (exact case)");
                Assert.AreEqual("used", disposition, file);
            }
        }
    }
}

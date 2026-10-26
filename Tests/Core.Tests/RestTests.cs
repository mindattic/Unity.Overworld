using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A11: the rest key kneels the leader, again lays it down, again kneels it; while laid the arrows set the
    /// sleep's hours (1 to 24) and confirming sleeps: the world fades to black, every hero gains one-eighth of each maximum
    /// an hour (stamina not while poisoned), the clock moves on, the leader stands and the world fades back; the Moon Berry
    /// and Morningstar sleep until 10 PM and 5 AM (RECONSTRUCTION 6.4).
    /// </summary>
    public class RestTests
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

        /// <summary>The ticks from white to black at 5 a channel a tick (255 / 5).</summary>
        const int FadeTicks = 51;

        Game game;
        Party party;
        SleepCycle sleep;
        WorldClock clock;
        Hero chrono;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5)).Install(game);
            var board = new MessageBoard(Registry.Hud);
            clock = new WorldClock(Registry.Time, Registry.Heroes.Locomotion.GameSpeed, board);
            sleep = new SleepCycle(party, clock).Install(game);
            clock.Install(game);
            board.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            chrono = party.Heroes[0];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void Rest() => Assert.IsTrue(game.Submit(new Rest(PlayerId.One)).Accepted);

        void Lay()
        {
            Rest();
            Rest();
        }

        [Test]
        public void TheTuningIsTheOriginals()
        {
            var r = Registry.Heroes.Rest;
            Assert.AreEqual(1, r.MinHours);
            Assert.AreEqual(24, r.MaxHours);
            Assert.AreEqual(0.08f, r.ChooserRate);
            Assert.AreEqual(0.125f, r.RestorePerHour);
            Assert.AreEqual(5, r.FadeStep);
        }

        [Test]
        public void TheRestKeyKneelsThenLaysDownThenKneels()
        {
            Rest();
            Assert.IsTrue(chrono.Kneeling);
            Assert.IsFalse(chrono.Laying);
            Assert.AreEqual(HeroState.Kneeling, chrono.State);

            Rest();
            Assert.IsTrue(chrono.Kneeling);
            Assert.IsTrue(chrono.Laying);
            Steps(1);
            Assert.AreEqual(Registry.Heroes.Sprites.Sleep(chrono.Facing), chrono.Sprite);

            Rest();
            Assert.IsTrue(chrono.Kneeling);
            Assert.IsFalse(chrono.Laying);
            Assert.IsTrue(raised.OfType<HeroRestChanged>().Any());
        }

        [Test]
        public void TheRestKeyIsRefusedInBattle()
        {
            chrono.Mode = HeroMode.Battle;
            Assert.IsFalse(game.Submit(new Rest(PlayerId.One)).Accepted);
            Assert.IsFalse(chrono.Kneeling);
        }

        [Test]
        public void WhileLaidTheArrowsSetTheHoursFromOneToTwentyFour()
        {
            Lay();
            var x = chrono.X;
            Assert.AreEqual(1, chrono.SleepHours);
            game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true)));
            Steps(6);
            Assert.AreEqual(2, chrono.SleepHours, "one hour more once 0.08 s has passed");
            Steps(3 * 60);
            Assert.AreEqual(24, chrono.SleepHours, "never more than 24");
            Assert.AreEqual(x, chrono.X, "a laid hero does not walk");

            game.Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(down: true)));
            Steps(3 * 60);
            Assert.AreEqual(1, chrono.SleepHours, "never fewer than 1");
            Assert.IsTrue(raised.OfType<SleepHoursChanged>().Any());
        }

        [Test]
        public void SleepIsRefusedUnlessTheLeaderLiesDown()
        {
            Assert.IsFalse(game.Submit(new Sleep(PlayerId.One)).Accepted);
            Rest();
            Assert.IsFalse(game.Submit(new Sleep(PlayerId.One)).Accepted);
            Rest();
            Assert.IsFalse(game.Submit(new Sleep(PlayerId.One, 25)).Accepted);
            Assert.IsTrue(game.Submit(new Sleep(PlayerId.One)).Accepted);
        }

        [Test]
        public void ASleepFadesOutRestoresAnEighthAnHourMovesTheClockAndFadesBackIn()
        {
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 1, null, null, 10, null)).Accepted);
            Lay();
            Assert.IsTrue(game.Submit(new Sleep(PlayerId.One, 1)).Accepted);
            Assert.IsTrue(sleep.Sleeping);

            Steps(1);
            Assert.AreEqual(new LightColour(250, 250, 250).ToString(), sleep.Ambient.ToString());
            Steps(FadeTicks - 2);
            Assert.IsTrue(sleep.Sleeping, "still fading");
            Assert.AreEqual(31, chrono.Health);
            Steps(1);

            Assert.IsFalse(sleep.Sleeping);
            Assert.AreEqual(new LightColour(0, 0, 0).ToString(), sleep.Ambient.ToString());
            Assert.AreEqual(1, raised.OfType<PartySlept>().Count());
            // 31 + 50 / 8 = 37.25 -> 37; 12 + 20 / 8 = 14.5 -> 14 and 10 + 60 / 8 = 17.5 -> 18 (VB rounds half to even).
            foreach (var h in party.Heroes)
            {
                Assert.AreEqual(37, h.Health, h.Name);
                Assert.AreEqual(14, h.Mana, h.Name);
            }

            Assert.AreEqual(60, chrono.Stamina);
            Assert.AreEqual(18, party.Heroes[1].Stamina);
            Assert.AreEqual(new TimeSpan(10, 0, 0), clock.Now.TimeOfDay);
            Assert.IsFalse(chrono.Kneeling, "the sleeper stands");
            Assert.IsFalse(chrono.Laying);
            Assert.IsTrue(party.Heroes[1].Laying, "the others lie on");
            Assert.AreEqual(1, chrono.SleepHours);

            Steps(FadeTicks);
            Assert.AreEqual(new LightColour(255, 255, 255).ToString(), sleep.Ambient.ToString());
            var lit = sleep.Lit(new LightColour(200, 100, 50));
            Assert.AreEqual(new LightColour(200, 100, 50).ToString(), lit.ToString());
        }

        [Test]
        public void ALongSleepRestoresToTheMaximumButNotStaminaWhilePoisoned()
        {
            Assert.IsTrue(game.Submit(new SetHeroVitals(PlayerId.One, 0, 1, 0, 0, HeroStatus.Poisoned)).Accepted);
            Lay();
            Assert.IsTrue(game.Submit(new Sleep(PlayerId.One, 12)).Accepted);
            Steps(FadeTicks);
            Assert.AreEqual(50, chrono.Health);
            Assert.AreEqual(20, chrono.Mana);
            Assert.AreEqual(0, chrono.Stamina, "a poisoned hero's stamina is not restored");
            Assert.AreEqual(new TimeSpan(21, 0, 0), clock.Now.TimeOfDay);
            Assert.AreEqual("Night", clock.Period.Name);
        }

        [Test]
        public void TheMoonBerrySleepsUntilTenAtNightAndTheMorningstarUntilFive()
        {
            Assert.IsTrue(game.Submit(new SleepUntil(PlayerId.One, 0, 22)).Accepted);
            Assert.IsTrue(chrono.Laying);
            Steps(FadeTicks);
            var day = clock.Now.Date;
            Assert.AreEqual(new TimeSpan(22, 0, 0), clock.Now.TimeOfDay, "10 PM, not the original's 11 PM");

            Steps(FadeTicks);
            Assert.IsTrue(game.Submit(new SleepUntil(PlayerId.One, 0, 5)).Accepted);
            Steps(FadeTicks);
            Assert.AreEqual(day.AddDays(1), clock.Now.Date);
            Assert.AreEqual(5, clock.Now.Hour, "5 AM, not the original's 6 AM");
            Assert.IsFalse(game.Submit(new SleepUntil(PlayerId.One, 0, 24)).Accepted);
            Assert.IsFalse(game.Submit(new SleepUntil(PlayerId.One, 7, 5)).Accepted);
        }

        [Test]
        public void TheHudAsksHowLongToRest()
        {
            var hud = Registry.Hud;
            Assert.AreEqual("Rest for 1 hour?", hud.RestText(1));
            Assert.AreEqual("Rest for 7 hours?", hud.RestText(7));
            Assert.AreEqual(220f, hud.Rest.X);
            Assert.AreEqual(220f, hud.Rest.Y);
            Assert.AreEqual("fontArialNarrow24", hud.Rest.Font.Name);
        }
    }
}

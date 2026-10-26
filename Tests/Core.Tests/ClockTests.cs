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
    /// U.OVW-US-A9: the clock runs one in-game minute a real second from 9:00 AM on 1 Frostfall 64; the day is Day (9 to
    /// 18), Evening (18 to 21), Night (21 to 6) and Morning (6 to 9); the light fades one step a minute toward each
    /// period's colour as the original's renderTime did; each period's first minute queues its name as a large message;
    /// the time and date read as the original wrote them, with its fantasy months and weekdays (RECONSTRUCTION 6.1 to 6.3, 13).
    /// </summary>
    public class ClockTests
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
        static TimeData Time => Registry.Time;

        Game game;
        MessageBoard board;
        WorldClock clock;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            // renderTime ran before renderMessages, so a period's message shows on the minute it comes.
            board = new MessageBoard(Registry.Hud);
            clock = new WorldClock(Time, Registry.Heroes.Locomotion.GameSpeed, board).Install(game);
            board.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Ticks(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void Minutes(int n) => Ticks(n * Clock.TicksPerSecond);

        static LightColour Rgb(byte r, byte g, byte b) => new LightColour(r, g, b);

        [Test]
        public void TheDataIsTheOriginals()
        {
            Assert.AreEqual(new DateTime(64, 1, 1, 9, 0, 0), Time.Start);
            Assert.AreEqual("Day", Time.StartPeriod.Name);
            Assert.AreEqual(1f, Time.SecondsPerMinute);
            Assert.AreEqual(1, Time.FadeStep);
            CollectionAssert.AreEqual(new[] { "Day", "Evening", "Night", "Morning" }, Time.Periods.Select(p => p.Name));
            Assert.AreEqual(Rgb(255, 255, 255), Time.Period("Day").Colour);
            Assert.AreEqual(Rgb(255, 128, 128), Time.Period("Evening").Colour);
            Assert.AreEqual(Rgb(128, 128, 255), Time.Period("Night").Colour);
            Assert.AreEqual(Rgb(255, 255, 255), Time.Period("Morning").Colour);
            CollectionAssert.AreEqual(new[] { FadeDirection.Fall, FadeDirection.Fall, FadeDirection.Rise }, Time.Period("Night").Fade);
            CollectionAssert.AreEqual(new[] { "Frostfall", "Dwelling", "Thawing", "Flora", "Rainy Season", "Swelter", "Pre-Harvest", "Reaping", "Goldera", "Yawning", "Gorging", "Yeardusk" },
                Time.Calendar.Months);
            CollectionAssert.AreEqual(new[] { "Solaraday", "Lunaday", "Terraday", "Humpday", "Titanday", "Pyreday", "Wynday" }, Time.Calendar.Weekdays);
            Assert.AreEqual("CE", Time.Calendar.YearSuffix);
        }

        [Test]
        public void EveryHourIsInTheOriginalsPeriod()
        {
            var expected = new Dictionary<string, int[]>
            {
                ["Day"] = new[] { 9, 10, 11, 12, 13, 14, 15, 16, 17 },
                ["Evening"] = new[] { 18, 19, 20 },
                ["Night"] = new[] { 21, 22, 23, 0, 1, 2, 3, 4, 5 },
                ["Morning"] = new[] { 6, 7, 8 },
            };
            foreach (var pair in expected)
                foreach (var hour in pair.Value)
                    Assert.AreEqual(pair.Key, Time.PeriodAt(hour).Name, "hour " + hour);
        }

        [Test]
        public void TheGameStartsAtNineOnTheFirstOfFrostfallInWhiteDaylight()
        {
            Assert.AreEqual(new DateTime(64, 1, 1, 9, 0, 0), clock.Now);
            Assert.AreEqual("Day", clock.Period.Name);
            Assert.AreEqual(Rgb(255, 255, 255), clock.Light);
            Assert.AreEqual("9 : 00 AM", clock.TimeText);
            Assert.AreEqual("Terraday, Frostfall 1,  64 CE", clock.DateText, "1 January 64 is a Tuesday");
            Assert.AreEqual("9 : 00 AM  Day", clock.TimeAndPeriodText);
        }

        [Test]
        public void AMinutePassesEachRealSecond()
        {
            Ticks(Clock.TicksPerSecond - 1);
            Assert.AreEqual("9 : 00 AM", clock.TimeText);
            Ticks(1);
            Assert.AreEqual("9 : 01 AM", clock.TimeText);
            game.Advance(10.0);
            Assert.AreEqual("9 : 01 AM", clock.TimeText, "a stalled frame runs at most ten ticks");
            Ticks(59 * Clock.TicksPerSecond - 10);
            Assert.AreEqual("10 : 00 AM", clock.TimeText);
        }

        [Test]
        public void TheStartIsNotAnnounced()
        {
            Minutes(2);
            Assert.IsFalse(raised.OfType<PeriodAnnounced>().Any());
            Assert.AreEqual(0, board.Shown.Count + board.Waiting);
        }

        [Test]
        public void EachPeriodIsAnnouncedOnItsFirstMinute()
        {
            Assert.IsTrue(game.Submit(new SetTimeRatio(PlayerId.One, 0f)).Accepted);
            Ticks(24 * 60);
            CollectionAssert.AreEqual(new[] { "Evening", "Night", "Morning", "Day" }, raised.OfType<PeriodAnnounced>().Select(e => e.Period.Name));
            CollectionAssert.AreEqual(new[] { "Evening", "Night", "Morning", "Day" }, raised.OfType<PeriodChanged>().Select(e => e.Period.Name));
            var evening = raised.OfType<PeriodAnnounced>().First();
            Assert.AreEqual(9 * 60, evening.Tick, "18:00 is the 540th minute");
            Assert.AreEqual(new DateTime(64, 1, 2, 9, 0, 0), clock.Now);
            Assert.AreEqual("Humpday, Frostfall 2,  64 CE", clock.DateText, "2 January 64 is a Wednesday");
        }

        [Test]
        public void TheAnnouncementIsALargeWhiteMessageAtTheCentre()
        {
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 17, 59)).Accepted);
            Minutes(1);
            Assert.AreEqual("6 : 00 PM", clock.TimeText);
            Assert.AreEqual(1, board.Shown.Count);
            var m = board.Shown[0];
            Assert.AreEqual("Evening", m.Caption);
            Assert.AreEqual("fontArialNarrow24", m.Font.Name);
            Assert.AreEqual((255, 255, 255), (m.R, m.G, m.B));
            Assert.AreEqual(320 - "Evening".Length * 3, m.X);
            Assert.AreEqual(241, m.Y, "shown at the centre, then drifted on the same tick as renderMessages did");
            Assert.AreEqual(253, m.A);
        }

        [Test]
        public void TheLightFadesOneStepAMinuteTowardEachPeriodsColour()
        {
            Assert.IsTrue(game.Submit(new SetTimeRatio(PlayerId.One, 0f)).Accepted);
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 17, 59)).Accepted);

            Ticks(1); // 18:00, Evening: every channel falls toward (255, 128, 128)
            Assert.AreEqual(Rgb(255, 254, 254), clock.Light);
            Ticks(126);
            Assert.AreEqual(Rgb(255, 128, 128), clock.Light);
            Ticks(54); // 21:00, Night: red and green fall, blue rises
            Assert.AreEqual("9 : 00 PM", clock.TimeText);
            Assert.AreEqual("Night", clock.Period.Name);
            Assert.AreEqual(Rgb(254, 128, 129), clock.Light);
            Ticks(126);
            Assert.AreEqual(Rgb(128, 128, 255), clock.Light);
            Ticks(9 * 60 - 126); // 6:00, Morning: every channel rises toward white
            Assert.AreEqual("6 : 00 AM", clock.TimeText);
            Assert.AreEqual(Rgb(129, 129, 255), clock.Light);
            Ticks(126);
            Assert.AreEqual(Rgb(255, 255, 255), clock.Light);
            Ticks(180);
            Assert.AreEqual("Day", clock.Period.Name);
            Assert.AreEqual(Rgb(255, 255, 255), clock.Light);
        }

        [Test]
        public void AChannelOnTheWrongSideOfItsTargetIsSetToIt()
        {
            // The DAYPERIOD path moves the hour and leaves the light: at night's colour the next minute is Evening, where red,
            // below its 255 but meant to fall, is set to it at once while blue falls one step.
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 2)).Accepted);
            Assert.AreEqual(Rgb(128, 128, 255), clock.Light, "the HOUR path sets the new period's colour");
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 19, settle: false)).Accepted);
            Assert.AreEqual("Night", clock.Period.Name, "settled at the next minute");
            Minutes(1);
            Assert.AreEqual("Evening", clock.Period.Name);
            Assert.AreEqual(Rgb(255, 128, 254), clock.Light);
            Assert.IsFalse(raised.OfType<PeriodAnnounced>().Any(), "19:01 is not Evening's first minute");
        }

        [Test]
        public void SettingAnEarlierHourMovesToTheNextDayAndKeepsTheMinute()
        {
            Minutes(5);
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 22)).Accepted);
            Assert.AreEqual(new DateTime(64, 1, 1, 22, 5, 0), clock.Now);
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 5)).Accepted);
            Assert.AreEqual(new DateTime(64, 1, 2, 5, 5, 0), clock.Now);
            Assert.AreEqual("Night", clock.Period.Name);
            Assert.IsTrue(game.Submit(new SetClock(PlayerId.One, 6, 0)).Accepted);
            Assert.AreEqual("Morning", clock.Period.Name);
            Assert.AreEqual("Morning", raised.OfType<PeriodAnnounced>().Single().Period.Name, "the HOUR path announces the first minute it lands on");
            Assert.AreEqual(new DateTime(64, 1, 2, 6, 0, 0), raised.OfType<ClockSet>().Last().Time);
        }

        [Test]
        public void BadClockCommandsAreRefused()
        {
            Assert.IsFalse(game.Submit(new SetClock(PlayerId.One, 24)).Accepted);
            Assert.IsFalse(game.Submit(new SetClock(PlayerId.One, 3, 60)).Accepted);
            Assert.IsFalse(game.Submit(new SetTimeRatio(PlayerId.One, -1f)).Accepted);
            Assert.IsFalse(game.Submit(new SetClock(new PlayerId(2), 3)).Accepted, "a player not in the game");
            Assert.AreEqual(new DateTime(64, 1, 1, 9, 0, 0), clock.Now);
        }

        [Test]
        public void TheTimeRatioSetsTheSecondsAMinuteTakes()
        {
            Assert.IsTrue(game.Submit(new SetTimeRatio(PlayerId.One, 0.5f)).Accepted);
            Ticks(30);
            Assert.AreEqual("9 : 01 AM", clock.TimeText);
            Assert.IsTrue(game.Submit(new SetTimeRatio(PlayerId.One, 0f)).Accepted);
            Ticks(3);
            Assert.AreEqual("9 : 04 AM", clock.TimeText);
        }

        [TestCase(0, 0, "12 : 00 AM")]
        [TestCase(0, 5, "12 : 05 AM")]
        [TestCase(9, 0, "9 : 00 AM")]
        [TestCase(11, 59, "11 : 59 AM")]
        [TestCase(12, 0, "12 : 00 PM")]
        [TestCase(12, 30, "12 : 30 PM")]
        [TestCase(13, 7, "1 : 07 PM")]
        [TestCase(23, 59, "11 : 59 PM")]
        public void TheTimeReadsAsTheOriginalWroteItWithNoonFixed(int hour, int minute, string text)
        {
            Assert.AreEqual(text, WorldClock.FormatTime(new DateTime(64, 1, 1, hour, minute, 0)));
        }

        [Test]
        public void TheDateUsesTheFantasyMonthsAndWeekdays()
        {
            var c = Time.Calendar;
            Assert.AreEqual("Humpday, Yeardusk 31,  64 CE", WorldClock.FormatDate(new DateTime(64, 12, 31), c), "64 is a leap year: 31 December is a Wednesday");
            Assert.AreEqual("Wynday", c.WeekdayName(new DateTime(64, 1, 5)));
            Assert.AreEqual("Solaraday, Rainy Season 4,  64 CE", WorldClock.FormatDate(new DateTime(64, 5, 4), c));
            for (var month = 1; month <= 12; month++)
                Assert.AreEqual(c.Months[month - 1], c.MonthName(new DateTime(64, month, 1)));
            Assert.AreEqual("Pyreday", c.WeekdayName(new DateTime(64, 1, 4)));
        }

        [Test]
        public void MessagesWaitTheirTurnFadeAndDrift()
        {
            var font = Registry.Hud.Font("fontArial");
            board.Add("first", font, 255, 255, 255);
            board.Add("second", font, 255, 255, 255);
            Ticks(1);
            Assert.AreEqual(1, board.Shown.Count);
            Assert.AreEqual(1, board.Waiting);
            Ticks(14);
            Assert.AreEqual(255, board.Shown[0].Y, "15 pixels below the centre: not yet more than 14");
            Assert.AreEqual(1, board.Waiting);
            Ticks(1);
            Assert.AreEqual(2, board.Shown.Count, "the next shows once the last is more than 14 below the centre");
            Assert.AreEqual(320 - 6 * 3, board.Shown[1].X);
            Ticks(128 - 16);
            Assert.AreEqual(1, board.Shown.Count, "a message is gone after 128 ticks");
            Ticks(16);
            Assert.AreEqual(0, board.Shown.Count);
        }

        [Test]
        public void AFullQueueReplacesItsLastMessage()
        {
            var font = Registry.Hud.Font("fontArial");
            for (var i = 0; i < Registry.Hud.Messages.MaxMessages + 5; i++) board.Add("m" + i, font, 255, 255, 255);
            Assert.AreEqual(Registry.Hud.Messages.MaxMessages, board.Waiting);
        }

        [Test]
        public void TheHudLinesAreWhereTheOriginalWroteThem()
        {
            var hud = Registry.Hud;
            Assert.AreEqual((640, 480), (hud.ScreenWidth, hud.ScreenHeight));
            Assert.AreEqual((5f, 410f), (hud.Period.X, hud.Period.Y));
            Assert.AreEqual((5f, 423f), (hud.Time.X, hud.Time.Y));
            Assert.AreEqual((390f, 25f), (hud.HeroScreenDate.X, hud.HeroScreenDate.Y));
            Assert.AreEqual((390f, 45f), (hud.HeroScreenTime.X, hud.HeroScreenTime.Y));
            Assert.AreEqual("fontCalisto", hud.Time.Font.Name);
            Assert.AreEqual((128, 255, 255, 0), (hud.Time.Colour.A, hud.Time.Colour.R, hud.Time.Colour.G, hud.Time.Colour.B));
            Assert.AreEqual("Calisto MT", hud.Time.Font.Families[0]);
            Assert.AreEqual(8f, hud.Time.Font.Points);
        }

        static ContentException Patched(string file, Func<string, string> patch)
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var original = disk.Read(file);
            var changed = patch(original);
            Assert.AreNotEqual(original, changed, "the patch to " + file + " changed nothing");
            return Assert.Throws<ContentException>(() => ContentRegistry.Load(new MemoryContentSource(disk).Set(file, changed)));
        }

        [Test]
        public void AnHourInNoPeriodIsRefused()
        {
            var e = Patched("time.json", t => t.Replace("\"fromHour\": 21, \"toHour\": 6", "\"fromHour\": 22, \"toHour\": 6"));
            StringAssert.Contains("time.json.periods: hour 21 must be in exactly one period (is in none)", e.Message);
        }

        [Test]
        public void AMissingMonthOrBadColourIsRefused()
        {
            var e = Patched("time.json", t => t.Replace("\"Yeardusk\"", "").Replace("\"Gorging\", ", "\"Gorging\"").Replace("[128, 128, 255]", "[128, 128, 256]"));
            StringAssert.Contains("time.json.calendar: 'months' must name 12 months (names 11)", e.Message);
            StringAssert.Contains("time.json.periods[2]: 'colour' must be an [r, g, b] colour", e.Message);
        }

        [Test]
        public void AnUnknownFontIsRefused()
        {
            var e = Patched("hud.json", t => t.Replace("\"font\": \"fontArialNarrow24\"", "\"font\": \"fontComic\""));
            StringAssert.Contains("hud.json.messages.announcement: 'font' \"fontComic\" is not one of the fonts", e.Message);
        }
    }
}

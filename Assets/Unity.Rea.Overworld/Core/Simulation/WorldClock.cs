using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// Sets the time of day (Script.setHour behind CHANGE TIME HOUR and DAYPERIOD, RECONSTRUCTION 6.2 and 9): the clock
    /// moves to <see cref="Hour"/> (and <see cref="Minute"/>, or the minute it shows), on the same day, or the next when
    /// that time of day is earlier than now. With <see cref="Settle"/> the period and the light are recomputed at once (the
    /// HOUR path's checkDayperiod and setDayColor, which announce a period whose first minute it lands on); without it they
    /// follow at the next minute, the light fading from where it was (the DAYPERIOD path).
    /// </summary>
    public sealed class SetClock : ICommand
    {
        public SetClock(PlayerId player, int hour, int? minute = null, bool settle = true)
        {
            Player = player;
            Hour = hour;
            Minute = minute;
            Settle = settle;
        }

        public PlayerId Player { get; }
        public int Hour { get; }

        /// <summary>The minute to show, or null to keep the current one (setHour kept it).</summary>
        public int? Minute { get; }

        public bool Settle { get; }
    }

    /// <summary>
    /// Changes how many real seconds an in-game minute takes (the original's timeRatio, which renderTime pinned to 1
    /// "Debug"): 0 adds a minute every tick, the fastest the clock can run. For scripted runs and tests.
    /// </summary>
    public sealed class SetTimeRatio : ICommand
    {
        public SetTimeRatio(PlayerId player, float secondsPerMinute) { Player = player; SecondsPerMinute = secondsPerMinute; }
        public PlayerId Player { get; }
        public float SecondsPerMinute { get; }
    }

    /// <summary>The clock was set by a command.</summary>
    public sealed class ClockSet : IGameEvent
    {
        public ClockSet(long tick, PlayerId player, DateTime time) { Tick = tick; Player = player; Time = time; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public DateTime Time { get; }
    }

    /// <summary>The day moved into another period.</summary>
    public sealed class PeriodChanged : IGameEvent
    {
        public PeriodChanged(long tick, PeriodDefinition previous, PeriodDefinition period) { Tick = tick; Previous = previous; Period = period; }
        public long Tick { get; }
        public PeriodDefinition Previous { get; }
        public PeriodDefinition Period { get; }
    }

    /// <summary>A period's first minute came and its name was queued as a large message.</summary>
    public sealed class PeriodAnnounced : IGameEvent
    {
        public PeriodAnnounced(long tick, PeriodDefinition period) { Tick = tick; Period = period; }
        public long Tick { get; }
        public PeriodDefinition Period { get; }
    }

    /// <summary>
    /// The game's clock and daylight (Sun.vb, module Lights: theTime, renderTime, checkDayperiod, setDayColor, renderDate;
    /// RECONSTRUCTION 6.1 to 6.3). It starts at 9:00 on 1 Frostfall 64 in the Day period with white light; every
    /// timeRatio x gameSpeed real seconds (one) it adds an in-game minute (at most one a tick), puts the day in the period
    /// that hour belongs to (queuing the period's name as a large message on its first minute), then moves each light
    /// channel one step toward that period's colour, rising or falling as the period says. The light is the colour the
    /// world is drawn in (the original's material diffuse and ambient); the HUD and screens are not lit.
    /// Installed on a <see cref="Game"/>, which then accepts <see cref="SetClock"/> and <see cref="SetTimeRatio"/>.
    /// </summary>
    public sealed class WorldClock : ITickSystem
    {
        readonly MessageBoard messages;
        double sinceMinute;

        public WorldClock(TimeData time, float gameSpeed = 1f, MessageBoard messages = null)
        {
            Data = time ?? throw new ArgumentNullException(nameof(time));
            if (!(gameSpeed >= 0f)) throw new ArgumentOutOfRangeException(nameof(gameSpeed));
            GameSpeed = gameSpeed;
            this.messages = messages;
            Now = time.Start;
            Period = time.StartPeriod;
            Light = time.StartPeriod.Colour;
            SecondsPerMinute = time.SecondsPerMinute;
        }

        public TimeData Data { get; }

        /// <summary>The in-game date and time (theTime).</summary>
        public DateTime Now { get; private set; }

        /// <summary>The part of the day (dayPeriod).</summary>
        public PeriodDefinition Period { get; private set; }

        /// <summary>The colour the world is lit in now (r, g, b).</summary>
        public LightColour Light { get; private set; }

        /// <summary>timeRatio: real seconds per in-game minute, before the game speed.</summary>
        public float SecondsPerMinute { get; private set; }

        /// <summary>gameSpeed: multiplies the seconds a minute takes (the - and = developer keys change it, <see cref="DeveloperTools"/>).</summary>
        public float GameSpeed { get; internal set; }

        /// <summary>The time as the HUD writes it (strTime): "9 : 05 AM".</summary>
        public string TimeText => FormatTime(Now);

        /// <summary>The hero screen's date line: "Solaraday, Frostfall 1,  64 CE".</summary>
        public string DateText => FormatDate(Now, Data.Calendar);

        /// <summary>The hero screen's time line: the time, two spaces, the period.</summary>
        public string TimeAndPeriodText => TimeText + "  " + Period.Name;

        public WorldClock Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<SetClock>(HandleSet);
            game.Register<SetTimeRatio>(HandleRatio);
            game.AddSystem(this);
            return this;
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            sinceMinute += Clock.TickSeconds;
            // The epsilon absorbs binary rounding so sixty ticks are exactly one second.
            if (sinceMinute + 1e-9 < SecondsPerMinute * GameSpeed) return;
            sinceMinute = 0;

            Now = Now.AddMinutes(1);
            CheckPeriod(tick, events);
            Fade();
        }

        /// <summary>checkDayperiod: the period the hour is in, announced on its first minute.</summary>
        void CheckPeriod(long tick, ICollection<IGameEvent> events)
        {
            var period = Data.PeriodAt(Now.Hour);
            if (period != Period)
            {
                events.Add(new PeriodChanged(tick, Period, period));
                Period = period;
            }

            if (Now.Hour == period.FromHour && Now.Minute == 0)
            {
                messages?.Add(period.Announcement, messages.Tuning.AnnouncementFont,
                    messages.Tuning.AnnouncementColour.R, messages.Tuning.AnnouncementColour.G, messages.Tuning.AnnouncementColour.B);
                events.Add(new PeriodAnnounced(tick, period));
            }
        }

        /// <summary>One minute's step of every channel toward the period's colour.</summary>
        void Fade()
        {
            var target = Period.Colour;
            Light = new LightColour(
                Toward(Light.R, target.R, Period.Fade[0]),
                Toward(Light.G, target.G, Period.Fade[1]),
                Toward(Light.B, target.B, Period.Fade[2]));
        }

        byte Toward(byte value, byte target, FadeDirection direction)
        {
            var step = Data.FadeStep;
            if (direction == FadeDirection.Rise) return value < target ? (byte)Math.Min(value + step, target) : target;
            return value > target ? (byte)Math.Max(value - step, target) : target;
        }

        CommandResult HandleSet(Game game, SetClock c)
        {
            if (c.Hour < 0 || c.Hour > 23) return CommandResult.Reject("The hour must be 0 to 23, not " + c.Hour + ".");
            if (c.Minute.HasValue && (c.Minute < 0 || c.Minute > 59)) return CommandResult.Reject("The minute must be 0 to 59, not " + c.Minute + ".");
            var events = new List<IGameEvent>();
            SetHour(c.Player, c.Hour, c.Minute, c.Settle, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>
        /// setHour (and, settled, checkDayperiod and setDayColor): the clock moves to <paramref name="hour"/> and the
        /// minute given or shown, today, or tomorrow when that is earlier than now. For <see cref="SetClock"/> and for a
        /// sleep that ends at an hour (<see cref="SleepUntil"/>). The hour and minute must be valid.
        /// </summary>
        internal void SetHour(PlayerId player, int hour, int? minute, bool settle, long tick, ICollection<IGameEvent> events)
        {
            var m = minute ?? Now.Minute;
            var day = Now.Date;
            if (new TimeSpan(hour, m, 0) < Now.TimeOfDay) day = day.AddDays(1);
            Now = day.Add(new TimeSpan(hour, m, 0));
            events.Add(new ClockSet(tick, player, Now));
            if (!settle) return;
            CheckPeriod(tick, events);
            Light = Period.Colour;
        }

        /// <summary>
        /// checkSleep's theTime.AddHours, checkDayperiod and setDayColor: the clock moves on <paramref name="by"/>, the
        /// period is the one the new hour is in (announced when it lands on a period's first minute) and the light is that
        /// period's colour at once. For the end of a sleep (<see cref="SleepCycle"/>).
        /// </summary>
        internal void Advance(PlayerId player, TimeSpan by, long tick, ICollection<IGameEvent> events)
        {
            Now = Now.Add(by);
            events.Add(new ClockSet(tick, player, Now));
            CheckPeriod(tick, events);
            Light = Period.Colour;
        }

        /// <summary>CHANGE LIGHT RED / GREEN / BLUE: the light is set (each minute then steps it toward the period's colour again).</summary>
        internal void SetLight(LightColour light) => Light = light;

        CommandResult HandleRatio(Game game, SetTimeRatio c)
        {
            if (!(c.SecondsPerMinute >= 0f) || float.IsInfinity(c.SecondsPerMinute))
                return CommandResult.Reject("The seconds a minute takes must be 0 or more.");
            SecondsPerMinute = c.SecondsPerMinute;
            return CommandResult.Accept();
        }

        /// <summary>
        /// renderTime's strTime: the 12-hour clock, "H : MM AM|PM", with "12" at midnight. The original's AM test was
        /// Hour &lt;= 12, so noon showed AM; that is fixed here (noon to 11:59 PM show PM; BIBLE, Clock noon).
        /// </summary>
        public static string FormatTime(DateTime time)
        {
            var hour = time.Hour == 0 ? 12 : time.Hour > 12 ? time.Hour - 12 : time.Hour;
            var colon = time.Minute < 10 ? " : 0" : " : ";
            return hour.ToString(CultureInfo.InvariantCulture) + colon + time.Minute.ToString(CultureInfo.InvariantCulture) + " " + (time.Hour < 12 ? "AM" : "PM");
        }

        /// <summary>renderheroScreen's date line: weekday, month day, two spaces, year and suffix.</summary>
        public static string FormatDate(DateTime time, CalendarDefinition calendar)
        {
            if (calendar == null) throw new ArgumentNullException(nameof(calendar));
            return calendar.WeekdayName(time) + ", " + calendar.MonthName(time) + " " + time.Day.ToString(CultureInfo.InvariantCulture) +
                   ",  " + time.Year.ToString(CultureInfo.InvariantCulture) + " " + calendar.YearSuffix;
        }
    }
}

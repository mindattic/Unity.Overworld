using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>Which way a light channel moves toward its period's colour each in-game minute.</summary>
    public enum FadeDirection
    {
        /// <summary>Below the target it rises one step; otherwise it is set to the target.</summary>
        Rise,

        /// <summary>Above the target it falls one step; otherwise it is set to the target.</summary>
        Fall,
    }

    /// <summary>A light colour of the original (a Direct3D material colour, 0 to 255 per channel).</summary>
    public readonly struct LightColour : IEquatable<LightColour>
    {
        public LightColour(byte r, byte g, byte b) { R = r; G = g; B = b; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public bool Equals(LightColour other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is LightColour c && Equals(c);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public override string ToString() => "(" + R + ", " + G + ", " + B + ")";
    }

    /// <summary>One part of the day (Lights.periodOfDay with its hours, colour and fade, RECONSTRUCTION 6.2).</summary>
    public sealed class PeriodDefinition
    {
        /// <summary>Its name as the original's enum wrote it ("Day"), shown on the HUD.</summary>
        public string Name { get; internal set; }

        /// <summary>The first hour of the period.</summary>
        public int FromHour { get; internal set; }

        /// <summary>The hour it ends at (not included); below <see cref="FromHour"/> when it runs past midnight.</summary>
        public int ToHour { get; internal set; }

        /// <summary>The colour the light fades toward during it.</summary>
        public LightColour Colour { get; internal set; }

        /// <summary>How red, green and blue move toward <see cref="Colour"/>.</summary>
        public IReadOnlyList<FadeDirection> Fade { get; internal set; }

        /// <summary>The large message queued on its first minute.</summary>
        public string Announcement { get; internal set; }

        public bool Contains(int hour) => FromHour < ToHour ? hour >= FromHour && hour < ToHour : hour >= FromHour || hour < ToHour;

        public override string ToString() => Name;
    }

    /// <summary>The fantasy calendar (Lights.renderDate, RECONSTRUCTION 6.3).</summary>
    public sealed class CalendarDefinition
    {
        /// <summary>Twelve names, January first.</summary>
        public IReadOnlyList<string> Months { get; internal set; }

        /// <summary>Seven names, Sunday first (as <see cref="DayOfWeek"/>).</summary>
        public IReadOnlyList<string> Weekdays { get; internal set; }

        public string YearSuffix { get; internal set; }

        public string MonthName(DateTime when) => Months[when.Month - 1];
        public string WeekdayName(DateTime when) => Weekdays[(int)when.DayOfWeek];
    }

    /// <summary>
    /// The clock, its periods and the calendar (time.json): when the game starts, how many real seconds an in-game minute
    /// takes, the colour each period fades the light toward and the names of the months and weekdays.
    /// </summary>
    public sealed class TimeData
    {
        public DateTime Start { get; internal set; }
        public PeriodDefinition StartPeriod { get; internal set; }

        /// <summary>The original's timeRatio: real seconds per in-game minute (times the game speed).</summary>
        public float SecondsPerMinute { get; internal set; }

        /// <summary>How far a light channel moves each in-game minute (1).</summary>
        public int FadeStep { get; internal set; }

        /// <summary>The periods in the original's enum order (Day, Evening, Night, Morning); every hour is in exactly one.</summary>
        public IReadOnlyList<PeriodDefinition> Periods { get; internal set; }

        public CalendarDefinition Calendar { get; internal set; }

        /// <summary>The period an hour of the day falls in (checkDayperiod).</summary>
        public PeriodDefinition PeriodAt(int hour)
        {
            foreach (var p in Periods) if (p.Contains(hour)) return p;
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "No period holds this hour.");
        }

        public PeriodDefinition Period(string name)
        {
            foreach (var p in Periods) if (p.Name == name) return p;
            return null;
        }

        internal static TimeData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var data = new TimeData();

            var periods = new List<PeriodDefinition>();
            foreach (var r in root.Objects("periods"))
            {
                var p = new PeriodDefinition
                {
                    Name = r.Text("name"),
                    FromHour = r.Integer("fromHour", 0, 23),
                    ToHour = r.Integer("toHour", 0, 23),
                    Colour = ReadColour(r, "colour"),
                    Fade = ReadFade(r, "fade"),
                    Announcement = r.Text("announcement"),
                };
                r.Check(p.FromHour != p.ToHour, "toHour", "must differ from 'fromHour'");
                if (periods.Exists(o => o.Name == p.Name)) r.Fail("name", "\"" + p.Name + "\" is listed twice");
                r.Done();
                periods.Add(p);
            }

            if (periods.Count == 0) errors.Add(file + ".periods: there must be at least one period");
            for (var hour = 0; hour < 24 && periods.Count > 0; hour++)
            {
                var holding = periods.FindAll(p => p.FromHour != p.ToHour && p.Contains(hour));
                if (holding.Count != 1)
                    errors.Add(file + ".periods: hour " + hour + " must be in exactly one period (is in " +
                               (holding.Count == 0 ? "none" : string.Join(", ", holding.ConvertAll(p => p.Name))) + ")");
            }

            data.Periods = periods;

            var clock = root.Section("clock");
            var start = clock.Section("start");
            var year = start.Integer("year", 1, 9999);
            var month = start.Integer("month", 1, 12);
            var day = start.Integer("day", 1, 31);
            var hourOfDay = start.Integer("hour", 0, 23);
            var minute = start.Integer("minute", 0, 59);
            if (day > DateTime.DaysInMonth(year, month)) start.Fail("day", "must be a day of that month (is " + day + ")");
            else data.Start = new DateTime(year, month, day, hourOfDay, minute, 0);
            start.Done();
            var startPeriod = clock.Text("startPeriod");
            data.StartPeriod = periods.Find(p => p.Name == startPeriod);
            if (data.StartPeriod == null && periods.Count > 0) clock.Fail("startPeriod", "\"" + startPeriod + "\" is not one of the periods");
            data.SecondsPerMinute = clock.Number("secondsPerMinute", 0f, 3600f);
            data.FadeStep = clock.Integer("fadeStep", 1, 255);
            clock.Done();

            var calendar = root.Section("calendar");
            var months = calendar.Texts("months");
            var weekdays = calendar.Texts("weekdays");
            calendar.Check(months.Count == 12, "months", "must name 12 months (names " + months.Count + ")");
            calendar.Check(weekdays.Count == 7, "weekdays", "must name 7 weekdays, Sunday first (names " + weekdays.Count + ")");
            data.Calendar = new CalendarDefinition { Months = months, Weekdays = weekdays, YearSuffix = calendar.Text("yearSuffix") };
            calendar.Done();
            root.Done();
            return data;
        }

        internal static LightColour ReadColour(ContentReader r, string key)
        {
            var values = ReadBytes(r, key, 3, "an [r, g, b] colour of three whole numbers from 0 to 255");
            return values == null ? new LightColour(255, 255, 255) : new LightColour(values[0], values[1], values[2]);
        }

        /// <summary>A list of <paramref name="count"/> whole numbers from 0 to 255; null (and reported) otherwise.</summary>
        internal static byte[] ReadBytes(ContentReader r, string key, int count, string what)
        {
            var value = r.Raw(key, JsonKind.Array, what);
            if (value == null) return null;
            if (value.Items.Count != count)
            {
                r.Fail(key, "must be " + what);
                return null;
            }

            var bytes = new byte[count];
            for (var i = 0; i < count; i++)
            {
                var item = value.Items[i];
                if (item.Kind != JsonKind.Number || Math.Floor(item.Number) != item.Number || item.Number < 0 || item.Number > 255)
                {
                    r.Fail(key, "must be " + what);
                    return null;
                }

                bytes[i] = (byte)item.Number;
            }

            return bytes;
        }

        static IReadOnlyList<FadeDirection> ReadFade(ContentReader r, string key)
        {
            var names = r.Texts(key);
            var list = new List<FadeDirection>();
            foreach (var n in names)
            {
                if (n == "rise") list.Add(FadeDirection.Rise);
                else if (n == "fall") list.Add(FadeDirection.Fall);
                else r.Fail(key, "must hold only \"rise\" or \"fall\" (has \"" + n + "\")");
            }

            if (names.Count != 3) r.Fail(key, "must give red, green and blue (gives " + names.Count + ")");
            while (list.Count < 3) list.Add(FadeDirection.Rise);
            return list;
        }
    }
}

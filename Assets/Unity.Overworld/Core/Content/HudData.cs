using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A text colour as Root.Write took it: alpha, red, green, blue, each 0 to 255.</summary>
    public readonly struct TextColour
    {
        public TextColour(byte a, byte r, byte g, byte b) { A = a; R = r; G = g; B = b; }
        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
    }

    /// <summary>One of the original's D3DX fonts (Fonts.vb): the faces to try in order, its size in points and weight.</summary>
    public sealed class HudFont
    {
        public string Name { get; internal set; }
        public IReadOnlyList<string> Families { get; internal set; }
        public float Points { get; internal set; }
        public bool Bold { get; internal set; }
    }

    /// <summary>A line of text written at a point of the 640 x 480 screen, left and top aligned, in a font and colour.</summary>
    public sealed class HudText
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public HudFont Font { get; internal set; }
        public TextColour Colour { get; internal set; }
    }

    /// <summary>How the queued messages are shown (Gui/Messages.vb, RECONSTRUCTION 13).</summary>
    public sealed class MessageTuning
    {
        public int MaxMessages { get; internal set; }
        public int CharacterWidth { get; internal set; }
        public int Spacing { get; internal set; }
        public int Fade { get; internal set; }
        public int Drift { get; internal set; }

        /// <summary>The font and colour of a day period's announcement.</summary>
        public HudFont AnnouncementFont { get; internal set; }

        public TextColour AnnouncementColour { get; internal set; }
    }

    /// <summary>
    /// The status box (Gui/StatusBox.vb renderStatus, RECONSTRUCTION 13): the leader's name and its HP, MP and AP
    /// against their maximums, then (in team mode) the other heroes' beside it.
    /// </summary>
    public sealed class StatusBoxDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public HudFont Font { get; internal set; }

        /// <summary>The name's point from the box's (+12, +0).</summary>
        public DataPoint Name { get; internal set; }

        /// <summary>The first row's "HP n" point (+14, +15).</summary>
        public DataPoint Value { get; internal set; }

        /// <summary>The first row's "  / max" point (+45, +15).</summary>
        public DataPoint Maximum { get; internal set; }

        /// <summary>How much lower each row is (15).</summary>
        public float RowSpacing { get; internal set; }

        /// <summary>How far right each other hero's column is (80).</summary>
        public float TeamSpacing { get; internal set; }
    }

    /// <summary>One of the bottom-left bars: where it is, its frame picture and its fill picture.</summary>
    public sealed class BarDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public string Frame { get; internal set; }
        public string Fill { get; internal set; }
    }

    /// <summary>The three bottom-left bars (Gui/HealthBar.vb, ManaBar.vb, StaminaBar.vb), all the same size.</summary>
    public sealed class BarsDefinition
    {
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public BarDefinition Health { get; internal set; }
        public BarDefinition Mana { get; internal set; }
        public BarDefinition Stamina { get; internal set; }

        /// <summary>Every picture of the bars, once each.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var list = new List<string>();
                foreach (var b in new[] { Health, Mana, Stamina })
                {
                    if (!list.Contains(b.Frame)) list.Add(b.Frame);
                    if (!list.Contains(b.Fill)) list.Add(b.Fill);
                }

                return list;
            }
        }

        /// <summary>
        /// The fill's width for <paramref name="value"/> of <paramref name="max"/>: width x value / max as VB assigned it
        /// to an Integer (rounded half to even), never below 0.
        /// </summary>
        public int FillWidth(int value, int max)
        {
            if (max <= 0) return 0;
            var fill = (int)Math.Round(Width * ((double)value / max), MidpointRounding.ToEven);
            return fill < 0 ? 0 : fill;
        }
    }

    /// <summary>
    /// The examine box (Gui/ExamineBox.vb renderExamine, RECONSTRUCTION 8.4): a panel picture at (X, Y) of Width x
    /// Height, the pickup's picture as an icon, and its name, effect, description, weight and value, each at its point
    /// from the panel's corner.
    /// </summary>
    public sealed class ExamineBoxDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public string Picture { get; internal set; }
        public DataPoint Icon { get; internal set; }
        public float IconSize { get; internal set; }
        public HudFont Font { get; internal set; }
        public TextColour Colour { get; internal set; }
        public DataPoint Name { get; internal set; }
        public DataPoint Effect { get; internal set; }
        public DataPoint Description { get; internal set; }
        public DataPoint Weight { get; internal set; }
        public DataPoint Value { get; internal set; }
        public string DescriptionText { get; internal set; }
        public string WeightText { get; internal set; }
        public string ValueText { get; internal set; }

        /// <summary>The box's five lines for <paramref name="pickup"/>, in draw order: name, effect, description, weight, value.</summary>
        public string[] Lines(PickupDefinition pickup)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return new[]
            {
                pickup.Name,
                pickup.Effect,
                string.Format(c, DescriptionText, pickup.Description),
                // VB wrote a Single as its shortest text (0.1, 5.3, 0); the round-trip format is the same.
                string.Format(c, WeightText, pickup.Weight.ToString(c)),
                string.Format(c, ValueText, pickup.Value),
            };
        }

        /// <summary>The five lines' points from the box's corner, in the order of <see cref="Lines"/>.</summary>
        public DataPoint[] Points => new[] { Name, Effect, Description, Weight, Value };
    }

    /// <summary>
    /// The heads-up text (hud.json): the virtual screen the original drew on, its fonts, where the clock's lines go on the
    /// HUD and the hero screen, and how messages show.
    /// </summary>
    public sealed class HudData
    {
        public int ScreenWidth { get; internal set; }
        public int ScreenHeight { get; internal set; }
        public IReadOnlyList<HudFont> Fonts { get; internal set; }

        /// <summary>The day period's name on the HUD.</summary>
        public HudText Period { get; internal set; }

        /// <summary>The time on the HUD.</summary>
        public HudText Time { get; internal set; }

        /// <summary>The hero screen's date line.</summary>
        public HudText HeroScreenDate { get; internal set; }

        /// <summary>The hero screen's time line.</summary>
        public HudText HeroScreenTime { get; internal set; }

        public MessageTuning Messages { get; internal set; }

        /// <summary>The status box of names and vitals.</summary>
        public StatusBoxDefinition StatusBox { get; internal set; }

        /// <summary>The health, mana and stamina bars.</summary>
        public BarsDefinition Bars { get; internal set; }

        /// <summary>Where and how the rest prompt is written while the leader lies down (Engine.showRestText).</summary>
        public HudText Rest { get; internal set; }

        /// <summary>The examine box (E, with the cursor over a pickup).</summary>
        public ExamineBoxDefinition Examine { get; internal set; }

        /// <summary>The rest prompt for one hour ("Rest for {0} hour?").</summary>
        public string RestOne { get; internal set; }

        /// <summary>The rest prompt for more hours ("Rest for {0} hours?").</summary>
        public string RestMany { get; internal set; }

        /// <summary>The rest prompt for a sleep of <paramref name="hours"/>: the one-hour text at 1, the plural otherwise.</summary>
        public string RestText(int hours) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, hours == 1 ? RestOne : RestMany, hours);

        public HudFont Font(string name)
        {
            foreach (var f in Fonts) if (f.Name == name) return f;
            return null;
        }

        internal static HudData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var data = new HudData();

            var screen = root.Section("screen");
            data.ScreenWidth = screen.Integer("width", 1, 10000);
            data.ScreenHeight = screen.Integer("height", 1, 10000);
            screen.Done();

            var fonts = new List<HudFont>();
            foreach (var r in root.Objects("fonts"))
            {
                var f = new HudFont { Name = r.Text("name"), Families = r.Texts("families"), Points = r.Positive("points"), Bold = r.Flag("bold") };
                r.Check(f.Families.Count > 0, "families", "must name at least one face");
                if (fonts.Exists(o => o.Name == f.Name)) r.Fail("name", "\"" + f.Name + "\" is listed twice");
                r.Done();
                fonts.Add(f);
            }

            data.Fonts = fonts;

            var clock = root.Section("clock");
            data.Period = ReadText(clock, "period", data, fonts);
            data.Time = ReadText(clock, "time", data, fonts);
            clock.Done();

            var hero = root.Section("heroScreen");
            data.HeroScreenDate = ReadText(hero, "date", data, fonts);
            data.HeroScreenTime = ReadText(hero, "time", data, fonts);
            hero.Done();

            var box = root.Section("statusBox");
            data.StatusBox = new StatusBoxDefinition
            {
                X = box.Number("x", 0f, data.ScreenWidth),
                Y = box.Number("y", 0f, data.ScreenHeight),
                Font = ReadFont(box, fonts),
                Name = box.Pair("name", 0f, data.ScreenWidth),
                Value = box.Pair("value", 0f, data.ScreenWidth),
                Maximum = box.Pair("maximum", 0f, data.ScreenWidth),
                RowSpacing = box.Number("rowSpacing", 0f, data.ScreenHeight),
                TeamSpacing = box.Number("teamSpacing", 0f, data.ScreenWidth),
            };
            box.Done();

            var bars = root.Section("bars");
            data.Bars = new BarsDefinition
            {
                Width = bars.Integer("width", 1, data.ScreenWidth),
                Height = bars.Integer("height", 1, data.ScreenHeight),
            };
            data.Bars.Health = ReadBar(bars, "health", data);
            data.Bars.Mana = ReadBar(bars, "mana", data);
            data.Bars.Stamina = ReadBar(bars, "stamina", data);
            bars.Done();

            var rest = root.Section("rest");
            data.Rest = ReadText(rest, "prompt", data, fonts);
            data.RestOne = rest.Text("one");
            data.RestMany = rest.Text("many");
            rest.Check(data.RestOne.Contains("{0}"), "one", "must hold {0} for the hours");
            rest.Check(data.RestMany.Contains("{0}"), "many", "must hold {0} for the hours");
            rest.Done();

            var ex = root.Section("examine");
            var examine = new ExamineBoxDefinition
            {
                X = ex.Number("x", 0f, data.ScreenWidth),
                Y = ex.Number("y", 0f, data.ScreenHeight),
                Width = ex.Positive("width"),
                Height = ex.Positive("height"),
                Picture = ex.Text("picture"),
                Font = ReadFont(ex, fonts),
                Name = ex.Pair("name", 0f, data.ScreenWidth),
                Effect = ex.Pair("effect", 0f, data.ScreenWidth),
                Description = ex.Pair("description", 0f, data.ScreenWidth),
                Weight = ex.Pair("weight", 0f, data.ScreenWidth),
                Value = ex.Pair("value", 0f, data.ScreenWidth),
                DescriptionText = ex.Text("descriptionText"),
                WeightText = ex.Text("weightText"),
                ValueText = ex.Text("valueText"),
            };
            var exColour = TimeData.ReadBytes(ex, "colour", 4, "an [a, r, g, b] colour of four whole numbers from 0 to 255");
            examine.Colour = exColour == null ? new TextColour(255, 255, 255, 255) : new TextColour(exColour[0], exColour[1], exColour[2], exColour[3]);
            var icon = ex.Section("icon");
            examine.Icon = new DataPoint(icon.Number("x", 0f, data.ScreenWidth), icon.Number("y", 0f, data.ScreenHeight));
            examine.IconSize = icon.Positive("size");
            icon.Done();
            ex.Check(examine.Picture.StartsWith("gfx/", StringComparison.Ordinal), "picture", "must be a path under gfx/");
            foreach (var (key, text) in new[] { ("descriptionText", examine.DescriptionText), ("weightText", examine.WeightText), ("valueText", examine.ValueText) })
                ex.Check(text.Contains("{0}"), key, "must hold {0} for the value");
            ex.Done();
            data.Examine = examine;

            var m = root.Section("messages");
            var tuning = new MessageTuning
            {
                MaxMessages = m.Integer("maxMessages", 1, 10000),
                CharacterWidth = m.Integer("characterWidth", 0, 1000),
                Spacing = m.Integer("spacing", 0, 10000),
                Fade = m.Integer("fade", 1, 255),
                Drift = m.Integer("drift", -1000, 1000),
            };
            var announcement = m.Section("announcement");
            tuning.AnnouncementFont = ReadFont(announcement, fonts);
            var colour = TimeData.ReadBytes(announcement, "colour", 3, "an [r, g, b] colour of three whole numbers from 0 to 255");
            tuning.AnnouncementColour = colour == null ? new TextColour(255, 255, 255, 255) : new TextColour(255, colour[0], colour[1], colour[2]);
            announcement.Done();
            m.Done();
            data.Messages = tuning;
            root.Done();
            return data;
        }

        static BarDefinition ReadBar(ContentReader parent, string key, HudData data)
        {
            var r = parent.Section(key);
            var bar = new BarDefinition
            {
                X = r.Number("x", 0f, data.ScreenWidth),
                Y = r.Number("y", 0f, data.ScreenHeight),
                Frame = r.Text("frame"),
                Fill = r.Text("fill"),
            };
            r.Check(bar.Frame.StartsWith("gfx/", StringComparison.Ordinal), "frame", "must be a path under gfx/");
            r.Check(bar.Fill.StartsWith("gfx/", StringComparison.Ordinal), "fill", "must be a path under gfx/");
            r.Done();
            return bar;
        }

        static HudText ReadText(ContentReader parent, string key, HudData data, List<HudFont> fonts)
        {
            var r = parent.Section(key);
            var text = new HudText
            {
                X = r.Number("x", 0f, data.ScreenWidth),
                Y = r.Number("y", 0f, data.ScreenHeight),
                Font = ReadFont(r, fonts),
            };
            var c = TimeData.ReadBytes(r, "colour", 4, "an [a, r, g, b] colour of four whole numbers from 0 to 255");
            text.Colour = c == null ? new TextColour(255, 255, 255, 255) : new TextColour(c[0], c[1], c[2], c[3]);
            r.Done();
            return text;
        }

        static HudFont ReadFont(ContentReader r, List<HudFont> fonts)
        {
            var name = r.Text("font");
            var font = fonts.Find(f => f.Name == name);
            if (font == null && name.Length > 0) r.Fail("font", "\"" + name + "\" is not one of the fonts");
            return font;
        }
    }
}

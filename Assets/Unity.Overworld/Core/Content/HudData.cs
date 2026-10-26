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

using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A picture stretched over a box of the 640 x 480 screen (the hero screen's and the options menu's menu.png).</summary>
    public sealed class ScreenPicture
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public string Picture { get; internal set; }
    }

    /// <summary>
    /// The hero screen (Gui/HeroScreen.vb renderheroScreen, RECONSTRUCTION 13): menu.png over its box, the date and time
    /// lines (hud.json "heroScreen" "date" and "time", <see cref="HudData.HeroScreenDate"/>), and the party's experience
    /// (the rebuild's line; BIBLE).
    /// </summary>
    public sealed class HeroScreenDefinition
    {
        public ScreenPicture Box { get; internal set; }
        public HudText Experience { get; internal set; }

        /// <summary>"Experience: {0}".</summary>
        public string ExperienceText { get; internal set; }

        public string ExperienceLine(int experience) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, ExperienceText, experience);
    }

    /// <summary>One row of a page of the options menu: its text and y, and what Enter on it does on the main page.</summary>
    public sealed class OptionsRow
    {
        public string Text { get; internal set; }
        public float Y { get; internal set; }

        /// <summary>The page Enter opens (main page only), or null.</summary>
        public string Opens { get; internal set; }

        /// <summary>Enter quits the game (Exit).</summary>
        public bool Quits { get; internal set; }
    }

    /// <summary>A page of the options menu (theOptionArea): its name and rows.</summary>
    public sealed class OptionsPage
    {
        public string Name { get; internal set; }
        public IReadOnlyList<OptionsRow> Rows { get; internal set; }
    }

    /// <summary>
    /// The options menu (Gui/Options.vb, Input.actionUp / actionDown / actionLeft / actionPickup; RECONSTRUCTION 13): its
    /// picture, the title, the cursor and the rows of each page. The first page is the main page.
    /// </summary>
    public sealed class OptionsMenuDefinition
    {
        public ScreenPicture Box { get; internal set; }
        public HudText Title { get; internal set; }
        public string TitleText { get; internal set; }
        public HudFont Font { get; internal set; }
        public TextColour Colour { get; internal set; }
        public string CursorText { get; internal set; }
        public float CursorX { get; internal set; }
        public float RowX { get; internal set; }

        /// <summary>The rows the cursor steps through on every page (maxOptions + 1 = 4).</summary>
        public int Rows { get; internal set; }

        public IReadOnlyList<OptionsPage> Pages { get; internal set; }

        /// <summary>The index of page <paramref name="name"/>, or -1.</summary>
        public int PageIndex(string name)
        {
            for (var i = 0; i < Pages.Count; i++)
                if (Pages[i].Name == name) return i;
            return -1;
        }
    }

    /// <summary>The frame-rate counter's text and its sampling (Gui/Fps.vb).</summary>
    public sealed class FpsDefinition
    {
        public HudText Text { get; internal set; }

        /// <summary>"{0} fps".</summary>
        public string Format { get; internal set; }

        /// <summary>iFPSProfileSpeed: the rate is worked out every this many milliseconds (100).</summary>
        public int SampleMilliseconds { get; internal set; }

        public string Line(int rate) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Format, rate);
    }

    /// <summary>
    /// The turn bars (Gui/TurnBar.vb, RECONSTRUCTION 13): a stamina gauge per hero at its slot, the leader's first and,
    /// in team mode, the others'.
    /// </summary>
    public sealed class TurnBarsDefinition
    {
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public IReadOnlyList<DataPoint> Slots { get; internal set; }

        /// <summary>The bar over a gauge that is not full (stamina_bar), over a full one (ready_bar), and the fill (stamina_fill).</summary>
        public string Frame { get; internal set; }

        public string Ready { get; internal set; }
        public string Fill { get; internal set; }

        /// <summary>TurnbarWidth x stamina / maxStamina as VB assigned it to an Integer (half to even), never below 0.</summary>
        public int FillWidth(int stamina, int max)
        {
            if (max <= 0) return 0;
            var fill = (int)Math.Round(Width * ((double)stamina / max), MidpointRounding.ToEven);
            return fill < 0 ? 0 : fill;
        }
    }

    /// <summary>The screen-space arrow drawn over the HUD's panel and buttons (Cursor/Cursor2D.vb).</summary>
    public sealed class Cursor2DDefinition
    {
        public string Picture { get; internal set; }
        public float Size { get; internal set; }

        /// <summary>From the virtual mouse to the picture's corner (-9, +4).</summary>
        public DataPoint Offset { get; internal set; }

        /// <summary>
        /// rendercursor2D's bShowCursor2D: the virtual mouse is over the open inventory panel, the team button or the
        /// inventory button.
        /// </summary>
        public static bool Shown(HudData hud, TeamButtonDefinition team, bool packOpen, float mouseX, float mouseY) =>
            (packOpen && hud.Inventory != null && hud.Inventory.Contains(mouseX, mouseY)) ||
            (team != null && team.Contains(mouseX, mouseY)) ||
            (hud.InventoryButton != null && hud.InventoryButton.Contains(mouseX, mouseY));
    }

    /// <summary>The name over a hero the world cursor is on (Cursor.checkCursorOverHero), as 3D text.</summary>
    public sealed class HoverNameDefinition
    {
        public DataPoint LabelPoint { get; internal set; }
        public DataPoint LabelOffset { get; internal set; }

        /// <summary>The offset while the hero kneels (lower).</summary>
        public DataPoint KneelingOffset { get; internal set; }

        public string Marker { get; internal set; }
        public float MarkerScale { get; internal set; }
        public float NameScale { get; internal set; }
    }

    /// <summary>The tutorial queued at start and on Reset (Root.sayTutorial).</summary>
    public sealed class TutorialDefinition
    {
        public HudFont Font { get; internal set; }
        public TextColour Colour { get; internal set; }
        public IReadOnlyList<string> Lines { get; internal set; }
    }

    /// <summary>The loading pictures (frmMain.Startup) and how long the rebuild holds one.</summary>
    public sealed class LoadingDefinition
    {
        public IReadOnlyList<string> Pictures { get; internal set; }
        public float Seconds { get; internal set; }

        /// <summary>getRndInt(1, 12): one of the pictures at random.</summary>
        public string Pick(Random random) => Pictures[(random ?? new Random()).Next(0, Pictures.Count)];
    }

    public sealed partial class HudData
    {
        /// <summary>The hero screen (Page Up).</summary>
        public HeroScreenDefinition HeroScreen { get; internal set; }

        /// <summary>The options menu (F11 or Escape).</summary>
        public OptionsMenuDefinition Options { get; internal set; }

        /// <summary>The frame-rate counter (F4).</summary>
        public FpsDefinition Fps { get; internal set; }

        /// <summary>The pause banner (Pause) and its text.</summary>
        public HudText Pause { get; internal set; }

        public string PauseText { get; internal set; }

        public TurnBarsDefinition TurnBars { get; internal set; }
        public Cursor2DDefinition Cursor2D { get; internal set; }
        public HoverNameDefinition HoverName { get; internal set; }
        public TutorialDefinition Tutorial { get; internal set; }
        public LoadingDefinition Loading { get; internal set; }

        static ScreenPicture ReadBox(ContentReader r, HudData data)
        {
            var box = new ScreenPicture
            {
                X = r.Number("x", 0f, data.ScreenWidth),
                Y = r.Number("y", 0f, data.ScreenHeight),
                Width = r.Positive("width"),
                Height = r.Positive("height"),
                Picture = ReadPicture(r, "picture"),
            };
            r.Check(box.X + box.Width <= data.ScreenWidth && box.Y + box.Height <= data.ScreenHeight, "x", "must keep the box on the screen");
            return box;
        }

        static HeroScreenDefinition ReadHeroScreen(ContentReader r, HudData data, List<HudFont> fonts)
        {
            var screen = new HeroScreenDefinition
            {
                Box = ReadBox(r, data),
                Experience = ReadText(r, "experience", data, fonts),
                ExperienceText = r.Text("experienceText"),
            };
            r.Check(screen.ExperienceText.Contains("{0}"), "experienceText", "must hold {0} for the experience");
            return screen;
        }

        static OptionsMenuDefinition ReadOptions(ContentReader r, HudData data, List<HudFont> fonts)
        {
            var menu = new OptionsMenuDefinition
            {
                Box = ReadBox(r, data),
                Title = ReadText(r, "title", data, fonts),
                TitleText = r.Text("titleText"),
                Font = ReadFont(r, fonts),
                Colour = ReadColour(r, "colour"),
                CursorText = r.Text("cursorText"),
                CursorX = r.Number("cursorX", 0f, data.ScreenWidth),
                RowX = r.Number("rowX", 0f, data.ScreenWidth),
                Rows = r.Integer("rows", 1, 100),
            };
            var pages = new List<OptionsPage>();
            foreach (var p in r.Objects("pages"))
            {
                var page = new OptionsPage { Name = p.Text("name") };
                var rows = new List<OptionsRow>();
                foreach (var row in p.Objects("rows"))
                {
                    rows.Add(new OptionsRow
                    {
                        Text = row.Text("text", allowEmpty: true),
                        Y = row.Number("y", 0f, data.ScreenHeight),
                        Opens = row.Has("opens") ? row.Text("opens") : null,
                        Quits = row.Flag("quits", false),
                    });
                    row.Done();
                }

                page.Rows = rows;
                p.Check(rows.Count == menu.Rows, "rows", "must have " + menu.Rows + " rows, one for each place of the cursor");
                if (pages.Exists(o => o.Name == page.Name)) p.Fail("name", "\"" + page.Name + "\" is listed twice");
                p.Done();
                pages.Add(page);
            }

            menu.Pages = pages;
            r.Check(pages.Count > 0, "pages", "must list the main page first");
            for (var i = 0; i < pages.Count; i++)
                foreach (var row in pages[i].Rows)
                {
                    if (row.Opens != null && menu.PageIndex(row.Opens) < 0) r.Fail("pages", "the row \"" + row.Text + "\" opens \"" + row.Opens + "\", which is not a page");
                    if (i > 0 && (row.Opens != null || row.Quits)) r.Fail("pages", "only the main page's rows open pages or quit (\"" + row.Text + "\")");
                }

            r.Done();
            return menu;
        }

        static void ReadInterface(ContentReader root, HudData data, List<HudFont> fonts)
        {
            var hero = root.Section("heroScreen");
            data.HeroScreenDate = ReadText(hero, "date", data, fonts);
            data.HeroScreenTime = ReadText(hero, "time", data, fonts);
            data.HeroScreen = ReadHeroScreen(hero, data, fonts);
            hero.Done();

            data.Options = ReadOptions(root.Section("options"), data, fonts);

            var fps = root.Section("fps");
            data.Fps = new FpsDefinition
            {
                Text = ReadText(fps, "text", data, fonts),
                Format = fps.Text("format"),
                SampleMilliseconds = fps.Integer("sampleMilliseconds", 1, 10000),
            };
            fps.Check(data.Fps.Format.Contains("{0}"), "format", "must hold {0} for the rate");
            fps.Done();

            var pause = root.Section("pause");
            data.Pause = ReadText(pause, "text", data, fonts);
            data.PauseText = pause.Text("caption");
            pause.Done();

            var bars = root.Section("turnBars");
            var slots = new List<DataPoint>();
            foreach (var s in bars.Objects("slots"))
            {
                slots.Add(new DataPoint(s.Number("x", 0f, data.ScreenWidth), s.Number("y", 0f, data.ScreenHeight)));
                s.Done();
            }

            data.TurnBars = new TurnBarsDefinition
            {
                Width = bars.Integer("width", 1, data.ScreenWidth),
                Height = bars.Integer("height", 1, data.ScreenHeight),
                Slots = slots,
                Frame = ReadPicture(bars, "frame"),
                Ready = ReadPicture(bars, "ready"),
                Fill = ReadPicture(bars, "fill"),
            };
            bars.Check(slots.Count > 0, "slots", "must give at least the leader's slot");
            bars.Done();

            var cursor = root.Section("cursor2D");
            data.Cursor2D = new Cursor2DDefinition
            {
                Picture = ReadPicture(cursor, "picture"),
                Size = cursor.Positive("size"),
                Offset = cursor.Pair("offset", -100f, 100f),
            };
            cursor.Done();

            var hover = root.Section("hoverName");
            data.HoverName = new HoverNameDefinition
            {
                LabelPoint = hover.Pair("labelPoint", -1f, 1f),
                LabelOffset = hover.Pair("labelOffset", -1f, 1f),
                KneelingOffset = hover.Pair("kneelingOffset", -1f, 1f),
                Marker = hover.Text("marker"),
                MarkerScale = hover.Positive("markerScale"),
                NameScale = hover.Positive("nameScale"),
            };
            hover.Done();

            var tutorial = root.Section("tutorial");
            var colour = TimeData.ReadBytes(tutorial, "colour", 3, "an [r, g, b] colour of three whole numbers from 0 to 255");
            data.Tutorial = new TutorialDefinition
            {
                Font = ReadFont(tutorial, fonts),
                Colour = colour == null ? new TextColour(255, 255, 255, 255) : new TextColour(255, colour[0], colour[1], colour[2]),
                Lines = tutorial.Texts("lines"),
            };
            tutorial.Check(data.Tutorial.Lines.Count > 0, "lines", "must hold the tutorial's lines");
            tutorial.Done();

            var loading = root.Section("loading");
            var pictures = loading.Texts("pictures");
            foreach (var p in pictures)
                loading.Check(p.StartsWith("gfx/", StringComparison.Ordinal), "pictures", "must be paths under gfx/ (\"" + p + "\")");
            data.Loading = new LoadingDefinition { Pictures = pictures, Seconds = loading.Number("seconds", 0f, 60f) };
            loading.Check(pictures.Count > 0, "pictures", "must name at least one picture");
            loading.Done();
        }
    }
}

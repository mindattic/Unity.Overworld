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

    /// <summary>A stack's count over its icon (fontAmount): at <see cref="Single"/> from the icon for 2 to 9, <see cref="Double"/> beyond.</summary>
    public sealed class CountText
    {
        public HudFont Font { get; internal set; }
        public TextColour Colour { get; internal set; }
        public DataPoint Single { get; internal set; }
        public DataPoint Double { get; internal set; }

        /// <summary>Where the count of <paramref name="amount"/> goes from its icon's corner; null when it shows none (1 or less).</summary>
        public DataPoint? Offset(int amount) => amount <= 1 ? (DataPoint?)null : amount <= 9 ? Single : Double;
    }

    /// <summary>
    /// The inventory panel (Inventory.vb, RECONSTRUCTION 8.3): the picture at (X, Y) of Width x Height, the icon slots
    /// from its corner (ten to a row, rows <see cref="RowPitch"/> apart), the item and swap cursors, and its text.
    /// </summary>
    public sealed class InventoryPanelDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public string Picture { get; internal set; }

        /// <summary>The first icon's corner from the panel's (8, 8).</summary>
        public DataPoint Icons { get; internal set; }

        public float IconSize { get; internal set; }
        public float ColumnPitch { get; internal set; }
        public float RowPitch { get; internal set; }

        /// <summary>Icons to a row (10); up and down move the cursor by this many.</summary>
        public int Columns { get; internal set; }

        /// <summary>The icon slots the panel has (101).</summary>
        public int Slots { get; internal set; }

        public string ItemCursor { get; internal set; }
        public string SwapCursor { get; internal set; }
        public HudFont Font { get; internal set; }

        /// <summary>The weight, gold, name, item weight and value colour (light green) and the effect and description colour (cyan).</summary>
        public TextColour Green { get; internal set; }

        public TextColour Cyan { get; internal set; }
        public DataPoint Weight { get; internal set; }
        public DataPoint Gold { get; internal set; }
        public DataPoint Name { get; internal set; }
        public DataPoint Effect { get; internal set; }
        public DataPoint Description { get; internal set; }
        public DataPoint ItemWeight { get; internal set; }

        /// <summary>The value's right-hand x from the panel (321) and y; the text starts <see cref="ValueCharacter"/> pixels left a character.</summary>
        public DataPoint Value { get; internal set; }

        public float ValueCharacter { get; internal set; }
        public string WeightText { get; internal set; }
        public string GoldText { get; internal set; }
        public string AmountText { get; internal set; }
        public string ValueText { get; internal set; }
        public CountText Count { get; internal set; }

        /// <summary>The top-left corner of icon slot <paramref name="slot"/> on the 640 x 480 screen.</summary>
        public DataPoint IconAt(int slot) =>
            new DataPoint(X + Icons.X + ColumnPitch * (slot % Columns), Y + Icons.Y + RowPitch * (slot / Columns));

        /// <summary>The virtual mouse at (<paramref name="mouseX"/>, <paramref name="mouseY"/>) is over the panel (bShowCursor2D, inclusive).</summary>
        public bool Contains(float mouseX, float mouseY) => mouseX >= X && mouseX <= X + Width && mouseY >= Y && mouseY <= Y + Height;

        /// <summary>
        /// checkCursorOverInventory: the first of the <paramref name="count"/> icons the virtual mouse is over (within half an
        /// icon of its centre on both axes), or -1. (The original's vertical test reached from its centre to the screen's top
        /// edge, so the hit was the band above each icon; BIBLE.)
        /// </summary>
        public int SlotUnder(float mouseX, float mouseY, int count)
        {
            if (!Contains(mouseX, mouseY)) return -1;
            var half = IconSize / 2f;
            for (var n = 0; n < count && n < Slots; n++)
            {
                var at = IconAt(n);
                if (WorldCamera.Near(mouseX, at.X + half, half) && WorldCamera.Near(mouseY, at.Y + half, half)) return n;
            }

            return -1;
        }

        /// <summary>"Weight: n / limit", each to one decimal place (FormatNumber(x, 1)).</summary>
        public string WeightLine(float weight, float limit) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, WeightText, Number(weight), Number(limit));

        /// <summary>FormatNumber(x, 1): one decimal place, thousands grouped.</summary>
        public static string Number(float value) => ((double)value).ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The quick bar (QuickBar.vb, RECONSTRUCTION 8.5): the picture at the bottom right and the first ten stacks' icons in it.</summary>
    public sealed class QuickBarDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public string Picture { get; internal set; }

        /// <summary>The first icon's x from the bar (1); the icons sit at the bar's top.</summary>
        public float IconX { get; internal set; }

        public float Pitch { get; internal set; }
        public float IconSize { get; internal set; }

        /// <summary>The slots the bar shows and the 1 to 0 keys use (10).</summary>
        public int Slots { get; internal set; }

        public CountText Count { get; internal set; }

        /// <summary>The top-left corner of quick slot <paramref name="slot"/>.</summary>
        public DataPoint IconAt(int slot) => new DataPoint(X + IconX + Pitch * slot, Y);
    }

    /// <summary>
    /// A button of the 640 x 480 screen (InventoryBtn.vb): its square, the hit box Cursor2D tested (across its width, from
    /// <see cref="HitTop"/> above its top to <see cref="HitBottom"/> from its bottom), and its pictures on and off.
    /// </summary>
    public sealed class ScreenButtonDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float HitTop { get; internal set; }
        public float HitBottom { get; internal set; }
        public string OnPicture { get; internal set; }
        public string OffPicture { get; internal set; }
        public string Picture(bool on) => on ? OnPicture : OffPicture;

        /// <summary>The virtual mouse is over the button (A_between_BC, inclusive).</summary>
        public bool Contains(float mouseX, float mouseY) =>
            mouseX >= X && mouseX <= X + Width && mouseY >= Y + HitTop && mouseY <= Y + Height + HitBottom;
    }

    /// <summary>
    /// The chosen target of an item (HeroSelect.vb renderHeroSelect, RECONSTRUCTION 4.7 and 8.3): select_hero.png on the
    /// hero's quad, and a marker and its name in 3D text at the hero's position plus <see cref="LabelPoint"/> times the
    /// hero size plus <see cref="LabelOffset"/>.
    /// </summary>
    public sealed class TargetMarkerDefinition
    {
        public string Picture { get; internal set; }
        public DataPoint LabelPoint { get; internal set; }
        public DataPoint LabelOffset { get; internal set; }
        public string Marker { get; internal set; }
        public float MarkerScale { get; internal set; }
        public float NameScale { get; internal set; }
    }

    /// <summary>
    /// The quest screen (Gui/QuestScreen.vb renderQuestScreen and showQuestPoints, RECONSTRUCTION 10): the page's picture
    /// over (X, Y) of Width x Height, the title and the lines (each <see cref="LineSpacing"/> lower, by its index) at their
    /// points from the screen's corner in its font and colours, and the "*" drawn in the world at every incomplete objective.
    /// </summary>
    public sealed class QuestScreenDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }

        /// <summary>The picture of the main page, of the subquest list and a subquest's page, and of the finished page.</summary>
        public string MainPicture { get; internal set; }

        public string SubquestsPicture { get; internal set; }
        public string FinishedPicture { get; internal set; }
        public HudFont Font { get; internal set; }

        /// <summary>The quest's text, from the screen's corner (+160, +25).</summary>
        public DataPoint Title { get; internal set; }

        /// <summary>The first line, from the screen's corner (+160, +60); line n is n x <see cref="LineSpacing"/> lower.</summary>
        public DataPoint FirstLine { get; internal set; }

        public float LineSpacing { get; internal set; }

        /// <summary>The title, an open objective and a subquest on the list (white).</summary>
        public TextColour Normal { get; internal set; }

        /// <summary>A completed objective and a finished quest (half-transparent white).</summary>
        public TextColour Faded { get; internal set; }

        /// <summary>The subquest chosen on the list (pale yellow).</summary>
        public TextColour Chosen { get; internal set; }

        /// <summary>"~ {0}", "X {0}" and "o {0}": an open objective, a completed one, a finished quest.</summary>
        public string OpenText { get; internal set; }

        public string DoneText { get; internal set; }
        public string FinishedText { get; internal set; }

        /// <summary>The 3D text drawn at an incomplete objective ("*") and its renderText scale (0.02).</summary>
        public string Marker { get; internal set; }

        public float MarkerScale { get; internal set; }

        /// <summary>The picture of <paramref name="page"/>.</summary>
        public string Picture(QuestPage page) =>
            page == QuestPage.Main ? MainPicture : page == QuestPage.Finished ? FinishedPicture : SubquestsPicture;

        /// <summary>The top-left point of line <paramref name="index"/> on the 640 x 480 screen.</summary>
        public DataPoint LineAt(int index) => new DataPoint(X + FirstLine.X, Y + FirstLine.Y + LineSpacing * index);

        /// <summary>The top-left point of the title on the 640 x 480 screen.</summary>
        public DataPoint TitleAt => new DataPoint(X + Title.X, Y + Title.Y);

        /// <summary>
        /// renderQuestScreen's text for <paramref name="view"/>: each line's text, its point and colour, in draw order (the
        /// title first where the page has one).
        /// </summary>
        public List<(string Text, DataPoint At, TextColour Colour)> Lines(QuestLog log, QuestScreenView view)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new List<(string, DataPoint, TextColour)>();
            void ShowQuest(Quest q)
            {
                lines.Add((q.Text, TitleAt, Normal));
                for (var o = 0; o < q.Objectives.Count; o++)
                {
                    var objective = q.Objectives[o];
                    lines.Add((string.Format(c, objective.Complete ? DoneText : OpenText, objective.Text), LineAt(o), objective.Complete ? Faded : Normal));
                }
            }

            switch (view.Page)
            {
                case QuestPage.Main:
                    ShowQuest(log.Current);
                    break;
                case QuestPage.Subquests:
                    for (var o = 0; o < log.Subquests.Count; o++)
                        lines.Add((log.Subquests[o].Text, LineAt(o), o == view.Subquest ? Chosen : Normal));
                    break;
                case QuestPage.SubquestInfo:
                    if (view.Subquest >= 0 && view.Subquest < log.Subquests.Count) ShowQuest(log.Subquests[view.Subquest]);
                    break;
                default:
                    // Each at its quest's own line, so an unfinished quest leaves its line empty.
                    for (var o = 0; o < log.Quests.Count; o++)
                        if (log.Quests[o].Status == QuestStatus.Success)
                            lines.Add((string.Format(c, FinishedText, log.Quests[o].Text), LineAt(o), Faded));
                    break;
            }

            return lines;
        }
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

        /// <summary>The inventory panel (Delete or the inventory button).</summary>
        public InventoryPanelDefinition Inventory { get; internal set; }

        /// <summary>The quick bar (Q).</summary>
        public QuickBarDefinition QuickBar { get; internal set; }

        /// <summary>The inventory button (inventory_Open while the panel is open, inventory_Closed otherwise).</summary>
        public ScreenButtonDefinition InventoryButton { get; internal set; }

        /// <summary>"You are overburdened", written at the centre while the leader is burdened, in the status box's colour.</summary>
        public HudText Burdened { get; internal set; }

        public string BurdenedText { get; internal set; }

        /// <summary>The marker over an item's chosen target.</summary>
        public TargetMarkerDefinition Target { get; internal set; }

        /// <summary>The quest screen (Page Down) and the objectives' markers.</summary>
        public QuestScreenDefinition QuestScreen { get; internal set; }

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

            data.Inventory = ReadInventory(root.Section("inventory"), data, fonts);
            data.QuickBar = ReadQuickBar(root.Section("quickBar"), data, fonts);
            data.InventoryButton = ReadButton(root.Section("inventoryButton"), data);
            var burdened = root.Section("burdened");
            data.Burdened = new HudText
            {
                X = burdened.Number("x", 0f, data.ScreenWidth),
                Y = burdened.Number("y", 0f, data.ScreenHeight),
                Font = ReadFont(burdened, fonts),
                Colour = new TextColour(255, 255, 255, 255),
            };
            data.BurdenedText = burdened.Text("text");
            burdened.Done();
            var target = root.Section("target");
            data.Target = new TargetMarkerDefinition
            {
                Picture = target.Text("picture"),
                LabelPoint = target.Pair("labelPoint", -1f, 1f),
                LabelOffset = target.Pair("labelOffset", -1f, 1f),
                Marker = target.Text("marker"),
                MarkerScale = target.Positive("markerScale"),
                NameScale = target.Positive("nameScale"),
            };
            target.Check(data.Target.Picture.StartsWith("gfx/", StringComparison.Ordinal), "picture", "must be a path under gfx/");
            target.Done();
            data.QuestScreen = ReadQuestScreen(root.Section("questScreen"), data, fonts);

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

        static TextColour ReadColour(ContentReader r, string key)
        {
            var c = TimeData.ReadBytes(r, key, 4, "an [a, r, g, b] colour of four whole numbers from 0 to 255");
            return c == null ? new TextColour(255, 255, 255, 255) : new TextColour(c[0], c[1], c[2], c[3]);
        }

        static string ReadPicture(ContentReader r, string key)
        {
            var file = r.Text(key);
            r.Check(file.StartsWith("gfx/", StringComparison.Ordinal), key, "must be a path under gfx/");
            return file;
        }

        static CountText ReadCount(ContentReader parent, List<HudFont> fonts)
        {
            var r = parent.Section("count");
            var count = new CountText
            {
                Font = ReadFont(r, fonts),
                Colour = ReadColour(r, "colour"),
                Single = r.Pair("single", 0f, 100f),
                Double = r.Pair("double", 0f, 100f),
            };
            r.Done();
            return count;
        }

        static InventoryPanelDefinition ReadInventory(ContentReader r, HudData data, List<HudFont> fonts)
        {
            var icons = r.Section("icons");
            var panel = new InventoryPanelDefinition
            {
                X = r.Number("x", 0f, data.ScreenWidth),
                Y = r.Number("y", 0f, data.ScreenHeight),
                Width = r.Positive("width"),
                Height = r.Positive("height"),
                Picture = ReadPicture(r, "picture"),
                Icons = new DataPoint(icons.Number("x", 0f, data.ScreenWidth), icons.Number("y", 0f, data.ScreenHeight)),
                IconSize = icons.Positive("size"),
                ColumnPitch = icons.Positive("columnPitch"),
                RowPitch = icons.Positive("rowPitch"),
                Columns = icons.Integer("columns", 1, 100),
                Slots = icons.Integer("slots", 1, 10000),
            };
            icons.Done();
            panel.ItemCursor = ReadPicture(r, "itemCursor");
            panel.SwapCursor = ReadPicture(r, "swapCursor");
            panel.Font = ReadFont(r, fonts);
            panel.Green = ReadColour(r, "green");
            panel.Cyan = ReadColour(r, "cyan");
            panel.Weight = r.Pair("weight", 0f, data.ScreenWidth);
            panel.Gold = r.Pair("gold", 0f, data.ScreenWidth);
            panel.Name = r.Pair("name", 0f, data.ScreenWidth);
            panel.Effect = r.Pair("effect", 0f, data.ScreenWidth);
            panel.Description = r.Pair("description", 0f, data.ScreenWidth);
            panel.ItemWeight = r.Pair("itemWeight", 0f, data.ScreenWidth);
            panel.Value = r.Pair("value", 0f, data.ScreenWidth);
            panel.ValueCharacter = r.Number("valueCharacter", 0f, 100f);
            panel.WeightText = r.Text("weightText");
            panel.GoldText = r.Text("goldText");
            panel.AmountText = r.Text("amountText");
            panel.ValueText = r.Text("valueText");
            panel.Count = ReadCount(r, fonts);
            r.Check(panel.WeightText.Contains("{0}") && panel.WeightText.Contains("{1}"), "weightText", "must hold {0} for the weight and {1} for the limit");
            r.Check(panel.GoldText.Contains("{0}"), "goldText", "must hold {0} for the gold");
            r.Check(panel.AmountText.Contains("{0}") && panel.AmountText.Contains("{1}"), "amountText", "must hold {0} for the name and {1} for the amount");
            r.Check(panel.ValueText.Contains("{0}"), "valueText", "must hold {0} for the value");
            r.Check(panel.X + panel.Width <= data.ScreenWidth && panel.Y + panel.Height <= data.ScreenHeight, "x", "must keep the panel on the screen");
            r.Done();
            return panel;
        }

        static QuickBarDefinition ReadQuickBar(ContentReader r, HudData data, List<HudFont> fonts)
        {
            var bar = new QuickBarDefinition
            {
                X = r.Number("x", 0f, data.ScreenWidth),
                Y = r.Number("y", 0f, data.ScreenHeight),
                Width = r.Positive("width"),
                Height = r.Positive("height"),
                Picture = ReadPicture(r, "picture"),
                IconX = r.Number("iconX", 0f, data.ScreenWidth),
                Pitch = r.Positive("pitch"),
                IconSize = r.Positive("iconSize"),
                Slots = r.Integer("slots", 1, 10),
                Count = ReadCount(r, fonts),
            };
            r.Done();
            return bar;
        }

        static QuestScreenDefinition ReadQuestScreen(ContentReader r, HudData data, List<HudFont> fonts)
        {
            var pictures = r.Section("pictures");
            var screen = new QuestScreenDefinition
            {
                X = r.Number("x", 0f, data.ScreenWidth),
                Y = r.Number("y", 0f, data.ScreenHeight),
                Width = r.Positive("width"),
                Height = r.Positive("height"),
                MainPicture = ReadPicture(pictures, "main"),
                SubquestsPicture = ReadPicture(pictures, "subquests"),
                FinishedPicture = ReadPicture(pictures, "finished"),
            };
            pictures.Done();
            screen.Font = ReadFont(r, fonts);
            screen.Title = r.Pair("title", 0f, data.ScreenWidth);
            screen.FirstLine = r.Pair("firstLine", 0f, data.ScreenWidth);
            screen.LineSpacing = r.Positive("lineSpacing");
            screen.Normal = ReadColour(r, "normal");
            screen.Faded = ReadColour(r, "faded");
            screen.Chosen = ReadColour(r, "chosen");
            screen.OpenText = r.Text("openText");
            screen.DoneText = r.Text("doneText");
            screen.FinishedText = r.Text("finishedText");
            screen.Marker = r.Text("marker");
            screen.MarkerScale = r.Positive("markerScale");
            foreach (var (key, text) in new[] { ("openText", screen.OpenText), ("doneText", screen.DoneText), ("finishedText", screen.FinishedText) })
                r.Check(text.Contains("{0}"), key, "must hold {0} for the text");
            r.Check(screen.X + screen.Width <= data.ScreenWidth && screen.Y + screen.Height <= data.ScreenHeight, "x", "must keep the screen on the screen");
            r.Done();
            return screen;
        }

        static ScreenButtonDefinition ReadButton(ContentReader r, HudData data)
        {
            var position = r.Pair("position", 0f);
            var size = r.Pair("size", 1f);
            var hit = r.Pair("hitTopBottom");
            var button = new ScreenButtonDefinition
            {
                X = position.X,
                Y = position.Y,
                Width = size.X,
                Height = size.Y,
                HitTop = hit.X,
                HitBottom = hit.Y,
                OnPicture = ReadPicture(r, "open"),
                OffPicture = ReadPicture(r, "closed"),
            };
            r.Check(button.X + button.Width <= data.ScreenWidth && button.Y + button.Height <= data.ScreenHeight, "position", "must keep the button on the screen");
            r.Check(button.Y + button.HitTop < button.Y + button.Height + button.HitBottom, "hitTopBottom", "must leave the hit box some height");
            r.Done();
            return button;
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

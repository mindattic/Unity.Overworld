using System;
using System.Collections.Generic;
using MindAttic.Overworld.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The heads-up text, in UI Toolkit over the world: the day period's name and the time where renderStatus wrote them
    /// (RECONSTRUCTION 6.1, 13), and the queued messages (the period announcements) where renderMessages drew them. The
    /// original wrote on a 640 x 480 screen the window stretched, so every point is placed as that same fraction of the
    /// view and the fonts scale with its height. The HUD is not lit: it never takes the clock's light.
    /// Built in code (<see cref="Create"/>); reads <see cref="WorldClock"/> and <see cref="MessageBoard"/> and never changes
    /// game state. The hero screen's date and time lines (hud.json "heroScreen") come with that screen (story A20).
    /// Once <see cref="ShowVitals"/> is called it also writes the status box (the leader's name, HP, MP and AP, and the
    /// team's beside it, in the inverse of the clock's light as renderStatus did) and draws the health, mana and stamina
    /// bars at the bottom left (U.OVW-US-A10).
    /// </summary>
    public sealed class HudPresenter : MonoBehaviour
    {
        /// <summary>Over the team button and the world: the panel draws after the cameras.</summary>
        public const int PanelOrder = 10;

        readonly List<Label> messageLabels = new List<Label>();
        readonly List<Label> statusLabels = new List<Label>();
        readonly Dictionary<BarDefinition, (VisualElement Fill, VisualElement Frame)> bars = new Dictionary<BarDefinition, (VisualElement, VisualElement)>();
        VisualElement statusLayer;
        TextureLibrary textures;
        readonly Dictionary<HudFont, Font> fonts = new Dictionary<HudFont, Font>();
        PanelSettings settings;
        VisualElement messageLayer;

        public HudData Hud { get; private set; }
        public WorldClock Clock { get; private set; }
        public MessageBoard Messages { get; private set; }
        public UIDocument Document { get; private set; }

        /// <summary>The day period's name on the HUD.</summary>
        public Label PeriodLabel { get; private set; }

        /// <summary>The time on the HUD.</summary>
        public Label TimeLabel { get; private set; }

        /// <summary>The labels of the messages shown now, in <see cref="MessageBoard.Shown"/> order (hidden ones beyond).</summary>
        public IReadOnlyList<Label> MessageLabels => messageLabels;

        /// <summary>The party whose vitals the status box and bars show (null until <see cref="ShowVitals"/>).</summary>
        public Party Party { get; private set; }

        /// <summary>The player whose leader the status box and bars show first.</summary>
        public PlayerId Player { get; private set; }

        /// <summary>The status box's labels shown now, in <see cref="StatusBoxView.Lines"/> order (hidden ones beyond).</summary>
        public IReadOnlyList<Label> StatusLabels => statusLabels;

        /// <summary>The fill of one of the bars (its width is the value's share), or null before <see cref="ShowVitals"/>.</summary>
        public VisualElement BarFill(BarDefinition bar) => bar != null && bars.TryGetValue(bar, out var b) ? b.Fill : null;

        /// <summary>The frame of one of the bars, drawn over its fill.</summary>
        public VisualElement BarFrame(BarDefinition bar) => bar != null && bars.TryGetValue(bar, out var b) ? b.Frame : null;

        /// <summary>
        /// Shows <paramref name="party"/>'s vitals from now on: the status box for the hero <paramref name="player"/> leads
        /// (and the team) and that hero's health, mana and stamina bars.
        /// </summary>
        public void ShowVitals(Party party, PlayerId player, TextureLibrary library = null)
        {
            Party = party;
            Player = player;
            textures = library ?? textures ?? new TextureLibrary();
            if (bars.Count == 0 && Hud.Bars != null && statusLayer != null)
            {
                // Health, mana, then stamina, each fill drawn first and its bar over it (renderHealthBar, rendermanaBar, renderStaminaBar).
                foreach (var bar in new[] { Hud.Bars.Health, Hud.Bars.Mana, Hud.Bars.Stamina })
                {
                    var fill = NewPicture(statusLayer, "fill " + bar.Fill, bar.Fill);
                    var frame = NewPicture(statusLayer, "bar " + bar.Frame, bar.Frame);
                    bars[bar] = (fill, frame);
                }
            }

            Refresh();
        }

        VisualElement NewPicture(VisualElement parent, string name, string file)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            var texture = textures.Load(file);
            if (texture != null) e.style.backgroundImage = new StyleBackground(texture);
            // The original stretched the whole picture over the quad, however wide the fill was.
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            parent.Add(e);
            return e;
        }

        public static HudPresenter Create(HudData hud, WorldClock clock, MessageBoard messages)
        {
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            var go = new GameObject("HUD");
            go.SetActive(false);
            var presenter = go.AddComponent<HudPresenter>();
            presenter.Build(hud, clock, messages);
            go.SetActive(true);
            presenter.BuildTree();
            presenter.Refresh();
            return presenter;
        }

        void Build(HudData hud, WorldClock clock, MessageBoard messages)
        {
            Hud = hud;
            Clock = clock;
            Messages = messages;

            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Overworld HUD";
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(hud.ScreenWidth, hud.ScreenHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;
            settings.sortingOrder = PanelOrder;
            settings.clearColor = false;

            Document = gameObject.AddComponent<UIDocument>();
            Document.panelSettings = settings;
        }

        /// <summary>Lays out the labels once the document's panel exists (it is made when the document is enabled).</summary>
        void BuildTree()
        {
            var root = Document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            // The status box and bars come before the clock's lines, as renderStatus wrote them before the bars and messages.
            statusLayer = new VisualElement { name = "status", pickingMode = PickingMode.Ignore };
            statusLayer.style.position = Position.Absolute;
            statusLayer.style.left = 0;
            statusLayer.style.top = 0;
            root.Add(statusLayer);
            PeriodLabel = NewLabel(root, "period", Hud.Period);
            TimeLabel = NewLabel(root, "time", Hud.Time);
            messageLayer = new VisualElement { name = "messages", pickingMode = PickingMode.Ignore };
            messageLayer.style.position = Position.Absolute;
            messageLayer.style.left = 0;
            messageLayer.style.top = 0;
            root.Add(messageLayer);
            messageLabels.Clear();
            statusLabels.Clear();
            bars.Clear();
        }

        void LateUpdate() => Refresh();

        /// <summary>
        /// Panel units per pixel of the 640 x 480 screen across: the panel is the screen's height in units (it scales with
        /// the window's height), so a window wider than 4:3 spreads the original's columns as its stretched back buffer did.
        /// </summary>
        public float HorizontalScale
        {
            get
            {
                var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : (float)Hud.ScreenWidth / Hud.ScreenHeight;
                return Hud.ScreenHeight * aspect / Hud.ScreenWidth;
            }
        }

        /// <summary>Writes this frame's period, time and messages.</summary>
        public void Refresh()
        {
            if (PeriodLabel == null) return;
            Place(PeriodLabel, Hud.Period.X, Hud.Period.Y);
            Place(TimeLabel, Hud.Time.X, Hud.Time.Y);
            if (Clock != null)
            {
                PeriodLabel.text = Clock.Period.Name;
                TimeLabel.text = Clock.TimeText;
            }

            RefreshVitals();

            var shown = Messages != null ? Messages.Shown : (IReadOnlyList<ShownMessage>)Array.Empty<ShownMessage>();
            for (var i = 0; i < shown.Count; i++)
            {
                if (i == messageLabels.Count) messageLabels.Add(NewLabel(messageLayer, "message " + i, null));
                var m = shown[i];
                var label = messageLabels[i];
                label.text = m.Caption;
                Place(label, m.X, m.Y);
                Style(label, m.Font);
                label.style.color = (Color)new Color32(m.R, m.G, m.B, m.A);
                label.style.display = DisplayStyle.Flex;
            }

            for (var i = shown.Count; i < messageLabels.Count; i++) messageLabels[i].style.display = DisplayStyle.None;
        }

        void RefreshVitals()
        {
            if (Party == null || statusLayer == null) return;
            var leader = Party.LeaderOf(Player);
            var light = Clock != null ? Clock.Light : new LightColour(255, 255, 255);
            var lines = StatusBoxView.Lines(Hud.StatusBox, Party, leader, light);
            for (var i = 0; i < lines.Count; i++)
            {
                if (i == statusLabels.Count) statusLabels.Add(NewLabel(statusLayer, "status " + i, null));
                var line = lines[i];
                var label = statusLabels[i];
                label.text = line.Text;
                Place(label, line.X, line.Y);
                Style(label, line.Font);
                label.style.color = (Color)new Color32(line.Colour.R, line.Colour.G, line.Colour.B, line.Colour.A);
                label.style.display = DisplayStyle.Flex;
            }

            for (var i = lines.Count; i < statusLabels.Count; i++) statusLabels[i].style.display = DisplayStyle.None;

            var hero = Party.Heroes[leader];
            var b = Hud.Bars;
            if (b == null) return;
            PlaceBar(b.Health, b, b.FillWidth(hero.Health, hero.MaxHealth));
            PlaceBar(b.Mana, b, b.FillWidth(hero.Mana, hero.MaxMana));
            PlaceBar(b.Stamina, b, b.FillWidth(hero.Stamina, hero.MaxStamina));
        }

        void PlaceBar(BarDefinition bar, BarsDefinition size, int fillWidth)
        {
            if (!bars.TryGetValue(bar, out var e)) return;
            var scale = HorizontalScale;
            foreach (var part in new[] { e.Fill, e.Frame })
            {
                part.style.left = bar.X * scale;
                part.style.top = bar.Y;
                part.style.height = size.Height;
            }

            e.Frame.style.width = size.Width * scale;
            e.Fill.style.width = fillWidth * scale;
            e.Fill.style.display = fillWidth > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        Label NewLabel(VisualElement parent, string name, HudText text)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.marginLeft = label.style.marginTop = label.style.marginRight = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingTop = label.style.paddingRight = label.style.paddingBottom = 0;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            if (text != null)
            {
                Place(label, text.X, text.Y);
                Style(label, text.Font);
                label.style.color = (Color)new Color32(text.Colour.R, text.Colour.G, text.Colour.B, text.Colour.A);
            }

            parent.Add(label);
            return label;
        }

        /// <summary>Puts the label's top left at (x, y) of the 640 x 480 screen, as the same fraction of the view.</summary>
        void Place(VisualElement e, float x, float y)
        {
            e.style.left = x * HorizontalScale;
            e.style.top = y;
        }

        void Style(Label label, HudFont font)
        {
            if (font == null) return;
            // A GDI point is 96 / 72 pixels; the panel is 480 units tall, as the original's screen was pixels.
            label.style.fontSize = font.Points * 96f / 72f;
            label.style.unityFontStyleAndWeight = font.Bold ? FontStyle.Bold : FontStyle.Normal;
            var face = FontFor(font);
            if (face != null) label.style.unityFontDefinition = FontDefinition.FromFont(face);
        }

        /// <summary>The first of the font's faces installed on this machine, or Unity's built-in face when none is.</summary>
        Font FontFor(HudFont font)
        {
            if (fonts.TryGetValue(font, out var face)) return face;
            var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
            foreach (var family in font.Families)
            {
                if (!installed.Contains(family)) continue;
                face = Font.CreateDynamicFontFromOSFont(family, Mathf.Max(1, Mathf.RoundToInt(font.Points * 96f / 72f)));
                if (face != null) break;
            }

            if (face == null) face = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fonts[font] = face;
            return face;
        }

        /// <summary>The face a label is drawn in (for checks).</summary>
        public Font FaceOf(HudFont font) => font == null ? null : FontFor(font);

        void OnDestroy()
        {
            if (settings != null)
            {
                Release(settings.themeStyleSheet);
                Release(settings);
            }

            foreach (var f in fonts.Values)
                if (f != null && f.name != "LegacyRuntime") Release(f);
        }

        static void Release(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}

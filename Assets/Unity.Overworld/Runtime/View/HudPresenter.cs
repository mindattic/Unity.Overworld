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
    /// bars at the bottom left (U.OVW-US-A10), and the rest prompt while that leader lies down (U.OVW-US-A11).
    /// </summary>
    [DefaultExecutionOrder(100)] // after the world presenter has placed the camera, so the pickup names sit on their pickups
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

        /// <summary>
        /// The rest prompt ("Rest for n hours?"), shown while the hero <see cref="Player"/> leads lies down (and kneels), as
        /// showRestText wrote it; hidden otherwise or before <see cref="ShowVitals"/>.
        /// </summary>
        public Label RestLabel { get; private set; }

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
            // The pickup names are 3D text in the world: under everything the HUD draws.
            labelLayer = NewLayer(root, "pickup labels");
            // renderinventoryBox came before renderStatus.
            buttonLayer = NewLayer(root, "inventory button");
            // The status box and bars come before the clock's lines, as renderStatus wrote them before the bars and messages.
            statusLayer = new VisualElement { name = "status", pickingMode = PickingMode.Ignore };
            statusLayer.style.position = Position.Absolute;
            statusLayer.style.left = 0;
            statusLayer.style.top = 0;
            root.Add(statusLayer);
            PeriodLabel = NewLabel(root, "period", Hud.Period);
            TimeLabel = NewLabel(root, "time", Hud.Time);
            RestLabel = Hud.Rest != null ? NewLabel(root, "rest", Hud.Rest) : null;
            if (RestLabel != null) RestLabel.style.display = DisplayStyle.None;
            // renderInventory, then renderquickIcon and renderquickBar, before the examine box.
            packLayer = NewLayer(root, "inventory");
            quickLayer = NewLayer(root, "quick bar");
            messageLayer = new VisualElement { name = "messages", pickingMode = PickingMode.Ignore };
            messageLayer.style.position = Position.Absolute;
            messageLayer.style.left = 0;
            messageLayer.style.top = 0;
            root.Add(messageLayer);
            examineLayer = NewLayer(root, "examine");
            messageLabels.Clear();
            statusLabels.Clear();
            bars.Clear();
        }

        // ---- pickups (U.OVW-US-A12): the name labels over the world and the examine box ---------------------------

        VisualElement labelLayer;
        VisualElement examineLayer;
        readonly Label[] examineLines = new Label[5];

        /// <summary>The pickups whose labels and examine box are shown (null until <see cref="ShowPickups"/>).</summary>
        public Pickups Pickups { get; private set; }

        /// <summary>The view whose cursor finds the pickup under it, and whose examine mode opens the box.</summary>
        public Viewpoint Viewpoint { get; private set; }

        /// <summary>The world drawn: its camera places the labels and its light colours them.</summary>
        public WorldViewPresenter World { get; private set; }

        /// <summary>The selected pickup's name and marker (showItemName), and the hovered one's (checkCursorOverItem).</summary>
        public Label SelectedName { get; private set; }

        public Label SelectedMarker { get; private set; }
        public Label HoverName { get; private set; }
        public Label HoverMarker { get; private set; }

        /// <summary>The examine box (its picture), shown in examine mode with the cursor over a pickup.</summary>
        public VisualElement ExamineBox { get; private set; }

        /// <summary>The examine box's icon of the pickup.</summary>
        public VisualElement ExamineIcon { get; private set; }

        /// <summary>The examine box's name, effect, description, weight and value lines.</summary>
        public IReadOnlyList<Label> ExamineLines => examineLines;

        /// <summary>The pickup the examine box shows now, or -1.</summary>
        public int Examined { get; private set; } = -1;

        /// <summary>
        /// Shows <paramref name="pickups"/> from now on: the name of the selected pickup in reach of the hero
        /// <see cref="Player"/> leads and of the one under <paramref name="viewpoint"/>'s cursor, as 3D text in
        /// <paramref name="world"/>'s light over the world (and under the HUD), and in examine mode the examine box.
        /// </summary>
        public void ShowPickups(Pickups pickups, Viewpoint viewpoint, WorldViewPresenter world, PlayerId player, TextureLibrary library = null)
        {
            Pickups = pickups;
            Viewpoint = viewpoint;
            World = world;
            Player = player;
            textures = library ?? textures ?? new TextureLibrary();
            if (SelectedName == null && labelLayer != null)
            {
                SelectedMarker = NewWorldLabel("selected marker");
                SelectedName = NewWorldLabel("selected name");
                HoverMarker = NewWorldLabel("hover marker");
                HoverName = NewWorldLabel("hover name");
                DropMarker = NewWorldLabel("drop marker");
                DropName = NewWorldLabel("drop name");
            }

            var box = Hud.Examine;
            if (ExamineBox == null && box != null && examineLayer != null)
            {
                ExamineBox = NewPicture(examineLayer, "examine", box.Picture);
                ExamineIcon = new VisualElement { name = "examine icon", pickingMode = PickingMode.Ignore };
                ExamineIcon.style.position = Position.Absolute;
                ExamineIcon.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                examineLayer.Add(ExamineIcon);
                for (var i = 0; i < examineLines.Length; i++)
                {
                    var line = NewLabel(examineLayer, "examine line " + i, null);
                    Style(line, box.Font);
                    line.style.color = (Color)new Color32(box.Colour.R, box.Colour.G, box.Colour.B, box.Colour.A);
                    line.style.whiteSpace = WhiteSpace.Pre;
                    examineLines[i] = line;
                }

                examineLayer.style.display = DisplayStyle.None;
            }

            Refresh();
        }

        Label NewWorldLabel(string name)
        {
            var label = NewLabel(labelLayer, name, null);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            var face = FontFor(Pickups.Data.Label.Font);
            if (face != null) label.style.unityFontDefinition = FontDefinition.FromFont(face);
            label.style.display = DisplayStyle.None;
            return label;
        }

        void RefreshPickups()
        {
            if (Pickups == null || SelectedName == null) return;
            var selected = Pickups.Selected(Player);
            var hover = -1;
            if (Viewpoint != null) hover = Pickups.PickupAt(Viewpoint.Cursor.X, Viewpoint.Cursor.Y);
            ShowLabel(SelectedName, SelectedMarker, selected);
            ShowLabel(HoverName, HoverMarker, hover);
            var drop = Pickups.SelectedDrop(Player);
            if (drop < 0) ShowLabel(DropName, DropMarker, null, 0f, 0f);
            else ShowLabel(DropName, DropMarker, Pickups.Drops[drop].Name, Pickups.Drops[drop].X, Pickups.Drops[drop].Y);

            Examined = Viewpoint != null && Viewpoint.Examine ? hover : -1;
            if (examineLayer == null || Hud.Examine == null) return;
            if (Examined < 0)
            {
                examineLayer.style.display = DisplayStyle.None;
                return;
            }

            var box = Hud.Examine;
            var p = Pickups.All[Examined];
            var scale = HorizontalScale;
            ExamineBox.style.left = box.X * scale;
            ExamineBox.style.top = box.Y;
            ExamineBox.style.width = box.Width * scale;
            ExamineBox.style.height = box.Height;
            ExamineIcon.style.left = (box.X + box.Icon.X) * scale;
            ExamineIcon.style.top = box.Y + box.Icon.Y;
            ExamineIcon.style.width = box.IconSize * scale;
            ExamineIcon.style.height = box.IconSize;
            var icon = textures.Load(p.File);
            ExamineIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
            var lines = box.Lines(p.Definition);
            var points = box.Points;
            for (var i = 0; i < examineLines.Length; i++)
            {
                examineLines[i].text = lines[i];
                Place(examineLines[i], box.X + points[i].X, box.Y + points[i].Y);
            }

            examineLayer.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// renderText's two lines over pickup <paramref name="index"/>: the marker and the name, each centred on its point
        /// above the pickup at the height its scale gives the font, in the world's light; hidden for -1 or off screen.
        /// </summary>
        void ShowLabel(Label name, Label marker, int index)
        {
            if (index < 0) ShowLabel(name, marker, null, 0f, 0f);
            else ShowLabel(name, marker, Pickups.All[index].Name, Pickups.All[index].X, Pickups.All[index].Y);
        }

        /// <summary>The same two lines over the item <paramref name="text"/> at (<paramref name="x"/>, <paramref name="y"/>); hidden for null.</summary>
        void ShowLabel(Label name, Label marker, string text, float x, float y)
        {
            if (text == null || World == null || World.Camera == null)
            {
                name.style.display = DisplayStyle.None;
                marker.style.display = DisplayStyle.None;
                return;
            }

            var l = Pickups.Data.Label;
            var colour = World.Tint;
            colour.a = 1f;
            PlaceWorldLabel(marker, l.Marker, x + l.MarkerOffset.X, y + l.MarkerOffset.Y, l.EmHeight(l.MarkerScale), colour);
            PlaceWorldLabel(name, text, x + l.NameOffset.X, y + l.NameOffset.Y, l.EmHeight(l.NameScale), colour);
        }

        /// <summary>The selected drop's name and marker (showItemName's second pair).</summary>
        public Label DropName { get; private set; }

        public Label DropMarker { get; private set; }

        void PlaceWorldLabel(Label label, string text, float x, float y, float em, Color colour)
        {
            var cam = World.Camera;
            var at = cam.WorldToViewportPoint(new Vector3(x, y, 0f));
            var top = cam.WorldToViewportPoint(new Vector3(x, y + em, 0f));
            if (at.z <= 0f || float.IsNaN(at.x) || float.IsNaN(at.y))
            {
                label.style.display = DisplayStyle.None;
                return;
            }

            // The view to the panel's units: the panel is the reference screen's height tall and as wide as the window's aspect.
            var height = Hud.ScreenHeight;
            var width = Hud.ScreenWidth * HorizontalScale;
            label.text = text;
            label.style.left = at.x * width;
            label.style.top = (1f - at.y) * height;
            label.style.fontSize = Mathf.Max(1f, Mathf.Abs(top.y - at.y) * height);
            label.style.color = colour;
            label.style.display = DisplayStyle.Flex;
        }

        // ---- the inventory (U.OVW-US-A13): the panel, the quick bar, the inventory button, "overburdened", the target --

        VisualElement buttonLayer;
        VisualElement packLayer;
        VisualElement quickLayer;
        readonly List<VisualElement> packIcons = new List<VisualElement>();
        readonly List<Label> packCounts = new List<Label>();
        readonly List<VisualElement> quickIcons = new List<VisualElement>();
        readonly List<Label> quickCounts = new List<Label>();

        /// <summary>The pack shown (null until <see cref="ShowInventory"/>).</summary>
        public InventoryControl Inventory { get; private set; }

        /// <summary>The panel's picture, shown while the panel is open.</summary>
        public VisualElement PackPanel { get; private set; }

        /// <summary>The item cursor and the swap cursor on the panel's icons.</summary>
        public VisualElement ItemCursor { get; private set; }

        public VisualElement SwapCursor { get; private set; }

        /// <summary>The panel's weight, gold, name, effect, description, item weight and value lines.</summary>
        public Label WeightLabel { get; private set; }

        public Label GoldLabel { get; private set; }
        public Label ItemNameLabel { get; private set; }
        public Label EffectLabel { get; private set; }
        public Label DescriptionLabel { get; private set; }
        public Label ItemWeightLabel { get; private set; }
        public Label ValueLabel { get; private set; }

        /// <summary>The icons of the panel's stacks, by slot (hidden beyond the pack).</summary>
        public IReadOnlyList<VisualElement> PackIcons => packIcons;

        /// <summary>The counts over the panel's icons, by slot (hidden for stacks of one).</summary>
        public IReadOnlyList<Label> PackCounts => packCounts;

        /// <summary>The quick bar's picture, its icons and their counts, shown while the bar is.</summary>
        public VisualElement QuickBar { get; private set; }

        public IReadOnlyList<VisualElement> QuickIcons => quickIcons;
        public IReadOnlyList<Label> QuickCounts => quickCounts;

        /// <summary>The inventory button (open or closed picture).</summary>
        public VisualElement InventoryButton { get; private set; }

        /// <summary>The picture the inventory button shows now.</summary>
        public string InventoryButtonFile { get; private set; }

        /// <summary>"You are overburdened", shown while the leader is burdened.</summary>
        public Label BurdenedLabel { get; private set; }

        /// <summary>The marker and name over the hero an item's target is being chosen on (renderHeroSelect's 3D text).</summary>
        public Label TargetName { get; private set; }

        public Label TargetMarker { get; private set; }

        /// <summary>
        /// Shows <paramref name="inventory"/> for <paramref name="player"/> from now on: the inventory button, the panel
        /// while it is open, the quick bar while it is shown, "You are overburdened" while the leader is, and the name over
        /// an item's target while it is chosen.
        /// </summary>
        public void ShowInventory(InventoryControl inventory, PlayerId player, TextureLibrary library = null)
        {
            Inventory = inventory;
            Player = player;
            textures = library ?? textures ?? new TextureLibrary();
            if (PackPanel == null && packLayer != null && Hud.Inventory != null)
            {
                var panel = Hud.Inventory;
                PackPanel = NewPicture(packLayer, "inventory panel", panel.Picture);
                // renderItemIcon, renderItemCursor, renderSwapCursor, then the text.
                for (var i = 0; i < panel.Slots; i++) packIcons.Add(NewIcon(packLayer, "icon " + i));
                ItemCursor = NewPicture(packLayer, "item cursor", panel.ItemCursor);
                SwapCursor = NewPicture(packLayer, "swap cursor", panel.SwapCursor);
                WeightLabel = NewPanelLabel("weight", panel.Green);
                GoldLabel = NewPanelLabel("gold", panel.Green);
                ItemNameLabel = NewPanelLabel("item name", panel.Green);
                EffectLabel = NewPanelLabel("effect", panel.Cyan);
                DescriptionLabel = NewPanelLabel("description", panel.Cyan);
                ItemWeightLabel = NewPanelLabel("item weight", panel.Green);
                ValueLabel = NewPanelLabel("value", panel.Green);
                for (var i = 0; i < panel.Slots; i++) packCounts.Add(NewCount(packLayer, "count " + i, panel.Count));
                packLayer.style.display = DisplayStyle.None;

                var bar = Hud.QuickBar;
                for (var i = 0; i < bar.Slots; i++) quickIcons.Add(NewIcon(quickLayer, "quick icon " + i));
                QuickBar = NewPicture(quickLayer, "quick bar", bar.Picture);
                for (var i = 0; i < bar.Slots; i++) quickCounts.Add(NewCount(quickLayer, "quick count " + i, bar.Count));
                quickLayer.style.display = DisplayStyle.None;

                var button = Hud.InventoryButton;
                InventoryButton = NewPicture(buttonLayer, "inventory button", button.Picture(false));
                InventoryButtonFile = button.Picture(false);

                BurdenedLabel = NewLabel(statusLayer, "burdened", Hud.Burdened);
                BurdenedLabel.text = Hud.BurdenedText;
                BurdenedLabel.style.display = DisplayStyle.None;

            }

            // The target's 3D text uses the pickup labels' font: it is made once the pickups are shown.
            if (TargetName == null && Pickups != null && labelLayer != null)
            {
                TargetMarker = NewWorldLabel("target marker");
                TargetName = NewWorldLabel("target name");
            }

            Refresh();
        }

        VisualElement NewIcon(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.style.display = DisplayStyle.None;
            parent.Add(e);
            return e;
        }

        Label NewPanelLabel(string name, TextColour colour)
        {
            var label = NewLabel(packLayer, name, null);
            Style(label, Hud.Inventory.Font);
            label.style.color = (Color)new Color32(colour.R, colour.G, colour.B, colour.A);
            label.style.whiteSpace = WhiteSpace.Pre;
            return label;
        }

        Label NewCount(VisualElement parent, string name, CountText count)
        {
            var label = NewLabel(parent, name, null);
            Style(label, count.Font);
            label.style.color = (Color)new Color32(count.Colour.R, count.Colour.G, count.Colour.B, count.Colour.A);
            label.style.display = DisplayStyle.None;
            return label;
        }

        /// <summary>Places a picture on the 640 x 480 screen at (x, y), w x h, as the same fraction of the view.</summary>
        void PlaceBox(VisualElement e, float x, float y, float w, float h)
        {
            var scale = HorizontalScale;
            e.style.left = x * scale;
            e.style.top = y;
            e.style.width = w * scale;
            e.style.height = h;
        }

        void SetPicture(VisualElement e, string file)
        {
            var texture = textures.Load(file);
            e.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
        }

        readonly Dictionary<VisualElement, string> iconFiles = new Dictionary<VisualElement, string>();

        void ShowIcon(VisualElement icon, string file, float x, float y, float size)
        {
            PlaceBox(icon, x, y, size, size);
            if (!iconFiles.TryGetValue(icon, out var shown) || shown != file)
            {
                iconFiles[icon] = file;
                SetPicture(icon, file);
            }

            icon.style.display = DisplayStyle.Flex;
        }

        void ShowCount(Label label, CountText count, int amount, DataPoint icon)
        {
            var offset = count.Offset(amount);
            if (!offset.HasValue)
            {
                label.style.display = DisplayStyle.None;
                return;
            }

            label.text = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Place(label, icon.X + offset.Value.X, icon.Y + offset.Value.Y);
            label.style.display = DisplayStyle.Flex;
        }

        static void SetText(Label label, string text, float x, float y, Action<VisualElement, float, float> place)
        {
            label.text = text;
            place(label, x, y);
            label.style.display = DisplayStyle.Flex;
        }

        void RefreshInventory()
        {
            if (Inventory == null || PackPanel == null) return;
            var v = Inventory.For(Player);
            var items = Inventory.Inventory.Items;

            // renderinventoryBox: open or closed.
            var button = Hud.InventoryButton;
            var file = button.Picture(v.Open);
            if (file != InventoryButtonFile)
            {
                InventoryButtonFile = file;
                SetPicture(InventoryButton, file);
            }

            PlaceBox(InventoryButton, button.X, button.Y, button.Width, button.Height);

            // renderStatus: "You are overburdened" in the status box's colour.
            var leader = Party != null ? Party.Heroes[Party.LeaderOf(Player)] : Inventory.Party.Leader(Player);
            if (leader.Burdened)
            {
                var light = Clock != null ? Clock.Light : new LightColour(255, 255, 255);
                Place(BurdenedLabel, Hud.Burdened.X, Hud.Burdened.Y);
                BurdenedLabel.style.color = (Color)new Color32((byte)(255 - light.R), 255, (byte)(255 - light.B), 255);
                BurdenedLabel.style.display = DisplayStyle.Flex;
            }
            else BurdenedLabel.style.display = DisplayStyle.None;

            RefreshPanel(v, items);
            RefreshQuickBar(v, items);
            RefreshTarget(v);
        }

        void RefreshPanel(PackView v, IReadOnlyList<InventoryItem> items)
        {
            if (!v.Open)
            {
                packLayer.style.display = DisplayStyle.None;
                return;
            }

            var panel = Hud.Inventory;
            packLayer.style.display = DisplayStyle.Flex;
            PlaceBox(PackPanel, panel.X, panel.Y, panel.Width, panel.Height);
            for (var i = 0; i < packIcons.Count; i++)
            {
                if (i >= items.Count)
                {
                    packIcons[i].style.display = DisplayStyle.None;
                    packCounts[i].style.display = DisplayStyle.None;
                    continue;
                }

                var at = panel.IconAt(i);
                ShowIcon(packIcons[i], items[i].Texture, at.X, at.Y, panel.IconSize);
                ShowCount(packCounts[i], panel.Count, items[i].Amount, at);
            }

            var any = items.Count > 0;
            ItemCursor.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            SwapCursor.style.display = any && v.Swapping ? DisplayStyle.Flex : DisplayStyle.None;
            if (any)
            {
                var at = panel.IconAt(v.Cursor);
                PlaceBox(ItemCursor, at.X, at.Y, panel.IconSize, panel.IconSize);
                var from = panel.IconAt(v.SwapFrom);
                PlaceBox(SwapCursor, from.X, from.Y, panel.IconSize, panel.IconSize);
            }

            var c = System.Globalization.CultureInfo.InvariantCulture;
            var x = panel.X;
            var y = panel.Y;
            SetText(WeightLabel, panel.WeightLine(Inventory.Inventory.Weight, Inventory.WeightLimit), x + panel.Weight.X, y + panel.Weight.Y, Place);
            SetText(GoldLabel, string.Format(c, panel.GoldText, Inventory.Inventory.Gold), x + panel.Gold.X, y + panel.Gold.Y, Place);
            foreach (var l in new[] { ItemNameLabel, EffectLabel, DescriptionLabel, ItemWeightLabel, ValueLabel })
                l.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (!any) return;

            var item = items[v.Cursor];
            var name = item.Amount > 1 ? string.Format(c, panel.AmountText, item.Name, item.Amount) : item.Name;
            SetText(ItemNameLabel, name, x + panel.Name.X, y + panel.Name.Y, Place);
            SetText(EffectLabel, item.Effect, x + panel.Effect.X, y + panel.Effect.Y, Place);
            SetText(DescriptionLabel, item.Description, x + panel.Description.X, y + panel.Description.Y, Place);
            SetText(ItemWeightLabel, InventoryPanelDefinition.Number(item.Weight), x + panel.ItemWeight.X, y + panel.ItemWeight.Y, Place);
            var value = item.Value.ToString(c);
            SetText(ValueLabel, string.Format(c, panel.ValueText, value), x + panel.Value.X - panel.ValueCharacter * value.Length, y + panel.Value.Y, Place);
        }

        void RefreshQuickBar(PackView v, IReadOnlyList<InventoryItem> items)
        {
            if (!v.QuickBar)
            {
                quickLayer.style.display = DisplayStyle.None;
                return;
            }

            var bar = Hud.QuickBar;
            quickLayer.style.display = DisplayStyle.Flex;
            PlaceBox(QuickBar, bar.X, bar.Y, bar.Width, bar.Height);
            for (var i = 0; i < quickIcons.Count; i++)
            {
                if (i >= items.Count)
                {
                    quickIcons[i].style.display = DisplayStyle.None;
                    quickCounts[i].style.display = DisplayStyle.None;
                    continue;
                }

                var at = bar.IconAt(i);
                ShowIcon(quickIcons[i], items[i].Texture, at.X, at.Y, bar.IconSize);
                ShowCount(quickCounts[i], bar.Count, items[i].Amount, at);
            }
        }

        /// <summary>renderHeroSelect's 3D text: the marker and the chosen hero's name over it while an item waits for its target.</summary>
        void RefreshTarget(PackView v)
        {
            if (TargetName == null) return;
            var party = Inventory.Party;
            var t = Hud.Target;
            if (!v.Choosing || World == null || World.Camera == null || Pickups == null || v.Target < 0 || v.Target >= party.Heroes.Count)
            {
                TargetName.style.display = DisplayStyle.None;
                TargetMarker.style.display = DisplayStyle.None;
                return;
            }

            var hero = party.Heroes[v.Target];
            var size = party.Data.HeroSize;
            var x = hero.X + size * t.LabelPoint.X + t.LabelOffset.X;
            var y = hero.Y + size * t.LabelPoint.Y + t.LabelOffset.Y;
            var l = Pickups.Data.Label;
            var colour = World.Tint;
            colour.a = 1f;
            PlaceWorldLabel(TargetMarker, t.Marker, x, y, l.EmHeight(t.MarkerScale), colour);
            PlaceWorldLabel(TargetName, hero.Name, x, y, l.EmHeight(t.NameScale), colour);
        }

        void LateUpdate() => Refresh();

        static VisualElement NewLayer(VisualElement root, string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            root.Add(layer);
            return layer;
        }

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
            RefreshRest();
            RefreshPickups();
            RefreshInventory();
            RefreshConsole();

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

        // ---- the script console (U.OVW-US-A14): checkScripter's prompt -----------------------------------------------

        /// <summary>The interpreter whose console is shown (null until <see cref="ShowConsole"/>).</summary>
        public ScriptInterpreter Scripts { get; private set; }

        PlayerId consolePlayer;

        /// <summary>The console's prompt ("/ > line|" or "\ > line "), shown while it is open.</summary>
        public Label ConsoleLabel { get; private set; }

        /// <summary>Shows <paramref name="player"/>'s script console: its blinking prompt at the console's place, over the rest of the HUD.</summary>
        public void ShowConsole(ScriptInterpreter scripts, PlayerId player)
        {
            Scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            consolePlayer = player;
            var c = scripts.Data.Console;
            ConsoleLabel = NewLabel(Document.rootVisualElement, "console", null);
            Style(ConsoleLabel, c.Font);
            ConsoleLabel.style.display = DisplayStyle.None;
            RefreshConsole();
        }

        void RefreshConsole()
        {
            if (ConsoleLabel == null || Scripts == null) return;
            var prompt = Scripts.Prompt(consolePlayer);
            if (prompt == null)
            {
                ConsoleLabel.style.display = DisplayStyle.None;
                return;
            }

            var c = Scripts.Data.Console;
            var colour = Scripts.For(consolePlayer).Colour;
            ConsoleLabel.text = prompt;
            Place(ConsoleLabel, c.X, c.Y);
            ConsoleLabel.style.color = (Color)new Color32(colour.R, colour.G, colour.B, 255);
            ConsoleLabel.style.display = DisplayStyle.Flex;
        }

        void RefreshRest()
        {
            if (RestLabel == null) return;
            var hero = Party != null ? Party.Heroes[Party.LeaderOf(Player)] : null;
            if (hero == null || !hero.Laying || !hero.Kneeling)
            {
                RestLabel.style.display = DisplayStyle.None;
                return;
            }

            RestLabel.text = Hud.RestText(hero.SleepHours);
            Place(RestLabel, Hud.Rest.X, Hud.Rest.Y);
            RestLabel.style.display = DisplayStyle.Flex;
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

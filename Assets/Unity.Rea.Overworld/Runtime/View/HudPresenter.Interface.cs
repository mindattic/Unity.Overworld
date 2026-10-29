using System.Collections.Generic;
using MindAttic.Overworld.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The interface's screens and widgets (U.OVW-US-A20; RECONSTRUCTION 2.3, 2.4, 5.3 and 13), each in the original's
    /// draw order: the FPS counter (renderFPS, red, F4), the turn bars under the status box (renderTurnBar), the options
    /// menu (renderOptionMenu, F11 or Escape), the hero screen (renderheroScreen, Page Up: the date, the time and the
    /// party's experience), the 2D arrow over the open panel and the buttons (rendercursor2D), the pause banner, the name
    /// over a hero the world cursor is on (checkCursorOverHero's 3D text, under the HUD) and the loading picture over
    /// everything. Reads <see cref="InterfaceControl"/>, the party, the pack and the clock; never changes game state.
    /// </summary>
    public sealed partial class HudPresenter
    {
        VisualElement fpsLayer;
        VisualElement turnLayer;
        VisualElement optionsLayer;
        VisualElement heroLayer;
        VisualElement cursorLayer;
        VisualElement pauseLayer;
        VisualElement loadingLayer;
        readonly List<(VisualElement Fill, VisualElement Frame)> turnBars = new List<(VisualElement, VisualElement)>();
        readonly List<string> turnBarFiles = new List<string>();
        readonly List<Label> optionRows = new List<Label>();
        FrameRateCounter frameRate;
        PlayerId interfacePlayer;

        /// <summary>The interface shown (null until <see cref="ShowInterface"/>).</summary>
        public InterfaceControl Interface { get; private set; }

        /// <summary>The team button, whose hit box shows the 2D arrow.</summary>
        public TeamButtonDefinition TeamButtonArea { get; private set; }

        /// <summary>The frame-rate counter's line ("n fps"), shown while it is on.</summary>
        public Label FpsLabel { get; private set; }

        /// <summary>The frames a second the counter shows.</summary>
        public FrameRateCounter FrameRate => frameRate;

        /// <summary>The pause banner, shown while the game is paused.</summary>
        public Label PauseLabel { get; private set; }

        /// <summary>The options menu's picture, title, cursor and rows (the rows hidden where a page has no text).</summary>
        public VisualElement OptionsPicture { get; private set; }

        public Label OptionsTitle { get; private set; }
        public Label OptionsCursor { get; private set; }
        public IReadOnlyList<Label> OptionRows => optionRows;

        /// <summary>The hero screen's picture and its date, time and experience lines.</summary>
        public VisualElement HeroScreenPicture { get; private set; }

        public Label HeroDateLabel { get; private set; }
        public Label HeroTimeLabel { get; private set; }
        public Label HeroExperienceLabel { get; private set; }

        /// <summary>The turn bars by slot: each fill and the bar over it (hidden beyond the heroes shown).</summary>
        public IReadOnlyList<(VisualElement Fill, VisualElement Frame)> TurnBars => turnBars;

        /// <summary>The picture each turn bar's frame shows now (stamina_bar or ready_bar), by slot.</summary>
        public IReadOnlyList<string> TurnBarFiles => turnBarFiles;

        /// <summary>The 2D arrow, shown over the open panel and the buttons.</summary>
        public VisualElement Cursor2D { get; private set; }

        /// <summary>bShowCursor2D: the 2D arrow is shown (and the world cursor is not).</summary>
        public bool Cursor2DShown { get; private set; }

        /// <summary>The name and marker over a hero (not the leader) the world cursor is on.</summary>
        public Label HeroHoverName { get; private set; }

        public Label HeroHoverMarker { get; private set; }

        /// <summary>The loading picture, shown while play has not begun.</summary>
        public VisualElement LoadingPicture { get; private set; }

        /// <summary>The loading picture shown now, or null.</summary>
        public string LoadingFile { get; private set; }

        void ClearInterface()
        {
            turnBars.Clear();
            turnBarFiles.Clear();
            optionRows.Clear();
            FpsLabel = PauseLabel = OptionsTitle = OptionsCursor = HeroDateLabel = HeroTimeLabel = HeroExperienceLabel = null;
            OptionsPicture = HeroScreenPicture = Cursor2D = LoadingPicture = null;
            HeroHoverName = HeroHoverMarker = null;
            LoadingFile = null;
        }

        /// <summary>
        /// Shows <paramref name="ui"/> for <paramref name="player"/> from now on. Call after <see cref="ShowVitals"/>,
        /// <see cref="ShowPickups"/> and <see cref="ShowInventory"/> (the turn bars read the party, the hover name the
        /// pickups' 3D-text font and the world, the 2D arrow the pack and the cursor's virtual mouse).
        /// </summary>
        public void ShowInterface(InterfaceControl ui, PlayerId player, TeamButtonDefinition teamButton, TextureLibrary library = null)
        {
            Interface = ui ?? throw new System.ArgumentNullException(nameof(ui));
            interfacePlayer = player;
            TeamButtonArea = teamButton;
            textures = library ?? textures ?? new TextureLibrary();
            if (FpsLabel == null && fpsLayer != null)
            {
                frameRate = new FrameRateCounter(Hud.Fps.SampleMilliseconds);
                FpsLabel = NewLabel(fpsLayer, "fps", Hud.Fps.Text);

                for (var i = 0; i < Hud.TurnBars.Slots.Count; i++)
                {
                    var fill = NewPicture(turnLayer, "turn fill " + i, Hud.TurnBars.Fill);
                    var frame = NewPicture(turnLayer, "turn bar " + i, Hud.TurnBars.Frame);
                    turnBars.Add((fill, frame));
                    turnBarFiles.Add(Hud.TurnBars.Frame);
                }

                var o = Hud.Options;
                OptionsPicture = NewPicture(optionsLayer, "options", o.Box.Picture);
                OptionsTitle = NewLabel(optionsLayer, "options title", o.Title);
                OptionsTitle.text = o.TitleText;
                OptionsCursor = NewOptionsLabel("options cursor");
                OptionsCursor.text = o.CursorText;
                for (var i = 0; i < o.Rows; i++) optionRows.Add(NewOptionsLabel("options row " + i));
                optionsLayer.style.display = DisplayStyle.None;

                var h = Hud.HeroScreen;
                HeroScreenPicture = NewPicture(heroLayer, "hero screen", h.Box.Picture);
                HeroDateLabel = NewLabel(heroLayer, "hero date", Hud.HeroScreenDate);
                HeroTimeLabel = NewLabel(heroLayer, "hero time", Hud.HeroScreenTime);
                HeroExperienceLabel = NewLabel(heroLayer, "hero experience", h.Experience);
                heroLayer.style.display = DisplayStyle.None;

                Cursor2D = NewPicture(cursorLayer, "cursor 2D", Hud.Cursor2D.Picture);
                Cursor2D.style.display = DisplayStyle.None;

                PauseLabel = NewLabel(pauseLayer, "pause", Hud.Pause);
                PauseLabel.text = Hud.PauseText;
                PauseLabel.style.display = DisplayStyle.None;
            }

            if (HeroHoverName == null && Pickups != null && labelLayer != null)
            {
                HeroHoverMarker = NewWorldLabel("hover marker");
                HeroHoverName = NewWorldLabel("hover name");
            }

            Refresh();
        }

        Label NewOptionsLabel(string name)
        {
            var label = NewLabel(optionsLayer, name, null);
            Style(label, Hud.Options.Font);
            var c = Hud.Options.Colour;
            label.style.color = (Color)new Color32(c.R, c.G, c.B, c.A);
            return label;
        }

        /// <summary>Shows <paramref name="file"/> over the whole window (frmMain's background while the engine loaded).</summary>
        public void ShowLoading(string file)
        {
            if (loadingLayer == null) return;
            textures = textures ?? new TextureLibrary();
            if (LoadingPicture == null) LoadingPicture = NewPicture(loadingLayer, "loading", file);
            else SetPicture(LoadingPicture, file);
            LoadingFile = file;
            loadingLayer.BringToFront();
            Refresh();
        }

        /// <summary>Takes the loading picture away (play begins).</summary>
        public void HideLoading()
        {
            LoadingFile = null;
            Refresh();
        }

        /// <summary>renderFPS counts the frames while the counter is on.</summary>
        void Update()
        {
            if (Interface == null || frameRate == null || !Interface.For(interfacePlayer).FpsShown) return;
            frameRate.Frame((long)(Time.realtimeSinceStartupAsDouble * 1000.0));
        }

        void RefreshInterface()
        {
            if (LoadingPicture != null)
            {
                if (LoadingFile != null) PlaceBox(LoadingPicture, 0f, 0f, Hud.ScreenWidth, Hud.ScreenHeight);
                LoadingPicture.style.display = LoadingFile != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (Interface == null || FpsLabel == null) return;
            var v = Interface.For(interfacePlayer);

            // renderFPS: the rate, red, at the top right; nothing while it is off (BIBLE).
            if (v.FpsShown)
            {
                FpsLabel.text = Hud.Fps.Line(frameRate.Rate);
                Place(FpsLabel, Hud.Fps.Text.X, Hud.Fps.Text.Y);
                FpsLabel.style.display = DisplayStyle.Flex;
            }
            else FpsLabel.style.display = DisplayStyle.None;

            RefreshTurnBars();
            RefreshOptions(v);
            RefreshHeroScreen(v);
            RefreshHoverName();
            RefreshCursor2D();
            PauseLabel.style.display = Interface.Paused ? DisplayStyle.Flex : DisplayStyle.None;
            if (Interface.Paused) Place(PauseLabel, Hud.Pause.X, Hud.Pause.Y);
        }

        void RefreshTurnBars()
        {
            var bars = Party != null ? TurnBarsView.Bars(Hud.TurnBars, Party, Party.LeaderOf(interfacePlayer)) : new List<TurnBar>();
            var def = Hud.TurnBars;
            for (var i = 0; i < turnBars.Count; i++)
            {
                var (fill, frame) = turnBars[i];
                if (i >= bars.Count)
                {
                    fill.style.display = DisplayStyle.None;
                    frame.style.display = DisplayStyle.None;
                    continue;
                }

                var b = bars[i];
                PlaceBox(fill, b.X, b.Y, b.FillWidth, def.Height);
                fill.style.display = b.FillWidth > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (turnBarFiles[i] != b.Frame)
                {
                    turnBarFiles[i] = b.Frame;
                    SetPicture(frame, b.Frame);
                }

                PlaceBox(frame, b.X, b.Y, def.Width, def.Height);
                frame.style.display = DisplayStyle.Flex;
            }
        }

        void RefreshOptions(InterfaceView v)
        {
            if (!v.OptionsOpen)
            {
                optionsLayer.style.display = DisplayStyle.None;
                return;
            }

            var o = Hud.Options;
            optionsLayer.style.display = DisplayStyle.Flex;
            PlaceBox(OptionsPicture, o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height);
            Place(OptionsTitle, o.Title.X, o.Title.Y);
            Place(OptionsCursor, o.CursorX, Interface.CursorY(interfacePlayer));
            var rows = Interface.PageOf(interfacePlayer).Rows;
            for (var i = 0; i < optionRows.Count; i++)
            {
                var label = optionRows[i];
                if (i >= rows.Count || rows[i].Text.Length == 0)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                label.text = rows[i].Text;
                Place(label, o.RowX, rows[i].Y);
                label.style.display = DisplayStyle.Flex;
            }
        }

        void RefreshHeroScreen(InterfaceView v)
        {
            if (!v.HeroScreenOpen)
            {
                heroLayer.style.display = DisplayStyle.None;
                return;
            }

            var h = Hud.HeroScreen;
            heroLayer.style.display = DisplayStyle.Flex;
            PlaceBox(HeroScreenPicture, h.Box.X, h.Box.Y, h.Box.Width, h.Box.Height);
            HeroDateLabel.text = Clock != null ? Clock.DateText : string.Empty;
            HeroTimeLabel.text = Clock != null ? Clock.TimeAndPeriodText : string.Empty;
            HeroExperienceLabel.text = h.ExperienceLine(Party != null ? Party.Experience : 0);
            Place(HeroDateLabel, Hud.HeroScreenDate.X, Hud.HeroScreenDate.Y);
            Place(HeroTimeLabel, Hud.HeroScreenTime.X, Hud.HeroScreenTime.Y);
            Place(HeroExperienceLabel, h.Experience.X, h.Experience.Y);
        }

        /// <summary>checkCursorOverHero: the marker and name over the first hero the world cursor is on, unless it leads.</summary>
        void RefreshHoverName()
        {
            if (HeroHoverName == null) return;
            var at = Party != null && Viewpoint != null ? Party.HeroAt(Viewpoint.Cursor.X, Viewpoint.Cursor.Y) : -1;
            if (at < 0 || at == Party.LeaderOf(interfacePlayer) || World == null || World.Camera == null)
            {
                HeroHoverName.style.display = DisplayStyle.None;
                HeroHoverMarker.style.display = DisplayStyle.None;
                return;
            }

            var hero = Party.Heroes[at];
            var d = Hud.HoverName;
            var size = Party.Data.HeroSize;
            var offset = hero.Kneeling ? d.KneelingOffset : d.LabelOffset;
            var x = hero.X + size * d.LabelPoint.X + offset.X;
            var y = hero.Y + size * d.LabelPoint.Y + offset.Y;
            var l = Pickups.Data.Label;
            var colour = World.Tint;
            colour.a = 1f;
            PlaceWorldLabel(HeroHoverMarker, d.Marker, x, y, l.EmHeight(d.MarkerScale), colour);
            PlaceWorldLabel(HeroHoverName, hero.Name, x, y, l.EmHeight(d.NameScale), colour);
        }

        /// <summary>rendercursor2D: the arrow at the virtual mouse + (-9, +4) while it is over the open panel or a button.</summary>
        void RefreshCursor2D()
        {
            var cursor = Viewpoint != null ? Viewpoint.Cursor : null;
            var packOpen = Inventory != null && Inventory.For(Player).Open;
            Cursor2DShown = cursor != null && Cursor2DDefinition.Shown(Hud, TeamButtonArea, packOpen, cursor.MouseX, cursor.MouseY);
            if (!Cursor2DShown)
            {
                Cursor2D.style.display = DisplayStyle.None;
                return;
            }

            var c = Hud.Cursor2D;
            PlaceBox(Cursor2D, cursor.MouseX + c.Offset.X, cursor.MouseY + c.Offset.Y, c.Size, c.Size);
            Cursor2D.style.display = DisplayStyle.Flex;
        }
    }
}

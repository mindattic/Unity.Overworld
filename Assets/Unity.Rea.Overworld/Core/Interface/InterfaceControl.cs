using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A key the options menu answers (actionUp, actionDown, actionLeft, actionPickup with bOptions on).</summary>
    public enum OptionsKey
    {
        Up,
        Down,
        Left,
        Enter,
    }

    /// <summary>F11 (and Escape, BIBLE): the player's options menu opens or shuts.</summary>
    public sealed class ToggleOptions : ICommand
    {
        public ToggleOptions(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>A key pressed with the player's options menu open.</summary>
    public sealed class PressOptionsKey : ICommand
    {
        public PressOptionsKey(PlayerId player, OptionsKey key) { Player = player; Key = key; }
        public PlayerId Player { get; }
        public OptionsKey Key { get; }
    }

    /// <summary>Page Up: the player's hero screen opens or shuts.</summary>
    public sealed class ToggleHeroScreen : ICommand
    {
        public ToggleHeroScreen(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>F4: the player's frame-rate counter shows or hides.</summary>
    public sealed class ToggleFps : ICommand
    {
        public ToggleFps(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>The Pause key: the game pauses or goes on (BIBLE 'Pause').</summary>
    public sealed class TogglePause : ICommand
    {
        public TogglePause(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    public sealed class OptionsChanged : IGameEvent
    {
        public OptionsChanged(long tick, PlayerId player, bool open, int page, int cursor) { Tick = tick; Player = player; Open = open; Page = page; Cursor = cursor; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Open { get; }
        public int Page { get; }
        public int Cursor { get; }
    }

    public sealed class HeroScreenToggled : IGameEvent
    {
        public HeroScreenToggled(long tick, PlayerId player, bool open) { Tick = tick; Player = player; Open = open; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Open { get; }
    }

    public sealed class FpsToggled : IGameEvent
    {
        public FpsToggled(long tick, PlayerId player, bool shown) { Tick = tick; Player = player; Shown = shown; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Shown { get; }
    }

    public sealed class PauseToggled : IGameEvent
    {
        public PauseToggled(long tick, PlayerId by, bool paused) { Tick = tick; By = by; Paused = paused; }
        public long Tick { get; }
        public PlayerId By { get; }
        public bool Paused { get; }
    }

    /// <summary>The options menu's Exit was chosen: the game ends (bRunning = False).</summary>
    public sealed class QuitRequested : IGameEvent
    {
        public QuitRequested(long tick, PlayerId by) { Tick = tick; By = by; }
        public long Tick { get; }
        public PlayerId By { get; }
    }

    /// <summary>One player's screens: the options menu (bOptions, optionArea, optionNum), the hero screen and the FPS counter.</summary>
    public sealed class InterfaceView
    {
        /// <summary>bOptions.</summary>
        public bool OptionsOpen { get; internal set; }

        /// <summary>optionArea: the page shown (0 the main page).</summary>
        public int OptionsPage { get; internal set; }

        /// <summary>optionNum: the row the '>' is on (0 to rows - 1).</summary>
        public int OptionsCursor { get; internal set; }

        /// <summary>bHeroScreen.</summary>
        public bool HeroScreenOpen { get; internal set; }

        /// <summary>bDrawFPS: on at start.</summary>
        public bool FpsShown { get; internal set; } = true;
    }

    /// <summary>
    /// The interface's state (U.OVW-US-A20; RECONSTRUCTION 2.4, 13): each player's options menu (F11 or Escape; its keys
    /// <see cref="PressOptionsKey"/>: Up and Down step the cursor through the rows, wrapping, Left goes back to the main
    /// page, Enter opens the row's page or, on Exit, raises <see cref="QuitRequested"/>), hero screen (Page Up) and FPS
    /// counter (F4), and the game's pause (the Pause key): while paused, <see cref="Game.Advance"/> runs no ticks, as
    /// oneFrameRender skipped getInput and every update while bPause was on. While a player's options menu is open, its
    /// arrows only mark their direction, the move button, the rest key and the pick-up button do nothing
    /// (<see cref="Party.OptionsOpen"/>), as actionLeft ... actionPickup tested bOptions. CHANGE OPTIONS opens and shuts
    /// the menu through <see cref="SetOptions"/>. Presentation reads <see cref="For"/> and <see cref="Paused"/>.
    /// </summary>
    public sealed class InterfaceControl
    {
        readonly Dictionary<PlayerId, InterfaceView> views = new Dictionary<PlayerId, InterfaceView>();

        public InterfaceControl(HudData hud, Party party)
        {
            Hud = hud ?? throw new ArgumentNullException(nameof(hud));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            if (hud.Options == null) throw new ArgumentException("The HUD data has no options menu.", nameof(hud));
        }

        public HudData Hud { get; }
        public Party Party { get; }
        public OptionsMenuDefinition Options => Hud.Options;

        /// <summary>bPause: the whole game is paused.</summary>
        public bool Paused { get; private set; }

        public InterfaceControl Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<ToggleOptions>(HandleToggleOptions);
            game.Register<PressOptionsKey>(HandleOptionsKey);
            game.Register<ToggleHeroScreen>(HandleHeroScreen);
            game.Register<ToggleFps>(HandleFps);
            game.Register<TogglePause>(HandlePause);
            Party.OptionsOpen = player => For(player).OptionsOpen;
            game.Held = () => Paused;
            return this;
        }

        public InterfaceView For(PlayerId player)
        {
            if (!views.TryGetValue(player, out var v)) views[player] = v = new InterfaceView();
            return v;
        }

        /// <summary>The page the player's options menu shows.</summary>
        public OptionsPage PageOf(PlayerId player) => Options.Pages[For(player).OptionsPage];

        /// <summary>The y of the row the player's '>' is on.</summary>
        public float CursorY(PlayerId player)
        {
            var v = For(player);
            var rows = Options.Pages[v.OptionsPage].Rows;
            return rows[Math.Min(v.OptionsCursor, rows.Count - 1)].Y;
        }

        OptionsChanged Changed(PlayerId player, long tick)
        {
            var v = For(player);
            return new OptionsChanged(tick, player, v.OptionsOpen, v.OptionsPage, v.OptionsCursor);
        }

        CommandResult HandleToggleOptions(Game game, ToggleOptions c)
        {
            var v = For(c.Player);
            v.OptionsOpen = !v.OptionsOpen;
            return CommandResult.Accept(Changed(c.Player, game.Tick));
        }

        /// <summary>CHANGE OPTIONS OPEN / CLOSE: <paramref name="player"/>'s menu opens or shuts (the page and cursor kept).</summary>
        public void SetOptions(PlayerId player, bool open, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.OptionsOpen == open) return;
            v.OptionsOpen = open;
            events?.Add(Changed(player, tick));
        }

        CommandResult HandleOptionsKey(Game game, PressOptionsKey c)
        {
            var v = For(c.Player);
            if (!v.OptionsOpen) return CommandResult.Reject("The options menu is shut.");
            var before = (v.OptionsPage, v.OptionsCursor);
            var events = new List<IGameEvent>();
            var last = Options.Rows - 1;
            switch (c.Key)
            {
                case OptionsKey.Up:
                    v.OptionsCursor = v.OptionsCursor > 0 ? v.OptionsCursor - 1 : last;
                    break;
                case OptionsKey.Down:
                    v.OptionsCursor = v.OptionsCursor < last ? v.OptionsCursor + 1 : 0;
                    break;
                case OptionsKey.Left:
                    v.OptionsPage = 0;
                    break;
                default:
                    // actionPickup: on the main page the row opens its page, or Exit ends the game; elsewhere nothing.
                    if (v.OptionsPage != 0) break;
                    var row = Options.Pages[0].Rows[v.OptionsCursor];
                    if (row.Opens != null) v.OptionsPage = Options.PageIndex(row.Opens);
                    if (row.Quits) events.Add(new QuitRequested(game.Tick, c.Player));
                    break;
            }

            if ((v.OptionsPage, v.OptionsCursor) != before) events.Insert(0, Changed(c.Player, game.Tick));
            return CommandResult.Accept(events.ToArray());
        }

        CommandResult HandleHeroScreen(Game game, ToggleHeroScreen c)
        {
            var v = For(c.Player);
            v.HeroScreenOpen = !v.HeroScreenOpen;
            return CommandResult.Accept(new HeroScreenToggled(game.Tick, c.Player, v.HeroScreenOpen));
        }

        CommandResult HandleFps(Game game, ToggleFps c)
        {
            var v = For(c.Player);
            v.FpsShown = !v.FpsShown;
            return CommandResult.Accept(new FpsToggled(game.Tick, c.Player, v.FpsShown));
        }

        CommandResult HandlePause(Game game, TogglePause c)
        {
            Paused = !Paused;
            return CommandResult.Accept(new PauseToggled(game.Tick, c.Player, Paused));
        }

        /// <summary>
        /// Root.Reset's part of the interface (for the Reset story, A24): every menu shut, the FPS counter on, the pause
        /// off, and the tutorial queued again on <paramref name="messages"/>.
        /// </summary>
        public void Reset(MessageBoard messages)
        {
            foreach (var v in views.Values)
            {
                v.OptionsOpen = false;
                v.FpsShown = true;
            }

            Paused = false;
            messages?.SayTutorial();
        }
    }

    /// <summary>
    /// Gui/Fps.vb renderFPS's counter: each frame counted, and once the sample time has passed since the last sample the
    /// frames counted become the rate (count x 1000 / sample) and the count starts again.
    /// </summary>
    public sealed class FrameRateCounter
    {
        long last;
        int count;

        public FrameRateCounter(int sampleMilliseconds)
        {
            if (sampleMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(sampleMilliseconds));
            SampleMilliseconds = sampleMilliseconds;
        }

        public int SampleMilliseconds { get; }

        /// <summary>iFrameRate: the frames a second at the last sample.</summary>
        public int Rate { get; private set; }

        /// <summary>One frame drawn at <paramref name="nowMilliseconds"/> (TickCount).</summary>
        public void Frame(long nowMilliseconds)
        {
            if (nowMilliseconds - last >= SampleMilliseconds)
            {
                last = nowMilliseconds;
                Rate = (int)Math.Round(count * (1000.0 / SampleMilliseconds), MidpointRounding.ToEven);
                count = 0;
            }

            count++;
        }
    }

    /// <summary>One turn bar to draw: its slot's corner, the fill's width and the bar's picture.</summary>
    public readonly struct TurnBar
    {
        public TurnBar(int hero, float x, float y, int fillWidth, string frame)
        {
            Hero = hero;
            X = x;
            Y = y;
            FillWidth = fillWidth;
            Frame = frame;
        }

        public int Hero { get; }
        public float X { get; }
        public float Y { get; }
        public int FillWidth { get; }
        public string Frame { get; }
    }

    /// <summary>
    /// renderTurnBar for one player: the hero it leads in the first slot and, in team mode, every other hero in party
    /// order in the next ones; each bar's fill its stamina's share, under ready_bar when full and stamina_bar otherwise.
    /// </summary>
    public static class TurnBarsView
    {
        public static IReadOnlyList<TurnBar> Bars(TurnBarsDefinition def, Party party, int leader)
        {
            var bars = new List<TurnBar>();
            if (def == null || party == null || leader < 0 || leader >= party.Heroes.Count) return bars;
            Add(bars, def, party.Heroes[leader], 0);
            if (!party.Team) return bars;
            var slot = 1;
            for (var h = 0; h < party.Heroes.Count && slot < def.Slots.Count; h++)
            {
                if (h == leader) continue;
                Add(bars, def, party.Heroes[h], slot++);
            }

            return bars;
        }

        static void Add(List<TurnBar> bars, TurnBarsDefinition def, Hero hero, int slot)
        {
            var at = def.Slots[slot];
            bars.Add(new TurnBar(hero.Index, at.X, at.Y, def.FillWidth(hero.Stamina, hero.MaxStamina),
                hero.Stamina == hero.MaxStamina ? def.Ready : def.Frame));
        }
    }
}

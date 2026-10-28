using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The one object in the near-empty main scene: it loads the content from StreamingAssets/Content, creates the game,
    /// its party (player 1 joined, leading Chrono), its camera control and its host, builds the world view through player
    /// 1's camera with the world cursor, following the party, and reads the player's controls with
    /// <see cref="HeroInput"/>, all in code.
    /// Scripted runs (the check that the hero moves and the view answers, without a hand on the keyboard or mouse):
    /// <c>-overworld-hold &lt;left,right,up,down,run&gt;</c> holds those keys; <c>-overworld-move-to &lt;x,y[,run]&gt;</c>
    /// holds the move button with the world cursor at (x, y); <c>-overworld-pointer &lt;x,y&gt;</c> puts the virtual mouse at
    /// (x, y) of the 640 x 480 screen (32 or 608 / 448 is an edge, which scrolls); <c>-overworld-wheel &lt;steps&gt;</c> turns
    /// the wheel (positive in, negative out); <c>-overworld-view-hold &lt;zoom,shift,x,z,num4,num6,num8,num2&gt;</c> holds
    /// those view keys; <c>-overworld-examine</c> and <c>-overworld-free-camera</c> switch those on;
    /// <c>-overworld-recall &lt;hero[,seconds]&gt;</c> recalls that hero (1 Belmont, 2 Flagg) beside the leader, at once or after
    /// that many seconds of play, so it falls in from z -1; <c>-overworld-solo</c> turns the team off (the others kneel);
    /// <c>-overworld-select &lt;hero|next|previous[,seconds]&gt;</c> makes that hero (0 Chrono, 1 Belmont, 2 Flagg) lead, as a
    /// left click on it does, or steps to the next or previous one as ] and [ do, at once or after that many seconds;
    /// <c>-overworld-clock &lt;hour:minute[,seconds]&gt;</c> sets the clock to that time of day (the period and light settled at
    /// once, as CHANGE TIME HOUR did) and, given seconds, makes an in-game minute take that many real seconds (0: a minute
    /// every tick, so a day passes in 24 seconds), so captures can show each period, its light and its announcement;
    /// <c>-overworld-vitals &lt;hero:key=value,...[;hero:...]&gt;</c> sets heroes' vitals as CHANGE HERO did (hp, mp and ap
    /// numbers, and poisoned or fine), e.g. <c>0:ap=5;1:hp=1;2:poisoned</c>, so captures can show each status animation;
    /// <c>-overworld-rest &lt;kneel|lay|hours[,seconds]&gt;</c> presses the rest key once (kneel) or twice (lay, showing the
    /// prompt), or lays the leader down and sleeps that many hours (1 to 24), at once or after that many seconds;
    /// <c>-overworld-sleep-until &lt;hour[,seconds]&gt;</c> lays the leader down and sleeps until that hour (0 to 23) as the
    /// Moon Berry (22) and Morningstar (5) do;
    /// <c>-overworld-seed &lt;n&gt;</c> seeds the scatter of the random gold and satchels (the original's was the clock's);
    /// <c>-overworld-place &lt;x,y&gt;</c> puts the leader at (x, y); <c>-overworld-reach &lt;pickup[,seconds]&gt;</c> puts the
    /// leader so that pickup (its index, 0 to 372) is in reach and selects it, showing its name;
    /// <c>-overworld-pickup &lt;pickup[,seconds]&gt;</c> does the same and then holds the pick-up button (the leader kneels
    /// and takes it); <c>-overworld-examine-pickup &lt;pickup[,seconds]&gt;</c> turns examine mode on with the world cursor
    /// over that pickup, opening the examine box;
    /// the inventory's <c>-overworld-give</c>, <c>-overworld-quickbar</c>, <c>-overworld-inventory</c>, <c>-overworld-swap</c>,
    /// <c>-overworld-drop</c>, <c>-overworld-use</c>, <c>-overworld-quick</c> and <c>-overworld-pickup-drop</c> (see
    /// <see cref="ApplyScriptedInventory"/>); the console's <c>-overworld-script</c>, <c>-overworld-console</c> and
    /// <c>-overworld-script-after</c> (see <see cref="ApplyScriptedConsole"/>); the quests' <c>-overworld-objective</c> and
    /// <c>-overworld-quest-screen</c> (see <see cref="ApplyScriptedQuests"/>); the books' <c>-overworld-book</c> (see
    /// <see cref="ApplyScriptedBook"/>); the weather's <c>-overworld-weather</c> (see <see cref="ApplyScriptedWeather"/>);
    /// battle mode's <c>-overworld-battle</c> (see <see cref="ApplyScriptedBattle"/>) and <c>-overworld-ring</c> (see
    /// <see cref="ApplyScriptedRing"/>); the interface's <c>-overworld-options</c>, <c>-overworld-hero-screen</c>,
    /// <c>-overworld-pause</c>, <c>-overworld-fps-off</c>, <c>-overworld-hover</c> and <c>-overworld-loading</c> (see
    /// <see cref="ApplyScriptedInterface"/>);
    /// <c>-overworld-capture &lt;file.png&gt;</c> saves a screenshot and quits, after a few frames or after
    /// <c>-overworld-capture-after &lt;seconds&gt;</c> of play. Any scripted argument (or a capture) leaves the real
    /// keyboard and mouse alone, so the system pointer is never taken.
    /// </summary>
    public sealed partial class OverworldBootstrap : MonoBehaviour
    {
        public const string CaptureArgument = "-overworld-capture";
        public const string CaptureAfterArgument = "-overworld-capture-after";
        public const string HoldArgument = "-overworld-hold";
        public const string MoveToArgument = "-overworld-move-to";
        public const string PointerArgument = "-overworld-pointer";
        public const string WheelArgument = "-overworld-wheel";
        public const string ViewHoldArgument = "-overworld-view-hold";
        public const string ExamineArgument = "-overworld-examine";
        public const string FreeCameraArgument = "-overworld-free-camera";
        public const string RecallArgument = "-overworld-recall";
        public const string SoloArgument = "-overworld-solo";
        public const string SelectArgument = "-overworld-select";
        public const string ClockArgument = "-overworld-clock";
        public const string VitalsArgument = "-overworld-vitals";
        public const string RestArgument = "-overworld-rest";
        public const string SleepUntilArgument = "-overworld-sleep-until";
        public const string PlaceArgument = "-overworld-place";
        public const string ReachArgument = "-overworld-reach";
        public const string PickUpArgument = "-overworld-pickup";
        public const string ExaminePickupArgument = "-overworld-examine-pickup";
        public const string SeedArgument = "-overworld-seed";

        public ContentRegistry Content { get; private set; }
        public GameHost Host { get; private set; }
        public Party Party { get; private set; }
        public CameraControl Control { get; private set; }
        public Viewpoint Viewpoint { get; private set; }
        public WorldViewPresenter World { get; private set; }
        public HeroInput Input { get; private set; }
        public TeamButtonPresenter TeamButton { get; private set; }
        public WorldClock Clock { get; private set; }
        public MessageBoard Messages { get; private set; }
        public HudPresenter Hud { get; private set; }
        public SleepCycle Sleep { get; private set; }
        public Pickups Pickups { get; private set; }
        public InventoryControl Inventory { get; private set; }
        public ScriptInterpreter Scripts { get; private set; }
        public QuestLog Quests { get; private set; }
        public BookReader Books { get; private set; }
        public Weather Weather { get; private set; }
        public Clouds Clouds { get; private set; }
        public InterfaceControl Interface { get; private set; }

        public const string OptionsArgument = "-overworld-options";
        public const string HeroScreenArgument = "-overworld-hero-screen";
        public const string PauseArgument = "-overworld-pause";
        public const string FpsOffArgument = "-overworld-fps-off";
        public const string HoverArgument = "-overworld-hover";
        public const string LoadingArgument = "-overworld-loading";

        public const string BookArgument = "-overworld-book";
        public const string WeatherArgument = "-overworld-weather";
        public const string BattleArgument = "-overworld-battle";
        public const string RingArgument = "-overworld-ring";

        public const string QuestScreenArgument = "-overworld-quest-screen";
        public const string ObjectiveArgument = "-overworld-objective";

        public const string ScriptArgument = "-overworld-script";
        public const string ConsoleArgument = "-overworld-console";
        public const string ScriptAfterArgument = "-overworld-script-after";

        public const string GiveArgument = "-overworld-give";
        public const string InventoryArgument = "-overworld-inventory";
        public const string SwapArgument = "-overworld-swap";
        public const string DropArgument = "-overworld-drop";
        public const string UseArgument = "-overworld-use";
        public const string QuickArgument = "-overworld-quick";
        public const string QuickBarArgument = "-overworld-quickbar";
        public const string PickUpDropArgument = "-overworld-pickup-drop";

        /// <summary>The content folder of this build (or of the project, in the editor).</summary>
        public static string ContentDirectory => Path.Combine(Application.streamingAssetsPath, "Content");

        void Start()
        {
            Content = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory));
            var args = Environment.GetCommandLineArgs();
            var game = new Game();
            Party = new Party(Content.Heroes, Content.Walls);
            // checkItemsInRange and checkItemPickup ran after getInput and before updateHero: the pickups step before the
            // party. The original seeded its scatter from the clock; -overworld-seed fixes it for captures.
            var seedText = ArgumentAfter(args, SeedArgument);
            var seed = 0;
            var seeded = seedText != null && int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
            if (seedText != null && !seeded) Debug.LogError("Unity.Overworld: " + SeedArgument + " wants a whole number, not '" + seedText + "'.");
            // updateCloud and updateRain ran first after getInput: the clouds, then the weather, step before everything else.
            // Their scatter was the clock's random too; -overworld-seed fixes it with the pickups'.
            Clouds = new Clouds(Content.Scenery, seeded ? new System.Random(seed) : null).Install(game);
            Weather = new Weather(Content.Scenery, seeded ? new System.Random(seed + 1) : null).Install(game);
            Pickups = new Pickups(Content.Pickups, Party, null, seeded ? new System.Random(seed) : null).Install(game);
            // getInput's inventory keys acted before the heroes: the pack steps after the pickups and before the party.
            Control = new CameraControl(Content.View, Party);
            Inventory = new InventoryControl(Pickups, Content.Hud.Inventory, Content.Hud.QuickBar, Control).Install(game);
            // Every script runs through one interpreter: the console, the items' effects, the gold, the Rune, the quests'
            // end scripts. It steps before the party, so a running MOVE steers its hero in the tick the party moves it.
            Scripts = new ScriptInterpreter(Content.Scripts, Party, Pickups.Inventory) { Pack = Inventory, View = Control }.Install(game);
            Pickups.Scripts = Scripts;
            Inventory.Scripts = Scripts;
            // The books: B, N, the arrows' and wheel's page turns and CHANGE BOOK. After the pack (a drop with the book
            // shown hides it instead) and before the party (left and right only mark their direction while it is shown).
            Books = new BookReader(Content.Books, Content.Hud.Book, Party).Install(game);
            Scripts.Books = Books;
            Inventory.Books = Books;
            // checkQuests and checkSubquests ran after getInput and before updateHero: the quests step before the party,
            // their scripts (HERO the item target, selectedHero) through the interpreter.
            Quests = new QuestLog(Content.Quests, Party) { Scripts = Scripts, ScriptHero = Scripts.TargetOf }.Install(game);
            Party.Install(game);
            // The options menu, hero screen, FPS counter and pause: commands only, no tick; while paused (Game.IsHeld) each system skips what the original's If Not bPause skipped.
            Interface = new InterfaceControl(Content.Hud, Party).Install(game);
            Scripts.Interface = Interface;
            // checkScroll's left edge waited while the mouse was on a button (rendercursor2D's overTeam, overInventoryBox).
            Control.OverButton = (x, y) => Content.View.TeamButton.Contains(x, y) || Content.Hud.InventoryButton.Contains(x, y);
            Control.Install(game);
            // getMouse's wheel: the battle's ring menu, the book, the open pack or the target being chosen, then the items in
            // reach, then the zoom.
            Control.WheelFirst = (player, steps) => Party.UseRingWheel(player, steps) || Books.UseWheel(player, steps) || Inventory.UseWheel(player, steps) || Pickups.UseWheel(player, steps);
            // renderTime ran before renderMessages, so the clock steps before the board: an announcement shows on its minute.
            Messages = new MessageBoard(Content.Hud);
            Clock = new WorldClock(Content.Time, Content.Heroes.Locomotion.GameSpeed, Messages);
            // checkSleep ran in renderTime after the heroes and before the minute: the sleep steps between party and clock.
            Sleep = new SleepCycle(Party, Clock).Install(game);
            Clock.Install(game);
            Messages.Install(game);
            // Engine's start-up queued the tutorial last, once everything had loaded.
            Messages.SayTutorial();
            Scripts.Sleep = Sleep;
            Scripts.Clock = Clock;
            Scripts.Messages = Messages;
            var joined = game.Submit(new JoinGame(PlayerId.One));
            if (!joined.Accepted) Debug.LogError("Unity.Overworld: player 1 could not join: " + joined.Reason);
            Viewpoint = Control.For(PlayerId.One);

            Host = GameHost.Create(game);
            Host.transform.SetParent(transform, false);
            World = WorldViewPresenter.Create(new WorldView(Content.View, Content.Props, Content.Heroes, Viewpoint.Camera));
            World.transform.SetParent(transform, false);
            World.Follow(Party, game);
            World.ShowCursor(Viewpoint.Cursor);
            World.LightBy(Clock);
            World.DimBy(Sleep);
            World.ShowPickups(Pickups);
            World.ShowWeather(Weather, game);
            World.ShowClouds(Clouds, game);
            Hud = HudPresenter.Create(Content.Hud, Clock, Messages);
            Hud.transform.SetParent(transform, false);
            Hud.ShowVitals(Party, PlayerId.One);
            Hud.ShowPickups(Pickups, Viewpoint, World, PlayerId.One);
            Hud.ShowInventory(Inventory, PlayerId.One);
            Hud.ShowQuests(Quests, PlayerId.One);
            Hud.ShowBook(Books, PlayerId.One);
            Hud.ShowConsole(Scripts, PlayerId.One);
            Hud.ShowInterface(Interface, PlayerId.One, Content.View.TeamButton);
            // renderCursor left the world cursor out under the 2D arrow (the open panel, the buttons) and while a target was chosen.
            World.CursorHidden = () =>
            {
                var pack = Inventory.For(PlayerId.One);
                return pack.Choosing || Cursor2DDefinition.Shown(Content.Hud, Content.View.TeamButton, pack.Open, Viewpoint.Cursor.MouseX, Viewpoint.Cursor.MouseY);
            };
            game.EventRaised += e =>
            {
                // Only this screen's own player quits it (U.OVW-T46: an online player's Exit is raised in the host's game).
                if (!(e is QuitRequested quit) || quit.By != PlayerId.One) return;
                Debug.Log("Unity.Overworld: Exit chosen in the options menu; quitting.");
                Application.Quit(0);
            };
            World.ShowTarget(Inventory, PlayerId.One, Content.Hud.Target);
            World.ShowRing(Party, PlayerId.One);
            TeamButton = TeamButtonPresenter.Create(World.Camera, Content.View.TeamButton, Content.View.Cursor, Party);
            Input = HeroInput.Create(game, PlayerId.One, World.Camera, Viewpoint, Party, Content.View.TeamButton);
            Input.Inventory = Inventory;
            Input.InventoryButton = Content.Hud.InventoryButton;
            Input.Scripts = Scripts;
            Input.Interface = Interface;
            Input.transform.SetParent(transform, false);
            // The developer tools (U.OVW-T47): off as shipped; -overworld-developer turns them on.
            InstallDeveloper(args, game);

            var scripted = ScriptedInput(args);
            if (scripted.HasValue)
            {
                Input.Scripted = scripted;
                Debug.Log("Unity.Overworld: scripted input " + Describe(scripted.Value));
            }

            var capture = ArgumentAfter(args, CaptureArgument);
            var scriptedView = ApplyScriptedView(args, game);
            var recalled = ApplyScriptedRecall(args, game);
            var party = ApplyScriptedParty(args, game);
            ApplyScriptedClock(args, game);
            var vitals = ApplyScriptedVitals(args, game);
            var rested = ApplyScriptedRest(args, game);
            var picked = ApplyScriptedPickups(args, game);
            var packed = ApplyScriptedInventory(args, game);
            var typed = ApplyScriptedConsole(args, game);
            var quested = ApplyScriptedQuests(args, game);
            var read = ApplyScriptedBook(args, game);
            var weathered = ApplyScriptedWeather(args, game);
            var battled = ApplyScriptedBattle(args, game);
            battled |= ApplyScriptedRing(args, game);
            var shown = ApplyScriptedInterface(args, game);
            if (scripted.HasValue || scriptedView || recalled || party || vitals || rested || picked || packed || typed || quested || read || weathered || battled || shown || seeded || capture != null) Input.UseDevices = false;

            // frmMain showed a random loading picture while the engine loaded; real play holds it a moment, scripted runs
            // skip it unless -overworld-loading asks for it.
            var loading = ArgumentAfter(args, LoadingArgument);
            if (Input.UseDevices || loading != null) StartCoroutine(LoadingScreen(loading));

            if (capture != null)
            {
                var after = ArgumentAfter(args, CaptureAfterArgument);
                var seconds = after != null && float.TryParse(after, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 0f;
                StartCoroutine(CaptureAndQuit(capture, seconds));
            }
        }

        /// <summary>The input <c>-overworld-hold</c> or <c>-overworld-move-to</c> asks for, or null when neither is given.</summary>
        public static MoveInput? ScriptedInput(string[] args)
        {
            var moveTo = ArgumentAfter(args, MoveToArgument);
            if (moveTo != null)
            {
                var parts = moveTo.Split(',');
                if (parts.Length >= 2 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    var run = parts.Length > 2 && string.Equals(parts[2].Trim(), "run", StringComparison.OrdinalIgnoreCase);
                    return MoveInput.Toward(x, y, run);
                }

                Debug.LogError("Unity.Overworld: " + MoveToArgument + " wants x,y[,run], not '" + moveTo + "'.");
            }

            var hold = ArgumentAfter(args, HoldArgument);
            if (hold == null) return null;
            bool left = false, right = false, up = false, down = false, runKey = false;
            foreach (var raw in hold.Split(','))
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "left": left = true; break;
                    case "right": right = true; break;
                    case "up": up = true; break;
                    case "down": down = true; break;
                    case "run": runKey = true; break;
                    case "": break;
                    default: Debug.LogError("Unity.Overworld: " + HoldArgument + " does not know the key '" + raw + "'."); break;
                }
            }

            return MoveInput.Arrows(left, right, up, down, runKey);
        }

        /// <summary>Applies the scripted view arguments; true when any was given.</summary>
        bool ApplyScriptedView(string[] args, Game game)
        {
            var any = false;
            var pointer = ArgumentAfter(args, PointerArgument);
            if (pointer != null)
            {
                any = true;
                if (TryPair(pointer, out var x, out var y)) Input.ScriptedPointer = new Vector2(x, y);
                else Debug.LogError("Unity.Overworld: " + PointerArgument + " wants x,y, not '" + pointer + "'.");
            }

            var wheel = ArgumentAfter(args, WheelArgument);
            if (wheel != null)
            {
                any = true;
                if (int.TryParse(wheel, NumberStyles.Integer, CultureInfo.InvariantCulture, out var steps)) Input.ScriptedWheel = steps;
                else Debug.LogError("Unity.Overworld: " + WheelArgument + " wants a whole number of steps, not '" + wheel + "'.");
            }

            var hold = ArgumentAfter(args, ViewHoldArgument);
            if (hold != null)
            {
                any = true;
                Input.ScriptedKeys = ScriptedViewKeys(hold);
            }

            if (HasFlag(args, ExamineArgument))
            {
                any = true;
                game.Submit(new ToggleExamine(PlayerId.One));
            }

            if (HasFlag(args, FreeCameraArgument))
            {
                any = true;
                game.Submit(new ToggleFreeCamera(PlayerId.One));
            }

            if (any)
                Debug.Log("Unity.Overworld: scripted view" + (pointer != null ? " pointer " + pointer : "") + (wheel != null ? " wheel " + wheel : "") +
                          (hold != null ? " holding " + hold : "") + (Viewpoint.Examine ? " examine" : "") + (Viewpoint.FreeCamera ? " free camera" : ""));
            return any;
        }

        /// <summary>Applies <c>-overworld-recall &lt;hero[,seconds]&gt;</c>; true when it was given.</summary>
        bool ApplyScriptedRecall(string[] args, Game game)
        {
            var recall = ArgumentAfter(args, RecallArgument);
            if (recall == null) return false;
            if (!TryRecall(recall, out var hero, out var seconds))
            {
                Debug.LogError("Unity.Overworld: " + RecallArgument + " wants a hero's place in the party (1 or 2) and optionally the seconds to wait, not '" + recall + "'.");
                return true;
            }

            StartCoroutine(RecallAfter(game, hero, seconds));
            return true;
        }

        /// <summary>Reads <c>hero[,seconds]</c>.</summary>
        public static bool TryRecall(string text, out int hero, out float seconds)
        {
            seconds = 0f;
            var parts = text.Split(',');
            return int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hero) &&
                   (parts.Length == 1 || (parts.Length == 2 && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f));
        }

        IEnumerator RecallAfter(Game game, int hero, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var result = game.Submit(new RecallHero(PlayerId.One, hero));
            if (result.Accepted) Debug.Log("Unity.Overworld: scripted recall of " + Party.Heroes[hero]);
            else Debug.LogError("Unity.Overworld: " + RecallArgument + " " + hero + " was refused: " + result.Reason);
        }

        /// <summary>Applies <c>-overworld-solo</c> and <c>-overworld-select</c>; true when either was given.</summary>
        bool ApplyScriptedParty(string[] args, Game game)
        {
            var any = false;
            if (HasFlag(args, SoloArgument))
            {
                any = true;
                game.Submit(new ToggleTeam(PlayerId.One));
                Debug.Log("Unity.Overworld: scripted solo (the team kneels)");
            }

            var select = ArgumentAfter(args, SelectArgument);
            if (select != null)
            {
                any = true;
                if (TrySelect(select, out var command, out var seconds)) StartCoroutine(SelectAfter(game, command, seconds));
                else Debug.LogError("Unity.Overworld: " + SelectArgument + " wants a hero (0 to 2), next or previous, and optionally the seconds to wait, not '" + select + "'.");
            }

            return any;
        }

        /// <summary>Reads <c>hero|next|previous[,seconds]</c> as the command it asks for (player 1's).</summary>
        public static bool TrySelect(string text, out ICommand command, out float seconds)
        {
            command = null;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (parts.Length == 2 && !(float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            var what = parts[0].Trim().ToLowerInvariant();
            if (what == "next") command = new CycleLeader(PlayerId.One, 1);
            else if (what == "previous") command = new CycleLeader(PlayerId.One, -1);
            else if (int.TryParse(what, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hero) && hero >= 0) command = new SelectHero(PlayerId.One, hero);
            return command != null;
        }

        IEnumerator SelectAfter(Game game, ICommand command, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var result = game.Submit(command);
            if (result.Accepted) Debug.Log("Unity.Overworld: scripted leader " + Party.Leader(PlayerId.One));
            else Debug.LogError("Unity.Overworld: " + SelectArgument + " was refused: " + result.Reason);
        }

        /// <summary>Applies <c>-overworld-clock &lt;hour:minute[,seconds]&gt;</c>; true when it was given.</summary>
        bool ApplyScriptedClock(string[] args, Game game)
        {
            var text = ArgumentAfter(args, ClockArgument);
            if (text == null) return false;
            if (!TryClock(text, out var hour, out var minute, out var seconds))
            {
                Debug.LogError("Unity.Overworld: " + ClockArgument + " wants hour:minute (0:00 to 23:59) and optionally the real seconds a minute takes, not '" + text + "'.");
                return true;
            }

            var set = game.Submit(new SetClock(PlayerId.One, hour, minute));
            if (!set.Accepted) Debug.LogError("Unity.Overworld: " + ClockArgument + " was refused: " + set.Reason);
            if (seconds.HasValue)
            {
                var ratio = game.Submit(new SetTimeRatio(PlayerId.One, seconds.Value));
                if (!ratio.Accepted) Debug.LogError("Unity.Overworld: " + ClockArgument + " seconds were refused: " + ratio.Reason);
            }

            Debug.Log("Unity.Overworld: scripted clock " + Clock.TimeText + " (" + Clock.Period.Name + "), a minute every " +
                      Clock.SecondsPerMinute.ToString(CultureInfo.InvariantCulture) + " s");
            return true;
        }

        /// <summary>Applies <c>-overworld-vitals</c>; true when it was given.</summary>
        bool ApplyScriptedVitals(string[] args, Game game)
        {
            var text = ArgumentAfter(args, VitalsArgument);
            if (text == null) return false;
            if (!TryVitals(text, out var commands))
            {
                Debug.LogError("Unity.Overworld: " + VitalsArgument + " wants hero:key=value,... groups split by ';' (keys hp, mp, ap; flags poisoned, fine), not '" + text + "'.");
                return true;
            }

            foreach (var command in commands)
            {
                var result = game.Submit(command);
                if (!result.Accepted) Debug.LogError("Unity.Overworld: " + VitalsArgument + " was refused: " + result.Reason);
                else Debug.Log("Unity.Overworld: scripted vitals " + Describe(Party.Heroes[command.Hero]));
            }

            return true;
        }

        /// <summary>Reads <c>hero:key=value,...</c> groups split by ';' as player 1's <see cref="SetHeroVitals"/> commands.</summary>
        public static bool TryVitals(string text, out List<SetHeroVitals> commands)
        {
            commands = new List<SetHeroVitals>();
            foreach (var group in text.Split(';'))
            {
                if (group.Trim().Length == 0) continue;
                var colon = group.IndexOf(':');
                if (colon < 0 || !int.TryParse(group.Substring(0, colon).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hero) || hero < 0) return false;
                int? hp = null, mp = null, ap = null;
                HeroStatus? status = null;
                foreach (var raw in group.Substring(colon + 1).Split(','))
                {
                    var item = raw.Trim().ToLowerInvariant();
                    if (item.Length == 0) continue;
                    if (item == "poisoned") { status = HeroStatus.Poisoned; continue; }
                    if (item == "fine") { status = HeroStatus.Fine; continue; }
                    var eq = item.IndexOf('=');
                    if (eq < 0 || !int.TryParse(item.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return false;
                    switch (item.Substring(0, eq))
                    {
                        case "hp": hp = value; break;
                        case "mp": mp = value; break;
                        case "ap": ap = value; break;
                        default: return false;
                    }
                }

                commands.Add(new SetHeroVitals(PlayerId.One, hero, hp, mp, ap, status));
            }

            return commands.Count > 0;
        }

        /// <summary>Applies <c>-overworld-rest</c> and <c>-overworld-sleep-until</c>; true when either was given.</summary>
        bool ApplyScriptedRest(string[] args, Game game)
        {
            var any = false;
            var rest = ArgumentAfter(args, RestArgument);
            if (rest != null)
            {
                any = true;
                if (TryRest(rest, out var presses, out var hours, out var seconds)) StartCoroutine(RestAfter(game, presses, hours, seconds));
                else Debug.LogError("Unity.Overworld: " + RestArgument + " wants kneel, lay or the hours to sleep (1 to 24), and optionally the seconds to wait, not '" + rest + "'.");
            }

            var until = ArgumentAfter(args, SleepUntilArgument);
            if (until != null)
            {
                any = true;
                if (TrySleepUntil(until, out var hour, out var wait)) StartCoroutine(SleepUntilAfter(game, hour, wait));
                else Debug.LogError("Unity.Overworld: " + SleepUntilArgument + " wants the hour to wake (0 to 23), and optionally the seconds to wait, not '" + until + "'.");
            }

            return any;
        }

        /// <summary>
        /// Reads <c>kneel|lay|hours[,seconds]</c>: the rest-key presses (1 kneels, 2 lays down) and, for a number, the hours
        /// to sleep once laid (null for kneel and lay).
        /// </summary>
        public static bool TryRest(string text, out int presses, out int? hours, out float seconds)
        {
            presses = 0;
            hours = null;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (parts.Length == 2 && !(float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            var what = parts[0].Trim().ToLowerInvariant();
            if (what == "kneel") presses = 1;
            else if (what == "lay") presses = 2;
            else if (int.TryParse(what, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h >= 1 && h <= 24)
            {
                presses = 2;
                hours = h;
            }

            return presses > 0;
        }

        /// <summary>Reads <c>hour[,seconds]</c>, the hour 0 to 23.</summary>
        public static bool TrySleepUntil(string text, out int hour, out float seconds)
        {
            hour = 0;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (parts.Length == 2 && !(float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            return int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hour) && hour >= 0 && hour <= 23;
        }

        IEnumerator RestAfter(Game game, int presses, int? hours, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            for (var i = 0; i < presses; i++)
            {
                var result = game.Submit(new Rest(PlayerId.One));
                if (result.Accepted) continue;
                Debug.LogError("Unity.Overworld: " + RestArgument + " was refused: " + result.Reason);
                yield break;
            }

            var leader = Party.Leader(PlayerId.One);
            Debug.Log("Unity.Overworld: scripted rest, " + leader.Name + (leader.Laying ? " lying down" : " kneeling"));
            if (!hours.HasValue) yield break;
            var sleep = game.Submit(new Sleep(PlayerId.One, hours));
            if (sleep.Accepted) Debug.Log("Unity.Overworld: scripted sleep of " + hours + " hour(s) from " + Clock.TimeText);
            else Debug.LogError("Unity.Overworld: " + RestArgument + " sleep was refused: " + sleep.Reason);
        }

        IEnumerator SleepUntilAfter(Game game, int hour, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var result = game.Submit(new SleepUntil(PlayerId.One, Party.LeaderOf(PlayerId.One), hour));
            if (result.Accepted) Debug.Log("Unity.Overworld: scripted sleep until " + hour + ":00 from " + Clock.TimeText);
            else Debug.LogError("Unity.Overworld: " + SleepUntilArgument + " was refused: " + result.Reason);
        }

        /// <summary>
        /// Applies <c>-overworld-place</c>, <c>-overworld-reach</c>, <c>-overworld-pickup</c> and
        /// <c>-overworld-examine-pickup</c>; true when any was given.
        /// </summary>
        bool ApplyScriptedPickups(string[] args, Game game)
        {
            var any = false;
            var place = ArgumentAfter(args, PlaceArgument);
            if (place != null)
            {
                any = true;
                if (TryPair(place, out var x, out var y))
                {
                    var result = game.Submit(new PlaceHero(PlayerId.One, Party.LeaderOf(PlayerId.One), x, y));
                    if (!result.Accepted) Debug.LogError("Unity.Overworld: " + PlaceArgument + " was refused: " + result.Reason);
                }
                else Debug.LogError("Unity.Overworld: " + PlaceArgument + " wants x,y, not '" + place + "'.");
            }

            foreach (var (name, hold) in new[] { (ReachArgument, false), (PickUpArgument, true) })
            {
                var text = ArgumentAfter(args, name);
                if (text == null) continue;
                any = true;
                if (TryPickup(text, Pickups.All.Count, out var index, out var seconds)) StartCoroutine(ReachAfter(game, index, seconds, hold));
                else Debug.LogError("Unity.Overworld: " + name + " wants a pickup's index (0 to " + (Pickups.All.Count - 1) + ") and optionally the seconds to wait, not '" + text + "'.");
            }

            var examine = ArgumentAfter(args, ExaminePickupArgument);
            if (examine != null)
            {
                any = true;
                if (TryPickup(examine, Pickups.All.Count, out var index, out var seconds)) StartCoroutine(ExamineAfter(game, index, seconds));
                else Debug.LogError("Unity.Overworld: " + ExaminePickupArgument + " wants a pickup's index and optionally the seconds to wait, not '" + examine + "'.");
            }

            return any;
        }

        /// <summary>Reads <c>index[,seconds]</c>, the index a pickup's (0 to <paramref name="count"/> - 1).</summary>
        public static bool TryPickup(string text, int count, out int index, out float seconds)
        {
            index = -1;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (parts.Length == 2 && !(float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            return int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out index) && index >= 0 && index < count;
        }

        static IEnumerator NextTick(Game game)
        {
            var tick = game.Tick;
            while (game.Tick == tick) yield return null;
        }

        /// <summary>
        /// Places the leader so pickup <paramref name="index"/> is in reach (its reach point on the pickup's), selects it
        /// with the , / . steps and, when <paramref name="hold"/>, holds the pick-up button from then on (it kneels and takes it).
        /// </summary>
        IEnumerator ReachAfter(Game game, int index, float seconds, bool hold)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var p = Pickups.All[index];
            var reach = Pickups.Data.Reach;
            var item = Pickups.Data.ItemSize;
            var size = Party.Data.HeroSize;
            var leader = Party.LeaderOf(PlayerId.One);
            var placed = game.Submit(new PlaceHero(PlayerId.One, leader,
                p.X + item * reach.ItemPoint.X - size * reach.HeroPoint.X, p.Y + item * reach.ItemPoint.Y - size * reach.HeroPoint.Y));
            if (!placed.Accepted)
            {
                Debug.LogError("Unity.Overworld: placing the leader by " + p + " was refused: " + placed.Reason);
                yield break;
            }

            // Chosen again, as a click on it would, so the camera centres on where it now stands.
            game.Submit(new SelectHero(PlayerId.One, leader));
            yield return NextTick(game);
            for (var i = 0; i < reach.Listed && Pickups.Selected(PlayerId.One) != index; i++) game.Submit(new CyclePickup(PlayerId.One, 1));
            Debug.Log("Unity.Overworld: scripted reach of " + p + ", in reach [" + string.Join(", ", Pickups.InReach(PlayerId.One)) + "], selected " +
                      Pickups.Selected(PlayerId.One));
            if (!hold) yield break;
            Input.Scripted = (Input.Scripted ?? default(MoveInput)).WithPickUp(true);
            yield return NextTick(game);
            Debug.Log("Unity.Overworld: scripted pickup, " + p + "; " + Party.Leader(PlayerId.One) + "; " + DescribePack());
        }

        /// <summary>Turns examine mode on and puts the world cursor over pickup <paramref name="index"/>.</summary>
        IEnumerator ExamineAfter(Game game, int index, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (!Viewpoint.Examine) game.Submit(new ToggleExamine(PlayerId.One));
            var p = Pickups.All[index];
            var h = Pickups.Data.Hover;
            game.Submit(new PlaceCursor(PlayerId.One, p.X + h.ItemOffset.X - h.CursorOffset.X, p.Y + h.ItemOffset.Y - h.CursorOffset.Y));
            Debug.Log("Unity.Overworld: scripted examine of " + p + ", under the cursor " + Pickups.PickupAt(Viewpoint.Cursor.X, Viewpoint.Cursor.Y));
        }

        /// <summary>
        /// Applies the inventory's scripted arguments; true when any was given:
        /// <c>-overworld-give &lt;pickup[*count],...&gt;</c> puts those pickups' items into the pack at once (e.g.
        /// <c>0*3,201*2,105</c>); <c>-overworld-quickbar</c> shows the quick bar; <c>-overworld-inventory &lt;slot[,seconds]&gt;</c>
        /// opens the panel with its cursor on that stack; <c>-overworld-swap &lt;from,to[,seconds]&gt;</c> marks stack from with
        /// the run key and swaps it with stack to; <c>-overworld-drop &lt;slot[*count][,seconds]&gt;</c> drops that many of the
        /// stack (Backspace), leaving the panel open; <c>-overworld-use &lt;slot[:hero][,seconds]&gt;</c> presses Enter on the
        /// stack and, when it waits for its target, chooses that hero and presses Enter again (without a hero it is left
        /// waiting, the target marked); <c>-overworld-quick &lt;key[:hero][,seconds]&gt;</c> presses that number key (1 to 9,
        /// 0) the same way; <c>-overworld-pickup-drop &lt;drop[,seconds]&gt;</c> puts the leader by that drop and holds the
        /// pick-up button.
        /// </summary>
        bool ApplyScriptedInventory(string[] args, Game game)
        {
            var any = false;
            var give = ArgumentAfter(args, GiveArgument);
            if (give != null)
            {
                any = true;
                if (TryGive(give, Pickups.All.Count, out var gifts))
                    foreach (var (pickup, count) in gifts)
                    {
                        var r = game.Submit(new GiveItem(PlayerId.One, pickup, count));
                        if (!r.Accepted) Debug.LogError("Unity.Overworld: " + GiveArgument + " " + pickup + " was refused: " + r.Reason);
                    }
                else Debug.LogError("Unity.Overworld: " + GiveArgument + " wants pickup[*count] items split by ',', not '" + give + "'.");
                Debug.Log("Unity.Overworld: scripted give; " + DescribePack());
            }

            if (HasFlag(args, QuickBarArgument))
            {
                any = true;
                game.Submit(new ToggleQuickBar(PlayerId.One));
            }

            foreach (var (name, kind) in new[] { (InventoryArgument, 'o'), (SwapArgument, 's'), (DropArgument, 'd'), (UseArgument, 'u'), (QuickArgument, 'q'), (PickUpDropArgument, 'p') })
            {
                var text = ArgumentAfter(args, name);
                if (text == null) continue;
                any = true;
                if (TryInventoryStep(text, kind, out var a, out var b, out var seconds)) StartCoroutine(InventoryAfter(game, kind, a, b, seconds));
                else Debug.LogError("Unity.Overworld: " + name + " was not understood: '" + text + "' (see OverworldBootstrap.ApplyScriptedInventory).");
            }

            return any;
        }

        /// <summary>Reads <c>pickup[*count],...</c>.</summary>
        public static bool TryGive(string text, int pickups, out List<(int Pickup, int Count)> gifts)
        {
            gifts = new List<(int, int)>();
            foreach (var raw in text.Split(','))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                var star = part.Split('*');
                var count = 1;
                if (star.Length > 2 || !int.TryParse(star[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pickup) || pickup < 0 || pickup >= pickups) return false;
                if (star.Length == 2 && !(int.TryParse(star[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && count >= 1)) return false;
                gifts.Add((pickup, count));
            }

            return gifts.Count > 0;
        }

        /// <summary>
        /// Reads one inventory step: <c>a[:b|*b][,seconds]</c> (b is the hero for use and quick, the count for drop), or
        /// <c>from,to[,seconds]</c> for swap. A missing b is -1 (a count of 1 for drop).
        /// </summary>
        public static bool TryInventoryStep(string text, char kind, out int a, out int b, out float seconds)
        {
            a = b = -1;
            seconds = 0f;
            var parts = text.Split(',');
            var first = parts[0].Trim();
            var rest = 1;
            if (kind == 's')
            {
                if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b) || b < 0) return false;
                rest = 2;
            }
            else
            {
                var split = first.Split(kind == 'd' ? '*' : ':');
                if (split.Length > 2) return false;
                if (split.Length == 2 && !(int.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out b) && b >= 0)) return false;
                first = split[0];
                if (kind == 'd' && b < 1) b = 1;
            }

            if (!int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out a) || a < 0) return false;
            if (parts.Length > rest + 1) return false;
            if (parts.Length == rest + 1 && !(float.TryParse(parts[rest].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            return kind != 'q' || a <= 9;
        }

        IEnumerator InventoryAfter(Game game, char kind, int a, int b, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var v = Inventory.For(PlayerId.One);
            void Do(ICommand c)
            {
                var r = game.Submit(c);
                if (!r.Accepted) Debug.LogError("Unity.Overworld: scripted " + c.GetType().Name + " was refused: " + r.Reason);
            }

            void Open(int slot)
            {
                if (!v.Open) Do(new ToggleInventory(PlayerId.One));
                if (slot >= 0) Do(new MoveInventoryCursor(PlayerId.One, slot));
            }

            void Target(int hero)
            {
                if (!v.Choosing || hero < 0) return;
                for (var i = 0; i < Party.Heroes.Count && v.Target != hero; i++) Do(new ChooseTarget(PlayerId.One, 1));
                Do(new UseItem(PlayerId.One));
            }

            switch (kind)
            {
                case 'o':
                    Open(a);
                    break;
                case 's':
                    Open(a);
                    Do(new SwapItem(PlayerId.One));
                    Do(new MoveInventoryCursor(PlayerId.One, b));
                    Do(new SwapItem(PlayerId.One));
                    break;
                case 'd':
                    Open(a);
                    for (var i = 0; i < b; i++) Do(new DropItem(PlayerId.One));
                    break;
                case 'u':
                    Open(a);
                    Do(new UseItem(PlayerId.One));
                    Target(b);
                    break;
                case 'q':
                    // The 1 key is quick slot 0 ... the 0 key slot 9.
                    Do(new QuickUse(PlayerId.One, (a + 9) % 10));
                    Target(b);
                    break;
                case 'p':
                    if (a >= Pickups.Drops.Count)
                    {
                        Debug.LogError("Unity.Overworld: " + PickUpDropArgument + " " + a + ": there are " + Pickups.Drops.Count + " drops.");
                        yield break;
                    }

                    if (v.Open) Do(new ToggleInventory(PlayerId.One));
                    var d = Pickups.Drops[a];
                    var reach = Pickups.Data.Reach;
                    var item = Pickups.Data.ItemSize;
                    var size = Party.Data.HeroSize;
                    var leader = Party.LeaderOf(PlayerId.One);
                    Do(new PlaceHero(PlayerId.One, leader, d.X + item * reach.ItemPoint.X - size * reach.HeroPoint.X, d.Y + item * reach.ItemPoint.Y - size * reach.HeroPoint.Y));
                    Do(new SelectHero(PlayerId.One, leader));
                    yield return NextTick(game);
                    for (var i = 0; i < reach.Listed && Pickups.SelectedDrop(PlayerId.One) != a; i++) Do(new CyclePickup(PlayerId.One, 1));
                    Debug.Log("Unity.Overworld: scripted reach of " + d + ", drops in reach [" + string.Join(", ", Pickups.DropsInReach(PlayerId.One)) + "]");
                    Input.Scripted = (Input.Scripted ?? default(MoveInput)).WithPickUp(true);
                    yield return NextTick(game);
                    break;
            }

            Debug.Log("Unity.Overworld: scripted inventory " + kind + " " + a + (b >= 0 ? " " + b : "") + "; " + DescribeInventory());
        }

        /// <summary>
        /// Applies the console's scripted arguments; true when any was given: <c>-overworld-script &lt;script&gt;</c> opens
        /// the console, types the script key by key as a player would (the C shortcut and all) and presses Enter, so it runs
        /// and becomes the one Insert recalls; <c>-overworld-console &lt;text&gt;</c> then opens the console and types the
        /// text, leaving it open so a capture shows the prompt; <c>-overworld-script-after &lt;seconds&gt;</c> waits that many
        /// seconds of play first.
        /// </summary>
        bool ApplyScriptedConsole(string[] args, Game game)
        {
            var run = ArgumentAfter(args, ScriptArgument);
            var show = ArgumentAfter(args, ConsoleArgument);
            if (run == null && show == null) return false;
            var seconds = 0f;
            var after = ArgumentAfter(args, ScriptAfterArgument);
            if (after != null && !(float.TryParse(after, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f))
            {
                Debug.LogError("Unity.Overworld: " + ScriptAfterArgument + " wants the seconds to wait, not '" + after + "'.");
                seconds = 0f;
            }

            StartCoroutine(ConsoleAfter(game, run, show, seconds));
            return true;
        }

        IEnumerator ConsoleAfter(Game game, string run, string show, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var console = Content.Scripts.Console;
            if (run != null && TypeLine(game, run))
            {
                var result = game.Submit(new PressConsoleKey(PlayerId.One, console.Run));
                var outcome = "not run";
                foreach (var e in result.Events)
                    if (e is ScriptRan ran) outcome = ran.Outcome.ToString();
                Debug.Log("Unity.Overworld: scripted script '" + run + "': " + outcome + "; " + Describe(Party.Leader(PlayerId.One)) + " at (" +
                          Party.Leader(PlayerId.One).X.ToString(CultureInfo.InvariantCulture) + ", " + Party.Leader(PlayerId.One).Y.ToString(CultureInfo.InvariantCulture) + "); " +
                          DescribeScripts());
            }

            if (show != null && TypeLine(game, show))
                Debug.Log("Unity.Overworld: scripted console '" + Scripts.Prompt(PlayerId.One) + "'");
        }

        /// <summary>Opens the console, clears its line and types <paramref name="text"/> with the console's keys; false when it has no keys for it.</summary>
        bool TypeLine(Game game, string text)
        {
            var console = Content.Scripts.Console;
            var keys = ScriptInterpreter.KeysFor(console, text);
            if (keys == null)
            {
                Debug.LogError("Unity.Overworld: the console has no keys to type '" + text + "'.");
                return false;
            }

            if (!Scripts.For(PlayerId.One).ConsoleOpen) game.Submit(new PressConsoleKey(PlayerId.One, console.Open));
            game.Submit(new PressConsoleKey(PlayerId.One, console.Clear));
            foreach (var (key, shift) in keys) game.Submit(new PressConsoleKey(PlayerId.One, key, shift));
            return true;
        }

        /// <summary>
        /// Applies the quests' scripted arguments; true when any was given: <c>-overworld-objective &lt;item+item...[,seconds]&gt;</c>
        /// completes objectives in turn, each item <c>q:o</c> (main quest q's objective o) or <c>s:n:o</c> (subquest n's):
        /// the leader is placed on every one but the last, which it walks into (placed 0.2 below and left of it, then the
        /// move button held toward it), so a capture shows the walk and the marker going; <c>-overworld-quest-screen
        /// &lt;main|subquests|subquest:n|finished[,seconds]&gt;</c> presses Page Down and the arrows to show that page.
        /// </summary>
        bool ApplyScriptedQuests(string[] args, Game game)
        {
            var any = false;
            var objective = ArgumentAfter(args, ObjectiveArgument);
            if (objective != null)
            {
                any = true;
                if (TryObjectives(objective, Quests, out var items, out var seconds)) StartCoroutine(ObjectivesAfter(game, items, seconds));
                else Debug.LogError("Unity.Overworld: " + ObjectiveArgument + " wants q:o or s:n:o items joined by +, and optionally the seconds to wait, not '" + objective + "'.");
            }

            var screen = ArgumentAfter(args, QuestScreenArgument);
            if (screen != null)
            {
                any = true;
                if (TryQuestScreen(screen, out var keys, out var seconds)) StartCoroutine(QuestScreenAfter(game, keys, seconds));
                else Debug.LogError("Unity.Overworld: " + QuestScreenArgument + " wants main, subquests, subquest:n or finished, and optionally the seconds to wait, not '" + screen + "'.");
            }

            return any;
        }

        /// <summary>Reads <c>item+item...[,seconds]</c>, each item <c>q:o</c> or <c>s:n:o</c>, as the objectives of <paramref name="quests"/>.</summary>
        public static bool TryObjectives(string text, QuestLog quests, out List<Objective> items, out float seconds)
        {
            items = new List<Objective>();
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (parts.Length == 2 && !(float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            foreach (var raw in parts[0].Split('+'))
            {
                var f = raw.Trim().Split(':');
                var sub = f.Length == 3 && f[0].Trim().ToLowerInvariant() == "s";
                if (!(f.Length == 2 || sub)) return false;
                var list = sub ? quests.Subquests : quests.Quests;
                var at = sub ? 1 : 0;
                if (!int.TryParse(f[at].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) || q < 0 || q >= list.Count) return false;
                if (!int.TryParse(f[at + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var o) || o < 0 || o >= list[q].Objectives.Count) return false;
                items.Add(list[q].Objectives[o]);
            }

            return items.Count > 0;
        }

        /// <summary>Reads <c>main|subquests|subquest:n|finished[,seconds]</c> as the arrows that show that page once the screen opens.</summary>
        public static bool TryQuestScreen(string text, out List<QuestKey> keys, out float seconds)
        {
            keys = new List<QuestKey>();
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (parts.Length == 2 && !(float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f)) return false;
            var what = parts[0].Trim().ToLowerInvariant();
            if (what == "main") return true;
            keys.Add(QuestKey.Down);
            if (what == "subquests") return true;
            if (what == "finished")
            {
                keys.Add(QuestKey.Down);
                return true;
            }

            if (!what.StartsWith("subquest:", StringComparison.Ordinal) ||
                !int.TryParse(what.Substring(9), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 0) return false;
            keys.Add(QuestKey.Right);
            for (var i = 0; i < n; i++) keys.Add(QuestKey.Down);
            keys.Add(QuestKey.Right);
            return true;
        }

        IEnumerator ObjectivesAfter(Game game, List<Objective> items, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var leader = Party.LeaderOf(PlayerId.One);
            for (var i = 0; i < items.Count; i++)
            {
                var o = items[i];
                var last = i == items.Count - 1;
                var at = last ? Quests.StandingAt(o.X - 0.2f, o.Y - 0.2f) : Quests.StandingAt(o.X, o.Y);
                var placed = game.Submit(new PlaceHero(PlayerId.One, leader, at.X, at.Y));
                if (!placed.Accepted)
                {
                    Debug.LogError("Unity.Overworld: placing the leader by " + o + " was refused: " + placed.Reason);
                    yield break;
                }

                // Chosen again, so the camera centres on where it now stands.
                game.Submit(new SelectHero(PlayerId.One, leader));
                yield return NextTick(game);
                if (!last) continue;
                // The move button heads the leader's point for the cursor plus the locomotion's offset: aim it at the objective.
                var offset = Party.Data.Locomotion.CursorOffset;
                Input.Scripted = MoveInput.Toward(o.X - offset.X, o.Y - offset.Y);
                var start = Time.realtimeSinceStartup;
                while (!o.Complete && Time.realtimeSinceStartup - start < 10f) yield return null;
                Input.Scripted = default(MoveInput);
            }

            Debug.Log("Unity.Overworld: scripted objectives; " + DescribeQuests());
        }

        IEnumerator QuestScreenAfter(Game game, List<QuestKey> keys, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (!Quests.For(PlayerId.One).Open) game.Submit(new ToggleQuestScreen(PlayerId.One));
            foreach (var key in keys)
            {
                var r = game.Submit(new PressQuestKey(PlayerId.One, key));
                if (!r.Accepted) Debug.LogError("Unity.Overworld: " + QuestScreenArgument + " " + key + " was refused: " + r.Reason);
            }

            var v = Quests.For(PlayerId.One);
            Debug.Log("Unity.Overworld: scripted quest screen " + v.Page + (v.Subquest >= 0 ? " subquest " + v.Subquest : ""));
        }

        string DescribeQuests()
        {
            var v = Quests.For(PlayerId.One);
            return "experience " + Party.Experience + ", current " + Quests.Current + ", objectives [" +
                   string.Join(", ", Quests.Quests.SelectMany(q => q.Objectives).Concat(Quests.Subquests.SelectMany(q => q.Objectives))) + "], subquests [" +
                   string.Join(", ", Quests.Subquests) + "], screen " + (v.Open ? "open on " + v.Page + (v.Subquest >= 0 ? " subquest " + v.Subquest : "") : "shut");
        }

        string DescribeBook()
        {
            var v = Books.For(PlayerId.One);
            return "'" + Books.Current(PlayerId.One).Title + "' pages " + (v.Page + 1) + " and " + (v.Page + 2) + (v.Shown ? " shown" : " hidden") +
                   ", " + v.Numbering + ", cover " + (v.Cover ?? "plain") + ", lettering " + v.Lettering;
        }

        /// <summary>
        /// <c>-overworld-book &lt;title|number[,page[,seconds]]&gt;</c>: the book shown at the spread holding that page (1
        /// first), at once or after that many seconds, styled as it opens in play: a book with a pickup as that item's
        /// script opens it (its cover, lettering and numbering), the Colesium (no pickup) as N loads it.
        /// </summary>
        bool ApplyScriptedBook(string[] args, Game game)
        {
            var text = ArgumentAfter(args, BookArgument);
            if (text == null) return false;
            if (TryBook(text, Books, out var book, out var page, out var seconds)) StartCoroutine(BookAfter(game, book, page, seconds));
            else Debug.LogError("Unity.Overworld: " + BookArgument + " wants a book's title or number (0 to " + (Books.Library.Books.Count - 1) +
                                "), optionally its page (1 first) and the seconds to wait, not '" + text + "'.");
            return true;
        }

        /// <summary>Reads <c>title|number[,page[,seconds]]</c>: the book's index, the page (0 first) and the seconds.</summary>
        public static bool TryBook(string text, BookReader books, out int book, out int page, out float seconds)
        {
            book = -1;
            page = 0;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 3) return false;
            var what = parts[0].Trim();
            book = int.TryParse(what, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : books.IndexOf(what);
            if (book < 0 || book >= books.Library.Books.Count) return false;
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) || p < 1 || p > books.Library.Books[book].Pages.Count) return false;
                page = p - 1;
            }

            return parts.Length < 3 || (float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f);
        }

        IEnumerator BookAfter(Game game, int book, int page, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var title = Books.Library.Books[book].Title;
            var item = Content.Pickups.Pickups.FirstOrDefault(p => p.Name == title && p.Script.Contains("CHANGE BOOK"));
            // The item's script but its TO ITEM (which names the pack's selected stack), or N's lettering.
            var style = item != null
                ? string.Join(" | ", item.Script.Split(new[] { " | " }, StringSplitOptions.None).Where(c => !c.Trim().StartsWith("CHANGE BOOK TO", StringComparison.OrdinalIgnoreCase)))
                : "CHANGE BOOK FONT " + Books.Screen.NextLettering;
            game.Submit(new RunScript(PlayerId.One, style));
            var r = game.Submit(new OpenBook(PlayerId.One, book, page));
            if (!r.Accepted) Debug.LogError("Unity.Overworld: " + BookArgument + " was refused: " + r.Reason);
            Debug.Log("Unity.Overworld: scripted book " + DescribeBook());
        }

        /// <summary>
        /// <c>-overworld-weather &lt;snowing|raining|clear|leaves|dust[,seconds]&gt;</c>: the weather set to that mode (the
        /// original's names, FallingLeaves and DustStorm, also do), at once or after that many seconds, as W would cycle to it.
        /// </summary>
        bool ApplyScriptedWeather(string[] args, Game game)
        {
            var text = ArgumentAfter(args, WeatherArgument);
            if (text == null) return false;
            if (TryWeather(text, out var kind, out var seconds)) StartCoroutine(WeatherAfter(game, kind, seconds));
            else Debug.LogError("Unity.Overworld: " + WeatherArgument + " wants snowing, raining, clear, leaves or dust and optionally the seconds to wait, not '" + text + "'.");
            return true;
        }

        /// <summary>Reads <c>kind[,seconds]</c>.</summary>
        public static bool TryWeather(string text, out WeatherKind kind, out float seconds)
        {
            kind = WeatherKind.Snowing;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            switch (parts[0].Trim().ToLowerInvariant())
            {
                case "snowing": case "snow": kind = WeatherKind.Snowing; break;
                case "raining": case "rain": kind = WeatherKind.Raining; break;
                case "clear": kind = WeatherKind.Clear; break;
                case "fallingleaves": case "leaves": kind = WeatherKind.FallingLeaves; break;
                case "duststorm": case "dust": kind = WeatherKind.DustStorm; break;
                default: return false;
            }

            return parts.Length < 2 || (float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f);
        }

        IEnumerator WeatherAfter(Game game, WeatherKind kind, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var r = game.Submit(new SetWeather(PlayerId.One, kind));
            if (!r.Accepted) Debug.LogError("Unity.Overworld: " + WeatherArgument + " was refused: " + r.Reason);
            Debug.Log("Unity.Overworld: scripted weather " + Weather.Kind);
        }

        /// <summary>
        /// <c>-overworld-battle &lt;seconds[,seconds]&gt;</c>: End is pressed after that many seconds of play (0: at once),
        /// entering battle mode with the team; a second number presses it again that many seconds later, leaving it. With
        /// <c>-overworld-vitals</c> (e.g. <c>0:ap=30;2:ap=50</c>) captures show heroes recharging beside ones drawing.
        /// </summary>
        bool ApplyScriptedBattle(string[] args, Game game)
        {
            var text = ArgumentAfter(args, BattleArgument);
            if (text == null) return false;
            if (TryBattle(text, out var enter, out var leave)) StartCoroutine(BattleAfter(game, enter, leave));
            else Debug.LogError("Unity.Overworld: " + BattleArgument + " wants the seconds to wait before End and optionally the seconds before End again, not '" + text + "'.");
            return true;
        }

        /// <summary>Reads <c>seconds[,seconds]</c>.</summary>
        public static bool TryBattle(string text, out float enter, out float? leave)
        {
            enter = 0f;
            leave = null;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out enter) || !(enter >= 0f)) return false;
            if (parts.Length == 1) return true;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var l) || !(l >= 0f)) return false;
            leave = l;
            return true;
        }

        IEnumerator BattleAfter(Game game, float enter, float? leave)
        {
            if (enter > 0f) yield return new WaitForSecondsRealtime(enter);
            ToggleBattleOnce(game);
            if (leave == null) yield break;
            if (leave.Value > 0f) yield return new WaitForSecondsRealtime(leave.Value);
            ToggleBattleOnce(game);
        }

        void ToggleBattleOnce(Game game)
        {
            var r = game.Submit(new ToggleBattle(PlayerId.One));
            if (!r.Accepted) Debug.LogError("Unity.Overworld: " + BattleArgument + " was refused: " + r.Reason);
            else Debug.Log("Unity.Overworld: scripted End; " + DescribeBattle());
        }

        /// <summary>
        /// <c>-overworld-ring &lt;steps[,choose][,seconds]&gt;</c>: after that many seconds of play (default 1, once End has
        /// opened the ring) the ring turns that many entries (negative left, positive right), each turn finished before the
        /// next, and with <c>choose</c> its top entry is chosen as Enter does, e.g. <c>-overworld-battle 0 -overworld-ring 0,choose
        /// -overworld-move-to 0.4,0</c> walks the leader its move distance.
        /// </summary>
        bool ApplyScriptedRing(string[] args, Game game)
        {
            var text = ArgumentAfter(args, RingArgument);
            if (text == null) return false;
            if (TryRing(text, out var steps, out var choose, out var seconds)) StartCoroutine(RingAfter(game, steps, choose, seconds));
            else Debug.LogError("Unity.Overworld: " + RingArgument + " wants the entries to turn (negative left), optionally 'choose' and the seconds to wait, not '" + text + "'.");
            return true;
        }

        /// <summary>Reads <c>steps[,choose][,seconds]</c>.</summary>
        public static bool TryRing(string text, out int steps, out bool choose, out float seconds)
        {
            choose = false;
            seconds = 1f;
            var parts = text.Split(',');
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out steps)) return false;
            for (var i = 1; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (string.Equals(p, "choose", StringComparison.OrdinalIgnoreCase)) choose = true;
                else if (!float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || !(seconds >= 0f)) return false;
            }

            return true;
        }

        IEnumerator RingAfter(Game game, int steps, bool choose, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var ring = Party.RingOf(PlayerId.One);
            for (var i = 0; i < Mathf.Abs(steps); i++)
            {
                var r = game.Submit(new TurnRing(PlayerId.One, steps < 0 ? -1 : 1));
                if (!r.Accepted)
                {
                    Debug.LogError("Unity.Overworld: " + RingArgument + " turn was refused: " + r.Reason);
                    yield break;
                }

                while (ring.Mode != RingMode.Select) yield return null;
            }

            if (choose)
            {
                var r = game.Submit(new ChooseFromRing(PlayerId.One));
                if (!r.Accepted) Debug.LogError("Unity.Overworld: " + RingArgument + " choose was refused: " + r.Reason);
            }

            Debug.Log("Unity.Overworld: scripted ring; " + DescribeBattle());
        }

        /// <summary>
        /// Applies the interface's scripted arguments; true when any was given (<c>-overworld-loading</c> is applied by the
        /// loading screen): <c>-overworld-options &lt;main|controls|video|audio[,row][,seconds]&gt;</c> opens the options menu
        /// (F11), picks that page from the main page with Down and Enter, and puts the cursor on that row (0 to 3) with Down;
        /// <c>-overworld-hero-screen</c> opens the hero screen (Page Up); <c>-overworld-pause</c> pauses the game (the Pause
        /// key); <c>-overworld-fps-off</c> hides the FPS counter (F4); <c>-overworld-hover &lt;hero[,seconds]&gt;</c> puts the
        /// world cursor on that hero (0 Chrono, 1 Belmont, 2 Flagg), showing its name when it does not lead.
        /// </summary>
        bool ApplyScriptedInterface(string[] args, Game game)
        {
            var any = false;
            void Do(ICommand c, string what)
            {
                var r = game.Submit(c);
                if (!r.Accepted) Debug.LogError("Unity.Overworld: " + what + " was refused: " + r.Reason);
            }

            var options = ArgumentAfter(args, OptionsArgument);
            if (options != null)
            {
                any = true;
                if (TryOptions(options, Interface.Options, out var page, out var row, out var seconds)) StartCoroutine(OptionsAfter(game, page, row, seconds));
                else Debug.LogError("Unity.Overworld: " + OptionsArgument + " wants a page (main, controls, video, audio), optionally the row (0 to " + (Interface.Options.Rows - 1) +
                                    ") and the seconds to wait, not '" + options + "'.");
            }

            if (HasFlag(args, HeroScreenArgument))
            {
                any = true;
                Do(new ToggleHeroScreen(PlayerId.One), HeroScreenArgument);
            }

            if (HasFlag(args, FpsOffArgument))
            {
                any = true;
                Do(new ToggleFps(PlayerId.One), FpsOffArgument);
            }

            if (HasFlag(args, PauseArgument))
            {
                any = true;
                Do(new TogglePause(PlayerId.One), PauseArgument);
            }

            var hover = ArgumentAfter(args, HoverArgument);
            if (hover != null)
            {
                any = true;
                if (TryRecall(hover, out var hero, out var wait) && hero >= 0 && hero < Party.Heroes.Count) StartCoroutine(HoverAfter(game, hero, wait));
                else Debug.LogError("Unity.Overworld: " + HoverArgument + " wants a hero (0 to " + (Party.Heroes.Count - 1) + ") and optionally the seconds to wait, not '" + hover + "'.");
            }

            if (any) Debug.Log("Unity.Overworld: scripted interface; " + DescribeInterface());
            return any;
        }

        /// <summary>Reads <c>page[,row][,seconds]</c>: the page's index in <paramref name="menu"/>, the row and the seconds.</summary>
        public static bool TryOptions(string text, OptionsMenuDefinition menu, out int page, out int row, out float seconds)
        {
            page = -1;
            row = 0;
            seconds = 0f;
            var parts = text.Split(',');
            if (parts.Length > 3) return false;
            var name = parts[0].Trim();
            for (var i = 0; i < menu.Pages.Count; i++)
                if (string.Equals(menu.Pages[i].Name, name, StringComparison.OrdinalIgnoreCase)) page = i;
            if (page < 0) return false;
            if (parts.Length > 1 && !(int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out row) && row >= 0 && row < menu.Rows)) return false;
            return parts.Length < 3 || (float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0f);
        }

        IEnumerator OptionsAfter(Game game, int page, int row, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var v = Interface.For(PlayerId.One);
            var menu = Interface.Options;
            void Key(OptionsKey key) => game.Submit(new PressOptionsKey(PlayerId.One, key));
            void DownTo(int target)
            {
                for (var i = 0; i < menu.Rows && v.OptionsCursor != target; i++) Key(OptionsKey.Down);
            }

            if (!v.OptionsOpen) game.Submit(new ToggleOptions(PlayerId.One));
            if (page > 0)
            {
                var main = menu.Pages[0].Rows;
                for (var i = 0; i < main.Count; i++)
                    if (main[i].Opens == menu.Pages[page].Name)
                    {
                        DownTo(i);
                        Key(OptionsKey.Enter);
                        break;
                    }
            }

            DownTo(row);
            Debug.Log("Unity.Overworld: scripted options; " + DescribeInterface());
        }

        /// <summary>Puts the world cursor where <see cref="Party.HeroAt"/> finds hero <paramref name="hero"/>.</summary>
        IEnumerator HoverAfter(Game game, int hero, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            var h = Party.Heroes[hero];
            var s = Party.Data.Selection;
            var size = Party.Data.HeroSize;
            var logged = false;
            // Held on the hero every frame, as a hand would follow it while it walks after the leader.
            while (true)
            {
                var x = h.X + size * s.HeroPoint.X + s.HeroOffset.X - s.CursorOffset.X;
                var y = h.Y + size * s.HeroPoint.Y + s.HeroOffset.Y - s.CursorOffset.Y;
                game.Submit(new PlaceCursor(PlayerId.One, x, y));
                if (!logged) Debug.Log("Unity.Overworld: scripted hover over " + h.Name + ", under the cursor " + Party.HeroAt(Viewpoint.Cursor.X, Viewpoint.Cursor.Y));
                logged = true;
                yield return null;
            }
        }

        /// <summary>
        /// frmMain.Startup's loading picture: <c>-overworld-loading &lt;1..12|random&gt;</c> picks one (else at random), shown
        /// over everything while the game is held, then play begins.
        /// </summary>
        IEnumerator LoadingScreen(string which)
        {
            var pictures = Content.Hud.Loading.Pictures;
            var file = Content.Hud.Loading.Pick(new System.Random());
            if (which != null && int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                if (n >= 1 && n <= pictures.Count) file = pictures[n - 1];
                else Debug.LogError("Unity.Overworld: " + LoadingArgument + " wants 1 to " + pictures.Count + " or random, not '" + which + "'.");
            }

            Hud.ShowLoading(file);
            Host.enabled = false;
            var devices = Input.enabled;
            Input.enabled = false;
            Debug.Log("Unity.Overworld: loading picture " + file);
            yield return new WaitForSecondsRealtime(Content.Hud.Loading.Seconds);
            Hud.HideLoading();
            Host.enabled = true;
            Input.enabled = devices;
        }

        string DescribeInterface()
        {
            var v = Interface.For(PlayerId.One);
            var leader = Party.LeaderOf(PlayerId.One);
            return "options " + (v.OptionsOpen ? "open on " + Interface.PageOf(PlayerId.One).Name + " row " + v.OptionsCursor : "shut") +
                   ", hero screen " + (v.HeroScreenOpen ? "open" : "shut") + ", fps " + (v.FpsShown ? "shown" : "hidden") + (Interface.Paused ? ", paused" : "") +
                   ", turn bars [" + string.Join(", ", TurnBarsView.Bars(Content.Hud.TurnBars, Party, leader).Select(b => Party.Heroes[b.Hero].Name + " " + b.FillWidth + " px " + b.Frame)) + "]" +
                   (Hud != null ? (Hud.Cursor2DShown ? ", 2D cursor shown" : "") + (Hud.LoadingFile != null ? ", loading " + Hud.LoadingFile : "") +
                                  (Hud.HeroHoverName != null && Hud.HeroHoverName.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex ? ", hover name '" + Hud.HeroHoverName.text + "'" : "") +
                                  (Hud.FpsLabel != null && Hud.FpsLabel.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex ? ", '" + Hud.FpsLabel.text + "'" : "") +
                                  (v.HeroScreenOpen && Hud.HeroDateLabel != null ? ", hero screen [" + Hud.HeroDateLabel.text + " | " + Hud.HeroTimeLabel.text + " | " + Hud.HeroExperienceLabel.text + "]" : "")
                       : "") +
                   (World != null && World.CursorRenderer != null ? ", world cursor " + (World.CursorRenderer.enabled ? "drawn" : "hidden") : "");
        }

        string DescribeBattle() =>
            "ring " + Party.RingOf(PlayerId.One) + ", leading " + Party.Leader(PlayerId.One).Name + " (move distance " +
            Party.Leader(PlayerId.One).MoveDistance.ToString("0.###", CultureInfo.InvariantCulture) + ")" +
            (World != null && World.RingIcons.Count > 0 ? ", ring icons drawn " + World.RingIcons.Count(r => r.enabled) + ", circle dots drawn " + World.CircleDots.Count(r => r.enabled) +
                                                          " [" + string.Join(" ", World.CircleDots.Where(r => r.enabled).Select(r => World.FileOf(r)).Distinct()) + "]" : "") + "; " +
            string.Join("; ", Party.Heroes.Select(h => h.Name + " " + h.Mode + " " + h.State + " " + h.BattleAction + " AP " + h.Stamina + "/" + h.MaxStamina +
                                                      " frame " + h.Frame + " showing " + h.Sprite + " weapon " + (h.WeaponSprite ?? "blank")));

        string DescribeScenery() =>
            Weather.Kind + ", " + (World != null && World.WeatherQuads != null && World.WeatherRenderer.enabled ? World.WeatherQuads.LastShown : 0) + " of " + Weather.Count +
            " particles drawn; " + (World != null && World.CloudQuads != null && World.CloudsRenderer.enabled ? World.CloudQuads.LastShown : 0) + " of " + Clouds.Count +
            " clouds drawn; tent " + (World != null && World.TentRenderer.enabled ? "drawn" : "culled") +
            (World != null ? ", light " + World.Tint : "");

        string DescribeScripts()
        {
            var s = Scripts.For(PlayerId.One);
            return "console " + (s.ConsoleOpen ? "open '" + s.Line + "'" : "shut") + ", last '" + s.Last + "'" + (s.Running ? ", script running" : "") +
                   ", colour " + s.Colour + (Interface != null && Interface.For(PlayerId.One).OptionsOpen ? ", options open" : "") +
                   (Viewpoint != null ? ", camera (" + Viewpoint.Camera.X.ToString(CultureInfo.InvariantCulture) + ", " + Viewpoint.Camera.Y.ToString(CultureInfo.InvariantCulture) + ", " +
                                        Viewpoint.Camera.Z.ToString(CultureInfo.InvariantCulture) + ")" : "") +
                   (Sleep != null ? ", ambient " + Sleep.Ambient : "") + (Clock != null ? ", clock " + Clock.TimeText + " light " + Clock.Light : "");
        }

        string DescribeInventory()
        {
            var v = Inventory.For(PlayerId.One);
            return (v.Open ? "panel open" : "panel shut") + ", cursor " + v.Cursor + (v.Swapping ? ", swap marked " + v.SwapFrom : "") +
                   (v.Choosing ? ", choosing a target: " + Party.Heroes[v.Target].Name : "") + (v.QuickBar ? ", quick bar shown" : "") +
                   "; " + DescribePack() + "; drops [" + string.Join(", ", Pickups.Drops) + "]";
        }

        string DescribePack()
        {
            var inv = Pickups.Inventory;
            return "gold " + inv.Gold + ", weight " + inv.Weight.ToString(CultureInfo.InvariantCulture) + ", pack [" + string.Join(", ", inv.Items) + "]";
        }

        static string Describe(Hero h) => h.Name + " HP " + h.Health + "/" + h.MaxHealth + " MP " + h.Mana + "/" + h.MaxMana + " AP " + h.Stamina + "/" + h.MaxStamina + " " + h.Status;

        /// <summary>Reads <c>hour:minute[,seconds]</c>.</summary>
        public static bool TryClock(string text, out int hour, out int minute, out float? seconds)
        {
            hour = minute = 0;
            seconds = null;
            var parts = text.Split(',');
            if (parts.Length > 2) return false;
            var time = parts[0].Split(':');
            if (time.Length != 2 ||
                !int.TryParse(time[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hour) ||
                !int.TryParse(time[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out minute) ||
                hour < 0 || hour > 23 || minute < 0 || minute > 59) return false;
            if (parts.Length == 2)
            {
                if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) || !(s >= 0f)) return false;
                seconds = s;
            }

            return true;
        }

        /// <summary>The view keys <c>-overworld-view-hold</c> names (zoom, shift, x, z, num4, num6, num8, num2).</summary>
        public static ViewInput ScriptedViewKeys(string keys)
        {
            bool zoom = false, shift = false, iso = false, deiso = false, l = false, r = false, u = false, d = false;
            foreach (var raw in keys.Split(','))
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "zoom": zoom = true; break;
                    case "shift": shift = true; break;
                    case "x": iso = true; break;
                    case "z": deiso = true; break;
                    case "num4": l = true; break;
                    case "num6": r = true; break;
                    case "num8": u = true; break;
                    case "num2": d = true; break;
                    case "": break;
                    default: Debug.LogError("Unity.Overworld: " + ViewHoldArgument + " does not know the key '" + raw + "'."); break;
                }
            }

            return new ViewInput(0, 0, 0, zoom, shift, l, r, u, d, iso, deiso);
        }

        static bool TryPair(string text, out float x, out float y)
        {
            x = y = 0f;
            var parts = text.Split(',');
            return parts.Length == 2 &&
                   float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                   float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
        }

        static bool HasFlag(string[] args, string name)
        {
            foreach (var a in args)
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string Describe(MoveInput i) =>
            i.Move ? "move to (" + i.CursorX.ToString(CultureInfo.InvariantCulture) + ", " + i.CursorY.ToString(CultureInfo.InvariantCulture) + ")" + (i.Run ? " running" : "")
                   : "hold" + (i.Left ? " left" : "") + (i.Right ? " right" : "") + (i.Up ? " up" : "") + (i.Down ? " down" : "") + (i.Run ? " run" : "");

        IEnumerator CaptureAndQuit(string file, float seconds)
        {
            for (var i = 0; i < 10; i++) yield return null;
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(file, shot.EncodeToPNG());
                Debug.Log("Unity.Overworld: captured " + Path.GetFullPath(file));
                if (Party != null)
                    Debug.Log("Unity.Overworld: " + (Party.Team ? "team" : "solo") + ", leading " + Party.Leader(PlayerId.One).Name +
                              (TeamButton != null ? ", team button showing " + TeamButton.File : ""));
                if (Party != null)
                    foreach (var hero in Party.Heroes)
                    {
                        Debug.Log("Unity.Overworld: " + hero + " z " + hero.Z.ToString(CultureInfo.InvariantCulture) + " showing " + hero.Sprite);
                        Debug.Log("Unity.Overworld: " + Describe(hero) + (hero.StatusIcon != null ? " icon " + hero.StatusIcon : ""));
                    }
                if (Viewpoint != null)
                {
                    var cam = Viewpoint.Camera;
                    Debug.Log("Unity.Overworld: camera at (" + cam.X.ToString(CultureInfo.InvariantCulture) + ", " + cam.Y.ToString(CultureInfo.InvariantCulture) +
                              ", " + cam.Z.ToString(CultureInfo.InvariantCulture) + ")" + (Viewpoint.FreeCamera ? " free" : "") + (Viewpoint.Centring ? " centring" : ""));
                    Debug.Log("Unity.Overworld: " + Viewpoint.Cursor + " showing " + Viewpoint.Cursor.File);
                }

                if (Pickups != null)
                {
                    var selected = Pickups.Selected(PlayerId.One);
                    Debug.Log("Unity.Overworld: pickups in reach [" + string.Join(", ", Pickups.InReach(PlayerId.One)) + "], selected " +
                              (selected >= 0 ? Pickups.All[selected].ToString() : "none") + "; " + DescribePack() +
                              (Hud != null && Hud.Examined >= 0 ? "; examining " + Pickups.All[Hud.Examined] : "") +
                              (Hud != null && Hud.SelectedName != null && Hud.SelectedName.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex
                                  ? "; label '" + Hud.SelectedName.text + "'" : ""));
                    if (Inventory != null)
                        Debug.Log("Unity.Overworld: inventory " + DescribeInventory() + "; weight limit " + Inventory.WeightLimit.ToString(CultureInfo.InvariantCulture) +
                                  (Party.Leader(PlayerId.One).Burdened ? "; the leader is burdened" : ""));
                }

                if (Scripts != null)
                    Debug.Log("Unity.Overworld: scripts " + DescribeScripts() +
                              (Hud != null && Hud.ConsoleLabel != null && Hud.ConsoleLabel.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex
                                  ? ", prompt '" + Hud.ConsoleLabel.text + "'" : ""));

                if (Weather != null && Clouds != null) Debug.Log("Unity.Overworld: weather " + DescribeScenery());
                if (Interface != null) Debug.Log("Unity.Overworld: interface " + DescribeInterface());

                if (Party != null)
                    Debug.Log("Unity.Overworld: battle " + DescribeBattle() +
                              (Hud != null ? "; status box [" + string.Join(" | ", Hud.StatusLabels.Where(l => l.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex).Select(l => l.text)) + "]" : ""));

                if (Books != null)
                    Debug.Log("Unity.Overworld: book " + DescribeBook() +
                              (Hud != null && Books.For(PlayerId.One).Shown
                                  ? "; face " + Hud.FaceNameOf(Books.Screen.Lettering(Books.For(PlayerId.One).Lettering)) + "; cover " + Hud.BookPictureFile + " [" + string.Join(" | ", Hud.BookLabels.Select(l => l.text.Length > 40 ? l.text.Substring(0, 40).Replace("\n", "/") + "..." : l.text.Replace("\n", "/"))) + "]"
                                  : ""));

                if (Quests != null)
                    Debug.Log("Unity.Overworld: quests " + DescribeQuests() +
                              (Hud != null ? "; markers " + Hud.MarkerLabels.Count(l => l.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex) +
                                             (Quests.For(PlayerId.One).Open ? "; screen " + Hud.QuestPictureFile + " [" + string.Join(" | ", Hud.QuestLabels.Where(l => l.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex).Select(l => l.text)) + "]" : "")
                                  : ""));

                if (Clock != null)
                {
                    if (Sleep != null) Debug.Log("Unity.Overworld: ambient " + Sleep.Ambient + (Sleep.Sleeping ? " sleeping" : "") +
                                                 (Hud != null && Hud.RestLabel != null && Hud.RestLabel.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex
                                                     ? ", prompt '" + Hud.RestLabel.text + "'" : ""));
                    Debug.Log("Unity.Overworld: clock " + Clock.TimeText + " " + Clock.Period.Name + ", " + Clock.DateText + ", light " + Clock.Light);
                    foreach (var m in Messages.Shown) Debug.Log("Unity.Overworld: message " + m);
                }
            }
            finally
            {
                Destroy(shot);
            }

            Application.Quit(0);
        }

        static string ArgumentAfter(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}

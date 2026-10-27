using MindAttic.Overworld.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The player's controls (RECONSTRUCTION 5.1 to 5.4) as Input System actions built in code. The move controls: the
    /// arrow keys, the run key (Right Ctrl or Space) and the move button (the right mouse button) heading for the world
    /// cursor. The view controls: the mouse's motion (the system pointer is hidden and held, as the original's
    /// DirectInput mouse was, and its motion drives the world cursor), the wheel and the middle button (zoom), Shift (fast
    /// edge scroll), E (examine), F (free camera), X / Z (isometric) and numpad 4 / 6 / 8 / 2 (strafe). The party controls
    /// (RECONSTRUCTION 4.7, 5.1 and 13): [ and ] (the previous and next hero), ' (team / solo), and the left button (on a
    /// hero, it leads; on the team button, the team toggles). The rest controls (RECONSTRUCTION 6.4): \ (kneel, lie down,
    /// kneel again) and Enter or the left button while lying down (sleep). The pickup controls (RECONSTRUCTION 8.2):
    /// Enter or the left button (off the team button) held is the pick-up button, and , and . choose the previous and
    /// next item in reach (the wheel does too, through Core, when any is in reach). The inventory controls (RECONSTRUCTION
    /// 8.3, 8.5): Delete or a click on the inventory button opens and closes the pack, Backspace drops, Q shows the quick
    /// bar and 1 to 0 use its slots (the arrows, run key, Enter and right button act on the open pack in Core). Page Down opens and shuts the quest screen (RECONSTRUCTION 10; the arrows work it in Core). B shows and
    /// hides the book and N loads the next book (RECONSTRUCTION 11; the arrows and the wheel turn its pages in Core). W moves
    /// the weather on (RECONSTRUCTION 14.1; a frmMain key, so it works while a script runs, though not while the console is
    /// open, where it is typed). End enters and leaves battle mode with the team (RECONSTRUCTION
    /// 12.1; the U key's lone flip is a debug key, <see cref="ToggleHeroMode"/>, for the developer tools). The script
    /// console (RECONSTRUCTION 9.3): ~ opens it and Insert opens it with the last script; while it is open every key goes to
    /// it (<see cref="PressConsoleKey"/>, by the WinForms name the original tested), and while it is open or a script runs
    /// nothing else is read. The interface keys (U.OVW-US-A20, RECONSTRUCTION 5.1, 5.2, 13): F11 and Escape open and shut
    /// the options menu (the arrows and Enter, or the left button, work it in Core), F4 the FPS counter, Pause the pause
    /// (frmMain keys, read while a script runs but not while the console is open), Page Up the hero screen. While paused
    /// nothing else is read but the options menu's keys, so its Exit can still quit (BIBLE). Every frame, before the game advances, it sends what the
    /// player holds as <see cref="SetMoveInput"/> and <see cref="SetViewInput"/> commands and the key presses and clicks as
    /// commands; it never touches game state itself. The <c>Scripted*</c> inputs replace the devices for scripted runs.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HeroInput : MonoBehaviour
    {
        InputAction left, right, up, down, run, move, point;
        InputAction motion, wheel, zoom, fast, examine, freeCamera, isometric, deisometric, strafeLeft, strafeRight, strafeUp, strafeDown;
        InputAction previousHero, nextHero, team, select, rest, confirm, previousItem, nextItem;
        InputAction inventory, drop, quickBar, questScreen, book, nextBook, weather, battle;
        InputAction options, fps, pause, heroScreen;
        readonly InputAction[] quickKeys = new InputAction[10];
        Vector2? scriptedMouse;
        int scriptedWheelSent;

        // The original's overHero: once the held left button has been over a hero, that hero is chosen until it is let go.
        int overHero = -1;
        int chosen = -1;

        public Game Game { get; private set; }
        public PlayerId Player { get; private set; }
        public Camera Camera { get; private set; }

        /// <summary>The player's view (its world cursor is where the move button heads); null aims through the camera.</summary>
        public Viewpoint Viewpoint { get; private set; }

        /// <summary>Read the keyboard and mouse (and hide and hold the system pointer). Off for scripted runs.</summary>
        public bool UseDevices { get; set; } = true;

        /// <summary>When set, sent instead of what the devices hold (the scripted-run arguments).</summary>
        public MoveInput? Scripted { get; set; }

        /// <summary>Scripted runs: the virtual mouse is moved to this point of the 640 x 480 screen (y down) and held there.</summary>
        public Vector2? ScriptedPointer { get; set; }

        /// <summary>Scripted runs: this many wheel steps, one a frame (positive zooms in, negative out).</summary>
        public int ScriptedWheel { get; set; }

        /// <summary>Scripted runs: the view keys held (its motion and wheel are ignored).</summary>
        public ViewInput ScriptedKeys { get; set; }

        /// <summary>The last input sent.</summary>
        public MoveInput Last { get; private set; }

        public ViewInput LastView { get; private set; }

        /// <summary>The party, to find the hero under the cursor (null: clicks choose no hero).</summary>
        public Party Party { get; private set; }

        /// <summary>The team button the left button toggles (null: none).</summary>
        public TeamButtonDefinition TeamButton { get; private set; }

        public static HeroInput Create(Game game, PlayerId player, Camera camera, Viewpoint viewpoint = null, Party party = null, TeamButtonDefinition teamButton = null)
        {
            var go = new GameObject("HeroInput");
            var input = go.AddComponent<HeroInput>();
            input.Game = game;
            input.Player = player;
            input.Camera = camera;
            input.Viewpoint = viewpoint;
            input.Party = party;
            input.TeamButton = teamButton;
            return input;
        }

        void Awake()
        {
            left = Button("Left", "<Keyboard>/leftArrow");
            right = Button("Right", "<Keyboard>/rightArrow");
            up = Button("Up", "<Keyboard>/upArrow");
            down = Button("Down", "<Keyboard>/downArrow");
            run = Button("Run", "<Keyboard>/rightCtrl", "<Keyboard>/space");
            move = Button("Move", "<Mouse>/rightButton");
            point = new InputAction("Point", InputActionType.Value, "<Pointer>/position");

            motion = new InputAction("Motion", InputActionType.Value, "<Mouse>/delta");
            wheel = new InputAction("Wheel", InputActionType.Value, "<Mouse>/scroll/y");
            zoom = Button("Zoom", "<Mouse>/middleButton");
            fast = Button("Fast", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
            examine = Button("Examine", "<Keyboard>/e");
            freeCamera = Button("FreeCamera", "<Keyboard>/f");
            isometric = Button("Isometric", "<Keyboard>/x");
            deisometric = Button("Deisometric", "<Keyboard>/z");
            strafeLeft = Button("StrafeLeft", "<Keyboard>/numpad4");
            strafeRight = Button("StrafeRight", "<Keyboard>/numpad6");
            strafeUp = Button("StrafeUp", "<Keyboard>/numpad8");
            strafeDown = Button("StrafeDown", "<Keyboard>/numpad2");

            previousHero = Button("PreviousHero", "<Keyboard>/leftBracket");
            nextHero = Button("NextHero", "<Keyboard>/rightBracket");
            team = Button("Team", "<Keyboard>/quote");
            select = Button("Select", "<Mouse>/leftButton");
            rest = Button("Rest", "<Keyboard>/backslash");
            confirm = Button("Confirm", "<Keyboard>/enter", "<Keyboard>/numpadEnter");
            previousItem = Button("PreviousItem", "<Keyboard>/comma");
            nextItem = Button("NextItem", "<Keyboard>/period");
            inventory = Button("Inventory", "<Keyboard>/delete");
            drop = Button("Drop", "<Keyboard>/backspace");
            quickBar = Button("QuickBar", "<Keyboard>/q");
            questScreen = Button("QuestScreen", "<Keyboard>/pageDown");
            book = Button("Book", "<Keyboard>/b");
            nextBook = Button("NextBook", "<Keyboard>/n");
            weather = Button("Weather", "<Keyboard>/w");
            battle = Button("Battle", "<Keyboard>/end");
            options = Button("Options", "<Keyboard>/f11", "<Keyboard>/escape");
            fps = Button("Fps", "<Keyboard>/f4");
            pause = Button("Pause", "<Keyboard>/pause");
            heroScreen = Button("HeroScreen", "<Keyboard>/pageUp");
            // 1 to 9 then 0: quick slots 0 to 9.
            for (var i = 0; i < quickKeys.Length; i++) quickKeys[i] = Button("Quick" + (i + 1) % 10, "<Keyboard>/digit" + (i + 1) % 10);
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            return action;
        }

        void OnEnable()
        {
            foreach (var a in Actions()) a?.Enable();
        }

        void OnDisable()
        {
            foreach (var a in Actions()) a?.Disable();
            ReleasePointer();
        }

        void OnDestroy()
        {
            foreach (var a in Actions()) a?.Dispose();
        }

        InputAction[] Actions() => new[]
        {
            left, right, up, down, run, move, point,
            motion, wheel, zoom, fast, examine, freeCamera, isometric, deisometric, strafeLeft, strafeRight, strafeUp, strafeDown,
            previousHero, nextHero, team, select, rest, confirm, previousItem, nextItem, inventory, drop, quickBar, questScreen, book, nextBook, weather, battle,
            options, fps, pause, heroScreen,
            quickKeys[0], quickKeys[1], quickKeys[2], quickKeys[3], quickKeys[4], quickKeys[5], quickKeys[6], quickKeys[7], quickKeys[8], quickKeys[9],
        };

        /// <summary>The options menu, hero screen, FPS counter and pause (null: none).</summary>
        public InterfaceControl Interface { get; set; }

        /// <summary>
        /// With the options menu open: Enter or the left button (actionPickup), then Left, Up and Down (actionLeft, actionUp,
        /// actionDown, each once a press) work it.
        /// </summary>
        void OptionsKeys()
        {
            if (Interface == null || !Interface.For(Player).OptionsOpen) return;
            if (confirm.WasPressedThisFrame() || select.WasPressedThisFrame()) Game.Submit(new PressOptionsKey(Player, OptionsKey.Enter));
            if (left.WasPressedThisFrame()) Game.Submit(new PressOptionsKey(Player, OptionsKey.Left));
            if (up.WasPressedThisFrame()) Game.Submit(new PressOptionsKey(Player, OptionsKey.Up));
            if (down.WasPressedThisFrame()) Game.Submit(new PressOptionsKey(Player, OptionsKey.Down));
        }

        /// <summary>The script console and its input lock (null: no console).</summary>
        public ScriptInterpreter Scripts { get; set; }

        // Enter or the left button that was down while the input was locked (Enter runs a console line and shuts it) acts
        // only once it has been let go, so the line's Enter does not also kneel the leader to pick up.
        bool swallowConfirm;

        void Update()
        {
            if (Game == null) return;
            if (UseDevices) HoldPointer();

            // frmMain took every key while the console was open, and getInput was not called while it was or a script ran.
            var locked = Scripts != null && Scripts.InputLocked(Player);
            // W was frmMain's KeyDown with the console shut: it moved the weather on even while a script ran getInput dry.
            var consoleWasOpen = Scripts != null && Scripts.For(Player).ConsoleOpen;
            if (UseDevices && Scripts != null) SendConsoleKeys();
            if (UseDevices && !consoleWasOpen && weather.WasPressedThisFrame()) Game.Submit(new CycleWeather(Player));
            // F11 (and Escape), F4 and Pause were frmMain keys too.
            if (UseDevices && !consoleWasOpen && Interface != null)
            {
                if (options.WasPressedThisFrame()) Game.Submit(new ToggleOptions(Player));
                if (fps.WasPressedThisFrame()) Game.Submit(new ToggleFps(Player));
                if (pause.WasPressedThisFrame()) Game.Submit(new TogglePause(Player));
            }

            // While paused getInput was not called; the options menu still answers, so Exit can quit from a pause.
            var paused = Interface != null && Interface.Paused;
            if (paused && !locked && UseDevices) OptionsKeys();
            if (locked || paused)
            {
                swallowConfirm = true;
                Last = default;
                Game.Submit(new SetMoveInput(Player, default));
                if (Viewpoint != null) Game.Submit(new SetViewInput(Player, default));
                return;
            }

            if (swallowConfirm && UseDevices && !confirm.IsPressed() && !select.IsPressed()) swallowConfirm = false;
            if (!UseDevices) swallowConfirm = false;

            var input = Scripted ?? (UseDevices ? Read() : default);
            Last = input;
            Game.Submit(new SetMoveInput(Player, input));

            if (Viewpoint == null) return;
            var view = UseDevices ? ReadView() : ScriptedView();
            LastView = view;
            Game.Submit(new SetViewInput(Player, view));
            if (UseDevices && examine.WasPressedThisFrame()) Game.Submit(new ToggleExamine(Player));
            if (UseDevices && freeCamera.WasPressedThisFrame()) Game.Submit(new ToggleFreeCamera(Player));
            if (!UseDevices) return;

            if (previousHero.WasPressedThisFrame()) Game.Submit(new CycleLeader(Player, -1));
            if (nextHero.WasPressedThisFrame()) Game.Submit(new CycleLeader(Player, 1));
            if (team.WasPressedThisFrame()) Game.Submit(new ToggleTeam(Player));
            if (rest.WasPressedThisFrame()) Game.Submit(new Rest(Player));
            if (previousItem.WasPressedThisFrame()) Game.Submit(new CyclePickup(Player, -1));
            if (nextItem.WasPressedThisFrame()) Game.Submit(new CyclePickup(Player, 1));
            if (inventory.WasPressedThisFrame()) Game.Submit(new ToggleInventory(Player));
            if (drop.WasPressedThisFrame()) Game.Submit(new DropItem(Player));
            if (quickBar.WasPressedThisFrame()) Game.Submit(new ToggleQuickBar(Player));
            if (questScreen.WasPressedThisFrame()) Game.Submit(new ToggleQuestScreen(Player));
            if (battle.WasPressedThisFrame()) Game.Submit(new ToggleBattle(Player));
            if (heroScreen.WasPressedThisFrame() && Interface != null) Game.Submit(new ToggleHeroScreen(Player));
            OptionsKeys();
            if (book.WasPressedThisFrame()) Game.Submit(new ToggleBook(Player));
            if (nextBook.WasPressedThisFrame()) Game.Submit(new NextBook(Player));
            for (var i = 0; i < quickKeys.Length; i++)
                if (quickKeys[i].WasPressedThisFrame()) Game.Submit(new QuickUse(Player, i));
            // getMouse's middle button in battle mode is actionDrop: the battle's cancel back to the ring.
            if (zoom.WasPressedThisFrame() && Party != null && Party.Leader(Player).Mode == HeroMode.Battle) Game.Submit(new DropItem(Player));
            if (swallowConfirm) return;
            Confirm(confirm.WasPressedThisFrame() || select.WasPressedThisFrame());
            ChooseFromRing(confirm.WasPressedThisFrame(), select.WasPressedThisFrame());
            Click(select.IsPressed(), select.WasPressedThisFrame());
        }

        /// <summary>
        /// The keys pressed this frame, by the WinForms name frmMain tested, for the console: every key while it is open,
        /// and its open and recall keys while it is shut. Shift is read as it is now.
        /// </summary>
        void SendConsoleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var console = Scripts.Data.Console;
            var open = Scripts.For(Player).ConsoleOpen;
            var shift = keyboard.shiftKey.isPressed;
            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame) continue;
                var name = ConsoleKeyName(control.keyCode);
                if (name == null) continue;
                if (!open && name != console.Open && name != console.Recall) continue;
                Game.Submit(new PressConsoleKey(Player, name, shift));
                open = Scripts.For(Player).ConsoleOpen;
            }
        }

        /// <summary>The WinForms key name (System.Windows.Forms.Keys) the original's KeyDown saw for <paramref name="key"/>, or null.</summary>
        public static string ConsoleKeyName(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return ((char)('A' + (key - Key.A))).ToString();
            if (key >= Key.Digit1 && key <= Key.Digit9) return "D" + (1 + (key - Key.Digit1));
            if (key >= Key.Numpad0 && key <= Key.Numpad9) return "NumPad" + (key - Key.Numpad0);
            if (key >= Key.F1 && key <= Key.F12) return "F" + (1 + (key - Key.F1));
            switch (key)
            {
                case Key.Digit0: return "D0";
                case Key.Space: return "Space";
                case Key.Enter: case Key.NumpadEnter: return "Enter";
                case Key.Tab: return "Tab";
                case Key.Backquote: return "Oemtilde";
                case Key.Quote: return "OemQuotes";
                case Key.Semicolon: return "OemSemicolon";
                case Key.Comma: return "Oemcomma";
                case Key.Period: return "OemPeriod";
                case Key.Slash: return "OemQuestion";
                case Key.Backslash: return "OemPipe";
                case Key.LeftBracket: return "OemOpenBrackets";
                case Key.RightBracket: return "OemCloseBrackets";
                case Key.Minus: return "OemMinus";
                case Key.Equals: return "Oemplus";
                case Key.Backspace: return "Back";
                case Key.Escape: return "Escape";
                case Key.Insert: return "Insert";
                case Key.Delete: return "Delete";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "PageUp";
                case Key.PageDown: return "Next";
                case Key.LeftArrow: return "Left";
                case Key.RightArrow: return "Right";
                case Key.UpArrow: return "Up";
                case Key.DownArrow: return "Down";
                case Key.LeftShift: case Key.RightShift: return "ShiftKey";
                case Key.LeftCtrl: case Key.RightCtrl: return "ControlKey";
                case Key.LeftAlt: case Key.RightAlt: return "Menu";
                case Key.CapsLock: return "Capital";
                case Key.NumLock: return "NumLock";
                case Key.ScrollLock: return "Scroll";
                case Key.PrintScreen: return "PrintScreen";
                case Key.Pause: return "Pause";
                case Key.NumpadPlus: return "Add";
                case Key.NumpadMinus: return "Subtract";
                case Key.NumpadMultiply: return "Multiply";
                case Key.NumpadDivide: return "Divide";
                case Key.NumpadPeriod: return "Decimal";
                case Key.LeftMeta: return "LWin";
                case Key.RightMeta: return "RWin";
                case Key.ContextMenu: return "Apps";
                default: return null;
            }
        }

        /// <summary>
        /// Enter or the left button (actionPickup's "Initiate Sleep"): pressed while the hero this player leads lies down,
        /// the party sleeps the hours chosen.
        /// </summary>
        public void Confirm(bool pressed)
        {
            if (!pressed || Party == null) return;
            // With the pack open or an item's target being chosen, Enter uses (Core reads it from the held button).
            if (Inventory != null && Inventory.FocusOf(Player) != InputFocus.World) return;
            // actionPickup's sleep was inside its "Not bOptions" test.
            if (Interface != null && Interface.For(Player).OptionsOpen) return;
            if (Party.Heroes[Party.LeaderOf(Player)].Laying) Game.Submit(new Sleep(Player));
        }

        /// <summary>
        /// actionPickup's actionRingSelect: Enter, or the left button off a hero (overHero) and off the team and inventory
        /// buttons, pressed with the battle's ring menu open chooses its top entry. Pressed on a hero, the left button chooses
        /// the hero instead (<see cref="Click"/>).
        /// </summary>
        public void ChooseFromRing(bool enter, bool leftButton)
        {
            if (Party == null || !Party.RingMenuOpen(Player)) return;
            if (!enter && !(leftButton && PickUpHeld(false, true))) return;
            if (Viewpoint != null && select.IsPressed() && Party.HeroAt(Viewpoint.Cursor.X, Viewpoint.Cursor.Y) >= 0) return;
            Game.Submit(new Core.ChooseFromRing(Player));
        }

        /// <summary>The party's pack: what the keys work on, and the inventory button the left button toggles (null: none).</summary>
        public InventoryControl Inventory { get; set; }

        /// <summary>The inventory button (null: none).</summary>
        public ScreenButtonDefinition InventoryButton { get; set; }

        bool OverInventoryButton => InventoryButton != null && Viewpoint != null && InventoryButton.Contains(Viewpoint.Cursor.MouseX, Viewpoint.Cursor.MouseY);

        /// <summary>
        /// The left button (getMouse's actionHeroSelect and actionPickup): held over a hero, that hero leads (and stays the
        /// one chosen until the button is let go, as the original's overHero did); pressed over the team button, the team
        /// toggles (once a press, bUseDown).
        /// </summary>
        public void Click(bool held, bool pressed)
        {
            if (!held || Viewpoint == null)
            {
                overHero = -1;
                chosen = -1;
                return;
            }

            var cursor = Viewpoint.Cursor;
            if (Party != null)
            {
                var at = Party.HeroAt(cursor.X, cursor.Y);
                if (at >= 0) overHero = at;
                if (overHero >= 0 && overHero != chosen)
                {
                    chosen = overHero;
                    Game.Submit(new SelectHero(Player, overHero));
                }
            }

            if (pressed && TeamButton != null && TeamButton.Contains(cursor.MouseX, cursor.MouseY))
                Game.Submit(new ToggleTeam(Player));
            if (pressed && OverInventoryButton) Game.Submit(new ToggleInventory(Player));
        }

        MoveInput Read()
        {
            var cursor = Vector2.zero;
            var held = move.IsPressed();
            if (held)
            {
                if (Viewpoint != null) cursor = new Vector2(Viewpoint.Cursor.X, Viewpoint.Cursor.Y);
                else if (Camera != null) cursor = GroundPoint(Camera, point.ReadValue<Vector2>());
            }

            return new MoveInput(left.IsPressed(), right.IsPressed(), up.IsPressed(), down.IsPressed(), run.IsPressed(), held, cursor.x, cursor.y,
                !swallowConfirm && PickUpHeld(confirm.IsPressed(), select.IsPressed()));
        }

        /// <summary>
        /// actionPickup's buttons: Enter, or the left button unless it is over the team button (overTeam) or the inventory
        /// button (a click on a button is only the button's: the original also used the panel's item, or knelt, under it;
        /// BIBLE). (A left click on a hero both chooses it and kneels it to pick up, as getMouse called actionHeroSelect
        /// then actionPickup.)
        /// </summary>
        public bool PickUpHeld(bool enter, bool leftButton)
        {
            if (enter) return true;
            if (!leftButton) return false;
            if (TeamButton != null && Viewpoint != null && TeamButton.Contains(Viewpoint.Cursor.MouseX, Viewpoint.Cursor.MouseY)) return false;
            if (OverInventoryButton) return false;
            return true;
        }

        ViewInput ReadView()
        {
            // The pointer's motion in this window's pixels, as virtual 640 x 480 pixels with y down.
            var d = motion.ReadValue<Vector2>();
            var def = Viewpoint.Cursor.Definition;
            var dx = Screen.width > 0 ? d.x * def.ScreenWidth / Screen.width : 0f;
            var dy = Screen.height > 0 ? -d.y * def.ScreenHeight / Screen.height : 0f;
            if (!Application.isFocused) dx = dy = 0f;
            var w = wheel.ReadValue<float>();
            return new ViewInput(dx, dy, w > 0f ? 1 : w < 0f ? -1 : 0, zoom.IsPressed(), fast.IsPressed(),
                strafeLeft.IsPressed(), strafeRight.IsPressed(), strafeUp.IsPressed(), strafeDown.IsPressed(),
                isometric.IsPressed(), deisometric.IsPressed());
        }

        ViewInput ScriptedView()
        {
            float dx = 0f, dy = 0f;
            if (ScriptedPointer.HasValue)
            {
                // Sent once: the motion that takes the virtual mouse from where it started to the point.
                var from = scriptedMouse ?? new Vector2(Viewpoint.Cursor.MouseX, Viewpoint.Cursor.MouseY);
                dx = ScriptedPointer.Value.x - from.x;
                dy = ScriptedPointer.Value.y - from.y;
                scriptedMouse = ScriptedPointer.Value;
            }

            var step = 0;
            if (scriptedWheelSent < Mathf.Abs(ScriptedWheel))
            {
                step = ScriptedWheel > 0 ? 1 : -1;
                scriptedWheelSent++;
            }

            var k = ScriptedKeys;
            return new ViewInput(dx, dy, step, k.Zoom, k.Fast, k.StrafeLeft, k.StrafeRight, k.StrafeUp, k.StrafeDown, k.Isometric, k.Deisometric);
        }

        /// <summary>The original hid the system pointer and read the mouse's raw motion: the pointer is hidden and held while the window has focus.</summary>
        void HoldPointer()
        {
            if (!Application.isPlaying || Application.isBatchMode) return;
            var want = Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None;
            if (UnityEngine.Cursor.lockState != want) UnityEngine.Cursor.lockState = want;
            UnityEngine.Cursor.visible = !Application.isFocused;
        }

        void ReleasePointer()
        {
            if (!Application.isPlaying || !UseDevices) return;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        /// <summary>Where the ray through <paramref name="screen"/> meets the world's plane (z 0), in world units.</summary>
        public static Vector2 GroundPoint(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-6f) return new Vector2(ray.origin.x, ray.origin.y);
            var t = -ray.origin.z / ray.direction.z;
            var p = ray.origin + ray.direction * t;
            return new Vector2(p.x, p.y);
        }
    }
}

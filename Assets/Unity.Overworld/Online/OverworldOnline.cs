using MindAttic.Components.Unity;
using MindAttic.Components.Unity.Core;
using MindAttic.Overworld.Runtime;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Overworld.Online
{
    /// <summary>
    /// Online play beside local play (U.OVW-T46), on the Unity.Multiplayer kit's drop-in front end. The game opens
    /// exactly as before, straight into local play, with the kit's menus hidden; O (the rebuild's key: the original had
    /// no online play) opens its main menu: Host, Join by IP, Join by code, Settings, Play alone and Quit. Hosting shows
    /// the kit's lobby (the join code and the roster) and Start begins the online game; joining forwards this screen's
    /// player commands to the host (<see cref="OverworldPlayer"/>); while a session runs Escape opens the kit's in-game
    /// menu instead of the options menu (F11 still opens the options).
    /// <para>While a menu is open it holds the game's controls back (<see cref="HeroInput.Hold"/>), frees the pointer
    /// for the mouse, and, on the main menu outside a session, holds the local game still
    /// (<see cref="GameHost.Suspended"/>). The server spawns the world sync (<see cref="OverworldWorldSync"/>) when its
    /// session starts, a dedicated server included.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)] // after HeroInput (-100) has read this frame, before the game advances
    public sealed class OverworldOnline : MonoBehaviour
    {
        /// <summary>The main menu's button that closes the menus and goes back to local play.</summary>
        public const string PlayAloneLabel = "Play alone";

        /// <summary>The key that opens the online menu over local play.</summary>
        public const string MenuBinding = "<Keyboard>/o";

        [SerializeField] private NetFrontEnd frontEnd;
        [SerializeField] private OverworldBootstrap game;
        [Tooltip("The OverworldWorldSync prefab (a NetworkObject, a NetKey and OverworldWorldSync) the server spawns for each session.")]
        [SerializeField] private GameObject worldSyncPrefab;

        private InputAction _menuKey;
        private HeroInput _wiredInput;
        private bool _cursorFreed;
        private CursorLockMode _savedLock;
        private bool _savedVisible;

        public NetFrontEnd FrontEnd
        {
            get => frontEnd;
            set => frontEnd = value;
        }

        public OverworldBootstrap Game
        {
            get => game;
            set => game = value;
        }

        public GameObject WorldSyncPrefab
        {
            get => worldSyncPrefab;
            set => worldSyncPrefab = value;
        }

        /// <summary>Whether one of the kit's menus is open on this screen (the game's controls are held back).</summary>
        public bool MenuOpen { get; private set; }

        /// <summary>Whether this screen is in an online session (hosting, joined, or connecting).</summary>
        public bool InSession => frontEnd != null && frontEnd.State != FrontEndState.Menu;

        /// <summary>Opens the kit's main menu over local play (what O does).</summary>
        public void OpenMenu()
        {
            if (frontEnd == null || InSession) return;
            frontEnd.MenusVisible = true;
            Reconcile();
        }

        /// <summary>Closes the main menu and goes back to local play (the menu's Play alone).</summary>
        public void CloseMenu()
        {
            if (frontEnd == null || InSession) return;
            frontEnd.MenusVisible = false;
            Reconcile();
        }

        private void Awake()
        {
            if (frontEnd == null) frontEnd = GetComponent<NetFrontEnd>();
            if (game == null) game = FindAnyObjectByType<OverworldBootstrap>();
            if (frontEnd == null) return;

            // Local play first, exactly as before: the menus stay hidden until O opens them.
            frontEnd.MenusVisible = false;
            frontEnd.AddMainMenuItems(s => s.AddButton(PlayAloneLabel, CloseMenu, ButtonVariant.Primary));
            frontEnd.SessionStarted += OnSessionStarted;
            frontEnd.Left += OnLeft;
            frontEnd.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (frontEnd != null)
            {
                frontEnd.SessionStarted -= OnSessionStarted;
                frontEnd.Left -= OnLeft;
                frontEnd.StateChanged -= OnStateChanged;
            }

            _menuKey?.Dispose();
            _menuKey = null;
        }

        private void OnEnable()
        {
            _menuKey ??= new InputAction("OnlineMenu", InputActionType.Button, MenuBinding);
            _menuKey.Enable();
        }

        private void OnDisable()
        {
            _menuKey?.Disable();
            if (game != null && game.Host != null) game.Host.Suspended = false;
            RestoreCursor();
        }

        private void Update()
        {
            WireInput();

            // O opens the menu over local play. Closing is the menu's own Play alone, so typing an "o" into a field never
            // closes it; the console takes every key while it is open, so an "o" typed there never opens it.
            if (_menuKey != null && _menuKey.WasPressedThisFrame() && !MenuOpen && !ConsoleOpen()) OpenMenu();

            Reconcile();
        }

        /// <summary>Gives the scene's controls the menus' hold and the session's Escape, once the bootstrap has made them.</summary>
        private void WireInput()
        {
            var input = game != null ? game.Input : null;
            if (input == null || input == _wiredInput) return;
            input.Hold = () => MenuOpen;
            input.EscapeTaken = () => InSession;
            _wiredInput = input;
        }

        private bool ConsoleOpen() =>
            game != null && game.Scripts != null && game.Scripts.For(OverworldPeer.LocalPlayer).ConsoleOpen;

        private void OnSessionStarted(NetSession session)
        {
            // A headless server (-mode server) is paced to the tick rate once it serves, not at launch, so a headless
            // local run (batch-mode captures) keeps the frame pacing it always had.
            if (session.IsServer && HeadlessBootstrap.IsHeadless && NetBootstrap.Instance != null)
                HeadlessBootstrap.Apply(NetBootstrap.Instance.BuildConfig().TickRate);

            if (session.IsServer && worldSyncPrefab != null) OverworldWorldSync.Serve(session, worldSyncPrefab);
        }

        private void OnLeft(Disconnection why)
        {
            // Back from someone else's game: this screen's own game plays on locally from where it was.
            var host = game != null ? game.Host : null;
            if (host != null && host.Game != null && host.Game.IsRemote) host.Game.Forward = null;
        }

        private void OnStateChanged(FrontEndState state) => Reconcile();

        /// <summary>Sets the controls' hold, the pointer and the local game's hold from what the front end shows now.</summary>
        private void Reconcile()
        {
            var open = frontEnd != null && frontEnd.HasMenus
                && ((frontEnd.MenusVisible && frontEnd.MainMenu != null && frontEnd.MainMenu.IsOpen)
                    || (frontEnd.PauseMenu != null && frontEnd.PauseMenu.gameObject.activeInHierarchy && frontEnd.PauseMenu.IsOpen));
            MenuOpen = open;

            if (open) FreeCursor();
            else RestoreCursor();

            // Hold the local game still behind the main menu; never online, where the authority's game runs on.
            var host = game != null ? game.Host : null;
            if (host != null) host.Suspended = open && !InSession;
        }

        private void FreeCursor()
        {
            if (_cursorFreed) return;
            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorFreed = true;
        }

        private void RestoreCursor()
        {
            if (!_cursorFreed) return;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
            _cursorFreed = false;
        }
    }
}

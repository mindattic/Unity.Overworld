using MindAttic.Overworld.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The original's move controls (RECONSTRUCTION 5.1 and 5.4) as Input System actions built in code: the arrow keys,
    /// the run key (Right Ctrl or Space) and the move button (the right mouse button), with the pointer turned into a
    /// world cursor on the ground plane. Every frame, before the game advances, it sends what the player holds as a
    /// <see cref="SetMoveInput"/> command; it never touches game state itself. A <see cref="Scripted"/> input replaces the
    /// devices for scripted runs.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HeroInput : MonoBehaviour
    {
        InputAction left, right, up, down, run, move, point;

        public Game Game { get; private set; }
        public PlayerId Player { get; private set; }
        public Camera Camera { get; private set; }

        /// <summary>When set, sent instead of what the devices hold (the scripted-run arguments).</summary>
        public MoveInput? Scripted { get; set; }

        /// <summary>The last input sent.</summary>
        public MoveInput Last { get; private set; }

        public static HeroInput Create(Game game, PlayerId player, Camera camera)
        {
            var go = new GameObject("HeroInput");
            var input = go.AddComponent<HeroInput>();
            input.Game = game;
            input.Player = player;
            input.Camera = camera;
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
        }

        void OnDestroy()
        {
            foreach (var a in Actions()) a?.Dispose();
        }

        InputAction[] Actions() => new[] { left, right, up, down, run, move, point };

        void Update()
        {
            if (Game == null) return;
            var input = Scripted ?? Read();
            Last = input;
            Game.Submit(new SetMoveInput(Player, input));
        }

        MoveInput Read()
        {
            var cursor = Vector2.zero;
            var held = move.IsPressed();
            if (held && Camera != null) cursor = GroundPoint(Camera, point.ReadValue<Vector2>());
            return new MoveInput(left.IsPressed(), right.IsPressed(), up.IsPressed(), down.IsPressed(), run.IsPressed(), held, cursor.x, cursor.y);
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

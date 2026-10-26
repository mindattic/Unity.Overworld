using System;
using System.Collections;
using System.Globalization;
using System.IO;
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
    /// <c>-overworld-capture &lt;file.png&gt;</c> saves a screenshot and quits, after a few frames or after
    /// <c>-overworld-capture-after &lt;seconds&gt;</c> of play. Any scripted argument (or a capture) leaves the real
    /// keyboard and mouse alone, so the system pointer is never taken.
    /// </summary>
    public sealed class OverworldBootstrap : MonoBehaviour
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

        public ContentRegistry Content { get; private set; }
        public GameHost Host { get; private set; }
        public Party Party { get; private set; }
        public CameraControl Control { get; private set; }
        public Viewpoint Viewpoint { get; private set; }
        public WorldViewPresenter World { get; private set; }
        public HeroInput Input { get; private set; }

        /// <summary>The content folder of this build (or of the project, in the editor).</summary>
        public static string ContentDirectory => Path.Combine(Application.streamingAssetsPath, "Content");

        void Start()
        {
            Content = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory));
            var game = new Game();
            Party = new Party(Content.Heroes).Install(game);
            Control = new CameraControl(Content.View, Party).Install(game);
            var joined = game.Submit(new JoinGame(PlayerId.One));
            if (!joined.Accepted) Debug.LogError("Unity.Overworld: player 1 could not join: " + joined.Reason);
            Viewpoint = Control.For(PlayerId.One);

            Host = GameHost.Create(game);
            Host.transform.SetParent(transform, false);
            World = WorldViewPresenter.Create(new WorldView(Content.View, Content.Props, Content.Heroes, Viewpoint.Camera));
            World.transform.SetParent(transform, false);
            World.Follow(Party, game);
            World.ShowCursor(Viewpoint.Cursor);
            Input = HeroInput.Create(game, PlayerId.One, World.Camera, Viewpoint);
            Input.transform.SetParent(transform, false);

            var args = Environment.GetCommandLineArgs();
            var scripted = ScriptedInput(args);
            if (scripted.HasValue)
            {
                Input.Scripted = scripted;
                Debug.Log("Unity.Overworld: scripted input " + Describe(scripted.Value));
            }

            var capture = ArgumentAfter(args, CaptureArgument);
            var scriptedView = ApplyScriptedView(args, game);
            if (scripted.HasValue || scriptedView || capture != null) Input.UseDevices = false;

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
                    foreach (var hero in Party.Heroes) Debug.Log("Unity.Overworld: " + hero + " showing " + hero.Sprite);
                if (Viewpoint != null)
                {
                    var cam = Viewpoint.Camera;
                    Debug.Log("Unity.Overworld: camera at (" + cam.X.ToString(CultureInfo.InvariantCulture) + ", " + cam.Y.ToString(CultureInfo.InvariantCulture) +
                              ", " + cam.Z.ToString(CultureInfo.InvariantCulture) + ")" + (Viewpoint.FreeCamera ? " free" : "") + (Viewpoint.Centring ? " centring" : ""));
                    Debug.Log("Unity.Overworld: " + Viewpoint.Cursor + " showing " + Viewpoint.Cursor.File);
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

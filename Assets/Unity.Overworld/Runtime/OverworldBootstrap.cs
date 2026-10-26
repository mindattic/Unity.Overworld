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
    /// its party (player 1 joined, leading Chrono) and its host, builds the world view following the party, and reads the
    /// player's controls with <see cref="HeroInput"/>, all in code.
    /// Scripted runs (the check that the hero moves, without a hand on the keyboard):
    /// <c>-overworld-hold &lt;left,right,up,down,run&gt;</c> holds those keys; <c>-overworld-move-to &lt;x,y[,run]&gt;</c>
    /// holds the move button with the world cursor at (x, y); <c>-overworld-capture &lt;file.png&gt;</c> saves a screenshot
    /// and quits, after a few frames or after <c>-overworld-capture-after &lt;seconds&gt;</c> of play.
    /// </summary>
    public sealed class OverworldBootstrap : MonoBehaviour
    {
        public const string CaptureArgument = "-overworld-capture";
        public const string CaptureAfterArgument = "-overworld-capture-after";
        public const string HoldArgument = "-overworld-hold";
        public const string MoveToArgument = "-overworld-move-to";

        public ContentRegistry Content { get; private set; }
        public GameHost Host { get; private set; }
        public Party Party { get; private set; }
        public WorldViewPresenter World { get; private set; }
        public HeroInput Input { get; private set; }

        /// <summary>The content folder of this build (or of the project, in the editor).</summary>
        public static string ContentDirectory => Path.Combine(Application.streamingAssetsPath, "Content");

        void Start()
        {
            Content = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory));
            var game = new Game();
            Party = new Party(Content.Heroes).Install(game);
            var joined = game.Submit(new JoinGame(PlayerId.One));
            if (!joined.Accepted) Debug.LogError("Unity.Overworld: player 1 could not join: " + joined.Reason);

            Host = GameHost.Create(game);
            Host.transform.SetParent(transform, false);
            World = WorldViewPresenter.Create(new WorldView(Content.View, Content.Props, Content.Heroes));
            World.transform.SetParent(transform, false);
            World.Follow(Party, game);
            Input = HeroInput.Create(game, PlayerId.One, World.Camera);
            Input.transform.SetParent(transform, false);

            var args = Environment.GetCommandLineArgs();
            var scripted = ScriptedInput(args);
            if (scripted.HasValue)
            {
                Input.Scripted = scripted;
                Debug.Log("Unity.Overworld: scripted input " + Describe(scripted.Value));
            }

            var capture = ArgumentAfter(args, CaptureArgument);
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

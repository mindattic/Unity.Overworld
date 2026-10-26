using System;
using System.Collections;
using System.IO;
using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The one object in the near-empty main scene: it loads the content from StreamingAssets/Content, creates the game
    /// and its host, and builds the world view, all in code. Launched with <c>-overworld-capture &lt;file.png&gt;</c> it
    /// saves a screenshot after a few frames and quits (the scripted check that the build launches and draws the world).
    /// </summary>
    public sealed class OverworldBootstrap : MonoBehaviour
    {
        public const string CaptureArgument = "-overworld-capture";

        public ContentRegistry Content { get; private set; }
        public GameHost Host { get; private set; }
        public WorldViewPresenter World { get; private set; }

        /// <summary>The content folder of this build (or of the project, in the editor).</summary>
        public static string ContentDirectory => Path.Combine(Application.streamingAssetsPath, "Content");

        void Start()
        {
            Content = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory));
            Host = GameHost.Create(new Game());
            Host.transform.SetParent(transform, false);
            World = WorldViewPresenter.Create(new WorldView(Content.View, Content.Props, Content.Heroes));
            World.transform.SetParent(transform, false);

            var capture = ArgumentAfter(CaptureArgument);
            if (capture != null) StartCoroutine(CaptureAndQuit(capture));
        }

        static IEnumerator CaptureAndQuit(string file)
        {
            for (var i = 0; i < 10; i++) yield return null;
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(file, shot.EncodeToPNG());
                Debug.Log("Unity.Overworld: captured " + Path.GetFullPath(file));
            }
            finally
            {
                Destroy(shot);
            }

            Application.Quit(0);
        }

        static string ArgumentAfter(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}

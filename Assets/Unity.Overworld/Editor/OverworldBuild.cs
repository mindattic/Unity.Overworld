using System;
using System.IO;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MindAttic.Overworld.Editor
{
    /// <summary>
    /// Everything the project needs made, made in code: the near-empty main scene (one bootstrap object), the Windows
    /// player, and the world-view screenshots. Each runs from the Overworld menu or headless with -executeMethod.
    /// </summary>
    public static class OverworldBuild
    {
        public const string MainScene = "Assets/Unity.Overworld/Scenes/Main.unity";
        public const string PlayerPath = "Build/Overworld/Overworld.exe";

        [MenuItem("Overworld/Build Main Scene")]
        public static void BuildMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Overworld").AddComponent<OverworldBootstrap>();
            EditorSceneManager.SaveScene(scene, MainScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Unity.Overworld: built " + MainScene);
        }

        [MenuItem("Overworld/Build Windows Player")]
        public static void BuildPlayer()
        {
            BuildMainScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("Unity.Overworld: player build " + report.summary.result + " (" + PlayerPath + ")");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Renders the world view at 640 x 480 (the original's window) at the nearest and farthest zoom into
        /// Logs/world-view-near.png and Logs/world-view-far.png.
        /// </summary>
        [MenuItem("Overworld/Capture World View")]
        public static void CaptureWorldView()
        {
            var exit = 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
                var presenter = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
                Directory.CreateDirectory("Logs");
                Capture(presenter, presenter.View.Camera.Definition.NearestZ, "Logs/world-view-near.png");
                Capture(presenter, presenter.View.Camera.Definition.FarthestZ, "Logs/world-view-far.png");
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                exit = 1;
            }

            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        static void Capture(WorldViewPresenter presenter, float z, string file)
        {
            var cam = presenter.View.Camera;
            cam.MoveTo(cam.X, cam.Y, z);
            presenter.Refresh();
            var target = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                presenter.Camera.aspect = 640f / 480f;
                presenter.Camera.targetTexture = target;
                presenter.Camera.Render();
                RenderTexture.active = target;
                var shot = new Texture2D(640, 480, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                shot.Apply();
                File.WriteAllBytes(file, shot.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(shot);
                Debug.Log("Unity.Overworld: captured " + Path.GetFullPath(file));
            }
            finally
            {
                presenter.Camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

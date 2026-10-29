using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A17 in Unity: the tent, the weather and the clouds drawn as Core says, in the original's layers and light.
    public class SceneryPresenterTests
    {
        ContentRegistry content;
        WorldViewPresenter presenter;
        Game game;
        Weather weather;
        Clouds clouds;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            presenter = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            game = new Game();
            clouds = new Clouds(content.Scenery, new System.Random(1)).Install(game);
            weather = new Weather(content.Scenery, new System.Random(2)).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null) Object.DestroyImmediate(presenter.gameObject);
        }

        [Test]
        public void EveryWeatherAndCloudPictureIsImported()
        {
            var library = new TextureLibrary();
            foreach (var file in content.Scenery.Files)
                Assert.IsNotNull(library.Load(file), file + " is not imported under Art/Resources");
        }

        [Test]
        public void TheTentStandsBetweenTheTilesAndThePickups()
        {
            var tent = presenter.TentRenderer;
            Assert.AreEqual(new Vector3(-0.7f, -0.04f, 0f), tent.transform.position);
            Assert.AreEqual("gfx/tent/Tent_128.PNG", presenter.FileOf(tent));
            var bounds = tent.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(0.3f, bounds.size.x, 1e-6f);
            Assert.AreEqual(0.3f, bounds.size.y, 1e-6f);
            Assert.Greater(tent.sortingOrder, presenter.Tiles.Max(t => t.Value.sortingOrder));
            Assert.Less(tent.sortingOrder, WorldViewPresenter.PickupsOrder);
            Assert.IsTrue(tent.enabled);
            Assert.AreSame(presenter.Tiles[0].Value.sharedMaterial, tent.sharedMaterial, "lit by the world's light like every world quad");

            presenter.View.Camera.MoveTo(2.5f, 0f, -1f);
            presenter.Refresh();
            Assert.IsFalse(tent.enabled, "out of the render range");
        }

        [Test]
        public void TheWeatherIsOneMeshOfEveryParticleOverTheObjects()
        {
            presenter.ShowWeather(weather, game);
            var r = presenter.WeatherRenderer;
            Assert.AreEqual(2501 * 4, r.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            Assert.AreEqual(1, presenter.GetComponentsInChildren<MeshRenderer>(true).Count(m => m.name == "Weather"), "one renderer, not thousands");
            Assert.Greater(r.sortingOrder, WorldViewPresenter.StatusIconOrder);
            Assert.Less(r.sortingOrder, WorldViewPresenter.CursorOrder);
            Assert.AreEqual("gfx/weather/snow1.PNG", presenter.FileOf(r));
            Assert.AreSame(presenter.TentRenderer.sharedMaterial, r.sharedMaterial, "lit by the world's light");
            Assert.IsTrue(r.enabled);

            game.Submit(new SetWeather(PlayerId.One, WeatherKind.DustStorm));
            presenter.Refresh();
            Assert.AreEqual("gfx/weather/dust1.PNG", presenter.FileOf(r));

            game.Submit(new SetWeather(PlayerId.One, WeatherKind.Clear));
            presenter.Refresh();
            Assert.IsFalse(r.enabled, "Clear draws nothing");
        }

        [Test]
        public void OnlyParticlesInRangeOnAllThreeAxesAreDrawn()
        {
            presenter.ShowWeather(weather, game);
            var expected = Enumerable.Range(0, weather.Count).Count(i => presenter.View.IsVisible(weather[i].X, weather[i].Y, weather[i].Z));
            Assert.AreEqual(expected, presenter.WeatherQuads.LastShown);
            Assert.Less(expected, weather.Count, "most start far above the camera");

            // A particle drawn sits where Core has it.
            var shown = Enumerable.Range(0, weather.Count).First(i => presenter.View.IsVisible(weather[i].X, weather[i].Y, weather[i].Z));
            var p = weather[shown];
            Assert.AreEqual(new Vector3(p.X, p.Y - 0.02f, p.Z), presenter.WeatherQuads.Corner(shown));
        }

        [Test]
        public void TheCloudsAreOneMeshOverOnePictureDrawnAfterTheWeather()
        {
            presenter.ShowWeather(weather, game);
            presenter.ShowClouds(clouds, game);
            var r = presenter.CloudsRenderer;
            Assert.AreEqual(401 * 4, r.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            Assert.IsNotNull(presenter.CloudAtlas, "cloud1, cloud2 and the balloon packed into one picture");
            Assert.Greater(r.sortingOrder, presenter.WeatherRenderer.sortingOrder);
            Assert.Less(r.sortingOrder, WorldViewPresenter.CursorOrder);
            Assert.AreSame(presenter.TentRenderer.sharedMaterial, r.sharedMaterial, "lit by the world's light");
            Assert.IsTrue(r.enabled);

            var expected = Enumerable.Range(0, clouds.Count).Count(i => presenter.View.IsVisible(clouds[i].X, clouds[i].Y, clouds[i].Z));
            Assert.AreEqual(expected, presenter.CloudQuads.LastShown);

            // The balloons show the balloon's part of the picture, the clouds another.
            var uv = r.GetComponent<MeshFilter>().sharedMesh.uv;
            Assert.AreNotEqual(uv[0], uv[4 * 4], "cloud 4 is not the balloon");
            Assert.AreEqual(uv[0], uv[4], "the first four are all the balloon");
        }

        [Test]
        public void TheWeatherKindArgumentReadsEveryMode()
        {
            Assert.IsTrue(OverworldBootstrap.TryWeather("rain", out var kind, out var seconds));
            Assert.AreEqual(WeatherKind.Raining, kind);
            Assert.AreEqual(0f, seconds);
            Assert.IsTrue(OverworldBootstrap.TryWeather("DustStorm,2.5", out kind, out seconds));
            Assert.AreEqual(WeatherKind.DustStorm, kind);
            Assert.AreEqual(2.5f, seconds);
            Assert.IsTrue(OverworldBootstrap.TryWeather("leaves", out kind, out _));
            Assert.AreEqual(WeatherKind.FallingLeaves, kind);
            Assert.IsFalse(OverworldBootstrap.TryWeather("hail", out _, out _));
        }
    }
}

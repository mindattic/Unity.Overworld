using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Editor;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A4 in Unity: the presenter draws exactly what Core's WorldView says, with the original's pictures.
    public class WorldViewPresenterTests
    {
        WorldViewPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            var content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            presenter = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
        }

        [TearDown]
        public void TearDown()
        {
            if (presenter != null) Object.DestroyImmediate(presenter.gameObject);
        }

        [Test]
        public void EveryPictureTheViewDrawsIsImported()
        {
            var library = new TextureLibrary();
            foreach (var file in presenter.View.View.Files)
                Assert.IsNotNull(library.Load(file), file + " is not imported under Art/Resources");
        }

        [Test]
        public void TheCameraLooksDownZFromMinus1At45DegreesOverMagenta()
        {
            var c = presenter.Camera;
            Assert.AreEqual(new Vector3(0, 0, -1), c.transform.position);
            Assert.AreEqual(Quaternion.identity, c.transform.rotation);
            Assert.AreEqual(45f, c.fieldOfView);
            Assert.IsFalse(c.orthographic);
            Assert.AreEqual(100f, c.farClipPlane);
            Assert.AreEqual(CameraClearFlags.SolidColor, c.clearFlags);
            Assert.AreEqual((Color)new Color32(255, 0, 255, 255), c.backgroundColor);
        }

        [Test]
        public void TheBackdropIsTheMenuBoxBehindEverything()
        {
            var b = presenter.Backdrop;
            Assert.AreEqual(new Vector3(67, 55, 38), b.transform.localScale);
            Assert.AreEqual("gfx/gui/menu.PNG", presenter.FileOf(b));
            Assert.AreEqual(36, b.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            Assert.Less(b.sortingOrder, presenter.Tiles.Min(t => t.Value.sortingOrder));
        }

        [Test]
        public void TilesSitOnTheGridAndOnlyThoseInRangeAreDrawn()
        {
            Assert.AreEqual(45, presenter.Tiles.Count);
            var a1 = presenter.Tiles.Single(t => t.Key.Name == "A1").Value;
            Assert.AreEqual(new Vector3(-3, 3, 0), a1.transform.position);
            var bounds = a1.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.AreEqual(new Vector3(0, -0.5f, 0), bounds.min);
            Assert.AreEqual(new Vector3(1, 0.5f, 0), bounds.max);

            Assert.AreEqual(25, presenter.Tiles.Count(t => t.Value.enabled));
            presenter.View.Camera.MoveTo(1, 0, -3.5f);
            presenter.Refresh();
            Assert.AreEqual(45, presenter.Tiles.Count(t => t.Value.enabled));
        }

        [Test]
        public void ObjectsAreLayeredBackToFrontByY()
        {
            var flagg = presenter.ObjectRenderer(DrawableKind.Hero, 2);
            var chrono = presenter.ObjectRenderer(DrawableKind.Hero, 0);
            var belmont = presenter.ObjectRenderer(DrawableKind.Hero, 1);
            var wall = presenter.ObjectRenderer(DrawableKind.PropBottom, 3);
            var tree = presenter.ObjectRenderer(DrawableKind.PropBottom, 0);
            Assert.Less(wall.sortingOrder, tree.sortingOrder);
            Assert.Less(tree.sortingOrder, flagg.sortingOrder);
            Assert.Less(flagg.sortingOrder, chrono.sortingOrder);
            Assert.Less(chrono.sortingOrder, belmont.sortingOrder);
            Assert.Less(chrono.sortingOrder, presenter.ObjectRenderer(DrawableKind.Weapon, 0).sortingOrder);
            Assert.Greater(belmont.sortingOrder, presenter.Tiles.Max(t => t.Value.sortingOrder));
            Assert.AreEqual(new Vector3(0.3f, -0.05f, 0), belmont.transform.position);

            // Belmont walks above everyone: he is now drawn first of the heroes.
            var poses = presenter.Poses;
            poses[1] = new HeroPose(0.3f, 0.5f, 0f, poses[1].Sprite, poses[1].WeaponSprite);
            presenter.Refresh();
            Assert.Less(belmont.sortingOrder, flagg.sortingOrder);
        }

        [Test]
        public void ObjectsOutOfRangeAreCulled()
        {
            Assert.IsTrue(presenter.ObjectRenderer(DrawableKind.PropBottom, 1).enabled);
            presenter.View.Camera.MoveTo(-2.2f, 0, -1);
            presenter.Refresh();
            Assert.IsFalse(presenter.ObjectRenderer(DrawableKind.PropBottom, 1).enabled);
            Assert.IsTrue(presenter.ObjectRenderer(DrawableKind.Hero, 2).enabled);
        }

        [Test]
        public void MagentaIsKeyedOutToTransparentBlack()
        {
            var pixels = new[] { new Color32(255, 0, 255, 255), new Color32(254, 0, 255, 255), new Color32(10, 20, 30, 255) };
            Assert.AreEqual(1, ColourKey.Apply(pixels));
            Assert.AreEqual(new Color32(0, 0, 0, 0), pixels[0]);
            Assert.AreEqual(new Color32(254, 0, 255, 255), pixels[1]);
        }
    }
}

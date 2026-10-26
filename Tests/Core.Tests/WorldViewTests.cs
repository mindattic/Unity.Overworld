using System;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A4: the world as the original drew it (RECONSTRUCTION 2.4, 3.1 to 3.4). The area grid, the camera and
    /// its culling, the backdrop and the back-to-front draw order, read from view.json and checked against the source.
    /// </summary>
    public class WorldViewTests
    {
        static string FindUp(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(relative + " was not found above " + AppContext.BaseDirectory);
        }

        static string ContentDirectory() => FindUp(Path.Combine("Assets", "StreamingAssets", "Content"));

        static readonly ContentRegistry Registry = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));

        static WorldView NewView() => new WorldView(Registry.View, Registry.Props, Registry.Heroes);

        static ContentException Rejected(Func<string, string> change)
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var original = disk.Read("view.json");
            var changed = change(original);
            Assert.AreNotEqual(original, changed, "the patch to view.json changed nothing");
            return Assert.Throws<ContentException>(() => ContentRegistry.Load(new MemoryContentSource(disk).Set("view.json", changed)));
        }

        // ---- the grid ------------------------------------------------------------------------------------------

        [Test]
        public void TheGridIs11By11WithThe45PicturedSlotsOfSection31()
        {
            var grid = Registry.View.Grid;
            Assert.AreEqual(11, grid.Rows);
            Assert.AreEqual(11, grid.Columns);
            Assert.AreEqual(1f, grid.TileSize);

            var expected = new[] { "A1-8", "B1-9", "C1-7", "D1-7", "E1-7", "F1-6", "F8-8" }
                .SelectMany(s =>
                {
                    var range = s.Substring(1).Split('-').Select(int.Parse).ToArray();
                    return Enumerable.Range(range[0], range[1] - range[0] + 1).Select(n => s[0] + n.ToString());
                }).ToArray();
            Assert.AreEqual(45, expected.Length);
            Assert.AreEqual(expected, grid.Tiles.Select(t => t.Name).ToArray());
        }

        [Test]
        public void SlotsSitWhereLoadBackgroundPutThemWithRowAAtTheTop()
        {
            var grid = Registry.View.Grid;
            void At(string name, float x, float y)
            {
                var t = grid.Find(name);
                Assert.IsNotNull(t, name);
                Assert.AreEqual(x, t.X, name + " x");
                Assert.AreEqual(y, t.Y, name + " y");
            }

            At("A1", -3, 3);
            At("A8", 4, 3);
            At("B9", 5, 2);
            At("D4", 0, 0);
            At("F1", -3, -2);
            At("F8", 4, -2);
            Assert.AreEqual(3f, grid.Tiles.Max(t => t.Y), "row A is the top row");
            Assert.IsTrue(grid.Tiles.All(t => t.Y == 3 - t.Row && t.X == -3 + t.Column - 1));
        }

        [Test]
        public void EveryTilePictureIsTheCataloguedAreaTileOfItsName()
        {
            var catalogue = File.ReadAllLines(FindUp(Path.Combine("docs", "asset-catalogue.tsv"))).Skip(1)
                .Select(l => l.Split('\t')).Where(c => c.Length == 4).ToDictionary(c => c[0], c => c[2], StringComparer.Ordinal);
            var loadedTiles = catalogue.Where(c => c.Value == "used" && System.Text.RegularExpressions.Regex.IsMatch(c.Key, "^gfx/area/[A-K][0-9]+\\.(png|PNG)$"))
                .Select(c => c.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var viewTiles = Registry.View.Grid.Tiles.Select(t => t.File).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(loadedTiles, viewTiles, "the view's tiles are exactly the catalogue's loaded area tiles");

            foreach (var file in Registry.View.Files)
            {
                Assert.IsTrue(catalogue.TryGetValue(file, out var disposition), file + " is not in the asset catalogue (exact case)");
                Assert.AreEqual("used", disposition, file + " is drawn, so the catalogue must call it used");
            }
        }

        // ---- the camera ----------------------------------------------------------------------------------------

        [Test]
        public void TheCameraStartsAtZMinus1With45DegreesAndAFourByThreeBase()
        {
            var c = NewView().Camera;
            Assert.AreEqual(0f, c.X);
            Assert.AreEqual(0f, c.Y);
            Assert.AreEqual(-1f, c.Z);
            Assert.AreEqual(45f, c.Definition.FieldOfView);
            Assert.AreEqual(4f / 3f, c.Definition.Aspect, 1e-6);
            Assert.AreEqual(100f, c.Definition.Far);
            Assert.AreEqual(2f, c.RenderRange, "renderRange = 1 + |-1|");
        }

        [Test]
        public void ZoomIsHeldBetweenMinus1AndMinus3Point5UnlessTheCameraIsFree()
        {
            var c = NewView().Camera;
            c.MoveTo(0, 0, -5);
            Assert.AreEqual(-3.5f, c.Z);
            Assert.AreEqual(4.5f, c.RenderRange);
            c.MoveTo(0, 0, 2);
            Assert.AreEqual(-1f, c.Z);
            c.MoveTo(0, 0, -2.25f);
            Assert.AreEqual(-2.25f, c.Z);

            c.FreeCamera = true;
            c.MoveTo(0, 0, -6);
            Assert.AreEqual(-6f, c.Z);
            Assert.AreEqual(7f, c.RenderRange);
        }

        [Test]
        public void RangeIsInclusiveOnBothAxes()
        {
            var c = NewView().Camera;
            Assert.IsTrue(c.InRange(2, -2));
            Assert.IsTrue(c.InRange(-2, 2));
            Assert.IsFalse(c.InRange(2.01f, 0));
            Assert.IsFalse(c.InRange(0, -2.01f));
            Assert.IsTrue(WorldCamera.Near(5, 3, 2));
            Assert.IsFalse(WorldCamera.Near(5.5f, 3, 2));
        }

        [Test]
        public void AtTheStartOnlyThe25TilesB2ToF6AreDrawn()
        {
            var view = NewView();
            var visible = view.Grid.Tiles.Where(view.IsVisible).Select(t => t.Name).ToArray();
            var expected = "BCDEF".SelectMany(r => Enumerable.Range(2, 5).Select(n => r + n.ToString())).OrderBy(n => n).ToArray();
            Assert.AreEqual(expected, visible.OrderBy(n => n).ToArray());
        }

        [Test]
        public void ZoomedOutTheWholeGridIsDrawn()
        {
            var view = NewView();
            view.Camera.MoveTo(0, 0, -3.5f);
            Assert.AreEqual(44, view.Grid.Tiles.Count(view.IsVisible), "range 4.5 reaches x -3..4 (B9 at x 5 is out) and y -2..3");
            Assert.IsFalse(view.IsVisible(view.Grid.Find("B9")));
            view.Camera.MoveTo(1, 0, -3.5f);
            Assert.AreEqual(45, view.Grid.Tiles.Count(view.IsVisible));
        }

        // ---- the backdrop --------------------------------------------------------------------------------------

        [Test]
        public void TheBackdropIsThe67By55By38MenuBoxClearedToMagenta()
        {
            var b = Registry.View.Backdrop;
            Assert.AreEqual(67f, b.Width);
            Assert.AreEqual(55f, b.Height);
            Assert.AreEqual(38f, b.Depth);
            Assert.AreEqual("gfx/gui/menu.PNG", b.Texture);
            Assert.AreEqual(new byte[] { 255, 0, 255, 255 }, b.ClearColor.ToArray());
        }

        // ---- the draw order ------------------------------------------------------------------------------------

        [Test]
        public void ObjectsAreTheThreeHeroesTheFourPropBottomsAndTheThreeWeapons()
        {
            var view = NewView();
            var objects = view.Objects(view.StartPoses());
            Assert.AreEqual(10, objects.Count);
            Assert.AreEqual(new[] { 3, 4, 3 }, new[] { DrawableKind.Hero, DrawableKind.PropBottom, DrawableKind.Weapon }.Select(k => objects.Count(o => o.Kind == k)).ToArray());

            var chrono = objects[0];
            Assert.AreEqual((0f, 0f, 0.144f, 0.072f), (chrono.X, chrono.Y, chrono.Width, chrono.HalfHeight));
            Assert.AreEqual("gfx/hero/Chrono/stand/face_right.PNG", chrono.File);
            var wall = objects.Single(o => o.Kind == DrawableKind.PropBottom && o.Index == 3);
            Assert.AreEqual((1f, 1.064f, 1f, 0.072f), (wall.X, wall.Y, wall.Width, wall.HalfHeight));
            Assert.AreEqual("gfx/prop/structure/wall_medium.PNG", wall.File);
            Assert.IsTrue(objects.Where(o => o.Kind == DrawableKind.PropBottom && o.Index < 3).All(o => o.File == "gfx/prop/tree/tree_bottom_1.PNG"));
            Assert.IsTrue(objects.Where(o => o.Kind == DrawableKind.Weapon).All(o => o.File == "gfx/blank.PNG"));
        }

        [Test]
        public void TheStartIsDrawnBackToFrontByYWithTiesInListOrder()
        {
            var view = NewView();
            var order = view.BackToFront(view.StartPoses()).Select(o => o.Kind + " " + o.Index).ToArray();
            Assert.AreEqual(new[]
            {
                "PropBottom 3", // y 1.064
                "PropBottom 0", "PropBottom 1", "PropBottom 2", // y 1
                "Hero 2", "Weapon 2", // Flagg, y 0.1
                "Hero 0", "Weapon 0", // Chrono, y 0
                "Hero 1", "Weapon 1", // Belmont, y -0.05
            }, order);
        }

        [Test]
        public void DrawOrderIsAStableDescendingSort()
        {
            Assert.AreEqual(new[] { 2, 0, 3, 1 }, DrawOrder.BackToFront(new[] { 1f, -1f, 2f, 1f }));
            Assert.AreEqual(new[] { 0, 1, 2 }, DrawOrder.BackToFront(new[] { 0f, 0f, 0f }));
            Assert.IsEmpty(DrawOrder.BackToFront(new float[0]));
        }

        [Test]
        public void AHeroThatWalksBelowAnotherIsDrawnAfterIt()
        {
            var view = NewView();
            var poses = view.StartPoses();
            poses[2] = new HeroPose(-0.3f, -0.5f, 0f, poses[2].Sprite, poses[2].WeaponSprite);
            var order = view.BackToFront(poses);
            Assert.AreEqual("Hero 2", order[order.Count - 2].Kind + " " + order[order.Count - 2].Index);
            Assert.AreEqual(DrawableKind.Weapon, order[order.Count - 1].Kind);
        }

        [Test]
        public void ObjectsBeyondTheRenderRangeAreCulled()
        {
            var view = NewView();
            var objects = view.Objects(view.StartPoses());
            Assert.IsTrue(objects.All(o => view.IsVisible(o)), "everything is within 2 of the start");
            view.Camera.MoveTo(-2.2f, 0, -1);
            Assert.IsFalse(objects.Where(o => o.Kind == DrawableKind.PropBottom).Any(o => view.IsVisible(o)), "props at x >= 1 are beyond -2.2 + 2");
            Assert.IsTrue(view.IsVisible(objects[2]), "Flagg at x -0.3 is still within range");
            Assert.IsFalse(view.IsVisible(objects[0]), "Chrono at x 0 is not");
        }

        // ---- validation ----------------------------------------------------------------------------------------

        [Test]
        public void ATileOutsideTheGridIsRejectedByFileKeyAndLine()
        {
            var e = Rejected(t => t.Replace("{\"name\": \"F8\", \"file\": \"gfx/area/F8.PNG\"}", "{\"name\": \"L1\", \"file\": \"gfx/area/L1.PNG\"}"));
            StringAssert.Contains("view.json.grid.tiles[44]: 'name' \"L1\" is outside the 11 x 11 grid (line ", e.Message);
        }

        [Test]
        public void ATileWhosePictureIsNotItsNameIsRejected()
        {
            var e = Rejected(t => t.Replace("{\"name\": \"A2\", \"file\": \"gfx/area/A2.png\"}", "{\"name\": \"A2\", \"file\": \"gfx/area/B4z.PNG\"}"));
            StringAssert.Contains("view.json.grid.tiles[1]: 'file' must be the picture named after its slot \"A2\"", e.Message);
        }

        [Test]
        public void ADuplicateOrMalformedTileIsRejected()
        {
            var e = Rejected(t => t.Replace("\"name\": \"A2\"", "\"name\": \"A1\"").Replace("\"name\": \"A3\"", "\"name\": \"A03\""));
            StringAssert.Contains("view.json.grid.tiles[1]: 'name' \"A1\" is listed twice", e.Message);
            StringAssert.Contains("view.json.grid.tiles[2]: 'name' must be a letter and a number", e.Message);
        }

        [Test]
        public void AZoomRangeTheWrongWayRoundIsRejected()
        {
            var e = Rejected(t => t.Replace("\"farthestZ\": -3.5", "\"farthestZ\": -0.5"));
            StringAssert.Contains("view.json.camera: 'farthestZ' must be farther from the world", e.Message);
        }

        [Test]
        public void APropTextureWithNoFileIsRejected()
        {
            var e = Rejected(t => t.Replace("{\"name\": \"wall_medium\", \"file\": \"gfx/prop/structure/wall_medium.PNG\"},", ""));
            StringAssert.Contains("view.json: 'textures' has no file for the props' texture \"wall_medium\"", e.Message);
        }

        [Test]
        public void ABackslashPathIsRejected()
        {
            var e = Rejected(t => t.Replace("\"gfx/gui/menu.PNG\"", "\"gfx\\\\gui\\\\menu.PNG\""));
            StringAssert.Contains("view.json.backdrop: 'texture' must be a relative path with forward slashes", e.Message);
        }
    }
}

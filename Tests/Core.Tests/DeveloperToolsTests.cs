using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-T47: the developer tools behind their switch (off as shipped): F1 / F2 / F3 / F5 render switches, G / H ring
    /// icons with the Coffee Bean, Book and Rune fillers, L's test message, - / = game speed and the P / O path dots
    /// (RECONSTRUCTION 5.1, 5.2, 14.3; BIBLE "Debug keys").
    /// </summary>
    public class DeveloperToolsTests
    {
        static string ContentDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Content");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Assets/StreamingAssets/Content");
        }

        static readonly ContentRegistry Registry = ContentRegistry.Load(new DirectoryContentSource(ContentDirectory()));

        Game game;
        Party party;
        WorldClock clock;
        MessageBoard messages;
        DeveloperTools tools;

        void Build(bool enabled)
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5)).Install(game);
            messages = new MessageBoard(Registry.Hud);
            clock = new WorldClock(Registry.Time, party.GameSpeed, messages).Install(game);
            tools = new DeveloperTools(Registry.Developer, party, clock, messages, enabled).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
        }

        bool Do(ICommand command) => game.Submit(command).Accepted;

        [Test]
        public void TheToolsAreOffAsShipped()
        {
            Assert.IsFalse(Registry.Developer.Enabled);
            game = new Game();
            party = new Party(Registry.Heroes).Install(game);
            tools = new DeveloperTools(Registry.Developer, party).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            Assert.IsFalse(tools.Enabled);
            Assert.IsFalse(Do(new ToggleRenderSwitch(PlayerId.One, RenderSwitch.Wireframe)));
            Assert.IsFalse(Do(new ChangeGameSpeed(PlayerId.One, 1)));
            Assert.IsFalse(Do(new CyclePathMode(PlayerId.One)));
            Assert.IsFalse(Do(new ClearPath(PlayerId.One)));
            Assert.IsFalse(Do(new ResizeRing(PlayerId.One, 1)));
            Assert.IsFalse(Do(new ShowTestMessage(PlayerId.One)));
            Assert.AreEqual(1f, party.GameSpeed);
        }

        [Test]
        public void TheFiguresAreTheOriginals()
        {
            var d = Registry.Developer;
            Assert.AreEqual(0.01f, d.GameSpeedStep);
            Assert.AreEqual(0f, d.GameSpeedFloor);
            Assert.AreEqual("gfx/gui/path.PNG", d.Path.Picture);
            Assert.AreEqual(0.02f, d.Path.Size);
            Assert.AreEqual(0.05f, d.Path.Range);
            Assert.AreEqual(1001, d.Path.Slots);
            Assert.AreEqual(20, d.Path.Clear);
            Assert.AreEqual("gfx/gui/terrain.PNG", d.WallPicture);
            Assert.AreEqual("WHAt's up, this worked!!!!", d.TestText);
            Assert.AreEqual("fontArial", d.TestFont.Name);
            var ring = Registry.Heroes.Battle.Ring;
            CollectionAssert.AreEqual(new[] { "gfx/items/coffee_bean.PNG", "gfx/items/book2.PNG", "gfx/items/rune1.PNG" }, ring.Fillers.Select(f => f.Picture));
            CollectionAssert.AreEqual(new[] { 6.28f, 3.14f, 2.093f, 1.57f, 1.256f, 1.046f, 0.897f, 0.785f }, ring.SpinSteps);
        }

        [Test]
        public void TheRenderSwitchesFlip()
        {
            Build(true);
            Assert.IsFalse(tools.Wireframe);
            Assert.IsTrue(tools.Textures);
            Assert.IsFalse(tools.BackFaceCulling);
            Assert.IsTrue(tools.LinearFilter);
            foreach (RenderSwitch s in Enum.GetValues(typeof(RenderSwitch)))
            {
                var before = tools.Switch(s);
                Assert.IsTrue(Do(new ToggleRenderSwitch(PlayerId.One, s)));
                Assert.AreEqual(!before, tools.Switch(s), s.ToString());
            }
        }

        [Test]
        public void TheGameSpeedStepsByAHundredthAndStopsAtNought()
        {
            Build(true);
            Assert.IsTrue(Do(new ChangeGameSpeed(PlayerId.One, 1)));
            Assert.AreEqual(1.01f, party.GameSpeed, 1e-6f);
            Assert.AreEqual(1.01f, clock.GameSpeed, 1e-6f, "the clock runs by the same speed");
            for (var i = 0; i < 200; i++) Do(new ChangeGameSpeed(PlayerId.One, -1));
            Assert.AreEqual(0f, party.GameSpeed);
        }

        [Test]
        public void LQueuesTheTestMessage()
        {
            Build(true);
            Assert.IsTrue(Do(new ShowTestMessage(PlayerId.One)));
            Assert.AreEqual(1, messages.Waiting);
        }

        [Test]
        public void GAndHResizeTheRingWithTheFillers()
        {
            Build(true);
            var ring = party.RingOf(PlayerId.One);
            Assert.AreEqual(5, ring.Count);
            for (var i = 0; i < 5; i++) Assert.IsTrue(Do(new ResizeRing(PlayerId.One, 1)));
            Assert.AreEqual(8, ring.Count, "maxRings 7: eight icons at most");
            Assert.AreEqual("gfx/items/rune1.PNG", ring.Picture(7));
            for (var i = 0; i < ring.Count; i++) Assert.AreEqual(1.57f + 0.785f * i, ring.Angle(i), 1e-5f);
            for (var i = 0; i < 10; i++) Assert.IsTrue(Do(new ResizeRing(PlayerId.One, -1)));
            Assert.AreEqual(1, ring.Count, "ringTotal 0: one icon at least");
        }

        [Test]
        public void PCyclesThePathAndTheLeaderLaysDotsInSetMode()
        {
            Build(true);
            var trail = tools.Trail;
            Assert.AreEqual(PathMode.Off, trail.Mode);
            Assert.IsTrue(Do(new CyclePathMode(PlayerId.One)));
            Assert.AreEqual(PathMode.Show, trail.Mode);
            game.Step();
            Assert.AreEqual(0, trail.Laid, "show draws, it does not lay");
            Assert.IsTrue(Do(new CyclePathMode(PlayerId.One)));
            Assert.AreEqual(PathMode.Set, trail.Mode);
            game.Step();
            Assert.AreEqual(1, trail.Laid, "the first dot is always laid (slot 0 is far off)");
            var hero = party.Leader(PlayerId.One);
            var size = Registry.Heroes.HeroSize;
            Assert.AreEqual(hero.X + size / 2f, trail.X(1), 1e-5f);
            Assert.AreEqual(hero.Y - size / 2f + 0.01f, trail.Y(1), 1e-5f);
            game.Step();
            Assert.AreEqual(1, trail.Laid, "standing still lays no more");
            Assert.IsTrue(Do(new CyclePathMode(PlayerId.One)));
            Assert.AreEqual(PathMode.Show2, trail.Mode);
            Assert.IsTrue(Do(new CyclePathMode(PlayerId.One)));
            Assert.AreEqual(PathMode.Off, trail.Mode);
        }

        [Test]
        public void OClearsTheLastTwentyOrAll()
        {
            Build(true);
            var trail = tools.Trail;
            Do(new CyclePathMode(PlayerId.One));
            Do(new CyclePathMode(PlayerId.One));
            var hero = party.Leader(PlayerId.One);
            hero.Facing = Facing.Right;
            for (var i = 0; i < 30; i++)
            {
                hero.X += 0.1f;
                game.Step();
            }

            Assert.AreEqual(30, trail.Laid);
            Assert.AreEqual(31, trail.Next);
            Assert.IsTrue(Do(new ClearPath(PlayerId.One)));
            Assert.AreEqual(11, trail.Next);
            Assert.AreEqual(10, trail.Laid, "slots 11 to 31 cleared");
            Assert.IsTrue(Do(new ClearPath(PlayerId.One)));
            Assert.AreEqual(1, trail.Next);
            Assert.AreEqual(0, trail.Laid);
        }
    }
}

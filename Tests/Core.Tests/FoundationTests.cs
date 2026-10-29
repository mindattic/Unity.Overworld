using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;

namespace MindAttic.Overworld.Tests
{
    public class FoundationTests
    {
        static WorldDefinition TwoAreaWorld() => new WorldDefinition("w", new[]
        {
            new AreaDefinition(0, "a", 2, 2, new[] { 1, 2, 3, 4 }),
            new AreaDefinition(7, "b", 3, 1, new[] { 9, 9, 9 }),
        });

        [Test]
        public void StepperRunsSixtyTicksPerSecondRegardlessOfFrameSize()
        {
            foreach (var frame in new[] { 1.0 / 30, 1.0 / 60, 1.0 / 144, 1.0 / 240 })
            {
                var s = new FixedStepper(1000);
                int ticks = 0;
                int frames = (int)System.Math.Round(10.0 / frame);
                for (int i = 0; i < frames; i++) ticks += s.Advance(frame);
                Assert.That(ticks, Is.InRange(599, 600), "frame " + frame);
            }
        }

        [Test]
        public void StepperCapsAStallAndDropsTheBacklog()
        {
            var s = new FixedStepper(5);
            Assert.AreEqual(5, s.Advance(10));
            Assert.AreEqual(0, s.Advance(0.001));
        }

        [Test]
        public void StepperIgnoresNegativeAndNaN()
        {
            var s = new FixedStepper();
            Assert.AreEqual(0, s.Advance(-1));
            Assert.AreEqual(0, s.Advance(double.NaN));
        }

        [Test]
        public void PerFrameConstantsConvertBothWays()
        {
            Assert.AreEqual(120f, Clock.PerSecond(2f), 1e-4);
            Assert.AreEqual(2f, Clock.PerTick(120f), 1e-4);
        }

        [Test]
        public void CommandsMustNameAMemberPlayer()
        {
            var g = new Game();
            Assert.False(g.Submit(new LoadWorld(PlayerId.None, TwoAreaWorld())).Accepted);
            Assert.False(g.Submit(new LoadWorld(PlayerId.One, TwoAreaWorld())).Accepted);
            Assert.IsNull(g.World);
            Assert.True(g.Submit(new JoinGame(PlayerId.One)).Accepted);
            Assert.False(g.Submit(new JoinGame(PlayerId.One)).Accepted);
        }

        [Test]
        public void AcceptedCommandChangesStateAndRaisesItsEvents()
        {
            var g = new Game();
            var seen = new List<IGameEvent>();
            g.EventRaised += seen.Add;
            g.Submit(new JoinGame(PlayerId.One));
            var r = g.Submit(new LoadWorld(PlayerId.One, TwoAreaWorld()));
            Assert.True(r.Accepted);
            Assert.AreEqual(2, g.World.Areas.Count);
            Assert.AreEqual(2, seen.Count);
            Assert.IsInstanceOf<PlayerJoined>(seen[0]);
            Assert.AreEqual(2, ((WorldLoaded)seen[1]).AreaCount);
        }

        [Test]
        public void RejectedCommandRaisesNothing()
        {
            var g = new Game();
            int n = 0;
            g.EventRaised += _ => n++;
            g.Submit(new LoadWorld(PlayerId.One, TwoAreaWorld()));
            Assert.AreEqual(0, n);
        }

        sealed class Unknown : ICommand
        {
            public Unknown(PlayerId p) { Player = p; }
            public PlayerId Player { get; }
        }

        [Test]
        public void UnregisteredCommandIsRejected()
        {
            var g = new Game();
            g.Submit(new JoinGame(PlayerId.One));
            Assert.False(g.Submit(new Unknown(PlayerId.One)).Accepted);
        }

        sealed class Counter : ITickSystem
        {
            public int Steps;
            public void Step(Game game, long tick, ICollection<IGameEvent> events) { Steps++; }
        }

        [Test]
        public void AdvanceRunsSystemsOncePerTick()
        {
            var g = new Game();
            var c = new Counter();
            g.AddSystem(c);
            for (int i = 0; i < 120; i++) g.Advance(1.0 / 60);
            Assert.AreEqual(120, c.Steps);
            Assert.AreEqual(c.Steps, g.Tick);
        }

        [Test]
        public void WorldTypesAreGeneralAndValidated()
        {
            var w = TwoAreaWorld();
            Assert.AreEqual(4, w.Find(0).TileAt(1, 1));
            Assert.AreEqual(3, w.Find(7).Width);
            Assert.IsNull(w.Find(99));
            Assert.Throws<System.ArgumentException>(() => new AreaDefinition(1, "x", 2, 2, new[] { 1 }));
            Assert.Throws<System.ArgumentException>(() => new WorldDefinition("d", new[]
            {
                new AreaDefinition(1, "a", 1, 1, new[] { 0 }), new AreaDefinition(1, "b", 1, 1, new[] { 0 }),
            }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => w.Find(0).TileAt(2, 0));
        }

        [Test]
        public void CoreSourceHasNoEngineReference()
        {
            var dir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Assets/Unity.Rea.Overworld/Core"));
            Assert.True(Directory.Exists(dir), dir);
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(0));
            Assert.That(files.Where(f => File.ReadAllText(f).Contains("UnityEngine")), Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(dir, "Unity.Rea.Overworld.Core.asmdef")), Does.Contain("\"noEngineReferences\": true"));
        }
    }
}

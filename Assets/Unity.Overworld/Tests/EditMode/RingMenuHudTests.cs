using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A19 in Unity: End opens the ring around the leader (the select frame and the five icons, Move at the top)
    // with the dotted move circle behind the heroes; a turn to Attack shows the red range circle; Move shuts the ring and
    // keeps the circle; leaving battle hides it all.
    public class RingMenuHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        WorldViewPresenter world;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls, new System.Random(3)).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes));
            world.Follow(party, game);
            world.ShowRing(party, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
            world.Refresh();
        }

        [Test]
        public void EveryRingAndDotPictureIsImported()
        {
            var library = new TextureLibrary();
            var ring = content.Heroes.Battle.Ring;
            var circle = content.Heroes.Battle.Circle;
            foreach (var file in ring.Entries.Select(e => e.Picture).Concat(new[] { ring.SelectPicture, circle.GreenPicture, circle.BluePicture, circle.RedPicture }))
                Assert.IsNotNull(library.Load(file), file);
        }

        [Test]
        public void TheRingAndItsCirclesAreDrawnAroundTheLeader()
        {
            Steps(60);
            Assert.IsFalse(world.RingSelectRenderer.enabled);
            Assert.IsFalse(world.RingIcons.Any(r => r.enabled));
            Assert.IsFalse(world.CircleDots.Any(r => r.enabled));

            Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);
            Steps(1);
            Assert.IsTrue(world.RingSelectRenderer.enabled);
            Assert.AreEqual(5, world.RingIcons.Count(r => r.enabled));
            Assert.AreEqual("gfx/ring/ring_Move.PNG", world.FileOf(world.RingIcons[0]));
            Assert.Greater(world.RingIcons[0].sortingOrder, world.RingSelectRenderer.sortingOrder, "the frame first, the icons over it");
            var chrono = party.Heroes[0];
            var top = world.RingIcons[0].transform.localPosition;
            Assert.AreEqual(chrono.X + 0.072f - 0.025f, top.x, 1e-3f, "Move at the top, over the leader's middle");
            Assert.AreEqual(chrono.Y - 0.072f + 0.04f + 0.1f, top.y, 1e-3f);

            Assert.AreEqual(21, world.CircleDots.Count(r => r.enabled));
            Assert.Less(world.CircleDots[0].sortingOrder, WorldViewPresenter.ObjectsOrder, "the dots are behind the heroes");

            Assert.IsTrue(game.Submit(new TurnRing(PlayerId.One, 1)).Accepted);
            Steps(15);
            Assert.AreEqual(RingEntry.Attack, party.RingOf(PlayerId.One).SelectedEntry);
            Assert.IsTrue(world.CircleDots.All(r => world.FileOf(r) == "gfx/gui/red_dot.PNG"));

            Assert.IsTrue(game.Submit(new TurnRing(PlayerId.One, -1)).Accepted);
            Steps(15);
            Assert.IsTrue(game.Submit(new ChooseFromRing(PlayerId.One)).Accepted);
            Steps(1);
            Assert.IsFalse(world.RingSelectRenderer.enabled, "Move shuts the ring");
            Assert.AreEqual(21, world.CircleDots.Count(r => r.enabled), "the move circle stays");

            Assert.IsTrue(game.Submit(new ToggleBattle(PlayerId.One)).Accepted);
            Steps(1);
            Assert.IsFalse(world.CircleDots.Any(r => r.enabled));
        }
    }
}

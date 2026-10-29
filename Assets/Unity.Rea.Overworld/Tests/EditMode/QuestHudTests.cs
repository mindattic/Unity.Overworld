using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A15 in Unity: a "*" over the world at every incomplete objective, in its light, gone once the objective
    // is complete; the quest screen on Page Down with its page's picture and lines where renderQuestScreen wrote them;
    // and the scripted quest arguments read as the bootstrap takes them.
    public class QuestHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        QuestLog quests;
        WorldViewPresenter world;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls);
            var pickups = new Pickups(content.Pickups, party, null, new System.Random(3)).Install(game);
            quests = new QuestLog(content.Quests, party).Install(game);
            party.Install(game);
            var control = new CameraControl(content.View, party).Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            var viewpoint = control.For(PlayerId.One);
            world = WorldViewPresenter.Create(new WorldView(content.View, content.Props, content.Heroes, viewpoint.Camera));
            world.Follow(party, game);
            hud = HudPresenter.Create(content.Hud, null, null);
            hud.ShowVitals(party, PlayerId.One);
            hud.ShowPickups(pickups, viewpoint, world, PlayerId.One);
            hud.ShowQuests(quests, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
        }

        int ShownMarkers => hud.MarkerLabels.Count(l => l.style.display.value == DisplayStyle.Flex);

        [Test]
        public void AStarMarksEveryIncompleteObjectiveUntilItIsReached()
        {
            world.Refresh();
            hud.Refresh();
            Assert.AreEqual(8, hud.MarkerLabels.Count, "six main objectives and two subquests'");
            Assert.Greater(ShownMarkers, 0, "those in view are drawn");
            var first = hud.MarkerLabels[0];
            Assert.AreEqual("*", first.text);
            Assert.AreEqual(world.Tint.r, first.style.color.value.r, 1e-4f, "in the world's light");

            var north = quests.Quests[0].Objectives[0];
            var at = quests.StandingAt(north.X, north.Y);
            Assert.IsTrue(game.Submit(new PlaceHero(PlayerId.One, 0, at.X, at.Y)).Accepted);
            game.Step();
            hud.Refresh();
            Assert.IsTrue(north.Complete);
            Assert.AreEqual(7, quests.Markers.Count, "one marker fewer");
            Assert.AreEqual(DisplayStyle.None, hud.MarkerLabels[7].style.display.value, "the eighth label is no longer used");
        }

        [Test]
        public void PageDownShowsTheQuestScreenWithItsPageAndLines()
        {
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.QuestPicture.parent.style.display.value, "shut at the start");
            Assert.IsTrue(game.Submit(new ToggleQuestScreen(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.QuestPicture.parent.style.display.value);
            Assert.AreEqual("gfx/gui/quest_screen_main.PNG", hud.QuestPictureFile);
            Assert.AreEqual(20f, hud.QuestPicture.style.top.value.value, 1e-4f);
            Assert.AreEqual(430f, hud.QuestPicture.style.height.value.value, 1e-4f);
            Assert.AreEqual(600f * hud.HorizontalScale, hud.QuestPicture.style.width.value.value, 1e-4f);
            Assert.AreEqual("Farmer Brinn wants you to check the crops", hud.QuestLabels[0].text);
            Assert.AreEqual(45f, hud.QuestLabels[0].style.top.value.value, 1e-4f);
            Assert.AreEqual(180f * hud.HorizontalScale, hud.QuestLabels[0].style.left.value.value, 1e-4f);
            Assert.AreEqual("~ Check North Field", hud.QuestLabels[1].text);
            Assert.AreEqual("~ Check South Field", hud.QuestLabels[2].text);
            Assert.AreEqual(100f, hud.QuestLabels[2].style.top.value.value, 1e-4f);

            Assert.IsTrue(game.Submit(new PressQuestKey(PlayerId.One, QuestKey.Down)).Accepted);
            Assert.IsTrue(game.Submit(new PressQuestKey(PlayerId.One, QuestKey.Right)).Accepted);
            hud.Refresh();
            Assert.AreEqual("gfx/gui/quest_screen_subquests.PNG", hud.QuestPictureFile);
            Assert.AreEqual("Check out the weird cirle", hud.QuestLabels[0].text);
            Assert.AreEqual(128f / 255f, hud.QuestLabels[0].style.color.value.b, 1e-3f, "the chosen subquest is pale yellow");
            Assert.AreEqual(DisplayStyle.None, hud.QuestLabels[2].style.display.value, "two subquests, no third line");

            Assert.IsTrue(game.Submit(new ToggleQuestScreen(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.QuestPicture.parent.style.display.value);
        }

        [Test]
        public void TheScriptedQuestArgumentsRead()
        {
            Assert.IsTrue(OverworldBootstrap.TryObjectives("0:0+0:1+s:1:0,2", quests, out var items, out var seconds));
            Assert.AreEqual(2f, seconds);
            CollectionAssert.AreEqual(new[] { "Check North Field", "Check South Field", "Go very far north" }, items.Select(o => o.Text));
            Assert.IsFalse(OverworldBootstrap.TryObjectives("4:0", quests, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryObjectives("0:2", quests, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryObjectives("s:2:0", quests, out _, out _));

            Assert.IsTrue(OverworldBootstrap.TryQuestScreen("main", out var keys, out seconds));
            Assert.IsEmpty(keys);
            Assert.IsTrue(OverworldBootstrap.TryQuestScreen("finished,1", out keys, out seconds));
            CollectionAssert.AreEqual(new[] { QuestKey.Down, QuestKey.Down }, keys);
            Assert.AreEqual(1f, seconds);
            Assert.IsTrue(OverworldBootstrap.TryQuestScreen("subquest:1", out keys, out _));
            CollectionAssert.AreEqual(new[] { QuestKey.Down, QuestKey.Right, QuestKey.Down, QuestKey.Right }, keys);
            Assert.IsFalse(OverworldBootstrap.TryQuestScreen("quests", out _, out _));
        }
    }
}

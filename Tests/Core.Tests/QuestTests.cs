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
    /// U.OVW-US-A15: the original's four main quests and two subquests (Quests.vb, RECONSTRUCTION 10), completed by the
    /// leader walking into each objective's range, earning the original's experience and running the end scripts through
    /// the interpreter; the quest screen on Page Down (QuestScreen.vb) with its pages, subquest list and lines; and the
    /// "*" at every incomplete objective. The last quest's experience is paid once (the original paid it every frame).
    /// </summary>
    public class QuestTests
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
        InventoryControl pack;
        WorldClock clock;
        ScriptInterpreter scripts;
        QuestLog quests;
        List<IGameEvent> raised;
        Hero chrono;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            var pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            var view = new CameraControl(Registry.View, party);
            pack = new InventoryControl(pickups, Registry.Hud.Inventory, Registry.Hud.QuickBar, view).Install(game);
            var messages = new MessageBoard(Registry.Hud);
            clock = new WorldClock(Registry.Time, Registry.Heroes.Locomotion.GameSpeed, messages);
            var sleep = new SleepCycle(party, clock);
            scripts = new ScriptInterpreter(Registry.Scripts, party, pickups.Inventory)
            {
                Sleep = sleep, Clock = clock, Pack = pack, View = view, Messages = messages,
            }.Install(game);
            pickups.Scripts = scripts;
            pack.Scripts = scripts;
            quests = new QuestLog(Registry.Quests, party) { Scripts = scripts, ScriptHero = scripts.TargetOf }.Install(game);
            party.Install(game);
            view.Install(game);
            sleep.Install(game);
            clock.Install(game);
            messages.Install(game);
            Submit(new JoinGame(PlayerId.One));
            chrono = party.Heroes[0];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Submit(ICommand c)
        {
            var r = game.Submit(c);
            Assert.IsTrue(r.Accepted, r.Reason);
        }

        /// <summary>Puts the leader's point at (x, y) and runs a tick.</summary>
        void StandOn(float x, float y)
        {
            var at = quests.StandingAt(x, y);
            Submit(new PlaceHero(PlayerId.One, party.LeaderOf(PlayerId.One), at.X, at.Y));
            game.Step();
        }

        void StandOn(Objective o) => StandOn(o.X, o.Y);

        Objective Main(int quest, int objective) => quests.Quests[quest].Objectives[objective];

        [Test]
        public void TheStoryStartsWithFarmerBrinnAndBothSubquests()
        {
            Assert.AreEqual(0, quests.CurrentIndex);
            CollectionAssert.AreEqual(new[] { QuestStatus.Active, QuestStatus.Inactive, QuestStatus.Inactive, QuestStatus.Inactive }, quests.Quests.Select(q => q.Status));
            CollectionAssert.AreEqual(new[] { QuestStatus.Active, QuestStatus.Active }, quests.Subquests.Select(q => q.Status));
            Assert.AreEqual(0, party.Experience);
            // showQuestPoints: every incomplete objective of every main quest, then of every Active subquest.
            CollectionAssert.AreEqual(
                new[] { "Check North Field", "Check South Field", "Check Center Field", "Go West, my boy!", "go East, my boy!", "Very Far West", "Go Way south", "Go very far north" },
                quests.Markers.Select(o => o.Text));
            // The leader's point is (x + heroSize / 2, y - heroSize / 3).
            var p = quests.PointOf(chrono);
            Assert.AreEqual(chrono.X + 0.072f, p.X, 1e-6f);
            Assert.AreEqual(chrono.Y - 0.048f, p.Y, 1e-6f);
        }

        [Test]
        public void WalkingIntoAnObjectivesRangeCompletesItOnBothAxesEdgesIncluded()
        {
            var north = Main(0, 0);
            StandOn(north.X + 0.12f, north.Y);
            Assert.IsFalse(north.Complete, "beyond the range across");
            StandOn(north.X, north.Y - 0.12f);
            Assert.IsFalse(north.Complete, "beyond the range down");
            StandOn(north.X + 0.099f, north.Y - 0.099f);
            Assert.IsTrue(north.Complete, "within 0.1 on both axes");
            Assert.AreEqual(100, party.Experience);
            var e = raised.OfType<QuestObjectiveCompleted>().Single();
            Assert.AreEqual((false, 0, 0, 100, 100), (e.Subquest, e.Quest, e.Objective, e.Exp, e.Experience));
            Assert.IsFalse(quests.Markers.Contains(north), "its marker is gone");

            StandOn(north);
            Assert.AreEqual(100, party.Experience, "an objective pays once");
        }

        [Test]
        public void OnlyTheCurrentMainQuestsObjectivesCount()
        {
            StandOn(Main(1, 0));
            StandOn(Main(3, 0));
            Assert.IsFalse(Main(1, 0).Complete);
            Assert.IsFalse(Main(3, 0).Complete);
            Assert.AreEqual(0, party.Experience);
        }

        [Test]
        public void TheWholeStoryPaysTheOriginalsExperienceAndRunsTheEastEndScript()
        {
            StandOn(Main(0, 0));
            StandOn(Main(0, 1));
            Assert.AreEqual(QuestStatus.Success, quests.Quests[0].Status);
            Assert.AreEqual(1, quests.CurrentIndex);
            Assert.AreEqual(QuestStatus.Active, quests.Quests[1].Status);
            Assert.AreEqual(100 + 150 + 1000, party.Experience);
            var done = raised.OfType<QuestCompleted>().Single();
            Assert.AreEqual((false, 0, 1000, 1250), (done.Subquest, done.Quest, done.Exp, done.Experience));
            Assert.AreEqual(1, raised.OfType<QuestStarted>().Single().Quest);

            StandOn(Main(1, 0));
            Assert.AreEqual(2, quests.CurrentIndex);
            Assert.AreEqual(1250 + 200 + 2000, party.Experience);

            StandOn(Main(2, 0));
            Assert.AreEqual(3450 + 320, party.Experience);
            Assert.AreEqual(9, clock.Now.Hour, "the west objective has no script");
            StandOn(Main(2, 1));
            Assert.AreEqual((18, "Evening"), (clock.Now.Hour, clock.Period.Name), "go East's end script: CHANGE TIME HOUR 18");
            Assert.AreEqual(3770 + 300 + 2500, party.Experience);
            Assert.AreEqual(3, quests.CurrentIndex);

            StandOn(Main(3, 0));
            Assert.AreEqual(6570 + 400 + 3500, party.Experience);
            Assert.AreEqual(QuestStatus.Success, quests.Quests[3].Status);
            CollectionAssert.AreEqual(new[] { "Go Way south", "Go very far north" }, quests.Markers.Select(o => o.Text), "only the subquests' markers are left");
        }

        [Test]
        public void TheLastQuestPaysOnceAndStaysFinished()
        {
            foreach (var (q, o) in new[] { (0, 0), (0, 1), (1, 0), (2, 0), (2, 1), (3, 0) }) StandOn(Main(q, o));
            var experience = party.Experience;
            Assert.AreEqual(10470, experience);
            for (var i = 0; i < 120; i++) game.Step();
            Assert.AreEqual(experience, party.Experience, "the original paid 3500 again every frame");
            Assert.AreEqual(QuestStatus.Success, quests.Current.Status);
            Assert.AreEqual(1, raised.OfType<QuestCompleted>().Count(e => e.Quest == 3));
        }

        [Test]
        public void SubquestsCompleteOnTheirOwnAlongsideTheStory()
        {
            StandOn(quests.Subquests[0].Objectives[0]);
            Assert.AreEqual(QuestStatus.Success, quests.Subquests[0].Status);
            Assert.AreEqual(200 + 350, party.Experience);
            Assert.AreEqual(0, quests.CurrentIndex, "the main story waits");
            StandOn(quests.Subquests[1].Objectives[0]);
            Assert.AreEqual(550 + 1200 + 1500, party.Experience);
            var done = raised.OfType<QuestCompleted>().Where(e => e.Subquest).Select(e => (e.Quest, e.Exp)).ToList();
            CollectionAssert.AreEqual(new[] { (0, 350), (1, 1500) }, done);
            Assert.IsFalse(quests.Markers.Any(o => o.Text.StartsWith("Go ", StringComparison.Ordinal) && o.Text.Contains("south")));
            for (var i = 0; i < 60; i++) game.Step();
            Assert.AreEqual(3250, party.Experience, "a finished subquest is no longer checked");
        }

        [Test]
        public void TheLeaderNotAFollowerCompletesObjectives()
        {
            Submit(new SelectHero(PlayerId.One, 1));
            var flagg = party.Heroes[2];
            var at = quests.StandingAt(Main(0, 0).X, Main(0, 0).Y);
            Submit(new PlaceHero(PlayerId.One, 2, at.X, at.Y));
            Submit(new ToggleTeam(PlayerId.One));
            game.Step();
            Assert.IsFalse(Main(0, 0).Complete, "Flagg follows; Belmont leads");
            StandOn(Main(0, 0));
            Assert.IsTrue(Main(0, 0).Complete);
            Assert.AreNotEqual(flagg, party.Leader(PlayerId.One));
        }

        // ---- the quest screen ---------------------------------------------------------------------------------------

        QuestScreenView Screen => quests.For(PlayerId.One);

        void Press(QuestKey key) => Submit(new PressQuestKey(PlayerId.One, key));

        [Test]
        public void PageDownOpensAndShutsTheScreenKeepingItsPage()
        {
            Assert.IsFalse(Screen.Open);
            Assert.IsFalse(game.Submit(new PressQuestKey(PlayerId.One, QuestKey.Down)).Accepted, "the arrows do nothing to a shut screen");
            Submit(new ToggleQuestScreen(PlayerId.One));
            Assert.IsTrue(Screen.Open);
            Press(QuestKey.Down);
            Assert.AreEqual(QuestPage.Subquests, Screen.Page);
            Submit(new ToggleQuestScreen(PlayerId.One));
            Submit(new ToggleQuestScreen(PlayerId.One));
            Assert.AreEqual(QuestPage.Subquests, Screen.Page, "it opens where it was");
        }

        [Test]
        public void TheArrowsWalkThePagesAndTheSubquestList()
        {
            Submit(new ToggleQuestScreen(PlayerId.One));
            // Up: Main, Finished, Subquests, Main. Down the other way.
            Press(QuestKey.Up);
            Assert.AreEqual(QuestPage.Finished, Screen.Page);
            Press(QuestKey.Up);
            Assert.AreEqual(QuestPage.Subquests, Screen.Page);
            Press(QuestKey.Up);
            Assert.AreEqual(QuestPage.Main, Screen.Page);
            Press(QuestKey.Right);
            Assert.AreEqual((QuestPage.Main, -1, false), (Screen.Page, Screen.Subquest, Screen.OnSubquests), "right enters only the subquest list");
            Press(QuestKey.Down);
            Assert.AreEqual(QuestPage.Subquests, Screen.Page);

            // Right enters the list on the first; up and down step through it, wrapping; right shows its page.
            Press(QuestKey.Right);
            Assert.AreEqual((QuestPage.Subquests, 0, true), (Screen.Page, Screen.Subquest, Screen.OnSubquests));
            Press(QuestKey.Up);
            Assert.AreEqual(1, Screen.Subquest);
            Press(QuestKey.Down);
            Assert.AreEqual(0, Screen.Subquest);
            Press(QuestKey.Down);
            Press(QuestKey.Right);
            Assert.AreEqual((QuestPage.SubquestInfo, 1), (Screen.Page, Screen.Subquest));
            // Up and down on a subquest's page still step the subquest (the page follows it).
            Press(QuestKey.Down);
            Assert.AreEqual((QuestPage.SubquestInfo, 0), (Screen.Page, Screen.Subquest));
            // Left: back to the list, then out of it.
            Press(QuestKey.Left);
            Assert.AreEqual((QuestPage.Subquests, 0, true), (Screen.Page, Screen.Subquest, Screen.OnSubquests));
            Press(QuestKey.Left);
            Assert.AreEqual((QuestPage.Subquests, -1, false), (Screen.Page, Screen.Subquest, Screen.OnSubquests));
            Press(QuestKey.Down);
            Assert.AreEqual(QuestPage.Finished, Screen.Page);
        }

        [Test]
        public void HeldArrowsWorkTheScreenOnceAPressAndDoNotMoveTheLeader()
        {
            Submit(new ToggleQuestScreen(PlayerId.One));
            var (x, y) = (chrono.X, chrono.Y);
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(down: true)));
            for (var i = 0; i < 30; i++) game.Step();
            Assert.AreEqual(QuestPage.Subquests, Screen.Page, "one page a press, however long it is held");
            Submit(new SetMoveInput(PlayerId.One, default));
            game.Step();
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(down: true)));
            game.Step();
            Assert.AreEqual(QuestPage.Finished, Screen.Page);
            Assert.AreEqual((x, y), (chrono.X, chrono.Y), "the arrows are the screen's");
            Assert.AreEqual(HeroState.Standing, chrono.State);

            Submit(new SetMoveInput(PlayerId.One, MoveInput.Toward(chrono.X + 1f, chrono.Y)));
            for (var i = 0; i < 30; i++) game.Step();
            Assert.AreEqual((x, y), (chrono.X, chrono.Y), "nor does the move button head for the cursor");
            Assert.IsFalse(game.Submit(new Rest(PlayerId.One)).Accepted, "nor does the leader rest");

            Submit(new ToggleQuestScreen(PlayerId.One));
            for (var i = 0; i < 30; i++) game.Step();
            Assert.Greater(chrono.X, x, "shut, the move button walks again");
        }

        [Test]
        public void TheScreenWritesTheOriginalsLines()
        {
            var def = Registry.Hud.QuestScreen;
            Assert.AreEqual((20f, 20f, 600f, 430f), (def.X, def.Y, def.Width, def.Height));
            Submit(new ToggleQuestScreen(PlayerId.One));
            StandOn(Main(0, 0));

            var main = def.Lines(quests, Screen);
            Assert.AreEqual("gfx/gui/quest_screen_main.PNG", def.Picture(Screen.Page));
            CollectionAssert.AreEqual(new[] { "Farmer Brinn wants you to check the crops", "X Check North Field", "~ Check South Field" }, main.Select(l => l.Text));
            CollectionAssert.AreEqual(new[] { (180f, 45f), (180f, 80f), (180f, 100f) }, main.Select(l => (l.At.X, l.At.Y)));
            CollectionAssert.AreEqual(new byte[] { 255, 128, 255 }, main.Select(l => l.Colour.A), "a completed objective is half transparent");

            Press(QuestKey.Down);
            Press(QuestKey.Right);
            var list = def.Lines(quests, Screen);
            Assert.AreEqual("gfx/gui/quest_screen_subquests.PNG", def.Picture(Screen.Page));
            CollectionAssert.AreEqual(new[] { "Check out the weird cirle", "Walk up to 3" }, list.Select(l => l.Text));
            CollectionAssert.AreEqual(new byte[] { 128, 255 }, list.Select(l => l.Colour.B), "the chosen one is pale yellow");

            Press(QuestKey.Right);
            CollectionAssert.AreEqual(new[] { "Check out the weird cirle", "~ Go Way south" }, def.Lines(quests, Screen).Select(l => l.Text));
            Assert.AreEqual("gfx/gui/quest_screen_subquests.PNG", def.Picture(Screen.Page));

            Press(QuestKey.Left);
            Press(QuestKey.Left);
            Press(QuestKey.Down);
            Assert.AreEqual("gfx/gui/quest_screen_completed.PNG", def.Picture(Screen.Page));
            Assert.IsEmpty(def.Lines(quests, Screen));
            StandOn(Main(0, 1));
            StandOn(Main(1, 0));
            var finished = def.Lines(quests, Screen);
            CollectionAssert.AreEqual(new[] { "o Farmer Brinn wants you to check the crops", "o Check the other crops" }, finished.Select(l => l.Text));
            CollectionAssert.AreEqual(new[] { 80f, 100f }, finished.Select(l => l.At.Y), "each at its own quest's line");
        }
    }
}

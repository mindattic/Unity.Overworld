using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>One objective of a quest in play: its definition and whether it is complete.</summary>
    public sealed class Objective
    {
        internal Objective(ObjectiveDefinition definition) { Definition = definition; }

        public ObjectiveDefinition Definition { get; }
        public bool Complete { get; internal set; }
        public string Text => Definition.Text;
        public float X => Definition.X;
        public float Y => Definition.Y;
        public override string ToString() => (Complete ? "X " : "~ ") + Text;
    }

    /// <summary>A main quest or a subquest in play: its definition, its status and its objectives.</summary>
    public sealed class Quest
    {
        readonly List<Objective> objectives = new List<Objective>();

        internal Quest(QuestDefinition definition, bool subquest)
        {
            Definition = definition;
            Subquest = subquest;
            Status = definition.Status;
            foreach (var o in definition.Objectives) objectives.Add(new Objective(o));
        }

        public QuestDefinition Definition { get; }
        public bool Subquest { get; }
        public int Index => Definition.Id;
        public string Text => Definition.Text;
        public QuestStatus Status { get; internal set; }
        public IReadOnlyList<Objective> Objectives => objectives;
        public override string ToString() => (Subquest ? "subquest " : "quest ") + Index + " '" + Text + "' " + Status;
    }

    /// <summary>The quest screen's pages (QuestBox.questCat).</summary>
    public enum QuestPage
    {
        /// <summary>The current main quest and its objectives.</summary>
        Main,

        /// <summary>The list of subquests.</summary>
        Subquests,

        /// <summary>One subquest and its objectives.</summary>
        SubquestInfo,

        /// <summary>The main quests finished.</summary>
        Finished,
    }

    /// <summary>One of the arrows as the quest screen reads it.</summary>
    public enum QuestKey
    {
        Left,
        Right,
        Up,
        Down,
    }

    /// <summary>One player's quest screen: open or shut, the page, and the subquest chosen.</summary>
    public sealed class QuestScreenView
    {
        internal QuestScreenView() { }

        /// <summary>The screen is shown (bQuestScreen, Page Down).</summary>
        public bool Open { get; internal set; }

        /// <summary>The page shown (questCategory).</summary>
        public QuestPage Page { get; internal set; } = QuestPage.Main;

        /// <summary>The subquest chosen on the list (subquestNum), or -1 for none.</summary>
        public int Subquest { get; internal set; } = -1;

        /// <summary>Up and down move through the subquests rather than the pages (onSubquests).</summary>
        public bool OnSubquests { get; internal set; }

        internal bool LeftHeld, RightHeld, UpHeld, DownHeld;
    }

    /// <summary>Page Down: the quest screen opens or shuts.</summary>
    public sealed class ToggleQuestScreen : ICommand
    {
        public ToggleQuestScreen(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>One press of an arrow on the quest screen (the held arrows do the same through the tick; scripted runs send this).</summary>
    public sealed class PressQuestKey : ICommand
    {
        public PressQuestKey(PlayerId player, QuestKey key) { Player = player; Key = key; }
        public PlayerId Player { get; }
        public QuestKey Key { get; }
    }

    /// <summary>A player's quest screen opened, shut, or changed page or subquest.</summary>
    public sealed class QuestScreenChanged : IGameEvent
    {
        public QuestScreenChanged(long tick, PlayerId player, bool open, QuestPage page, int subquest)
        {
            Tick = tick;
            Player = player;
            Open = open;
            Page = page;
            Subquest = subquest;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Open { get; }
        public QuestPage Page { get; }
        public int Subquest { get; }
    }

    /// <summary>A player's leader walked into an objective's range: it is complete and its experience is the party's.</summary>
    public sealed class QuestObjectiveCompleted : IGameEvent
    {
        public QuestObjectiveCompleted(long tick, PlayerId player, bool subquest, int quest, int objective, int exp, int experience)
        {
            Tick = tick;
            Player = player;
            Subquest = subquest;
            Quest = quest;
            Objective = objective;
            Exp = exp;
            Experience = experience;
        }

        public long Tick { get; }

        /// <summary>The player whose leader reached it.</summary>
        public PlayerId Player { get; }

        public bool Subquest { get; }
        public int Quest { get; }
        public int Objective { get; }

        /// <summary>The objective's experience.</summary>
        public int Exp { get; }

        /// <summary>The party's experience now.</summary>
        public int Experience { get; }
    }

    /// <summary>Every objective of a quest is complete: it succeeded and its experience is the party's.</summary>
    public sealed class QuestCompleted : IGameEvent
    {
        public QuestCompleted(long tick, PlayerId player, bool subquest, int quest, int exp, int experience)
        {
            Tick = tick;
            Player = player;
            Subquest = subquest;
            Quest = quest;
            Exp = exp;
            Experience = experience;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Subquest { get; }
        public int Quest { get; }
        public int Exp { get; }
        public int Experience { get; }
    }

    /// <summary>The main story moved on: quest <see cref="Quest"/> is the current one and Active.</summary>
    public sealed class QuestStarted : IGameEvent
    {
        public QuestStarted(long tick, int quest) { Tick = tick; Quest = quest; }
        public long Tick { get; }
        public int Quest { get; }
    }

    /// <summary>
    /// The quests (U.OVW-US-A15; Quests.vb and Gui/QuestScreen.vb, RECONSTRUCTION 10): the four main quests, one at a
    /// time, and the two subquests, all at once, completed by walking.
    /// <para>
    /// Each tick (checkQuests, then checkSubquests) every incomplete objective of the current main quest, and of every
    /// Active subquest, whose point lies within its range on both axes of a leader's point (x + heroSize / 2,
    /// y - heroSize / 3) is complete: its experience is added to the party's and its end script runs (the east objective of
    /// the third quest sets the clock to 6 PM). Once every objective of a quest is complete it succeeds, its experience is
    /// added and its end script runs; a main quest then hands on to the next, which becomes Active and runs its start
    /// script. The last main quest succeeds once and stays Success (the original set it Active again and paid its 3500
    /// experience again every frame; BIBLE). The next quest's objectives are first checked in the next tick, as the next frame did.
    /// </para>
    /// <para>
    /// Page Down (<see cref="ToggleQuestScreen"/>) opens and shuts each player's quest screen; while it is open each new
    /// press of an arrow (held, read from the player's input with the original's per-key latches, or
    /// <see cref="PressQuestKey"/>) works it: up and down step the page Main, Subquests, Finished (or, once right has
    /// entered the subquest list, the subquest chosen, wrapping), right enters the list and then shows the chosen
    /// subquest's page, left leaves the list or that page. The screen does not shut by itself and keeps its page.
    /// </para>
    /// Installed after the scripts and before the party, as checkQuests ran after getInput and before updateHero.
    /// Presentation reads <see cref="Quests"/>, <see cref="Subquests"/>, <see cref="Current"/>, <see cref="For"/> and
    /// <see cref="Markers"/>.
    /// </summary>
    public sealed class QuestLog : ITickSystem
    {
        readonly List<Quest> quests = new List<Quest>();
        readonly List<Quest> subquests = new List<Quest>();
        readonly Dictionary<PlayerId, QuestScreenView> views = new Dictionary<PlayerId, QuestScreenView>();

        public QuestLog(QuestData data, Party party)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            if (data.Quests.Count == 0) throw new ArgumentException("There must be at least one main quest.", nameof(data));
            foreach (var q in data.Quests) quests.Add(new Quest(q, false));
            foreach (var q in data.Subquests) subquests.Add(new Quest(q, true));
        }

        public QuestData Data { get; }
        public Party Party { get; }

        /// <summary>Runs the quests' and objectives' start and end scripts (null: they do not run).</summary>
        public IScriptRunner Scripts { get; set; }

        /// <summary>The hero a script's HERO means for a player (selectedHero); null: its leader.</summary>
        public Func<PlayerId, int> ScriptHero { get; set; }

        public IReadOnlyList<Quest> Quests => quests;
        public IReadOnlyList<Quest> Subquests => subquests;

        /// <summary>The current main quest's index (currQuest).</summary>
        public int CurrentIndex { get; private set; }

        public Quest Current => quests[CurrentIndex];

        /// <summary>The party's experience.</summary>
        public int Experience => Party.Experience;

        public QuestLog Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<ToggleQuestScreen>(HandleToggle);
            game.Register<PressQuestKey>(HandleKey);
            Party.QuestScreenOpen = player => For(player).Open;
            game.AddSystem(this);
            return this;
        }

        /// <summary><paramref name="player"/>'s quest screen.</summary>
        public QuestScreenView For(PlayerId player)
        {
            if (!views.TryGetValue(player, out var v)) views[player] = v = new QuestScreenView();
            return v;
        }

        /// <summary>
        /// showQuestPoints: every incomplete objective of every main quest (current, later and earlier alike), then of every
        /// Active subquest, where its "*" is drawn in the world.
        /// </summary>
        public IReadOnlyList<Objective> Markers
        {
            get
            {
                var list = new List<Objective>();
                foreach (var q in quests)
                    foreach (var o in q.Objectives)
                        if (!o.Complete) list.Add(o);
                foreach (var q in subquests)
                {
                    if (q.Status != QuestStatus.Active) continue;
                    foreach (var o in q.Objectives)
                        if (!o.Complete) list.Add(o);
                }

                return list;
            }
        }

        /// <summary>The point of hero <paramref name="hero"/> an objective's range is measured from.</summary>
        public DataPoint PointOf(Hero hero)
        {
            var size = Party.Data.HeroSize;
            return new DataPoint(hero.X + size * Data.HeroPoint.X, hero.Y + size * Data.HeroPoint.Y);
        }

        /// <summary>Where a leader stands with its point on (<paramref name="x"/>, <paramref name="y"/>) (for scripted runs).</summary>
        public DataPoint StandingAt(float x, float y)
        {
            var size = Party.Data.HeroSize;
            return new DataPoint(x - size * Data.HeroPoint.X, y - size * Data.HeroPoint.Y);
        }

        // ---- the screen ---------------------------------------------------------------------------------------------

        /// <summary>actionQuestScreen: the screen opens or shuts (not in a particular mode; it keeps its page).</summary>
        CommandResult HandleToggle(Game game, ToggleQuestScreen c)
        {
            var v = For(c.Player);
            v.Open = !v.Open;
            return CommandResult.Accept(Changed(c.Player, v, game.Tick));
        }

        CommandResult HandleKey(Game game, PressQuestKey c)
        {
            var v = For(c.Player);
            if (!v.Open) return CommandResult.Reject("The quest screen is shut.");
            var events = new List<IGameEvent>();
            Press(c.Player, c.Key, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        static QuestScreenChanged Changed(PlayerId player, QuestScreenView v, long tick) => new QuestScreenChanged(tick, player, v.Open, v.Page, v.Subquest);

        /// <summary>actionLeft / Right / Up / Down with the quest screen open (bQuestLeft ... bQuestDown, once a press).</summary>
        void Press(PlayerId player, QuestKey key, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            var before = (v.Page, v.Subquest, v.OnSubquests);
            var count = subquests.Count;
            switch (key)
            {
                case QuestKey.Left:
                    if (v.Page != QuestPage.SubquestInfo)
                    {
                        v.Subquest = -1;
                        v.OnSubquests = false;
                    }
                    else
                    {
                        v.Page = QuestPage.Subquests;
                        v.OnSubquests = true;
                    }

                    break;
                case QuestKey.Right:
                    if (!v.OnSubquests)
                    {
                        if (v.Page == QuestPage.Subquests && count > 0)
                        {
                            v.Subquest = 0;
                            v.OnSubquests = true;
                        }
                    }
                    else v.Page = QuestPage.SubquestInfo;

                    break;
                case QuestKey.Up:
                    if (v.OnSubquests) v.Subquest = v.Subquest > 0 ? v.Subquest - 1 : count - 1;
                    else v.Page = PageUp(v.Page);
                    break;
                default:
                    if (v.OnSubquests) v.Subquest = v.Subquest < count - 1 ? v.Subquest + 1 : 0;
                    else v.Page = PageDown(v.Page);
                    break;
            }

            if ((v.Page, v.Subquest, v.OnSubquests) != before) events.Add(Changed(player, v, tick));
        }

        /// <summary>QuestCategoryUp: Main to Finished to Subquests to Main (a subquest's page stays).</summary>
        static QuestPage PageUp(QuestPage page)
        {
            switch (page)
            {
                case QuestPage.Main: return QuestPage.Finished;
                case QuestPage.Finished: return QuestPage.Subquests;
                case QuestPage.Subquests: return QuestPage.Main;
                default: return page;
            }
        }

        /// <summary>QuestCategoryDown: Main to Subquests to Finished to Main (a subquest's page stays).</summary>
        static QuestPage PageDown(QuestPage page)
        {
            switch (page)
            {
                case QuestPage.Main: return QuestPage.Subquests;
                case QuestPage.Subquests: return QuestPage.Finished;
                case QuestPage.Finished: return QuestPage.Main;
                default: return page;
            }
        }

        static bool Pressed(bool held, ref bool wasHeld)
        {
            var pressed = held && !wasHeld;
            wasHeld = held;
            return pressed;
        }

        // ---- the tick: the screen's arrows, then checkQuests and checkSubquests -------------------------------------

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            foreach (var player in game.Players)
            {
                var v = For(player);
                var input = Party.InputOf(player);
                // The latches follow the keys whether or not the screen is open (KeysUp cleared them either way).
                var left = Pressed(input.Left, ref v.LeftHeld);
                var right = Pressed(input.Right, ref v.RightHeld);
                var up = Pressed(input.Up, ref v.UpHeld);
                var down = Pressed(input.Down, ref v.DownHeld);
                if (!v.Open) continue;
                // actionLeft, actionRight, actionUp, actionDown, in getKeyboard's order.
                if (left) Press(player, QuestKey.Left, tick, events);
                if (right) Press(player, QuestKey.Right, tick, events);
                if (up) Press(player, QuestKey.Up, tick, events);
                if (down) Press(player, QuestKey.Down, tick, events);
            }

            foreach (var player in game.Players) CheckQuest(game, player, tick, events);
            foreach (var player in game.Players) CheckSubquests(game, player, tick, events);
        }

        bool Reached(Hero hero, Objective o)
        {
            if (o.Definition.Type != ObjectiveType.MoveTo) return false;
            var p = PointOf(hero);
            var range = o.Definition.Range;
            return WorldCamera.Near(p.X, o.X, range) && WorldCamera.Near(p.Y, o.Y, range);
        }

        void Award(int exp) => Party.Experience += exp;

        void RunScript(Game game, PlayerId player, string script, long tick, ICollection<IGameEvent> events)
        {
            if (string.IsNullOrEmpty(script) || Scripts == null) return;
            var hero = ScriptHero != null ? ScriptHero(player) : Party.LeaderOf(player);
            Scripts.Run(game, player, hero, script, tick, events);
        }

        /// <summary>
        /// checkQuests for <paramref name="player"/>'s leader: the current main quest's objectives, then its completion and
        /// the hand-on to the next. (TalkTo, GiveTo and GetItem are never evaluated, as in the original.)
        /// </summary>
        void CheckQuest(Game game, PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            var quest = Current;
            if (quest.Status == QuestStatus.Success) return;
            var hero = Party.Leader(player);
            for (var n = 0; n < quest.Objectives.Count; n++)
            {
                var o = quest.Objectives[n];
                if (o.Complete || !Reached(hero, o)) continue;
                o.Complete = true;
                Award(o.Definition.Exp);
                events.Add(new QuestObjectiveCompleted(tick, player, false, quest.Index, n, o.Definition.Exp, Party.Experience));
                RunScript(game, player, o.Definition.EndScript, tick, events);
            }

            if (!AllComplete(quest)) return;
            quest.Status = QuestStatus.Success;
            Award(quest.Definition.Exp);
            events.Add(new QuestCompleted(tick, player, false, quest.Index, quest.Definition.Exp, Party.Experience));
            RunScript(game, player, quest.Definition.EndScript, tick, events);

            // The main story moves on; the last quest stays Success (the original made it Active again; BIBLE).
            if (CurrentIndex >= quests.Count - 1) return;
            CurrentIndex++;
            Current.Status = QuestStatus.Active;
            events.Add(new QuestStarted(tick, CurrentIndex));
            RunScript(game, player, Current.Definition.StartScript, tick, events);
        }

        /// <summary>checkSubquests for <paramref name="player"/>'s leader: every Active subquest's objectives, then its completion.</summary>
        void CheckSubquests(Game game, PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            var hero = Party.Leader(player);
            foreach (var quest in subquests)
            {
                if (quest.Status != QuestStatus.Active) continue;
                for (var n = 0; n < quest.Objectives.Count; n++)
                {
                    var o = quest.Objectives[n];
                    if (o.Complete || !Reached(hero, o)) continue;
                    o.Complete = true;
                    Award(o.Definition.Exp);
                    events.Add(new QuestObjectiveCompleted(tick, player, true, quest.Index, n, o.Definition.Exp, Party.Experience));
                    RunScript(game, player, o.Definition.EndScript, tick, events);
                }

                if (!AllComplete(quest)) continue;
                quest.Status = QuestStatus.Success;
                Award(quest.Definition.Exp);
                events.Add(new QuestCompleted(tick, player, true, quest.Index, quest.Definition.Exp, Party.Experience));
                RunScript(game, player, quest.Definition.EndScript, tick, events);
            }
        }

        static bool AllComplete(Quest quest)
        {
            foreach (var o in quest.Objectives)
                if (!o.Complete) return false;
            return true;
        }
    }
}

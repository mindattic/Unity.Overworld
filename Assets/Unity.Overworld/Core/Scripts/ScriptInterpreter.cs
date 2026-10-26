using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The original's scripting language and its console (U.OVW-US-A14; Script.vb and frmMain's scripter mode,
    /// RECONSTRUCTION 9). Every script runs here: the console's lines, every item's effect (<see cref="IScriptRunner"/>,
    /// for the pack and the gold), the Rune's teleport and the quests' end scripts (story A15 calls <see cref="Execute"/>).
    /// <para>
    /// A script is commands joined by " | ", upper-cased; a command is four words, Command Type Item Parameter, or the two
    /// of a keyword (RUN NIGHT). CHANGE HERO / HERO1..3 (HERO is the item's target, selectedHero) HP, MP, AP (+n / -n add,
    /// capped at the maximum and kept at 0 or more; anything else assigns within 0 and the maximum, a leading = stripped),
    /// X, Y, Z, FACING, STATUS, AI (FACE / STAND / WALK / RUN a direction, which run the script; REST / SIT / KNEEL; LAY /
    /// LAYDOWN / SLEEP, a sleep until the hour a CHANGE TIME HOUR in the same script sets, as the draughts mean; STOP), GOLD
    /// / MONEY; CHANGE ENEMY / BADGUY HEALTH, AI (stubs until the battle stories); CHANGE LIGHT AMBIENT (r,g,b or +/-n),
    /// RED, GREEN, BLUE; CHANGE TIME HOUR / CLOCK (set, the next day when earlier, or +/-n hours, the period and light
    /// settled) and DAYPERIOD (to the period's hour, the light following on the minute); CHANGE SCRIPT COLOR, RUNNING;
    /// CHANGE CAMERA / CAM X, Y, Z, POS HERO; CHANGE INVENTORY OPEN / CLOSE; CHANGE OPTIONS OPEN / CLOSE (recorded in
    /// <see cref="ScriptState"/> for the options screen of story A20); CHANGE BOOK (the player's book, <see cref="Books"/>);
    /// CHANGE CURSOR POS CENTER; MOVE HERO
    /// TO x,y,z (walks the hero there and runs until it arrives); RUN NIGHT; MAKE (nothing, as in the original). An
    /// unknown item of a known type is an error ("Script failed to execute.", the other commands still run); a number that
    /// is not one stops the script ("Script failed to run."). Unknown commands and types do nothing, as before.
    /// </para>
    /// <para>
    /// While a player's console is open or one of its scripts runs, the party ignores that player's input
    /// (<see cref="InputLocked"/>); the console's own keys stay live. Each tick a running MOVE steers its heroes and ends
    /// once each is within the arrive distance of its target (the original never called that check; BIBLE), and the
    /// prompt's I-beam blinks. Installed after the pack and before the party, as getInput ran before the heroes.
    /// </para>
    /// </summary>
    public sealed class ScriptInterpreter : IScriptRunner, ITickSystem
    {
        readonly Dictionary<PlayerId, ScriptState> states = new Dictionary<PlayerId, ScriptState>();

        public ScriptInterpreter(ScriptData data, Party party, Inventory inventory)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public ScriptData Data { get; }
        public Party Party { get; }

        /// <summary>The party's purse (GOLD) and pack (CHANGE BOOK TO ITEM names the book by its selected stack).</summary>
        public Inventory Inventory { get; }

        /// <summary>The sleep AI LAYDOWN starts and the ambient CHANGE LIGHT AMBIENT sets (null: a laid hero only lies down).</summary>
        public SleepCycle Sleep { get; set; }

        /// <summary>The clock CHANGE TIME sets and the light CHANGE LIGHT sets (null: they do nothing).</summary>
        public WorldClock Clock { get; set; }

        /// <summary>The panel CHANGE INVENTORY opens and the item target HERO means (null: neither).</summary>
        public InventoryControl Pack { get; set; }

        /// <summary>The camera and cursor CHANGE CAMERA and CHANGE CURSOR move (null: they do nothing).</summary>
        public CameraControl View { get; set; }

        /// <summary>Where the failure messages go (null: none are shown).</summary>
        public MessageBoard Messages { get; set; }

        /// <summary>The books CHANGE BOOK loads, shows and styles (null: CHANGE BOOK does nothing).</summary>
        public BookReader Books { get; set; }

        public ScriptInterpreter Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<RunScript>(HandleRun);
            game.Register<PressConsoleKey>(HandleKey);
            Party.InputLocked = InputLocked;
            game.AddSystem(this);
            return this;
        }

        /// <summary><paramref name="player"/>'s console, running script and recorded settings.</summary>
        public ScriptState For(PlayerId player)
        {
            if (!states.TryGetValue(player, out var s)) states[player] = s = new ScriptState(Data.Console.Colour);
            return s;
        }

        /// <summary>The console is open or a script runs: the game does not read <paramref name="player"/>'s input.</summary>
        public bool InputLocked(PlayerId player) => states.TryGetValue(player, out var s) && (s.ConsoleOpen || s.Running);

        /// <summary>The prompt the console writes for <paramref name="player"/> now (checkScripter), or null while it is shut.</summary>
        public string Prompt(PlayerId player)
        {
            var s = For(player);
            return s.ConsoleOpen ? Data.Console.Prompt(s.Line, s.Beam) : null;
        }

        /// <summary>The item target HERO means for <paramref name="player"/> (selectedHero), or its leader without a pack.</summary>
        public int TargetOf(PlayerId player) => Pack != null ? Pack.For(player).Target : Party.LeaderOf(player);

        public bool Run(Game game, PlayerId player, int hero, string script, long tick, ICollection<IGameEvent> events) =>
            Execute(game, player, hero, script, tick, events) == ScriptOutcome.Ran;

        CommandResult HandleRun(Game game, RunScript c)
        {
            if (c.Hero.HasValue && (c.Hero < 0 || c.Hero >= Party.Heroes.Count)) return CommandResult.Reject("There is no hero " + c.Hero + " in the party.");
            var events = new List<IGameEvent>();
            Execute(game, c.Player, c.Hero ?? TargetOf(c.Player), c.Script, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        // ---- the console (frmMain.OnKeyDown in scripter mode) ---------------------------------------------------------

        CommandResult HandleKey(Game game, PressConsoleKey c)
        {
            if (string.IsNullOrEmpty(c.Key)) return CommandResult.Reject("No key.");
            var s = For(c.Player);
            var con = Data.Console;
            var events = new List<IGameEvent>();
            var tick = game.Tick;
            var key = c.Key;
            var wasOpen = s.ConsoleOpen;
            var line = s.Line;

            if (!s.ConsoleOpen)
            {
                // Closed, only ~ (open) and Insert (open with the last script) are the console's.
                if (key == con.Open) s.ConsoleOpen = true;
                else if (key == con.Recall)
                {
                    s.Line = s.Last;
                    s.ConsoleOpen = true;
                }
                else return CommandResult.Accept();
            }
            else if (key == con.ShortcutKey)
            {
                var typed = false;
                foreach (var (from, to) in con.Shortcuts)
                {
                    if (s.Line != from) continue;
                    s.Line = to;
                    typed = true;
                    break;
                }

                if (!typed) s.Line += key;
            }
            else if (Contains(con.Close, key)) s.ConsoleOpen = false;
            else if (key == con.Recall) s.Line = s.Last;
            else if (Contains(con.Ignore, key)) { }
            else if (key == con.Clear) s.Line = string.Empty;
            else if (key == con.Erase)
            {
                if (s.Line.Length > 0) s.Line = s.Line.Substring(0, s.Line.Length - 1);
            }
            else if (key == con.Run)
            {
                var script = s.Line;
                s.Line = string.Empty;
                s.ConsoleOpen = false;
                Execute(game, c.Player, TargetOf(c.Player), script, tick, events);
                s.Last = script;
            }
            else
            {
                // Shift is read as it is now (the original's SHIFT stuck on after the first press; BIBLE).
                var typing = con.Typing(key);
                s.Line += typing != null ? (c.Shift ? typing.Shifted : typing.Text) : key;
            }

            if (s.ConsoleOpen != wasOpen || s.Line != line)
            {
                if (s.ConsoleOpen && !wasOpen)
                {
                    s.Beam = false;
                    s.LastBeamTime = tick * Core.Clock.TickSeconds;
                }

                events.Insert(0, new ConsoleChanged(tick, c.Player, s.ConsoleOpen, s.Line));
            }

            return CommandResult.Accept(events.ToArray());
        }

        static bool Contains(IReadOnlyList<string> list, string key)
        {
            foreach (var k in list) if (k == key) return true;
            return false;
        }

        /// <summary>
        /// The console keys (and whether Shift is held) that type <paramref name="text"/> on an empty line, upper-cased as
        /// the console types it: the shortcut key where the line it makes is wanted (and taken back with Back where it is
        /// not, as a player would), each character's key otherwise. Null when a character has no key.
        /// </summary>
        public static List<(string Key, bool Shift)> KeysFor(ConsoleDefinition console, string text)
        {
            if (console == null) throw new ArgumentNullException(nameof(console));
            var want = (text ?? string.Empty).ToUpperInvariant();
            var keys = new List<(string, bool)>();
            var line = string.Empty;
            while (line.Length < want.Length)
            {
                var next = want[line.Length];
                string to = null;
                foreach (var (from, shortcut) in console.Shortcuts)
                    if (from == line) to = shortcut;
                if (to != null && console.ShortcutKey.Length == 1 && next == console.ShortcutKey[0])
                {
                    // On this line the key types a whole word: keep as much of it as is wanted and take the rest back.
                    var keep = 0;
                    while (keep < to.Length && keep < want.Length && to[keep] == want[keep]) keep++;
                    if (keep <= line.Length || !to.StartsWith(line, StringComparison.Ordinal)) return null;
                    keys.Add((console.ShortcutKey, false));
                    for (var i = to.Length; i > keep; i--) keys.Add((console.Erase, false));
                    line = to.Substring(0, keep);
                    continue;
                }

                var key = KeyFor(console, next);
                if (key == null) return null;
                keys.Add(key.Value);
                line += next;
            }

            return keys;
        }

        static (string, bool)? KeyFor(ConsoleDefinition console, char c)
        {
            if (c >= 'A' && c <= 'Z') return (c.ToString(), false);
            foreach (var k in console.Keys)
            {
                if (k.Text.Length == 1 && k.Text[0] == c) return (k.Key, false);
                if (k.Shifted.Length == 1 && k.Shifted[0] == c) return (k.Key, true);
            }

            return null;
        }

        // ---- the tick: checkScripts (now called) and checkScripter's blink ---------------------------------------------

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            var now = tick * Core.Clock.TickSeconds;
            foreach (var pair in states)
            {
                var s = pair.Value;
                // The tick clock is exact; the epsilon keeps 30 ticks from falling a hair short of 0.5 s.
                if (s.ConsoleOpen && now + 1e-6 >= s.LastBeamTime + Data.Console.BlinkSeconds)
                {
                    s.Beam = !s.Beam;
                    s.LastBeamTime = now;
                }

                if (s.Running && s.targets.Count > 0) Steer(pair.Key, s, tick, events);
            }
        }

        /// <summary>
        /// checkScripts, as it was written to work: each target hero walks toward its point on each axis it is not yet
        /// within the arrive distance of (facing the way it walks, the vertical last, as MOVE set it); once every one is
        /// there on both axes they stand and the script stops running.
        /// </summary>
        void Steer(PlayerId player, ScriptState s, long tick, ICollection<IGameEvent> events)
        {
            var arrived = true;
            foreach (var t in s.targets)
            {
                var hero = Party.Heroes[t.Hero];
                var nearX = WorldCamera.Near(hero.X, t.X, Data.Arrive);
                var nearY = WorldCamera.Near(hero.Y, t.Y, Data.Arrive);
                hero.MoveRight = !nearX && hero.X < t.X;
                hero.MoveLeft = !nearX && hero.X > t.X;
                hero.MoveUp = !nearY && hero.Y < t.Y;
                hero.MoveDown = !nearY && hero.Y > t.Y;
                if (nearX && nearY) continue;
                arrived = false;
                if (!nearX) hero.Facing = hero.MoveRight ? Facing.Right : Facing.Left;
                if (!nearY) hero.Facing = hero.MoveUp ? Facing.Up : Facing.Down;
                if (hero.State != HeroState.Running) hero.State = HeroState.Walking;
            }

            if (!arrived) return;
            foreach (var t in s.targets)
            {
                var hero = Party.Heroes[t.Hero];
                hero.MoveLeft = hero.MoveRight = hero.MoveUp = hero.MoveDown = false;
                if (hero.State == HeroState.Walking || hero.State == HeroState.Running) hero.State = HeroState.Standing;
            }

            SetRunning(player, s, false, tick, events);
        }

        void SetRunning(PlayerId player, ScriptState s, bool running, long tick, ICollection<IGameEvent> events)
        {
            if (!running) s.targets.Clear();
            if (s.Running == running) return;
            s.Running = running;
            events.Add(new ScriptRunningChanged(tick, player, running));
        }

        // ---- Execute ----------------------------------------------------------------------------------------------

        /// <summary>A number or coordinate the script could not read: the original's exception ("Script failed to run.").</summary>
        sealed class ScriptException : Exception
        {
            public ScriptException(string message) : base(message) { }
        }

        /// <summary>One command's words.</summary>
        sealed class Command
        {
            public string Verb, Type, Item, Parameter;
        }

        /// <summary>What one run of <see cref="Execute"/> works with.</summary>
        sealed class RunContext
        {
            public Game Game;
            public PlayerId Player;
            public ScriptState State;
            public int Hero;
            public long Tick;
            public ICollection<IGameEvent> Events;
            public bool Error;
            public int? DraughtHour;
            public Command DraughtClock;
        }

        /// <summary>
        /// Script.Execute: runs <paramref name="script"/> for <paramref name="player"/> with HERO meaning
        /// <paramref name="hero"/>, reporting a failure on the message board and as <see cref="ScriptFailed"/>.
        /// </summary>
        public ScriptOutcome Execute(Game game, PlayerId player, int hero, string script, long tick, ICollection<IGameEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            var run = new RunContext { Game = game, Player = player, State = For(player), Hero = hero, Tick = tick, Events = events };
            ScriptOutcome outcome;
            try
            {
                var commands = Divide(script ?? string.Empty, run);
                FindDraught(commands, run);
                foreach (var c in commands) Do(c, run, 0);
                outcome = run.Error ? ScriptOutcome.Failed : ScriptOutcome.Ran;
            }
            catch (ScriptException)
            {
                outcome = ScriptOutcome.Crashed;
            }

            if (outcome != ScriptOutcome.Ran)
            {
                var crashed = outcome == ScriptOutcome.Crashed;
                Messages?.Add(crashed ? Data.CrashedText : Data.FailedText, Data.MessageFont, Data.MessageColour.R, Data.MessageColour.G, Data.MessageColour.B);
                events.Add(new ScriptFailed(tick, player, script, crashed));
            }

            events.Add(new ScriptRan(tick, player, script, outcome));
            return outcome;
        }

        /// <summary>
        /// divideScript for every command: upper-cased, split at " | ", each into its words. A command of neither four words
        /// nor two is an error and is skipped (the original reused the previous command's words, so an empty Enter ran the
        /// last item's effect again; BIBLE); a two-word command has no item or parameter.
        /// </summary>
        static List<Command> Divide(string script, RunContext run)
        {
            var list = new List<Command>();
            foreach (var part in script.ToUpperInvariant().Split(new[] { " | " }, StringSplitOptions.None))
            {
                var w = part.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (w.Length == 4) list.Add(new Command { Verb = w[0], Type = w[1], Item = w[2], Parameter = w[3] });
                else if (w.Length == 2) list.Add(new Command { Verb = w[0], Type = w[1], Item = string.Empty, Parameter = string.Empty });
                else run.Error = true;
            }

            return list;
        }

        /// <summary>
        /// The draughts (BIBLE): a script that lays a hero down (CHANGE HERO AI LAYDOWN) and sets the hour (CHANGE TIME HOUR
        /// h) is a sleep until that hour, the clock set when the party wakes rather than before it sleeps.
        /// </summary>
        void FindDraught(List<Command> commands, RunContext run)
        {
            if (Sleep == null) return;
            var lays = false;
            foreach (var c in commands)
                if (c.Verb == "CHANGE" && IsHero(c.Type) && c.Item == "AI" && (c.Parameter == "LAY" || c.Parameter == "LAYDOWN" || c.Parameter == "SLEEP"))
                    lays = true;
            if (!lays) return;
            foreach (var c in commands)
            {
                if (c.Verb != "CHANGE" || c.Type != "TIME" || (c.Item != "HOUR" && c.Item != "CLOCK")) continue;
                var p = c.Parameter;
                if (p.StartsWith("+", StringComparison.Ordinal) || p.StartsWith("-", StringComparison.Ordinal)) continue;
                if (p.StartsWith("=", StringComparison.Ordinal)) p = p.Substring(1);
                if (!TryInteger(p, out var hour) || hour < 0 || hour > 23) continue;
                run.DraughtHour = hour;
                run.DraughtClock = c;
            }
        }

        static bool IsHero(string type) => type == "HERO" || type == "HERO1" || type == "HERO2" || type == "HERO3";

        void Do(Command c, RunContext run, int depth)
        {
            switch (c.Verb)
            {
                case "CHANGE":
                    if (c.Item.Length == 0)
                    {
                        // A two-word CHANGE has no item: every handler's Case Else.
                        if (IsKnownType(c.Type)) run.Error = true;
                        return;
                    }

                    switch (c.Type)
                    {
                        case "HERO": case "HERO1": case "HERO2": case "HERO3": ChangeHero(c, run); break;
                        case "ENEMY": case "BADGUY": ChangeEnemy(c, run); break;
                        case "LIGHT": ChangeLight(c, run); break;
                        case "TIME": ChangeTime(c, run); break;
                        case "SCRIPT": ChangeScript(c, run); break;
                        case "CAMERA": case "CAM": ChangeCamera(c, run); break;
                        case "INVENTORY": ChangeInventory(c, run); break;
                        case "OPTIONS": ChangeOptions(c, run); break;
                        case "BOOK": ChangeBook(c, run); break;
                        case "CURSOR": ChangeCursor(c, run); break;
                    }

                    break;
                case "MOVE":
                    if (IsHero(c.Type))
                    {
                        if (c.Item.Length == 0) run.Error = true;
                        else MoveHero(c, run);
                    }

                    break;
                case "RUN":
                    // cmdRunScript: a named script, run in place; the commands after the RUN still run (the original's
                    // nested Execute overwrote the outer one's list and counter, so they were skipped; BIBLE).
                    if (depth < 8 && Data.Runs.TryGetValue(c.Type, out var named))
                        foreach (var inner in Divide(named, run)) Do(inner, run, depth + 1);
                    break;
            }
        }

        static bool IsKnownType(string type)
        {
            switch (type)
            {
                case "HERO": case "HERO1": case "HERO2": case "HERO3": case "ENEMY": case "BADGUY": case "LIGHT": case "TIME": case "SCRIPT":
                case "CAMERA": case "CAM": case "INVENTORY": case "OPTIONS": case "BOOK": case "CURSOR":
                    return true;
                default:
                    return false;
            }
        }

        int HeroOf(string type, RunContext run, bool current)
        {
            int index;
            switch (type)
            {
                case "HERO1": index = 0; break;
                case "HERO2": index = 1; break;
                case "HERO3": index = 2; break;
                default: index = current ? Party.LeaderOf(run.Player) : run.Hero; break;
            }

            if (index < 0 || index >= Party.Heroes.Count) throw new ScriptException("There is no hero " + index + ".");
            return index;
        }

        // ---- CHANGE HERO ------------------------------------------------------------------------------------------

        void ChangeHero(Command c, RunContext run)
        {
            var index = HeroOf(c.Type, run, false);
            var hero = Party.Heroes[index];
            var p = c.Parameter;
            switch (c.Item)
            {
                case "HP": case "HEALTH": case "LIFE":
                    Vital(index, 0, hero.Health, hero.MaxHealth, p, run);
                    break;
                case "MP": case "MANA": case "MAGIC":
                    Vital(index, 1, hero.Mana, hero.MaxMana, p, run);
                    break;
                case "AP": case "STAMINA": case "ENERGY":
                    Vital(index, 2, hero.Stamina, hero.MaxStamina, p, run);
                    break;
                case "X":
                case "Y":
                {
                    var add = IsArithmetic(ref p);
                    var value = Single(p);
                    var x = c.Item == "X" ? (add ? hero.X + value : value) : hero.X;
                    var y = c.Item == "Y" ? (add ? hero.Y + value : value) : hero.Y;
                    run.Events.Add(Party.Place(index, x, y, run.Tick));
                    break;
                }
                case "Z":
                {
                    var add = IsArithmetic(ref p);
                    var value = Single(p);
                    hero.Z = hero.PreviousZ = add ? hero.Z + value : value;
                    break;
                }
                case "FACING":
                    var facing = Direction(p);
                    if (facing.HasValue) hero.Facing = facing.Value;
                    break;
                case "STATUS":
                    HeroStatus? status = null;
                    switch (p)
                    {
                        case "EXHAUSTED": case "TIRED": status = HeroStatus.Exhausted; break;
                        case "ANGRY": case "ENRAGED": status = HeroStatus.Angry; break;
                        case "POISONED": case "POISON": status = HeroStatus.Poisoned; break;
                        case "FINE": status = HeroStatus.Fine; break;
                    }

                    if (status.HasValue) Party.SetVitals(index, null, null, null, status, run.Tick, run.Events);
                    break;
                case "AI":
                    ChangeAi(hero, p, run);
                    break;
                case "GOLD": case "MONEY":
                {
                    var add = IsArithmetic(ref p);
                    var amount = Integer(p);
                    var before = Inventory.Gold;
                    Inventory.Gold = add ? before + amount : amount;
                    run.Events.Add(new GoldChanged(run.Tick, run.Player, Inventory.Gold, before));
                    break;
                }
                default:
                    run.Error = true;
                    break;
            }
        }

        /// <summary>HP, MP or AP: an addition capped at the maximum and kept at 0 or more (BIBLE), or an assignment within 0 and the maximum.</summary>
        void Vital(int index, int which, int current, int max, string p, RunContext run)
        {
            var add = IsArithmetic(ref p);
            var amount = Integer(p);
            var value = add ? (int)Math.Max(0L, Math.Min((long)max, (long)current + amount)) : amount;
            Party.SetVitals(index, which == 0 ? value : (int?)null, which == 1 ? value : (int?)null, which == 2 ? value : (int?)null, null, run.Tick, run.Events);
        }

        static Facing? Direction(string word)
        {
            switch (word)
            {
                case "LEFT": return Facing.Left;
                case "RIGHT": return Facing.Right;
                case "UP": return Facing.Up;
                case "DOWN": return Facing.Down;
                default: return null;
            }
        }

        void ChangeAi(Hero hero, string p, RunContext run)
        {
            foreach (var (prefix, state) in new[] { ("FACE", HeroState.Standing), ("STAND", HeroState.Standing), ("WALK", HeroState.Walking), ("RUN", HeroState.Running) })
            {
                if (!p.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var facing = Direction(p.Substring(prefix.Length));
                if (!facing.HasValue) continue;
                if (state != HeroState.Standing)
                {
                    switch (facing.Value)
                    {
                        case Facing.Left: hero.MoveLeft = true; break;
                        case Facing.Right: hero.MoveRight = true; break;
                        case Facing.Up: hero.MoveUp = true; break;
                        default: hero.MoveDown = true; break;
                    }
                }

                hero.Facing = facing.Value;
                hero.State = state;
                SetRunning(run.Player, run.State, true, run.Tick, run.Events);
                return;
            }

            switch (p)
            {
                case "REST": case "SIT": case "KNEEL":
                    hero.Kneeling = true;
                    hero.State = HeroState.Kneeling;
                    break;
                case "LAY": case "LAYDOWN": case "SLEEP":
                    if (Sleep != null) run.Events.Add(Sleep.LayDown(run.Player, hero.Index, run.DraughtHour, run.Tick));
                    else
                    {
                        hero.Laying = true;
                        hero.State = HeroState.Kneeling;
                    }

                    break;
                case "STOP":
                    SetRunning(run.Player, run.State, false, run.Tick, run.Events);
                    break;
            }
        }

        // ---- the other CHANGE types -------------------------------------------------------------------------------

        /// <summary>cmdChangeEnemy: HEALTH and AI are stubs until the enemies come (the original's "Health" never matched the upper-cased script; BIBLE).</summary>
        static void ChangeEnemy(Command c, RunContext run)
        {
            if (c.Item != "HEALTH" && c.Item != "AI") run.Error = true;
        }

        /// <summary>
        /// cmdChangeLight: AMBIENT r,g,b sets the device ambient, +/-n adds to each channel; RED, GREEN and BLUE add or set
        /// one channel of the day's light. Every channel stays within 0 and 255 (the original's byte conversions threw on
        /// a negative number and on any first channel past 9; BIBLE).
        /// </summary>
        void ChangeLight(Command c, RunContext run)
        {
            var p = c.Parameter;
            switch (c.Item)
            {
                case "AMBIENT":
                {
                    if (Sleep == null) return;
                    var a = Sleep.Ambient;
                    if (p.IndexOf(',') >= 0)
                    {
                        var parts = p.Split(',');
                        if (parts.Length < 3) throw new ScriptException("AMBIENT wants r,g,b.");
                        Sleep.SetAmbient(new LightColour(Channel(Integer(parts[0])), Channel(Integer(parts[1])), Channel(Integer(parts[2]))));
                    }
                    else
                    {
                        IsArithmetic(ref p);
                        var n = Integer(p);
                        Sleep.SetAmbient(new LightColour(Channel(a.R + n), Channel(a.G + n), Channel(a.B + n)));
                    }

                    break;
                }
                case "RED": case "R": case "GREEN": case "G": case "BLUE": case "B":
                {
                    var add = IsArithmetic(ref p);
                    var n = Integer(p);
                    if (Clock == null) return;
                    var l = Clock.Light;
                    var which = c.Item[0];
                    int Value(byte now) => Channel(add ? now + n : n);
                    Clock.SetLight(new LightColour(
                        which == 'R' ? (byte)Value(l.R) : l.R,
                        which == 'G' ? (byte)Value(l.G) : l.G,
                        which == 'B' ? (byte)Value(l.B) : l.B));
                    break;
                }
                default:
                    run.Error = true;
                    break;
            }
        }

        static byte Channel(long value) => (byte)Math.Max(0L, Math.Min(255L, value));

        /// <summary>
        /// cmdChangeTime: HOUR / CLOCK sets the hour (the next day when earlier; an hour past 23 throws, as New Date did) or
        /// with +/-n moves the clock on that many hours (the original computed it and threw it away; BIBLE), the period and
        /// light settled either way; DAYPERIOD sets its hour, the light following on the clock's minutes.
        /// </summary>
        void ChangeTime(Command c, RunContext run)
        {
            var p = c.Parameter;
            switch (c.Item)
            {
                case "HOUR": case "CLOCK":
                {
                    if (ReferenceEquals(c, run.DraughtClock)) return; // the draught's sleep sets it when the party wakes
                    var add = IsArithmetic(ref p);
                    var n = Integer(p);
                    if (!add && (n < 0 || n > 23)) throw new ScriptException("An hour is 0 to 23.");
                    if (Clock == null) return;
                    if (add) Clock.Advance(run.Player, TimeSpan.FromHours(n), run.Tick, run.Events);
                    else Clock.SetHour(run.Player, n, null, true, run.Tick, run.Events);
                    break;
                }
                case "DAYPERIOD":
                    if (Clock != null && Data.DayPeriods.TryGetValue(p, out var hour)) Clock.SetHour(run.Player, hour, null, false, run.Tick, run.Events);
                    break;
                default:
                    run.Error = true;
                    break;
            }
        }

        /// <summary>cmdChangeScript: COLOR (the console's text) and RUNNING ON / OFF.</summary>
        void ChangeScript(Command c, RunContext run)
        {
            switch (c.Item)
            {
                case "COLOR":
                    if (Data.Colours.TryGetValue(c.Parameter, out var colour)) run.State.Colour = colour;
                    break;
                case "RUNNING":
                    if (c.Parameter == "ON") SetRunning(run.Player, run.State, true, run.Tick, run.Events);
                    else if (c.Parameter == "OFF") SetRunning(run.Player, run.State, false, run.Tick, run.Events);
                    break;
                default:
                    run.Error = true;
                    break;
            }
        }

        /// <summary>cmdChangeCamera: X, Y, Z add or set (the zoom held within its limits as every tick holds it); POS HERO centres it on the current hero.</summary>
        void ChangeCamera(Command c, RunContext run)
        {
            var p = c.Parameter;
            switch (c.Item)
            {
                case "X": case "Y": case "Z":
                {
                    var add = IsArithmetic(ref p);
                    var value = Single(p);
                    if (View == null) return;
                    var cam = View.For(run.Player).Camera;
                    float Set(float now) => add ? now + value : value;
                    cam.MoveTo(c.Item == "X" ? Set(cam.X) : cam.X, c.Item == "Y" ? Set(cam.Y) : cam.Y, c.Item == "Z" ? Set(cam.Z) : cam.Z);
                    break;
                }
                case "XY": case "XYZ": case "POS": case "POSITION":
                {
                    if (p != "HERO" || View == null) return;
                    var cam = View.For(run.Player).Camera;
                    var hero = Party.Leader(run.Player);
                    var size = Party.Data.HeroSize;
                    cam.MoveTo(hero.X + size / Data.CameraXDivisor, hero.Y - size / Data.CameraYDivisor, cam.Z);
                    break;
                }
                default:
                    run.Error = true;
                    break;
            }
        }

        static bool? OnOff(string p)
        {
            switch (p)
            {
                case "ON": case "TRUE": return true;
                case "OFF": case "FALSE": return false;
                default: return null;
            }
        }

        /// <summary>cmdChangeInventory: OPEN ON / TRUE opens the panel, OFF / FALSE shuts it; CLOSE the other way round.</summary>
        void ChangeInventory(Command c, RunContext run)
        {
            if (c.Item != "OPEN" && c.Item != "CLOSE")
            {
                run.Error = true;
                return;
            }

            var on = OnOff(c.Parameter);
            if (!on.HasValue || Pack == null) return;
            Pack.SetOpen(run.Player, c.Item == "OPEN" ? on.Value : !on.Value, run.Tick, run.Events);
        }

        /// <summary>cmdChangeOptions: as INVENTORY, for the options screen (recorded until story A20 draws it).</summary>
        void ChangeOptions(Command c, RunContext run)
        {
            if (c.Item != "OPEN" && c.Item != "CLOSE")
            {
                run.Error = true;
                return;
            }

            var on = OnOff(c.Parameter);
            if (!on.HasValue) return;
            run.State.OptionsOpen = c.Item == "OPEN" ? on.Value : !on.Value;
            run.Events.Add(new ScriptSettingChanged(run.Tick, run.Player, "OPTIONS " + c.Item));
        }

        /// <summary>
        /// cmdChangeBook on the player's book (<see cref="Books"/>): TO / TITLE / CURRENT ITEM loads the book named like the
        /// pack's selected stack, NUM / NUMBER n loads book n (one that is not a book stops the script: the original read
        /// past its tables; BIBLE), SHOW ON / OFF, NUMSYS / NUMBERING, COLOR / COVER and FONT / LETTERING; an unknown word
        /// for a known item does nothing, as before.
        /// </summary>
        void ChangeBook(Command c, RunContext run)
        {
            var p = c.Parameter;
            switch (c.Item)
            {
                case "TO": case "TITLE": case "CURRENT":
                    if (p != "ITEM" || Books == null) return;
                    // loadBook(items(currItem).Name): the selected stack's name.
                    var cursor = Pack != null ? Pack.For(run.Player).Cursor : 0;
                    var title = cursor >= 0 && cursor < Inventory.Items.Count ? Inventory.Items[cursor].Name : string.Empty;
                    Books.LoadTitle(run.Player, title, run.Tick, run.Events);
                    break;
                case "NUM": case "NUMBER":
                    var number = Integer(p);
                    if (Books != null && !Books.LoadNumber(run.Player, number, run.Tick, run.Events)) throw new ScriptException("There is no book " + number + ".");
                    break;
                case "SHOW":
                    var on = OnOff(p);
                    if (on.HasValue) Books?.SetShown(run.Player, on.Value, run.Tick, run.Events);
                    break;
                case "NUMSYS": case "NUMBERING":
                    BookNumbering numbering;
                    switch (p)
                    {
                        case "STANDARD": numbering = BookNumbering.Standard; break;
                        case "ROMAN": case "ROMANNUMERAL": case "ROMANUMERAL": numbering = BookNumbering.RomanNumeral; break;
                        case "ANCHIENT": numbering = BookNumbering.Ancient; break;
                        case "NONE": numbering = BookNumbering.None; break;
                        default: return;
                    }

                    Books?.SetNumbering(run.Player, numbering, run.Tick, run.Events);
                    break;
                case "COLOR": case "COVER":
                    Books?.SetCover(run.Player, p, run.Tick, run.Events);
                    break;
                case "FONT": case "LETTERING":
                    Books?.SetLettering(run.Player, p, run.Tick, run.Events); // SYMBOL, or any other word, does nothing
                    break;
                default:
                    run.Error = true;
                    return;
            }
        }

        /// <summary>
        /// cmdChangeCursor POS CENTER: the virtual mouse to the screen's centre and the world cursor to the camera's (the
        /// original wrote the centre into the mouse's last motion, which the next poll overwrote, so it did nothing; BIBLE).
        /// </summary>
        void ChangeCursor(Command c, RunContext run)
        {
            switch (c.Item)
            {
                case "POS": case "POSITION": case "XY": case "XYZ":
                    if (c.Parameter != "CENTER" || View == null) return;
                    var v = View.For(run.Player);
                    var def = v.Cursor.Definition;
                    v.Cursor.MouseX = def.ScreenWidth / 2f;
                    v.Cursor.MouseY = def.ScreenHeight / 2f;
                    v.Cursor.X = v.Camera.X;
                    v.Cursor.Y = v.Camera.Y;
                    v.Cursor.Settle();
                    break;
                default:
                    run.Error = true;
                    break;
            }
        }

        /// <summary>cmdMoveHero TO x,y,z: the hero (HERO is the current one) walks toward the point and the script runs until it arrives; AWAY does nothing.</summary>
        void MoveHero(Command c, RunContext run)
        {
            var index = HeroOf(c.Type, run, true);
            var parts = c.Parameter.Split(',');
            if (parts.Length < 3) throw new ScriptException("MOVE wants x,y,z.");
            var x = Single(parts[0]);
            var y = Single(parts[1]);
            Single(parts[2]);
            switch (c.Item)
            {
                case "AWAY": case "AWAYFROM":
                    break;
                case "TO":
                    var hero = Party.Heroes[index];
                    if (hero.X < x) { hero.MoveRight = true; hero.Facing = Facing.Right; }
                    if (hero.X > x) { hero.MoveLeft = true; hero.Facing = Facing.Left; }
                    if (hero.Y < y) { hero.MoveUp = true; hero.Facing = Facing.Up; }
                    if (hero.Y > y) { hero.MoveDown = true; hero.Facing = Facing.Down; }
                    hero.State = HeroState.Walking;
                    run.State.targets.RemoveAll(t => t.Hero == index);
                    run.State.targets.Add(new MoveTarget(index, x, y));
                    SetRunning(run.Player, run.State, true, run.Tick, run.Events);
                    break;
                default:
                    run.Error = true;
                    break;
            }
        }

        // ---- numbers (isArithmetic, CInt, CSng) --------------------------------------------------------------------

        /// <summary>isArithmetic: a leading = is stripped and assigns; a leading + or - adds; anything else assigns.</summary>
        static bool IsArithmetic(ref string parameter)
        {
            if (parameter.Length == 0) throw new ScriptException("No parameter.");
            if (parameter[0] == '=')
            {
                parameter = parameter.Substring(1);
                return false;
            }

            return parameter[0] == '+' || parameter[0] == '-';
        }

        static bool TryInteger(string text, out int value)
        {
            value = 0;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return false;
            d = Math.Round(d, MidpointRounding.ToEven);
            if (d < int.MinValue || d > int.MaxValue) return false;
            value = (int)d;
            return true;
        }

        /// <summary>CInt: a number rounded half to even; anything else throws.</summary>
        static int Integer(string text)
        {
            if (!TryInteger(text, out var value)) throw new ScriptException("\"" + text + "\" is not a whole number.");
            return value;
        }

        /// <summary>CSng: a number; anything else throws.</summary>
        static float Single(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || double.IsInfinity((float)d))
                throw new ScriptException("\"" + text + "\" is not a number.");
            return (float)d;
        }
    }
}

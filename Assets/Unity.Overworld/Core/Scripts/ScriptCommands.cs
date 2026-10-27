using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// Runs a script for a player on a hero (the item scripts, the gold's, the Rune's, the quests' end scripts): true when
    /// it ran. A runner reports its own failure (<see cref="ScriptFailed"/> and its message), so the caller only decides
    /// what a failed script means for it (an item that stays in the pack).
    /// </summary>
    public interface IScriptRunner
    {
        bool Run(Game game, PlayerId player, int hero, string script, long tick, ICollection<IGameEvent> events);
    }

    /// <summary>How a script ended (Script.Execute).</summary>
    public enum ScriptOutcome
    {
        /// <summary>Every command was understood and done.</summary>
        Ran,

        /// <summary>A command was not understood (scriptError): "Script failed to execute." The rest still ran.</summary>
        Failed,

        /// <summary>A command threw (a number that is not one, a missing coordinate): "Script failed to run." The rest did not run.</summary>
        Crashed,
    }

    /// <summary>
    /// Runs <see cref="Script"/> for <see cref="Player"/>, as Enter in the console did (and the scripted runs): HERO is
    /// <see cref="Hero"/>, or the player's item target (selectedHero) when null.
    /// </summary>
    public sealed class RunScript : ICommand
    {
        public RunScript(PlayerId player, string script, int? hero = null) { Player = player; Script = script; Hero = hero; }
        public PlayerId Player { get; }
        public string Script { get; }
        public int? Hero { get; }
    }

    /// <summary>
    /// One key press for the script console (frmMain.OnKeyDown in scripter mode, or the two keys that open it), by the
    /// WinForms key name the original tested ("Oemtilde", "Insert", "C", "D5", "OemPipe", "Enter" ...), with whether
    /// Shift is held. A closed console takes only its open and recall keys; the game has the others.
    /// </summary>
    public sealed class PressConsoleKey : ICommand
    {
        public PressConsoleKey(PlayerId player, string key, bool shift = false) { Player = player; Key = key; Shift = shift; }
        public PlayerId Player { get; }
        public string Key { get; }
        public bool Shift { get; }
    }

    /// <summary>A script ran (whatever its <see cref="Outcome"/>).</summary>
    public sealed class ScriptRan : IGameEvent
    {
        public ScriptRan(long tick, PlayerId player, string script, ScriptOutcome outcome) { Tick = tick; Player = player; Script = script; Outcome = outcome; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public string Script { get; }
        public ScriptOutcome Outcome { get; }
    }

    /// <summary>
    /// A script did not run cleanly: "Script failed to execute." (a command not understood), or with
    /// <see cref="Crashed"/> "Script failed to run." (it stopped on an exception).
    /// </summary>
    public sealed class ScriptFailed : IGameEvent
    {
        public ScriptFailed(long tick, PlayerId player, string script, bool crashed = false) { Tick = tick; Player = player; Script = script; Crashed = crashed; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public string Script { get; }
        public bool Crashed { get; }
    }

    /// <summary>The console opened, closed, or its line changed.</summary>
    public sealed class ConsoleChanged : IGameEvent
    {
        public ConsoleChanged(long tick, PlayerId player, bool open, string line) { Tick = tick; Player = player; Open = open; Line = line; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Open { get; }
        public string Line { get; }
    }

    /// <summary>A player's script started or stopped running (bScriptRunning): while it runs, that player's input is not read.</summary>
    public sealed class ScriptRunningChanged : IGameEvent
    {
        public ScriptRunningChanged(long tick, PlayerId player, bool running) { Tick = tick; Player = player; Running = running; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Running { get; }
    }

    /// <summary>A script changed an options setting (CHANGE OPTIONS; the menu itself raises <see cref="OptionsChanged"/>).</summary>
    public sealed class ScriptSettingChanged : IGameEvent
    {
        public ScriptSettingChanged(long tick, PlayerId player, string setting) { Tick = tick; Player = player; Setting = setting; }
        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The command's type and item ("OPTIONS OPEN").</summary>
        public string Setting { get; }
    }

    /// <summary>A hero a MOVE HERO TO script walks, and the point it walks to.</summary>
    public readonly struct MoveTarget
    {
        public MoveTarget(int hero, float x, float y) { Hero = hero; X = x; Y = y; }
        public int Hero { get; }
        public float X { get; }
        public float Y { get; }
    }

    /// <summary>One player's scripting: the console, the script running, and the settings scripts recorded.</summary>
    public sealed class ScriptState
    {
        internal ScriptState(LightColour colour) { Colour = colour; }

        /// <summary>bScripter: the console is open.</summary>
        public bool ConsoleOpen { get; internal set; }

        /// <summary>theScript: the line being typed.</summary>
        public string Line { get; internal set; } = string.Empty;

        /// <summary>lastScript: the last line run from the console (Insert recalls it).</summary>
        public string Last { get; internal set; } = string.Empty;

        /// <summary>IBeam: the prompt's I-beam is showing.</summary>
        public bool Beam { get; internal set; }

        internal double LastBeamTime;

        /// <summary>scriptColor: the console's text colour.</summary>
        public LightColour Colour { get; internal set; }

        /// <summary>bScriptRunning: a script moves a hero; the player's input is not read.</summary>
        public bool Running { get; internal set; }

        internal readonly List<MoveTarget> targets = new List<MoveTarget>();

        /// <summary>The MOVE HERO TO targets the running script ends at.</summary>
        public IReadOnlyList<MoveTarget> Targets => targets;
    }
}

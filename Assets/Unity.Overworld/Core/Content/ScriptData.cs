using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A key of the console that types text: its text, and the text with Shift held.</summary>
    public sealed class ConsoleKeyText
    {
        public string Key { get; internal set; }
        public string Text { get; internal set; }

        /// <summary>The text with Shift held (the plain text when the key has none).</summary>
        public string Shifted { get; internal set; }
    }

    /// <summary>
    /// The script console (frmMain's scripter mode and Script.checkScripter, RECONSTRUCTION 9.3): where and how its
    /// prompt is written, how fast it blinks, and what each key does, by the WinForms key name the original tested.
    /// </summary>
    public sealed class ConsoleDefinition
    {
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public HudFont Font { get; internal set; }

        /// <summary>scriptColor until CHANGE SCRIPT COLOR changes it.</summary>
        public LightColour Colour { get; internal set; }

        /// <summary>How long the I-beam stays on or off (0.5 s).</summary>
        public float BlinkSeconds { get; internal set; }

        /// <summary>The prompt with the I-beam on ("/ > {0}|") and off ("\ > {0} ").</summary>
        public string BeamOn { get; internal set; }

        public string BeamOff { get; internal set; }

        public string Open { get; internal set; }
        public string Recall { get; internal set; }
        public IReadOnlyList<string> Close { get; internal set; }
        public string Clear { get; internal set; }
        public string Erase { get; internal set; }
        public string Run { get; internal set; }
        public IReadOnlyList<string> Ignore { get; internal set; }

        /// <summary>The key that types the shortcuts ("C").</summary>
        public string ShortcutKey { get; internal set; }

        /// <summary>The shortcut key's steps: on a line that is exactly the first, it becomes the second.</summary>
        public IReadOnlyList<(string From, string To)> Shortcuts { get; internal set; }

        public IReadOnlyList<ConsoleKeyText> Keys { get; internal set; }

        /// <summary>The key that types <paramref name="key"/>'s text, or null.</summary>
        public ConsoleKeyText Typing(string key)
        {
            foreach (var k in Keys) if (k.Key == key) return k;
            return null;
        }

        /// <summary>The prompt for <paramref name="line"/> with the I-beam on or off.</summary>
        public string Prompt(string line, bool beam) => string.Format(System.Globalization.CultureInfo.InvariantCulture, beam ? BeamOn : BeamOff, line ?? string.Empty);
    }

    /// <summary>
    /// The script language's numbers, tables and texts (scripts.json; Script.vb, RECONSTRUCTION 9): the console, the two
    /// failure messages, CHANGE SCRIPT COLOR's colours, CHANGE TIME DAYPERIOD's hours, RUN's named scripts, where CHANGE
    /// CAMERA POS HERO puts the camera and how near a MOVE HERO TO must come.
    /// </summary>
    public sealed class ScriptData
    {
        public ConsoleDefinition Console { get; internal set; }

        /// <summary>"Script failed to execute.": a command was not understood.</summary>
        public string FailedText { get; internal set; }

        /// <summary>"Script failed to run.": the script stopped on an exception.</summary>
        public string CrashedText { get; internal set; }

        public HudFont MessageFont { get; internal set; }
        public LightColour MessageColour { get; internal set; }

        /// <summary>CHANGE SCRIPT COLOR's colours by name.</summary>
        public IReadOnlyDictionary<string, LightColour> Colours { get; internal set; }

        /// <summary>CHANGE TIME DAYPERIOD's hours by name.</summary>
        public IReadOnlyDictionary<string, int> DayPeriods { get; internal set; }

        /// <summary>RUN's scripts by name.</summary>
        public IReadOnlyDictionary<string, string> Runs { get; internal set; }

        /// <summary>CHANGE CAMERA POS HERO: the camera at the hero plus heroSize / this across ...</summary>
        public float CameraXDivisor { get; internal set; }

        /// <summary>... and less heroSize / this up.</summary>
        public float CameraYDivisor { get; internal set; }

        /// <summary>A MOVE HERO TO ends within this of its target on both axes.</summary>
        public float Arrive { get; internal set; }

        internal static ScriptData Read(IContentSource source, string file, HudData hud, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var fonts = hud?.Fonts ?? Array.Empty<HudFont>();
            var data = new ScriptData();

            var c = root.Section("console");
            var console = new ConsoleDefinition
            {
                X = c.Number("x", 0f, hud?.ScreenWidth ?? 10000),
                Y = c.Number("y", 0f, hud?.ScreenHeight ?? 10000),
                Font = ReadFont(c, fonts),
                Colour = Colour(c, "colour"),
                BlinkSeconds = c.Positive("blinkSeconds"),
                BeamOn = c.Text("beamOn"),
                BeamOff = c.Text("beamOff"),
                Open = c.Text("open"),
                Recall = c.Text("recall"),
                Close = c.Texts("close"),
                Clear = c.Text("clear"),
                Erase = c.Text("erase"),
                Run = c.Text("run"),
                Ignore = c.Texts("ignore"),
            };
            c.Check(console.BeamOn.Contains("{0}"), "beamOn", "must hold {0} for the line typed");
            c.Check(console.BeamOff.Contains("{0}"), "beamOff", "must hold {0} for the line typed");
            var shortcut = c.Section("shortcut");
            console.ShortcutKey = shortcut.Text("key");
            var steps = new List<(string, string)>();
            foreach (var s in shortcut.Objects("steps"))
            {
                steps.Add((s.Text("from", allowEmpty: true), s.Text("to")));
                s.Done();
            }

            shortcut.Done();
            console.Shortcuts = steps;
            var keys = new List<ConsoleKeyText>();
            foreach (var k in c.Objects("keys"))
            {
                var key = new ConsoleKeyText { Key = k.Text("key"), Text = k.Text("text") };
                key.Shifted = k.Has("shifted") ? k.Text("shifted") : key.Text;
                if (keys.Exists(o => o.Key == key.Key)) k.Fail("key", "\"" + key.Key + "\" is listed twice");
                k.Done();
                keys.Add(key);
            }

            console.Keys = keys;
            c.Done();
            data.Console = console;

            var m = root.Section("messages");
            data.FailedText = m.Text("failed");
            data.CrashedText = m.Text("crashed");
            data.MessageFont = ReadFont(m, fonts);
            data.MessageColour = Colour(m, "colour");
            m.Done();

            var colours = new Dictionary<string, LightColour>(StringComparer.Ordinal);
            foreach (var r in root.Objects("colours"))
            {
                var name = r.Text("name");
                if (colours.ContainsKey(name)) r.Fail("name", "\"" + name + "\" is listed twice");
                colours[name] = Colour(r, "colour");
                r.Done();
            }

            data.Colours = colours;

            var periods = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in root.Objects("dayPeriods"))
            {
                var name = r.Text("name");
                if (periods.ContainsKey(name)) r.Fail("name", "\"" + name + "\" is listed twice");
                periods[name] = r.Integer("hour", 0, 23);
                r.Done();
            }

            data.DayPeriods = periods;

            var runs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in root.Objects("run"))
            {
                var name = r.Text("name");
                var script = r.Text("script");
                if (runs.ContainsKey(name)) r.Fail("name", "\"" + name + "\" is listed twice");
                var problem = ScriptSyntax.Check(script);
                if (problem != null) r.Fail("script", problem);
                runs[name] = script;
                r.Done();
            }

            data.Runs = runs;

            var camera = root.Section("camera");
            data.CameraXDivisor = camera.Positive("xDivisor");
            data.CameraYDivisor = camera.Positive("yDivisor");
            camera.Done();

            var move = root.Section("move");
            data.Arrive = move.Positive("arrive");
            move.Done();
            root.Done();

            // RUN calls a script by name; one that calls itself (or another that calls it back) would never end.
            foreach (var run in runs)
                if (Calls(run.Value, run.Key, runs, new HashSet<string>()))
                    errors.Add(file + ".run: \"" + run.Key + "\" runs itself");
            return data;
        }

        static bool Calls(string script, string name, IReadOnlyDictionary<string, string> runs, HashSet<string> seen)
        {
            foreach (var part in script.ToUpperInvariant().Split('|'))
            {
                var words = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 2 || words[0] != "RUN") continue;
                if (words[1] == name) return true;
                if (seen.Add(words[1]) && runs.TryGetValue(words[1], out var next) && Calls(next, name, runs, seen)) return true;
            }

            return false;
        }

        static LightColour Colour(ContentReader r, string key)
        {
            var c = TimeData.ReadBytes(r, key, 3, "an [r, g, b] colour of three whole numbers from 0 to 255");
            return c == null ? new LightColour(255, 255, 255) : new LightColour(c[0], c[1], c[2]);
        }

        static HudFont ReadFont(ContentReader r, IReadOnlyList<HudFont> fonts)
        {
            var name = r.Text("font");
            foreach (var f in fonts) if (f.Name == name) return f;
            if (name.Length > 0) r.Fail("font", "\"" + name + "\" is not one of hud.json's fonts");
            return null;
        }
    }
}

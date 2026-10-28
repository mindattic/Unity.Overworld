using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>The gamepad's buttons (Input.vb's btn* map, RECONSTRUCTION 5.5): each one acts as its keyboard key does.</summary>
    public enum GamepadButton
    {
        /// <summary>btnRun (button 0, A): held, as Right Ctrl or Space (actionRun).</summary>
        Run,

        /// <summary>btnDrop (button 1, B): pressed, as Backspace (actionDrop).</summary>
        Drop,

        /// <summary>btnPickup (button 3, X): held, as Enter (actionPickup: pick up, use, sleep, the ring's and options' choice).</summary>
        PickUp,

        /// <summary>btnInventory (button 4, Y): pressed, as Delete (actionInventory).</summary>
        Inventory,

        /// <summary>btnRest (button 8, START): pressed, as \ (actionRest).</summary>
        Rest,

        /// <summary>btnNextHero (button 9, L2): pressed, as ] (nextHero).</summary>
        NextHero,

        /// <summary>btnLastHero (button 10, R2): pressed, as [ (lastHero).</summary>
        LastHero,
    }

    /// <summary>
    /// The gamepad (U.OVW-US-A21; RECONSTRUCTION 5.5; BIBLE "Gamepad"), gamepad.json: whether it is read at the start
    /// (bJoystick, True; J flips it), how far a stick must lean before it counts as an arrow (joyOutsideDeadZone 250 of the
    /// +/-1000 range set in initInput: 0.25), the sticks and pads that are the arrows, and the controls each of the
    /// original's buttons is on. The controls are Input System control paths, kept here as plain text: Core never reads a
    /// device.
    /// </summary>
    public sealed class GamepadData
    {
        /// <summary>The gamepad is read from the start (bJoystick starts True); J turns it off and on.</summary>
        public bool Enabled { get; internal set; }

        /// <summary>A stick or pad direction counts as its arrow once it leans this far, 0 to 1 (250 / 1000).</summary>
        public float Threshold { get; internal set; }

        /// <summary>The two-axis controls that are the arrow keys (joyLeft / joyRight / joyUp / joyDown): the left stick and the pad.</summary>
        public IReadOnlyList<string> Sticks { get; internal set; }

        readonly Dictionary<GamepadButton, IReadOnlyList<string>> buttons = new Dictionary<GamepadButton, IReadOnlyList<string>>();

        /// <summary>The controls that are <paramref name="button"/> (empty: none).</summary>
        public IReadOnlyList<string> Controls(GamepadButton button) =>
            buttons.TryGetValue(button, out var list) ? list : Array.Empty<string>();

        internal static GamepadData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var data = new GamepadData
            {
                Enabled = root.Flag("enabled"),
                Threshold = root.Number("threshold", 0.01f, 1f),
            };

            data.Sticks = Paths(root, "sticks");

            var map = root.Section("buttons");
            foreach (GamepadButton b in Enum.GetValues(typeof(GamepadButton)))
            {
                var key = char.ToLowerInvariant(b.ToString()[0]) + b.ToString().Substring(1);
                data.buttons[b] = Paths(map, key);
            }

            map.Done();
            root.Done();
            return data;
        }

        static IReadOnlyList<string> Paths(ContentReader r, string key)
        {
            var list = r.Texts(key);
            if (list.Count == 0) r.Fail(key, "must name at least one control");
            foreach (var p in list)
                if (p.Length < 3 || p[0] != '<' || p.IndexOf(">/", StringComparison.Ordinal) < 0)
                    r.Fail(key, "\"" + p + "\" is not a control path (<Device>/control)");
            return list;
        }
    }
}

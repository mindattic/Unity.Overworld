using System.Collections;
using System.Globalization;
using MindAttic.Overworld.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PadButton = UnityEngine.InputSystem.LowLevel.GamepadButton;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The gamepad (U.OVW-US-A21; BIBLE "Gamepad"): the player's input reads it as gamepad.json maps it, on from the start
    /// (J turns it off and on). For scripted runs, <c>-overworld-gamepad &lt;x&gt;,&lt;y&gt;[,&lt;control&gt;...]</c> plugs in
    /// a virtual pad holding its left stick at (x, y) (-1 to 1, y up) and the named controls down (buttonSouth,
    /// buttonEast, buttonWest, buttonNorth, start, leftTrigger, rightTrigger...) for the whole run, reads only that pad,
    /// and logs the leader and the pack a second and a half in, so the real bindings are seen working without a pad.
    /// </summary>
    public sealed partial class OverworldBootstrap
    {
        public const string GamepadArgument = "-overworld-gamepad";

        void InstallGamepad(string[] args)
        {
            Input.UseGamepad(Content.Gamepad);
            var spec = ArgumentAfter(args, GamepadArgument);
            if (spec == null) return;

            var parts = spec.Split(',');
            if (parts.Length < 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                Debug.LogError("Unity.Overworld: " + GamepadArgument + " wants x,y[,control...], not '" + spec + "'.");
                return;
            }

            var state = new GamepadState { leftStick = new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f)) };
            for (var i = 2; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (name.Length == 0) continue;
                // gamepad.json's control names (buttonSouth) are the low-level button names without "button" (South).
                if (name.Length > 6 && name.StartsWith("button", System.StringComparison.OrdinalIgnoreCase)) name = name.Substring(6);
                if (string.Equals(name, "leftTrigger", System.StringComparison.OrdinalIgnoreCase)) state.leftTrigger = 1f;
                else if (string.Equals(name, "rightTrigger", System.StringComparison.OrdinalIgnoreCase)) state.rightTrigger = 1f;
                else if (System.Enum.TryParse<PadButton>(name, true, out var button)) state = state.WithButton(button);
                else Debug.LogError("Unity.Overworld: " + GamepadArgument + " does not know the control '" + parts[i].Trim() + "'.");
            }

            // The pad must answer although the window may not have the focus.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var pad = InputSystem.AddDevice<Gamepad>("OverworldScriptedGamepad");
            InputSystem.QueueStateEvent(pad, state);
            Input.UseDevices = false;
            Input.ScriptedGamepad = true;
            Debug.Log("Unity.Overworld: scripted gamepad " + spec + " (gamepad " + (Input.GamepadOn ? "on" : "off") + ").");
            StartCoroutine(ReportGamepad(spec));
        }

        IEnumerator ReportGamepad(string spec)
        {
            var leader = Party.LeaderOf(PlayerId.One);
            var hero = Party.Heroes[leader];
            float x0 = hero.X, y0 = hero.Y;
            yield return new WaitForSecondsRealtime(1.5f);
            leader = Party.LeaderOf(PlayerId.One);
            hero = Party.Heroes[leader];
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "Unity.Overworld: gamepad {0}: leader {1} ({2}) moved ({3:0.###}, {4:0.###}), state {5}, running {6}, kneeling {7}, laying {8}, pack {9}.",
                spec, leader, hero.Definition.Name, hero.X - x0, hero.Y - y0, hero.State, hero.Running, hero.Kneeling, hero.Laying,
                Inventory.For(PlayerId.One).Open ? "open" : "shut"));
        }
    }
}

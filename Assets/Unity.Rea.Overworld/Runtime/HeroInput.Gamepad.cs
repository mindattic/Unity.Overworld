using System;
using MindAttic.Overworld.Core;
using UnityEngine.InputSystem;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The gamepad (U.OVW-US-A21; RECONSTRUCTION 5.5; BIBLE "Gamepad"), from gamepad.json: getJoystick ran beside
    /// getKeyboard and called the very same actions, so the pad here only adds to what the keys hold and press, on the
    /// same commands. The left stick (and the pad) past the threshold is the arrows; A is run, X pick up (Enter: it also
    /// sleeps, chooses from the ring and works the options menu), Y the inventory, START rest, B drop, L2 / R2 the next /
    /// previous hero. J (a frmMain key: with the console shut, even while a script runs or the game is paused) turns the
    /// pad off and on; it starts as gamepad.json says (on, as bJoystick did). Nothing is read from it while the input is
    /// held, locked or paused, exactly as the keys.
    /// </summary>
    public sealed partial class HeroInput
    {
        static readonly GamepadButton[] PadButtons = (GamepadButton[])Enum.GetValues(typeof(GamepadButton));

        InputAction joystickKey;
        InputAction padLeft, padRight, padUp, padDown;
        readonly InputAction[] padButtons = new InputAction[PadButtons.Length];

        // This frame's and the last frame's held state: the arrows (left, right, up, down) then the buttons.
        readonly bool[] padHeld = new bool[4 + PadButtons.Length];
        readonly bool[] padWasHeld = new bool[4 + PadButtons.Length];

        /// <summary>The map and threshold (null: no gamepad is read).</summary>
        public GamepadData GamepadMap { get; private set; }

        /// <summary>bJoystick: the pad is read (J flips it).</summary>
        public bool GamepadOn { get; set; }

        /// <summary>Scripted runs: the pad is read although the keyboard and mouse are not (the run's virtual pad).</summary>
        public bool ScriptedGamepad { get; set; }

        /// <summary>The pad is read this frame: on, a map given, and devices (or a scripted pad) read.</summary>
        bool PadReadable => GamepadOn && GamepadMap != null && (UseDevices || ScriptedGamepad);

        /// <summary>Gives the pad its map (called once, before the first frame) and turns it on as the map says.</summary>
        public void UseGamepad(GamepadData map)
        {
            DisposeGamepad();
            GamepadMap = map;
            GamepadOn = map != null && map.Enabled;
            if (map == null) return;
            padLeft = Axis("PadLeft", map, "left");
            padRight = Axis("PadRight", map, "right");
            padUp = Axis("PadUp", map, "up");
            padDown = Axis("PadDown", map, "down");
            for (var i = 0; i < PadButtons.Length; i++)
            {
                var b = PadButtons[i];
                var action = new InputAction("Pad" + b, InputActionType.Button);
                foreach (var path in map.Controls(b)) action.AddBinding(path);
                padButtons[i] = action;
            }

            if (isActiveAndEnabled) foreach (var a in PadActions()) a.Enable();
        }

        /// <summary>A direction of every stick, as a value (its lean, 0 to 1) so the threshold, not the press point, decides.</summary>
        static InputAction Axis(string name, GamepadData map, string direction)
        {
            var action = new InputAction(name, InputActionType.Value, expectedControlType: "Axis");
            foreach (var stick in map.Sticks) action.AddBinding(stick + "/" + direction);
            return action;
        }

        System.Collections.Generic.IEnumerable<InputAction> PadActions()
        {
            if (padLeft == null) yield break;
            yield return padLeft;
            yield return padRight;
            yield return padUp;
            yield return padDown;
            foreach (var a in padButtons) if (a != null) yield return a;
        }

        void EnableGamepad()
        {
            joystickKey ??= Button("Joystick", "<Keyboard>/j");
            joystickKey.Enable();
            foreach (var a in PadActions()) a.Enable();
        }

        void DisableGamepad()
        {
            joystickKey?.Disable();
            foreach (var a in PadActions()) a.Disable();
        }

        void DisposeGamepad()
        {
            foreach (var a in PadActions()) a.Dispose();
            padLeft = padRight = padUp = padDown = null;
            for (var i = 0; i < padButtons.Length; i++) padButtons[i] = null;
        }

        /// <summary>frmMain's J: joystickToggle = Not joystickToggle. The held pad is let go at once.</summary>
        void JoystickKey()
        {
            if (joystickKey != null && joystickKey.WasPressedThisFrame()) GamepadOn = !GamepadOn;
        }

        /// <summary>Reads the pad once a frame, first thing: what is held now and what was held the frame before.</summary>
        void PollGamepad()
        {
            Array.Copy(padHeld, padWasHeld, padHeld.Length);
            var on = PadReadable;
            var t = GamepadMap?.Threshold ?? 1f;
            padHeld[0] = on && padLeft.ReadValue<float>() >= t;
            padHeld[1] = on && padRight.ReadValue<float>() >= t;
            padHeld[2] = on && padUp.ReadValue<float>() >= t;
            padHeld[3] = on && padDown.ReadValue<float>() >= t;
            for (var i = 0; i < PadButtons.Length; i++) padHeld[4 + i] = on && padButtons[i].IsPressed();
        }

        bool PadHeld(GamepadButton b) => padHeld[4 + (int)b];
        bool PadPressed(GamepadButton b) => padHeld[4 + (int)b] && !padWasHeld[4 + (int)b];

        bool PadLeftHeld => padHeld[0];
        bool PadRightHeld => padHeld[1];
        bool PadUpHeld => padHeld[2];
        bool PadDownHeld => padHeld[3];
        bool PadLeftPressed => padHeld[0] && !padWasHeld[0];
        bool PadUpPressed => padHeld[2] && !padWasHeld[2];
        bool PadDownPressed => padHeld[3] && !padWasHeld[3];

        /// <summary>The pad's pressed buttons that are their keys' commands (getJoystick's Select Case on a pushed button).</summary>
        void GamepadButtons()
        {
            if (PadPressed(GamepadButton.LastHero)) Game.Submit(new CycleLeader(Player, -1));
            if (PadPressed(GamepadButton.NextHero)) Game.Submit(new CycleLeader(Player, 1));
            if (PadPressed(GamepadButton.Rest)) Game.Submit(new Rest(Player));
            if (PadPressed(GamepadButton.Inventory)) Game.Submit(new ToggleInventory(Player));
            if (PadPressed(GamepadButton.Drop)) Game.Submit(new DropItem(Player));
        }
    }
}

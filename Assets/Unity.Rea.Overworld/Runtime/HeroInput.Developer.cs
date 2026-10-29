using MindAttic.Overworld.Core;
using UnityEngine.InputSystem;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The developer tools' keys (U.OVW-T47; RECONSTRUCTION 5.1 and 5.2; BIBLE "Debug keys"), read only while the tools are
    /// on (<see cref="Developer"/> set and enabled): frmMain's F1 wireframe, F2 textures, F3 back-face culling, F5 filter,
    /// G / H one ring icon fewer / more, U the leader's lone mode flip, L the test message, P the path mode and O the path
    /// clear (with the console shut and the game not paused), and getInput's - / = game speed, every frame either is held
    /// (not while paused, a script runs or the console is open). R (Reset) waits for the Reset story (A24).
    /// </summary>
    public sealed partial class HeroInput
    {
        /// <summary>The developer tools (null or off: none of these keys is read).</summary>
        public DeveloperTools Developer { get; set; }

        bool DeveloperOn => Developer != null && Developer.Enabled;

        void DeveloperKeys()
        {
            var k = Keyboard.current;
            if (!DeveloperOn || k == null) return;
            if (k.f1Key.wasPressedThisFrame) Game.Submit(new ToggleRenderSwitch(Player, RenderSwitch.Wireframe));
            if (k.f2Key.wasPressedThisFrame) Game.Submit(new ToggleRenderSwitch(Player, RenderSwitch.Textures));
            if (k.f3Key.wasPressedThisFrame) Game.Submit(new ToggleRenderSwitch(Player, RenderSwitch.BackFaceCulling));
            if (k.f5Key.wasPressedThisFrame) Game.Submit(new ToggleRenderSwitch(Player, RenderSwitch.LinearFilter));
            if (k.gKey.wasPressedThisFrame) Game.Submit(new ResizeRing(Player, -1));
            if (k.hKey.wasPressedThisFrame) Game.Submit(new ResizeRing(Player, 1));
            if (k.uKey.wasPressedThisFrame) Game.Submit(new ToggleHeroMode(Player));
            if (k.lKey.wasPressedThisFrame) Game.Submit(new ShowTestMessage(Player));
            if (k.pKey.wasPressedThisFrame) Game.Submit(new CyclePathMode(Player));
            if (k.oKey.wasPressedThisFrame) Game.Submit(new ClearPath(Player));
        }

        /// <summary>getInput's held - then = (minus first, so holding both comes back to where it was, floor aside).</summary>
        void DeveloperSpeedKeys()
        {
            var k = Keyboard.current;
            if (!DeveloperOn || k == null) return;
            if (k.minusKey.isPressed) Game.Submit(new ChangeGameSpeed(Player, -1));
            if (k.equalsKey.isPressed) Game.Submit(new ChangeGameSpeed(Player, 1));
        }
    }
}

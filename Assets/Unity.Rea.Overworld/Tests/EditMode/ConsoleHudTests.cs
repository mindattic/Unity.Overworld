using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A14 in Unity: the keyboard's keys reach the console by the WinForms names the original tested; the prompt
    // shows at (55, 75) in its colour while the console is open, blinking, and hides when it shuts; a script's failure
    // shows on the message board; and CHANGE SCRIPT COLOR colours the prompt.
    public class ConsoleHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        ScriptInterpreter scripts;
        MessageBoard messages;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls);
            var pickups = new Pickups(content.Pickups, party, null, new System.Random(3)).Install(game);
            messages = new MessageBoard(content.Hud);
            scripts = new ScriptInterpreter(content.Scripts, party, pickups.Inventory) { Messages = messages }.Install(game);
            party.Install(game);
            messages.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            hud = HudPresenter.Create(content.Hud, null, messages);
            hud.ShowConsole(scripts, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
        }

        void Press(string key, bool shift = false) => Assert.IsTrue(game.Submit(new PressConsoleKey(PlayerId.One, key, shift)).Accepted);

        [Test]
        public void TheKeysHaveTheNamesTheOriginalTested()
        {
            Assert.AreEqual("Oemtilde", HeroInput.ConsoleKeyName(Key.Backquote));
            Assert.AreEqual("Insert", HeroInput.ConsoleKeyName(Key.Insert));
            Assert.AreEqual("C", HeroInput.ConsoleKeyName(Key.C));
            Assert.AreEqual("Z", HeroInput.ConsoleKeyName(Key.Z));
            Assert.AreEqual("D0", HeroInput.ConsoleKeyName(Key.Digit0));
            Assert.AreEqual("D7", HeroInput.ConsoleKeyName(Key.Digit7));
            Assert.AreEqual("OemPipe", HeroInput.ConsoleKeyName(Key.Backslash), "the \\| key types the delimiter's pipe");
            Assert.AreEqual("Oemplus", HeroInput.ConsoleKeyName(Key.Equals));
            Assert.AreEqual("OemMinus", HeroInput.ConsoleKeyName(Key.Minus));
            Assert.AreEqual("Oemcomma", HeroInput.ConsoleKeyName(Key.Comma));
            Assert.AreEqual("OemPeriod", HeroInput.ConsoleKeyName(Key.Period));
            Assert.AreEqual("Back", HeroInput.ConsoleKeyName(Key.Backspace));
            Assert.AreEqual("Enter", HeroInput.ConsoleKeyName(Key.NumpadEnter));
            Assert.AreEqual("ShiftKey", HeroInput.ConsoleKeyName(Key.RightShift));
            Assert.AreEqual("F11", HeroInput.ConsoleKeyName(Key.F11));
            Assert.AreEqual("NumPad4", HeroInput.ConsoleKeyName(Key.Numpad4));
        }

        [Test]
        public void ThePromptShowsWhileTheConsoleIsOpen()
        {
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.ConsoleLabel.style.display.value, "shut at the start");
            Press("Oemtilde");
            Press("C");
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.Flex, hud.ConsoleLabel.style.display.value);
            Assert.AreEqual("\\ > CHANGE  ", hud.ConsoleLabel.text);
            Assert.AreEqual(75f, hud.ConsoleLabel.style.top.value.value, 1e-4f);
            Assert.AreEqual(55f * hud.HorizontalScale, hud.ConsoleLabel.style.left.value.value, 1e-4f);
            Assert.AreEqual((Color)new Color32(128, 255, 128, 255), hud.ConsoleLabel.style.color.value);
            for (var i = 0; i < 30; i++) game.Step();
            hud.Refresh();
            Assert.AreEqual("/ > CHANGE |", hud.ConsoleLabel.text, "the I-beam blinks on after half a second");

            Press("Delete");
            foreach (var (key, shift) in ScriptInterpreter.KeysFor(content.Scripts.Console, "CHANGE SCRIPT COLOR RED | CHANGE HERO WEAPON ITEM"))
                Press(key, shift);
            Press("Enter");
            game.Step();
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, hud.ConsoleLabel.style.display.value, "Enter shuts it");
            Assert.AreEqual("Script failed to execute.", hud.MessageLabels[0].text);
            Press("Insert");
            hud.Refresh();
            Assert.AreEqual((Color)new Color32(255, 0, 0, 255), hud.ConsoleLabel.style.color.value, "CHANGE SCRIPT COLOR RED");
            StringAssert.Contains("CHANGE SCRIPT COLOR RED | CHANGE HERO WEAPON ITEM", hud.ConsoleLabel.text, "Insert recalls the last script");
        }
    }
}

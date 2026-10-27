using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MindAttic.Overworld.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Overworld.Tests
{
    /// <summary>
    /// U.OVW-US-A14: the original's scripting language and its tilde console (Script.vb and frmMain's scripter mode,
    /// RECONSTRUCTION 9): CHANGE, MOVE and RUN over HERO, CAMERA, TIME, LIGHT, INVENTORY, BOOK, OPTIONS, CURSOR and
    /// SCRIPT, compound with " | ", +/- adding and anything else assigning; the console's keys, Insert's recall and the
    /// blinking prompt; the input lock while the console is open or a script runs; the two failure messages; the item,
    /// Rune and quest scripts running through it; and the seven defects of the original, fixed.
    /// </summary>
    public class ScriptTests
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

        const int Rune = 111;

        Game game;
        Party party;
        Pickups pickups;
        InventoryControl pack;
        CameraControl view;
        WorldClock clock;
        SleepCycle sleep;
        MessageBoard messages;
        ScriptInterpreter scripts;
        BookReader books;
        InterfaceControl ui;
        List<IGameEvent> raised;
        Hero chrono, belmont;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            view = new CameraControl(Registry.View, party);
            pack = new InventoryControl(pickups, Registry.Hud.Inventory, Registry.Hud.QuickBar, view).Install(game);
            messages = new MessageBoard(Registry.Hud);
            clock = new WorldClock(Registry.Time, Registry.Heroes.Locomotion.GameSpeed, messages);
            sleep = new SleepCycle(party, clock);
            scripts = new ScriptInterpreter(Registry.Scripts, party, pickups.Inventory)
            {
                Sleep = sleep, Clock = clock, Pack = pack, View = view, Messages = messages,
            }.Install(game);
            pickups.Scripts = scripts;
            pack.Scripts = scripts;
            books = new BookReader(Registry.Books, Registry.Hud.Book, party).Install(game);
            scripts.Books = books;
            pack.Books = books;
            party.Install(game);
            ui = new InterfaceControl(Registry.Hud, party).Install(game);
            scripts.Interface = ui;
            view.Install(game);
            sleep.Install(game);
            clock.Install(game);
            messages.Install(game);
            Submit(new JoinGame(PlayerId.One));
            chrono = party.Heroes[0];
            belmont = party.Heroes[1];
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Submit(ICommand c)
        {
            var r = game.Submit(c);
            Assert.IsTrue(r.Accepted, r.Reason);
        }

        ScriptOutcome Run(string script) => scripts.Execute(game, PlayerId.One, scripts.TargetOf(PlayerId.One), script, game.Tick, raised);

        void Steps(int n)
        {
            for (var i = 0; i < n; i++) game.Step();
        }

        void Key(string key, bool shift = false) => Submit(new PressConsoleKey(PlayerId.One, key, shift));

        void Type(string text)
        {
            var keys = ScriptInterpreter.KeysFor(Registry.Scripts.Console, text);
            Assert.IsNotNull(keys, "every character of '" + text + "' has a key");
            foreach (var (key, shift) in keys) Key(key, shift);
        }

        ScriptState State => scripts.For(PlayerId.One);

        /// <summary>The messages shown and waiting, once the board has shown them all.</summary>
        List<string> Messages()
        {
            var seen = new List<ShownMessage>();
            for (var i = 0; i < 2000 && (messages.Waiting > 0 || seen.Count == 0); i++)
            {
                game.Step();
                foreach (var m in messages.Shown)
                    if (!seen.Any(s => ReferenceEquals(s, m))) seen.Add(m);
            }

            return seen.Select(m => m.Caption).ToList();
        }

        [Test]
        public void CompoundCommandsAddWithASignAndAssignOtherwise()
        {
            Assert.AreEqual((31, 12, 60), (chrono.Health, chrono.Mana, chrono.Stamina));
            Assert.AreEqual(ScriptOutcome.Ran, Run("change hero hp -10 | CHANGE HERO MP =5 | CHANGE HERO2 AP 7 | CHANGE HERO3 HEALTH +9999 | CHANGE HERO GOLD +3"));
            Assert.AreEqual((21, 5), (chrono.Health, chrono.Mana), "HERO is the item target (the leader until another is chosen)");
            Assert.AreEqual(7, belmont.Stamina);
            Assert.AreEqual(50, party.Heroes[2].Health, "an addition stops at the maximum");
            Assert.AreEqual(3, pickups.Inventory.Gold);

            Run("CHANGE HERO LIFE -100 | CHANGE HERO1 MAGIC 999 | CHANGE HERO ENERGY =-4 | CHANGE HERO MONEY =-2");
            Assert.AreEqual((0, 20, 0), (chrono.Health, chrono.Mana, chrono.Stamina), "kept within 0 and the maximum (mana to its maximum, not its current value; BIBLE)");
            Assert.AreEqual(-2, pickups.Inventory.Gold, "= assigns a negative number");

            Run("CHANGE HERO STATUS POISON | CHANGE HERO2 STATUS ENRAGED | CHANGE HERO FACING LEFT");
            Assert.AreEqual(HeroStatus.Poisoned, chrono.Status);
            Assert.AreEqual(HeroStatus.Angry, belmont.Status);
            Assert.AreEqual(Facing.Left, chrono.Facing);

            Run("CHANGE HERO X 0.5 | CHANGE HERO Y -0.25 | CHANGE HERO X +0.1 | CHANGE HERO Z -1");
            Assert.AreEqual(0.6f, chrono.X, 1e-6f);
            Assert.AreEqual(-0.25f, chrono.Y, 1e-6f);
            Assert.AreEqual(-1f, chrono.Z);
            Assert.AreEqual(3, raised.OfType<HeroPlaced>().Count(p => p.Hero == 0), "X and Y are placed, drawn there at once");
        }

        [Test]
        public void AnUnknownItemFailsToExecuteAndANumberThatIsNotOneFailsToRun()
        {
            Assert.AreEqual(ScriptOutcome.Failed, Run("CHANGE HERO WEAPON ITEM | CHANGE HERO HP +5"));
            Assert.AreEqual(36, chrono.Health, "the other commands still run");
            Assert.IsFalse(raised.OfType<ScriptFailed>().Single().Crashed);

            Assert.AreEqual(ScriptOutcome.Crashed, Run("CHANGE HERO HP +5 | CHANGE HERO HP lots | CHANGE HERO HP +5"));
            Assert.AreEqual(41, chrono.Health, "an exception stops the script where it is");
            Assert.IsTrue(raised.OfType<ScriptFailed>().Last().Crashed);
            Assert.AreEqual(ScriptOutcome.Crashed, Run("MOVE HERO TO 0.5,0.5"), "MOVE wants x,y,z");
            Assert.AreEqual(ScriptOutcome.Crashed, Run("CHANGE TIME HOUR 25"));

            CollectionAssert.AreEqual(new[] { "Script failed to execute.", "Script failed to run.", "Script failed to run.", "Script failed to run." }, Messages());
            var font = Registry.Hud.Font("fontArial");
            Assert.AreEqual(font, Registry.Scripts.MessageFont);

            // Unknown commands and types do nothing, as before; MAKE is empty.
            Assert.AreEqual(ScriptOutcome.Ran, Run("FROB HERO HP 1 | CHANGE WIDGET HP 1 | MAKE PROP ORCHARD1 0,0,0 | MOVE ENEMY TO 1,1,1"));
        }

        [Test]
        public void TheOriginalsDefectsAreFixed()
        {
            // 1. A command of neither four words nor two reused the last command's words: an empty Enter ran the last effect again.
            Run("CHANGE HERO HP +5");
            Assert.AreEqual(ScriptOutcome.Failed, Run(""));
            Assert.AreEqual(ScriptOutcome.Failed, Run("CHANGE HERO HP"));
            Assert.AreEqual(ScriptOutcome.Failed, Run("CHANGE HERO HP +5 +5"));
            Assert.AreEqual(36, chrono.Health);

            // 2. RUN NIGHT skipped every command after it.
            Assert.AreEqual(ScriptOutcome.Ran, Run("RUN NIGHT | CHANGE HERO HP +1"));
            Assert.AreEqual(22, clock.Now.Hour);
            Assert.AreEqual(37, chrono.Health);

            // 3. CHANGE TIME HOUR +n computed the time and threw it away.
            Run("CHANGE TIME HOUR +3");
            Assert.AreEqual(1, clock.Now.Hour);
            Assert.AreEqual(2, clock.Now.Day, "past midnight, the next day");
            Assert.AreEqual("Night", clock.Period.Name);

            // 4. CHANGE LIGHT threw on a negative number and on a first channel past 9.
            Assert.AreEqual(ScriptOutcome.Ran, Run("CHANGE LIGHT AMBIENT 128,128,128"));
            Assert.AreEqual(new LightColour(128, 128, 128), sleep.Ambient);
            Assert.AreEqual(ScriptOutcome.Ran, Run("CHANGE LIGHT AMBIENT -200 | CHANGE LIGHT RED -10 | CHANGE LIGHT G 300 | CHANGE LIGHT BLUE =0"));
            Assert.AreEqual(new LightColour(0, 0, 0), sleep.Ambient);
            Assert.AreEqual(new LightColour((byte)(clock.Period.Colour.R - 10), 255, 0), clock.Light);

            // 5. CHANGE CURSOR POS CENTER wrote the centre into the mouse's last motion, which the next poll overwrote.
            var v = view.For(PlayerId.One);
            v.Cursor.MouseX = 40;
            v.Cursor.MouseY = 400;
            Run("CHANGE CURSOR POS CENTER");
            Assert.AreEqual((320f, 240f), (v.Cursor.MouseX, v.Cursor.MouseY));
            Assert.AreEqual((v.Camera.X, v.Camera.Y), (v.Cursor.X, v.Cursor.Y));

            // 6. CHANGE ENEMY HEALTH never matched the upper-cased "Health"; it is the stub it was meant to be.
            Assert.AreEqual(ScriptOutcome.Ran, Run("CHANGE ENEMY HEALTH 5 | CHANGE BADGUY AI PASSIVE"));
            Assert.AreEqual(ScriptOutcome.Failed, Run("CHANGE ENEMY STATE RETREAT"));

            // 7. Shift stuck on for the session after its first press: each key carries whether Shift is held.
            Key("Oemtilde");
            Key("Oemplus", shift: true);
            Key("Oemplus");
            Assert.AreEqual("=+", State.Line);
        }

        [Test]
        public void TheConsoleTypesRunsRemembersAndRecalls()
        {
            var c = Registry.Scripts.Console;
            Key("A");
            Assert.IsFalse(State.ConsoleOpen, "closed, the console takes only ~ and Insert");
            Assert.AreEqual(string.Empty, State.Line);

            Key("Oemtilde");
            Assert.IsTrue(State.ConsoleOpen);
            Assert.IsTrue(scripts.InputLocked(PlayerId.One));
            Key("C");
            Assert.AreEqual("CHANGE ", State.Line);
            Key("C");
            Assert.AreEqual("CHANGE CAMERA ", State.Line);
            Key("C");
            Assert.AreEqual("CHANGE CAMERA C", State.Line);
            Key("Back");
            Key("Up");
            Key("ShiftKey");
            Key("Z");
            Key("Space");
            Key("D1");
            Assert.AreEqual("CHANGE CAMERA Z 1", State.Line);
            Key("Delete");
            Assert.AreEqual(string.Empty, State.Line);
            foreach (var k in new[] { "OemSemicolon", "OemPipe", "OemPeriod", "OemBackslash", "OemQuestion", "OemMinus", "Oemcomma", "OemQuotes", "F5" })
                Key(k);
            Assert.AreEqual(":|./?-,\"F5", State.Line, "punctuation by its key; any other key types its name");
            Key("Escape");
            Assert.IsFalse(State.ConsoleOpen);
            Key("Oemtilde");
            Assert.AreEqual(":|./?-,\"F5", State.Line, "the line waits for the console to open again");
            Key("F11");
            Assert.IsFalse(State.ConsoleOpen);

            Key("Oemtilde");
            Key("Delete");
            Type("CHANGE HERO HP -1 | CHANGE HERO AP =12");
            Assert.AreEqual("CHANGE HERO HP -1 | CHANGE HERO AP =12", State.Line);
            Key("Enter");
            Assert.IsFalse(State.ConsoleOpen, "Enter runs the line and shuts the console");
            Assert.AreEqual((30, 12), (chrono.Health, chrono.Stamina));
            Assert.AreEqual(string.Empty, State.Line);
            Assert.AreEqual("CHANGE HERO HP -1 | CHANGE HERO AP =12", State.Last);
            Assert.IsTrue(raised.OfType<ScriptRan>().Last().Outcome == ScriptOutcome.Ran);

            Key("Insert");
            Assert.IsTrue(State.ConsoleOpen, "Insert opens the console with the last script");
            Assert.AreEqual("CHANGE HERO HP -1 | CHANGE HERO AP =12", State.Line);
            Key("Enter");
            Assert.AreEqual(29, chrono.Health);
            Assert.IsFalse(scripts.InputLocked(PlayerId.One));
            Assert.IsNotEmpty(raised.OfType<ConsoleChanged>());

            // CHANGE SCRIPT COLOR colours the console's text.
            Assert.AreEqual(c.Colour, State.Colour);
            Run("CHANGE SCRIPT COLOR MAGENTA");
            Assert.AreEqual(new LightColour(255, 0, 255), State.Colour);
            Run("CHANGE SCRIPT COLOR PLAID");
            Assert.AreEqual(new LightColour(255, 0, 255), State.Colour, "an unknown colour changes nothing");
        }

        [Test]
        public void ThePromptBlinksEveryHalfSecondAtItsPlace()
        {
            var c = Registry.Scripts.Console;
            Assert.AreEqual((55f, 75f, 0.5f), (c.X, c.Y, c.BlinkSeconds));
            Assert.AreEqual(new LightColour(128, 255, 128), c.Colour);
            Assert.AreEqual(Registry.Hud.Font("fontArial"), c.Font);
            Assert.IsNull(scripts.Prompt(PlayerId.One));
            Key("Oemtilde");
            Key("R");
            Assert.AreEqual("\\ > R ", scripts.Prompt(PlayerId.One));
            Steps(29);
            Assert.AreEqual("\\ > R ", scripts.Prompt(PlayerId.One));
            Steps(2);
            Assert.AreEqual("/ > R|", scripts.Prompt(PlayerId.One));
            Steps(31);
            Assert.AreEqual("\\ > R ", scripts.Prompt(PlayerId.One));
        }

        [Test]
        public void TheKeysForAScriptTypeItExactly()
        {
            foreach (var text in new[] { "CHANGE CURSOR POS CENTER", "CHANGE CAMERA POS HERO", "CHANGE HERO HP +10 | CHANGE HERO AP =5", "MOVE HERO TO 0.3,-0.2,0", "RUN NIGHT", "CHANGE LIGHT AMBIENT 0,0,0", "CAB" })
            {
                Key("Oemtilde");
                Key("Delete");
                Type(text);
                Assert.AreEqual(text, State.Line);
                Key("Escape");
            }

            Assert.IsNull(ScriptInterpreter.KeysFor(Registry.Scripts.Console, "~"), "the tilde closes the console; it cannot be typed");
        }

        [Test]
        public void WhileTheConsoleIsOpenThePartyIgnoresThePlayer()
        {
            var x = chrono.X;
            Key("Oemtilde");
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(right: true)));
            Steps(30);
            Assert.AreEqual(x, chrono.X, "the arrows do nothing while the console is open");
            Assert.AreEqual(default(MoveInput), party.InputOf(PlayerId.One));
            Key("Escape");
            Steps(30);
            Assert.Greater(chrono.X, x, "shut, they walk the leader again");
        }

        [Test]
        public void AMoveScriptWalksTheHeroToItsTargetAndThenGivesControlBack()
        {
            Submit(new PlaceHero(PlayerId.One, 0, 0f, 0f));
            Submit(new SetMoveInput(PlayerId.One, MoveInput.Arrows(left: true)));
            Assert.AreEqual(ScriptOutcome.Ran, Run("MOVE HERO TO 0.3,0.2,0"));
            Assert.IsTrue(State.Running);
            Assert.IsTrue(scripts.InputLocked(PlayerId.One));
            CollectionAssert.AreEqual(new[] { new MoveTarget(0, 0.3f, 0.2f) }, State.Targets);
            var ticks = 0;
            while (State.Running && ticks < 3000)
            {
                game.Step();
                ticks++;
            }

            Assert.IsFalse(State.Running, "the script ends at its target (the original never checked; BIBLE)");
            Assert.AreEqual(0.3f, chrono.X, 0.05f);
            Assert.AreEqual(0.2f, chrono.Y, 0.05f);
            Assert.IsTrue(raised.OfType<ScriptRunningChanged>().Select(e => e.Running).SequenceEqual(new[] { true, false }));
            Assert.IsFalse(scripts.InputLocked(PlayerId.One));

            // Control comes back: the held left arrow walks the leader again.
            var x = chrono.X;
            Steps(30);
            Assert.Less(chrono.X, x);
        }

        [Test]
        public void AnAiWalkRunsUntilItIsStoppedAndTheConsoleStaysLive()
        {
            Submit(new PlaceHero(PlayerId.One, 0, 0f, 0f));
            Run("CHANGE HERO AI WALKLEFT");
            Assert.AreEqual(HeroState.Walking, chrono.State);
            Steps(60);
            Assert.Less(chrono.X, -0.05f, "it walks left on its own");
            Assert.IsTrue(scripts.InputLocked(PlayerId.One));

            Key("Oemtilde");
            Assert.IsTrue(State.ConsoleOpen, "the console opens while a script runs");
            Type("CHANGE HERO AI STOP");
            Key("Enter");
            Assert.IsFalse(State.Running);
            Steps(2);
            var x = chrono.X;
            Steps(30);
            Assert.AreEqual(x, chrono.X, 1e-6f, "control back and nothing held: it stands");

            Run("CHANGE HERO AI FACEUP | CHANGE SCRIPT RUNNING OFF");
            Assert.AreEqual(Facing.Up, chrono.Facing);
            Assert.IsFalse(State.Running);
            Run("CHANGE HERO2 AI KNEEL");
            Assert.IsTrue(belmont.Kneeling);
            Run("CHANGE SCRIPT RUNNING ON");
            Assert.IsTrue(scripts.InputLocked(PlayerId.One));
        }

        [Test]
        public void TheRuneTeleportsItsUserHomeAndTheCameraFollows()
        {
            Submit(new GiveItem(PlayerId.One, Rune));
            Submit(new ToggleTeam(PlayerId.One));
            Submit(new PlaceHero(PlayerId.One, 0, 2f, 1.5f));
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new UseItem(PlayerId.One));
            Assert.IsFalse(pack.For(PlayerId.One).Open, "CHANGE INVENTORY OPEN FALSE");
            Assert.AreEqual(7, chrono.Mana, "MANA -5");
            Assert.AreEqual((0f, 0f, -1f), (chrono.X, chrono.Y, chrono.Z), "X 0, Y 0, from Z -1");
            var cam = view.For(PlayerId.One).Camera;
            var size = party.Data.HeroSize;
            Assert.AreEqual(size / 2f, cam.X, 1e-6f, "CAMERA POS HERO");
            Assert.AreEqual(-size / 3f, cam.Y, 1e-6f);
            Assert.AreEqual(1, pickups.Inventory.Items.Count, "the Rune is Reusable");
            Steps(40);
            Assert.AreEqual(0f, chrono.Z, "it falls into place");
        }

        [Test]
        public void TheQuestEndScriptRunsThroughTheInterpreter()
        {
            var end = Registry.Quests.Quests.SelectMany(q => q.Objectives).Select(o => o.EndScript).Single(s => s.Length > 0);
            Assert.AreEqual("CHANGE TIME HOUR 18", end);
            Assert.AreEqual(ScriptOutcome.Ran, scripts.Execute(game, PlayerId.One, 0, end, game.Tick, raised));
            Assert.AreEqual((18, "Evening"), (clock.Now.Hour, clock.Period.Name));
            Assert.AreEqual(Registry.Time.Periods.Single(p => p.Name == "Evening").Colour, clock.Light, "HOUR settles the light");
        }

        [Test]
        public void TheOtherTypesDoWhatTheOriginalDid()
        {
            // TIME DAYPERIOD sets the hour and leaves the period and light to the clock's next minute.
            Run("CHANGE TIME DAYPERIOD DUSK");
            Assert.AreEqual(19, clock.Now.Hour);
            Assert.AreEqual("Day", clock.Period.Name);
            Steps(60);
            Assert.AreEqual("Evening", clock.Period.Name);
            Run("CHANGE TIME CLOCK 7");
            Assert.AreEqual((7, 2), (clock.Now.Hour, clock.Now.Day), "an earlier hour is tomorrow's");

            // CAMERA X / Y / Z add or set, the zoom held within its limits.
            var cam = view.For(PlayerId.One).Camera;
            var z = cam.Z;
            Run("CHANGE CAMERA X 1 | CHANGE CAM Y +0.5 | CHANGE CAMERA Z -0.5");
            Assert.AreEqual((1f, z - 0.5f), (cam.X, cam.Z), "-0.5 adds");
            Run("CHANGE CAMERA Z =-2");
            Assert.AreEqual(-2f, cam.Z, "=-2 assigns");
            Run("CHANGE CAMERA Z -50");
            Assert.AreEqual(cam.Definition.FarthestZ, cam.Z);

            // INVENTORY, OPTIONS, BOOK.
            Run("CHANGE INVENTORY OPEN TRUE");
            Assert.IsTrue(pack.For(PlayerId.One).Open);
            Run("CHANGE INVENTORY CLOSE ON");
            Assert.IsFalse(pack.For(PlayerId.One).Open);
            Run("CHANGE OPTIONS OPEN ON");
            Assert.IsTrue(ui.For(PlayerId.One).OptionsOpen, "CHANGE OPTIONS opens the options menu");
            Run("CHANGE OPTIONS CLOSE TRUE");
            Assert.IsFalse(ui.For(PlayerId.One).OptionsOpen);
            Run("CHANGE BOOK NUM 2 | CHANGE BOOK NUMSYS ANCHIENT | CHANGE BOOK COLOR BLUE | CHANGE BOOK LETTERING CALISTO | CHANGE BOOK SHOW ON");
            var book = books.For(PlayerId.One);
            Assert.AreEqual(("Grimoire de'Alchemiste", BookNumbering.Ancient, "BLUE", "CALISTO", true), (books.Current(PlayerId.One).Title, book.Numbering, book.Cover, book.Lettering, book.Shown),
                "book 2 in folder order");
            Assert.AreEqual(ScriptOutcome.Crashed, Run("CHANGE BOOK NUM 5"), "there is no book 5");
            Assert.AreEqual(ScriptOutcome.Ran, Run("CHANGE BOOK COVER GREEN | CHANGE BOOK FONT SYMBOL"), "an unknown cover or lettering does nothing");
            Assert.AreEqual(("BLUE", "CALISTO"), (book.Cover, book.Lettering));
            Assert.AreEqual(ScriptOutcome.Failed, Run("CHANGE BOOK PAGE 3"));
            Assert.AreEqual(ScriptOutcome.Failed, Run("CHANGE INVENTORY SWAP ON"));

            // RUN with an unknown name does nothing.
            Assert.AreEqual(ScriptOutcome.Ran, Run("RUN DAWN"));
        }

        [Test]
        public void TheScriptCommandRunsForTheItemTarget()
        {
            Submit(new RunScript(PlayerId.One, "CHANGE HERO HP 1"));
            Assert.AreEqual(1, chrono.Health);
            Submit(new RunScript(PlayerId.One, "CHANGE HERO HP 2", 1));
            Assert.AreEqual(2, belmont.Health);
            Assert.IsFalse(game.Submit(new RunScript(PlayerId.One, "CHANGE HERO HP 2", 7)).Accepted);
        }

        [Test]
        public void AScriptThatRunsItselfIsRefused()
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var original = disk.Read("scripts.json");
            var changed = original.Replace("\"script\": \"CHANGE TIME DAYPERIOD NIGHT\"", "\"script\": \"RUN NIGHT\"");
            Assert.AreNotEqual(original, changed);
            var e = Assert.Throws<ContentException>(() => ContentRegistry.Load(new MemoryContentSource(disk).Set("scripts.json", changed)));
            StringAssert.Contains("scripts.json.run: \"NIGHT\" runs itself", e.Message);
        }
    }
}

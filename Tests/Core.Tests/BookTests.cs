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
    /// U.OVW-US-A16: the five books read two pages at a time (Book.vb, Input.vb's page and book paths, frmMain's B and N,
    /// Script.cmdChangeBook, RECONSTRUCTION 11): the start book, B and N, the arrows' and the wheel's page turns, the book
    /// items opening their books through their scripts, what the pack's keys do while a book is shown, the move button
    /// hiding it, the page numbers, and the two defects fixed (a book number past the books, the Roman numerals past 64).
    /// </summary>
    public class BookTests
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

        const int BrambleNut = 104, Grimoire = 105, DragonAlmanac = 109, PoetryBook = 110, MattsBook = 371;
        const int Colesium = 0, Dragon = 1, Alchemiste = 2, Matts = 3, Poetry = 4;

        Game game;
        Party party;
        Pickups pickups;
        InventoryControl pack;
        CameraControl view;
        ScriptInterpreter scripts;
        BookReader books;
        List<IGameEvent> raised;

        [SetUp]
        public void SetUp()
        {
            game = new Game();
            party = new Party(Registry.Heroes, null, new Random(5));
            pickups = new Pickups(Registry.Pickups, party, null, new Random(7)).Install(game);
            view = new CameraControl(Registry.View, party);
            pack = new InventoryControl(pickups, Registry.Hud.Inventory, Registry.Hud.QuickBar, view).Install(game);
            books = new BookReader(Registry.Books, Registry.Hud.Book, party).Install(game);
            scripts = new ScriptInterpreter(Registry.Scripts, party, pickups.Inventory) { Pack = pack, View = view, Books = books }.Install(game);
            pickups.Scripts = scripts;
            pack.Scripts = scripts;
            pack.Books = books;
            party.Install(game);
            view.Install(game);
            view.WheelFirst = (player, steps) => books.UseWheel(player, steps) || pack.UseWheel(player, steps) || pickups.UseWheel(player, steps);
            Submit(new JoinGame(PlayerId.One));
            raised = new List<IGameEvent>();
            game.EventRaised += raised.Add;
        }

        void Submit(ICommand c)
        {
            var r = game.Submit(c);
            Assert.IsTrue(r.Accepted, r.Reason);
        }

        void Hold(MoveInput input) => Submit(new SetMoveInput(PlayerId.One, input));

        /// <summary>One press of the held keys: held for a tick, then let go for a tick.</summary>
        void Press(MoveInput input)
        {
            Hold(input);
            game.Step();
            Hold(default);
            game.Step();
        }

        BookView Book => books.For(PlayerId.One);
        string Title => books.Current(PlayerId.One).Title;

        void Give(int pickup) => Submit(new GiveItem(PlayerId.One, pickup));

        static readonly MoveInput Left = MoveInput.Arrows(left: true);
        static readonly MoveInput Right = MoveInput.Arrows(right: true);

        [Test]
        public void TheFiveBooksAreInFolderOrderAndEachStartsHiddenOnTheGrimoire()
        {
            CollectionAssert.AreEqual(new[] { "Colesium Vol I", "Dragon Almanac", "Grimoire de'Alchemiste", "Matt's Book", "Poetry Book" },
                Registry.Books.Books.Select(b => b.Title).ToArray());
            // loadBooks: the Grimoire, Corsiva 8, Roman, the plain cover; bBook starts False.
            Assert.AreEqual(("Grimoire de'Alchemiste", false, 0, "CORSIVA8", BookNumbering.RomanNumeral, (string)null),
                (Title, Book.Shown, Book.Page, Book.Lettering, Book.Numbering, Book.Cover));
            Assert.AreEqual("gfx/gui/book.PNG", Registry.Hud.Book.CoverPicture(Book.Cover));
            // Each book's last spread starts at its page count less two.
            CollectionAssert.AreEqual(new[] { 8, 10, 6, 4, 14 }, Enumerable.Range(0, 5).Select(books.LastPage).ToArray());
        }

        [Test]
        public void BShowsAndHidesTheBookAndNCyclesTheBooksInCorsiva()
        {
            Submit(new ToggleBook(PlayerId.One));
            Assert.IsTrue(Book.Shown);
            Assert.IsTrue(books.Shown(PlayerId.One));
            Submit(new ToggleBook(PlayerId.One));
            Assert.IsFalse(Book.Shown);

            // N: the next book in folder order, at its first spread, in fontCorsiva, Roman; shown or not as it was.
            Submit(new OpenBook(PlayerId.One, Alchemiste, 4));
            Submit(new RunScript(PlayerId.One, "CHANGE BOOK COVER BLUE | CHANGE BOOK NUMSYS STANDARD"));
            Submit(new NextBook(PlayerId.One));
            Assert.AreEqual(("Matt's Book", true, 0, "CORSIVA", BookNumbering.RomanNumeral, "BLUE"),
                (Title, Book.Shown, Book.Page, Book.Lettering, Book.Numbering, Book.Cover), "the cover is kept");
            Submit(new NextBook(PlayerId.One));
            Assert.AreEqual("Poetry Book", Title);
            Submit(new NextBook(PlayerId.One));
            Assert.AreEqual("Colesium Vol I", Title, "N wraps to the first book");
            Assert.IsTrue(raised.OfType<BookChanged>().Any());
        }

        [Test]
        public void TheArrowsTurnASpreadAPressAndTheWheelTurnsBackTowardTheScreen()
        {
            Submit(new OpenBook(PlayerId.One, Dragon));
            Press(Right);
            Assert.AreEqual(2, Book.Page);
            // Held, it turns once (bTurnPage) ...
            Hold(Right);
            for (var i = 0; i < 10; i++) game.Step();
            Assert.AreEqual(4, Book.Page);
            // ... and left with right still held does not turn either (one latch for the pair).
            Hold(new MoveInput(true, true, false, false, false, false, 0f, 0f));
            game.Step();
            Assert.AreEqual(4, Book.Page);
            Hold(default);
            game.Step();
            for (var i = 0; i < 6; i++) Press(Right);
            Assert.AreEqual(10, Book.Page, "held at the last spread (pages 11 and 12)");
            Press(Left);
            Assert.AreEqual(8, Book.Page);
            for (var i = 0; i < 6; i++) Press(Left);
            Assert.AreEqual(0, Book.Page, "held at the first spread");

            // The wheel (before the zoom): away from the screen turns on, toward it back, a spread a step.
            var z = view.For(PlayerId.One).Camera.Z;
            for (var i = 0; i < 3; i++) Submit(new SetViewInput(PlayerId.One, new ViewInput(0f, 0f, -1, false, false, false, false, false, false, false, false)));
            game.Step();
            Assert.AreEqual(6, Book.Page);
            Submit(new SetViewInput(PlayerId.One, new ViewInput(0f, 0f, 1, false, false, false, false, false, false, false, false)));
            game.Step();
            Assert.AreEqual(4, Book.Page);
            Assert.AreEqual(z, view.For(PlayerId.One).Camera.Z, "the book took the wheel: no zoom");

            // A scripted turn, and none while the book is hidden.
            Submit(new TurnPage(PlayerId.One, 1));
            Assert.AreEqual(6, Book.Page);
            Submit(new ToggleBook(PlayerId.One));
            Assert.IsFalse(game.Submit(new TurnPage(PlayerId.One, 1)).Accepted);
            Press(Right);
            Assert.AreEqual(6, Book.Page, "the arrows turn nothing while the book is hidden");
        }

        [Test]
        public void WhileTheBookIsShownLeftAndRightDoNotWalkButUpAndDownDo()
        {
            var leader = party.Heroes[party.LeaderOf(PlayerId.One)];
            Submit(new ToggleBook(PlayerId.One));
            var (x, y) = (leader.X, leader.Y);
            Hold(Left);
            for (var i = 0; i < 20; i++) game.Step();
            Assert.AreEqual(x, leader.X, "actionLeft only marks the direction while bBook is on");
            Hold(MoveInput.Arrows(up: true));
            for (var i = 0; i < 20; i++) game.Step();
            Assert.Greater(leader.Y, y, "actionUp has no bBook test: the leader walks");
            Assert.IsTrue(Book.Shown);
        }

        [Test]
        public void TheMoveButtonHidesTheBook()
        {
            Submit(new ToggleBook(PlayerId.One));
            Hold(MoveInput.Toward(1f, 1f));
            game.Step();
            Assert.IsFalse(Book.Shown);
            Assert.IsTrue(raised.OfType<BookChanged>().Any(e => !e.Shown));
        }

        [Test]
        public void EachBookItemOpensItsOwnBookInItsCoverAndLettering()
        {
            Give(Grimoire);
            Give(DragonAlmanac);
            Give(PoetryBook);
            Give(MattsBook);
            Submit(new ToggleInventory(PlayerId.One));
            var expected = new[]
            {
                ("Grimoire de'Alchemiste", "BROWN", "CORSIVA8"),
                ("Dragon Almanac", "BLUE", "CALISTO"),
                ("Poetry Book", "BROWN", "CORSIVA8"),
                ("Matt's Book", "BLUE", "CALISTO"),
            };
            for (var i = 0; i < expected.Length; i++)
            {
                Submit(new MoveInventoryCursor(PlayerId.One, i));
                Submit(new OpenBook(PlayerId.One, Colesium, 2));
                Submit(new UseItem(PlayerId.One));
                Assert.AreEqual(expected[i], (Title, Book.Cover, Book.Lettering), "item " + i);
                Assert.AreEqual((true, 0, BookNumbering.RomanNumeral), (Book.Shown, Book.Page, Book.Numbering));
            }

            Assert.AreEqual(4, pickups.Inventory.Items.Count, "books are Reusable");
            Assert.IsTrue(pack.For(PlayerId.One).Open, "using a book leaves the pack open; the book is drawn over it");
        }

        [Test]
        public void ANameOrNumberThatIsNoBookReloadsTheOpenOneOrStopsTheScript()
        {
            Give(BrambleNut);
            Submit(new OpenBook(PlayerId.One, Poetry, 6));
            // TO ITEM on a stack that is no book: loadBook found no title and reloaded currBook at its first page.
            Assert.AreEqual(ScriptOutcome.Ran, scripts.Execute(game, PlayerId.One, 0, "CHANGE BOOK TO ITEM", game.Tick, raised));
            Assert.AreEqual(("Poetry Book", 0), (Title, Book.Page));
            Submit(new OpenBook(PlayerId.One, Poetry, 6));
            Assert.AreEqual(ScriptOutcome.Ran, scripts.Execute(game, PlayerId.One, 0, "CHANGE BOOK NUM -1", game.Tick, raised));
            Assert.AreEqual(("Poetry Book", 0), (Title, Book.Page), "-1 is the open book");
            Assert.AreEqual(ScriptOutcome.Ran, scripts.Execute(game, PlayerId.One, 0, "CHANGE BOOK NUM 0", game.Tick, raised));
            Assert.AreEqual("Colesium Vol I", Title);
            // A number past the books read past the original's tables (a crash, or a one-page book that crashed every
            // frame it was drawn): here the script stops ("Script failed to run.") and the book is unchanged (BIBLE).
            Assert.AreEqual(ScriptOutcome.Crashed, scripts.Execute(game, PlayerId.One, 0, "CHANGE BOOK NUM 7", game.Tick, raised));
            Assert.AreEqual(ScriptOutcome.Crashed, scripts.Execute(game, PlayerId.One, 0, "CHANGE BOOK NUM -2", game.Tick, raised));
            Assert.AreEqual("Colesium Vol I", Title);
            Assert.AreEqual(ScriptOutcome.Ran, scripts.Execute(game, PlayerId.One, 0, "CHANGE BOOK SHOW OFF | CHANGE BOOK NUMSYS NONE | CHANGE BOOK FONT ARIALNARROW", game.Tick, raised));
            Assert.AreEqual((false, BookNumbering.None, "ARIALNARROW"), (Book.Shown, Book.Numbering, Book.Lettering));
        }

        [Test]
        public void WithThePackOpenTheBookTakesTheArrowsDeleteAndTheDropKey()
        {
            Give(BrambleNut);
            Give(Grimoire);
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new OpenBook(PlayerId.One, Poetry));
            var packView = pack.For(PlayerId.One);
            Assert.AreEqual(0, packView.Cursor);
            Press(Right);
            Assert.AreEqual((0, 2), (packView.Cursor, Book.Page), "selectInventoryRight waits while bBook is on; the page turns");

            // actionDrop with the book shown hides it and drops nothing.
            Submit(new DropItem(PlayerId.One));
            Assert.IsFalse(Book.Shown);
            Assert.AreEqual(2, pickups.Inventory.Items.Count);
            Assert.IsEmpty(raised.OfType<ItemDropped>());
            Assert.IsTrue(packView.Open);

            // The move button with the pack open drops, unless the book is shown: then it only hides the book.
            Submit(new ToggleBook(PlayerId.One));
            Press(MoveInput.Toward(1f, 1f));
            Assert.IsFalse(Book.Shown);
            Assert.AreEqual(2, pickups.Inventory.Items.Count);

            // actionInventory with the book shown hides it and keeps the pack open.
            Submit(new ToggleBook(PlayerId.One));
            Submit(new ToggleInventory(PlayerId.One));
            Assert.AreEqual((false, true), (Book.Shown, packView.Open));
            Submit(new ToggleBook(PlayerId.One));
            Submit(new ToggleInventory(PlayerId.One));
            Submit(new ToggleInventory(PlayerId.One));
            Assert.IsFalse(packView.Open);
            Submit(new ToggleBook(PlayerId.One));
            Submit(new ToggleInventory(PlayerId.One));
            Assert.AreEqual((false, true), (Book.Shown, packView.Open), "it also opens a shut pack");

            // Once hidden, the pack's arrows and drop are the pack's again.
            Press(Right);
            Assert.AreEqual(1, packView.Cursor);
            Submit(new DropItem(PlayerId.One));
            Assert.AreEqual(1, raised.OfType<ItemDropped>().Count());
        }

        [Test]
        public void PageNumbersAreRomanStandardOrNoneAndRomanIsRightPast64()
        {
            Assert.AreEqual("I II III IV V VI VII VIII IX X XI XII XIII XIV XV XVI",
                string.Join(" ", Enumerable.Range(1, 16).Select(n => BookReader.PageNumber(BookNumbering.RomanNumeral, n))));
            // getRomanNumeral's table wrote "LV" for 65 and "LXIV" for 74 (BIBLE).
            Assert.AreEqual(("LXIV", "LXV", "LXIX", "LXXIV", "LXXX", "XCIX"),
                (BookReader.Roman(64), BookReader.Roman(65), BookReader.Roman(69), BookReader.Roman(74), BookReader.Roman(80), BookReader.Roman(99)));
            Assert.AreEqual("12", BookReader.PageNumber(BookNumbering.Standard, 12));
            Assert.AreEqual("", BookReader.PageNumber(BookNumbering.None, 3));
            Assert.AreEqual("", BookReader.PageNumber(BookNumbering.Ancient, 3));
        }

        [Test]
        public void TheSpreadIsWrittenWhereRenderBookWroteIt()
        {
            var screen = Registry.Hud.Book;
            Submit(new OpenBook(PlayerId.One, Alchemiste, 6));
            var lines = screen.Lines(books.Current(PlayerId.One), Book);
            Assert.AreEqual(4, lines.Count);
            Assert.AreEqual((Registry.Books.Books[Alchemiste].Pages[6], 50f, 50f), (lines[0].Text, lines[0].At.X, lines[0].At.Y));
            StringAssert.Contains("My best student and lover", lines[1].Text);
            Assert.AreEqual((340f, 50f), (lines[1].At.X, lines[1].At.Y));
            Assert.AreEqual(("VII", 170f, 425f, "VIII", 460f, 425f), (lines[2].Text, lines[2].At.X, lines[2].At.Y, lines[3].Text, lines[3].At.X, lines[3].At.Y));
            Assert.AreSame(screen.Lettering("CORSIVA8"), lines[0].Font);
            Assert.AreSame(Registry.Hud.Font("fontAmount"), lines[2].Font);
            Assert.AreEqual(("Monotype Corsiva", 8f, true), (lines[0].Font.Families[0], lines[0].Font.Points, lines[0].Font.Italic));
            Assert.AreEqual(((byte)255, (byte)0, (byte)0, (byte)0), (screen.Colour.A, screen.Colour.R, screen.Colour.G, screen.Colour.B));
            Assert.AreEqual(("gfx/gui/book_brown.PNG", "gfx/gui/book_blue.PNG", (string)null),
                (screen.CoverPicture("BROWN"), screen.CoverPicture("BLUE"), screen.CoverPicture("GREEN")));
            Assert.AreEqual((20f, 20f, 600f, 450f), (screen.X, screen.Y, screen.Width, screen.Height));
        }

        [Test]
        public void AStartBookThatIsNoBookIsAContentError()
        {
            var disk = new DirectoryContentSource(ContentDirectory());
            var source = new MemoryContentSource(disk)
                .Set("hud.json", disk.Read("hud.json").Replace("\"startTitle\": \"Grimoire de'Alchemiste\"", "\"startTitle\": \"Necronomicon\"")
                    .Replace("\"nextLettering\": \"CORSIVA\"", "\"nextLettering\": \"GOTHIC\""));
            var errors = Assert.Throws<ContentException>(() => ContentRegistry.Load(source)).Errors;
            Assert.IsTrue(errors.Any(e => e.Contains("hud.json") && e.Contains("Necronomicon")), string.Join("\n", errors));
            Assert.IsTrue(errors.Any(e => e.Contains("hud.json") && e.Contains("nextLettering")), string.Join("\n", errors));
        }
    }
}

using System.Linq;
using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Overworld.Tests
{
    // U.OVW-US-A16 in Unity: the book on B, its cover picture over 600 x 450 at (20, 20), the two pages and their Roman
    // numbers where renderBook wrote them in the book's lettering, black; a book item's cover and lettering; the bars
    // drawn over the book as renderHealthBar came after renderBook; and the scripted book argument read as the bootstrap
    // takes it.
    public class BookHudTests
    {
        ContentRegistry content;
        Game game;
        Party party;
        BookReader books;
        ScriptInterpreter scripts;
        HudPresenter hud;

        [SetUp]
        public void SetUp()
        {
            content = ContentRegistry.Load(new DirectoryContentSource(OverworldBootstrap.ContentDirectory));
            game = new Game();
            party = new Party(content.Heroes, content.Walls);
            var pickups = new Pickups(content.Pickups, party, null, new System.Random(3)).Install(game);
            books = new BookReader(content.Books, content.Hud.Book, party).Install(game);
            scripts = new ScriptInterpreter(content.Scripts, party, pickups.Inventory) { Books = books }.Install(game);
            party.Install(game);
            Assert.IsTrue(game.Submit(new JoinGame(PlayerId.One)).Accepted);
            hud = HudPresenter.Create(content.Hud, null, null);
            hud.ShowVitals(party, PlayerId.One);
            hud.ShowBook(books, PlayerId.One);
        }

        [TearDown]
        public void TearDown()
        {
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
        }

        VisualElement Layer => hud.BookPicture.parent;

        [Test]
        public void BShowsTheGrimoireWithItsPagesAndRomanNumbers()
        {
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, Layer.style.display.value, "hidden at the start");
            Assert.IsTrue(game.Submit(new ToggleBook(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.Flex, Layer.style.display.value);
            Assert.AreEqual("gfx/gui/book.PNG", hud.BookPictureFile, "the plain cover until a script names another");
            Assert.IsNotNull(hud.BookPicture.style.backgroundImage.value.texture, "book.png is imported");
            Assert.AreEqual(20f, hud.BookPicture.style.top.value.value, 1e-4f);
            Assert.AreEqual(450f, hud.BookPicture.style.height.value.value, 1e-4f);
            Assert.AreEqual(600f * hud.HorizontalScale, hud.BookPicture.style.width.value.value, 1e-4f);

            var left = hud.BookLabels[0];
            StringAssert.StartsWith("Alchemy is the fusing of science with \nmagic", left.text, "the page's hand wrap and spaces kept");
            Assert.IsFalse(left.text.Contains("\r"), "the pages' line ends are written as line breaks");
            Assert.AreEqual(50f, left.style.top.value.value, 1e-4f);
            Assert.AreEqual(50f * hud.HorizontalScale, left.style.left.value.value, 1e-4f);
            Assert.AreEqual(340f * hud.HorizontalScale, hud.BookLabels[1].style.left.value.value, 1e-4f);
            Assert.AreEqual(Color.black, left.style.color.value);
            Assert.AreEqual(8f * 96f / 72f, left.style.fontSize.value.value, 1e-3f, "Monotype Corsiva 8");
            // Monotype Corsiva's one face is an italic (Windows lists it as "Monotype Corsiva Italic"): used as it is, as
            // GDI used it; a stand-in face is slanted instead.
            var face = hud.FaceNameOf(content.Hud.Font("fontCorsiva8"));
            var corsiva = face == "Monotype Corsiva";
            Assert.AreEqual(corsiva ? FontStyle.Normal : FontStyle.Italic, left.style.unityFontStyleAndWeight.value, face);
            if (corsiva) Assert.IsNotNull(left.style.unityFontDefinition.value.fontAsset, "Corsiva is loaded as a font asset by its family and style");
            else Assert.AreSame(hud.FaceOf(content.Hud.Font("fontCorsiva8")), left.style.unityFontDefinition.value.font);
            Assert.AreEqual(("I", "II"), (hud.BookLabels[2].text, hud.BookLabels[3].text));
            Assert.AreEqual(425f, hud.BookLabels[2].style.top.value.value, 1e-4f);
            Assert.AreEqual(170f * hud.HorizontalScale, hud.BookLabels[2].style.left.value.value, 1e-4f);

            Assert.IsTrue(game.Submit(new TurnPage(PlayerId.One, 1)).Accepted);
            hud.Refresh();
            Assert.AreEqual(("III", "IV"), (hud.BookLabels[2].text, hud.BookLabels[3].text));

            Assert.IsTrue(game.Submit(new ToggleBook(PlayerId.One)).Accepted);
            hud.Refresh();
            Assert.AreEqual(DisplayStyle.None, Layer.style.display.value);
        }

        [Test]
        public void ABookScriptChangesTheCoverAndLetteringAndTheBarsDrawOverTheBook()
        {
            game.Submit(new RunScript(PlayerId.One, "CHANGE BOOK COVER BLUE | CHANGE BOOK FONT CALISTO | CHANGE BOOK NUM 1 | CHANGE BOOK SHOW TRUE"));
            hud.Refresh();
            Assert.AreEqual("gfx/gui/book_blue.PNG", hud.BookPictureFile);
            StringAssert.Contains("dinosaurs", string.Join(" ", hud.BookLabels.Take(2).Select(l => l.text)));
            Assert.AreEqual(FontStyle.Bold, hud.BookLabels[0].style.unityFontStyleAndWeight.value, "fontCalisto is bold");
            Assert.AreSame(hud.FaceOf(content.Hud.Font("fontCalisto")), hud.BookLabels[0].style.unityFontDefinition.value.font);

            var root = hud.Document.rootVisualElement;
            var bars = hud.BarFrame(content.Hud.Bars.Health).parent;
            Assert.Greater(root.IndexOf(bars), root.IndexOf(Layer), "renderHealthBar came after renderBook");
        }

        [Test]
        public void TheScriptedBookArgumentReads()
        {
            Assert.IsTrue(OverworldBootstrap.TryBook("Dragon Almanac,5,2", books, out var book, out var page, out var seconds));
            Assert.AreEqual((1, 4, 2f), (book, page, seconds));
            Assert.IsTrue(OverworldBootstrap.TryBook("0", books, out book, out page, out seconds));
            Assert.AreEqual((0, 0, 0f), (book, page, seconds));
            Assert.IsFalse(OverworldBootstrap.TryBook("Necronomicon", books, out _, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryBook("5", books, out _, out _, out _));
            Assert.IsFalse(OverworldBootstrap.TryBook("Matt's Book,7", books, out _, out _, out _), "Matt's Book has six pages");
        }
    }
}

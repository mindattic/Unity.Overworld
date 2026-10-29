using System;
using System.Collections.Generic;
using System.Text;

namespace MindAttic.Overworld.Core
{
    /// <summary>The page numbering of a book (theBook.numSys).</summary>
    public enum BookNumbering
    {
        Standard,
        RomanNumeral,
        Ancient,
        None,
    }

    /// <summary>
    /// One player's book (Book.vb's bBook, currBook, currPage and the book's numbering, cover and font): shown or not,
    /// which book and which spread, and how it is drawn.
    /// </summary>
    public sealed class BookView
    {
        internal BookView() { }

        /// <summary>bBook: the book is shown.</summary>
        public bool Shown { get; internal set; }

        /// <summary>currBook: the book open, an index into <see cref="BookData.Books"/> (the N key's folder order).</summary>
        public int Book { get; internal set; }

        /// <summary>currPage: the left page of the spread shown (0, 2, 4 ...); the right page is the next.</summary>
        public int Page { get; internal set; }

        public BookNumbering Numbering { get; internal set; }

        /// <summary>The cover word a script chose (BROWN, BLUE), or null for the plain book.png.</summary>
        public string Cover { get; internal set; }

        /// <summary>The lettering word the pages are written in (CORSIVA8, CALISTO ...).</summary>
        public string Lettering { get; internal set; }

        /// <summary>bTurnPage: a page was turned by an arrow still held.</summary>
        internal bool TurnHeld;
    }

    /// <summary>B: the book is shown or hidden.</summary>
    public sealed class ToggleBook : ICommand
    {
        public ToggleBook(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>N: the next book in folder order (wrapping), at its first spread, in the N key's lettering.</summary>
    public sealed class NextBook : ICommand
    {
        public NextBook(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>A turn of the shown book by one spread back (-1) or on (+1), as an arrow or a turn of the wheel does (scripted runs send this).</summary>
    public sealed class TurnPage : ICommand
    {
        public TurnPage(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }
        public int Step { get; }
    }

    /// <summary>Scripted runs: book <see cref="Book"/> loaded (as loadBook did) and shown at the spread holding page <see cref="Page"/> (0 first).</summary>
    public sealed class OpenBook : ICommand
    {
        public OpenBook(PlayerId player, int book, int page = 0) { Player = player; Book = book; Page = page; }
        public PlayerId Player { get; }
        public int Book { get; }
        public int Page { get; }
    }

    /// <summary>A player's book was shown or hidden, changed, turned, or drawn differently.</summary>
    public sealed class BookChanged : IGameEvent
    {
        public BookChanged(long tick, PlayerId player, bool shown, int book, int page)
        {
            Tick = tick;
            Player = player;
            Shown = shown;
            Book = book;
            Page = page;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Shown { get; }
        public int Book { get; }
        public int Page { get; }
    }

    /// <summary>
    /// The books (U.OVW-US-A16; Book.vb, Input.vb's page and book paths, frmMain's B and N, Script.cmdChangeBook,
    /// RECONSTRUCTION 11): the five books read two pages at a time.
    /// <para>
    /// Each player starts with the start book of hud.json "book" (Grimoire de'Alchemiste) loaded in its lettering and
    /// numbering, the plain cover, hidden. B (<see cref="ToggleBook"/>) shows and hides it; N (<see cref="NextBook"/>)
    /// loads the next book in folder order (wrapping) at its first spread in the N key's lettering (Corsiva), Roman
    /// numbered, the cover kept, shown or hidden as it was. While it is shown a new press of left or right (the arrows
    /// read from the player's input with the original's one latch for both, bTurnPage) or a turn of the wheel (before the
    /// pack and the zoom: toward the screen back, away on) turns a spread, clamped to the first and the last; the arrows
    /// then only mark their direction for the leader (<see cref="Party.BookShown"/>); and the move button hides it. With
    /// the pack open the pack's arrows wait, Delete hides the book and keeps the pack open, and the drop key (Backspace or
    /// the move button) hides the book instead of dropping (<see cref="InventoryControl.Books"/>).
    /// </para>
    /// <para>
    /// Scripts (CHANGE BOOK, through <see cref="ScriptInterpreter.Books"/>) load the book named like the pack's selected
    /// stack (TO ITEM; a name that is no book reloads the open one) or by number (NUM; -1 reloads the open one), show and
    /// hide it, and set its numbering, cover and lettering: a book item's script does all of these, which is how a book
    /// is opened from the pack. Loading always starts at the first spread, Roman numbered, keeping the lettering.
    /// </para>
    /// Installed after the pack (so a drop with the book shown sees it shown) and before the party (so its arrows are
    /// marked only). Presentation reads <see cref="For"/>, <see cref="Current"/> and <see cref="Screen"/>.
    /// </summary>
    public sealed class BookReader : ITickSystem
    {
        readonly Dictionary<PlayerId, BookView> views = new Dictionary<PlayerId, BookView>();
        readonly int startBook;

        public BookReader(BookData library, BookScreenDefinition screen, Party party)
        {
            Library = library ?? throw new ArgumentNullException(nameof(library));
            Screen = screen ?? throw new ArgumentNullException(nameof(screen));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            if (library.Books.Count == 0) throw new ArgumentException("There must be at least one book.", nameof(library));
            startBook = IndexOf(screen.StartTitle);
            if (startBook < 0) throw new ArgumentException("The start book \"" + screen.StartTitle + "\" is not one of the books.", nameof(screen));
        }

        /// <summary>The books, in folder order.</summary>
        public BookData Library { get; }

        /// <summary>How the book is drawn, and how it starts.</summary>
        public BookScreenDefinition Screen { get; }

        public Party Party { get; }

        public BookReader Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<ToggleBook>(HandleToggle);
            game.Register<NextBook>(HandleNext);
            game.Register<TurnPage>(HandleTurn);
            game.Register<OpenBook>(HandleOpen);
            Party.BookShown = Shown;
            game.AddSystem(this);
            return this;
        }

        /// <summary><paramref name="player"/>'s book, as loadBooks left it at start-up until something changes it.</summary>
        public BookView For(PlayerId player)
        {
            if (!views.TryGetValue(player, out var v))
            {
                views[player] = v = new BookView
                {
                    Book = startBook,
                    Numbering = Screen.Numbering,
                    Lettering = Screen.StartLettering,
                };
            }

            return v;
        }

        /// <summary>The book <paramref name="player"/> has open (shown or not).</summary>
        public BookDefinition Current(PlayerId player) => Library.Books[For(player).Book];

        /// <summary>bBook for <paramref name="player"/>.</summary>
        public bool Shown(PlayerId player) => views.TryGetValue(player, out var v) && v.Shown;

        /// <summary>The index of the book titled <paramref name="title"/>, or -1.</summary>
        public int IndexOf(string title)
        {
            for (var i = 0; i < Library.Books.Count; i++)
                if (Library.Books[i].Title == title) return i;
            return -1;
        }

        /// <summary>The last spread's left page of book <paramref name="book"/> (Pages.Length - 2, never below 0).</summary>
        public int LastPage(int book) => Math.Max(0, Library.Books[book].Pages.Count - 2);

        // ---- commands ---------------------------------------------------------------------------------------------

        CommandResult HandleToggle(Game game, ToggleBook c)
        {
            var v = For(c.Player);
            v.Shown = !v.Shown;
            return CommandResult.Accept(Changed(c.Player, v, game.Tick));
        }

        /// <summary>frmMain's N: currBook on (wrapping), loadBook in the N key's lettering.</summary>
        CommandResult HandleNext(Game game, NextBook c)
        {
            var v = For(c.Player);
            Load(v, v.Book < Library.Books.Count - 1 ? v.Book + 1 : 0);
            v.Lettering = Screen.NextLettering;
            return CommandResult.Accept(Changed(c.Player, v, game.Tick));
        }

        CommandResult HandleTurn(Game game, TurnPage c)
        {
            if (c.Step != 1 && c.Step != -1) return CommandResult.Reject("Turn back (-1) or on (+1).");
            var v = For(c.Player);
            if (!v.Shown) return CommandResult.Reject("The book is not shown.");
            Turn(v, c.Step);
            return CommandResult.Accept(Changed(c.Player, v, game.Tick));
        }

        CommandResult HandleOpen(Game game, OpenBook c)
        {
            if (c.Book < 0 || c.Book >= Library.Books.Count) return CommandResult.Reject("There is no book " + c.Book + " (there are " + Library.Books.Count + ").");
            if (c.Page < 0 || c.Page >= Library.Books[c.Book].Pages.Count) return CommandResult.Reject(Library.Books[c.Book].Title + " has no page " + c.Page + ".");
            var v = For(c.Player);
            Load(v, c.Book);
            v.Page = Math.Min(c.Page - c.Page % 2, LastPage(c.Book));
            v.Shown = true;
            return CommandResult.Accept(Changed(c.Player, v, game.Tick));
        }

        static BookChanged Changed(PlayerId player, BookView v, long tick) => new BookChanged(tick, player, v.Shown, v.Book, v.Page);

        /// <summary>loadBook: the book, Roman numbered (every caller's numbering), at its first spread; the lettering is the caller's.</summary>
        void Load(BookView v, int book)
        {
            v.Book = book;
            v.Numbering = Screen.Numbering;
            v.Page = 0;
        }

        /// <summary>lastPage / nextPage: two pages back or on, held within the first and the last spread.</summary>
        void Turn(BookView v, int step)
        {
            v.Page = Math.Max(0, Math.Min(v.Page + 2 * step, LastPage(v.Book)));
        }

        // ---- the tick: the arrows' page turns (getKeyboard, with KeysUp's bTurnPage) and the move button (getMouse) ------

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            // Inside oneFrameRender's 'If Not bPause' (getInput and the updates): nothing here while paused.
            if (game.IsHeld) return;
            foreach (var player in game.Players)
            {
                if (!views.TryGetValue(player, out var v)) continue;
                var input = Party.InputOf(player);
                var before = (v.Shown, v.Page);
                // actionLeft then actionRight: lastPage / nextPage, one turn a press for the pair.
                if (v.Shown && input.Left && !v.TurnHeld)
                {
                    v.TurnHeld = true;
                    Turn(v, -1);
                }

                if (v.Shown && input.Right && !v.TurnHeld)
                {
                    v.TurnHeld = true;
                    Turn(v, 1);
                }

                // getMouse: the move button (unless the left button came first) hides the book.
                if (input.Move && !input.PickUp) v.Shown = false;
                // KeysUp: neither arrow held.
                if (!input.Left && !input.Right) v.TurnHeld = false;
                if ((v.Shown, v.Page) != before) events.Add(Changed(player, v, tick));
            }
        }

        /// <summary>
        /// getMouse's wheel while the book is shown (it comes before the pack and the zoom): a turn toward the screen
        /// (positive) goes back a spread, away on, unless an arrow has just turned it. True when the book took it.
        /// </summary>
        public bool UseWheel(PlayerId player, int steps)
        {
            if (steps == 0 || !Shown(player)) return false;
            var v = For(player);
            if (v.TurnHeld) return true;
            for (var i = 0; i < Math.Abs(steps); i++) Turn(v, steps > 0 ? -1 : 1);
            return true;
        }

        // ---- what scripts and the pack do (cmdChangeBook, actionInventory, actionDrop) --------------------------------

        /// <summary>CHANGE BOOK TO ITEM: loadBook by title; a title that is no book reloads the one open.</summary>
        internal void LoadTitle(PlayerId player, string title, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            var book = IndexOf(title);
            Load(v, book >= 0 ? book : v.Book);
            events.Add(Changed(player, v, tick));
        }

        /// <summary>CHANGE BOOK NUM n: loadBook by number (-1 reloads the one open); false when there is no such book.</summary>
        internal bool LoadNumber(PlayerId player, int number, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (number < -1 || number >= Library.Books.Count) return false;
            Load(v, number == -1 ? v.Book : number);
            events.Add(Changed(player, v, tick));
            return true;
        }

        /// <summary>Shows or hides <paramref name="player"/>'s book (CHANGE BOOK SHOW, and the pack's Delete and drop).</summary>
        public void SetShown(PlayerId player, bool shown, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.Shown == shown) return;
            v.Shown = shown;
            events.Add(Changed(player, v, tick));
        }

        internal void SetNumbering(PlayerId player, BookNumbering numbering, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            v.Numbering = numbering;
            events.Add(Changed(player, v, tick));
        }

        /// <summary>CHANGE BOOK COVER: one of the covers' words; any other does nothing (false).</summary>
        internal bool SetCover(PlayerId player, string cover, long tick, ICollection<IGameEvent> events)
        {
            if (Screen.CoverPicture(cover) == null) return false;
            For(player).Cover = cover;
            events.Add(Changed(player, For(player), tick));
            return true;
        }

        /// <summary>CHANGE BOOK FONT: one of the letterings' words; any other (SYMBOL) does nothing (false).</summary>
        internal bool SetLettering(PlayerId player, string lettering, long tick, ICollection<IGameEvent> events)
        {
            if (Screen.Lettering(lettering) == null) return false;
            For(player).Lettering = lettering;
            events.Add(Changed(player, For(player), tick));
            return true;
        }

        // ---- the page numbers (getRomanNumeral) ---------------------------------------------------------------------

        /// <summary>
        /// The number written under page <paramref name="number"/> (1 first): Roman, standard, or nothing (none and
        /// ancient). Roman numerals are correct at every number (the original's table repeated wrong values past 64; BIBLE).
        /// </summary>
        public static string PageNumber(BookNumbering numbering, int number)
        {
            switch (numbering)
            {
                case BookNumbering.RomanNumeral: return Roman(number);
                case BookNumbering.Standard: return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }

        static readonly (int Value, string Text)[] Numerals =
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
            (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        };

        /// <summary>A Roman numeral (empty below 1, as getRomanNumeral's table had no entry there).</summary>
        public static string Roman(int number)
        {
            var text = new StringBuilder();
            foreach (var (value, numeral) in Numerals)
            {
                while (number >= value)
                {
                    text.Append(numeral);
                    number -= value;
                }
            }

            return text.ToString();
        }
    }
}

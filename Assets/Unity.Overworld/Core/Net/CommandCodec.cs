using System;
using System.IO;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// A player's <see cref="ICommand"/> on the wire (U.OVW-T46, the family's online rule 9): an online client's input
    /// reader makes exactly the commands local play makes, and its <c>GameHost</c> hands each one to its networked player,
    /// which sends these bytes to the one authority. The authority decodes them for the sender's own seat
    /// (<see cref="Decode"/>), never for whatever player id the bytes name, so a client only ever acts as itself.
    /// <para>Only what a player's controls make travels (<see cref="IsSent"/>): the keys, the mouse, the console, the
    /// pack, the books, the quests, the ring, rest and sleep. The authority's own commands (joining, leaving, loading a
    /// world) and the developer's scripted-run commands (placing a hero or the cursor, setting the clock, the weather or
    /// a hero's vitals, giving an item) are never sent.</para>
    /// </summary>
    public static class CommandCodec
    {
        /// <summary>The wire version; bytes with any other first byte are refused.</summary>
        public const byte Version = 1;

        enum Kind : byte
        {
            SetMoveInput = 1,
            SetViewInput = 2,
            ToggleExamine = 3,
            ToggleFreeCamera = 4,
            CycleLeader = 5,
            SelectHero = 6,
            ToggleTeam = 7,
            Rest = 8,
            Sleep = 9,
            CyclePickup = 10,
            PickUp = 11,
            ToggleInventory = 12,
            ToggleQuickBar = 13,
            MoveInventoryCursor = 14,
            SwapItem = 15,
            DropItem = 16,
            UseItem = 17,
            QuickUse = 18,
            ChooseTarget = 19,
            ToggleQuestScreen = 20,
            PressQuestKey = 21,
            ToggleBook = 22,
            NextBook = 23,
            TurnPage = 24,
            ToggleBattle = 25,
            TurnRing = 26,
            ChooseFromRing = 27,
            CycleWeather = 28,
            ToggleOptions = 29,
            PressOptionsKey = 30,
            ToggleHeroScreen = 31,
            ToggleFps = 32,
            TogglePause = 33,
            PressConsoleKey = 34,
            RunScript = 35,
            RecallHero = 36,
        }

        /// <summary>Whether <paramref name="command"/> is one a player's controls make, and so travels to the authority.</summary>
        public static bool IsSent(ICommand command) => command != null && KindOf(command).HasValue;

        /// <summary>The command as bytes. The player id is left out: the authority puts the sender's seat back.</summary>
        /// <exception cref="ArgumentException">The command is not one that is sent (<see cref="IsSent"/>).</exception>
        public static byte[] Encode(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var kind = KindOf(command) ?? throw new ArgumentException(command.GetType().Name + " is not sent to the authority.", nameof(command));
            using (var stream = new MemoryStream(32))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                w.Write((byte)kind);
                switch (command)
                {
                    case SetMoveInput c:
                        var m = c.Input;
                        w.Write(Bits(m.Left, m.Right, m.Up, m.Down, m.Run, m.Move, m.PickUp));
                        w.Write(m.CursorX);
                        w.Write(m.CursorY);
                        break;
                    case SetViewInput c:
                        var v = c.Input;
                        w.Write(v.MouseX);
                        w.Write(v.MouseY);
                        w.Write(v.Wheel);
                        w.Write(Bits(v.Zoom, v.Fast, v.StrafeLeft, v.StrafeRight, v.StrafeUp, v.StrafeDown, v.Isometric, v.Deisometric));
                        break;
                    case CycleLeader c: w.Write(c.Step); break;
                    case SelectHero c: w.Write(c.Hero); break;
                    case Sleep c:
                        w.Write(c.Hours.HasValue);
                        w.Write(c.Hours ?? 0);
                        break;
                    case CyclePickup c: w.Write(c.Step); break;
                    case MoveInventoryCursor c: w.Write(c.Slot); break;
                    case QuickUse c: w.Write(c.Slot); break;
                    case ChooseTarget c: w.Write(c.Step); break;
                    case PressQuestKey c: w.Write((int)c.Key); break;
                    case TurnPage c: w.Write(c.Step); break;
                    case TurnRing c: w.Write(c.Step); break;
                    case PressOptionsKey c: w.Write((int)c.Key); break;
                    case PressConsoleKey c:
                        w.Write(c.Key ?? string.Empty);
                        w.Write(c.Shift);
                        break;
                    case RunScript c:
                        w.Write(c.Script ?? string.Empty);
                        w.Write(c.Hero.HasValue);
                        w.Write(c.Hero ?? 0);
                        break;
                    case RecallHero c: w.Write(c.Hero); break;
                }

                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// The command <paramref name="bytes"/> carry, issued by <paramref name="sender"/> (the seat the bytes arrived
        /// from); null for bytes that are not a command of this version.
        /// </summary>
        public static ICommand Decode(byte[] bytes, PlayerId sender)
        {
            if (bytes == null || bytes.Length < 2 || bytes[0] != Version) return null;
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (var r = new BinaryReader(stream))
                {
                    r.ReadByte();
                    var command = Read((Kind)r.ReadByte(), r, sender);
                    return command != null && stream.Position == stream.Length ? command : null;
                }
            }
            catch (EndOfStreamException) { return null; }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
        }

        static ICommand Read(Kind kind, BinaryReader r, PlayerId p)
        {
            switch (kind)
            {
                case Kind.SetMoveInput:
                {
                    var b = r.ReadByte();
                    var x = r.ReadSingle();
                    var y = r.ReadSingle();
                    return new SetMoveInput(p, new MoveInput(Bit(b, 0), Bit(b, 1), Bit(b, 2), Bit(b, 3), Bit(b, 4), Bit(b, 5), x, y, Bit(b, 6)));
                }
                case Kind.SetViewInput:
                {
                    var mx = r.ReadSingle();
                    var my = r.ReadSingle();
                    var wheel = r.ReadInt32();
                    var b = r.ReadByte();
                    return new SetViewInput(p, new ViewInput(mx, my, wheel, Bit(b, 0), Bit(b, 1), Bit(b, 2), Bit(b, 3), Bit(b, 4), Bit(b, 5), Bit(b, 6), Bit(b, 7)));
                }
                case Kind.ToggleExamine: return new ToggleExamine(p);
                case Kind.ToggleFreeCamera: return new ToggleFreeCamera(p);
                case Kind.CycleLeader: return new CycleLeader(p, r.ReadInt32());
                case Kind.SelectHero: return new SelectHero(p, r.ReadInt32());
                case Kind.ToggleTeam: return new ToggleTeam(p);
                case Kind.Rest: return new Rest(p);
                case Kind.Sleep:
                {
                    var has = r.ReadBoolean();
                    var hours = r.ReadInt32();
                    return new Sleep(p, has ? hours : (int?)null);
                }
                case Kind.CyclePickup: return new CyclePickup(p, r.ReadInt32());
                case Kind.PickUp: return new PickUp(p);
                case Kind.ToggleInventory: return new ToggleInventory(p);
                case Kind.ToggleQuickBar: return new ToggleQuickBar(p);
                case Kind.MoveInventoryCursor: return new MoveInventoryCursor(p, r.ReadInt32());
                case Kind.SwapItem: return new SwapItem(p);
                case Kind.DropItem: return new DropItem(p);
                case Kind.UseItem: return new UseItem(p);
                case Kind.QuickUse: return new QuickUse(p, r.ReadInt32());
                case Kind.ChooseTarget: return new ChooseTarget(p, r.ReadInt32());
                case Kind.ToggleQuestScreen: return new ToggleQuestScreen(p);
                case Kind.PressQuestKey: return new PressQuestKey(p, (QuestKey)r.ReadInt32());
                case Kind.ToggleBook: return new ToggleBook(p);
                case Kind.NextBook: return new NextBook(p);
                case Kind.TurnPage: return new TurnPage(p, r.ReadInt32());
                case Kind.ToggleBattle: return new ToggleBattle(p);
                case Kind.TurnRing: return new TurnRing(p, r.ReadInt32());
                case Kind.ChooseFromRing: return new ChooseFromRing(p);
                case Kind.CycleWeather: return new CycleWeather(p);
                case Kind.ToggleOptions: return new ToggleOptions(p);
                case Kind.PressOptionsKey: return new PressOptionsKey(p, (OptionsKey)r.ReadInt32());
                case Kind.ToggleHeroScreen: return new ToggleHeroScreen(p);
                case Kind.ToggleFps: return new ToggleFps(p);
                case Kind.TogglePause: return new TogglePause(p);
                case Kind.PressConsoleKey:
                {
                    var key = r.ReadString();
                    return new PressConsoleKey(p, key, r.ReadBoolean());
                }
                case Kind.RunScript:
                {
                    var script = r.ReadString();
                    var has = r.ReadBoolean();
                    var hero = r.ReadInt32();
                    return new RunScript(p, script, has ? hero : (int?)null);
                }
                case Kind.RecallHero: return new RecallHero(p, r.ReadInt32());
                default: return null;
            }
        }

        static Kind? KindOf(ICommand command)
        {
            switch (command)
            {
                case SetMoveInput _: return Kind.SetMoveInput;
                case SetViewInput _: return Kind.SetViewInput;
                case ToggleExamine _: return Kind.ToggleExamine;
                case ToggleFreeCamera _: return Kind.ToggleFreeCamera;
                case CycleLeader _: return Kind.CycleLeader;
                case SelectHero _: return Kind.SelectHero;
                case ToggleTeam _: return Kind.ToggleTeam;
                case Rest _: return Kind.Rest;
                case Sleep _: return Kind.Sleep;
                case CyclePickup _: return Kind.CyclePickup;
                case PickUp _: return Kind.PickUp;
                case ToggleInventory _: return Kind.ToggleInventory;
                case ToggleQuickBar _: return Kind.ToggleQuickBar;
                case MoveInventoryCursor _: return Kind.MoveInventoryCursor;
                case SwapItem _: return Kind.SwapItem;
                case DropItem _: return Kind.DropItem;
                case UseItem _: return Kind.UseItem;
                case QuickUse _: return Kind.QuickUse;
                case ChooseTarget _: return Kind.ChooseTarget;
                case ToggleQuestScreen _: return Kind.ToggleQuestScreen;
                case PressQuestKey _: return Kind.PressQuestKey;
                case ToggleBook _: return Kind.ToggleBook;
                case NextBook _: return Kind.NextBook;
                case TurnPage _: return Kind.TurnPage;
                case ToggleBattle _: return Kind.ToggleBattle;
                case TurnRing _: return Kind.TurnRing;
                case ChooseFromRing _: return Kind.ChooseFromRing;
                case CycleWeather _: return Kind.CycleWeather;
                case ToggleOptions _: return Kind.ToggleOptions;
                case PressOptionsKey _: return Kind.PressOptionsKey;
                case ToggleHeroScreen _: return Kind.ToggleHeroScreen;
                case ToggleFps _: return Kind.ToggleFps;
                case TogglePause _: return Kind.TogglePause;
                case PressConsoleKey _: return Kind.PressConsoleKey;
                case RunScript _: return Kind.RunScript;
                case RecallHero _: return Kind.RecallHero;
                default: return null;
            }
        }

        static byte Bits(params bool[] bits)
        {
            byte b = 0;
            for (var i = 0; i < bits.Length; i++)
                if (bits[i]) b |= (byte)(1 << i);
            return b;
        }

        static bool Bit(byte b, int i) => (b & (1 << i)) != 0;
    }
}

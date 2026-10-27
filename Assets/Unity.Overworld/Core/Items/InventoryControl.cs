using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>What a player's keys work on now (Inventory.bInventory, HeroSelect.bHeroSelect).</summary>
    public enum InputFocus
    {
        /// <summary>The world: the leader moves, picks up and rests.</summary>
        World,

        /// <summary>The pack is open: the arrows move its cursor, the run key swaps, Enter uses, the move button drops.</summary>
        Inventory,

        /// <summary>An item's target is being chosen: left and right choose the hero, Enter uses the item on it.</summary>
        Target,
    }

    /// <summary>One player's view of the party's pack: the panel, its cursor, the swap mark, the item's target and the quick bar.</summary>
    public sealed class PackView
    {
        internal PackView() { }

        /// <summary>The panel is open (bInventory).</summary>
        public bool Open { get; internal set; }

        /// <summary>The selected stack (currItem): what Enter uses, the run key marks or swaps and Backspace drops.</summary>
        public int Cursor { get; internal set; }

        /// <summary>A stack is marked for a swap (bSwap) at <see cref="SwapFrom"/>; the next run-key press swaps it with the cursor's.</summary>
        public bool Swapping { get; internal set; }

        public int SwapFrom { get; internal set; }

        /// <summary>The item at the cursor waits for its target (bHeroSelect): left and right choose it, Enter uses it on it.</summary>
        public bool Choosing { get; internal set; }

        /// <summary>The hero items are used on (selectedHero): the leader, until another is chosen.</summary>
        public int Target { get; internal set; }

        /// <summary>The quick bar is shown (bQuickBar, Q).</summary>
        public bool QuickBar { get; internal set; }

        /// <summary>What the keys work on: the open pack first, then the target being chosen, else the world.</summary>
        public InputFocus Focus => Open ? InputFocus.Inventory : Choosing ? InputFocus.Target : InputFocus.World;

        internal int LastLeader = -1;
        internal bool LeftHeld, RightHeld, UpHeld, DownHeld, RunHeld, UseHeld, MoveHeld;
    }

    /// <summary>The Delete key or the inventory button: the panel opens or closes (not in battle mode).</summary>
    public sealed class ToggleInventory : ICommand
    {
        public ToggleInventory(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>The Q key: the quick bar shows or hides.</summary>
    public sealed class ToggleQuickBar : ICommand
    {
        public ToggleQuickBar(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>A click on an icon of the open panel (overInventory), or a scripted run: the cursor goes to stack <see cref="Slot"/>.</summary>
    public sealed class MoveInventoryCursor : ICommand
    {
        public MoveInventoryCursor(PlayerId player, int slot) { Player = player; Slot = slot; }
        public PlayerId Player { get; }
        public int Slot { get; }
    }

    /// <summary>One press of the run key with the panel open (actionRun): marks the cursor's stack, or swaps it with the marked one.</summary>
    public sealed class SwapItem : ICommand
    {
        public SwapItem(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>
    /// One press of Backspace (actionDrop), or of the move button with the panel open: the choosing of a target stops, and
    /// with the panel open in adventure mode one of the cursor's stack falls by the leader (an empty pack closes the panel).
    /// </summary>
    public sealed class DropItem : ICommand
    {
        public DropItem(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>
    /// One press of Enter or the left button with the panel open or a target being chosen (actionPickup's use): see
    /// <see cref="InventoryControl"/>.
    /// </summary>
    public sealed class UseItem : ICommand
    {
        public UseItem(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>One of the 1 to 0 keys (actionQuickItem): the stack in quick slot <see cref="Slot"/> (0 to 9), if there is one, is used.</summary>
    public sealed class QuickUse : ICommand
    {
        public QuickUse(PlayerId player, int slot) { Player = player; Slot = slot; }
        public PlayerId Player { get; }
        public int Slot { get; }
    }

    /// <summary>Left (-1) or right (+1) while a target is chosen (selectLastHero / selectNextHero): the previous or next hero, wrapping.</summary>
    public sealed class ChooseTarget : ICommand
    {
        public ChooseTarget(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }
        public int Step { get; }
    }

    /// <summary>
    /// Puts <see cref="Count"/> of pickup <see cref="Pickup"/>'s item into the pack, as picking it up would (for scripted
    /// runs and the script story); a UseOnPickup item (gold) is never carried.
    /// </summary>
    public sealed class GiveItem : ICommand
    {
        public GiveItem(PlayerId player, int pickup, int count = 1) { Player = player; Pickup = pickup; Count = count; }
        public PlayerId Player { get; }
        public int Pickup { get; }
        public int Count { get; }
    }

    /// <summary>The panel opened or closed.</summary>
    public sealed class InventoryToggled : IGameEvent
    {
        public InventoryToggled(long tick, PlayerId player, bool open) { Tick = tick; Player = player; Open = open; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Open { get; }
    }

    /// <summary>The quick bar showed or hid.</summary>
    public sealed class QuickBarToggled : IGameEvent
    {
        public QuickBarToggled(long tick, PlayerId player, bool shown) { Tick = tick; Player = player; Shown = shown; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Shown { get; }
    }

    /// <summary>The panel's cursor moved to another stack.</summary>
    public sealed class InventoryCursorMoved : IGameEvent
    {
        public InventoryCursorMoved(long tick, PlayerId player, int slot) { Tick = tick; Player = player; Slot = slot; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public int Slot { get; }
    }

    /// <summary>A stack was marked for a swap (<see cref="Swapped"/> false), or two stacks changed places.</summary>
    public sealed class InventorySwapped : IGameEvent
    {
        public InventorySwapped(long tick, PlayerId player, int from, int to, bool swapped)
        {
            Tick = tick;
            Player = player;
            From = from;
            To = to;
            Swapped = swapped;
        }

        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The marked stack's slot.</summary>
        public int From { get; }

        /// <summary>The cursor's slot (the same as <see cref="From"/> when only marked).</summary>
        public int To { get; }

        public bool Swapped { get; }
    }

    /// <summary>One of a stack fell out of the pack into the world, at drop slot <see cref="Drop"/>.</summary>
    public sealed class ItemDropped : IGameEvent
    {
        public ItemDropped(long tick, PlayerId player, int hero, string name, int drop, float x, float y)
        {
            Tick = tick;
            Player = player;
            Hero = hero;
            Name = name;
            Drop = drop;
            X = x;
            Y = y;
        }

        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The leader it fell by.</summary>
        public int Hero { get; }

        public string Name { get; }
        public int Drop { get; }
        public float X { get; }
        public float Y { get; }
    }

    /// <summary>An item was used on a hero; <see cref="Consumed"/> when one of it left the pack (all but a Reusable item).</summary>
    public sealed class ItemUsed : IGameEvent
    {
        public ItemUsed(long tick, PlayerId player, int target, string name, bool consumed)
        {
            Tick = tick;
            Player = player;
            Target = target;
            Name = name;
            Consumed = consumed;
        }

        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The hero its script ran on (HERO).</summary>
        public int Target { get; }

        public string Name { get; }
        public bool Consumed { get; }
    }

    /// <summary>The choosing of an item's target started or stopped, or another target was chosen.</summary>
    public sealed class TargetChanged : IGameEvent
    {
        public TargetChanged(long tick, PlayerId player, bool choosing, int target)
        {
            Tick = tick;
            Player = player;
            Choosing = choosing;
            Target = target;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public bool Choosing { get; }
        public int Target { get; }
    }

    /// <summary>Items were put into the pack (<see cref="GiveItem"/>).</summary>
    public sealed class ItemGiven : IGameEvent
    {
        public ItemGiven(long tick, PlayerId player, string name, int count, InventoryItem stack)
        {
            Tick = tick;
            Player = player;
            Name = name;
            Count = count;
            Stack = stack;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public string Name { get; }
        public int Count { get; }
        public InventoryItem Stack { get; }
    }

    /// <summary>A hero became burdened (the pack over the party's limit) or no longer is.</summary>
    public sealed class HeroBurdened : IGameEvent
    {
        public HeroBurdened(long tick, int hero, bool burdened) { Tick = tick; Hero = hero; Burdened = burdened; }
        public long Tick { get; }
        public int Hero { get; }
        public bool Burdened { get; }
    }

    /// <summary>
    /// The inventory (U.OVW-US-A13; Inventory.vb, QuickBar.vb, HeroSelect.vb and Input.vb's inventory paths,
    /// RECONSTRUCTION 4.6, 4.7, 8.3 and 8.5): each player's panel over the party's one <see cref="Inventory"/>.
    /// <para>
    /// The panel opens and closes with <see cref="ToggleInventory"/> (Delete or the inventory button; not in battle mode;
    /// any swap mark is cleared). While it is open, each new press of an arrow moves the cursor (left and right wrap,
    /// up and down move a row of ten and stop at the ends), the run key marks the cursor's stack and a second press swaps
    /// it with the cursor's (<see cref="SwapItem"/>), the move button drops (<see cref="DropItem"/>, as Backspace does),
    /// the wheel moves the cursor (before it zooms), and Enter or the left button uses (<see cref="UseItem"/>): over an
    /// icon other than the cursor's the press only moves the cursor there; with a swap marked it swaps; otherwise in solo
    /// mode the item is used on the leader, in team mode a GroupItem or Reusable item is used at once (on the last target)
    /// and any other closes the panel and waits for its target to be chosen: left and right (or the wheel) choose the hero
    /// and Enter uses it on that hero, Backspace stops choosing. The 1 to 0 keys (<see cref="QuickUse"/>) put the cursor on
    /// that quick slot's stack and, in team mode, wait for its target; in solo mode use it on the leader. Q shows the quick bar.
    /// </para>
    /// <para>
    /// Using runs the item's script (<see cref="Scripts"/>) with HERO the target, then one of it leaves the pack unless it
    /// is Reusable; a script that fails (<see cref="ScriptFailed"/>) leaves the item in the pack. Dropping puts
    /// one of the cursor's stack into the world by the leader (<see cref="Pickups.Drop"/>), where it can be picked up again.
    /// The weight carried burdens the heroes (<see cref="Party.Pack"/>).
    /// </para>
    /// Installed after the pickups and before the party, so the keys act in the tick they are pressed, as getInput ran
    /// before the heroes. Presentation reads <see cref="For"/> and <see cref="Inventory"/>.
    /// </summary>
    public sealed class InventoryControl : ITickSystem
    {
        readonly Dictionary<PlayerId, PackView> views = new Dictionary<PlayerId, PackView>();

        public InventoryControl(Pickups pickups, InventoryPanelDefinition panel, QuickBarDefinition quickBar, CameraControl view = null)
        {
            Pickups = pickups ?? throw new ArgumentNullException(nameof(pickups));
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            QuickBarLayout = quickBar ?? throw new ArgumentNullException(nameof(quickBar));
            View = view;
            Scripts = pickups.Scripts;
        }

        public Pickups Pickups { get; }
        public Party Party => Pickups.Party;
        public Inventory Inventory => Pickups.Inventory;
        public InventoryPanelDefinition Panel { get; }
        public QuickBarDefinition QuickBarLayout { get; }

        /// <summary>Whose virtual mouse finds the icon a click is over (null: clicks never land on an icon).</summary>
        public CameraControl View { get; }

        /// <summary>Runs an item's script on its target when it is used.</summary>
        public IScriptRunner Scripts { get; set; }

        /// <summary>
        /// The players' books (null: none): while one is shown the panel's arrows wait, Delete hides it and leaves the
        /// panel open, and the drop key hides it instead of dropping (Input.vb's bBook tests).
        /// </summary>
        public BookReader Books { get; set; }

        bool BookShown(PlayerId player) => Books != null && Books.Shown(player);

        /// <summary>The party's limit (28.5): every hero's Strength + Agility / 2.</summary>
        public float WeightLimit => Party.Data.PartyWeightLimit;

        /// <summary><paramref name="player"/>'s panel, cursor, swap mark, target and quick bar.</summary>
        public PackView For(PlayerId player)
        {
            if (!views.TryGetValue(player, out var v))
            {
                views[player] = v = new PackView();
                v.Target = Party.LeaderOf(player);
                v.LastLeader = v.Target;
            }

            return v;
        }

        /// <summary>What <paramref name="player"/>'s keys work on now.</summary>
        public InputFocus FocusOf(PlayerId player) => For(player).Focus;

        public InventoryControl Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<ToggleInventory>(HandleToggle);
            game.Register<ToggleQuickBar>(HandleQuickBar);
            game.Register<MoveInventoryCursor>(HandleCursor);
            game.Register<SwapItem>((g, c) => CommandResult.Accept(Swap(c.Player, g.Tick)));
            game.Register<DropItem>((g, c) => CommandResult.Accept(Drop(c.Player, g.Tick)));
            game.Register<UseItem>(HandleUse);
            game.Register<QuickUse>(HandleQuickUse);
            game.Register<ChooseTarget>(HandleChoose);
            game.Register<GiveItem>(HandleGive);
            Party.Focus = FocusOf;
            Pickups.Focus = FocusOf;
            Party.Pack = Inventory;
            Party.Rested = Close;
            Party.BattleLeft = HideQuickBar;
            Party.QuickBarShown = ShowQuickBar;
            Party.OpenPack = OpenForRing;
            game.AddSystem(this);
            return this;
        }

        // ---- commands ---------------------------------------------------------------------------------------------

        /// <summary>actionInventory: the swap mark clears; with the book shown it is hidden and the panel opens, otherwise out of battle the panel opens or closes.</summary>
        CommandResult HandleToggle(Game game, ToggleInventory c)
        {
            var v = For(c.Player);
            v.Swapping = false;
            if (BookShown(c.Player))
            {
                var events = new List<IGameEvent>();
                Books.SetShown(c.Player, false, game.Tick, events);
                SetOpen(c.Player, true, game.Tick, events);
                return CommandResult.Accept(events.ToArray());
            }

            if (Party.Leader(c.Player).Mode == HeroMode.Battle) return CommandResult.Reject("The pack does not open in battle mode (the ring's Item entry opens it).");
            v.Open = !v.Open;
            return CommandResult.Accept(new InventoryToggled(game.Tick, c.Player, v.Open));
        }

        /// <summary>actionBattleToggle on leaving battle: bQuickBar = False.</summary>
        void HideQuickBar(PlayerId player, long tick, ICollection<IGameEvent> events) => ShowQuickBar(player, false, tick, events);

        /// <summary>The ring's highlight (checkRingEffect): the Item entry shows the quick bar, any other hides it.</summary>
        void ShowQuickBar(PlayerId player, bool shown, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.QuickBar == shown) return;
            v.QuickBar = shown;
            events.Add(new QuickBarToggled(tick, player, shown));
        }

        /// <summary>The ring's Item entry (actionRingSelect): with items in the pack it opens; false when it is empty.</summary>
        bool OpenForRing(PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            if (Inventory.Items.Count == 0) return false;
            SetOpen(player, true, tick, events);
            return true;
        }

        CommandResult HandleQuickBar(Game game, ToggleQuickBar c)
        {
            var v = For(c.Player);
            v.QuickBar = !v.QuickBar;
            return CommandResult.Accept(new QuickBarToggled(game.Tick, c.Player, v.QuickBar));
        }

        CommandResult HandleCursor(Game game, MoveInventoryCursor c)
        {
            if (c.Slot < 0 || c.Slot >= Inventory.Items.Count) return CommandResult.Reject("There is no stack " + c.Slot + " in the pack (it holds " + Inventory.Items.Count + ").");
            var events = new List<IGameEvent>();
            MoveCursor(c.Player, c.Slot, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        CommandResult HandleUse(Game game, UseItem c)
        {
            var events = new List<IGameEvent>();
            Use(game, c.Player, false, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>actionQuickItem: a stack in that slot takes the cursor; in team mode its target is chosen, in solo it is used on the leader.</summary>
        CommandResult HandleQuickUse(Game game, QuickUse c)
        {
            if (c.Slot < 0 || c.Slot >= QuickBarLayout.Slots) return CommandResult.Reject("The quick slots are 0 to " + (QuickBarLayout.Slots - 1) + ".");
            if (c.Slot >= Inventory.Items.Count) return CommandResult.Accept();
            var v = For(c.Player);
            var events = new List<IGameEvent>();
            MoveCursor(c.Player, c.Slot, game.Tick, events);
            if (Party.Team) StartChoosing(c.Player, game.Tick, events);
            else
            {
                // The original left the target as it was (a hero chosen long ago); solo, it is the leader (BIBLE).
                v.Target = Party.LeaderOf(c.Player);
                ApplyUse(game, c.Player, game.Tick, events);
            }

            return CommandResult.Accept(events.ToArray());
        }

        CommandResult HandleChoose(Game game, ChooseTarget c)
        {
            if (c.Step != 1 && c.Step != -1) return CommandResult.Reject("Step to the previous (-1) or next (+1) hero.");
            var v = For(c.Player);
            if (!v.Choosing) return CommandResult.Reject("No item is waiting for its target.");
            StepTarget(v, c.Step);
            return CommandResult.Accept(new TargetChanged(game.Tick, c.Player, true, v.Target));
        }

        CommandResult HandleGive(Game game, GiveItem c)
        {
            var data = Pickups.Data;
            if (c.Pickup < 0 || c.Pickup >= data.Pickups.Count) return CommandResult.Reject("There is no pickup " + c.Pickup + ".");
            if (c.Count < 1) return CommandResult.Reject("Give at least one.");
            var def = data.Pickups[c.Pickup];
            if (def.Type == ItemType.UseOnPickup) return CommandResult.Reject(def.Name + " is used when it is picked up; it is never carried.");
            InventoryItem stack = null;
            for (var i = 0; i < c.Count; i++) stack = Inventory.Add(def, data.InventoryFile(def));
            return CommandResult.Accept(new ItemGiven(game.Tick, c.Player, def.Name, c.Count, stack));
        }

        // ---- the tick: the held keys' presses (getKeyboard and getMouse with their KeysUp latches) -------------------

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            // Inside oneFrameRender's 'If Not bPause' (getInput and the updates): nothing here while paused.
            if (game.IsHeld) return;
            foreach (var player in game.Players)
            {
                var v = For(player);
                // changeHero and actionHeroSelect set selectedHero to the new leader.
                var leader = Party.LeaderOf(player);
                if (leader != v.LastLeader)
                {
                    v.LastLeader = leader;
                    v.Target = leader;
                }

                var input = Party.InputOf(player);
                if (Pressed(input.Run, ref v.RunHeld) && v.Open) foreach (var e in Swap(player, tick)) events.Add(e);
                // The press that chose from the battle's ring (opening the pack) does not also use from it.
                if (Pressed(input.PickUp, ref v.UseHeld) && !Party.RingChoiceHeld(player)) Use(game, player, true, tick, events);
                if (Pressed(input.Move, ref v.MoveHeld) && v.Open) foreach (var e in Drop(player, tick)) events.Add(e);
                if (Pressed(input.Left, ref v.LeftHeld)) Arrow(player, -1, 0, tick, events);
                if (Pressed(input.Right, ref v.RightHeld)) Arrow(player, 1, 0, tick, events);
                if (Pressed(input.Up, ref v.UpHeld)) Arrow(player, 0, -1, tick, events);
                if (Pressed(input.Down, ref v.DownHeld)) Arrow(player, 0, 1, tick, events);
            }
        }

        /// <summary>A key's new press: held now and not last tick (the original's per-key "down" latches).</summary>
        static bool Pressed(bool held, ref bool wasHeld)
        {
            var pressed = held && !wasHeld;
            wasHeld = held;
            return pressed;
        }

        /// <summary>
        /// selectInventoryLeft / Right / Up / Down with the panel open, selectLastHero / selectNextHero while a target is
        /// chosen (left and right only).
        /// </summary>
        void Arrow(PlayerId player, int dx, int dy, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.Open)
            {
                // selectInventory*: not while the book is shown (its pages take left and right).
                if (BookShown(player)) return;
                var count = Inventory.Items.Count;
                var last = Math.Max(0, count - 1);
                var slot = v.Cursor;
                if (dx < 0) slot = slot > 0 ? slot - 1 : last;
                else if (dx > 0) slot = slot < count - 1 ? slot + 1 : 0;
                else if (dy < 0) slot = Math.Max(0, slot - Panel.Columns);
                else slot = Math.Min(slot + Panel.Columns, last);
                MoveCursor(player, slot, tick, events);
                return;
            }

            if (v.Choosing && dx != 0)
            {
                StepTarget(v, dx);
                events.Add(new TargetChanged(tick, player, true, v.Target));
            }
        }

        /// <summary>
        /// getMouse's wheel: with the panel open a turn toward the screen moves the cursor left and away right; while a
        /// target is chosen, toward the screen chooses the next hero and away the previous. True when it was used.
        /// </summary>
        public bool UseWheel(PlayerId player, int steps)
        {
            if (steps == 0) return false;
            var v = For(player);
            if (!v.Open && !v.Choosing) return false;
            var events = new List<IGameEvent>();
            for (var i = 0; i < Math.Abs(steps); i++)
            {
                if (v.Open) Arrow(player, steps > 0 ? -1 : 1, 0, 0, events);
                else StepTarget(v, steps > 0 ? 1 : -1);
            }

            return true;
        }

        void StepTarget(PackView v, int step)
        {
            var n = Party.Heroes.Count;
            v.Target = ((v.Target + step) % n + n) % n;
        }

        void MoveCursor(PlayerId player, int slot, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.Cursor == slot) return;
            v.Cursor = slot;
            events.Add(new InventoryCursorMoved(tick, player, slot));
        }

        void StartChoosing(PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            v.Choosing = true;
            events.Add(new TargetChanged(tick, player, true, v.Target));
        }

        // ---- swap, drop, use ------------------------------------------------------------------------------------

        /// <summary>actionRun with the panel open: mark the cursor's stack, or swap the marked one with it (SwapItem).</summary>
        IGameEvent[] Swap(PlayerId player, long tick)
        {
            var v = For(player);
            if (Inventory.Items.Count == 0) return Array.Empty<IGameEvent>();
            if (!v.Swapping)
            {
                v.Swapping = true;
                v.SwapFrom = v.Cursor;
                return new IGameEvent[] { new InventorySwapped(tick, player, v.Cursor, v.Cursor, false) };
            }

            v.Swapping = false;
            Inventory.Swap(v.Cursor, v.SwapFrom);
            return new IGameEvent[] { new InventorySwapped(tick, player, v.SwapFrom, v.Cursor, true) };
        }

        /// <summary>
        /// actionDrop and checkDrop: the choosing of a target stops; in battle mode the panel closes and the ring menu
        /// opens reset (the battle's cancel); in adventure mode with the panel open one of the cursor's stack falls by the leader, or
        /// with an empty pack the panel closes.
        /// </summary>
        IGameEvent[] Drop(PlayerId player, long tick)
        {
            var v = For(player);
            var events = new List<IGameEvent>();
            if (v.Choosing)
            {
                v.Choosing = false;
                events.Add(new TargetChanged(tick, player, false, v.Target));
            }

            var leader = Party.Leader(player);
            if (leader.Mode == HeroMode.Battle)
            {
                if (v.Open) Close(player, tick, events);
                Party.CancelToRing(player, tick, events);
                return events.ToArray();
            }

            if (!v.Open) return events.ToArray();
            // With the book shown the drop key hides it and drops nothing.
            if (BookShown(player))
            {
                Books.SetShown(player, false, tick, events);
                return events.ToArray();
            }

            if (Inventory.Items.Count == 0)
            {
                Close(player, tick, events);
                return events.ToArray();
            }

            var stack = Inventory.Items[v.Cursor];
            var drop = Pickups.Drop(stack, leader);
            events.Add(new ItemDropped(tick, player, leader.Index, stack.Name, drop.Index, drop.X, drop.Y));
            TakeOne(v.Cursor);
            return events.ToArray();
        }

        void Close(PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (!v.Open) return;
            v.Open = false;
            events.Add(new InventoryToggled(tick, player, false));
        }

        /// <summary>Opens or closes <paramref name="player"/>'s panel for a script (CHANGE INVENTORY OPEN / CLOSE).</summary>
        internal void SetOpen(PlayerId player, bool open, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.Open == open) return;
            v.Open = open;
            events.Add(new InventoryToggled(tick, player, open));
        }

        /// <summary>
        /// One of the stack at <paramref name="slot"/> leaves the pack (RemoveFromInventory closes the list up): every
        /// player's cursor past the end comes back to the last stack, and a swap mark is cleared (the original kept it,
        /// so the next swap moved a stack past the end of the pack, where it was lost; BIBLE).
        /// </summary>
        void TakeOne(int slot)
        {
            if (!Inventory.TakeOne(slot)) return;
            var count = Inventory.Items.Count;
            foreach (var v in views.Values)
            {
                v.Swapping = false;
                if (v.Cursor > count - 1) v.Cursor = Math.Max(0, count - 1);
            }
        }

        /// <summary>
        /// actionPickup's use (bUseDown): see the class summary. <paramref name="click"/> is a press of the held button,
        /// which first moves the cursor to an icon the virtual mouse is over.
        /// </summary>
        void Use(Game game, PlayerId player, bool click, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (v.Open)
            {
                if (click && View != null)
                {
                    var cursor = View.For(player).Cursor;
                    var over = Panel.SlotUnder(cursor.MouseX, cursor.MouseY, Inventory.Items.Count);
                    if (over >= 0 && over != v.Cursor)
                    {
                        MoveCursor(player, over, tick, events);
                        return;
                    }
                }

                if (v.Swapping)
                {
                    foreach (var e in Swap(player, tick)) events.Add(e);
                    return;
                }

                if (Inventory.Items.Count == 0) return;
                if (!Party.Team)
                {
                    v.Target = Party.LeaderOf(player);
                    ApplyUse(game, player, tick, events);
                    return;
                }

                var type = Inventory.Items[v.Cursor].Type;
                if (type == ItemType.GroupItem || type == ItemType.Reusable)
                {
                    ApplyUse(game, player, tick, events);
                    return;
                }

                Close(player, tick, events);
                StartChoosing(player, tick, events);
                return;
            }

            if (!v.Choosing) return;
            v.Choosing = false;
            events.Add(new TargetChanged(tick, player, false, v.Target));
            ApplyUse(game, player, tick, events);
        }

        /// <summary>
        /// checkUseItem: the cursor's item's script runs on the target; unless the item is Reusable one of it leaves the
        /// pack. A script that fails (the Rusty Sword's WEAPON, until the weapon story) is reported by the runner and the item stays.
        /// In battle mode the leader pays 10 percent of its gauge, the items' target goes back to the leader, the panel closes
        /// and the ring menu comes back.
        /// </summary>
        void ApplyUse(Game game, PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            var v = For(player);
            if (Inventory.Items.Count == 0) return;
            var slot = v.Cursor;
            var stack = Inventory.Items[slot];
            // The runner reports its own failure (ScriptFailed and the message); without one the script cannot run at all.
            if (Scripts == null)
            {
                events.Add(new ScriptFailed(tick, player, stack.Script));
                return;
            }

            if (!Scripts.Run(game, player, v.Target, stack.Script, tick, events)) return;

            // The script may have changed the pack (the stack is found again by itself, not by its slot).
            var consumed = stack.Type != ItemType.Reusable;
            if (consumed)
            {
                var at = IndexOf(stack);
                if (at >= 0) TakeOne(at);
            }

            events.Add(new ItemUsed(tick, player, v.Target, stack.Name, consumed));

            if (Party.Leader(player).Mode != HeroMode.Battle) return;
            Party.PayForItem(player, tick, events);
            v.Target = Party.LeaderOf(player);
            Close(player, tick, events);
        }

        int IndexOf(InventoryItem stack)
        {
            for (var i = 0; i < Inventory.Items.Count; i++)
                if (ReferenceEquals(Inventory.Items[i], stack)) return i;
            return -1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Overworld.Core
{
    /// <summary>One pickup in the world (the original's pickup(n)): where it lies, and whether it has been picked up.</summary>
    public sealed class Pickup
    {
        internal Pickup(PickupDefinition definition, DataPoint position, string file)
        {
            Definition = definition;
            X = position.X;
            Y = position.Y;
            File = file;
        }

        public PickupDefinition Definition { get; }
        public int Index => Definition.Index;
        public string Name => Definition.Name;

        /// <summary>The quad's left edge and vertical centre, in world units.</summary>
        public float X { get; }

        public float Y { get; }

        /// <summary>The picture it is drawn with (and shows in the examine box).</summary>
        public string File { get; }

        /// <summary>Picked up: not drawn and not in reach until it grows back (if it ever does).</summary>
        public bool PickedUp { get; internal set; }

        /// <summary>When it was picked up or last grew back, in game seconds (lastGrowCheck).</summary>
        internal double LastGrowTime { get; set; }

        public override string ToString() => Name + " " + Index + " at (" + X.ToString(CultureInfo.InvariantCulture) + ", " + Y.ToString(CultureInfo.InvariantCulture) + ")" + (PickedUp ? " picked up" : "");
    }

    /// <summary>
    /// An item dropped from the pack into the world (the original's dropped(n), Inventory.checkDrop): a copy of the
    /// stack's item lying where it fell, drawn with its inventory picture, until it is picked up again. It never grows back.
    /// </summary>
    public sealed class DroppedItem
    {
        internal DroppedItem(int index) { Index = index; PickedUp = true; }

        /// <summary>Its slot among the drops (a slot whose item was picked up again is used for the next drop).</summary>
        public int Index { get; }

        /// <summary>The pickup the dropped stack was first made from (its name, script and texts).</summary>
        public PickupDefinition Source { get; internal set; }

        public string Name => Source?.Name;

        /// <summary>The picture it lies as and goes back into the pack with: the stack's inventory picture.</summary>
        public string File { get; internal set; }

        public float X { get; internal set; }
        public float Y { get; internal set; }

        /// <summary>Picked up again (or never dropped): not drawn and not in reach.</summary>
        public bool PickedUp { get; internal set; }

        public override string ToString() => Name + " dropped " + Index + " at (" + X.ToString(CultureInfo.InvariantCulture) + ", " + Y.ToString(CultureInfo.InvariantCulture) + ")" + (PickedUp ? " picked up" : "");
    }

    /// <summary>A hero picked a dropped item up again; it went back into the pack (<see cref="Stack"/>).</summary>
    public sealed class DroppedItemCollected : IGameEvent
    {
        public DroppedItemCollected(long tick, PlayerId player, int hero, int drop, string name, InventoryItem stack)
        {
            Tick = tick;
            Player = player;
            Hero = hero;
            Drop = drop;
            Name = name;
            Stack = stack;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public int Hero { get; }

        /// <summary>The drop's slot.</summary>
        public int Drop { get; }

        public string Name { get; }
        public InventoryItem Stack { get; }
    }

    /// <summary>The , and . keys (Input.actionItemLast / actionItemNext), or a turn of the wheel: the previous (-1) or next (+1) item in reach.</summary>
    public sealed class CyclePickup : ICommand
    {
        public CyclePickup(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }

        /// <summary>-1 for the previous item (,), +1 for the next (.).</summary>
        public int Step { get; }
    }

    /// <summary>
    /// One press of the pick-up button, for scripts and scripted runs: the next tick does what a held button does for that
    /// tick (the leader kneels and picks up the selected item in reach). Players hold the button through <see cref="MoveInput.PickUp"/>.
    /// </summary>
    public sealed class PickUp : ICommand
    {
        public PickUp(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>A hero picked up a pickup: it was used at once (gold) or went into the pack (<see cref="Stack"/>).</summary>
    public sealed class PickupCollected : IGameEvent
    {
        public PickupCollected(long tick, PlayerId player, int hero, int pickup, string name, bool used, InventoryItem stack)
        {
            Tick = tick;
            Player = player;
            Hero = hero;
            Pickup = pickup;
            Name = name;
            Used = used;
            Stack = stack;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public int Hero { get; }

        /// <summary>The pickup's index.</summary>
        public int Pickup { get; }

        public string Name { get; }

        /// <summary>Its script ran on pickup (a UseOnPickup item); it went into no stack.</summary>
        public bool Used { get; }

        /// <summary>The stack it joined, or null when it was used.</summary>
        public InventoryItem Stack { get; }
    }

    /// <summary>A picked-up pickup grew back where it was.</summary>
    public sealed class PickupGrew : IGameEvent
    {
        public PickupGrew(long tick, int pickup) { Tick = tick; Pickup = pickup; }
        public long Tick { get; }
        public int Pickup { get; }
    }

    /// <summary>A player chose another of the items in reach.</summary>
    public sealed class PickupSelected : IGameEvent
    {
        public PickupSelected(long tick, PlayerId player, int pickup) { Tick = tick; Player = player; Pickup = pickup; }
        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The pickup now selected, or -1 when none is in reach.</summary>
        public int Pickup { get; }
    }

    /// <summary>The party's gold changed.</summary>
    public sealed class GoldChanged : IGameEvent
    {
        public GoldChanged(long tick, PlayerId player, int gold, int previous) { Tick = tick; Player = player; Gold = gold; Previous = previous; }
        public long Tick { get; }
        public PlayerId Player { get; }
        public int Gold { get; }
        public int Previous { get; }
    }

    /// <summary>
    /// The world's pickups (U.OVW-US-A12; Items.vb and Cursor.checkCursorOverItem, RECONSTRUCTION 8.1 and 8.2): the
    /// original's 372 and the restored Nitro Bean at their places (the gold and satchels scattered by the seeded random),
    /// what is in reach of each player's leader, which of those is selected, picking up, and growing back.
    /// Each tick, before the party (as checkItemsInRange and checkItemPickup ran after getInput and before updateHero):
    /// a picked-up pickup whose growth time (game seconds, as the original's AppTime) has passed since it was picked
    /// grows back (renderItem's checkGrowth); for each player the held pick-up button kneels the leader with
    /// <see cref="Hero.PickingUp"/> (once a press, not while lying down; letting go clears it, as KeysUp did); the
    /// unpicked pickups in reach of the leader are listed (at most the reach's 'listed', in index order); and a leader
    /// picking up takes the selected one: a UseOnPickup item's script runs at once (the gold), anything else goes into
    /// the party's <see cref="Inventory"/>. Accepts <see cref="CyclePickup"/> and <see cref="PickUp"/>; the wheel
    /// cycles the items in reach before it zooms (<see cref="UseWheel"/>, given to the <see cref="CameraControl"/>).
    /// Presentation reads <see cref="All"/>, <see cref="Selected"/> and <see cref="PickupAt"/>.
    /// </summary>
    public sealed class Pickups : ITickSystem
    {
        sealed class Reach
        {
            public readonly List<int> InReach = new List<int>();
            public int Slot;
            public readonly List<int> DropsInReach = new List<int>();
            public int DropSlot;
            public bool Latched;
            public bool Pressed;
        }

        readonly Pickup[] pickups;
        readonly List<DroppedItem> drops = new List<DroppedItem>();
        readonly Dictionary<PlayerId, Reach> reaches = new Dictionary<PlayerId, Reach>();
        readonly Random random;

        /// <param name="data">The pickups and how reach, hover and labels work.</param>
        /// <param name="party">Whose leaders pick up.</param>
        /// <param name="inventory">The party's pack and purse (null: a new, empty one).</param>
        /// <param name="random">Scatters the random pickups (x then y, in index order); null for an unseeded one, as the original's was.</param>
        public Pickups(PickupData data, Party party, Inventory inventory = null, Random random = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Party = party ?? throw new ArgumentNullException(nameof(party));
            if (data.Reach == null || data.Hover == null || data.Label == null) throw new ArgumentException("The pickup data has no reach, hover or label.", nameof(data));
            Inventory = inventory ?? new Inventory();
            var rng = random ?? new Random();
            this.random = rng;
            var positions = data.ResolvePositions(rng.NextDouble);
            pickups = new Pickup[data.Pickups.Count];
            for (var i = 0; i < pickups.Length; i++) pickups[i] = new Pickup(data.Pickups[i], positions[i], data.WorldFile(data.Pickups[i]));
        }

        public PickupData Data { get; }
        public Party Party { get; }
        public Inventory Inventory { get; }

        /// <summary>Runs a UseOnPickup item's script (the gold's CHANGE HERO GOLD): the <see cref="ScriptInterpreter"/>. Null: none runs.</summary>
        public IScriptRunner Scripts { get; set; }

        /// <summary>Every pickup, by index.</summary>
        public IReadOnlyList<Pickup> All => pickups;

        /// <summary>Every drop slot used so far, by slot (picked-up ones are free for the next drop).</summary>
        public IReadOnlyList<DroppedItem> Drops => drops;

        /// <summary>
        /// Inventory.checkDrop: a copy of <paramref name="stack"/>'s item falls at <paramref name="hero"/>'s position plus
        /// the drop spread times a random fraction (x then y), into the first free drop slot (the original's next slot, which
        /// ran out after 372 drops; BIBLE). The pack is not changed here.
        /// </summary>
        internal DroppedItem Drop(InventoryItem stack, Hero hero)
        {
            DroppedItem drop = null;
            foreach (var d in drops)
                if (d.PickedUp)
                {
                    drop = d;
                    break;
                }

            if (drop == null)
            {
                drop = new DroppedItem(drops.Count);
                drops.Add(drop);
            }

            var spread = Data.DropSpread;
            drop.Source = stack.Source;
            drop.File = stack.Texture;
            drop.X = hero.X + spread * (float)random.NextDouble();
            drop.Y = hero.Y + spread * (float)random.NextDouble();
            drop.PickedUp = false;
            return drop;
        }

        /// <summary>The drops in reach of the hero <paramref name="player"/> leads, as last listed, in slot order.</summary>
        public IReadOnlyList<int> DropsInReach(PlayerId player) => For(player).DropsInReach;

        /// <summary>The selected drop in reach of <paramref name="player"/>'s leader (its name shows over it), or -1.</summary>
        public int SelectedDrop(PlayerId player)
        {
            var r = For(player);
            if (r.DropsInReach.Count == 0 || r.DropSlot >= r.DropsInReach.Count) return -1;
            var index = r.DropsInReach[r.DropSlot];
            return drops[index].PickedUp ? -1 : index;
        }

        public Pickups Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<CyclePickup>(HandleCycle);
            game.Register<PickUp>(HandlePickUp);
            game.AddSystem(this);
            return this;
        }

        Reach For(PlayerId player)
        {
            if (!reaches.TryGetValue(player, out var r)) reaches[player] = r = new Reach();
            return r;
        }

        /// <summary>The pickups in reach of the hero <paramref name="player"/> leads, as last listed, in index order.</summary>
        public IReadOnlyList<int> InReach(PlayerId player) => For(player).InReach;

        /// <summary>The selected pickup in reach of <paramref name="player"/>'s leader (the one its name shows over), or -1.</summary>
        public int Selected(PlayerId player)
        {
            var r = For(player);
            if (r.InReach.Count == 0 || r.Slot >= r.InReach.Count) return -1;
            var index = r.InReach[r.Slot];
            return pickups[index].PickedUp ? -1 : index;
        }

        /// <summary>
        /// checkCursorOverItem: the first unpicked pickup (in index order) under the world cursor at
        /// (<paramref name="cursorX"/>, <paramref name="cursorY"/>), or -1. Its name shows, and in examine mode the examine box.
        /// </summary>
        public int PickupAt(float cursorX, float cursorY)
        {
            var h = Data.Hover;
            var cx = cursorX + h.CursorOffset.X;
            var cy = cursorY + h.CursorOffset.Y;
            foreach (var p in pickups)
            {
                if (p.PickedUp) continue;
                if (WorldCamera.Near(p.X + h.ItemOffset.X, cx, h.Range) && WorldCamera.Near(p.Y + h.ItemOffset.Y, cy, h.Range)) return p.Index;
            }

            return -1;
        }

        /// <summary>
        /// getMouse's wheel: with items in reach of <paramref name="player"/>'s leader a turn toward the screen selects the
        /// next and away the previous, one a step, and the camera does not zoom (true); with none it zooms (false).
        /// </summary>
        public bool UseWheel(PlayerId player, int steps)
        {
            var r = For(player);
            if ((r.InReach.Count == 0 && r.DropsInReach.Count == 0) || steps == 0) return false;
            for (var i = 0; i < Math.Abs(steps); i++) Cycle(r, steps > 0 ? 1 : -1);
            return true;
        }

        CommandResult HandleCycle(Game game, CyclePickup c)
        {
            if (c.Step != 1 && c.Step != -1) return CommandResult.Reject("Step to the previous (-1) or next (+1) item in reach.");
            Cycle(For(c.Player), c.Step);
            return CommandResult.Accept(new PickupSelected(game.Tick, c.Player, Selected(c.Player)));
        }

        /// <summary>
        /// actionItemLast / actionItemNext: wrapping through the items in reach, and through the drops in reach beside
        /// them (one step moves both selections, as the original's did); with none, the first slot.
        /// </summary>
        static void Cycle(Reach r, int step)
        {
            r.Slot = Cycle(r.Slot, r.InReach.Count - 1, step);
            r.DropSlot = Cycle(r.DropSlot, r.DropsInReach.Count - 1, step);
        }

        static int Cycle(int slot, int last, int step)
        {
            if (last < 0) return 0;
            if (step < 0) return slot > 0 ? slot - 1 : last;
            return slot < last ? slot + 1 : 0;
        }

        CommandResult HandlePickUp(Game game, PickUp c)
        {
            For(c.Player).Pressed = true;
            return CommandResult.Accept();
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            var now = tick * Clock.TickSeconds;

            // checkGrowth (renderItem, after the last frame's update): a picked pickup with a growth time grows back.
            foreach (var p in pickups)
            {
                var grow = p.Definition.GrowSeconds;
                if (grow <= 0f || !p.PickedUp) continue;
                // The tick clock is exact; the epsilon keeps 720 ticks from falling a hair short of 12 s.
                if (now + 1e-6 < p.LastGrowTime + grow) continue;
                p.PickedUp = false;
                p.LastGrowTime = now;
                events.Add(new PickupGrew(tick, p.Index));
            }

            foreach (var player in game.Players)
            {
                var r = For(player);
                var hero = Party.Leader(player);
                var held = Party.InputOf(player).PickUp || r.Pressed;
                r.Pressed = false;
                if (held)
                {
                    // actionPickup kneels only with the pack shut, no item's target being chosen (Enter uses there) and
                    // the battle's ring menu shut (Enter chooses from it there).
                    if ((Focus == null || Focus(player) == InputFocus.World) && !Party.RingMenuOpen(player)) Press(hero, r);
                }
                else
                {
                    // KeysUp: the button let go.
                    r.Latched = false;
                    hero.PickingUp = false;
                }

                List(hero, r);
                Collect(game, player, hero, r, now, tick, events);
                ListDrops(hero, r);
                CollectDrop(player, hero, r, tick, events);
            }
        }

        /// <summary>What a player's keys work on now (the pack open, a target being chosen, or the world); null: always the world.</summary>
        public Func<PlayerId, InputFocus> Focus { get; set; }

        /// <summary>checkDroppedInRange: the drops in reach of the leader, as <see cref="List"/> finds the pickups.</summary>
        void ListDrops(Hero hero, Reach r)
        {
            var reach = Data.Reach;
            var size = Data.ItemSize;
            var heroSize = Party.Data.HeroSize;
            var hx = hero.X + heroSize * reach.HeroPoint.X;
            var hy = hero.Y + heroSize * reach.HeroPoint.Y;
            r.DropsInReach.Clear();
            foreach (var d in drops)
            {
                if (d.PickedUp) continue;
                if (!WorldCamera.Near(d.X + size * reach.ItemPoint.X, hx, reach.Range) || !WorldCamera.Near(d.Y + size * reach.ItemPoint.Y, hy, reach.Range)) continue;
                r.DropsInReach.Add(d.Index);
                if (r.DropsInReach.Count >= reach.Listed) break;
            }

            if (r.DropsInReach.Count <= 1 || r.DropSlot >= r.DropsInReach.Count) r.DropSlot = 0;
        }

        /// <summary>
        /// checkDroppedPickup: a leader still picking up (no pickup was taken this tick) takes the selected drop back into
        /// the pack by name; a drop never grows back.
        /// </summary>
        void CollectDrop(PlayerId player, Hero hero, Reach r, long tick, ICollection<IGameEvent> events)
        {
            if (!hero.PickingUp) return;
            var index = SelectedDrop(player);
            if (index < 0) return;
            hero.PickingUp = false;
            PayForPickUp(hero);
            var d = drops[index];
            d.PickedUp = true;
            var stack = Inventory.Add(d.Source, d.File);
            events.Add(new DroppedItemCollected(tick, player, hero.Index, index, d.Name, stack));
        }

        /// <summary>
        /// checkItemPickup and checkDroppedPickup: a hero Moving in battle loses 5 percent of its maximum stamina (VB's
        /// Integer assignment, rounded half to even), not below 0.
        /// </summary>
        void PayForPickUp(Hero hero)
        {
            if (hero.BattleAction != BattleAction.Moving) return;
            var cost = hero.MaxStamina * Math.Round((double)Data.BattlePickupStamina, 6);
            hero.Stamina = Math.Max(0, (int)Math.Round(hero.Stamina - cost, MidpointRounding.ToEven));
        }

        /// <summary>actionPickup: the leader stops running; unless it lies down, a new press kneels it to pick up.</summary>
        static void Press(Hero hero, Reach r)
        {
            hero.Running = false;
            if (hero.Laying || r.Latched) return;
            r.Latched = true;
            hero.PickingUp = true;
            hero.State = HeroState.Kneeling;
        }

        /// <summary>
        /// checkItemsInRange: the unpicked pickups whose point is within reach of the leader's, in index order, up to the
        /// listed most. A selection left beyond the list goes back to the first (the original kept a stale slot, which
        /// named and picked up an item out of reach; BIBLE).
        /// </summary>
        void List(Hero hero, Reach r)
        {
            var reach = Data.Reach;
            var size = Data.ItemSize;
            var heroSize = Party.Data.HeroSize;
            var hx = hero.X + heroSize * reach.HeroPoint.X;
            var hy = hero.Y + heroSize * reach.HeroPoint.Y;
            r.InReach.Clear();
            foreach (var p in pickups)
            {
                if (p.PickedUp) continue;
                if (!WorldCamera.Near(p.X + size * reach.ItemPoint.X, hx, reach.Range) || !WorldCamera.Near(p.Y + size * reach.ItemPoint.Y, hy, reach.Range)) continue;
                r.InReach.Add(p.Index);
                if (r.InReach.Count >= reach.Listed) break;
            }

            if (r.InReach.Count <= 1 || r.Slot >= r.InReach.Count) r.Slot = 0;
        }

        /// <summary>
        /// checkItemPickup: a leader picking up with an item in reach stops picking up and takes the selected one: it is
        /// picked up (its growth timer starts), and a UseOnPickup item's script runs at once while anything else joins the
        /// pack. A hero Moving in battle pays 5 percent of its maximum stamina for it (<see cref="PayForPickUp"/>).
        /// </summary>
        void Collect(Game game, PlayerId player, Hero hero, Reach r, double now, long tick, ICollection<IGameEvent> events)
        {
            if (!hero.PickingUp) return;
            var index = Selected(player);
            if (index < 0) return;
            hero.PickingUp = false;
            PayForPickUp(hero);
            var p = pickups[index];
            p.PickedUp = true;
            p.LastGrowTime = now;
            if (p.Definition.Type == ItemType.UseOnPickup)
            {
                // The runner reports its own failure; without one the script cannot run at all.
                if (Scripts == null) events.Add(new ScriptFailed(tick, player, p.Definition.Script));
                else Scripts.Run(game, player, hero.Index, p.Definition.Script, tick, events);
                events.Add(new PickupCollected(tick, player, hero.Index, index, p.Name, true, null));
                return;
            }

            var stack = Inventory.Add(p.Definition, Data.InventoryFile(p.Definition));
            events.Add(new PickupCollected(tick, player, hero.Index, index, p.Name, false, stack));
        }
    }
}

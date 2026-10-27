using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The battle ring menu, the move budget, the range circles and the passing of the turn (U.OVW-US-A19; RingMenu.vb,
    /// Circle.vb, Input.actionRingSelect, NextHeroInBattle, actionHeroSelect, Heroes.checkHeroMove and checkBattleSelect;
    /// RECONSTRUCTION 12.2 and 12.3).
    /// <para>
    /// The ring opens around the leader on entering battle: Move, End, Skill, Item and Attack, the entry at the top
    /// selected. Held left and right arrows (and the wheel) turn it an entry at a time with an eased spin, Up pulses it out
    /// and back, and Enter or a click (<see cref="ChooseFromRing"/>) chooses: Move (with stamina left) shuts the ring and
    /// lets the leader walk with the move button as far as its move distance allows (agility x 0.05 at a full gauge), the
    /// gauge falling with it; Item (with a pack not empty) opens the pack, and a use there costs 10 percent of the gauge and
    /// brings the ring back; End passes the turn. Highlighting Move shows the move circle (dots at the move distance),
    /// Attack the weapon's range circle, Item the quick bar, End kneels the leader. When the move distance runs out, or End
    /// is chosen, the leader kneels recharging and the next hero in party order that is in battle and Ready leads, the camera
    /// following; with none ready the ring shuts until the first battle hero's gauge is full.
    /// </para>
    /// </summary>
    public sealed partial class Party
    {
        readonly Dictionary<PlayerId, RingMenu> rings = new Dictionary<PlayerId, RingMenu>();

        /// <summary>A player's ring menu and range circles (made shut the first time it is asked for).</summary>
        public RingMenu RingOf(PlayerId player)
        {
            if (!rings.TryGetValue(player, out var ring)) rings[player] = ring = new RingMenu(Data.Battle);
            return ring;
        }

        /// <summary>
        /// True while a player's battle ring menu is open (bRingMenu). While it is open the arrows only mark the direction
        /// held (they turn the ring), the move button does not head for the cursor and the pick-up button does not kneel.
        /// </summary>
        public bool RingMenuOpen(PlayerId player) => rings.TryGetValue(player, out var ring) && ring.Open;

        /// <summary>Opens or shuts a player's ring menu as it is (no reset), for tests and scripted runs.</summary>
        internal void SetRingMenu(PlayerId player, bool open) => RingOf(player).Open = open;

        /// <summary>
        /// The button that chose from a player's ring is still held (bItemPickup and bUseDown stayed set): until it is let go
        /// it neither kneels to pick up nor uses from the pack the choice opened.
        /// </summary>
        public bool RingChoiceHeld(PlayerId player) => rings.TryGetValue(player, out var ring) && ring.ChoiceHeld;

        /// <summary>Shows or hides a player's quick bar (the Item entry's highlight; the <see cref="InventoryControl"/> sets it).</summary>
        internal Action<PlayerId, bool, long, ICollection<IGameEvent>> QuickBarShown { get; set; }

        /// <summary>Opens a player's pack for the Item entry; false when the pack is empty (the <see cref="InventoryControl"/> sets it).</summary>
        internal Func<PlayerId, long, ICollection<IGameEvent>, bool> OpenPack { get; set; }

        // ---- input (getKeyboard's arrows, getMouse's wheel, KeysUp) -------------------------------------------------

        /// <summary>
        /// actionLeft / actionRight / actionUp with the ring open: a held arrow turns it (right wins when both are held), Up
        /// pulses it unless the choosing button is held (bRingSelect). The choosing button let go frees its latch.
        /// </summary>
        void RingInput(PlayerId player, MoveInput input)
        {
            var ring = RingOf(player);
            if (!input.PickUp) ring.ChoiceHeld = false;
            if (!ring.Open) return;
            if (input.Left) RequestTurn(ring, RingMode.Left);
            if (input.Right) RequestTurn(ring, RingMode.Right);
            if (input.Up && !input.PickUp) RequestPulse(ring);
        }

        /// <summary>
        /// A turn waits for a pulse to finish and a pulse for a turn (the original let either cut the other off, leaving the
        /// ring stuck wide or its entries off their places until the next turn or pulse; BIBLE).
        /// </summary>
        static void RequestTurn(RingMenu ring, RingMode mode)
        {
            if (ring.Mode == RingMode.Out || ring.Mode == RingMode.In) return;
            ring.Mode = mode;
        }

        static void RequestPulse(RingMenu ring)
        {
            if (ring.Mode == RingMode.Left || ring.Mode == RingMode.Right) return;
            ring.Mode = RingMode.Out;
        }

        /// <summary>
        /// getMouse's wheel with the ring open (before anything else takes it): toward you turns it left, away right. True
        /// when it was used.
        /// </summary>
        public bool UseRingWheel(PlayerId player, int steps)
        {
            if (steps == 0 || !RingMenuOpen(player)) return false;
            RequestTurn(RingOf(player), steps < 0 ? RingMode.Left : RingMode.Right);
            return true;
        }

        CommandResult HandleTurnRing(Game game, TurnRing command)
        {
            if (command.Step != 1 && command.Step != -1) return CommandResult.Reject("Turn the ring left (-1) or right (+1).");
            if (!RingMenuOpen(command.Player)) return CommandResult.Reject("The ring menu is shut.");
            RequestTurn(RingOf(command.Player), command.Step < 0 ? RingMode.Left : RingMode.Right);
            return CommandResult.Accept();
        }

        // ---- choosing (actionRingSelect) ----------------------------------------------------------------------------

        CommandResult HandleChooseFromRing(Game game, ChooseFromRing command)
        {
            var ring = RingOf(command.Player);
            if (!ring.Open) return CommandResult.Reject("The ring menu is shut.");
            ring.ChoiceHeld = true;
            var hero = Leader(command.Player);
            var entry = ring.SelectedEntry;
            var events = new List<IGameEvent>();
            var taken = false;
            switch (entry)
            {
                case RingEntry.Move:
                    if (hero.Stamina > 0)
                    {
                        var before = hero.BattleAction;
                        hero.BattleAction = BattleAction.Moving;
                        // The distance is what the gauge allows now (the original kept the one worked out when the hero
                        // was last Ready, and checkHeroMove's mirror then filled a part-spent gauge back up; BIBLE).
                        SyncMoveDistance(hero);
                        ring.Open = false;
                        taken = true;
                        if (before != hero.BattleAction) events.Add(new HeroBattleActionChanged(game.Tick, hero.Index, hero.BattleAction));
                    }

                    break;
                case RingEntry.Item:
                    if (OpenPack != null && OpenPack(command.Player, game.Tick, events))
                    {
                        ring.Open = false;
                        taken = true;
                    }

                    break;
                case RingEntry.End:
                    taken = true;
                    PassTurn(game, command.Player, game.Tick, events, false);
                    break;
            }

            events.Insert(0, new RingEntryChosen(game.Tick, command.Player, hero.Index, entry, taken));
            return CommandResult.Accept(events.ToArray());
        }

        // ---- the ring's effects (resetRing, checkRingEffect, determineBattleStance) -----------------------------------

        /// <summary>resetRing: every entry back in its place with Move at the top, at rest, then its effect.</summary>
        void ResetRing(PlayerId player, RingMenu ring, long tick, ICollection<IGameEvent> events)
        {
            ring.ResetAngles();
            // The original left a turn or pulse under way, which then finished from the reset places (BIBLE).
            ring.Mode = RingMode.Select;
            ring.Radius = ring.Ring.Radius;
            ApplyRingEffect(player, ring, tick, events);
        }

        /// <summary>
        /// checkRingEffect, with the ring open: Move shows the move circle, Attack the attack circle, Item the quick bar
        /// (each hiding the others), Skill none; each stands the leader up into its stance. End hides them all and kneels
        /// the leader.
        /// </summary>
        void ApplyRingEffect(PlayerId player, RingMenu ring, long tick, ICollection<IGameEvent> events)
        {
            if (!ring.Open) return;
            var hero = Leader(player);
            var entry = ring.SelectedEntry;
            ring.ShowMove = entry == RingEntry.Move;
            ring.ShowAttack = entry == RingEntry.Attack;
            QuickBarShown?.Invoke(player, entry == RingEntry.Item, tick, events);
            if (entry == RingEntry.End)
            {
                hero.Kneeling = true;
                hero.State = HeroState.Kneeling;
                return;
            }

            hero.Kneeling = false;
            DetermineStance(hero);
        }

        /// <summary>determineBattleStance: kneeling, standing or drawing, the hero draws (from the first frame unless already drawing); otherwise it holds its stance.</summary>
        static void DetermineStance(Hero hero)
        {
            if (hero.State != HeroState.Kneeling && hero.State != HeroState.Standing && hero.State != HeroState.Drawing)
            {
                hero.State = HeroState.Battle;
                return;
            }

            if (hero.State != HeroState.Drawing) hero.Frame = 0;
            hero.State = HeroState.Drawing;
        }

        // ---- the turn (checkHeroMove, NextHeroInBattle, checkBattleSelect, actionHeroSelect) --------------------------

        /// <summary>
        /// checkHeroMove for a hero Moving in battle: what it covers this tick (|dX| or |dY| above 0.001, both with dY halved,
        /// and both with dY halved when neither is) comes off its move distance and the gauge is set to the share left (VB's
        /// Integer assignment, half to even); once the distance is spent the hero stops, stands and recharges. True then.
        /// </summary>
        internal static bool SpendMove(Hero hero)
        {
            if (hero.BattleAction != BattleAction.Moving) return false;
            var ax = Math.Abs(hero.DX);
            var ay = Math.Abs(hero.DY);
            var moved = ax + ay / 2f;
            if (ax > 0.001) moved = ax;
            if (ay > 0.001) moved = ay;
            if (ax > 0.001 && ay > 0.001) moved = ax + ay / 2f;

            if (hero.MoveDistance > 0f)
            {
                hero.MoveDistance -= moved;
                var share = hero.MoveDistanceMax > 0f ? hero.MoveDistance / hero.MoveDistanceMax * hero.MaxStamina : 0f;
                hero.Stamina = Math.Max(0, (int)Math.Round((double)share, MidpointRounding.ToEven));
            }

            if (hero.MoveDistance > 0f) return false;
            hero.DX = 0f;
            hero.DY = 0f;
            hero.State = HeroState.Standing;
            hero.BattleAction = BattleAction.Recharging;
            return true;
        }

        /// <summary>
        /// NextHeroInBattle: the leader kneels and recharges, and the next hero after it in party order (wrapping) that is in
        /// battle mode, Ready and led by no other player leads: it gets up, draws unless it holds its stance, the ring is
        /// reset and the camera centres on it. With none, the turn comes back to the same hero, which gets up and draws
        /// while it recharges; when the move distance ran out (<paramref name="spent"/>) the ring then shuts and the first
        /// battle hero whose gauge fills will lead (bNoOneReady), and after End it stays open.
        /// </summary>
        void PassTurn(Game game, PlayerId player, long tick, ICollection<IGameEvent> events, bool spent)
        {
            var ring = RingOf(player);
            var from = LeaderOf(player);
            var old = heroes[from];
            // resetRing came first, while the old leader still led: its effect is overwritten by the kneel.
            ResetRing(player, ring, tick, events);
            old.Kneeling = true;
            old.State = HeroState.Kneeling;
            old.BattleAction = BattleAction.Recharging;

            var next = from;
            for (var k = 1; k < heroes.Count; k++)
            {
                var i = (from + k) % heroes.Count;
                var h = heroes[i];
                if (h.Mode != HeroMode.Battle || h.BattleAction != BattleAction.Ready || LedByAnother(game, i, player)) continue;
                next = i;
                break;
            }

            leaders[player] = next;
            var hero = heroes[next];
            hero.Kneeling = false;
            if (hero.State != HeroState.Battle)
            {
                hero.Frame = 0;
                hero.State = HeroState.Drawing;
            }

            if (spent)
            {
                ring.Open = next != from;
                ring.NoOneReady = next == from;
            }

            events.Add(new TurnPassed(tick, player, from, next == from ? -1 : next));
            // CenterOnHero.
            events.Add(new LeaderChanged(tick, player, next, from, false));
        }

        /// <summary>
        /// actionHeroSelect on a hero in battle mode: only one whose gauge is full. The leader is set recharging, the ring
        /// reset and opened, and the chosen hero leads: it gets up and, unless it holds its stance, draws and is Ready; the
        /// camera centres on it.
        /// </summary>
        CommandResult SelectInBattle(Game game, PlayerId player, int index)
        {
            var chosen = heroes[index];
            if (chosen.Stamina < chosen.MaxStamina)
                return CommandResult.Reject(chosen.Name + " is recharging; a hero in battle is chosen once its gauge is full.");
            var events = new List<IGameEvent>();
            var previous = LeaderOf(player);
            var old = heroes[previous];
            old.BattleAction = BattleAction.Recharging;
            var ring = RingOf(player);
            // resetRing came before the ring opened and the new hero led: its effect fell on the old one, if any.
            ResetRing(player, ring, game.Tick, events);
            ring.Open = true;
            leaders[player] = index;
            chosen.Laying = false;
            chosen.Kneeling = false;
            if (chosen.State != HeroState.Battle)
            {
                chosen.State = HeroState.Drawing;
                chosen.BattleAction = BattleAction.Ready;
            }

            events.Add(new LeaderChanged(game.Tick, player, index, previous, false));
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>
        /// After the heroes have moved: checkBattleSelect for every player no one of whose battle heroes was ready, then each
        /// open ring's turn and pulse (checkRingRotate) and each shown range circle's orbit and colours (renderCircle).
        /// </summary>
        void StepBattleTurns(Game game, long tick, ICollection<IGameEvent> events)
        {
            foreach (var player in game.Players)
            {
                if (!rings.TryGetValue(player, out var ring)) continue;
                SelectReady(game, player, ring, tick, events);
                StepRing(player, ring, tick, events);
                StepCircle(player, ring);
            }
        }

        /// <summary>The paused frame's part of the ring (renderRingIcon, renderCircle); checkBattleSelect was in updateHero and waits.</summary>
        void StepRingsPaused(Game game, long tick, ICollection<IGameEvent> events)
        {
            foreach (var player in game.Players)
            {
                if (!rings.TryGetValue(player, out var ring)) continue;
                StepRing(player, ring, tick, events);
                StepCircle(player, ring);
            }
        }

        /// <summary>
        /// checkBattleSelect: while no one was ready, the first hero in party order whose gauge is full leads: it gets up,
        /// draws from the first frame, the ring opens reset and the camera centres on it. Only a hero in battle mode and led
        /// by no other player (the original took any full hero, in adventure mode too; BIBLE).
        /// </summary>
        void SelectReady(Game game, PlayerId player, RingMenu ring, long tick, ICollection<IGameEvent> events)
        {
            if (!ring.NoOneReady) return;
            for (var i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                if (h.Mode != HeroMode.Battle || h.Stamina != h.MaxStamina || LedByAnother(game, i, player)) continue;
                ring.NoOneReady = false;
                var from = LeaderOf(player);
                leaders[player] = i;
                h.Kneeling = false;
                h.Frame = 0;
                h.State = HeroState.Drawing;
                ring.Open = true;
                ResetRing(player, ring, tick, events);
                events.Add(new TurnPassed(tick, player, from, i));
                events.Add(new LeaderChanged(tick, player, i, from, false));
                return;
            }
        }

        /// <summary>
        /// checkRingRotate, with the ring open: turning, every entry's angle eases toward its last place plus (right) or
        /// minus (left) one step by the spin's base plus its ease times what is left, and once past it snaps there; when the
        /// last entry snaps the selection moves (right: back one, left: on one, wrapping) and its effect applies. Pulsing,
        /// the radius grows by the pulse step to the pulse radius, then shrinks back to its rest radius.
        /// </summary>
        void StepRing(PlayerId player, RingMenu ring, long tick, ICollection<IGameEvent> events)
        {
            if (!ring.Open) return;
            var t = ring.Ring;
            var last = ring.Count - 1;
            var spinBase = Fraction(t.SpinBase);
            var spinEase = Fraction(t.SpinEase);
            if (ring.Mode == RingMode.Right || ring.Mode == RingMode.Left)
            {
                var right = ring.Mode == RingMode.Right;
                for (var r = 0; r <= last; r++)
                {
                    var target = right ? ring.lastAngles[r] + t.SpinStep : ring.lastAngles[r] - t.SpinStep;
                    var a = ring.angles[r];
                    if (right ? a < target : a > target)
                    {
                        var step = spinBase + spinEase * (right ? target - a : a - target);
                        ring.angles[r] = (float)(right ? a + step : a - step);
                        continue;
                    }

                    ring.angles[r] = target;
                    ring.lastAngles[r] = target;
                    if (r != last) continue;
                    if (right) ring.Selected = ring.Selected > 0 ? ring.Selected - 1 : last;
                    else ring.Selected = ring.Selected < last ? ring.Selected + 1 : 0;
                    ring.Mode = RingMode.Select;
                    ApplyRingEffect(player, ring, tick, events);
                    events.Add(new RingTurned(tick, player, ring.SelectedEntry));
                }
            }

            if (ring.Mode == RingMode.Out)
            {
                if (ring.Radius < t.PulseRadius) ring.Radius = (float)(ring.Radius + Fraction(t.PulseStep));
                else ring.Mode = RingMode.In;
            }

            if (ring.Mode == RingMode.In)
            {
                if (ring.Radius > t.Radius * 2f) ring.Radius = (float)(ring.Radius - Fraction(t.PulseStep));
                else
                {
                    ring.Radius = t.Radius;
                    ring.Mode = RingMode.Select;
                }
            }
        }

        /// <summary>
        /// renderCircle and checkDotColor, while a circle is shown: every dot orbits (up while the leader faces left or
        /// down, down otherwise); the radius is the leader's move distance (Move) or its weapon's range (Attack); a dot near
        /// a hero's point is red at the leader and blue at an ally, else green, and every Attack dot is red.
        /// </summary>
        void StepCircle(PlayerId player, RingMenu ring)
        {
            if (!ring.CircleShown) return;
            var hero = Leader(player);
            var c = ring.Circle;
            var orbit = Fraction(c.Orbit);
            var up = hero.Facing == Facing.Left || hero.Facing == Facing.Down;
            for (var i = 0; i < ring.dotAngles.Length; i++)
                ring.dotAngles[i] = (float)(up ? ring.dotAngles[i] + orbit : ring.dotAngles[i] - orbit);

            ring.CircleRadius = ring.ShowMove ? hero.MoveDistance : hero.Weapon != null ? hero.Weapon.Range : 0f;
            var size = Data.HeroSize;
            for (var i = 0; i < ring.dotAngles.Length; i++)
            {
                ring.DotOffset(i, size, out var ox, out var oy);
                var x = hero.X + ox;
                var y = hero.Y + oy;
                var colour = DotColour.Green;
                foreach (var h in heroes)
                {
                    var hx = h.X + size * c.NearPoint.X + c.NearOffset.X;
                    var hy = h.Y + size * c.NearPoint.Y + c.NearOffset.Y;
                    if (WorldCamera.Near(x, hx, c.NearRange) && WorldCamera.Near(y, hy, c.NearRange))
                        colour = h == hero ? DotColour.Red : DotColour.Blue;
                }

                if (ring.ShowAttack) colour = DotColour.Red;
                ring.dotColours[i] = colour;
            }
        }

        // ---- the pack in battle (checkUseItem, actionDrop) -----------------------------------------------------------

        /// <summary>
        /// checkUseItem in battle mode: the leader pays CInt(maximum x 0.1) of its gauge (not below 0) and the ring comes
        /// back as it was (not reset). The move distance follows the gauge (BIBLE).
        /// </summary>
        internal void PayForItem(PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            var hero = Leader(player);
            if (hero.Mode != HeroMode.Battle) return;
            var cost = (int)Math.Round(hero.MaxStamina * Fraction(Data.Battle.ItemStamina), MidpointRounding.ToEven);
            hero.Stamina = Math.Max(0, hero.Stamina - cost);
            SyncMoveDistance(hero);
            events.Add(new HeroVitalsChanged(tick, hero.Index, hero.Health, hero.Mana, hero.Stamina));
            RingOf(player).Open = true;
        }

        /// <summary>actionDrop in battle mode (Backspace, or the middle button): the battle's cancel; the ring opens reset.</summary>
        internal void CancelToRing(PlayerId player, long tick, ICollection<IGameEvent> events)
        {
            if (Leader(player).Mode != HeroMode.Battle) return;
            var ring = RingOf(player);
            ring.Open = true;
            ResetRing(player, ring, tick, events);
        }
    }
}

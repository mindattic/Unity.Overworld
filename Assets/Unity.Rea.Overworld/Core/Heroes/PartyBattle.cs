using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The End key (Input.actionBattleToggle, RECONSTRUCTION 12.1) for the hero <see cref="Player"/> leads. Out of battle:
    /// the ring menu opens and the leader enters battle mode, drawing its weapon if its turn gauge (stamina) is full and
    /// recharging otherwise; with the team on every other hero enters with it, each drawing or recharging by its own gauge.
    /// In battle: the ring menu closes and the leader (with the team on, every hero) returns to adventure mode, standing
    /// and ready.
    /// </summary>
    public sealed class ToggleBattle : ICommand
    {
        public ToggleBattle(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>
    /// The U key (frmMain's KeyDown, RECONSTRUCTION 5.2): only the mode of the hero <see cref="Player"/> leads flips
    /// between adventure and battle, with none of the End key's drawing, recharging, ring menu or team.
    /// </summary>
    public sealed class ToggleHeroMode : ICommand
    {
        public ToggleHeroMode(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>A player's leader entered or left battle mode with the End key (and, with the team on, the others with it).</summary>
    public sealed class BattleToggled : IGameEvent
    {
        public BattleToggled(long tick, PlayerId player, int leader, bool battle, IReadOnlyList<int> heroes)
        {
            Tick = tick;
            Player = player;
            Leader = leader;
            Battle = battle;
            Heroes = heroes;
        }

        public long Tick { get; }
        public PlayerId Player { get; }
        public int Leader { get; }

        /// <summary>True on entering battle mode, false on leaving it.</summary>
        public bool Battle { get; }

        /// <summary>Every hero whose mode changed with the leader's (the leader first).</summary>
        public IReadOnlyList<int> Heroes { get; }
    }

    /// <summary>A hero's mode was flipped on its own (the U key).</summary>
    public sealed class HeroModeChanged : IGameEvent
    {
        public HeroModeChanged(long tick, int hero, HeroMode mode) { Tick = tick; Hero = hero; Mode = mode; }
        public long Tick { get; }
        public int Hero { get; }
        public HeroMode Mode { get; }
    }

    /// <summary>A hero's battle action changed (it began recharging, became Ready, ...).</summary>
    public sealed class HeroBattleActionChanged : IGameEvent
    {
        public HeroBattleActionChanged(long tick, int hero, BattleAction action) { Tick = tick; Hero = hero; Action = action; }
        public long Tick { get; }
        public int Hero { get; }
        public BattleAction Action { get; }
    }

    /// <summary>
    /// Battle mode's stances and turn gauge (U.OVW-US-A18; RECONSTRUCTION 7, 12.1, 12.2 and 12.4): the End key, the U key,
    /// the refill of a recharging hero's stamina every 0.18 s, and the draw and battle-stance animations with the weapon
    /// drawn over them in step.
    /// </summary>
    public sealed partial class Party
    {
        static float MoveDistanceFor(Hero hero) => hero.MaxStamina > 0 ? hero.MoveDistanceMax * ((float)hero.Stamina / hero.MaxStamina) : 0f;

        /// <summary>The move distance set to what the hero's gauge allows now (agility x 0.05 x stamina / maximum).</summary>
        internal static void SyncMoveDistance(Hero hero) => hero.MoveDistance = MoveDistanceFor(hero);

        CommandResult HandleBattle(Game game, ToggleBattle command)
        {
            var leader = Leader(command.Player);
            var changed = new List<int> { leader.Index };
            var entering = leader.Mode == HeroMode.Adventure;
            var ring = RingOf(command.Player);
            // bNoOneReady is the battle's; leaving forgets it (the original kept it, and its first full hero then drew and
            // opened the ring in adventure mode; BIBLE).
            ring.NoOneReady = false;
            if (entering)
            {
                ring.Open = true;
                leader.Mode = HeroMode.Battle;
                EnterStance(leader);
            }
            else
            {
                ring.Open = false;
                ring.ShowMove = false;
                ring.ShowAttack = false;
                LeaveBattle(leader);
            }

            if (Team)
            {
                foreach (var hero in heroes)
                {
                    // The original changed every other hero; another player's leader is that player's to change.
                    if (hero == leader || LedByAnother(game, hero.Index, command.Player)) continue;
                    if (entering)
                    {
                        hero.Mode = HeroMode.Battle;
                        EnterStance(hero);
                    }
                    else
                    {
                        LeaveBattle(hero);
                    }

                    changed.Add(hero.Index);
                }
            }

            var events = new List<IGameEvent> { new BattleToggled(game.Tick, command.Player, leader.Index, entering, changed) };
            // Entering resets the ring (its Move entry at the top, the move circle shown); leaving hides the quick bar and
            // the range circles.
            if (entering) ResetRing(command.Player, ring, game.Tick, events);
            else BattleLeft?.Invoke(command.Player, game.Tick, events);
            return CommandResult.Accept(events.ToArray());
        }

        /// <summary>Called when a player leaves battle mode (the <see cref="InventoryControl"/> hides that player's quick bar, as actionBattleToggle did).</summary>
        internal Action<PlayerId, long, ICollection<IGameEvent>> BattleLeft { get; set; }

        /// <summary>
        /// actionBattleToggle on entering, for each hero: full, it draws its weapon from the first frame; otherwise it
        /// recharges (and kneels at the next refill check, or at once when nothing is held). Each hero's action is its own:
        /// the original copied the leader's action onto the others first, so a full hero entering behind a recharging
        /// leader drew while Recharging and began drawing again at the next check (BIBLE).
        /// </summary>
        static void EnterStance(Hero hero)
        {
            if (hero.Stamina == hero.MaxStamina)
            {
                hero.BattleAction = BattleAction.Ready;
                hero.Frame = 0;
                hero.State = HeroState.Drawing;
            }
            else
            {
                hero.BattleAction = BattleAction.Recharging;
            }
        }

        /// <summary>
        /// actionBattleToggle on leaving: adventure mode, standing, ready. The kneel of the recharge is lifted too (the
        /// original left Kneeling set, so the leader could not head for the cursor until an arrow stood it up; the others,
        /// it set drawing or recharging again in adventure mode; BIBLE).
        /// </summary>
        static void LeaveBattle(Hero hero)
        {
            hero.Mode = HeroMode.Adventure;
            hero.State = HeroState.Standing;
            hero.BattleAction = BattleAction.Ready;
            hero.Kneeling = false;
        }

        CommandResult HandleHeroMode(Game game, ToggleHeroMode command)
        {
            var hero = Leader(command.Player);
            hero.Mode = hero.Mode == HeroMode.Adventure ? HeroMode.Battle : HeroMode.Adventure;
            return CommandResult.Accept(new HeroModeChanged(game.Tick, hero.Index, hero.Mode));
        }

        /// <summary>
        /// checkRegen in battle mode: once more than the refill interval (0.18 s) has passed since the hero's last check, a
        /// hero Recharging below its maximum gains a stamina point and kneels; one already full is Ready, gets up, has its
        /// move distance recomputed and, unless it already holds its battle stance, draws its weapon from the first frame.
        /// </summary>
        void Refill(Hero hero, double now)
        {
            // The tick clock is exact; the epsilon keeps a whole number of ticks from falling a hair short.
            if (!(now + 1e-6 > hero.LastBattleTime + Data.Battle.RefillInterval)) return;
            if (hero.BattleAction == BattleAction.Recharging)
            {
                if (hero.Stamina < hero.MaxStamina)
                {
                    hero.Stamina = Math.Min(hero.MaxStamina, hero.Stamina + Data.Battle.RefillStamina);
                    hero.Kneeling = true;
                    hero.State = HeroState.Kneeling;
                }
                else
                {
                    hero.Stamina = hero.MaxStamina;
                    hero.MoveDistance = MoveDistanceFor(hero);
                    hero.BattleAction = BattleAction.Ready;
                    hero.Kneeling = false;
                    if (hero.State != HeroState.Battle)
                    {
                        hero.Frame = 0;
                        hero.State = HeroState.Drawing;
                    }
                }
            }

            hero.LastBattleTime = now;
        }

        /// <summary>
        /// aiDrawing: still, the draw cycle of its side at the draw rate (0.05 s); on its last frame the hero takes its battle
        /// stance at a random frame of it.
        /// </summary>
        void DrawWeapon(Hero hero, double now)
        {
            hero.DX = 0f;
            hero.DY = 0f;
            var d = hero.Definition;
            Animate(hero, now, d.DrawRate, Sprites.Draw(hero.Facing), d.DrawFrames);
            if (hero.Frame < d.DrawFrames) return;
            hero.Frame = random.Next(0, d.BattleFrames + 1);
            hero.State = HeroState.Battle;
        }

        /// <summary>aiBattle: still, the battle stance's cycle of its side at the battle rate (0.1 s).</summary>
        void HoldStance(Hero hero, double now)
        {
            hero.DX = 0f;
            hero.DY = 0f;
            Animate(hero, now, hero.Definition.BattleRate, Sprites.Battle(hero.Facing), hero.Definition.BattleFrames);
        }
    }
}

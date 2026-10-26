using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// Confirms a sleep (Enter or the left button while laid; Input.actionPickup's "Initiate Sleep", RECONSTRUCTION 6.4):
    /// the hero <see cref="Player"/> leads must be lying down. With <see cref="Hours"/> the sleep's hours are set first, as
    /// the arrows would set them (for scripted runs).
    /// </summary>
    public sealed class Sleep : ICommand
    {
        public Sleep(PlayerId player, int? hours = null) { Player = player; Hours = hours; }
        public PlayerId Player { get; }

        /// <summary>The hours to sleep (the fewest to the most), or null for the hours the leader has chosen.</summary>
        public int? Hours { get; }
    }

    /// <summary>
    /// The sleeping draughts' path (the Moon Berry and Morningstar scripts, <c>CHANGE HERO AI LAYDOWN | CHANGE TIME HOUR
    /// h</c>; Script.vb, RECONSTRUCTION 6.4 and 8): <see cref="Hero"/> lies down (its AI Kneeling, laid, as LAYDOWN did) and
    /// the party sleeps as from the rest key, except that the clock wakes it at <see cref="Hour"/> (the minute kept, as
    /// setHour kept it; the next such hour) rather than moving on the chosen hours. Null <see cref="Hour"/> is LAYDOWN
    /// alone: a sleep of the chosen hours. The script story (A14) runs the item scripts onto this command (BIBLE).
    /// </summary>
    public sealed class SleepUntil : ICommand
    {
        public SleepUntil(PlayerId player, int hero, int? hour)
        {
            Player = player;
            Hero = hero;
            Hour = hour;
        }

        public PlayerId Player { get; }

        /// <summary>The hero the draught lays down (the hero the item is used on).</summary>
        public int Hero { get; }

        /// <summary>The hour (0 to 23) the party wakes at, or null for the chosen hours.</summary>
        public int? Hour { get; }
    }

    /// <summary>A sleep began: the screen fades to black.</summary>
    public sealed class SleepStarted : IGameEvent
    {
        public SleepStarted(long tick, PlayerId player, int hero, int? wakeHour)
        {
            Tick = tick;
            Player = player;
            Hero = hero;
            WakeHour = wakeHour;
        }

        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The hero that lay down to sleep.</summary>
        public int Hero { get; }

        /// <summary>The hour the party will wake at, or null when it sleeps the chosen hours.</summary>
        public int? WakeHour { get; }
    }

    /// <summary>The screen went black and the party slept: its vitals were restored and the clock moved on.</summary>
    public sealed class PartySlept : IGameEvent
    {
        public PartySlept(long tick, PlayerId player, int hours, DateTime time)
        {
            Tick = tick;
            Player = player;
            Hours = hours;
            Time = time;
        }

        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The hours the sleep restored.</summary>
        public int Hours { get; }

        /// <summary>The time the party woke.</summary>
        public DateTime Time { get; }
    }

    /// <summary>
    /// Sleep (Sun.checkSleep and the device ambient, RECONSTRUCTION 6.4): once a sleep is confirmed, every ambient channel
    /// falls by the fade step a tick to black; on the tick it is black every hero kneels and lies down and gains its
    /// maximum health and mana times the hours times one-eighth (stamina too, unless Poisoned), the clock moves on the hours
    /// (or to the draught's hour) with the period and light settled, the sleeper's hours return to the fewest and the
    /// sleeper stands; then, and whenever no sleep is under way, the ambient rises by the fade step a tick back to white.
    /// The ambient multiplies the world's light (the original's device ambient over the material colour); the HUD is not lit.
    /// Installed after the party and before the clock, as checkSleep ran in renderTime after updateHero and before the minute.
    /// </summary>
    public sealed class SleepCycle : ITickSystem
    {
        static readonly LightColour White = new LightColour(255, 255, 255);

        public SleepCycle(Party party, WorldClock clock)
        {
            Party = party ?? throw new ArgumentNullException(nameof(party));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Ambient = White;
        }

        public Party Party { get; }
        public WorldClock Clock { get; }
        RestTuning Rest => Party.Data.Rest;

        /// <summary>The device ambient (AmbientColor): white, falling to black while a sleep begins and rising after it.</summary>
        public LightColour Ambient { get; private set; }

        /// <summary>A sleep is under way (bSleeping): the screen is fading to black.</summary>
        public bool Sleeping { get; private set; }

        /// <summary>The player whose sleep it is: whose leader's hours it restores and who stands when it ends.</summary>
        public PlayerId Sleeper { get; private set; }

        /// <summary>The hour a draught's sleep wakes at, or null for the chosen hours.</summary>
        public int? WakeHour { get; private set; }

        /// <summary>The colour the world is drawn in: the clock's light times the ambient.</summary>
        public LightColour Lit(LightColour light) => new LightColour(
            (byte)(light.R * Ambient.R / 255), (byte)(light.G * Ambient.G / 255), (byte)(light.B * Ambient.B / 255));

        public SleepCycle Install(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.Register<Sleep>(HandleSleep);
            game.Register<SleepUntil>(HandleSleepUntil);
            game.AddSystem(this);
            return this;
        }

        CommandResult HandleSleep(Game game, Sleep c)
        {
            var hero = Party.Leader(c.Player);
            if (!hero.Laying) return CommandResult.Reject(hero.Name + " is not lying down; the rest key lays a kneeling hero down.");
            if (c.Hours.HasValue)
            {
                if (c.Hours < Rest.MinHours || c.Hours > Rest.MaxHours)
                    return CommandResult.Reject("A sleep is " + Rest.MinHours + " to " + Rest.MaxHours + " hours, not " + c.Hours + ".");
                hero.SleepHours = c.Hours.Value;
            }

            return CommandResult.Accept(Begin(game, c.Player, hero, null));
        }

        CommandResult HandleSleepUntil(Game game, SleepUntil c)
        {
            if (c.Hero < 0 || c.Hero >= Party.Heroes.Count) return CommandResult.Reject("There is no hero " + c.Hero + " in the party.");
            if (c.Hour.HasValue && (c.Hour < 0 || c.Hour > 23)) return CommandResult.Reject("The hour must be 0 to 23, not " + c.Hour + ".");
            return CommandResult.Accept(LayDown(c.Player, c.Hero, c.Hour, game.Tick));
        }

        /// <summary>
        /// CHANGE HERO AI LAYDOWN (with the draught's hour, or null): hero <paramref name="index"/> lies down (its AI
        /// Kneeling) and the party's sleep begins. The hero and hour must be valid.
        /// </summary>
        internal SleepStarted LayDown(PlayerId player, int index, int? hour, long tick)
        {
            var hero = Party.Heroes[index];
            hero.Laying = true;
            hero.State = HeroState.Kneeling;
            return Begin(tick, player, hero, hour);
        }

        SleepStarted Begin(Game game, PlayerId player, Hero hero, int? wakeHour) => Begin(game.Tick, player, hero, wakeHour);

        SleepStarted Begin(long tick, PlayerId player, Hero hero, int? wakeHour)
        {
            Sleeping = true;
            Sleeper = player;
            WakeHour = wakeHour;
            return new SleepStarted(tick, player, hero.Index, wakeHour);
        }

        public void Step(Game game, long tick, ICollection<IGameEvent> events)
        {
            var step = Rest.FadeStep;
            if (!Sleeping)
            {
                Ambient = new LightColour(Rise(Ambient.R, step), Rise(Ambient.G, step), Rise(Ambient.B, step));
                return;
            }

            Ambient = new LightColour(Fall(Ambient.R, step), Fall(Ambient.G, step), Fall(Ambient.B, step));
            if (Ambient.R == 0 && Ambient.G == 0 && Ambient.B == 0) Wake(tick, events);
        }

        /// <summary>The black screen: every hero lies down and is restored, the clock moves on, the sleeper stands.</summary>
        void Wake(long tick, ICollection<IGameEvent> events)
        {
            var sleeper = Party.Leader(Sleeper);
            var hours = sleeper.SleepHours;
            // VB multiplied the Single hours by the Double 0.125: the share as the decimal the data wrote.
            var share = hours * Math.Round((double)Rest.RestorePerHour, 6);
            foreach (var hero in Party.Heroes)
            {
                var before = (hero.Health, hero.Mana, hero.Stamina);
                hero.State = HeroState.Kneeling;
                hero.Kneeling = true;
                hero.Laying = true;
                hero.Health = Restore(hero.Health, hero.MaxHealth, share);
                hero.Mana = Restore(hero.Mana, hero.MaxMana, share);
                if (hero.Status != HeroStatus.Poisoned) hero.Stamina = Restore(hero.Stamina, hero.MaxStamina, share);
                if ((hero.Health, hero.Mana, hero.Stamina) != before)
                    events.Add(new HeroVitalsChanged(tick, hero.Index, hero.Health, hero.Mana, hero.Stamina));
            }

            if (WakeHour.HasValue) Clock.SetHour(Sleeper, WakeHour.Value, null, true, tick, events);
            else Clock.Advance(Sleeper, TimeSpan.FromHours(hours), tick, events);

            sleeper.SleepHours = Rest.MinHours;
            sleeper.Kneeling = false;
            sleeper.Laying = false;
            Sleeping = false;
            WakeHour = null;
            events.Add(new PartySlept(tick, Sleeper, hours, Clock.Now));
        }

        /// <summary>value + max x share, assigned to an Integer as VB did (rounded half to even), capped at the maximum.</summary>
        static int Restore(int value, int max, double share)
        {
            var restored = (int)Math.Round(value + max * share, MidpointRounding.ToEven);
            return restored > max ? max : restored;
        }

        static byte Fall(byte value, int step) => (byte)(value > step ? value - step : 0);
        static byte Rise(byte value, int step) => (byte)(value < 255 - step ? value + step : 255);
    }
}

using System;
using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>A rule that runs once every simulation tick, and may emit events.</summary>
    public interface ITickSystem
    {
        void Step(Game game, long tick, ICollection<IGameEvent> events);
    }

    /// <summary>
    /// The authority over the game. State changes only through <see cref="Submit"/>, which checks who is asking,
    /// runs the command handler and returns the events it produced; time moves only through
    /// <see cref="Advance"/> / <see cref="Step"/> at the fixed 60 Hz tick. Subscribers read the events.
    /// </summary>
    public sealed class Game
    {
        readonly HashSet<PlayerId> players = new HashSet<PlayerId>();
        readonly Dictionary<Type, Func<Game, ICommand, CommandResult>> handlers = new Dictionary<Type, Func<Game, ICommand, CommandResult>>();
        readonly List<ITickSystem> systems = new List<ITickSystem>();
        readonly FixedStepper stepper = new FixedStepper();

        public Game()
        {
            Register<JoinGame>(HandleJoin);
            Register<LoadWorld>(HandleLoadWorld);
        }

        public long Tick { get; private set; }
        public WorldDefinition World { get; private set; }
        public float Alpha => stepper.Alpha;
        public IReadOnlyCollection<PlayerId> Players => players;

        /// <summary>Raised for every event, in order, after the state change that caused it.</summary>
        public event Action<IGameEvent> EventRaised;

        public bool HasPlayer(PlayerId player) => players.Contains(player);

        public void Register<TCommand>(Func<Game, TCommand, CommandResult> handler) where TCommand : ICommand
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            handlers[typeof(TCommand)] = (g, c) => handler(g, (TCommand)c);
        }

        public void AddSystem(ITickSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            systems.Add(system);
        }

        public CommandResult Submit(ICommand command)
        {
            if (command == null) return CommandResult.Reject("No command.");
            if (command.Player.IsNone) return CommandResult.Reject("A command must name a player.");
            if (!(command is JoinGame) && !players.Contains(command.Player))
                return CommandResult.Reject(command.Player + " is not in the game.");
            if (!handlers.TryGetValue(command.GetType(), out var handler))
                return CommandResult.Reject("No handler for " + command.GetType().Name + ".");

            var result = handler(this, command);
            if (result.Accepted)
                foreach (var e in result.Events) Raise(e);
            return result;
        }

        /// <summary>
        /// While this returns true the game is held (paused). The ticks still run, as oneFrameRender still ran while
        /// bPause was on, but each system skips what the original skipped: everything inside its 'If Not bPause' block
        /// (getInput, the clouds and rain, the pickups' reach and taking, flocking, the quests, checkCursorOverInventory and
        /// updateHero), while what it did in the draw (the clock and light, the sleep's fade, the messages, regrowth, the
        /// ring's turning, the range circles, the status animation, the cursor's scrolling and the camera) goes on.
        /// Commands are still accepted. Null: never held.
        /// </summary>
        public Func<bool> Held { get; set; }

        /// <summary>True while <see cref="Held"/> says the game is paused; systems read it in their step.</summary>
        public bool IsHeld => Held != null && Held();

        /// <summary>Feeds real elapsed time; runs however many whole ticks it adds up to. Returns the ticks run.</summary>
        public int Advance(double elapsedSeconds)
        {
            int ticks = stepper.Advance(elapsedSeconds);
            for (int i = 0; i < ticks; i++) Step();
            return ticks;
        }

        /// <summary>Runs exactly one tick.</summary>
        public void Step()
        {
            Tick++;
            var events = new List<IGameEvent>();
            for (int i = 0; i < systems.Count; i++) systems[i].Step(this, Tick, events);
            for (int i = 0; i < events.Count; i++) Raise(events[i]);
        }

        void Raise(IGameEvent e) => EventRaised?.Invoke(e);

        static CommandResult HandleJoin(Game g, JoinGame c)
        {
            if (!g.players.Add(c.Player)) return CommandResult.Reject(c.Player + " is already in the game.");
            return CommandResult.Accept(new PlayerJoined(g.Tick, c.Player));
        }

        static CommandResult HandleLoadWorld(Game g, LoadWorld c)
        {
            if (c.World == null) return CommandResult.Reject("No world given.");
            g.World = c.World;
            return CommandResult.Accept(new WorldLoaded(g.Tick, c.Player, c.World.Name, c.World.Areas.Count));
        }
    }
}

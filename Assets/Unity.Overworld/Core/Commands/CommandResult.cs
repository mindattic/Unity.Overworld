using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>The outcome of one submitted command: rejected with a reason, or accepted with its events.</summary>
    public sealed class CommandResult
    {
        static readonly IGameEvent[] NoEvents = new IGameEvent[0];

        public bool Accepted { get; }
        public string Reason { get; }
        public IReadOnlyList<IGameEvent> Events { get; }

        CommandResult(bool accepted, string reason, IReadOnlyList<IGameEvent> events)
        {
            Accepted = accepted;
            Reason = reason;
            Events = events;
        }

        public static CommandResult Accept(params IGameEvent[] events) => new CommandResult(true, null, events ?? NoEvents);
        public static CommandResult Reject(string reason) => new CommandResult(false, reason, NoEvents);
    }
}

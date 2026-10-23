namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// What happened because of an accepted command or a tick. Presentation reads these and plays them;
    /// it never mutates the game.
    /// </summary>
    public interface IGameEvent
    {
        /// <summary>The simulation tick the event happened on.</summary>
        long Tick { get; }
    }
}

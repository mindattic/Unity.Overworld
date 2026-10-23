namespace MindAttic.Overworld.Core
{
    /// <summary>A request to change the game. The only way state changes; it names who asked.</summary>
    public interface ICommand
    {
        PlayerId Player { get; }
    }
}

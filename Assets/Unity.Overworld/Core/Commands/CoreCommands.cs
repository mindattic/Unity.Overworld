namespace MindAttic.Overworld.Core
{
    /// <summary>Adds a player to the game. The one command accepted from a player that is not yet in it.</summary>
    public sealed class JoinGame : ICommand
    {
        public JoinGame(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>Replaces the game's world with the given definition.</summary>
    public sealed class LoadWorld : ICommand
    {
        public LoadWorld(PlayerId player, WorldDefinition world) { Player = player; World = world; }
        public PlayerId Player { get; }
        public WorldDefinition World { get; }
    }
}

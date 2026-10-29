namespace MindAttic.Overworld.Core
{
    public sealed class PlayerJoined : IGameEvent
    {
        public PlayerJoined(long tick, PlayerId player) { Tick = tick; Player = player; }
        public long Tick { get; }
        public PlayerId Player { get; }
    }

    public sealed class PlayerLeft : IGameEvent
    {
        public PlayerLeft(long tick, PlayerId player) { Tick = tick; Player = player; }
        public long Tick { get; }
        public PlayerId Player { get; }
    }

    public sealed class WorldLoaded : IGameEvent
    {
        public WorldLoaded(long tick, PlayerId by, string worldName, int areaCount) { Tick = tick; By = by; WorldName = worldName; AreaCount = areaCount; }
        public long Tick { get; }
        public PlayerId By { get; }
        public string WorldName { get; }
        public int AreaCount { get; }
    }
}

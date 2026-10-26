namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The team / solo toggle (the ' key or the team button; Input.actionTeamToggle, RECONSTRUCTION 4.5 and 13): with the
    /// team on, the heroes no player leads follow; with it off they kneel where they are.
    /// </summary>
    public sealed class ToggleTeam : ICommand
    {
        public ToggleTeam(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
    }

    /// <summary>
    /// Makes <see cref="Hero"/> the hero <see cref="Player"/> leads, as a left click on it did (Input.actionHeroSelect,
    /// RECONSTRUCTION 4.7): the new leader stands up; with the team off the old one kneels.
    /// </summary>
    public sealed class SelectHero : ICommand
    {
        public SelectHero(PlayerId player, int hero) { Player = player; Hero = hero; }
        public PlayerId Player { get; }

        /// <summary>The hero's place in the party.</summary>
        public int Hero { get; }
    }

    /// <summary>
    /// The [ and ] keys (Input.lastHero / nextHero and changeHero, RECONSTRUCTION 4.7): the previous (-1) or next (+1)
    /// hero in party order, wrapping, becomes the leader; both stand, the camera centres and the cursor snaps to it.
    /// </summary>
    public sealed class CycleLeader : ICommand
    {
        public CycleLeader(PlayerId player, int step) { Player = player; Step = step; }
        public PlayerId Player { get; }

        /// <summary>-1 for the previous hero ([), +1 for the next (]).</summary>
        public int Step { get; }
    }

    /// <summary>Following was turned on (team) or off (solo).</summary>
    public sealed class TeamChanged : IGameEvent
    {
        public TeamChanged(long tick, PlayerId player, bool team) { Tick = tick; Player = player; Team = team; }
        public long Tick { get; }

        /// <summary>Who toggled it.</summary>
        public PlayerId Player { get; }

        public bool Team { get; }
    }

    /// <summary>A player now leads another hero.</summary>
    public sealed class LeaderChanged : IGameEvent
    {
        public LeaderChanged(long tick, PlayerId player, int hero, int previous, bool cycled)
        {
            Tick = tick;
            Player = player;
            Hero = hero;
            Previous = previous;
            Cycled = cycled;
        }

        public long Tick { get; }
        public PlayerId Player { get; }

        /// <summary>The hero the player leads now.</summary>
        public int Hero { get; }

        /// <summary>The hero the player led before.</summary>
        public int Previous { get; }

        /// <summary>Chosen with [ or ] (the cursor snaps to the new leader), not by a click.</summary>
        public bool Cycled { get; }
    }
}

using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// Drives Core from Unity: feeds real frame time to the game, which runs its own fixed 60 Hz ticks.
    /// Presentation reads <see cref="Game"/> and its events; nothing here changes game state directly.
    /// Created in code (<see cref="Create"/>), never wired by hand in the Inspector.
    /// </summary>
    public sealed class GameHost : MonoBehaviour
    {
        public Game Game { get; private set; }

        public static GameHost Create(Game game)
        {
            var go = new GameObject("GameHost");
            var host = go.AddComponent<GameHost>();
            host.Game = game;
            return host;
        }

        void Update()
        {
            if (Game != null) Game.Advance(Time.unscaledDeltaTime);
        }
    }
}

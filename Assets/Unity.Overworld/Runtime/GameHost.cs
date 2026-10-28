using MindAttic.Overworld.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// Drives Core from Unity: feeds real frame time to the game, which runs its own fixed 60 Hz ticks.
    /// Presentation reads <see cref="Game"/> and its events; nothing here changes game state directly.
    /// Created in code (<see cref="Create"/>), never wired by hand in the Inspector.
    /// <para>Online (U.OVW-T46) a client's game forwards its player commands to the one authority
    /// (<see cref="Core.Game.IsRemote"/>) and is not stepped here: the authority's game is the game. In local play,
    /// and on the host, it steps exactly as before.</para>
    /// </summary>
    public sealed class GameHost : MonoBehaviour
    {
        public Game Game { get; private set; }

        /// <summary>
        /// While true the game is not stepped (U.OVW-T46: the online menu open over local play holds it, as a menu over a
        /// running game does). False in play, so it steps exactly as before.
        /// </summary>
        public bool Suspended { get; set; }

        public static GameHost Create(Game game)
        {
            var go = new GameObject("GameHost");
            var host = go.AddComponent<GameHost>();
            host.Game = game;
            return host;
        }

        void Update()
        {
            if (Game != null && !Game.IsRemote && !Suspended) Game.Advance(Time.unscaledDeltaTime);
        }
    }
}

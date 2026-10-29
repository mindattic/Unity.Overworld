using Unity.Shared.Core;
using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// Honours Unity.Shared's CLI session mute for the whole game: launched with <c>-muted</c> or
    /// <c>MINDATTIC_CLI_MUTED=1</c>, Unity.Shared's bootstrap turns on <see cref="Profiles.SessionMuted"/>
    /// before the first scene loads, and this silences the engine-wide <see cref="AudioListener"/> for that
    /// run. Overworld has no volume settings of its own yet, so this reads only the session-mute flag, not a
    /// profile's settings; nothing here writes settings.json.
    /// </summary>
    public static class SessionAudio
    {
        /// <summary>True once <see cref="Apply"/> found the CLI session mute on.</summary>
        public static bool Muted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnLoad() => Apply();

        /// <summary>
        /// Zeroes the listener if <see cref="Profiles.SessionMuted"/> is on. Never unmutes what something
        /// else muted. Returns <see cref="Muted"/>.
        /// </summary>
        public static bool Apply()
        {
            Muted = Profiles.SessionMuted;
            if (Muted)
            {
                AudioListener.volume = 0f;
                Debug.Log("Unity.Overworld: audio muted for this session (Unity.Shared CLI mute; -muted / MINDATTIC_CLI_MUTED=1).");
            }
            return Muted;
        }
    }
}

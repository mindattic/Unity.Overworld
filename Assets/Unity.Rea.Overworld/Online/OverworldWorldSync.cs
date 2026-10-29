using System;
using MindAttic.Overworld.Core;
using Unity.Multiplayer;      // NetEntity, NetSession
using Unity.Multiplayer.Core; // [ToClients]
using UnityEngine;

namespace MindAttic.Overworld.Online
{
    /// <summary>
    /// The one authority's game, sent to every peer (U.OVW-T46). The server spawns one per session, keyed
    /// <see cref="Key"/> (<c>session.Objects.Spawn</c>, found on any peer with <see cref="For"/>), and
    /// <see cref="SendRate"/> times a second captures its game as a <see cref="WorldSnapshot"/> (the tick, the clock, the
    /// weather, the pause, every hero and every player's leader) and sends it to the clients; each peer keeps the newest
    /// as <see cref="Latest"/> for presentation to read. Nothing on a client changes a game with it: the authority is the
    /// only thing that changes the game.
    /// </summary>
    public partial class OverworldWorldSync : NetEntity
    {
        /// <summary>The network key the server spawns it under.</summary>
        public const string Key = "overworld:world";

        /// <summary>Snapshots a second, by default.</summary>
        public const float DefaultSendRate = 20f;

        // server -> every peer: an encoded WorldSnapshot. Reliable, so a snapshot larger than one packet still arrives.
        [ToClients]
        public partial void Publish(byte[] snapshot);

        [Tooltip("Snapshots a second the server sends.")]
        [SerializeField, Range(1f, 60f)] private float sendRate = DefaultSendRate;

        private float _untilNext;
        private int _sequence;

        /// <summary>Snapshots a second the server sends.</summary>
        public float SendRate
        {
            get => sendRate;
            set => sendRate = Mathf.Clamp(value, 1f, 60f);
        }

        /// <summary>Server: the authority whose game it sends.</summary>
        public OverworldPeer Authority { get; private set; }

        /// <summary>Every peer: the newest snapshot received (null before the first).</summary>
        public WorldSnapshot Latest { get; private set; }

        /// <summary>Every peer: snapshots received that decoded and were newer than <see cref="Latest"/>.</summary>
        public int Received { get; private set; }

        /// <summary>Every peer: raised with each newer snapshot.</summary>
        public event Action<WorldSnapshot> SnapshotReceived;

        /// <summary>The world sync of <paramref name="session"/>, once it has spawned there (null before).</summary>
        public static OverworldWorldSync For(NetSession session) =>
            session != null && session.Objects.TryGet(Key, out OverworldWorldSync sync) ? sync : null;

        /// <summary>Server: spawns the world sync for <paramref name="session"/> from <paramref name="prefab"/> (a prefab with a NetKey).</summary>
        public static OverworldWorldSync Serve(NetSession session, GameObject prefab)
        {
            var existing = For(session);
            if (existing != null) return existing;
            return session.Objects.Spawn(prefab, Key).GetComponent<OverworldWorldSync>();
        }

        /// <summary>Server: captures the authority's game and sends it now (it also does so <see cref="SendRate"/> times a second).</summary>
        public void PublishNow()
        {
            if (!IsServer || Authority == null) return;
            Publish(Authority.Capture(++_sequence).Encode());
        }

        protected override void OnSpawned()
        {
            if (IsServer) Authority = OverworldAuthority.For(Session);
        }

        protected override void OnDespawned()
        {
            Authority = null;
        }

        private void OnPublish(byte[] snapshot)
        {
            var decoded = WorldSnapshot.Decode(snapshot);
            if (decoded == null || (Latest != null && decoded.Sequence <= Latest.Sequence)) return;
            Latest = decoded;
            Received++;
            SnapshotReceived?.Invoke(decoded);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || Authority == null) return;

            // Real time: the snapshots keep going while the game is held, so peers see the pause.
            _untilNext -= Time.unscaledDeltaTime;
            if (_untilNext <= 0f)
            {
                _untilNext += 1f / sendRate;
                if (_untilNext < 0f) _untilNext = 0f;
                PublishNow();
            }
        }
    }
}

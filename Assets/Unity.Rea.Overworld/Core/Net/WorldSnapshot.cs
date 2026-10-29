using System;
using System.Collections.Generic;
using System.IO;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// What the one authority's game looks like at one moment, as it travels to the online peers (U.OVW-T46): the tick,
    /// the in-game time, the weather, every hero (where it stands, its facing, mode and vitals) and every player in the
    /// game with the hero it leads. Captured from the authority's own systems (any may be null) and read by presentation;
    /// nothing changes a game with it.
    /// </summary>
    public sealed class WorldSnapshot
    {
        /// <summary>The wire version; bytes with any other first byte are refused.</summary>
        public const byte Version = 1;

        /// <summary>One hero as the authority has it.</summary>
        public readonly struct HeroShown
        {
            public HeroShown(float x, float y, float z, Facing facing, HeroMode mode, int health, int mana, int stamina)
            {
                X = x; Y = y; Z = z; Facing = facing; Mode = mode; Health = health; Mana = mana; Stamina = stamina;
            }

            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public Facing Facing { get; }
            public HeroMode Mode { get; }
            public int Health { get; }
            public int Mana { get; }
            public int Stamina { get; }
        }

        /// <summary>One player in the authority's game and the hero it leads (-1 when there is no party).</summary>
        public readonly struct PlayerShown
        {
            public PlayerShown(PlayerId id, int leader) { Id = id; Leader = leader; }
            public PlayerId Id { get; }
            public int Leader { get; }
        }

        readonly List<HeroShown> heroes = new List<HeroShown>();
        readonly List<PlayerShown> players = new List<PlayerShown>();

        /// <summary>Counts up with each snapshot the authority sends, so a peer keeps only the newest.</summary>
        public int Sequence { get; private set; }

        public long Tick { get; private set; }

        /// <summary>The in-game date and time (DateTime ticks; 0 when the authority has no clock).</summary>
        public long TimeTicks { get; private set; }

        /// <summary>The weather now (Clear when the authority has none).</summary>
        public WeatherKind WeatherNow { get; private set; }

        /// <summary>Whether the authority's game is held (paused).</summary>
        public bool Held { get; private set; }

        public IReadOnlyList<HeroShown> Heroes => heroes;
        public IReadOnlyList<PlayerShown> Players => players;

        /// <summary>The player <paramref name="id"/>'s entry, when the snapshot has it.</summary>
        public bool TryGetPlayer(PlayerId id, out PlayerShown player)
        {
            foreach (var p in players)
                if (p.Id == id) { player = p; return true; }
            player = default;
            return false;
        }

        /// <summary>The authority's game now. Party, clock and weather are optional.</summary>
        public static WorldSnapshot Capture(Game game, Party party, WorldClock clock, Weather weather, int sequence)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var s = new WorldSnapshot
            {
                Sequence = sequence,
                Tick = game.Tick,
                TimeTicks = clock != null ? clock.Now.Ticks : 0L,
                WeatherNow = weather != null ? weather.Kind : default,
                Held = game.IsHeld,
            };
            if (party != null)
                foreach (var h in party.Heroes)
                    s.heroes.Add(new HeroShown(h.X, h.Y, h.Z, h.Facing, h.Mode, h.Health, h.Mana, h.Stamina));
            var ids = new List<PlayerId>(game.Players);
            ids.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var id in ids) s.players.Add(new PlayerShown(id, party != null ? party.LeaderOf(id) : -1));
            return s;
        }

        public byte[] Encode()
        {
            using (var stream = new MemoryStream(64 + heroes.Count * 32))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                w.Write(Sequence);
                w.Write(Tick);
                w.Write(TimeTicks);
                w.Write((int)WeatherNow);
                w.Write(Held);
                w.Write(heroes.Count);
                foreach (var h in heroes)
                {
                    w.Write(h.X);
                    w.Write(h.Y);
                    w.Write(h.Z);
                    w.Write((int)h.Facing);
                    w.Write((int)h.Mode);
                    w.Write(h.Health);
                    w.Write(h.Mana);
                    w.Write(h.Stamina);
                }

                w.Write(players.Count);
                foreach (var p in players)
                {
                    w.Write(p.Id.Value);
                    w.Write(p.Leader);
                }

                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The snapshot <paramref name="bytes"/> carry; null for bytes that are not one of this version.</summary>
        public static WorldSnapshot Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1 || bytes[0] != Version) return null;
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (var r = new BinaryReader(stream))
                {
                    r.ReadByte();
                    var s = new WorldSnapshot
                    {
                        Sequence = r.ReadInt32(),
                        Tick = r.ReadInt64(),
                        TimeTicks = r.ReadInt64(),
                        WeatherNow = (WeatherKind)r.ReadInt32(),
                        Held = r.ReadBoolean(),
                    };
                    var heroCount = r.ReadInt32();
                    if (heroCount < 0 || heroCount > 1024) return null;
                    for (var i = 0; i < heroCount; i++)
                        s.heroes.Add(new HeroShown(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), (Facing)r.ReadInt32(), (HeroMode)r.ReadInt32(),
                            r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
                    var playerCount = r.ReadInt32();
                    if (playerCount < 0 || playerCount > 1024) return null;
                    for (var i = 0; i < playerCount; i++)
                        s.players.Add(new PlayerShown(new PlayerId(r.ReadInt32()), r.ReadInt32()));
                    return stream.Position == stream.Length ? s : null;
                }
            }
            catch (EndOfStreamException) { return null; }
            catch (IOException) { return null; }
        }
    }
}

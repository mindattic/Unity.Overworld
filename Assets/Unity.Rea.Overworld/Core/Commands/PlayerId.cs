using System;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// Who issued a command. Every command carries one, there is one authority and no global
    /// "current player", so a second player or a network seam needs no rewrite.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <summary>Not a player: never accepted as a command's issuer.</summary>
        public static readonly PlayerId None = new PlayerId(0);

        /// <summary>The first (and, in the original, only) player.</summary>
        public static readonly PlayerId One = new PlayerId(1);

        public int Value { get; }

        public PlayerId(int value) { Value = value; }

        public bool IsNone => Value == 0;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsNone ? "none" : "player " + Value;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
    }
}

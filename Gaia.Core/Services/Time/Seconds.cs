using System;

namespace Gaia.Core.Services.Time
{
    /// <summary>A length of time in seconds, or a moment measured as the time since the clock started.</summary>
    /// <remarks>
    ///     Construction from float is explicit, so a bare number can never be mistaken for a duration, and the number of
    ///     seconds comes back out through Value. Arithmetic and comparisons stay on the type, so two times can be compared or
    ///     taken apart without ever leaving it.
    /// </remarks>
    public readonly struct Seconds : IEquatable<Seconds>
    {
        /// <summary>The number of seconds.</summary>
        public float Value { get; }

        /// <summary>Creates a time of the given number of seconds.</summary>
        public Seconds(float value)
        {
            Value = value;
        }

        /// <summary>No time at all.</summary>
        public static readonly Seconds Zero = new(0f);

        public static Seconds operator +(Seconds left, Seconds right)
        {
            return new Seconds(left.Value + right.Value);
        }

        public static Seconds operator -(Seconds left, Seconds right)
        {
            return new Seconds(left.Value - right.Value);
        }

        public static bool operator <(Seconds left, Seconds right)
        {
            return left.Value < right.Value;
        }

        public static bool operator >(Seconds left, Seconds right)
        {
            return left.Value > right.Value;
        }

        public static bool operator <=(Seconds left, Seconds right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >=(Seconds left, Seconds right)
        {
            return left.Value >= right.Value;
        }

        public bool Equals(Seconds other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is Seconds other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(Seconds left, Seconds right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Seconds left, Seconds right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Value}s";
        }
    }
}
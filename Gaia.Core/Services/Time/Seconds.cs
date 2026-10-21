using System;

namespace Gaia.Core.Services.Time
{
    /// <summary>A length of time in seconds, or a moment measured as the time since the clock started.</summary>
    /// <remarks>
    ///     <para>
    ///         There is no implicit conversion from a number, so a bare number cannot stand in for a duration. Arithmetic
    ///         and comparisons stay on the type, so two times can be compared or taken apart without ever leaving it.
    ///     </para>
    ///     <para>
    ///         The backing value is a double because absolute session time outgrows a float. A float that accumulates
    ///         sixtieths of a second runs several percent off from around four and a half hours into a session. From around
    ///         seventy-seven hours it runs almost twice as fast as the time added to it, and from around a hundred and sixteen
    ///         hours it stops advancing, because the step falls below half the gap between adjacent float values. A double
    ///         holds tick precision for any session a game can run.
    ///     </para>
    /// </remarks>
    public readonly struct Seconds : IEquatable<Seconds>
    {
        /// <summary>The number of seconds.</summary>
        public double Value { get; }

        /// <summary>Creates a time of the given number of seconds.</summary>
        public Seconds(double value)
        {
            Value = value;
        }

        /// <summary>No time at all.</summary>
        public static readonly Seconds Zero = new(0.0);

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
            return FormattableString.Invariant($"{Value}s");
        }
    }
}

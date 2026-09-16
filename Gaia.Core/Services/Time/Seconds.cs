using System;

namespace Gaia.Core.Services.Time
{
    /// <summary>A length of time in seconds, or a moment measured as the time since the clock started.</summary>
    /// <remarks>
    ///     <para>
    ///         Construction from a number is explicit, so a bare number can never be mistaken for a duration, and the number
    ///         of seconds comes back out through Value. Arithmetic and comparisons stay on the type, so two times can be
    ///         compared or taken apart without ever leaving it.
    ///     </para>
    ///     <para>
    ///         The backing value is a double because absolute session time outgrows a float. Accumulating sixtieths of a
    ///         second in a float drifts by several percent from around four and a half hours of session, runs almost twice as
    ///         fast from around seventy three, and stops advancing near a hundred and forty five, where the step falls below
    ///         half a unit of precision. A double holds tick precision for any session a game can run. The engine boundary
    ///         converts once, and a float fed to the constructor widens losslessly.
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

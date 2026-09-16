using System;

namespace Gaia.Core.Spatial
{
    /// <summary>An infinite flat surface, held as a unit normal and the distance from the origin along it.</summary>
    /// <remarks>A position lies on the plane when the dot product of Normal with it equals Distance.</remarks>
    /// <seealso>
    ///     Fletcher Dunn and Ian Parberry, 3D Math Primer for Graphics and Game Development, described at
    ///     https://gamemath.com/book/geomprims.html#planes
    /// </seealso>
    public readonly struct Plane : IEquatable<Plane>
    {
        /// <summary>The unit vector pointing out of the front of the plane.</summary>
        public Vector3 Normal { get; }

        /// <summary>How far the plane lies from the origin, measured along Normal.</summary>
        public float Distance { get; }

        /// <summary>Creates a plane from a normal and a distance, scaling both down by the normal's length.</summary>
        /// <remarks>
        ///     A plane is unchanged when its normal and distance are divided by the same amount, so dividing both by the
        ///     normal's length yields the same plane held with the unit normal that SignedDistance relies on.
        /// </remarks>
        /// <param name="normal">Which way the front of the plane faces. Need not be unit length, but must not be zero.</param>
        /// <param name="distance">How far the plane lies from the origin along the normal, before normalizing.</param>
        /// <exception cref="ArgumentException">The normal has no length.</exception>
        public Plane(Vector3 normal, float distance)
        {
            float length = normal.Magnitude;

            if (length <= 0f)
            {
                throw new ArgumentException("A plane needs a normal with some length.", nameof(normal));
            }

            Normal = normal / length;
            Distance = distance / length;
        }

        /// <summary>Makes the plane through a given position that faces a given way.</summary>
        /// <param name="point">A position on the plane.</param>
        /// <param name="normal">Which way the front of the plane faces. Need not be unit length, but must not be zero.</param>
        public static Plane FromPointAndNormal(Vector3 point, Vector3 normal)
        {
            // Dot(normal, point) overstates the distance by the normal's length whenever the normal is not unit, but
            // the constructor divides the normal and the distance by that same length, so the plane comes out right.
            return new Plane(normal, Vector3.Dot(normal, point));
        }

        /// <summary>How far a position lies in front of the plane. Negative means behind it.</summary>
        public float SignedDistance(Vector3 position)
        {
            return Vector3.Dot(Normal, position) - Distance;
        }

        /// <summary>True if the position is on the side Normal points to, or on the plane itself.</summary>
        public bool IsInFront(Vector3 position)
        {
            return SignedDistance(position) >= 0f;
        }

        public bool Equals(Plane other)
        {
            return Normal.Equals(other.Normal) && Distance.Equals(other.Distance);
        }

        public override bool Equals(object? obj)
        {
            return obj is Plane other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Normal, Distance);
        }

        public static bool operator ==(Plane left, Plane right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Plane left, Plane right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"Normal {Normal}, Distance {Distance}");
        }
    }
}

using System;

namespace Gaia.Core.Spatial
{
    /// <summary>A position or direction in two dimensions.</summary>
    /// <seealso>
    ///     Fletcher Dunn and Ian Parberry, 3D Math Primer for Graphics and Game Development, described at
    ///     https://gamemath.com/book/vectors.html
    /// </seealso>
    public readonly struct Vector2 : IEquatable<Vector2>
    {
        /// <summary>The component along the X axis.</summary>
        public float X { get; }

        /// <summary>The component along the Y axis.</summary>
        public float Y { get; }

        /// <summary>Creates a vector from its two components.</summary>
        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The vector with every component 0.</summary>
        public static readonly Vector2 Zero = new(0f, 0f);

        /// <summary>The length of this vector.</summary>
        public float Magnitude => MathF.Sqrt(SquaredMagnitude);

        /// <summary>The length of this vector, squared.</summary>
        /// <remarks>Cheaper than Magnitude, since there is no square root. Enough for comparing two lengths.</remarks>
        public float SquaredMagnitude => X * X + Y * Y;

        /// <summary>This vector turned a quarter turn counterclockwise, which is (-Y, X).</summary>
        public Vector2 Perpendicular => new(-Y, X);

        /// <summary>The straight-line distance between two positions.</summary>
        public static float Distance(Vector2 from, Vector2 to)
        {
            return (to - from).Magnitude;
        }

        /// <summary>The straight-line distance between two positions, squared.</summary>
        /// <remarks>Cheaper than Distance, and enough for comparing against a radius that has itself been squared.</remarks>
        public static float SquaredDistance(Vector2 from, Vector2 to)
        {
            return (to - from).SquaredMagnitude;
        }

        /// <summary>The dot product of two vectors.</summary>
        public static float Dot(Vector2 left, Vector2 right)
        {
            return left.X * right.X + left.Y * right.Y;
        }

        /// <summary>The cross product of two vectors, which in two dimensions is a number rather than a vector.</summary>
        /// <remarks>
        ///     The signed area of the parallelogram the two span, which is the Z component of the three-dimensional cross
        ///     product of the same two vectors laid in a plane. It is positive when right lies counterclockwise of left, so its
        ///     sign is which side of left the other falls on.
        /// </remarks>
        public static float Cross(Vector2 left, Vector2 right)
        {
            return left.X * right.Y - left.Y * right.X;
        }

        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            return new Vector2(left.X + right.X, left.Y + right.Y);
        }

        public static Vector2 operator -(Vector2 left, Vector2 right)
        {
            return new Vector2(left.X - right.X, left.Y - right.Y);
        }

        public static Vector2 operator *(Vector2 vector, float scale)
        {
            return new Vector2(vector.X * scale, vector.Y * scale);
        }

        public static Vector2 operator /(Vector2 vector, float divisor)
        {
            return new Vector2(vector.X / divisor, vector.Y / divisor);
        }

        public bool Equals(Vector2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object? obj)
        {
            return obj is Vector2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(Vector2 left, Vector2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2 left, Vector2 right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"({X}, {Y})");
        }
    }
}

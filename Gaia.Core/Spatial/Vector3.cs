using System;

namespace Gaia.Core.Spatial
{
    /// <summary>A position or direction in three dimensions.</summary>
    /// <seealso>
    ///     Fletcher Dunn and Ian Parberry, 3D Math Primer for Graphics and Game Development, described at
    ///     https://gamemath.com/book/vectors.html
    /// </seealso>
    public readonly struct Vector3 : IEquatable<Vector3>
    {
        /// <summary>The component along the X axis, which is right.</summary>
        public float X { get; }

        /// <summary>The component along the Y axis, which is up.</summary>
        public float Y { get; }

        /// <summary>The component along the Z axis, which is forward.</summary>
        public float Z { get; }

        /// <summary>Creates a vector from its three components.</summary>
        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The vector with every component 0.</summary>
        public static readonly Vector3 Zero = new(0f, 0f, 0f);

        /// <summary>The length of this vector.</summary>
        public float Magnitude => MathF.Sqrt(SquaredMagnitude);

        /// <summary>The length of this vector, squared.</summary>
        /// <remarks>Cheaper than Magnitude, since there is no square root. Enough for comparing two lengths.</remarks>
        public float SquaredMagnitude => X * X + Y * Y + Z * Z;

        /// <summary>The straight-line distance between two positions.</summary>
        public static float Distance(Vector3 from, Vector3 to)
        {
            return (to - from).Magnitude;
        }

        /// <summary>The straight-line distance between two positions, squared.</summary>
        /// <remarks>Cheaper than Distance, and enough for comparing against a radius that has itself been squared.</remarks>
        public static float SquaredDistance(Vector3 from, Vector3 to)
        {
            return (to - from).SquaredMagnitude;
        }

        /// <summary>The dot product of two vectors.</summary>
        public static float Dot(Vector3 left, Vector3 right)
        {
            return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
        }

        /// <summary>The cross product of two vectors, which is perpendicular to both.</summary>
        public static Vector3 Cross(Vector3 left, Vector3 right)
        {
            return new Vector3(left.Y * right.Z - left.Z * right.Y, left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }

        public static Vector3 operator +(Vector3 left, Vector3 right)
        {
            return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vector3 operator -(Vector3 left, Vector3 right)
        {
            return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vector3 operator *(Vector3 vector, float scale)
        {
            return new Vector3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        public static Vector3 operator /(Vector3 vector, float divisor)
        {
            return new Vector3(vector.X / divisor, vector.Y / divisor, vector.Z / divisor);
        }

        public bool Equals(Vector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object? obj)
        {
            return obj is Vector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static bool operator ==(Vector3 left, Vector3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3 left, Vector3 right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"({X}, {Y}, {Z})");
        }
    }
}

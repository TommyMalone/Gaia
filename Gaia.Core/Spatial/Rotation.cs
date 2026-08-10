using System;

namespace Gaia.Core.Spatial
{
    /// <summary>An orientation in three dimensions, held as a quaternion.</summary>
    /// <remarks>
    ///     The axes follow Unity's convention, which is Y up, Z forward and X right. An engine that arranges them
    ///     differently converts at its boundary rather than pushing its arrangement through the AI. Every conversion here
    ///     assumes the quaternion is unit length, which is what an engine hands over.
    /// </remarks>
    public readonly struct Rotation : IEquatable<Rotation>
    {
        /// <summary>
        ///     The sine of the pitch beyond which yaw and roll stop being separable, just short of straight up or down, where
        ///     ToEulerAngles switches to its gimbal lock branch.
        /// </summary>
        private const float GimbalLockThreshold = 0.99999f;

        /// <summary>Converts an angle in radians to the degrees ToEulerAngles reports.</summary>
        private const float RadiansToDegrees = 180f / MathF.PI;

        /// <summary>The X component of the quaternion.</summary>
        public float X { get; }

        /// <summary>The Y component of the quaternion.</summary>
        public float Y { get; }

        /// <summary>The Z component of the quaternion.</summary>
        public float Z { get; }

        /// <summary>The W component of the quaternion.</summary>
        public float W { get; }

        /// <summary>Creates a rotation from the four components of a unit quaternion.</summary>
        public Rotation(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>No rotation at all.</summary>
        public static readonly Rotation Identity = new(0f, 0f, 0f, 1f);

        /// <summary>The direction this rotation calls forward.</summary>
        /// <remarks>The third column of the rotation matrix this quaternion describes.</remarks>
        public Vector3 Forward => new(2f * (X * Z + W * Y), 2f * (Y * Z - W * X), 1f - 2f * (X * X + Y * Y));

        /// <summary>The direction this rotation calls right.</summary>
        /// <remarks>The first column of the rotation matrix this quaternion describes.</remarks>
        public Vector3 Right => new(1f - 2f * (Y * Y + Z * Z), 2f * (X * Y + W * Z), 2f * (X * Z - W * Y));

        /// <summary>The direction this rotation calls up.</summary>
        /// <remarks>The second column of the rotation matrix this quaternion describes.</remarks>
        public Vector3 Up => new(2f * (X * Y - W * Z), 1f - 2f * (X * X + Z * Z), 2f * (Y * Z + W * X));

        /// <summary>Reports this rotation as pitch, yaw and roll in degrees.</summary>
        /// <remarks>
        ///     <para>
        ///         The angles describe a yaw around Y, then a pitch around X, then a roll around Z, which is the order Unity
        ///         reports and the order most people mean by heading, pitch and bank. They are laid out as X for pitch, Y for yaw
        ///         and Z for roll, matching that same convention.
        ///     </para>
        ///     <para>
        ///         Pointing very nearly straight up or down leaves yaw and roll describing the same turn, so there is no single
        ///         answer. This reports the whole turn as yaw and no roll at all, which keeps the result usable and still rebuilds
        ///         the same orientation.
        ///     </para>
        ///     <para>
        ///         The arithmetic is the standard conversion, with its terms rearranged from the cited page's aerospace axis
        ///         order to the order above.
        ///     </para>
        /// </remarks>
        /// <returns>Pitch, yaw and roll in degrees.</returns>
        /// <seealso>
        ///     Conversion between quaternions and Euler angles, described at
        ///     https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles
        /// </seealso>
        public Vector3 ToEulerAngles()
        {
            float sinPitch = 2f * (W * X - Y * Z);

            float pitch;
            float yaw;
            float roll;

            if (MathF.Abs(sinPitch) >= GimbalLockThreshold)
            {
                pitch = sinPitch > 0f ? MathF.PI * 0.5f : -MathF.PI * 0.5f;
                yaw = MathF.Atan2(-2f * (X * Z - W * Y), 1f - 2f * (Y * Y + Z * Z));
                roll = 0f;
            }
            else
            {
                pitch = MathF.Asin(sinPitch);
                yaw = MathF.Atan2(2f * (X * Z + W * Y), 1f - 2f * (X * X + Y * Y));
                roll = MathF.Atan2(2f * (X * Y + W * Z), 1f - 2f * (X * X + Z * Z));
            }

            return new Vector3(pitch * RadiansToDegrees, yaw * RadiansToDegrees, roll * RadiansToDegrees);
        }

        public bool Equals(Rotation other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        }

        public override bool Equals(object? obj)
        {
            return obj is Rotation other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, W);
        }

        public static bool operator ==(Rotation left, Rotation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Rotation left, Rotation right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z}, {W})";
        }
    }
}

using System;

namespace Gaia.Core.Spatial
{
    /// <summary>An orientation in three dimensions, held as a quaternion.</summary>
    /// <remarks>
    ///     The axes follow Unity's convention, which is Y up, Z forward and X right. An engine that arranges them
    ///     differently converts to this arrangement before handing a rotation over. Every conversion here assumes the
    ///     quaternion is unit length, as the engines require of a rotation, and nothing here normalizes one, so a quaternion
    ///     the host built from raw components or let drift over many products is the host's to normalize before it is handed
    ///     over.
    /// </remarks>
    public readonly struct Rotation : IEquatable<Rotation>
    {
        /// <summary>
        ///     The sine of the pitch beyond which yaw and roll stop being separable, just short of straight up or down, where
        ///     ToEulerAngles switches to its gimbal lock branch.
        /// </summary>
        private const float GimbalLockThreshold = 0.99999f;

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

        /// <summary>Builds the rotation that pitch, yaw and roll in degrees describe, the inverse of ToEulerAngles.</summary>
        /// <remarks>
        ///     The turns compose in the order ToEulerAngles describes, a yaw around Y, then a pitch around X, then a roll
        ///     around Z, so the angles it reports rebuild the rotation they came from. The arithmetic is the product of the three
        ///     single-axis quaternions, multiplied out.
        /// </remarks>
        /// <param name="pitch">The turn around X in degrees, positive toward the ground.</param>
        /// <param name="yaw">The turn around Y in degrees, positive to the right.</param>
        /// <param name="roll">The turn around Z in degrees, positive raising the right side.</param>
        public static Rotation FromEulerAngles(float pitch, float yaw, float roll)
        {
            float halfPitch = pitch * MathHelpers.DegreesToRadians * 0.5f;
            float halfYaw = yaw * MathHelpers.DegreesToRadians * 0.5f;
            float halfRoll = roll * MathHelpers.DegreesToRadians * 0.5f;

            float sinPitch = MathF.Sin(halfPitch);
            float cosPitch = MathF.Cos(halfPitch);
            float sinYaw = MathF.Sin(halfYaw);
            float cosYaw = MathF.Cos(halfYaw);
            float sinRoll = MathF.Sin(halfRoll);
            float cosRoll = MathF.Cos(halfRoll);

            return new Rotation(cosYaw * sinPitch * cosRoll + sinYaw * cosPitch * sinRoll,
                sinYaw * cosPitch * cosRoll - cosYaw * sinPitch * sinRoll,
                cosYaw * cosPitch * sinRoll - sinYaw * sinPitch * cosRoll,
                cosYaw * cosPitch * cosRoll + sinYaw * sinPitch * sinRoll);
        }

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
        ///     <para>The arithmetic is the cited conversion, with its axes relabeled to the order above.</para>
        /// </remarks>
        /// <returns>Pitch, yaw and roll in degrees, pitch from -90 to 90 and yaw and roll from -180 to 180.</returns>
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

            return new Vector3(pitch * MathHelpers.RadiansToDegrees, yaw * MathHelpers.RadiansToDegrees,
                roll * MathHelpers.RadiansToDegrees);
        }

        /// <summary>
        ///     The rotation that turns by the left operand first and then by the right one about the axes the first turn
        ///     sets, the Hamilton product of the two quaternions.
        /// </summary>
        /// <remarks>
        ///     FromEulerAngles builds its turn as yaw * pitch * roll, the yaw, then the pitch about the yawed axes, then the
        ///     roll about the pitched ones.
        /// </remarks>
        public static Rotation operator *(Rotation left, Rotation right)
        {
            return new Rotation(left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
                left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
                left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
                left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z);
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
            return FormattableString.Invariant($"({X}, {Y}, {Z}, {W})");
        }
    }
}

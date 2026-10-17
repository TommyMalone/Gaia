using System;

namespace Gaia.Core.Spatial
{
    /// <summary>
    ///     Where something is in the world, which way it faces and how it is stretched, an origin, a rotation and a
    ///     scale.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A point in local coordinates is so far along Right, so far along Up and so far along Forward from Origin,
    ///         each distance multiplied by the matching component of Scale.
    ///     </para>
    ///     <para>The three directions are computed once, when the transform is made, rather than on every conversion.</para>
    /// </remarks>
    /// <seealso>
    ///     Unreal Engine's FTransform, described at
    ///     https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Core/Math/FTransform
    /// </seealso>
    public readonly struct Transform : IEquatable<Transform>
    {
        /// <summary>Creates a transform at an origin, turned by a rotation, with a scale of (1, 1, 1).</summary>
        public Transform(Vector3 origin, Rotation rotation) : this(origin, rotation, new Vector3(1f, 1f, 1f))
        {
        }

        /// <summary>Creates a transform at an origin, turned by a rotation and stretched along its own axes by a scale.</summary>
        public Transform(Vector3 origin, Rotation rotation, Vector3 scale)
        {
            Origin = origin;
            Rotation = rotation;
            Scale = scale;
            Right = rotation.Right;
            Forward = rotation.Forward;
            Up = rotation.Up;
        }

        /// <summary>Copies a transform's directions and scale to another origin, without recomputing the directions.</summary>
        private Transform(Vector3 origin, Transform directions)
        {
            Origin = origin;
            Rotation = directions.Rotation;
            Scale = directions.Scale;
            Right = directions.Right;
            Forward = directions.Forward;
            Up = directions.Up;
        }

        /// <summary>
        ///     The transform at the world's origin with no turn and a scale of (1, 1, 1), whose local coordinates are the
        ///     world's.
        /// </summary>
        public static readonly Transform Identity = new(Vector3.Zero, Rotation.Identity);

        /// <summary>The world position of local (0, 0, 0).</summary>
        public Vector3 Origin { get; }

        /// <summary>How the local axes are turned from the world's.</summary>
        public Rotation Rotation { get; }

        /// <summary>How far one local unit stretches along Right, Up and Forward.</summary>
        public Vector3 Scale { get; }

        /// <summary>The world direction of local X, the rotation's Right, unit length whatever Scale is.</summary>
        public Vector3 Right { get; }

        /// <summary>The world direction of local Y, the rotation's Up, unit length whatever Scale is.</summary>
        public Vector3 Up { get; }

        /// <summary>The world direction of local Z, the rotation's Forward, unit length whatever Scale is.</summary>
        public Vector3 Forward { get; }

        /// <summary>
        ///     The same transform moved to another origin, keeping its rotation, its scale and the directions already
        ///     computed.
        /// </summary>
        public Transform WithOrigin(Vector3 origin)
        {
            return new Transform(origin, this);
        }

        /// <summary>Brings a world position into local coordinates, the inverse of ToWorld(Vector3).</summary>
        public Vector3 ToLocal(Vector3 position)
        {
            Vector3 offset = position - Origin;

            return new Vector3(Vector3.Dot(offset, Right) / Scale.X, Vector3.Dot(offset, Up) / Scale.Y,
                Vector3.Dot(offset, Forward) / Scale.Z);
        }

        /// <summary>Carries a point in local coordinates out to the world.</summary>
        public Vector3 ToWorld(Vector3 point)
        {
            return Origin + Right * (Scale.X * point.X) + Up * (Scale.Y * point.Y) + Forward * (Scale.Z * point.Z);
        }

        /// <summary>Places a transform given in this one's local coordinates into the world.</summary>
        /// <remarks>
        ///     The local origin is carried out as ToWorld(Vector3) carries a point, so this transform's turn and scale swing
        ///     and stretch it around Origin. The rotation is this transform's Rotation times the local one, which turns by this
        ///     transform first and then by the local rotation about this transform's axes. Where Scale has a negative component,
        ///     the local rotation is first mirrored by its signs, so a flipped transform mirrors the local turn as it mirrors the
        ///     local origin. The scales multiply axis by axis, which is exact when this transform's scale is the same size along
        ///     every axis, whatever its signs, or the local transform is unturned inside it, and otherwise stretches the local
        ///     transform along its own axes where an exact composition would shear it.
        /// </remarks>
        public Transform ToWorld(Transform local)
        {
            return new Transform(ToWorld(local.Origin), ToWorldRotation(local.Rotation), ToWorldScale(local.Scale));
        }

        /// <summary>The world rotation of a local transform turned by the given rotation, as ToWorld(Transform) composes it.</summary>
        internal Rotation ToWorldRotation(Rotation local)
        {
            Rotation turn = local;

            if (Scale.X < 0f || Scale.Y < 0f || Scale.Z < 0f)
            {
                float signX = Scale.X < 0f ? -1f : 1f;
                float signY = Scale.Y < 0f ? -1f : 1f;
                float signZ = Scale.Z < 0f ? -1f : 1f;

                turn = new Rotation(turn.X * signY * signZ, turn.Y * signX * signZ, turn.Z * signX * signY, turn.W);
            }

            return Rotation * turn;
        }

        /// <summary>The world scale of a local transform stretched by the given scale, as ToWorld(Transform) composes it.</summary>
        internal Vector3 ToWorldScale(Vector3 local)
        {
            return new Vector3(Scale.X * local.X, Scale.Y * local.Y, Scale.Z * local.Z);
        }

        public bool Equals(Transform other)
        {
            return Origin.Equals(other.Origin) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
        }

        public override bool Equals(object? obj)
        {
            return obj is Transform other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Origin, Rotation, Scale);
        }

        public static bool operator ==(Transform left, Transform right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Transform left, Transform right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Origin} turned {Rotation} scaled {Scale}";
        }
    }
}

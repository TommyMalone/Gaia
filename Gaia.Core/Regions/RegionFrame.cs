using System;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>Where a flat shape sits in the world and which way it faces, an origin and a rotation.</summary>
    /// <remarks>
    ///     <para>
    ///         A shape describes itself in the plane of its frame, as <see cref="Vector2" /> points with X along the
    ///         rotation's <see cref="Rotation.Right" /> and Y along its <see cref="Rotation.Forward" />, so an unrotated frame
    ///         lays the plane on the ground with Y along the world's Z. Height is the distance along
    ///         <see cref="Rotation.Up" />, which a shape either ignores or bounds.
    ///     </para>
    ///     <para>The three directions are computed once, when the frame is made, rather than on every containment test.</para>
    /// </remarks>
    public readonly struct RegionFrame : IEquatable<RegionFrame>
    {
        private readonly Vector3 _forward;
        private readonly Vector3 _right;
        private readonly Vector3 _up;

        /// <summary>Creates a frame at an origin, turned from the ground by a rotation.</summary>
        public RegionFrame(Vector3 origin, Rotation rotation)
        {
            Origin = origin;
            Rotation = rotation;
            _right = rotation.Right;
            _forward = rotation.Forward;
            _up = rotation.Up;
        }

        /// <summary>Copies a frame's directions to another origin, without recomputing them.</summary>
        private RegionFrame(Vector3 origin, RegionFrame directions)
        {
            Origin = origin;
            Rotation = directions.Rotation;
            _right = directions._right;
            _forward = directions._forward;
            _up = directions._up;
        }

        /// <summary>The frame at the world's origin with no rotation, whose plane is the ground.</summary>
        public static readonly RegionFrame Identity = new(Vector3.Zero, Rotation.Identity);

        /// <summary>The world position of the plane's (0, 0).</summary>
        public Vector3 Origin { get; }

        /// <summary>How the plane is turned from the ground.</summary>
        public Rotation Rotation { get; }

        /// <summary>The same frame moved to another origin, which is how a target's position stands in for a configured one.</summary>
        public RegionFrame WithOrigin(Vector3 origin)
        {
            return new RegionFrame(origin, this);
        }

        /// <summary>Brings a world position into the plane, reporting its height above the plane separately.</summary>
        public Vector2 ToLocal(Vector3 position, out float height)
        {
            Vector3 offset = position - Origin;

            height = Vector3.Dot(offset, _up);

            return new Vector2(Vector3.Dot(offset, _right), Vector3.Dot(offset, _forward));
        }

        /// <summary>Carries a point of the plane, raised to a height above it, out to the world.</summary>
        public Vector3 ToWorld(Vector2 point, float height)
        {
            return Origin + _right * point.X + _forward * point.Y + _up * height;
        }

        public bool Equals(RegionFrame other)
        {
            return Origin.Equals(other.Origin) && Rotation.Equals(other.Rotation);
        }

        public override bool Equals(object? obj)
        {
            return obj is RegionFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Origin, Rotation);
        }

        public static bool operator ==(RegionFrame left, RegionFrame right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RegionFrame left, RegionFrame right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Origin} turned {Rotation}";
        }
    }
}

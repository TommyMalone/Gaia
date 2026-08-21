using System;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Temp;

namespace Gaia.Core.Regions
{
    /// <summary>A disc in the ground plane, which ignores height.</summary>
    /// <remarks>
    ///     The center is Center, or CenterTarget if one is configured, so that a region can follow a character. A
    ///     CenterTarget whose <see cref="TargetBase.TryGetPosition" /> fails leaves the region containing nothing.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CircleRegion : RegionBase
    {
        private float _radius;
        private float _radiusSquared;

        /// <summary>Where the disc is, unless CenterTarget overrides it.</summary>
        public Vector3 Center { get; internal set; }

        /// <summary>The target whose position replaces Center while one is configured, or null to use Center.</summary>
        public TargetBase? CenterTarget { get; internal set; }

        /// <summary>How far the region reaches from its center.</summary>
        public float Radius
        {
            get => _radius;
            internal set
            {
                _radius = value;

                // Squared once here rather than on every containment test.
                _radiusSquared = value * value;
            }
        }

        /// <inheritdoc />
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            if (!TryGetCenter(out Vector3 center))
            {
                return false;
            }

            float offsetX = position.X - center.X;
            float offsetZ = position.Z - center.Z;

            return offsetX * offsetX + offsetZ * offsetZ <= _radiusSquared;
        }

        /// <inheritdoc />
        /// <remarks>A random position is reported at the center's height, since the disc ignores height.</remarks>
        /// <seealso>Disk point picking, described at https://mathworld.wolfram.com/DiskPointPicking.html</seealso>
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetCenter(out Vector3 center))
            {
                position = Vector3.Zero;

                return false;
            }

            // The square root keeps the density even. Without it, positions would crowd toward the center, since
            // a ring near the edge has far more area than a ring of the same width near the middle.
            float distance = Radius * MathF.Sqrt(RandomSource.NextFloat());
            float angle = RandomSource.NextFloat() * 2f * MathF.PI;

            position = new Vector3(center.X + distance * MathF.Cos(angle), center.Y,
                center.Z + distance * MathF.Sin(angle));

            return true;
        }

        /// <summary>
        ///     Finds where the center is this cycle, asking CenterTarget if one is configured and reading Center otherwise.
        ///     Fails only when the target cannot answer.
        /// </summary>
        private bool TryGetCenter(out Vector3 center)
        {
            if (CenterTarget == null)
            {
                center = Center;

                return true;
            }

            return CenterTarget.TryGetPosition(out center);
        }
    }
}

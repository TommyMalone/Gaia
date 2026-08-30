using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A ball swept along a vertical segment, which takes height into account.</summary>
    /// <remarks>
    ///     <para>
    ///         The segment runs from HalfHeight below Center to HalfHeight above it. A position is inside if it is within
    ///         Radius of the nearest point of the segment, which caps the ends with hemispheres rather than cutting them flat.
    ///     </para>
    ///     <para>
    ///         A random position is found by guessing inside the bounding box and keeping the first guess inside the
    ///         capsule, as for <see cref="SphereRegion" />. A capsule fills at least as much of its box as a ball fills of its
    ///         cube, since shrinking HalfHeight to nothing leaves exactly that ball, so the attempts are no more likely to run
    ///         out.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CapsuleRegion : RegionBase
    {
        private float _radius;
        private float _radiusSquared;

        /// <summary>Where the middle of the swept segment is.</summary>
        public Vector3 Center { get; internal set; }

        /// <summary>How far the swept segment reaches above and below Center.</summary>
        public float HalfHeight { get; internal set; }

        /// <summary>How far the region reaches from the segment.</summary>
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
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            float clampedY = Math.Clamp(position.Y, Center.Y - HalfHeight, Center.Y + HalfHeight);
            Vector3 nearestOnSegment = new(Center.X, clampedY, Center.Z);

            return Vector3.SquaredDistance(nearestOnSegment, position) <= _radiusSquared;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            Vector3 reach = new(Radius, HalfHeight + Radius, Radius);
            Vector3 min = Center - reach;
            Vector3 max = Center + reach;

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }
    }
}

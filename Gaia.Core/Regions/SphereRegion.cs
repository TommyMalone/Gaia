using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Temp;

namespace Gaia.Core.Regions
{
    /// <summary>A ball around a center, which takes height into account.</summary>
    /// <remarks>
    ///     <para>The center is Center, or CenterTarget if one is configured, as for <see cref="CircleRegion" />.</para>
    ///     <para>
    ///         A random position is found by guessing inside the bounding box and keeping the first guess inside the ball,
    ///         so it can fail, though a ball fills over half its box and <see cref="MathHelpers.MaxSampleAttempts" /> is
    ///         generous.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class SphereRegion : RegionBase
    {
        private float _radius;
        private float _radiusSquared;

        /// <summary>Where the ball is, unless CenterTarget overrides it.</summary>
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

            return Vector3.SquaredDistance(center, position) <= _radiusSquared;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetCenter(out Vector3 center))
            {
                position = Vector3.Zero;

                return false;
            }

            Vector3 reach = new(Radius, Radius, Radius);
            Vector3 min = center - reach;
            Vector3 max = center + reach;

            return MathHelpers.TryRejectionSample(this, min, max, out position);
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

using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>A ball around an origin, which takes height into account.</summary>
    /// <remarks>
    ///     <para>
    ///         The ball is around Origin, or around OriginTarget's position while one is configured, as for
    ///         <see cref="CircleRegion" />.
    ///     </para>
    ///     <para>
    ///         A random position is found by guessing inside the bounding box and keeping the first guess inside the ball,
    ///         so it can fail, though a ball fills over half its box and <see cref="MathHelpers.MaxSampleAttempts" /> is
    ///         generous.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class SphereRegion : RegionBase
    {
        private static readonly AiString OriginAttribute = "Origin";
        private static readonly AiString RadiusAttribute = "Radius";

        private float _radius;
        private float _radiusSquared;

        /// <summary>Where the ball is, unless OriginTarget overrides it. The world's origin until it is set.</summary>
        public Vector3 Origin { get; internal set; }

        /// <summary>The target whose position replaces Origin while one is configured, or null to use Origin.</summary>
        public TargetBase? OriginTarget { get; internal set; }

        /// <summary>How far the region reaches from its origin.</summary>
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
            if (!LoadOriginTarget(creationData, out TargetBase? originTarget))
            {
                return false;
            }

            OriginTarget = originTarget;

            SpecificationNode node = creationData.Node;

            if (node.HasAttribute(OriginAttribute))
            {
                if (!node.ReadVector3(OriginAttribute, out Vector3 origin))
                {
                    return false;
                }

                Origin = origin;
            }

            if (!node.ReadFloat(RadiusAttribute, out float radius))
            {
                return false;
            }

            Radius = radius;
            return true;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            if (!TryGetOrigin(out Vector3 origin))
            {
                return false;
            }

            return Vector3.SquaredDistance(origin, position) <= _radiusSquared;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetOrigin(out Vector3 origin))
            {
                position = Vector3.Zero;

                return false;
            }

            Vector3 reach = new(Radius, Radius, Radius);
            Vector3 min = origin - reach;
            Vector3 max = origin + reach;

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }

        /// <summary>
        ///     Finds where the origin is this cycle, asking OriginTarget if one is configured and reading Origin otherwise.
        ///     Fails only when the target cannot answer.
        /// </summary>
        private bool TryGetOrigin(out Vector3 origin)
        {
            if (OriginTarget == null)
            {
                origin = Origin;

                return true;
            }

            return OriginTarget.TryGetPosition(out origin);
        }
    }
}

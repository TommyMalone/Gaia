using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>A disc extruded a half-height either side of its plane, which makes a cylinder.</summary>
    /// <remarks>
    ///     <para>
    ///         Origin is the middle of the cylinder, and OriginTarget's position stands in for it while one is configured,
    ///         as for <see cref="CircleRegion" />. Unrotated, the cylinder stands upright.
    ///     </para>
    ///     <para>
    ///         A random position is picked on the disc the way <see cref="CircleRegion" /> picks one and spread evenly along
    ///         the height, so it never fails.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CylinderRegion : PlanarRegionBase
    {
        private static readonly AiString HalfHeightAttribute = "HalfHeight";
        private static readonly AiString RadiusAttribute = "Radius";

        private float _radius;
        private float _radiusSquared;

        /// <summary>The target whose position replaces Origin while one is configured, or null to use Origin.</summary>
        public TargetBase? OriginTarget { get; internal set; }

        /// <summary>How far the region reaches from its axis.</summary>
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

        /// <summary>How far the region reaches either side of its plane.</summary>
        public float HalfHeight { get; internal set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!LoadOriginTarget(creationData, out TargetBase? originTarget))
            {
                return false;
            }

            OriginTarget = originTarget;

            SpecificationNode node = creationData.Node;

            if (!LoadFrame(creationData) || !node.ReadFloat(RadiusAttribute, out float radius) ||
                !node.ReadFloat(HalfHeightAttribute, out float halfHeight))
            {
                return false;
            }

            Radius = radius;
            HalfHeight = halfHeight;
            return true;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            if (!TryGetFrame(OriginTarget, out RegionFrame frame))
            {
                return false;
            }

            Vector2 point = frame.ToLocal(position, out float height);

            return MathF.Abs(height) <= HalfHeight && point.SquaredMagnitude <= _radiusSquared;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetFrame(OriginTarget, out RegionFrame frame))
            {
                position = Vector3.Zero;

                return false;
            }

            Vector2 point = MathHelpers.RandomInDisc(RandomSource, Radius);
            float height = MathHelpers.RandomInRange(RandomSource, -HalfHeight, HalfHeight);

            position = frame.ToWorld(point, height);

            return true;
        }
    }
}

using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Regions
{
    /// <summary>A ball swept along a vertical segment, which takes height into account.</summary>
    /// <remarks>
    ///     <para>
    ///         The segment runs from HalfHeight below Origin to HalfHeight above it. A position is inside if it is within
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
        private static readonly AiString HalfHeightAttribute = "HalfHeight";
        private static readonly AiString OriginAttribute = "Origin";
        private static readonly AiString RadiusAttribute = "Radius";

        private float _radius;
        private float _radiusSquared;

        /// <summary>Where the middle of the swept segment is. The world's origin until it is set.</summary>
        public Vector3 Origin { get; set; }

        /// <summary>How far the swept segment reaches above and below Origin.</summary>
        public float HalfHeight { get; set; }

        /// <summary>How far the region reaches from the segment.</summary>
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = value;

                // Squared once here rather than on every containment test.
                _radiusSquared = value * value;
            }
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.Children.Count > 0)
            {
                SpecificationNode child = node.Children[0];

                creationData.ReportError(
                    $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                return false;
            }

            if (node.HasAttribute(OriginAttribute))
            {
                if (!node.ReadVector3(OriginAttribute, out Vector3 origin))
                {
                    return false;
                }

                Origin = origin;
            }

            if (!node.ReadFloat(RadiusAttribute, out float radius) ||
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
            float clampedY = Math.Clamp(position.Y, Origin.Y - HalfHeight, Origin.Y + HalfHeight);
            Vector3 nearestOnSegment = new(Origin.X, clampedY, Origin.Z);

            return Vector3.SquaredDistance(nearestOnSegment, position) <= _radiusSquared;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            Vector3 reach = new(Radius, HalfHeight + Radius, Radius);
            Vector3 min = Origin - reach;
            Vector3 max = Origin + reach;

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }
    }
}

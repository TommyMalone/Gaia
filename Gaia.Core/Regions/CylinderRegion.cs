using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Regions
{
    /// <summary>A disc footprint between a floor and a ceiling, which makes it a vertical cylinder.</summary>
    /// <remarks>
    ///     <para>
    ///         Only the X and Z of Center matter, as for <see cref="CircleRegion" />, since Floor and Ceiling bound the
    ///         height instead, as for <see cref="ExtrudedPolygonRegion" />.
    ///     </para>
    ///     <para>
    ///         A random position is picked on the disc the way <see cref="CircleRegion" /> picks one and spread evenly
    ///         between Floor and Ceiling, so it never fails.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CylinderRegion : RegionBase
    {
        private static readonly AiString CeilingAttribute = "Ceiling";
        private static readonly AiString CenterAttribute = "Center";
        private static readonly AiString FloorAttribute = "Floor";
        private static readonly AiString RadiusAttribute = "Radius";

        private float _radius;
        private float _radiusSquared;

        /// <summary>Where the disc is. Its Y is ignored, since Floor and Ceiling bound the height.</summary>
        public Vector3 Center { get; internal set; }

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

        /// <summary>The lowest height inside the region.</summary>
        public float Floor { get; internal set; }

        /// <summary>The highest height inside the region.</summary>
        public float Ceiling { get; internal set; }

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

            if (!node.ReadVector3(CenterAttribute, out Vector3 center) ||
                !node.ReadFloat(RadiusAttribute, out float radius) ||
                !node.ReadFloat(FloorAttribute, out float floor) ||
                !node.ReadFloat(CeilingAttribute, out float ceiling))
            {
                return false;
            }

            Center = center;
            Radius = radius;
            Floor = floor;
            Ceiling = ceiling;
            return true;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            float offsetX = position.X - Center.X;
            float offsetZ = position.Z - Center.Z;

            return position.Y >= Floor && position.Y <= Ceiling &&
                   offsetX * offsetX + offsetZ * offsetZ <= _radiusSquared;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            // The square root keeps the density even. The reasoning and the citation live on CircleRegion.
            float distance = Radius * MathF.Sqrt(RandomSource.NextFloat());
            float angle = RandomSource.NextFloat() * 2f * MathF.PI;
            float height = MathHelpers.RandomInRange(RandomSource, Floor, Ceiling);

            position = new Vector3(Center.X + distance * MathF.Cos(angle), height,
                Center.Z + distance * MathF.Sin(angle));

            return true;
        }
    }
}

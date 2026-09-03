using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Regions
{
    /// <summary>A footprint of any shape between a floor and a ceiling.</summary>
    /// <remarks>
    ///     <para>
    ///         It is the only shape here that is both concave and bounded in height, which is what most level geometry is. A
    ///         room is a footprint with a floor and a ceiling, and so is the inside of a building.
    ///     </para>
    ///     <para>A random position is spread evenly between Floor and Ceiling.</para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class ExtrudedPolygonRegion : PolygonRegionBase
    {
        private static readonly AiString CeilingAttribute = "Ceiling";
        private static readonly AiString FloorAttribute = "Floor";

        /// <summary>The lowest height inside the region.</summary>
        public float Floor { get; internal set; }

        /// <summary>The highest height inside the region.</summary>
        public float Ceiling { get; internal set; }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (!node.ReadFloat(FloorAttribute, out float floor) ||
                !node.ReadFloat(CeilingAttribute, out float ceiling))
            {
                return false;
            }

            Floor = floor;
            Ceiling = ceiling;
            return LoadFootprint(creationData);
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return position.Y >= Floor && position.Y <= Ceiling && IsInFootprint(position);
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!HasArea)
            {
                position = Vector3.Zero;

                return false;
            }

            Vector3 min = new(BoundsMin.X, Floor, BoundsMin.Z);
            Vector3 max = new(BoundsMax.X, Ceiling, BoundsMax.Z);

            return MathHelpers.TryRejectionSample(this, min, max, out position);
        }
    }
}

using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.Regions
{
    /// <summary>An axis-aligned rectangle in the ground plane, which ignores height.</summary>
    /// <remarks>Only X and Z of Min and Max are used by IsInRegion. A random position is reported at Min.Y.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class RectangleRegion : RegionBase
    {
        private static readonly AiString MaxAttribute = "Max";
        private static readonly AiString MinAttribute = "Min";

        /// <summary>The corner with the smallest X and Z.</summary>
        public Vector3 Min { get; internal set; }

        /// <summary>The corner with the largest X and Z.</summary>
        public Vector3 Max { get; internal set; }

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

            if (!node.ReadVector3(MinAttribute, out Vector3 min) || !node.ReadVector3(MaxAttribute, out Vector3 max))
            {
                return false;
            }

            Min = min;
            Max = max;
            return true;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            return position.X >= Min.X && position.X <= Max.X && position.Z >= Min.Z && position.Z <= Max.Z;
        }

        /// <inheritdoc />
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            position = new Vector3(MathHelpers.RandomInRange(RandomSource, Min.X, Max.X), Min.Y,
                MathHelpers.RandomInRange(RandomSource, Min.Z, Max.Z));

            return true;
        }
    }
}

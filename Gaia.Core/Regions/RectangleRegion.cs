using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.Regions
{
    /// <summary>A rectangle in the plane of its frame, which ignores height.</summary>
    /// <remarks>
    ///     Min and Max are the corners with the smallest and the largest coordinates in that plane, measured from Origin,
    ///     so a Rotation turns the rectangle about Origin. A rectangle that should turn about its middle puts Origin there and
    ///     Min opposite Max.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class RectangleRegion : PlanarRegionBase
    {
        private static readonly DescriptionBuilder Builder =
            DescribeFlatShape("A rectangle on the ground, which ignores height.");

        private static readonly AttributeDescription MinAttribute = Builder.Required("Min", AttributeKind.Vector2,
            "The corner with the smallest X and Y in the shape's plane, measured from Origin.");

        private static readonly AttributeDescription MaxAttribute = Builder.Required("Max", AttributeKind.Vector2,
            "The corner with the largest X and Y in the shape's plane, measured from Origin.");

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>The corner with the smallest X and Y, measured from Origin.</summary>
        public Vector2 Min { get; set; }

        /// <summary>The corner with the largest X and Y, measured from Origin.</summary>
        public Vector2 Max { get; set; }

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

            if (!LoadFrame(creationData) || !node.ReadVector2(MinAttribute.Name, out Vector2 min) ||
                !node.ReadVector2(MaxAttribute.Name, out Vector2 max))
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
            Vector2 point = Frame.ToLocal(position, out _);

            return point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
        }

        /// <inheritdoc />
        /// <remarks>A random position lies in the rectangle's own plane, since the rectangle ignores height.</remarks>
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            Vector2 point = new(MathHelpers.RandomInRange(RandomSource, Min.X, Max.X),
                MathHelpers.RandomInRange(RandomSource, Min.Y, Max.Y));

            position = Frame.ToWorld(point, 0f);

            return true;
        }
    }
}

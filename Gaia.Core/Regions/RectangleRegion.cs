using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>
    ///     A rectangle around its Origin in its own plane, reaching Extents along its right and forward, which ignores
    ///     height.
    /// </summary>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class RectangleRegion : PlanarRegionBase
    {
        private static readonly DescriptionBuilder Builder =
            DescribeShape("A rectangle on the ground, which ignores height.");

        private static readonly AttributeDescription ExtentsAttribute = Builder.Required("Extents",
            AttributeKind.Vector2,
            "Half the rectangle's size, the first number along its own right and the second along its forward, the world's X and Z while it is unturned. A negative component is read as its size.");

        internal static readonly ComponentDescription Description = Builder.Build();

        private Vector2 _extents;

        /// <summary>Creates a rectangle at the world's origin whose size is set through Extents.</summary>
        public RectangleRegion()
        {
        }

        /// <summary>Creates a rectangle reaching the given extents out from a center.</summary>
        /// <param name="center">Where the middle of the rectangle is, which becomes Origin.</param>
        /// <param name="extents">
        ///     How far the rectangle reaches from center along its own right and forward, half of its size along
        ///     each, a negative component read as its size.
        /// </param>
        public RectangleRegion(Vector3 center, Vector2 extents)
        {
            Origin = center;
            Extents = extents;
        }

        /// <summary>
        ///     How far the rectangle reaches from Origin, half of its size along each axis of its plane. X runs along its own
        ///     right and Y along its forward, the world's X and Z while it is unturned.
        /// </summary>
        /// <remarks>A negative component is stored as its size, so (-5, 10) reads back as (5, 10).</remarks>
        public Vector2 Extents
        {
            get => _extents;
            set => _extents = new Vector2(MathF.Abs(value.X), MathF.Abs(value.Y));
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!creationData.Node.ReadVector2(ExtentsAttribute.Name, out Vector2 extents))
            {
                return false;
            }

            Extents = extents;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            Vector2 point = InPlane(local, out _);
            Vector2 tolerance = InPlane(ToleranceAlongAxes, out _);

            return MathF.Abs(point.X) <= Extents.X + tolerance.X && MathF.Abs(point.Y) <= Extents.Y + tolerance.Y;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Never fails, since the point is picked inside Extents directly rather than guessed and checked. The point lies
        ///     in the rectangle's own plane, since the rectangle ignores height.
        /// </remarks>
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            Vector2 point = new(MathHelpers.RandomInRange(RandomSource, -Extents.X, Extents.X),
                MathHelpers.RandomInRange(RandomSource, -Extents.Y, Extents.Y));

            local = OutOfPlane(point, 0f);

            return true;
        }
    }
}

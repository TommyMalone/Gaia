using System;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;

namespace Gaia.Core.Regions
{
    /// <summary>A box around its Origin, reaching Extents along its own right, up and forward.</summary>
    /// <remarks>TryGetRandomLocalPosition never fails, since every point of the box is inside it.</remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class BoxRegion : RegionBase
    {
        private static readonly DescriptionBuilder Builder = DescribeShape("A box that can be placed and turned.");

        private static readonly AttributeDescription ExtentsAttribute = Builder.Required("Extents",
            AttributeKind.Vector3,
            "Half the box's size along its own right, up and forward. A negative component is read as its size.");

        internal static readonly ComponentDescription Description = Builder.Build();

        private Vector3 _extents;

        /// <summary>Creates a box at the world's origin whose size is set through Extents.</summary>
        public BoxRegion()
        {
        }

        /// <summary>Creates a box reaching the given extents out from a center.</summary>
        /// <param name="center">Where the middle of the box is, which becomes Origin.</param>
        /// <param name="extents">
        ///     How far the box reaches from center along each of its own axes, half of its size along each, a
        ///     negative component read as its size.
        /// </param>
        public BoxRegion(Vector3 center, Vector3 extents)
        {
            Origin = center;
            Extents = extents;
        }

        /// <summary>How far the box reaches from Origin along its own right, up and forward, half of its size along each.</summary>
        /// <remarks>A negative component is stored as its size, so (-5, 1, 1) reads back as (5, 1, 1).</remarks>
        public Vector3 Extents
        {
            get => _extents;
            set => _extents = new Vector3(MathF.Abs(value.X), MathF.Abs(value.Y), MathF.Abs(value.Z));
        }

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            if (!creationData.Node.ReadVector3(ExtentsAttribute.Name, out Vector3 extents))
            {
                return false;
            }

            Extents = extents;
            return true;
        }

        /// <inheritdoc />
        protected override bool IsInShape(Vector3 local)
        {
            Vector3 tolerance = ToleranceAlongAxes;

            return MathF.Abs(local.X) <= Extents.X + tolerance.X && MathF.Abs(local.Y) <= Extents.Y + tolerance.Y &&
                   MathF.Abs(local.Z) <= Extents.Z + tolerance.Z;
        }

        /// <inheritdoc />
        protected override bool TryGetRandomLocalPosition(out Vector3 local)
        {
            local = MathHelpers.RandomInBox(RandomSource, Vector3.Zero - Extents, Extents);

            return true;
        }
    }
}

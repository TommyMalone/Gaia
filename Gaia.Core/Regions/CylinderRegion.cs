using System;
using Gaia.Core.Spatial;
using Gaia.Core.Temp;

namespace Gaia.Core.Regions
{
    /// <summary>A disc footprint between a floor and a ceiling, which makes it a vertical cylinder.</summary>
    /// <remarks>
    ///     <para>
    ///         Only the X and Z of Center matter, as for <see cref="CircleRegion" />, since Floor and Ceiling bound the
    ///         height instead.
    ///     </para>
    ///     <para>
    ///         A random position is picked on the disc the way <see cref="CircleRegion" /> picks one and spread evenly
    ///         between Floor and Ceiling, so it never fails.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CylinderRegion : RegionBase
    {
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
        public override bool Init(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
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

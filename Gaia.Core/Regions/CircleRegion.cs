using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;
using Gaia.Core.Targets;

namespace Gaia.Core.Regions
{
    /// <summary>A disc in the plane of its frame, which ignores height.</summary>
    /// <remarks>
    ///     The disc is around Origin, or around OriginTarget's position while one is configured, so that a region can
    ///     follow a character. An OriginTarget whose <see cref="TargetBase.TryGetPosition" /> fails leaves the region
    ///     containing nothing.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.6</seealso>
    public sealed class CircleRegion : PlanarRegionBase
    {
        private static readonly AiString RadiusAttribute = "Radius";

        private float _radius;
        private float _radiusSquared;

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

            if (!LoadFrame(creationData) || !creationData.Node.ReadFloat(RadiusAttribute, out float radius))
            {
                return false;
            }

            Radius = radius;
            return true;
        }

        /// <inheritdoc />
        public override bool IsInRegion(Vector3 position)
        {
            if (!TryGetFrame(OriginTarget, out RegionFrame frame))
            {
                return false;
            }

            return frame.ToLocal(position, out _).SquaredMagnitude <= _radiusSquared;
        }

        /// <inheritdoc />
        /// <remarks>A random position lies in the disc's own plane, since the disc ignores height.</remarks>
        public override bool TryGetRandomPosition(out Vector3 position)
        {
            if (!TryGetFrame(OriginTarget, out RegionFrame frame))
            {
                position = Vector3.Zero;

                return false;
            }

            position = frame.ToWorld(MathHelpers.RandomInDisc(RandomSource, Radius), 0f);

            return true;
        }
    }
}

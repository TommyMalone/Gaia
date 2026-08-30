using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Measures the distance between two targets and hands it to a weight function.</summary>
    /// <remarks>
    ///     The two ends are targets rather than positions, which is what makes one consideration serve a sniper judging a
    ///     contact and a guard judging its post. Each target's <see cref="TargetBase.TryGetPosition" /> is called every cycle,
    ///     since a target that tracks an entity can answer differently each time.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.3.2</seealso>
    public sealed class DistanceConsideration : WeightedConsiderationBase
    {
        /// <summary>One end of the distance.</summary>
        public TargetBase? FromTarget { get; internal set; }

        /// <summary>The other end of the distance.</summary>
        public TargetBase? ToTarget { get; internal set; }

        /// <summary>True to measure along the ground plane rather than through the air.</summary>
        public bool IgnoreHeight { get; internal set; }

        /// <summary>What to report when FromTarget or ToTarget is null or its <see cref="TargetBase.TryGetPosition" /> fails.</summary>
        /// <remarks>
        ///     Neutral by default, so a target that has nothing to point at leaves its option alone. A configuration that
        ///     would rather veto the option until both ends exist sets this to do so.
        /// </remarks>
        public WeightValues MissingTargetWeights { get; internal set; } = WeightValues.Neutral;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            // Configurations cannot presently be loaded successfully.
            return false;
        }

        /// <inheritdoc />
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction)
        {
            if (FromTarget == null || ToTarget == null)
            {
                return MissingTargetWeights;
            }

            if (!FromTarget.TryGetPosition(out Vector3 from) || !ToTarget.TryGetPosition(out Vector3 to))
            {
                return MissingTargetWeights;
            }

            if (IgnoreHeight)
            {
                from = new Vector3(from.X, 0f, from.Z);
                to = new Vector3(to.X, 0f, to.Z);
            }

            return weightFunction.CalculateFloat(Vector3.Distance(from, to));
        }
    }
}

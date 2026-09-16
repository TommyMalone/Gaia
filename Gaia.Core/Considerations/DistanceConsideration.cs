using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;
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
        private static readonly AiString FromElement = "From";
        private static readonly AiString IgnoreHeightAttribute = "IgnoreHeight";
        private static readonly AiString MissingElement = "Missing";
        private static readonly AiString ToElement = "To";

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
        protected override string QueryPhrase => "a float";

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.HasAttribute(IgnoreHeightAttribute))
            {
                if (!node.ReadBool(IgnoreHeightAttribute, out bool ignoreHeight))
                {
                    return false;
                }

                IgnoreHeight = ignoreHeight;
            }

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == FromElement)
                {
                    if (!TryLoadTarget(child, creationData, out TargetBase? fromTarget))
                    {
                        return false;
                    }

                    FromTarget = fromTarget;
                }
                else if (child.Name == ToElement)
                {
                    if (!TryLoadTarget(child, creationData, out TargetBase? toTarget))
                    {
                        return false;
                    }

                    ToTarget = toTarget;
                }
                else if (child.Name == MissingElement)
                {
                    if (!WeightValues.TryRead(child, out WeightValues missingWeights))
                    {
                        return false;
                    }

                    MissingTargetWeights = missingWeights;
                }
                else if (child.Name == WeightFunctionElement)
                {
                    if (!TryLoadWeightFunction(child, creationData))
                    {
                        return false;
                    }
                }
                else
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }
            }

            if (FromTarget == null || ToTarget == null)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.Name.Text} element needs both a From and a To element");
                return false;
            }

            if (WeightFunction == null)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.Name.Text} element has no WeightFunction element");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsFloat;
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

        /// <summary>Builds one end of the measurement from its role-named element.</summary>
        /// <remarks>The factory reads only the element's Type, so a role element is an ordinary target node.</remarks>
        private static bool TryLoadTarget(
            SpecificationNode roleNode,
            AiCreationData creationData,
            out TargetBase? target)
        {
            return creationData.Factories.Targets.TryCreate(creationData.ForChild(roleNode), out target);
        }
    }
}

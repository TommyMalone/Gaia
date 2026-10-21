using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Considerations
{
    /// <summary>Measures the distance between two targets and hands it to a weight function.</summary>
    /// <remarks>
    ///     The two ends are targets rather than positions, so either end is whichever target the configuration names, one
    ///     of the three the library ships, the character the AI controls, an entity looked up by name or a fixed point, or one
    ///     the game registered itself. Each target's <see cref="TargetBase.TryGetPosition" /> is called every cycle rather
    ///     than once at load, since a target that tracks an entity reports where the entity is at the time.
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.3.2</seealso>
    public sealed class DistanceConsideration : WeightedConsiderationBase
    {
        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.Considerations,
            "Measures the distance between two targets.");

        private static readonly AttributeDescription IgnoreHeightAttribute = Builder.Optional("IgnoreHeight",
            AttributeKind.Bool, "false", "True to measure along the ground rather than through the air.");

        private static readonly SlotDescription FromSlot = Builder.Slot("From", ConceptualAbstraction.Targets, 1, 1);

        private static readonly SlotDescription ToSlot = Builder.Slot("To", ConceptualAbstraction.Targets, 1, 1);

        private static readonly SlotDescription MissingSlot =
            Builder.Add(WeightFunctionSlot).Slot("Missing", WeightValues.Description, 0, 1);

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Creates a consideration that a configuration fills.</summary>
        internal DistanceConsideration()
        {
        }

        /// <summary>One end of the distance.</summary>
        public TargetBase? FromTarget { get; internal set; }

        /// <summary>The other end of the distance.</summary>
        public TargetBase? ToTarget { get; internal set; }

        /// <summary>True to measure along the ground plane rather than through the air.</summary>
        public bool IgnoreHeight { get; internal set; }

        /// <summary>What to report when FromTarget or ToTarget is null or its <see cref="TargetBase.TryGetPosition" /> fails.</summary>
        /// <remarks>
        ///     Neutral by default, so a target that has nothing to point at leaves its option alone. A configuration that
        ///     would rather veto the option until both ends exist writes Veto="true" on its Missing element, which reads as
        ///     <see cref="WeightValues.Vetoed" />.
        /// </remarks>
        public WeightValues MissingTargetWeights { get; internal set; } = WeightValues.Neutral;

        /// <inheritdoc />
        protected override string QueryPhrase => "a float";

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.HasAttribute(IgnoreHeightAttribute.Name))
            {
                if (!node.ReadBool(IgnoreHeightAttribute.Name, out bool ignoreHeight))
                {
                    return false;
                }

                IgnoreHeight = ignoreHeight;
            }

            // Every child is one of the four slots, since the factory refused anything else against the description.
            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == FromSlot.Name)
                {
                    if (!TryLoadTarget(child, creationData, out TargetBase? fromTarget))
                    {
                        return false;
                    }

                    FromTarget = fromTarget;
                }
                else if (child.Name == ToSlot.Name)
                {
                    if (!TryLoadTarget(child, creationData, out TargetBase? toTarget))
                    {
                        return false;
                    }

                    ToTarget = toTarget;
                }
                else if (child.Name == MissingSlot.Name)
                {
                    if (!WeightValues.TryRead(child, out WeightValues missingWeights))
                    {
                        return false;
                    }

                    MissingTargetWeights = missingWeights;
                }
                else if (child.Name == WeightFunctionSlot.Name)
                {
                    if (!TryLoadWeightFunction(child, creationData))
                    {
                        return false;
                    }
                }
            }

            // The two ends are refused together in one sentence, which the factory's count of each slot alone cannot
            // say. The WeightFunction slot's minimum is the factory's to hold once this returns.
            if (FromTarget == null || ToTarget == null)
            {
                creationData.ReportError(
                    $"Line {node.LineNumber}, the {node.NameSpelling} element needs both a From and a To element");
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

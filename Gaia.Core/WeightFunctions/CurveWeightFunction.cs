using System.Collections.Generic;
using Gaia.Core.Curves;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Runs the input through the Curve configured for the addend and the one configured for the multiplier.</summary>
    /// <remarks>
    ///     <para>
    ///         The addend and the multiplier each have a curve of their own, so the addend curve can raise the addend of an
    ///         <see cref="Options.OptionBase" /> as the input climbs while the multiplier curve vetoes that option outside a
    ///         band.
    ///     </para>
    ///     <para>
    ///         Rank is reported as configured rather than curved, because it names a tier rather than a quantity and a value
    ///         part way between two tiers means nothing.
    ///     </para>
    ///     <para>
    ///         A multiplier below 0 has no meaning, as <see cref="WeightValues.Multiplier" /> says, so the multiplier
    ///         curve's output is clamped at 0. A multiplier curve with any keyframe authored below 0 is reported in one
    ///         loading warning, and a segment that overshoots below 0 between keyframes clamps the same way at evaluation.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class CurveWeightFunction : WeightFunctionBase
    {
        private static readonly DescriptionBuilder KeyframeBuilder =
            new("One point on the curve, with the slope into it and out of it.");

        private static readonly AttributeDescription InputAttribute = KeyframeBuilder.Required("Input",
            AttributeKind.Float, "The input this point is at.");

        private static readonly AttributeDescription ValueAttribute = KeyframeBuilder.Required("Value",
            AttributeKind.Float, "The output at this input.");

        private static readonly AttributeDescription InTangentAttribute = KeyframeBuilder.Optional("InTangent",
            AttributeKind.Float, "0", "The slope arriving at the point.");

        private static readonly AttributeDescription OutTangentAttribute = KeyframeBuilder.Optional("OutTangent",
            AttributeKind.Float, "0", "The slope leaving the point.");

        private static readonly DescriptionBuilder CurveBuilder = new("A curve through its keyframes.");

        private static readonly AttributeDescription LinearAttribute = CurveBuilder.Optional("Linear",
            AttributeKind.Bool, "false", "True to join the keyframes with straight lines in place of their slopes.");

        private static readonly SlotDescription KeyframeSlot = CurveBuilder.Slot("Keyframe", KeyframeBuilder.Build(),
            1, SlotDescription.Unbounded);

        private static readonly ComponentDescription CurveDescription = CurveBuilder.Build();

        private static readonly DescriptionBuilder Builder = new(ConceptualAbstraction.WeightFunctions,
            "Maps the input through one curve for the addend and another for the multiplier.");

        private static readonly AttributeDescription RankAttribute = Builder.Optional("Rank", AttributeKind.Float, null,
            "The tier the option is put in, whatever the input. Absent, the lowest tier.");

        private static readonly SlotDescription AddendCurveSlot = Builder.Slot("AddendCurve", CurveDescription, 0, 1);

        private static readonly SlotDescription MultiplierCurveSlot =
            Builder.Slot("MultiplierCurve", CurveDescription, 0, 1);

        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Creates a function that a configuration fills.</summary>
        internal CurveWeightFunction()
        {
        }

        /// <summary>The curve mapping the input to an addend, or null to contribute no addend.</summary>
        public Curve? AddendCurve { get; internal set; }

        /// <summary>The curve mapping the input to a multiplier, or null to leave the multiplier alone.</summary>
        public Curve? MultiplierCurve { get; internal set; }

        /// <summary>The rank to report, whatever the input.</summary>
        public float Rank { get; internal set; } = WeightValues.NoRank;

        /// <inheritdoc />
        public override bool ConvertsFloat => true;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.HasAttribute(RankAttribute.Name))
            {
                if (!node.ReadFloat(RankAttribute.Name, out float configuredRank))
                {
                    return false;
                }

                Rank = configuredRank;
            }

            // Every child is an AddendCurve or a MultiplierCurve, since the factory refused anything else against the
            // description.
            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == AddendCurveSlot.Name)
                {
                    if (!TryLoadCurve(child, creationData, out Curve? loadedAddendCurve))
                    {
                        return false;
                    }

                    AddendCurve = loadedAddendCurve;
                }
                else if (child.Name == MultiplierCurveSlot.Name)
                {
                    if (!TryLoadCurve(child, creationData, out Curve? loadedMultiplierCurve))
                    {
                        return false;
                    }

                    WarnAboutNegativeKeyframes(child, loadedMultiplierCurve!, creationData);

                    MultiplierCurve = loadedMultiplierCurve;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override WeightValues CalculateFloat(float value)
        {
            float addend = AddendCurve?.Evaluate(value) ?? 0f;
            float multiplier = MultiplierCurve?.Evaluate(value) ?? 1f;

            // A negative multiplier would flip the option's weight rather than discourage it, so the curve's output
            // clamps at the veto.
            if (multiplier < 0f)
            {
                multiplier = 0f;
            }

            return new WeightValues(addend, multiplier, Rank);
        }

        /// <summary>Warns once about a multiplier curve authored below 0, whose output will clamp at the veto.</summary>
        private static void WarnAboutNegativeKeyframes(
            SpecificationNode curveNode,
            Curve curve,
            AiCreationData creationData)
        {
            foreach (CurveKeyframe keyframe in curve.Keyframes)
            {
                if (keyframe.Value < 0f)
                {
                    creationData.ReportWarning(
                        $"Line {curveNode.LineNumber}, the {curveNode.NameSpelling} element dips below 0 and will be clamped to 0");
                    return;
                }
            }
        }

        /// <summary>Loads one curve from an AddendCurve or MultiplierCurve element's Keyframe children.</summary>
        /// <remarks>
        ///     A keyframe's tangents default to 0, which flattens the curve at the keyframe. A curve element carrying
        ///     Linear="true" replaces every tangent with the slope of its neighboring segment once its keyframes are loaded, so
        ///     explicit tangents and Linear do not combine. The factory refuses an empty curve element against the Keyframe slot's
        ///     minimum once Init returns, so this method does not check for one.
        /// </remarks>
        private static bool TryLoadCurve(SpecificationNode curveNode, AiCreationData creationData, out Curve? curve)
        {
            curve = null;

            Curve loaded = new();
            Dictionary<float, int> linesByInput = new();

            // Every child is a Keyframe, since the factory refused anything else against the curve's description.
            foreach (SpecificationNode child in curveNode.Children)
            {
                if (!child.ReadFloat(InputAttribute.Name, out float input) ||
                    !child.ReadFloat(ValueAttribute.Name, out float value))
                {
                    return false;
                }

                float inTangent = 0f;
                float outTangent = 0f;

                if (child.HasAttribute(InTangentAttribute.Name) &&
                    !child.ReadFloat(InTangentAttribute.Name, out inTangent))
                {
                    return false;
                }

                if (child.HasAttribute(OutTangentAttribute.Name) &&
                    !child.ReadFloat(OutTangentAttribute.Name, out outTangent))
                {
                    return false;
                }

                if (linesByInput.TryGetValue(input, out int firstLine))
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {child.NameSpelling} element repeats the input {input} of the {child.NameSpelling} on line {firstLine}");
                    return false;
                }

                linesByInput[input] = child.LineNumber;

                loaded.AddKeyframe(new CurveKeyframe(input, value, inTangent, outTangent));
            }

            bool linear = false;

            if (curveNode.HasAttribute(LinearAttribute.Name) && !curveNode.ReadBool(LinearAttribute.Name, out linear))
            {
                return false;
            }

            if (linear)
            {
                loaded.SetLinearTangents();
            }

            curve = loaded;
            return true;
        }
    }
}

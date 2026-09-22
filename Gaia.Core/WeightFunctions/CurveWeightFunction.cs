using System;
using System.Collections.Generic;
using Gaia.Core.Curves;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Runs the input through the Curve configured for the addend and the one configured for the multiplier.</summary>
    /// <remarks>
    ///     <para>
    ///         AddendCurve and MultiplierCurve each map the input on their own, so one configuration can raise the addend of
    ///         an <see cref="Options.OptionBase" /> as a value climbs while another vetoes it outside a band. An addend or
    ///         multiplier with no curve reports its identity, which is 0 for the addend and 1 for the multiplier, and so has
    ///         no effect on the option's weight.
    ///     </para>
    ///     <para>
    ///         Rank is reported as configured rather than curved, because it names a tier rather than a quantity and a value
    ///         part way between two tiers means nothing.
    ///     </para>
    ///     <para>
    ///         A multiplier below 0 has no meaning, as <see cref="WeightValues.Multiplier" /> says, so the multiplier
    ///         curve's output is clamped at 0. A keyframe authored below 0 draws a loading warning, and a segment that
    ///         overshoots below 0 between keyframes clamps the same way at evaluation.
    ///     </para>
    ///     <para>
    ///         OnInit takes every name it reads by from <see cref="Description" />, by position, rather than from the name
    ///         fields the description is built from, so an attribute or element the description leaves out is one loading
    ///         cannot read.
    ///     </para>
    /// </remarks>
    /// <seealso>"Modular AI", Section 8.5.2</seealso>
    public sealed class CurveWeightFunction : WeightFunctionBase
    {
        private static readonly AiString AddendCurveElement = "AddendCurve";
        private static readonly AiString InputAttribute = "Input";
        private static readonly AiString InTangentAttribute = "InTangent";
        private static readonly AiString KeyframeElement = "Keyframe";
        private static readonly AiString LinearAttribute = "Linear";
        private static readonly AiString MultiplierCurveElement = "MultiplierCurve";
        private static readonly AiString OutTangentAttribute = "OutTangent";
        private static readonly AiString RankAttribute = "Rank";
        private static readonly AiString ValueAttribute = "Value";

        private static readonly ComponentDescription KeyframeDescription = new(
            "One point on the curve, with the slope into it and out of it.", new[]
            {
                AttributeDescription.Required(InputAttribute, "Input", AttributeKind.Float,
                    "The input this point is at."),
                AttributeDescription.Required(ValueAttribute, "Value", AttributeKind.Float,
                    "The output at this input."),
                AttributeDescription.Optional(InTangentAttribute, "InTangent", AttributeKind.Float, "0",
                    "The slope arriving at the point."),
                AttributeDescription.Optional(OutTangentAttribute, "OutTangent", AttributeKind.Float, "0",
                    "The slope leaving the point."),
            }, Array.Empty<SlotDescription>());

        private static readonly SlotDescription KeyframeSlot = new(KeyframeElement, "Keyframe", KeyframeDescription, 1,
            SlotDescription.Unbounded);

        private static readonly ComponentDescription CurveDescription = new("A curve through its keyframes.", new[]
        {
            AttributeDescription.Optional(LinearAttribute, "Linear", AttributeKind.Bool, "false",
                "True to join the keyframes with straight lines in place of their slopes."),
        }, new[] { KeyframeSlot });

        private static readonly SlotDescription AddendCurveSlot = new(AddendCurveElement, "AddendCurve",
            CurveDescription, 0, 1);

        private static readonly SlotDescription MultiplierCurveSlot = new(MultiplierCurveElement, "MultiplierCurve",
            CurveDescription, 0, 1);

        /// <summary>Describes this function. Its attributes and slots are in the positions OnInit reads them from.</summary>
        internal static readonly ComponentDescription Description = new(ConceptualAbstraction.WeightFunctions,
            "Maps the input through one curve for the addend and another for the multiplier.", new[]
            {
                AttributeDescription.Optional(RankAttribute, "Rank", AttributeKind.Float, null,
                    "The tier the option is put in, whatever the input. Absent, the lowest tier."),
            }, new[] { AddendCurveSlot, MultiplierCurveSlot });

        /// <summary>Creates a function that a configuration fills.</summary>
        internal CurveWeightFunction()
        {
        }

        /// <summary>The curve mapping the input to an addend, or null to contribute no addend.</summary>
        public Curve? AddendCurve { get; internal set; }

        /// <summary>The curve mapping the input to a multiplier, or null to leave the multiplier alone.</summary>
        public Curve? MultiplierCurve { get; internal set; }

        /// <summary>The rank to report, whatever the input.</summary>
        public float Rank { get; internal set; } = float.MinValue;

        /// <inheritdoc />
        public override bool ConvertsFloat => true;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            AttributeDescription rank = Description.Attributes[0];
            SlotDescription addendCurve = Description.Slots[0];
            SlotDescription multiplierCurve = Description.Slots[1];

            if (node.HasAttribute(rank.Name))
            {
                if (!node.ReadFloat(rank.Name, out float configuredRank))
                {
                    return false;
                }

                Rank = configuredRank;
            }

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == addendCurve.Name)
                {
                    if (!TryLoadCurve(child, addendCurve.Inline!, creationData, out Curve? loadedAddendCurve))
                    {
                        return false;
                    }

                    AddendCurve = loadedAddendCurve;
                }
                else if (child.Name == multiplierCurve.Name)
                {
                    if (!TryLoadCurve(child, multiplierCurve.Inline!, creationData, out Curve? loadedMultiplierCurve))
                    {
                        return false;
                    }

                    WarnAboutNegativeKeyframes(child, loadedMultiplierCurve!, creationData);

                    MultiplierCurve = loadedMultiplierCurve;
                }
                else
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {node.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
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
                        $"Line {curveNode.LineNumber}, the {curveNode.Name.Text} element dips below 0 and will be clamped to 0");
                    return;
                }
            }
        }

        /// <summary>Loads one curve from a curve element's Keyframe children, reading by the names the curve's description holds.</summary>
        /// <remarks>
        ///     A keyframe's tangents default to 0, which flattens the curve at the keyframe. A curve element carrying
        ///     Linear="true" replaces every tangent with the slope of its neighboring segment once its keyframes are loaded, so
        ///     explicit tangents and Linear do not combine. A curve with no keyframes evaluates to 0 for every input, which would
        ///     silently veto through a multiplier, so an empty curve element is refused instead.
        /// </remarks>
        private static bool TryLoadCurve(
            SpecificationNode curveNode,
            ComponentDescription curveDescription,
            AiCreationData creationData,
            out Curve? curve)
        {
            curve = null;

            AttributeDescription linearAttribute = curveDescription.Attributes[0];
            SlotDescription keyframeSlot = curveDescription.Slots[0];
            ComponentDescription keyframeDescription = keyframeSlot.Inline!;
            AttributeDescription inputAttribute = keyframeDescription.Attributes[0];
            AttributeDescription valueAttribute = keyframeDescription.Attributes[1];
            AttributeDescription inTangentAttribute = keyframeDescription.Attributes[2];
            AttributeDescription outTangentAttribute = keyframeDescription.Attributes[3];

            Curve loaded = new();
            Dictionary<float, int> linesByInput = new();

            foreach (SpecificationNode child in curveNode.Children)
            {
                if (child.Name != keyframeSlot.Name)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {curveNode.Name.Text} element does not take {child.Name.Text} elements");
                    return false;
                }

                if (!child.ReadFloat(inputAttribute.Name, out float input) ||
                    !child.ReadFloat(valueAttribute.Name, out float value))
                {
                    return false;
                }

                float inTangent = 0f;
                float outTangent = 0f;

                if (child.HasAttribute(inTangentAttribute.Name) &&
                    !child.ReadFloat(inTangentAttribute.Name, out inTangent))
                {
                    return false;
                }

                if (child.HasAttribute(outTangentAttribute.Name) &&
                    !child.ReadFloat(outTangentAttribute.Name, out outTangent))
                {
                    return false;
                }

                if (linesByInput.TryGetValue(input, out int firstLine))
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {child.Name.Text} element repeats the input {input} of the {child.Name.Text} on line {firstLine}");
                    return false;
                }

                linesByInput[input] = child.LineNumber;

                loaded.AddKeyframe(new CurveKeyframe(input, value, inTangent, outTangent));
            }

            if (loaded.Keyframes.Count == 0)
            {
                creationData.ReportError(
                    $"Line {curveNode.LineNumber}, the {curveNode.Name.Text} element has no Keyframe elements");
                return false;
            }

            bool linear = false;

            if (curveNode.HasAttribute(linearAttribute.Name) && !curveNode.ReadBool(linearAttribute.Name, out linear))
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

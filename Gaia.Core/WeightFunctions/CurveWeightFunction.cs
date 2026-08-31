using Gaia.Core.Curves;
using Gaia.Core.Factories;
using Gaia.Core.Specifications;
using Gaia.Core.Strings;

namespace Gaia.Core.WeightFunctions
{
    /// <summary>Runs the input through a response curve.</summary>
    /// <remarks>
    ///     <para>
    ///         AddendCurve and MultiplierCurve each map the input on their own, so one configuration can raise an option's
    ///         desire as a value climbs while another vetoes it outside a band. A channel with no curve reports its identity,
    ///         which is 0 for the addend and 1 for the multiplier, and so has no effect on the option's weight.
    ///     </para>
    ///     <para>
    ///         Rank is reported as configured rather than curved, because it names a tier rather than a quantity and a value
    ///         part way between two tiers means nothing.
    ///     </para>
    ///     <para>
    ///         A multiplier below 0 has no meaning, two of them would cancel to a positive weight, so the multiplier curve's
    ///         output is clamped at 0. A keyframe authored below 0 draws a loading warning, and a segment that overshoots
    ///         below 0 between keyframes clamps the same way at evaluation.
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

        /// <summary>The curve mapping the input to an addend, or null to contribute no addend.</summary>
        public Curve? AddendCurve { get; internal set; }

        /// <summary>The curve mapping the input to a multiplier, or null to leave the multiplier alone.</summary>
        public Curve? MultiplierCurve { get; internal set; }

        /// <summary>The rank to report, whatever the input.</summary>
        public float Rank { get; internal set; } = float.MinValue;

        /// <inheritdoc />
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.HasAttribute(RankAttribute))
            {
                if (!node.ReadFloat(RankAttribute, out float rank))
                {
                    return false;
                }

                Rank = rank;
            }

            foreach (SpecificationNode child in node.Children)
            {
                if (child.Name == AddendCurveElement)
                {
                    if (!TryLoadCurve(child, creationData, out Curve? addendCurve))
                    {
                        return false;
                    }

                    AddendCurve = addendCurve;
                }
                else if (child.Name == MultiplierCurveElement)
                {
                    if (!TryLoadCurve(child, creationData, out Curve? multiplierCurve))
                    {
                        return false;
                    }

                    WarnAboutNegativeKeyframes(child, multiplierCurve!, creationData);

                    MultiplierCurve = multiplierCurve;
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
                        $"Line {curveNode.LineNumber}, the {curveNode.Name.Text} element dips below 0 and will be " +
                        "clamped to 0");
                    return;
                }
            }
        }

        /// <summary>Loads one curve from a curve element's Keyframe children.</summary>
        /// <remarks>
        ///     A keyframe's tangents default to 0, which flattens the curve at the keyframe. A curve element carrying
        ///     Linear="true" replaces every tangent with the slope of its neighboring segment once its keyframes are loaded, so
        ///     explicit tangents and Linear do not combine. A curve with no keyframes evaluates to 0 for every input, which would
        ///     silently veto through a multiplier, so an empty curve element is refused instead.
        /// </remarks>
        private static bool TryLoadCurve(SpecificationNode curveNode, AiCreationData creationData, out Curve? curve)
        {
            curve = null;

            Curve loaded = new();

            foreach (SpecificationNode child in curveNode.Children)
            {
                if (child.Name != KeyframeElement)
                {
                    creationData.ReportError(
                        $"Line {child.LineNumber}, the {curveNode.Name.Text} element does not take " +
                        $"{child.Name.Text} elements");
                    return false;
                }

                if (!child.ReadFloat(InputAttribute, out float input) ||
                    !child.ReadFloat(ValueAttribute, out float value))
                {
                    return false;
                }

                float inTangent = 0f;
                float outTangent = 0f;

                if (child.HasAttribute(InTangentAttribute) && !child.ReadFloat(InTangentAttribute, out inTangent))
                {
                    return false;
                }

                if (child.HasAttribute(OutTangentAttribute) && !child.ReadFloat(OutTangentAttribute, out outTangent))
                {
                    return false;
                }

                loaded.AddKeyframe(new CurveKeyframe(input, value, inTangent, outTangent));
            }

            if (loaded.Keyframes.Count == 0)
            {
                creationData.ReportError(
                    $"Line {curveNode.LineNumber}, the {curveNode.Name.Text} element has no Keyframe elements");
                return false;
            }

            bool linear = false;

            if (curveNode.HasAttribute(LinearAttribute) && !curveNode.ReadBool(LinearAttribute, out linear))
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

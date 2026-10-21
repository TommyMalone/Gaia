namespace Gaia.Core.Curves
{
    /// <summary>A single point on a <see cref="Curve" />, along with the slope the curve arrives at and leaves with.</summary>
    /// <remarks>
    ///     <para>The tangents are the slopes of the Hermite formulation cited on Curve, in units of value per input.</para>
    ///     <para>
    ///         A two-keyframe curve whose tangents are all 0 is not a straight line. It is flat at both keyframes and
    ///         steepest midway between them.
    ///     </para>
    /// </remarks>
    public readonly struct CurveKeyframe
    {
        /// <summary>The input value at which this keyframe sits.</summary>
        public float Input { get; }

        /// <summary>The value the curve reports at this input.</summary>
        public float Value { get; }

        /// <summary>The slope the curve arrives at this keyframe with.</summary>
        public float InTangent { get; }

        /// <summary>The slope the curve leaves this keyframe with.</summary>
        public float OutTangent { get; }

        /// <summary>Creates a keyframe the curve flattens out at, with InTangent and OutTangent both zero.</summary>
        public CurveKeyframe(float input, float value) : this(input, value, 0f, 0f)
        {
        }

        /// <summary>Creates a keyframe with the given input, value and tangents.</summary>
        public CurveKeyframe(float input, float value, float inTangent, float outTangent)
        {
            Input = input;
            Value = value;
            InTangent = inTangent;
            OutTangent = outTangent;
        }

        /// <summary>Returns a copy of this keyframe with the given tangents.</summary>
        public CurveKeyframe WithTangents(float inTangent, float outTangent)
        {
            return new CurveKeyframe(Input, Value, inTangent, outTangent);
        }
    }
}

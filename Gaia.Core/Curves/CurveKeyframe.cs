namespace Gaia.Core.Curves
{
    /// <summary>A single point on a <see cref="Curve" />, along with the slope the curve arrives at and leaves with.</summary>
    /// <remarks>
    ///     <para>
    ///         The tangents are in units of value per input, the slopes in the Hermite formulation cited on
    ///         <see cref="Curve" />.
    ///     </para>
    ///     <para>
    ///         A segment is a straight line when both of its tangents equal the slope between the two keyframes. Tangents of
    ///         0 make the curve flatten out as it reaches the keyframe, which is what gives a two-keyframe curve an ease-in
    ///         and ease-out shape.
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

using System.Collections.Generic;

namespace Gaia.Core.Curves
{
    /// <summary>
    ///     An arbitrary mapping from one float to another, defined by a set of keyframes and interpolated between them
    ///     with a cubic Hermite spline.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the same shape as an animation curve in an engine editor, and it can describe responses an exponent
    ///         cannot, such as a value that peaks in the middle of its range and falls away at both ends.
    ///     </para>
    ///     <para>
    ///         Inputs below the first keyframe report the first keyframe's value, and inputs above the last report the last,
    ///         so a curve never extrapolates past what it was given.
    ///     </para>
    /// </remarks>
    public class Curve
    {
        private readonly List<CurveKeyframe> _keyframes = new();

        /// <summary>The keyframes, in ascending order of input.</summary>
        public IReadOnlyList<CurveKeyframe> Keyframes => _keyframes;

        /// <summary>Adds a keyframe, keeping the keyframes ordered by input.</summary>
        public void AddKeyframe(CurveKeyframe keyframe)
        {
            int index = 0;

            while (index < _keyframes.Count && _keyframes[index].Input < keyframe.Input)
            {
                index++;
            }

            _keyframes.Insert(index, keyframe);
        }

        /// <summary>
        ///     Replaces every tangent with the slope of the neighboring segment, turning the curve into a series of straight
        ///     lines.
        /// </summary>
        public void SetLinearTangents()
        {
            for (int index = 0; index < _keyframes.Count; index++)
            {
                float inTangent = index > 0 ? LinearSlopeBetween(index - 1, index) : 0f;
                float outTangent = index < _keyframes.Count - 1 ? LinearSlopeBetween(index, index + 1) : 0f;

                _keyframes[index] = _keyframes[index].WithTangents(inTangent, outTangent);
            }
        }

        /// <summary>Reports the value of the curve at the given input.</summary>
        /// <returns>The interpolated value, or 0 if the curve has no keyframes.</returns>
        public float Evaluate(float input)
        {
            if (_keyframes.Count == 0)
            {
                return 0f;
            }

            if (input <= _keyframes[0].Input)
            {
                return _keyframes[0].Value;
            }

            CurveKeyframe last = _keyframes[_keyframes.Count - 1];

            if (input >= last.Input)
            {
                return last.Value;
            }

            int segment = 0;

            while (_keyframes[segment + 1].Input < input)
            {
                segment++;
            }

            return EvaluateSegment(segment, input);
        }

        /// <summary>The slope of the straight line between two keyframes, or 0 for keyframes at the same input.</summary>
        private float LinearSlopeBetween(int firstIndex, int secondIndex)
        {
            float inputSpan = _keyframes[secondIndex].Input - _keyframes[firstIndex].Input;

            return inputSpan == 0f ? 0f : (_keyframes[secondIndex].Value - _keyframes[firstIndex].Value) / inputSpan;
        }

        /// <summary>
        ///     Evaluates the Hermite polynomial across one segment, blending the values and tangents of the keyframes at its
        ///     ends.
        /// </summary>
        private float EvaluateSegment(int segment, float input)
        {
            CurveKeyframe start = _keyframes[segment];
            CurveKeyframe end = _keyframes[segment + 1];

            float inputSpan = end.Input - start.Input;

            if (inputSpan == 0f)
            {
                return end.Value;
            }

            float position = (input - start.Input) / inputSpan;
            float positionSquared = position * position;
            float positionCubed = positionSquared * position;

            // The four cubic Hermite basis functions, weighting the values and tangents at each end of the segment. See
            // the reference on Curve. The tangents are multiplied by inputSpan below because they are slopes per unit
            // of input, and position runs from 0 to 1 across the segment however wide the segment really is.
            float startValueWeight = 2f * positionCubed - 3f * positionSquared + 1f;
            float startTangentWeight = positionCubed - 2f * positionSquared + position;
            float endValueWeight = -2f * positionCubed + 3f * positionSquared;
            float endTangentWeight = positionCubed - positionSquared;

            return startValueWeight * start.Value + startTangentWeight * inputSpan * start.OutTangent +
                   endValueWeight * end.Value + endTangentWeight * inputSpan * end.InTangent;
        }
    }
}

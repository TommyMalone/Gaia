using System;
using System.Collections.Generic;

namespace Gaia.Core.Curves
{
    /// <summary>
    ///     An arbitrary mapping from one float to another, defined by a set of keyframes and interpolated between them
    ///     with a cubic Hermite spline.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the same shape as an animation curve in an engine editor, and it can describe responses a power
    ///         function of the input cannot. One such response is a value that peaks in the middle of its range and falls away
    ///         at both ends.
    ///     </para>
    ///     <para>
    ///         Inputs below the first keyframe report the first keyframe's value, and inputs above the last report the last,
    ///         so a curve never extrapolates past what it was given.
    ///     </para>
    ///     <para>
    ///         A curve holds one keyframe per input. Two values at one input have no interpolation between them, so a second
    ///         keyframe at an input another already holds is refused rather than resolved.
    ///     </para>
    ///     <para>
    ///         Curves are built in code as well as loaded, so a host or a tool builds one through AddKeyframe, SetKeyframe,
    ///         RemoveKeyframe and SetLinearTangents without a configuration.
    ///     </para>
    /// </remarks>
    /// <seealso>Cubic Hermite splines, described at https://en.wikibooks.org/wiki/Cg_Programming/Unity/Hermite_Curves</seealso>
    public sealed class Curve
    {
        private readonly List<CurveKeyframe> _keyframes = new();

        /// <summary>The keyframes, in ascending order of input.</summary>
        public IReadOnlyList<CurveKeyframe> Keyframes => _keyframes;

        /// <summary>Adds a keyframe, keeping the keyframes ordered by Input.</summary>
        /// <exception cref="ArgumentException">A keyframe already holds the given <see cref="CurveKeyframe.Input" />.</exception>
        public void AddKeyframe(CurveKeyframe keyframe)
        {
            int index = IndexAtOrAfter(keyframe.Input);

            // The walk stops where a keyframe of this input would sit, so the one place a twin can be is here.
            if (index < _keyframes.Count && _keyframes[index].Input == keyframe.Input)
            {
                throw new ArgumentException(
                    $"A curve already holds a keyframe at input {keyframe.Input}. A curve holds one keyframe per input.",
                    nameof(keyframe));
            }

            _keyframes.Insert(index, keyframe);
        }

        /// <summary>Replaces the keyframe at the given keyframe's input, if the curve holds one.</summary>
        /// <remarks>
        ///     The one way to change a keyframe, since a curve's only keyframe cannot be removed and added back. A new value
        ///     changes the neighbors' slopes as a removal does, so a host that wants straight lines runs SetLinearTangents again.
        /// </remarks>
        /// <returns>True if a keyframe was replaced. False when no keyframe holds the input.</returns>
        public bool SetKeyframe(CurveKeyframe keyframe)
        {
            int index = IndexAtOrAfter(keyframe.Input);

            if (index == _keyframes.Count || _keyframes[index].Input != keyframe.Input)
            {
                return false;
            }

            _keyframes[index] = keyframe;
            return true;
        }

        /// <summary>Removes the keyframe at an input, if the curve holds one.</summary>
        /// <remarks>
        ///     <para>
        ///         The last keyframe is never removed, since an empty curve reports 0 for every input and would veto silently
        ///         through a multiplier, which is why loading refuses an empty curve too.
        ///     </para>
        ///     <para>
        ///         Removing a keyframe changes its neighbors' slopes, so tangents SetLinearTangents wrote describe a curve that
        ///         no longer exists. A host that removes and wants straight lines runs it again.
        ///     </para>
        /// </remarks>
        /// <returns>True if a keyframe was removed. False when no keyframe holds the input.</returns>
        /// <exception cref="InvalidOperationException">The keyframe at the input is the curve's last.</exception>
        public bool RemoveKeyframe(float input)
        {
            int index = IndexAtOrAfter(input);

            if (index == _keyframes.Count || _keyframes[index].Input != input)
            {
                return false;
            }

            if (_keyframes.Count == 1)
            {
                throw new InvalidOperationException(
                    $"A curve keeps its last keyframe, the one at input {input}. An empty curve reports 0 for every input.");
            }

            _keyframes.RemoveAt(index);
            return true;
        }

        /// <summary>
        ///     Replaces every tangent with the slope of the neighboring segment, or with 0 where no segment lies on that
        ///     side, turning the curve into a series of straight lines.
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

        /// <summary>Finds the index of the keyframe at an input, or the index one would be inserted at.</summary>
        private int IndexAtOrAfter(float input)
        {
            int index = 0;

            while (index < _keyframes.Count && _keyframes[index].Input < input)
            {
                index++;
            }

            return index;
        }

        /// <summary>The slope of the straight line between two keyframes.</summary>
        private float LinearSlopeBetween(int firstIndex, int secondIndex)
        {
            return (_keyframes[secondIndex].Value - _keyframes[firstIndex].Value) /
                   (_keyframes[secondIndex].Input - _keyframes[firstIndex].Input);
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

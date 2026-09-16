using Gaia.Core.Curves;

namespace Gaia.Core.Tests.Curves;

/// <summary>
///     Covers <see cref="Curve" />, an arbitrary mapping from one float to another defined by keyframes and
///     interpolated between them with a cubic Hermite spline.
/// </summary>
/// <remarks>Most of what is asserted here is float arithmetic, so comparisons carry a tolerance.</remarks>
[TestFixture]
public class CurveSpec
{
    [SetUp]
    public void BuildCurve()
    {
        _curve = new Curve();
    }

    private const float Tolerance = 0.0001f;

    private Curve _curve;

    [Test]
    public void SortsKeyframesByInputWhateverOrderTheyWereAddedIn()
    {
        CurveKeyframe last = new(10f, 10f);
        CurveKeyframe first = new(0f, 0f);
        CurveKeyframe middle = new(5f, 5f);

        _curve.AddKeyframe(last);
        _curve.AddKeyframe(first);
        _curve.AddKeyframe(middle);

        Assert.Multiple(() =>
        {
            Assert.That(_curve.Keyframes[0].Input, Is.EqualTo(0f));
            Assert.That(_curve.Keyframes[1].Input, Is.EqualTo(5f));
            Assert.That(_curve.Keyframes[2].Input, Is.EqualTo(10f));
        });
    }

    [Test]
    public void RefusesASecondKeyframeAtAnInputItAlreadyHolds()
    {
        // Without the refusal the twin stops the walk at its own index and is inserted before it, so the curve
        // reports the second value where the configuration wrote the first.
        CurveKeyframe held = new(5f, 3f);
        CurveKeyframe twin = new(5f, 8f);

        _curve.AddKeyframe(held);

        Assert.Multiple(() =>
        {
            Assert.That(() => _curve.AddKeyframe(twin), Throws.ArgumentException);
            Assert.That(_curve.Keyframes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ReportsZeroWithNoKeyframes()
    {
        Assert.That(_curve.Evaluate(5f), Is.EqualTo(0f));
    }

    [Test]
    public void ReportsItsOnlyKeyframesValueForAnyInput()
    {
        CurveKeyframe only = new(5f, 3f);
        _curve.AddKeyframe(only);

        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(-100f), Is.EqualTo(3f));
            Assert.That(_curve.Evaluate(5f), Is.EqualTo(3f));
            Assert.That(_curve.Evaluate(100f), Is.EqualTo(3f));
        });
    }

    [Test]
    public void ClampsRatherThanExtrapolatingPastItsEnds()
    {
        AddStraightLine();

        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(-50f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_curve.Evaluate(50f), Is.EqualTo(10f).Within(Tolerance));
        });
    }

    [Test]
    public void ReportsAKeyframesOwnValueAtItsInput()
    {
        AddStraightLine();

        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(0f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_curve.Evaluate(10f), Is.EqualTo(10f).Within(Tolerance));
        });
    }

    [Test]
    public void RunsStraightBetweenKeyframesWithLinearTangents()
    {
        AddStraightLine();

        // Tangents matching the slope of the segment leave no curvature at all, so the value tracks the input.
        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(2.5f), Is.EqualTo(2.5f).Within(Tolerance));
            Assert.That(_curve.Evaluate(5f), Is.EqualTo(5f).Within(Tolerance));
            Assert.That(_curve.Evaluate(7.5f), Is.EqualTo(7.5f).Within(Tolerance));
        });
    }

    [Test]
    public void FlattensAtTheKeyframesWithZeroTangents()
    {
        CurveKeyframe start = new(0f, 0f);
        CurveKeyframe end = new(10f, 10f);

        _curve.AddKeyframe(start);
        _curve.AddKeyframe(end);

        // Symmetry still puts the midpoint at 5, so only a sample away from the middle shows the ease in and out.
        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(5f), Is.EqualTo(5f).Within(Tolerance));
            Assert.That(_curve.Evaluate(2.5f), Is.LessThan(2.5f));
            Assert.That(_curve.Evaluate(7.5f), Is.GreaterThan(7.5f));
        });
    }

    [Test]
    public void FollowsAPeakThatFallsAwayAtBothEnds()
    {
        // The response an exponent cannot describe, and the reason to prefer a curve over an exponent.
        CurveKeyframe start = new(0f, 0f);
        CurveKeyframe peak = new(5f, 1f);
        CurveKeyframe end = new(10f, 0f);

        _curve.AddKeyframe(start);
        _curve.AddKeyframe(peak);
        _curve.AddKeyframe(end);

        _curve.SetLinearTangents();

        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(5f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(_curve.Evaluate(2.5f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(_curve.Evaluate(7.5f), Is.EqualTo(0.5f).Within(Tolerance));
        });
    }

    [Test]
    public void SetsLinearTangentsOnACurveWithTooFewKeyframesToHaveASlope()
    {
        Curve emptyCurve = new();

        CurveKeyframe only = new(5f, 3f);
        Curve singleKeyframeCurve = new();
        singleKeyframeCurve.AddKeyframe(only);

        Assert.Multiple(() =>
        {
            Assert.That(() => emptyCurve.SetLinearTangents(), Throws.Nothing);
            Assert.That(() => singleKeyframeCurve.SetLinearTangents(), Throws.Nothing);
        });
    }

    [Test]
    public void ClampsInfiniteInputsToItsEnds()
    {
        AddStraightLine();

        Assert.Multiple(() =>
        {
            Assert.That(_curve.Evaluate(float.NegativeInfinity), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_curve.Evaluate(float.PositiveInfinity), Is.EqualTo(10f).Within(Tolerance));
        });
    }

    [Test]
    public void ReportsNotANumberForAnInputThatIsNotANumber()
    {
        AddStraightLine();

        // No comparison against a NaN is ever true, so it falls past both clamps and out of the segment search on the
        // first segment, and the arithmetic reports NaN in turn. That leaves the option holding this curve unable to
        // be selected, which is silent but safe. The alternative would be substituting a number the configuration never
        // specified.
        Assert.That(_curve.Evaluate(float.NaN), Is.NaN);
    }

    /// <summary>Adds two keyframes running from 0 to 10 with tangents that leave the segment straight.</summary>
    private void AddStraightLine()
    {
        CurveKeyframe start = new(0f, 0f);
        CurveKeyframe end = new(10f, 10f);

        _curve.AddKeyframe(start);
        _curve.AddKeyframe(end);

        _curve.SetLinearTangents();
    }
}

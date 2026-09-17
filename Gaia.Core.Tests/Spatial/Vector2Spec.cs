using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Spatial;

/// <summary>
///     Covers <see cref="Vector2" /> as a value, the two members it has that <see cref="Vector3" /> does not, and the
///     invariant formatting every spatial value now shares.
/// </summary>
/// <remarks>
///     The equality asserts are exact because every value here is chosen to add, scale and divide without rounding.
///     The reading of a Vector2 attribute is covered where the other kinds are, in SpecificationNodeSpec.
/// </remarks>
[TestFixture]
public class Vector2Spec
{
    [Test]
    public void AddsSubtractsScalesAndDivides()
    {
        Vector2 left = new(3f, 4f);
        Vector2 right = new(1f, 2f);

        Assert.Multiple(() =>
        {
            Assert.That(left + right, Is.EqualTo(new Vector2(4f, 6f)));
            Assert.That(left - right, Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(left * 2f, Is.EqualTo(new Vector2(6f, 8f)));
            Assert.That(left / 2f, Is.EqualTo(new Vector2(1.5f, 2f)));
        });
    }

    [Test]
    public void ReportsItsLengthAndTheDistanceToAnother()
    {
        Vector2 threeFourFive = new(3f, 4f);

        Assert.Multiple(() =>
        {
            Assert.That(threeFourFive.Magnitude, Is.EqualTo(5f));
            Assert.That(threeFourFive.SquaredMagnitude, Is.EqualTo(25f));
            Assert.That(Vector2.Distance(Vector2.Zero, threeFourFive), Is.EqualTo(5f));
            Assert.That(Vector2.SquaredDistance(Vector2.Zero, threeFourFive), Is.EqualTo(25f));
        });
    }

    [Test]
    public void HoldsTheEqualityContract()
    {
        Vector2 value = new(1.5f, -2f);
        Vector2 same = new(1.5f, -2f);
        Vector2 other = new(1.5f, 2f);

        Assert.Multiple(() =>
        {
            Assert.That(value.Equals(same), Is.True);
            Assert.That(value == same, Is.True);
            Assert.That(value != other, Is.True);
            Assert.That(value.Equals((object)same), Is.True, "equality survives boxing");
            Assert.That(value.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        });
    }

    [Test]
    public void ReportsTheSignedAreaTwoVectorsSpan()
    {
        Vector2 alongX = new(2f, 0f);
        Vector2 alongY = new(0f, 3f);

        Assert.Multiple(() =>
        {
            Assert.That(Vector2.Cross(alongX, alongY), Is.EqualTo(6f), "counterclockwise reads positive");
            Assert.That(Vector2.Cross(alongY, alongX), Is.EqualTo(-6f), "and the other way round reads negative");
            Assert.That(Vector2.Cross(alongX, alongX), Is.EqualTo(0f), "two vectors along one line span nothing");
        });
    }

    [Test]
    public void TurnsAQuarterTurnAndAgreesWithCross()
    {
        Vector2 edge = new(2f, 1f);
        Vector2 point = new(-1f, 4f);

        Assert.Multiple(() =>
        {
            Assert.That(edge.Perpendicular, Is.EqualTo(new Vector2(-1f, 2f)));
            Assert.That(Vector2.Dot(edge, edge.Perpendicular), Is.EqualTo(0f), "a quarter turn is at right angles");
            Assert.That(Vector2.Dot(edge.Perpendicular, point), Is.EqualTo(Vector2.Cross(edge, point)),
                "which is why a containment test can ask either way");
        });
    }
}

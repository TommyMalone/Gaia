using Gaia.Core.Spatial;

namespace Gaia.Core.Tests.Spatial;

/// <summary>Covers <see cref="Plane" /> as a value, its front side test and its refusal of a zero normal.</summary>
/// <remarks>
///     The equality asserts are exact because construction only divides by the normal's length, and the lengths
///     chosen here divide without rounding.
/// </remarks>
[TestFixture]
public class PlaneSpec
{
    [SetUp]
    public void BuildWall()
    {
        _towardPositiveX = new Vector3(1f, 0f, 0f);
        _wall = new Plane(_towardPositiveX, 10f);
    }

    private Vector3 _towardPositiveX;
    private Plane _wall;

    [Test]
    public void RefusesANormalWithNoLength()
    {
        Assert.That(() => new Plane(Vector3.Zero, 5f), Throws.ArgumentException);
    }

    [Test]
    public void TellsTheFrontFromTheBack()
    {
        Vector3 inFront = new(11f, 0f, 0f);
        Vector3 onThePlane = new(10f, 5f, -3f);
        Vector3 behind = new(9f, 0f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_wall.IsInFront(inFront), Is.True);
            Assert.That(_wall.IsInFront(onThePlane), Is.True, "the plane itself counts as in front");
            Assert.That(_wall.IsInFront(behind), Is.False);
        });
    }

    [Test]
    public void EqualsAnotherPlaneDescribingTheSameSurface()
    {
        // The same wall handed over with a longer normal, which construction scales away.
        Vector3 longerNormal = new(2f, 0f, 0f);
        Plane sameWall = new(longerNormal, 20f);

        Assert.Multiple(() =>
        {
            Assert.That(_wall == sameWall, Is.True);
            Assert.That(_wall != sameWall, Is.False);
        });

        Plane fartherWall = new(_towardPositiveX, 11f);

        Assert.Multiple(() =>
        {
            Assert.That(_wall == fartherWall, Is.False);
            Assert.That(_wall != fartherWall, Is.True);
            Assert.That(_wall.Distance, Is.EqualTo(10f));
            Assert.That(fartherWall.Distance, Is.EqualTo(11f));
        });
    }

    [Test]
    public void EqualsTheSamePlaneArrivingBoxed()
    {
        object boxedSameWall = new Plane(_towardPositiveX, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_wall.Equals(boxedSameWall), Is.True);

            // A value of another type is never equal, whatever numbers it holds.
            Assert.That(_wall.Equals(_towardPositiveX), Is.False);
        });
    }

    [Test]
    public void HashesEqualPlanesAlike()
    {
        Plane sameWall = new(_towardPositiveX, 10f);

        Assert.That(_wall.GetHashCode(), Is.EqualTo(sameWall.GetHashCode()));
    }

    [Test]
    public void PrintsItsNormalAndDistance()
    {
        Assert.That(_wall.ToString(), Is.EqualTo("Normal (1, 0, 0), Distance 10"));
    }
}

using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="PolygonRegion" />.</summary>
/// <remarks>
///     The footprint is an L, since a rectangular footprint would not tell <see cref="PolygonRegionBase" />'s
///     crossing test from a bounding box. It is a 10 by 10 square with its upper right quarter missing.
/// </remarks>
[TestFixture]
public class PolygonRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _logSink = new RecordingLogSink();
        _randomSource = new Xoshiro128RandomSource(12345);

        _polygon = new PolygonRegion { RandomSource = _randomSource };
        RegionSpecHelpers.AddLShapedFootprint(_polygon);
    }

    private RecordingLogSink _logSink;

    private Xoshiro128RandomSource _randomSource;

    private PolygonRegion _polygon;

    [Test]
    public void ContainsWhatIsInsideItsFootprint()
    {
        Vector3 lowerLeftQuarter = new(2f, 0f, 2f);
        Vector3 lowerRightQuarter = new(8f, 0f, 2f);
        Vector3 upperLeftQuarter = new(2f, 0f, 8f);
        Vector3 missingQuarter = new(8f, 0f, 8f);
        Vector3 pastTheLeftSide = new(-1f, 0f, 2f);
        Vector3 pastTheTop = new(2f, 0f, 11f);

        Assert.Multiple(() =>
        {
            Assert.That(_polygon.IsInRegion(lowerLeftQuarter), Is.True);
            Assert.That(_polygon.IsInRegion(lowerRightQuarter), Is.True);
            Assert.That(_polygon.IsInRegion(upperLeftQuarter), Is.True);
            Assert.That(_polygon.IsInRegion(missingQuarter), Is.False, "the missing quarter is outside the L");
            Assert.That(_polygon.IsInRegion(pastTheLeftSide), Is.False);
            Assert.That(_polygon.IsInRegion(pastTheTop), Is.False);
        });
    }

    [Test]
    public void ContainsEveryPointOnItsEdgesAndCorners()
    {
        Assert.Multiple(() =>
        {
            foreach (Vector2 boundaryPoint in RegionSpecHelpers.LShapedFootprintBoundaryPoints)
            {
                Vector3 onTheBoundary = new(boundaryPoint.X, 0f, boundaryPoint.Y);

                Assert.That(_polygon.IsInRegion(onTheBoundary), Is.True, $"{onTheBoundary} is on the boundary");
            }
        });
    }

    [Test]
    public void ContainsEveryPointOnASlantedEdge()
    {
        // (6, 6) is inside the bounds of the slanted edge's corners and beside the edge, so the test fails if the
        // bounds alone decide. The nearest float past the middle is less than a thousandth of a millimeter outside the
        // edge, so it counts as on it. A cross product is the edge's length, about 14, times the distance from the
        // edge. Compared with the tolerance as a distance, it holds a point only within 0.07 millimeters, so the point
        // half a millimeter out fails it.
        float justPastFive = MathF.BitIncrement(5f);
        Vector2 rightAngleCorner = new(0f, 0f);
        Vector2 cornerAlongX = new(10f, 0f);
        Vector2 cornerAlongZ = new(0f, 10f);
        Vector3 nearTheTopOfTheSlantedEdge = new(2f, 0f, 8f);
        Vector3 middleOfTheSlantedEdge = new(5f, 0f, 5f);
        Vector3 nearTheFootOfTheSlantedEdge = new(7.5f, 0f, 2.5f);
        Vector3 besideTheSlantedEdge = new(6f, 0f, 6f);
        Vector3 justPastTheMiddleOfTheSlantedEdge = new(justPastFive, 0f, justPastFive);
        Vector3 outOfTheSlantedEdge = new Vector3(1f, 0f, 1f) / MathF.Sqrt(2f);

        PolygonRegion triangle = new();
        triangle.AddCorner(rightAngleCorner);
        triangle.AddCorner(cornerAlongZ);
        triangle.AddCorner(cornerAlongX);

        Assert.Multiple(() =>
        {
            Assert.That(triangle.IsInRegion(nearTheTopOfTheSlantedEdge), Is.True);
            Assert.That(triangle.IsInRegion(middleOfTheSlantedEdge), Is.True);
            Assert.That(triangle.IsInRegion(nearTheFootOfTheSlantedEdge), Is.True);
            Assert.That(triangle.IsInRegion(besideTheSlantedEdge), Is.False);
            Assert.That(triangle.IsInRegion(justPastTheMiddleOfTheSlantedEdge), Is.True,
                "the nearest float past the middle of the edge");
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(triangle, middleOfTheSlantedEdge,
                outOfTheSlantedEdge);
        });
    }

    [Test]
    public void ContainsNothingPastItsEdges()
    {
        // The L is moved to (100, 0, 100). Each position is 2 millimeters, twice the tolerance, outside the middle of
        // an edge, past a corner along an edge, or into the notch from its inner corner.
        const float twoMillimetersUnder100 = 99.998f;
        const float twoMillimetersOver105 = 105.002f;
        const float twoMillimetersOver110 = 110.002f;
        Vector3[] outsideTheMiddleOfEachEdge =
        {
            new(105f, 0f, twoMillimetersUnder100), new(twoMillimetersOver110, 0f, 102.5f),
            new(107.5f, 0f, twoMillimetersOver105), new(twoMillimetersOver105, 0f, 107.5f),
            new(102.5f, 0f, twoMillimetersOver110), new(twoMillimetersUnder100, 0f, 105f),
        };
        Vector3[] pastACornerAlongEachEdge =
        {
            new(twoMillimetersOver110, 0f, 100f), new(110f, 0f, twoMillimetersOver105),
            new(twoMillimetersOver110, 0f, 105f), new(105f, 0f, twoMillimetersOver110),
            new(twoMillimetersOver105, 0f, 110f), new(100f, 0f, twoMillimetersUnder100),
        };
        Vector3 intoTheNotchFromItsInnerCorner = new(twoMillimetersOver105, 0f, twoMillimetersOver105);

        _polygon.Origin = new Vector3(100f, 0f, 100f);

        Assert.Multiple(() =>
        {
            foreach (Vector3 outsideAnEdge in outsideTheMiddleOfEachEdge)
            {
                Assert.That(_polygon.IsInRegion(outsideAnEdge), Is.False, $"{outsideAnEdge} is outside an edge");
            }

            foreach (Vector3 pastACorner in pastACornerAlongEachEdge)
            {
                Assert.That(_polygon.IsInRegion(pastACorner), Is.False, $"{pastACorner} is past a corner");
            }

            Assert.That(_polygon.IsInRegion(intoTheNotchFromItsInnerCorner), Is.False);
        });
    }

    [Test]
    public void ContainsItsEdgesFarFromTheWorldsOriginOnceTurned()
    {
        IEnumerable<Vector3> onTheBoundary =
            RegionSpecHelpers.LShapedFootprintBoundaryPoints.Select(point => new Vector3(point.X, 0f, point.Y));

        RegionSpecHelpers.AssertItsBoundaryHoldsFarFromTheWorldsOrigin(_polygon, onTheBoundary);
    }

    [Test]
    public void HoldsItsEdgesAndCornersWithinAMillimeter()
    {
        // A position out from the inner corner along the diagonal into the notch is nearer the corner's two edges than
        // the corner, by a factor of the square root of 2. The position half a millimeter out is about 0.35
        // millimeters from both edges and the one two millimeters out about 1.41, still past the tolerance.
        Vector3 middleOfTheBottomEdge = new(5f, 0f, 0f);
        Vector3 middleOfTheRightEdge = new(10f, 0f, 2.5f);
        Vector3 middleOfTheNotchsFloor = new(7.5f, 0f, 5f);
        Vector3 lowerLeftCorner = new(0f, 0f, 0f);
        Vector3 innerCorner = new(5f, 0f, 5f);
        Vector3 down = new(0f, 0f, -1f);
        Vector3 right = new(1f, 0f, 0f);
        Vector3 up = new(0f, 0f, 1f);
        Vector3 downAndLeft = new Vector3(-1f, 0f, -1f) / MathF.Sqrt(2f);
        Vector3 upAndRight = new Vector3(1f, 0f, 1f) / MathF.Sqrt(2f);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_polygon, middleOfTheBottomEdge, down);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_polygon, middleOfTheRightEdge, right);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_polygon, middleOfTheNotchsFloor, up);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_polygon, lowerLeftCorner, downAndLeft);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutButNotTwo(_polygon, innerCorner, upAndRight);
        });
    }

    [TestCase(100f, 100f, 100f)]
    [TestCase(0.01f, 0.01f, 0.01f)]
    [TestCase(8f, 0.5f, 2f)]
    public void HoldsItsEdgesWithinAMillimeterInTheWorldAtAnyScale(float scaleX, float scaleY, float scaleZ)
    {
        // A distance from an edge mixes the plane's two axes, local X and Z, so the tolerance is taken along the one
        // with the smaller scale, Z in each case here, and is 1 mm in the world across the edges along X.
        Vector3 middleOfTheBottomEdge = new(5f, 0f, 0f);
        Vector3 middleOfTheNotchsFloor = new(7.5f, 0f, 5f);
        Vector3 down = new(0f, 0f, -1f);
        Vector3 up = new(0f, 0f, 1f);

        _polygon.Rotation = RegionSpecHelpers.AwkwardTurn;
        _polygon.Scale = new Vector3(scaleX, scaleY, scaleZ);

        Assert.Multiple(() =>
        {
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_polygon, middleOfTheBottomEdge, down);
            RegionSpecHelpers.AssertItHoldsHalfAMillimeterOutInTheWorldButNotTwo(_polygon, middleOfTheNotchsFloor, up);
        });
    }

    [Test]
    public void HoldsAPositionExactlyTheTolerancePastAnEdge()
    {
        // At the world's origin and unturned, a position comes into the footprint's plane unchanged, so this one, out
        // from the middle of the bottom edge, has exactly the squared distance from it that the polygon compares with,
        // the tolerance squared in float, which only an inclusive comparison holds.
        Vector3 exactlyTheTolerancePast = new(5f, 0f, -0.001f);

        Assert.That(_polygon.IsInRegion(exactlyTheTolerancePast), Is.True);
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveLowerLeftQuarter = new(2f, 100f, 2f);

        Assert.That(_polygon.IsInRegion(farAboveLowerLeftQuarter), Is.True);
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_polygon);
    }

    [Test]
    public void TurnsAboutItsOriginWithItsRotation()
    {
        // A quarter turn to the right lays the L along the world's X, between Z of 0 and -10, with the missing quarter
        // now at the far end.
        Vector3 lowerRightQuarterOnceTurned = new(2f, 0f, -8f);
        Vector3 missingQuarterOnceTurned = new(8f, 0f, -8f);
        Vector3 lowerLeftQuarterBeforeTurning = new(2f, 0f, 2f);

        _polygon.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(_polygon.IsInRegion(lowerRightQuarterOnceTurned), Is.True);
            Assert.That(_polygon.IsInRegion(missingQuarterOnceTurned), Is.False, "the missing quarter turns with it");
            Assert.That(_polygon.IsInRegion(lowerLeftQuarterBeforeTurning), Is.False);
        });
    }

    [Test]
    public void SamplesInsideItselfOnceTurned()
    {
        _polygon.Rotation = Rotation.FromEulerAngles(0f, 90f, 0f);

        RegionSpecHelpers.AssertEverySampleLandsInside(_polygon);
    }

    [Test]
    public void ContainsNothingWithFewerThanThreeCorners()
    {
        Vector2 lineStart = new(0f, 0f);
        Vector2 lineEnd = new(10f, 0f);
        Vector3 onTheLine = new(5f, 0f, 0f);

        PolygonRegion line = new() { RandomSource = _randomSource };
        line.AddCorner(lineStart);
        line.AddCorner(lineEnd);

        Assert.Multiple(() =>
        {
            Assert.That(line.IsInRegion(onTheLine), Is.False);
            Assert.That(line.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void ContainsTheSegmentOfCornersInALine()
    {
        // Every guess inside the bounds of three corners along X lies on their line, so the first guess is kept.
        Vector2 lineStart = new(0f, 0f);
        Vector2 lineMiddle = new(5f, 0f);
        Vector2 lineEnd = new(10f, 0f);
        Vector3 onTheLine = new(2f, 0f, 0f);
        Vector3 atTheEndOfTheLine = new(10f, 0f, 0f);
        Vector3 besideTheLine = new(2f, 0f, 1f);
        Vector3 pastTheEndOfTheLine = new(11f, 0f, 0f);
        Vector3 firstGuess = new(5.3889074f, 0f, 0f);

        PolygonRegion line = new() { RandomSource = _randomSource };
        line.AddCorner(lineStart);
        line.AddCorner(lineMiddle);
        line.AddCorner(lineEnd);

        bool wasSampled = line.TryGetRandomPosition(out Vector3 sample);

        Assert.Multiple(() =>
        {
            Assert.That(line.IsInRegion(onTheLine), Is.True);
            Assert.That(line.IsInRegion(atTheEndOfTheLine), Is.True);
            Assert.That(line.IsInRegion(besideTheLine), Is.False);
            Assert.That(line.IsInRegion(pastTheEndOfTheLine), Is.False);
            Assert.That(wasSampled, Is.True);
            Assert.That(sample, Is.EqualTo(firstGuess));
            Assert.That(line.IsInRegion(sample), Is.True);
        });
    }

    [Test]
    public void ContainsThePlaceOfCornersAllAtOnePlace()
    {
        // Every edge between corners at one place has no length, so its nearest point is that place. Measured along
        // an edge of no length, the fraction would be zero over zero, which is not a number, and nothing would be held.
        Vector2 onePlace = new(2f, 3f);
        Vector3 atThatPlace = new(2f, 0f, 3f);
        Vector3 twoMillimetersAway = new(2.002f, 0f, 3f);

        PolygonRegion point = new();
        point.AddCorner(onePlace);
        point.AddCorner(onePlace);
        point.AddCorner(onePlace);

        Assert.Multiple(() =>
        {
            Assert.That(point.IsInRegion(atThatPlace), Is.True);
            Assert.That(point.IsInRegion(twoMillimetersAway), Is.False);
        });
    }

    [Test]
    public void LoadsItsCornersFromCornerElements()
    {
        const string snippet = """
                               <Region Type="Polygon">
                                   <Corner Position="(0, 0)"/>
                                   <Corner Position="(10, 0)"/>
                                   <Corner Position="(0, 10)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        PolygonRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Corners, Has.Count.EqualTo(3));
            Assert.That(loaded.IsInRegion(new Vector3(2f, 0f, 2f)), Is.True);
            Assert.That(loaded.IsInRegion(new Vector3(9f, 0f, 9f)), Is.False, "beyond the diagonal edge");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAFootprintWithoutArea()
    {
        const string snippet = """
                               <Region Type="Polygon">
                                   <Corner Position="(0, 0)"/>
                                   <Corner Position="(10, 0)"/>
                               </Region>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the short slot after Init returns true, so the test loads through the factory rather than
        // calling Init.
        bool wasCreated = creationData.Factories.Regions.TryCreate(creationData, out RegionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has fewer than 3 Corner elements",
            }));
        });
    }
}

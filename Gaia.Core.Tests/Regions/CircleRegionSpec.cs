using Gaia.Core.Factories;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Regions;

/// <summary>Covers <see cref="CircleRegion" />.</summary>
[TestFixture]
public class CircleRegionSpec
{
    [SetUp]
    public void BuildRegion()
    {
        _randomSource = new SystemRandomSource(12345);

        _circle = new CircleRegion
        {
            Center = new Vector3(10f, 0f, 10f),
            Radius = 5f,
            RandomSource = _randomSource,
        };
    }

    private SystemRandomSource _randomSource;

    private CircleRegion _circle;

    [Test]
    public void ContainsWhatIsWithinItsRadius()
    {
        Vector3 center = new(10f, 0f, 10f);
        Vector3 insideNearEdge = new(14f, 0f, 10f);
        Vector3 onEdge = new(15f, 0f, 10f);
        Vector3 justOutside = new(16f, 0f, 10f);
        Vector3 boundingSquareCorner = new(14f, 0f, 14f);

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(center), Is.True);
            Assert.That(_circle.IsInRegion(insideNearEdge), Is.True);
            Assert.That(_circle.IsInRegion(onEdge), Is.True, "the edge counts as inside");
            Assert.That(_circle.IsInRegion(justOutside), Is.False);
            Assert.That(_circle.IsInRegion(boundingSquareCorner), Is.False, "the bounding square's corner is outside");
        });
    }

    [Test]
    public void IgnoresHeight()
    {
        Vector3 farAboveCenter = new(10f, 100f, 10f);
        Vector3 farBelowCenter = new(10f, -100f, 10f);

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(farAboveCenter), Is.True);
            Assert.That(_circle.IsInRegion(farBelowCenter), Is.True);
        });
    }

    [Test]
    public void SamplesInsideItself()
    {
        RegionSpecHelpers.AssertEverySampleLandsInside(_circle);
    }

    [Test]
    public void ReportsASampleAtTheCentersHeight()
    {
        _circle.Center = new Vector3(10f, 7f, 10f);

        _circle.TryGetRandomPosition(out Vector3 position);

        Assert.That(position.Y, Is.EqualTo(7f));
    }

    [Test]
    public void FollowsItsCenterTarget()
    {
        Vector3 initialCenter = new(10f, 0f, 10f);
        Vector3 nearFirstTargetPosition = new(102f, 0f, 100f);
        Vector3 nearSecondTargetPosition = new(202f, 0f, 200f);

        PositionTarget target = new() { Position = new Vector3(100f, 0f, 100f) };
        _circle.CenterTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(initialCenter), Is.False, "the initial center no longer applies");
            Assert.That(_circle.IsInRegion(nearFirstTargetPosition), Is.True);
        });

        target.Position = new Vector3(200f, 0f, 200f);

        Assert.That(_circle.IsInRegion(nearSecondTargetPosition), Is.True);
    }

    [Test]
    public void ContainsNothingWhenItsCenterTargetHasNoPosition()
    {
        Vector3 initialCenter = new(10f, 0f, 10f);

        SelfTarget target = new();
        _circle.CenterTarget = target;

        Assert.Multiple(() =>
        {
            Assert.That(_circle.IsInRegion(initialCenter), Is.False);
            Assert.That(_circle.TryGetRandomPosition(out _), Is.False);
        });
    }

    [Test]
    public void LoadsItsGeometryAndCenterTarget()
    {
        const string snippet = """
                               <Region Type="Circle" Center="(0, 0, 0)" Radius="5">
                                   <CenterTarget Type="Position" Position="(100, 0, 100)"/>
                               </Region>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        CircleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Radius, Is.EqualTo(5f));
            Assert.That(loaded.CenterTarget, Is.InstanceOf<PositionTarget>());
            Assert.That(loaded.IsInRegion(new Vector3(102f, 0f, 102f)), Is.True,
                "containment follows the loaded center target rather than the fixed center");
            Assert.That(loaded.IsInRegion(new Vector3(2f, 0f, 2f)), Is.False);
            Assert.That(logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesANodeMissingItsRadius()
    {
        const string snippet = """
                               <Region Type="Circle" Center="(0, 0, 0)"/>
                               """;
        RecordingLogSink logSink = new();
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

        CircleRegion loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Region element has no Radius attribute",
            }));
        });
    }
}

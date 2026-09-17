using Gaia.Core.Considerations;
using Gaia.Core.Curves;
using Gaia.Core.Entities;
using Gaia.Core.Regions;
using Gaia.Core.Services.Random;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;

namespace Gaia.Core.HostSpecs;

/// <summary>Proves that game code can build regions, targets and curves and query them through the public surface alone.</summary>
/// <remarks>
///     A region is queried by the host, not by the library. Nothing here fills a <see cref="Contact" /> list, so a
///     host tests each contact against a region itself and writes the ones inside to the blackboard entity list a
///     configuration's <see cref="EntityExistsConsideration" /> reads, which is what the public surface is for.
/// </remarks>
[TestFixture]
public class GeometryHostSpec
{
    [Test]
    public void BuildsARegionAndSortsPositionsWithIt()
    {
        CircleRegion killZone = new()
        {
            Origin = new Vector3(0f, 0f, 150f),
            Radius = 40f,
        };

        Assert.Multiple(() =>
        {
            Assert.That(killZone.IsInRegion(new Vector3(10f, 0f, 140f)), Is.True);
            Assert.That(killZone.IsInRegion(new Vector3(0f, 0f, 0f)), Is.False);
        });
    }

    [Test]
    public void SamplesAPositionFromAHandBuiltRegion()
    {
        BoxRegion spawnArea = new()
        {
            Min = new Vector3(-5f, 0f, -5f),
            Max = new Vector3(5f, 2f, 5f),
            RandomSource = new Xoshiro128RandomSource(7),
        };

        Assert.Multiple(() =>
        {
            Assert.That(spawnArea.TryGetRandomPosition(out Vector3 position), Is.True);
            Assert.That(spawnArea.IsInRegion(position), Is.True, "what the region hands out is inside it");
        });
    }

    [Test]
    public void TurnsAHandBuiltRegion()
    {
        // A corridor that runs along the world's X is a rectangle turned a quarter turn, which a host writes as a
        // rotation rather than by working out the turned corners.
        RectangleRegion corridor = new()
        {
            Origin = new Vector3(0f, 0f, 0f),
            Rotation = Rotation.FromEulerAngles(0f, 90f, 0f),
            Min = new Vector2(-2f, 0f),
            Max = new Vector2(2f, 50f),
        };

        Assert.Multiple(() =>
        {
            Assert.That(corridor.IsInRegion(new Vector3(25f, 0f, 0f)), Is.True, "along the world's X once turned");
            Assert.That(corridor.IsInRegion(new Vector3(0f, 0f, 25f)), Is.False, "where it would lie unturned");
        });
    }

    [Test]
    public void BuildsABoxFromItsCenterAndExtents()
    {
        // A spawn volume is usually given as a point and a half size, so a host builds one that way rather than
        // working out the corners itself.
        BoxRegion spawnArea = new(new Vector3(0f, 1f, 0f), new Vector3(5f, 1f, 5f));

        Assert.Multiple(() =>
        {
            Assert.That(spawnArea.Min, Is.EqualTo(new Vector3(-5f, 0f, -5f)));
            Assert.That(spawnArea.Max, Is.EqualTo(new Vector3(5f, 2f, 5f)));
        });
    }

    [Test]
    public void PointsATargetAndReadsItBack()
    {
        Vector3 vantagePosition = new(3f, 10f, 4f);

        PositionTarget vantage = new() { Position = vantagePosition };

        Assert.Multiple(() =>
        {
            Assert.That(vantage.TryGetPosition(out Vector3 position), Is.True);
            Assert.That(position, Is.EqualTo(vantagePosition));
        });
    }

    [Test]
    public void BuildsACurveByHand()
    {
        Curve response = new();
        response.AddKeyframe(new CurveKeyframe(0f, 0f));
        response.AddKeyframe(new CurveKeyframe(1f, 2f));
        response.SetLinearTangents();

        Assert.That(response.Evaluate(0.5f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ReshapesACurveByHand()
    {
        // Raising the end and taking out the peak leaves a straight line from 0 to 4, once the tangents are set
        // again, which is how a tuning tool moves a response around without rebuilding it.
        Curve response = new();
        response.AddKeyframe(new CurveKeyframe(0f, 0f));
        response.AddKeyframe(new CurveKeyframe(1f, 5f));
        response.AddKeyframe(new CurveKeyframe(2f, 2f));
        response.SetLinearTangents();

        bool raised = response.SetKeyframe(new CurveKeyframe(2f, 4f));
        bool removed = response.RemoveKeyframe(1f);
        response.SetLinearTangents();

        Assert.Multiple(() =>
        {
            Assert.That(raised, Is.True);
            Assert.That(removed, Is.True);
            Assert.That(response.Keyframes, Has.Count.EqualTo(2));
            Assert.That(response.Evaluate(1f), Is.EqualTo(2f).Within(0.0001f));
        });
    }
}

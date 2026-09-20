using Gaia.Core.Considerations;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>Covers <see cref="DistanceConsideration" />.</summary>
/// <remarks>
///     The weight function is stubbed, so what is asserted is the distance that reached it. One end is a
///     <see cref="Targets.SelfTarget" /> on an actor and the other a <see cref="Targets.PositionTarget" />, since those
///     are the two ends the sniper's configuration in sniper.xml uses.
/// </remarks>
[TestFixture]
public class DistanceConsiderationSpec
{
    [SetUp]
    public void BuildConsideration()
    {
        _logSink = new RecordingLogSink();
        _reportedWeights = new WeightValues(3f, 0.5f, 2f);
        _missingWeights = WeightValues.Vetoed;

        _weightFunction = new RecordingWeightFunction { Result = _reportedWeights };

        _self = new Actor
        {
            Name = "Sniper",
            Position = new Vector3(0f, 0f, 0f),
        };
        _selfTarget = new SelfTarget { Self = _self };
        _positionTarget = new PositionTarget { Position = new Vector3(3f, 0f, 4f) };

        _consideration = new DistanceConsideration
        {
            FromTarget = _selfTarget,
            ToTarget = _positionTarget,
            WeightFunction = _weightFunction,
            MissingTargetWeights = _missingWeights,
        };
    }

    private RecordingLogSink _logSink;

    private WeightValues _reportedWeights;
    private WeightValues _missingWeights;

    private RecordingWeightFunction _weightFunction;

    private Actor _self;
    private SelfTarget _selfTarget;
    private PositionTarget _positionTarget;

    private DistanceConsideration _consideration;

    [Test]
    public void HandsTheDistanceBetweenItsTargetsToTheWeightFunction()
    {
        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastFloat, Is.EqualTo(5f));
            Assert.That(_consideration.Weights, Is.EqualTo(_reportedWeights));
        });
    }

    [Test]
    public void MeasuresThroughTheAirByDefault()
    {
        _positionTarget.Position = new Vector3(0f, 12f, 5f);

        _consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(13f));
    }

    [Test]
    public void MeasuresAlongTheGroundWhenToldToIgnoreHeight()
    {
        // Measured along the ground, a sniper on a roof is no farther from the kill zone than one at street level.
        _positionTarget.Position = new Vector3(0f, 12f, 5f);
        _consideration.IgnoreHeight = true;

        _consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(5f));
    }

    [Test]
    public void MeasuresAgainEveryCycle()
    {
        _consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(5f));

        _self.Position = new Vector3(3f, 0f, 0f);

        _consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(4f));
    }

    [Test]
    public void ReportsItsMissingValuesWhenATargetCannotGiveAPosition()
    {
        _selfTarget.Self = null;

        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_consideration.Weights, Is.EqualTo(_missingWeights));
            Assert.That(_weightFunction.CalculationCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ReportsItsMissingValuesWithNoTargetConfigured()
    {
        _consideration.ToTarget = null;

        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_missingWeights));
    }

    [Test]
    public void ReportsNeutralValuesForAMissingTargetByDefault()
    {
        DistanceConsideration consideration = new()
        {
            FromTarget = _selfTarget,
            WeightFunction = _weightFunction,
        };

        consideration.Calculate();

        Assert.That(consideration.Weights, Is.EqualTo(WeightValues.Neutral));
    }

    [Test]
    public void WorksWithAByNameTargetAtEitherEnd()
    {
        // The end that tracks a contact is a ByNameTarget on the contact list, since a ByNameTarget is the one target
        // that finds an entity in a list.
        StubEntity lookout = new("Lookout") { Position = new Vector3(0f, 0f, 50f) };
        List<EntityBase> contacts = [lookout];

        StubDataStore store = new();
        store.SetEntityList("Contacts", contacts);

        ByNameTarget lookoutTarget = new()
        {
            DataStore = store,
            ListName = "Contacts",
            EntityName = "Lookout",
        };

        _consideration.ToTarget = lookoutTarget;

        _consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(50f));
    }

    [Test]
    public void ServesTheSnipersRangeBand()
    {
        // Listing 8.11 of the chapter cited on DistanceConsideration vetoes below 50 m and above 300 m, allows in
        // between. This is the consideration and a shipped weight function together, as a configuration would combine
        // them.
        FloatSequenceWeightFunction rangeBand = new(WeightValues.Vetoed);
        FloatSequenceEntry nearEdge = new(50f, WeightValues.Vetoed);
        FloatSequenceEntry farEdge = new(300f, WeightValues.Neutral);
        rangeBand.AddEntry(nearEdge);
        rangeBand.AddEntry(farEdge);

        _consideration.WeightFunction = rangeBand;

        _positionTarget.Position = new Vector3(0f, 0f, 25f);
        _consideration.Calculate();
        WeightValues tooClose = _consideration.Weights;

        _positionTarget.Position = new Vector3(0f, 0f, 150f);
        _consideration.Calculate();
        WeightValues inRange = _consideration.Weights;

        _positionTarget.Position = new Vector3(0f, 0f, 400f);
        _consideration.Calculate();
        WeightValues tooFar = _consideration.Weights;

        Assert.Multiple(() =>
        {
            Assert.That(tooClose, Is.EqualTo(WeightValues.Vetoed));
            Assert.That(inRange, Is.EqualTo(WeightValues.Neutral));
            Assert.That(tooFar, Is.EqualTo(WeightValues.Vetoed));
        });
    }

    [Test]
    public void LoadsItsEndsAndWeightFunction()
    {
        const string snippet = """
                               <Consideration Type="Distance" IgnoreHeight="true">
                                   <From Type="Position" Position="(0, 50, 0)"/>
                                   <To Type="Position" Position="(0, 0, 100)"/>
                                   <Missing Multiplier="0"/>
                                   <WeightFunction Type="FloatSequence">
                                       <Entry Threshold="50" Multiplier="0"/>
                                       <Entry Threshold="300" Addend="1"/>
                                       <Default Multiplier="0"/>
                                   </WeightFunction>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        DistanceConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.MissingTargetWeights, Is.EqualTo(WeightValues.Vetoed));

            loaded.Calculate();

            // With height ignored, the measured distance is 100, inside the loaded 50 to 300 band.
            Assert.That(loaded.Weights, Is.EqualTo(new WeightValues(1f, 1f, float.MinValue)));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesANodeMissingAnEnd()
    {
        const string snippet = """
                               <Consideration Type="Distance">
                                   <From Type="Position" Position="(0, 0, 0)"/>
                                   <WeightFunction Type="Constant"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        DistanceConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Consideration element needs both a From and a To element",
            }));
        });
    }
}

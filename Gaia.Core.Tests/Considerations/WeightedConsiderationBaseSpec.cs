using Gaia.Core.Considerations;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>Covers <see cref="WeightedConsiderationBase" />.</summary>
[TestFixture]
public class WeightedConsiderationBaseSpec
{
    [SetUp]
    public void BuildConsideration()
    {
        _reportedWeights = new WeightValues(3f, 0.5f, 2f);

        _weightFunction = new RecordingWeightFunction { Result = _reportedWeights };

        _consideration = new StubWeightedConsideration
        {
            WeightFunction = _weightFunction,
            QueryResult = 7f,
        };
        _bareConsideration = new StubWeightedConsideration();
    }

    private WeightValues _reportedWeights;

    private RecordingWeightFunction _weightFunction;

    private StubWeightedConsideration _consideration;
    private StubWeightedConsideration _bareConsideration;

    [Test]
    public void ReportsWhatItsWeightFunctionReturned()
    {
        _consideration.Calculate();

        Assert.That(_consideration.Weights, Is.EqualTo(_reportedWeights));
    }

    [Test]
    public void HandsItsQueryResultToTheWeightFunction()
    {
        _consideration.Calculate();

        Assert.That(_weightFunction.LastFloat, Is.EqualTo(7f));
    }

    [Test]
    public void ReportsNeutralValuesWithNoWeightFunction()
    {
        _bareConsideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_bareConsideration.Weights, Is.EqualTo(WeightValues.Neutral));

            // A consideration with no weight function has nowhere to send a measurement, so CalculateWeights is never
            // reached.
            Assert.That(_bareConsideration.CalculateWeightsCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void PassesSelectionAlongToItsWeightFunction()
    {
        _consideration.Select();
        _consideration.Deselect();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.SelectCount, Is.EqualTo(1));
            Assert.That(_weightFunction.DeselectCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void TracksItsOwnSelectionState()
    {
        _consideration.Select();

        Assert.That(_consideration.IsSelected, Is.True);

        _consideration.Deselect();

        Assert.That(_consideration.IsSelected, Is.False);
    }

    [Test]
    public void SelectsAndDeselectsWithNoWeightFunction()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => _bareConsideration.Select(), Throws.Nothing);
            Assert.That(() => _bareConsideration.Deselect(), Throws.Nothing);
        });
    }
}

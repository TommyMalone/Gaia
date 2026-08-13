using Gaia.Core.Considerations;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.Tests.Options;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>
///     Covers how a <see cref="ConsiderationSetConsideration" /> combines what it groups and passes the lifecycle
///     down.
/// </summary>
/// <remarks>
///     The grouped considerations are stubs, so combining is asserted on values set by hand, and selection is
///     observed through the <see cref="ConsiderationBase.IsSelected" /> every consideration carries. That an option handed
///     to the group reaches the considerations inside it is covered in <see cref="OptionBaseSpec" />.
/// </remarks>
[TestFixture]
public class ConsiderationSetConsiderationSpec
{
    [SetUp]
    public void BuildGroup()
    {
        WeightValues firstReport = new(2f, 0.5f, 1f);
        WeightValues secondReport = new(3f, 0.5f, 4f);

        _firstInner = new StubConsideration(firstReport);
        _secondInner = new StubConsideration(secondReport);

        _group = new ConsiderationSetConsideration([_firstInner, _secondInner]);
    }

    private StubConsideration _firstInner;
    private StubConsideration _secondInner;

    private ConsiderationSetConsideration _group;

    [Test]
    public void CombinesWhatItsGroupedConsiderationsReport()
    {
        // The addends are summed, the multipliers multiplied, and the highest rank taken.
        WeightValues combined = new(5f, 0.25f, 4f);

        _group.Calculate();

        Assert.That(_group.Weights, Is.EqualTo(combined));
    }

    [Test]
    public void LetsOneGroupedConsiderationVetoTheWholeGroup()
    {
        _secondInner.Veto();

        _group.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_group.Weights.Multiplier, Is.EqualTo(0f));
            Assert.That(_group.Weights.Addend, Is.EqualTo(2f), "the other consideration still reports through");
        });
    }

    [Test]
    public void CombinesFreshValuesEveryCycle()
    {
        WeightValues raisedReport = new(7f, 0.5f, 1f);
        WeightValues recombined = new(10f, 0.25f, 4f);

        _group.Calculate();

        _firstInner.Set(raisedReport);

        _group.Calculate();

        Assert.That(_group.Weights, Is.EqualTo(recombined));
    }

    [Test]
    public void ContributesNothingWithAnEmptyGroup()
    {
        ConsiderationSetConsideration emptyGroup = new([]);

        emptyGroup.Calculate();

        Assert.That(emptyGroup.Weights, Is.EqualTo(WeightValues.Neutral));
    }

    [Test]
    public void PassesSelectionDownIntoTheGroup()
    {
        _group.Select();

        Assert.Multiple(() =>
        {
            Assert.That(_group.IsSelected, Is.True);
            Assert.That(_firstInner.IsSelected, Is.True);
            Assert.That(_secondInner.IsSelected, Is.True);
        });
    }

    [Test]
    public void PassesDeselectionDownIntoTheGroup()
    {
        _group.Select();
        _group.Deselect();

        Assert.Multiple(() =>
        {
            Assert.That(_group.IsSelected, Is.False);
            Assert.That(_firstInner.IsSelected, Is.False);
            Assert.That(_secondInner.IsSelected, Is.False);
        });
    }
}

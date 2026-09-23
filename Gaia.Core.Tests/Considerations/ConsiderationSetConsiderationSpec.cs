using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.Tests.Options;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>
///     Covers how a <see cref="ConsiderationSetConsideration" /> combines what it groups, passes the lifecycle down
///     and reports the considerations it groups.
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
        _logSink = new RecordingLogSink();
        WeightValues firstReport = new(2f, 0.5f, 1f);
        WeightValues secondReport = new(3f, 0.5f, 4f);

        _firstInner = new StubConsideration(firstReport);
        _secondInner = new StubConsideration(secondReport);

        _group = new ConsiderationSetConsideration([_firstInner, _secondInner]);
    }

    private RecordingLogSink _logSink;

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

    [Test]
    public void LoadsItsNestedConsiderations()
    {
        const string snippet = """
                               <Consideration Type="ConsiderationSet">
                                   <Consideration Type="Tuning" Addend="1"/>
                                   <Consideration Type="Tuning" Addend="2" Rank="3"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationSetConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            loaded.Calculate();

            Assert.That(loaded.Weights, Is.EqualTo(new WeightValues(3f, 1f, 3f)),
                "the addends sum and the highest rank is taken");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void ReportsTheConsiderationsItGroupsInTheOrderTheyWereLoaded()
    {
        // Each child's addend is its place in the document, so the addends read back show the order the considerations
        // are reported in.
        const string snippet = """
                               <Consideration Type="ConsiderationSet">
                                   <Consideration Type="Tuning" Addend="1"/>
                                   <Consideration Type="Tuning" Addend="2"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        ConsiderationSetConsideration loaded = new();

        Assert.That(loaded.Init(creationData), Is.True);

        List<float> addends = new();

        foreach (ConsiderationBase consideration in loaded.Considerations)
        {
            addends.Add(consideration.Weights.Addend);
        }

        Assert.That(addends, Is.EqualTo(new[] { 1f, 2f }));
    }
}

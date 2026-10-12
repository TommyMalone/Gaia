using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>
///     Covers the order in which <see cref="GroupConsiderationBase" /> calculates, combines and loads for a group
///     deriving it, and what it reports and hands the group's rule.
/// </summary>
/// <remarks>
///     The group is a <see cref="RecordingGroupConsideration" />, which records what it saw when the base asked it.
///     That the shipped group, rebuilt on the base, combines, selects and loads as before is covered in
///     <see cref="ConsiderationSetConsiderationSpec" />.
/// </remarks>
[TestFixture]
public class GroupConsiderationBaseSpec
{
    [SetUp]
    public void BuildSink()
    {
        _logSink = new RecordingLogSink();
    }

    private static readonly WeightValues FirstReport = new(2f, 0.5f, 1f);
    private static readonly WeightValues SecondReport = new(3f, 0.5f, 4f);

    private RecordingLogSink _logSink;

    [Test]
    public void CalculatesWhatItHoldsBeforeAskingHowToCombine()
    {
        CountingConsideration counting = new();
        RecordingGroupConsideration group = new([counting]);

        group.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(counting.Weights.Addend, Is.EqualTo(1f), "the held consideration calculated once");
            Assert.That(group.FirstAddendAtCombine, Is.EqualTo(1f),
                "the held consideration had calculated when the group was asked how to combine");
        });
    }

    [Test]
    public void ReportsWhatCombineWeightsAnswers()
    {
        WeightValues answer = new(7f, 0.5f, 2f);
        StubConsideration first = new(FirstReport);
        StubConsideration second = new(SecondReport);
        RecordingGroupConsideration group = new([first, second]) { Answer = answer };

        group.Calculate();

        Assert.That(group.Weights, Is.EqualTo(answer));
    }

    [Test]
    public void HandsARuleTheStandardTotals()
    {
        // The addends are summed, the multipliers multiplied, and the highest rank taken.
        WeightValues standardTotals = new(5f, 0.25f, 4f);
        StubConsideration first = new(FirstReport);
        StubConsideration second = new(SecondReport);
        RecordingGroupConsideration group = new([first, second]);

        group.Calculate();

        Assert.That(group.TotalsAtCombine, Is.EqualTo(standardTotals));
    }

    [Test]
    public void LoadsWhatItHoldsBeforeItsOwnConfiguration()
    {
        const string snippet = """
                               <Consideration Type="RecordingGroup">
                                   <Consideration Type="Tuning" Addend="1"/>
                                   <Consideration Type="Tuning" Addend="2"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RecordingGroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.CountAtGroupInit, Is.EqualTo(2), "both held considerations were in when the hook ran");
        });
    }

    [Test]
    public void RefusesWhenItsOwnConfigurationFails()
    {
        const string snippet = """
                               <Consideration Type="RecordingGroup">
                                   <Consideration Type="Tuning" Addend="1"/>
                                   <Consideration Type="Tuning" Addend="2"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RecordingGroupConsideration loaded = new() { GroupInitAnswer = false };

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(loaded.CountAtGroupInit, Is.EqualTo(2),
                "both held considerations loaded before the hook refused");
        });
    }

    [Test]
    public void SkipsItsOwnConfigurationWhenAHeldConsiderationFailsToLoad()
    {
        const string snippet = """
                               <Consideration Type="RecordingGroup">
                                   <Consideration Type="Unregistered"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        RecordingGroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Consideration element's type Unregistered is not registered",
            }));
            Assert.That(loaded.CountAtGroupInit, Is.EqualTo(-1), "the hook never ran");
        });
    }

    /// <summary>A consideration whose Calculate adds 1 to its addend, so a reader can tell whether it has calculated.</summary>
    private sealed class CountingConsideration : ConsiderationBase
    {
        /// <summary>Adds 1 to the addend.</summary>
        public override void Calculate()
        {
            Weights = new WeightValues(Weights.Addend + 1f, Weights.Multiplier, Weights.Rank);
        }
    }
}

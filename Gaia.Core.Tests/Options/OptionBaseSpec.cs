using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Options;

/// <summary>
///     Covers how the considerations an option takes learn which option they belong to, and the lists an option
///     reports what it holds through.
/// </summary>
/// <remarks>
///     The rest of <see cref="OptionBase" />, combining, screening and the lifecycle, is covered through the reasoner
///     specs. What is asserted here is the Option back-reference a consideration needs when it evaluates its own option
///     rather than the world, and that Considerations and Actions report what was added to the option, in order, which is
///     the walk a reader outside the library makes down a loaded tree.
/// </remarks>
[TestFixture]
public class OptionBaseSpec
{
    [SetUp]
    public void BuildOption()
    {
        _consideration = new StubConsideration(WeightValues.Neutral);
        _option = new StubOption([_consideration], []);
    }

    private StubConsideration _consideration;
    private StubOption _option;

    [Test]
    public void TellsAConsiderationWhichOptionItBelongsTo()
    {
        Assert.That(_consideration.Option, Is.SameAs(_option));
    }

    [Test]
    public void TellsAConsiderationAddedLaterAsWell()
    {
        StubConsideration lateConsideration = new(WeightValues.Neutral);

        _option.AddConsideration(lateConsideration);

        Assert.That(lateConsideration.Option, Is.SameAs(_option));
    }

    [Test]
    public void TellsTheConsiderationsInsideANestedGroup()
    {
        StubConsideration innerConsideration = new(WeightValues.Neutral);
        GroupConsideration nestedGroup = new([innerConsideration]);

        _option.AddConsideration(nestedGroup);

        Assert.Multiple(() =>
        {
            Assert.That(nestedGroup.Option, Is.SameAs(_option));
            Assert.That(innerConsideration.Option, Is.SameAs(_option));
        });
    }

    [Test]
    public void SetsTheOptionBeforeOnAttachRuns()
    {
        AttachRecordingConsideration recorder = new();

        _option.AddConsideration(recorder);

        Assert.That(recorder.OptionWhenAttached, Is.SameAs(_option));
    }

    [Test]
    public void LeavesAConsiderationNoOptionUntilOneTakesIt()
    {
        StubConsideration looseConsideration = new(WeightValues.Neutral);

        Assert.That(looseConsideration.Option, Is.Null);
    }

    [Test]
    public void ReportsTheConsiderationsThatScoreItInTheOrderTheyWereAdded()
    {
        // A neutral tuning consideration of its own keeps the option from being given the default one, so the set
        // holds exactly what this test added.
        TuningConsideration neutralTuning = new(WeightValues.Neutral);
        StubConsideration score = new(WeightValues.Neutral);
        StubOption option = new([score, neutralTuning], []);

        Assert.That(option.Considerations, Is.EqualTo(new ConsiderationBase[] { score, neutralTuning }));
    }

    [Test]
    public void ReportsTheActionsItRunsInTheOrderTheyWereAdded()
    {
        List<string> executed = new();
        RecordAction firstAction = new("First", executed, false);
        RecordAction secondAction = new("Second", executed, false);
        StubOption option = new([], [firstAction, secondAction]);

        Assert.That(option.Actions, Is.EqualTo(new ActionBase[] { firstAction, secondAction }));
    }

    /// <summary>A consideration that records its own Option at the moment OnAttach runs.</summary>
    private sealed class AttachRecordingConsideration : ConsiderationBase
    {
        /// <summary>The Option this consideration read when OnAttach ran, or null until it has run.</summary>
        internal OptionBase? OptionWhenAttached { get; private set; }

        /// <inheritdoc />
        public override void Calculate()
        {
            Weights = WeightValues.Neutral;
        }

        /// <summary>Records Option as it stands when the hook runs.</summary>
        protected override void OnAttach(OptionBase option)
        {
            OptionWhenAttached = Option;
        }
    }
}

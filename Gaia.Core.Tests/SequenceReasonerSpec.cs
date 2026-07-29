using Gaia.Core.Reasoner;
using Gaia.Core.Temp;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests;

/// <summary>
///     Covers SequenceReasoner, which runs its options one at a time in the order they were added and moves on when
///     the current one reports that it is done. It behaves much like a sequence node in a behavior tree. Unlike the
///     other reasoners it always executes every option, so the weights their considerations report play no part in
///     which option runs.
/// </summary>
/// <remarks>
///     The fixture is three options, each holding a single <see cref="RecordAction" /> whose
///     <see cref="RecordAction.IsDone" /> is true by default.
/// </remarks>
/// <seealso>"Modular AI", Section 8.5.3</seealso>
[TestFixture]
public class SequenceReasonerSpec
{
    [SetUp]
    public void BuildReasoner()
    {
        _executed = new List<string>();

        _sequenceReasoner = new SequenceReasoner();
        _sequenceReasoner.Enable();

        _option1Consideration = new StubConsideration(WeightValues.Neutral);
        _option2Consideration = new StubConsideration(WeightValues.Neutral);
        _option3Consideration = new StubConsideration(WeightValues.Neutral);

        _option1RecordAction = new RecordAction("Option1", _executed);
        _option2RecordAction = new RecordAction("Option2", _executed);
        _option3RecordAction = new RecordAction("Option3", _executed);

        StubOption option1 = new([_option1Consideration], [_option1RecordAction]);
        StubOption option2 = new([_option2Consideration], [_option2RecordAction]);
        StubOption option3 = new([_option3Consideration], [_option3RecordAction]);

        _sequenceReasoner.AddOption(option1);
        _sequenceReasoner.AddOption(option2);
        _sequenceReasoner.AddOption(option3);
    }

    private List<string> _executed;

    private SequenceReasoner _sequenceReasoner;

    private StubConsideration _option1Consideration;
    private StubConsideration _option2Consideration;
    private StubConsideration _option3Consideration;

    private RecordAction _option1RecordAction;
    private RecordAction _option2RecordAction;
    private RecordAction _option3RecordAction;

    [Test]
    public void SelectsTheFirstOptionOnTheFirstTick()
    {
        _sequenceReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Option1.Select", "Option1.Update" }));
    }

    [Test]
    public void AdvancesToTheNextOptionWhenTheCurrentOneIsDone()
    {
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Option1.Select", "Option1.Update",
            "Option1.Deselect", "Option2.Select", "Option2.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void StaysWithAnOptionThatIsNotDone()
    {
        _option1RecordAction.SetDone(false);

        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        _option1RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        // This is what separates a sequence from a per-tick walk: the decision depends on what was chosen last tick,
        // not only on the current situation.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Option1.Select", "Option1.Update",
            "Option1.Update",
            "Option1.Update",
            "Option1.Deselect", "Option2.Select", "Option2.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void ExecutesOptionsEvenWhenTheirConsiderationsVetoThem()
    {
        // A RuleBasedReasoner or DualUtilityReasoner would select nothing here. This one executes every option
        // whatever the considerations report.
        _option1Consideration.Veto();
        _option2Consideration.Veto();
        _option3Consideration.Veto();

        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Option1.Select", "Option1.Update",
            "Option1.Deselect", "Option2.Select", "Option2.Update",
            "Option2.Deselect", "Option3.Select", "Option3.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void SelectsNothingOnceEveryOptionHasRun()
    {
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        Assert.Multiple(() =>
        {
            // Running off the end deselects the last option and selects nothing, which is what makes IsDone report
            // true.
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Option1.Select", "Option1.Update",
                "Option1.Deselect", "Option2.Select", "Option2.Update",
                "Option2.Deselect", "Option3.Select", "Option3.Update",
                "Option3.Deselect",
                // @formatter:on
            }));

            Assert.That(_sequenceReasoner.SelectedOption, Is.Null);
            Assert.That(_sequenceReasoner.IsDone(), Is.True);
        });
    }

    [Test]
    public void DoesNotRestartAfterFinishing()
    {
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        int countAfterFinishing = _executed.Count;

        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        // Running out of options is terminal rather than a loop back to the start.
        Assert.That(_executed.GetRange(countAfterFinishing, _executed.Count - countAfterFinishing), Is.Empty);
    }

    [Test]
    public void StartsOverWhenEnabledAgain()
    {
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        int countAfterFinishing = _executed.Count;

        _sequenceReasoner.Disable();
        _sequenceReasoner.Enable();

        _sequenceReasoner.Update();

        // A SequenceReasoner held by a SubreasonerAction is enabled every time its containing option is selected, so
        // being enabled has to put it back at the top.
        Assert.That(_executed.GetRange(countAfterFinishing, _executed.Count - countAfterFinishing),
            Is.EqualTo(new[] { "Option1.Select", "Option1.Update" }));
    }

    [Test]
    public void AbandonsTheRunningOptionWhenDisabled()
    {
        _sequenceReasoner.Update();

        _sequenceReasoner.Disable();

        Assert.Multiple(() =>
        {
            // Disabling mid-run tears down whatever was executing rather than leaving it selected. The last entry
            // comes from Disable rather than from a tick.
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Option1.Select", "Option1.Update",
                "Option1.Deselect",
                // @formatter:on
            }));

            Assert.That(_sequenceReasoner.IsDone(), Is.True);
        });
    }
}

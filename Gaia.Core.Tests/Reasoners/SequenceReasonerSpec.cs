using Gaia.Core.Actions;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Reasoners;

/// <summary>
///     Covers <see cref="SequenceReasoner" />, which runs its options one at a time in the order they were added and
///     moves on when the selected option's <see cref="OptionBase.IsDone" /> is true.
/// </summary>
/// <remarks>
///     The fixture is three options, each holding a single <see cref="RecordAction" /> constructed with work left to
///     do, so the sequence holds each step until a test marks it done. The instantaneous tests mark steps done up front,
///     which is the case <see cref="ActionBase.IsDone" /> describes, an action that answers true from the moment it is
///     selected.
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

        _option1RecordAction = new RecordAction("Option1", _executed, false);
        _option2RecordAction = new RecordAction("Option2", _executed, false);
        _option3RecordAction = new RecordAction("Option3", _executed, false);

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
    public void SelectsTheFirstOptionOnTheFirstCycle()
    {
        _sequenceReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Option1.Select", "Option1.Update" }));
    }

    [Test]
    public void AdvancesToTheNextOptionWhenTheCurrentOneIsDone()
    {
        _sequenceReasoner.Update();

        _option1RecordAction.SetDone(true);

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
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        _option1RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        // This is what separates a sequence from a RuleBasedReasoner or DualUtilityReasoner, which decide from the
        // current weights alone each cycle. The decision depends on what was chosen last cycle, not only on the current
        // situation.
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
    public void RunsInstantaneousStepsInASingleCycle()
    {
        _option1RecordAction.SetDone(true);
        _option2RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        // Option3 still has work, so the cycle stops there. The two instantaneous steps were passed with Select and
        // Deselect and no Update between, which is why an instantaneous action completes its work in OnSelect.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Option1.Select", "Option1.Deselect", "Option2.Select", "Option2.Deselect", "Option3.Select", "Option3.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void FinishesAWhollyInstantaneousSequenceInOneCycle()
    {
        _option1RecordAction.SetDone(true);
        _option2RecordAction.SetDone(true);
        _option3RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Option1.Select", "Option1.Deselect", "Option2.Select", "Option2.Deselect", "Option3.Select", "Option3.Deselect",
                // @formatter:on
            }));

            Assert.That(_sequenceReasoner.SelectedOption, Is.Null);
            Assert.That(_sequenceReasoner.IsDone, Is.True);
        });
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

        _option1RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        _option2RecordAction.SetDone(true);

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
        RunTheWholeSequence();

        Assert.Multiple(() =>
        {
            // Running off the end deselects the last option and leaves the index past it, which is what makes IsDone
            // report true.
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
            Assert.That(_sequenceReasoner.IsDone, Is.True);
        });
    }

    [Test]
    public void DoesNotRestartAfterFinishing()
    {
        RunTheWholeSequence();

        int countAfterFinishing = _executed.Count;

        _sequenceReasoner.Update();
        _sequenceReasoner.Update();

        // Running out of options is terminal rather than a loop back to the start.
        Assert.That(_executed.GetRange(countAfterFinishing, _executed.Count - countAfterFinishing), Is.Empty);
    }

    [Test]
    public void StartsOverWhenEnabledAgain()
    {
        RunTheWholeSequence();

        int countAfterFinishing = _executed.Count;

        _option1RecordAction.SetDone(false);

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
            // comes from Disable rather than from a cycle.
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Option1.Select", "Option1.Update",
                "Option1.Deselect",
                // @formatter:on
            }));

            Assert.That(_sequenceReasoner.IsDone, Is.False,
                "an abandoned run is not a finished one, being enabled again starts it over");
        });
    }

    [Test]
    public void HoldsItsStepThroughATransientVeto()
    {
        // One cycle in which the nested RuleBasedReasoner has nothing eligible must hold the sequence at the fight step
        // rather than read as that step completing. With nothing selected, the nested reasoner has no selected option
        // to be done, so under its default rule the SubreasonerAction holding it is not done and the sequence stays
        // on that step.
        RuleBasedReasoner nested = new();
        StubConsideration nestedGate = new(WeightValues.Neutral);
        RecordAction nestedAction = new("Nested.Work", _executed, false);
        StubOption nestedOption = new([nestedGate], [nestedAction]);
        nested.AddOption(nestedOption);

        SubreasonerAction fightStep = new(nested);
        StubOption fightOption = new([new StubConsideration(WeightValues.Neutral)], [fightStep]);

        RecordAction lootAction = new("Loot", _executed, false);
        StubOption lootOption = new([new StubConsideration(WeightValues.Neutral)], [lootAction]);

        SequenceReasoner sequence = new();
        sequence.AddOption(fightOption);
        sequence.AddOption(lootOption);
        sequence.Enable();

        sequence.Update();

        nestedGate.Veto();
        sequence.Update();

        nestedGate.Set(WeightValues.Neutral);
        sequence.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_executed, Has.None.EqualTo("Loot.Select"),
                "the cycle with nothing eligible did not advance the sequence past the fight");
            Assert.That(_executed.Count(entry => entry == "Nested.Work.Select"), Is.EqualTo(2),
                "the nested work resumed once something was eligible again");
        });
    }

    /// <summary>Runs all three options to completion, marking each done in turn, leaving the sequence finished.</summary>
    private void RunTheWholeSequence()
    {
        _sequenceReasoner.Update();

        _option1RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        _option2RecordAction.SetDone(true);

        _sequenceReasoner.Update();

        _option3RecordAction.SetDone(true);

        _sequenceReasoner.Update();
    }
}

using Gaia.Core.Actions;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Actions;

/// <summary>Covers how decisions flow through a hierarchy when options hold a <see cref="SubreasonerAction" />.</summary>
/// <remarks>
///     The tests share one tree, except the ones that need a sequence or a third level, which build their own. The
///     shared tree is a root reasoner with two options, each carrying a <see cref="RecordAction" /> and a
///     <see cref="SubreasonerAction" />, and each subreasoner holds two options of its own.
/// </remarks>
[TestFixture]
public class SubreasonerActionSpec
{
    [SetUp]
    public void BuildReasoners()
    {
        _logSink = new RecordingLogSink();
        _executed = new List<string>();

        _reasoner = new RuleBasedReasoner();
        _reasoner.Enable();

        // The subreasoners are left disabled. Enabling them is the containing action's job.
        _subreasoner1 = new RuleBasedReasoner();
        _subreasoner2 = new RuleBasedReasoner();

        _reasonerConsideration1 = new StubConsideration(WeightValues.Neutral);
        _reasonerConsideration2 = new StubConsideration(WeightValues.Neutral);

        _subreasoner1Consideration1 = new StubConsideration(WeightValues.Neutral);
        _subreasoner1Consideration2 = new StubConsideration(WeightValues.Neutral);

        _subreasoner2Consideration1 = new StubConsideration(WeightValues.Neutral);
        _subreasoner2Consideration2 = new StubConsideration(WeightValues.Neutral);

        RecordAction reasonerRecordAction1 = new("Reasoner.First", _executed, false);
        RecordAction reasonerRecordAction2 = new("Reasoner.Second", _executed, false);

        _subreasoner1RecordAction1 = new RecordAction("Subreasoner1.First", _executed, false);
        RecordAction subreasoner1RecordAction2 = new("Subreasoner1.Second", _executed, false);

        RecordAction subreasoner2RecordAction1 = new("Subreasoner2.First", _executed, false);
        RecordAction subreasoner2RecordAction2 = new("Subreasoner2.Second", _executed, false);

        _subreasonerAction1 = new SubreasonerAction(_subreasoner1);
        SubreasonerAction subreasonerAction2 = new(_subreasoner2);

        // The RecordAction comes first in each root option, so the log shows the parent acting before the
        // subreasoner it contains.
        StubOption reasonerOption1 = new([_reasonerConsideration1], [reasonerRecordAction1, _subreasonerAction1]);
        StubOption reasonerOption2 = new([_reasonerConsideration2], [reasonerRecordAction2, subreasonerAction2]);

        StubOption subreasoner1Option1 = new([_subreasoner1Consideration1], [_subreasoner1RecordAction1]);
        StubOption subreasoner1Option2 = new([_subreasoner1Consideration2], [subreasoner1RecordAction2]);

        StubOption subreasoner2Option1 = new([_subreasoner2Consideration1], [subreasoner2RecordAction1]);
        StubOption subreasoner2Option2 = new([_subreasoner2Consideration2], [subreasoner2RecordAction2]);

        _reasoner.AddOption(reasonerOption1);
        _reasoner.AddOption(reasonerOption2);

        _subreasoner1.AddOption(subreasoner1Option1);
        _subreasoner1.AddOption(subreasoner1Option2);

        _subreasoner2.AddOption(subreasoner2Option1);
        _subreasoner2.AddOption(subreasoner2Option2);
    }

    private RecordingLogSink _logSink;

    private List<string> _executed;

    private RuleBasedReasoner _reasoner;
    private RuleBasedReasoner _subreasoner1;
    private RuleBasedReasoner _subreasoner2;

    private SubreasonerAction _subreasonerAction1;
    private RecordAction _subreasoner1RecordAction1;

    private StubConsideration _reasonerConsideration1;
    private StubConsideration _reasonerConsideration2;

    private StubConsideration _subreasoner1Consideration1;
    private StubConsideration _subreasoner1Consideration2;

    private StubConsideration _subreasoner2Consideration1;
    private StubConsideration _subreasoner2Consideration2;

    [Test]
    public void UpdatesTheContainedReasonerWhenItsOptionIsSelected()
    {
        _reasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
                // @formatter:on
            }));

            // Selecting the containing action is what enables the subreasoner. The other one was never selected.
            Assert.That(_subreasoner1.IsEnabled, Is.True);
            Assert.That(_subreasoner2.IsEnabled, Is.False);
        });
    }

    [Test]
    public void KeepsUpdatingTheContainedReasonerAfterItHasBeenSelected()
    {
        _reasoner.Update();
        _reasoner.Update();

        // The subreasoner is updated every cycle rather than once at selection.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
            "Reasoner.First.Update", "Subreasoner1.First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void PropagatesDeselectionToTheContainedReasoner()
    {
        _reasoner.Update();

        _reasonerConsideration1.Veto();

        _reasoner.Update();

        Assert.Multiple(() =>
        {
            // The subreasoner's running option is torn down as part of deselecting the option that contains it,
            // before the incoming branch starts up.
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
                "Reasoner.First.Deselect", "Subreasoner1.First.Deselect", "Reasoner.Second.Select", "Reasoner.Second.Update", "Subreasoner2.First.Select", "Subreasoner2.First.Update",
                // @formatter:on
            }));

            Assert.That(_subreasoner1.IsEnabled, Is.False);
            Assert.That(_subreasoner2.IsEnabled, Is.True);
        });
    }

    [Test]
    public void RestartsTheContainedReasonerWhenItsOptionIsSelectedAgain()
    {
        _reasoner.Update();

        _reasonerConsideration1.Veto();

        _reasoner.Update();

        _reasonerConsideration1.Pass();

        _reasoner.Update();

        // A subreasoner that was disabled and then selected again starts cleanly, with no state left over from its
        // first run.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
            "Reasoner.First.Deselect", "Subreasoner1.First.Deselect", "Reasoner.Second.Select", "Reasoner.Second.Update", "Subreasoner2.First.Select", "Subreasoner2.First.Update",
            "Reasoner.Second.Deselect", "Subreasoner2.First.Deselect", "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void SwitchesTheContainedReasonersOwnOptionWithoutDisturbingTheParent()
    {
        _reasoner.Update();

        _subreasoner1Consideration1.Veto();

        _reasoner.Update();

        // The parent's option stays selected throughout, so only the subreasoner swaps.
        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
            "Reasoner.First.Update", "Subreasoner1.First.Deselect", "Subreasoner1.Second.Select", "Subreasoner1.Second.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void StopsUpdatingTheContainedReasonerOnceItsOptionIsDeselected()
    {
        _reasoner.Update();

        _reasonerConsideration1.Veto();

        _reasoner.Update();

        int countAfterTheSwap = _executed.Count;

        _reasoner.Update();
        _reasoner.Update();

        // Only the branch that is still selected keeps logging. A disabled reasoner that carried on thinking would
        // show up here as extra Subreasoner1 entries.
        Assert.That(_executed.GetRange(countAfterTheSwap, _executed.Count - countAfterTheSwap), Is.EqualTo(new[]
        {
            // @formatter:off
            "Reasoner.Second.Update", "Subreasoner2.First.Update",
            "Reasoner.Second.Update", "Subreasoner2.First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void IsNotDoneBeforeItsReasonerHasChosen()
    {
        // Nothing has run yet, so the subreasoner has no selected option, and having no selected option does not count
        // as done.
        Assert.That(_subreasonerAction1.IsDone, Is.False);
    }

    [Test]
    public void ReportsDoneOnceTheOptionItsReasonerChoseIsDone()
    {
        _reasoner.Update();

        // The subreasoner picked an option whose action still has work, so the containing action is not finished.
        Assert.That(_subreasonerAction1.IsDone, Is.False);

        _subreasoner1RecordAction1.SetDone(true);

        Assert.That(_subreasonerAction1.IsDone, Is.True, "the step ends when what its reasoner chose has finished");
    }

    [Test]
    public void IsNotDoneOnACycleWithNothingEligible()
    {
        _reasoner.Update();

        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        Assert.That(_subreasonerAction1.IsDone, Is.False, "an all-vetoed cycle must not read as the subtree finishing");
    }

    [Test]
    public void ReportsDoneWhenItsSequenceRunsOffItsEnd()
    {
        // A sequence is the reasoner with an end, so a subreasoner step wrapping one ends exactly when the
        // sequence runs off its last option.
        SequenceReasoner sequence = new();
        RecordAction onlyStepAction = new("OnlyStep", _executed);
        sequence.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [onlyStepAction]));

        SubreasonerAction sequenceAction = new(sequence);

        Assert.That(sequenceAction.IsDone, Is.False, "the sequence has not started, so there is work left");

        sequenceAction.Select();
        sequenceAction.Update();

        Assert.That(sequenceAction.IsDone, Is.True, "the instantaneous step ran and the sequence ran off its end");
    }

    [Test]
    public void IsNotDoneWhileItsSequenceHasStepsLeft()
    {
        // A parent sequence reads this action's IsDone in its Think, before its Act runs the sequence this action holds
        // past the finished step. A finished step must therefore not make the action report done while the sequence has
        // steps left.
        SequenceReasoner sequence = new();
        RecordAction firstStepAction = new("FirstStep", _executed, false);
        RecordAction secondStepAction = new("SecondStep", _executed, false);
        sequence.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [firstStepAction]));
        sequence.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [secondStepAction]));

        SubreasonerAction sequenceAction = new(sequence);
        sequenceAction.Select();
        sequenceAction.Update();

        firstStepAction.SetDone(true);

        Assert.That(sequenceAction.IsDone, Is.False, "the first step finished, but the sequence has a second");

        sequenceAction.Update();
        secondStepAction.SetDone(true);
        sequenceAction.Update();

        Assert.That(sequenceAction.IsDone, Is.True, "the sequence ran off its last step");
    }

    [Test]
    public void LeavesTheParentRunningWhenTheContainedReasonerHasNothingValid()
    {
        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        Assert.Multiple(() =>
        {
            // A subtree that selected nothing is not a failure, and not completion either. The parent's option stays
            // selected, its other actions run, and the subtree stays not done, waiting for something eligible.
            Assert.That(_executed, Is.EqualTo(new[] { "Reasoner.First.Select", "Reasoner.First.Update" }));

            Assert.That(_subreasoner1.IsEnabled, Is.True);
            Assert.That(_subreasoner1.IsDone, Is.False);
        });
    }

    [Test]
    public void PropagatesDisablingTheRootReasoner()
    {
        _reasoner.Update();

        _reasoner.Disable();

        Assert.Multiple(() =>
        {
            // Brain.Disable forwards to the root reasoner's Disable, which this test calls directly. That call is a
            // different path from a veto, and it has to deselect every level beneath the root as a veto does. The last
            // two entries come from Disable rather than from a cycle.
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Reasoner.First.Select", "Reasoner.First.Update", "Subreasoner1.First.Select", "Subreasoner1.First.Update",
                "Reasoner.First.Deselect", "Subreasoner1.First.Deselect",
                // @formatter:on
            }));

            Assert.That(_subreasoner1.IsEnabled, Is.False);
        });
    }

    [Test]
    public void PropagatesThroughMoreThanTwoLevels()
    {
        // Builds its own tree rather than using the shared one, so that adding a third level does not change what
        // every other test in this fixture expects to see. Each reasoner has a single option, so vetoing the root's
        // consideration leaves nothing selected anywhere.
        RuleBasedReasoner rootReasoner = new();
        rootReasoner.Enable();

        RuleBasedReasoner childReasoner = new();
        RuleBasedReasoner grandchildReasoner = new();

        StubConsideration rootConsideration = new(WeightValues.Neutral);
        StubConsideration childConsideration = new(WeightValues.Neutral);
        StubConsideration grandchildConsideration = new(WeightValues.Neutral);

        RecordAction rootRecordAction = new("Root", _executed, false);
        RecordAction childRecordAction = new("Child", _executed, false);
        RecordAction grandchildRecordAction = new("Grandchild", _executed, false);

        SubreasonerAction childSubreasonerAction = new(childReasoner);
        SubreasonerAction grandchildSubreasonerAction = new(grandchildReasoner);

        StubOption rootOption = new([rootConsideration], [rootRecordAction, childSubreasonerAction]);
        StubOption childOption = new([childConsideration], [childRecordAction, grandchildSubreasonerAction]);
        StubOption grandchildOption = new([grandchildConsideration], [grandchildRecordAction]);

        rootReasoner.AddOption(rootOption);
        childReasoner.AddOption(childOption);
        grandchildReasoner.AddOption(grandchildOption);

        rootReasoner.Update();

        rootConsideration.Veto();

        rootReasoner.Update();

        Assert.Multiple(() =>
        {
            // Selection reaches the bottom on the first cycle, and tearing down the root disables every level below
            // it on the second.
            Assert.That(_executed, Is.EqualTo(new[]
            {
                // @formatter:off
                "Root.Select", "Root.Update", "Child.Select", "Child.Update", "Grandchild.Select", "Grandchild.Update",
                "Root.Deselect", "Child.Deselect", "Grandchild.Deselect",
                // @formatter:on
            }));

            Assert.That(childReasoner.IsEnabled, Is.False);
            Assert.That(grandchildReasoner.IsEnabled, Is.False);
        });
    }

    [Test]
    public void EndsOnceNothingHasBeenEligibleForTheConfiguredCycles()
    {
        // The limit is 2 here, so the first all-vetoed cycle is not the end and the second is.
        _subreasonerAction1.DoneWhen = SubreasonerCompletion.NothingEligible;
        _subreasonerAction1.NothingEligibleCycleLimit = 2;

        _reasoner.Update();

        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        Assert.That(_subreasonerAction1.IsDone, Is.False,
            "one all-vetoed cycle is fewer than NothingEligibleCycleLimit");

        _reasoner.Update();

        Assert.That(_subreasonerAction1.IsDone, Is.True,
            "nothing has been eligible for NothingEligibleCycleLimit cycles");
    }

    [Test]
    public void RestartsItsCountWhenSomethingIsEligibleAgain()
    {
        _subreasonerAction1.DoneWhen = SubreasonerCompletion.NothingEligible;
        _subreasonerAction1.NothingEligibleCycleLimit = 2;

        _reasoner.Update();

        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        _subreasoner1Consideration1.Pass();

        _reasoner.Update();

        _subreasoner1Consideration1.Veto();

        _reasoner.Update();

        Assert.That(_subreasonerAction1.IsDone, Is.False, "the count restarted when the subtree selected something");
    }

    [Test]
    public void IsNotDoneUnderTheNothingEligibleRuleWhileItsReasonerChoosesFinishedOptions()
    {
        _subreasonerAction1.DoneWhen = SubreasonerCompletion.NothingEligible;

        _reasoner.Update();

        _subreasoner1RecordAction1.SetDone(true);

        Assert.That(_subreasonerAction1.IsDone, Is.False,
            "a finished choice is not the end while something is still eligible");
    }

    [Test]
    public void IsNotDoneUnderTheReasonerDoneRuleWhileItContainsAScoringReasoner()
    {
        _subreasonerAction1.DoneWhen = SubreasonerCompletion.ReasonerDone;

        _reasoner.Update();

        _subreasoner1RecordAction1.SetDone(true);

        Assert.That(_subreasonerAction1.IsDone, Is.False, "a scoring reasoner never runs out of work");

        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        Assert.That(_subreasonerAction1.IsDone, Is.False, "and nothing eligible is not its end either");
    }

    [Test]
    public void ReportsDoneUnderTheReasonerDoneRuleWhenItsSequenceRunsOffItsEnd()
    {
        SequenceReasoner sequence = new();
        RecordAction onlyStepAction = new("OnlyStep", _executed);
        sequence.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [onlyStepAction]));

        SubreasonerAction sequenceAction = new(sequence) { DoneWhen = SubreasonerCompletion.ReasonerDone };

        sequenceAction.Select();
        sequenceAction.Update();

        Assert.That(sequenceAction.IsDone, Is.True, "the reasoner's own end counts under every rule");
    }

    [Test]
    public void LoadsItsCompletionRuleInAnyCasing()
    {
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate" DoneWhen="nothingeligible"
                                       NothingEligibleCycleLimit="3">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.DoneWhen, Is.EqualTo(SubreasonerCompletion.NothingEligible));
            Assert.That(loaded.NothingEligibleCycleLimit, Is.EqualTo(3));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase("SelectedOptionDone", SubreasonerCompletion.SelectedOptionDone)]
    [TestCase("NothingEligible", SubreasonerCompletion.NothingEligible)]
    [TestCase("ReasonerDone", SubreasonerCompletion.ReasonerDone)]
    public void LoadsEachCompletionRuleByName(string doneWhen, SubreasonerCompletion rule)
    {
        string snippet = $"""
                          <Action Type="Subreasoner" Name="Investigate" DoneWhen="{doneWhen}">
                              <Reasoner Type="Sequence"/>
                          </Action>
                          """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.DoneWhen, Is.EqualTo(rule));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesACompletionRuleThatNamesNothing()
    {
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate" DoneWhen="WhenReady">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Action element's DoneWhen is not SelectedOptionDone, NothingEligible or ReasonerDone, it reads \"WhenReady\"",
            }));
        });
    }

    [Test]
    public void RefusesANothingEligibleCycleLimitBelowOne()
    {
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate" DoneWhen="NothingEligible"
                                       NothingEligibleCycleLimit="0">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Action element's NothingEligibleCycleLimit is not 1 or more, it reads \"0\"",
            }));
        });
    }

    [Test]
    public void LoadsANothingEligibleCycleLimitOfOne()
    {
        // 1 is the lowest limit the action takes, above the 0 that RefusesANothingEligibleCycleLimitBelowOne refuses.
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate" DoneWhen="NothingEligible"
                                       NothingEligibleCycleLimit="1">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.NothingEligibleCycleLimit, Is.EqualTo(1));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesANothingEligibleCycleLimitThatIsNotANumber()
    {
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate" DoneWhen="NothingEligible"
                                       NothingEligibleCycleLimit="many">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Action element's NothingEligibleCycleLimit is not a whole number, it reads \"many\"",
            }));
        });
    }

    [Test]
    public void RefusesANothingEligibleCycleLimitUnderAnotherRule()
    {
        // No DoneWhen is set, so the rule defaults to SelectedOptionDone, and only NothingEligible takes a cycle
        // limit.
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate" NothingEligibleCycleLimit="3">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Action element sets NothingEligibleCycleLimit under the SelectedOptionDone rule",
            }));
        });
    }

    [Test]
    public void LoadsItsContainedReasoner()
    {
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate">
                                   <Reasoner Type="Sequence"/>
                               </Action>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        SubreasonerAction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Reasoner, Is.InstanceOf<SequenceReasoner>());
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesANodeWithNoReasoner()
    {
        const string snippet = """
                               <Action Type="Subreasoner" Name="Investigate"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        // The factory refuses the short slot after Init returns true, so the test loads through the factory rather than
        // calling Init.
        bool wasCreated = creationData.Factories.Actions.TryCreate(creationData, out ActionBase? loaded);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Action element has no Reasoner element",
            }));
        });
    }
}

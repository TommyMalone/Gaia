using Gaia.Core.Actions;
using Gaia.Core.Reasoners;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Actions;

/// <summary>Covers how decisions flow through a hierarchy when options hold a <see cref="SubreasonerAction" />.</summary>
/// <remarks>
///     Every test but one shares the same tree, which is a root reasoner with two options, each carrying a
///     <see cref="RecordAction" /> and a <see cref="SubreasonerAction" />, and each subreasoner holding two options of its
///     own. The exception builds its own tree, three levels deep.
/// </remarks>
[TestFixture]
public class SubreasonerActionSpec
{
    [SetUp]
    public void BuildReasoners()
    {
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

        RecordAction reasonerRecordAction1 = new("Reasoner.First", _executed);
        RecordAction reasonerRecordAction2 = new("Reasoner.Second", _executed);

        RecordAction subreasoner1RecordAction1 = new("Subreasoner1.First", _executed);
        RecordAction subreasoner1RecordAction2 = new("Subreasoner1.Second", _executed);

        RecordAction subreasoner2RecordAction1 = new("Subreasoner2.First", _executed);
        RecordAction subreasoner2RecordAction2 = new("Subreasoner2.Second", _executed);

        _subreasonerAction1 = new SubreasonerAction(_subreasoner1);
        SubreasonerAction subreasonerAction2 = new(_subreasoner2);

        // The RecordAction comes first in each root option, so the log shows the parent acting before the
        // subreasoner it contains.
        StubOption reasonerOption1 = new([_reasonerConsideration1], [reasonerRecordAction1, _subreasonerAction1]);
        StubOption reasonerOption2 = new([_reasonerConsideration2], [reasonerRecordAction2, subreasonerAction2]);

        StubOption subreasoner1Option1 = new([_subreasoner1Consideration1], [subreasoner1RecordAction1]);
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

    private List<string> _executed;

    private RuleBasedReasoner _reasoner;
    private RuleBasedReasoner _subreasoner1;
    private RuleBasedReasoner _subreasoner2;

    private SubreasonerAction _subreasonerAction1;

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
    public void PropagatesDeselection()
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
            Assert.That(_subreasoner1.IsDone, Is.True);

            Assert.That(_subreasoner2.IsEnabled, Is.True);
            Assert.That(_subreasoner2.IsDone, Is.False);
        });
    }

    [Test]
    public void SupportsReselection()
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
    public void ReportsDoneWheneverTheContainedReasonerHasNothingSelected()
    {
        // Nothing has run yet, so the subreasoner has no selected option and the action containing it is finished.
        Assert.That(_subreasonerAction1.IsDone, Is.True);

        _reasoner.Update();

        // The subreasoner picked an option, so the containing action is not finished. Without IsDone delegating to
        // Reasoner, a SequenceReasoner would move on while the whole subtree was still running.
        Assert.That(_subreasonerAction1.IsDone, Is.False);

        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        Assert.That(_subreasonerAction1.IsDone, Is.True);
    }

    [Test]
    public void LeavesTheParentRunningWhenTheContainedReasonerHasNothingValid()
    {
        _subreasoner1Consideration1.Veto();
        _subreasoner1Consideration2.Veto();

        _reasoner.Update();

        Assert.Multiple(() =>
        {
            // An empty-handed subtree is not a failure. The parent's option stays selected and its other actions run.
            Assert.That(_executed, Is.EqualTo(new[] { "Reasoner.First.Select", "Reasoner.First.Update" }));

            Assert.That(_subreasoner1.IsEnabled, Is.True);
            Assert.That(_subreasoner1.IsDone, Is.True);
        });
    }

    [Test]
    public void PropagatesDisablingTheRootReasoner()
    {
        _reasoner.Update();

        _reasoner.Disable();

        Assert.Multiple(() =>
        {
            // Shutting off the brain is a different entry point from an option becoming invalid, and it has to reach
            // the same way down the tree. The last group comes from Disable rather than from a cycle.
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
        List<string> executed = new();

        RuleBasedReasoner rootReasoner = new();
        rootReasoner.Enable();

        RuleBasedReasoner childReasoner = new();
        RuleBasedReasoner grandchildReasoner = new();

        StubConsideration rootConsideration = new(WeightValues.Neutral);
        StubConsideration childConsideration = new(WeightValues.Neutral);
        StubConsideration grandchildConsideration = new(WeightValues.Neutral);

        RecordAction rootRecordAction = new("Root", executed);
        RecordAction childRecordAction = new("Child", executed);
        RecordAction grandchildRecordAction = new("Grandchild", executed);

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
            Assert.That(executed, Is.EqualTo(new[]
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
}

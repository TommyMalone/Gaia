using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;

namespace Gaia.Core.Tests.Brains;

/// <summary>Covers how enabling, disabling and updating a <see cref="Brain" /> reaches whatever its reasoner runs.</summary>
/// <remarks>
///     The reasoner is a <see cref="RuleBasedReasoner" /> rather than a stub, with one always-valid option holding a
///     <see cref="RecordAction" />, so what is asserted is which lifecycle calls reached the action rather than anything
///     about how the reasoner decided.
/// </remarks>
[TestFixture]
public class BrainSpec
{
    [SetUp]
    public void BuildBrain()
    {
        _logSink = new RecordingLogSink();
        _executed = new List<string>();

        RecordAction action = new("Idle", _executed, false);
        StubOption option = new([], [action]);

        _reasoner = new RuleBasedReasoner();
        _reasoner.AddOption(option);

        _brain = new Brain { RootReasoner = _reasoner };

        // The actor a loading test hands the creation data as Self, so the loaded brain takes its name.
        _guard = new Actor { Name = "Guard" };
    }

    private RecordingLogSink _logSink;

    private List<string> _executed;

    private RuleBasedReasoner _reasoner;
    private Brain _brain;

    private Actor _guard;

    [Test]
    public void StartsDisabled()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_brain.IsEnabled, Is.False);
            Assert.That(_reasoner.IsEnabled, Is.False);
        });
    }

    [Test]
    public void HasABlackboardOfItsOwn()
    {
        Brain anotherBrain = new();

        Assert.Multiple(() =>
        {
            Assert.That(_brain.Blackboard, Is.Not.Null);
            Assert.That(_brain.Blackboard, Is.Not.SameAs(anotherBrain.Blackboard));
        });
    }

    [Test]
    public void TakesItsRootReasonerAtConstruction()
    {
        Brain constructed = new(_reasoner);

        Assert.That(constructed.RootReasoner, Is.SameAs(_reasoner));
    }

    [Test]
    public void EnablesItsReasonerWhenEnabled()
    {
        _brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(_brain.IsEnabled, Is.True);
            Assert.That(_reasoner.IsEnabled, Is.True);
        });
    }

    [Test]
    public void UpdatesItsReasonerWhileEnabled()
    {
        _brain.Enable();

        _brain.Update();
        _brain.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update", "Idle.Update" }));
    }

    [Test]
    public void DoesNothingWhenUpdatedBeforeBeingEnabled()
    {
        _brain.Update();

        Assert.That(_executed, Is.Empty);
    }

    [Test]
    public void TearsDownWhatItWasRunningWhenDisabled()
    {
        // Disabling the reasoner deselects its option, which deselects the action. The player taking control of the
        // AI character should not leave the AI's last action running underneath.
        _brain.Enable();
        _brain.Update();

        _brain.Disable();

        Assert.Multiple(() =>
        {
            Assert.That(_brain.IsEnabled, Is.False);
            Assert.That(_reasoner.IsEnabled, Is.False);
            Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update", "Idle.Deselect" }));
        });
    }

    [Test]
    public void StaysQuietAfterBeingDisabled()
    {
        _brain.Enable();
        _brain.Update();
        _brain.Disable();

        _executed.Clear();

        _brain.Update();

        Assert.That(_executed, Is.Empty);
    }

    [Test]
    public void CountsNoCyclesWhileDisabled()
    {
        // The monitor's count numbers the cycles in the decision history. An Update on a disabled brain runs no cycle,
        // so it leaves the count where it was.
        MonitorSettings settings = new();
        BrainMonitor monitor = new(settings);
        _brain.Monitor = monitor;

        _brain.Enable();
        _brain.Update();
        _brain.Disable();

        _brain.Update();

        Assert.That(monitor.CycleIndex, Is.EqualTo(1));
    }

    [Test]
    public void StartsAgainWhenReEnabled()
    {
        _brain.Enable();
        _brain.Update();
        _brain.Disable();

        _executed.Clear();

        _brain.Enable();
        _brain.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update" }));
    }

    [Test]
    public void EnablesOnlyOnce()
    {
        // A second Enable must not restart the reasoner. A SequenceReasoner makes that observable, since enabling one
        // puts it back at its first option. The sequence here is mid-run on Third, having passed its instantaneous
        // First and Second in the first cycle. A restart would set its position back to First, so stepping on once
        // Third finishes would land on Second and replay it instead of finishing the sequence.
        RecordAction firstAction = new("First", _executed);
        RecordAction secondAction = new("Second", _executed);
        RecordAction thirdAction = new("Third", _executed, false);

        StubOption firstStep = new([], [firstAction]);
        StubOption secondStep = new([], [secondAction]);
        StubOption thirdStep = new([], [thirdAction]);

        SequenceReasoner sequence = new();
        sequence.AddOption(firstStep);
        sequence.AddOption(secondStep);
        sequence.AddOption(thirdStep);

        Brain brain = new() { RootReasoner = sequence };

        brain.Enable();
        brain.Update();
        brain.Update();

        brain.Enable();

        thirdAction.SetDone(true);
        brain.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Deselect", "Second.Select", "Second.Deselect", "Third.Select", "Third.Update",
            "Third.Update",
            "Third.Deselect",
            // @formatter:on
        }));
    }

    [Test]
    public void DoesNothingWithNoReasoner()
    {
        Brain emptyBrain = new();

        emptyBrain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(() => emptyBrain.Update(), Throws.Nothing);
            Assert.That(() => emptyBrain.Disable(), Throws.Nothing);
        });
    }

    [Test]
    public void WarnsWhenItIsEnabledWithNoReasoner()
    {
        Brain emptyBrain = new()
        {
            LogSink = _logSink,
            LogChannel = "Sentry",
        };

        emptyBrain.Enable();

        Assert.That(_logSink.Entries, Is.EqualTo(new[] { "Warning Sentry brain enabled with no root reasoner" }));
    }

    [Test]
    public void LoadsItsRootReasonerAndStampsItsChannel()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased">
                                       <Option Type="ConsiderationAndAction" Name="Idle"/>
                                   </Reasoner>
                               </Brain>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        creationData.Self = _guard;

        AiString guardChannel = "Guard";
        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.RootReasoner, Is.InstanceOf<RuleBasedReasoner>());
            Assert.That(loaded.LogChannel, Is.EqualTo(guardChannel), "the channel comes from the Self actor's name");
            Assert.That(loaded.RootReasoner!.LogChannel, Is.EqualTo(guardChannel), "and reaches the tree beneath");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void GivesItsOwnBlackboardToWhatItLoads()
    {
        // The Watch option vetoes while SeesPlayer is missing, so it only runs if the loaded consideration reads
        // the brain's own blackboard, the one the host writes to after loading.
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased">
                                       <Option Type="ConsiderationAndAction" Name="Watch">
                                           <Consideration Type="BooleanVariable" Variable="SeesPlayer">
                                               <Missing Multiplier="0"/>
                                               <WeightFunction Type="Boolean"/>
                                           </Consideration>
                                       </Option>
                                   </Reasoner>
                               </Brain>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);

            loaded.Blackboard.SetBool("SeesPlayer", true);
            loaded.Enable();
            loaded.Update();

            Assert.That(loaded.RootReasoner!.SelectedOption, Is.Not.Null,
                "the loaded consideration read the value off the brain's own blackboard");
        });
    }

    [Test]
    public void RefusesElementsItDoesNotTake()
    {
        const string snippet = """
                               <Brain>
                                   <Option Type="ConsiderationAndAction" Name="Stray"/>
                               </Brain>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Brain element does not take Option elements",
            }));
        });
    }

    [Test]
    public void RefusesAnAttributeOnTheBrainElement()
    {
        const string snippet = """
                               <Brain Name="Guard">
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(loaded.RootReasoner, Is.Null, "the refusal comes before any child is built");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Brain element does not take the Name attribute",
            }));
        });
    }

    [Test]
    public void RefusesATypeOnTheBrainElement()
    {
        // No factory picks a brain by a Type, so a Type on the root is a stray, refused as any other attribute the
        // description does not name.
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad("""<Brain Type="Guard"/>""", _logSink);

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Brain element does not take the Type attribute",
            }));
        });
    }

    [Test]
    public void LoadsABrainElementWithNoReasoner()
    {
        // The root's Reasoner slot has a MinCount of 0, so the check after the children load refuses nothing.
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad("<Brain/>", _logSink);

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.RootReasoner, Is.Null);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesASecondReasoner()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Brain element takes only one Reasoner element",
            }), "silently keeping one of two reasoners would ship half the author's tree");
        });
    }

    [Test]
    public void StampsItsBlackboardWithItsSinkAndChannel()
    {
        const string snippet = """
                               <Brain>
                                   <Reasoner Type="RuleBased"/>
                               </Brain>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        creationData.Self = _guard;

        Brain loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.Blackboard.LogSink, Is.SameAs(_logSink),
                "a mismatch on the character's own store reports where the brain reports");
            Assert.That(loaded.Blackboard.LogChannel.Text, Is.EqualTo("Guard"));
        });
    }

    [Test]
    public void LetsAFaultOutByDefault()
    {
        Brain brain = BuildThrowingBrain(false);

        brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(() => brain.Update(), Throws.TypeOf<InvalidOperationException>(),
                "under Rethrow the exception comes out of Update with its stack intact");
            Assert.That(brain.IsEnabled, Is.True, "the library did not stop the brain, the host decides");
            Assert.That(brain.Fault, Is.Null);
            Assert.That(_logSink.Entries, Has.None.StartsWith("Error"));
        });
    }

    [Test]
    public void ContainsAFaultWhenAskedTo()
    {
        Brain brain = BuildThrowingBrain(false);
        brain.FaultPolicy = FaultPolicy.Contain;

        brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(() => brain.Update(), Throws.Nothing);
            Assert.That(brain.Fault, Is.TypeOf<InvalidOperationException>());
            Assert.That(brain.IsEnabled, Is.False, "a contained fault stops the brain");
            Assert.That(brain.RootReasoner!.IsEnabled, Is.False, "and tears the tree down beneath it");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Message Sentry brain enabled",
                "Error Sentry a component threw and the brain is stopped, InvalidOperationException: the action failed",
                "Message Sentry brain disabled",
            }));
        });
    }

    [Test]
    public void ReportsAFaultThatThrowsAgainWhileStopping()
    {
        // Disable deselects the action that just threw, and the same bug usually throws again there. Containment covers
        // the stop too, so the second throw is reported on a line of its own and the brain still ends disabled.
        Brain brain = BuildThrowingBrain(true);
        brain.FaultPolicy = FaultPolicy.Contain;

        brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(() => brain.Update(), Throws.Nothing);
            Assert.That(brain.IsEnabled, Is.False);
            Assert.That(brain.Fault!.Message, Is.EqualTo("the action failed"), "the fault kept is the first");
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Message Sentry brain enabled",
                "Error Sentry a component threw and the brain is stopped, InvalidOperationException: the action failed",
                "Message Sentry brain disabled",
                "Error Sentry the brain threw again while stopping, InvalidOperationException: the action failed again",
            }));
        });
    }

    [Test]
    public void StaysStoppedWhenEnabledAfterAFault()
    {
        Brain brain = BuildThrowingBrain(false);
        brain.FaultPolicy = FaultPolicy.Contain;

        brain.Enable();
        brain.Update();

        brain.Enable();

        Assert.Multiple(() =>
        {
            Assert.That(brain.IsEnabled, Is.False, "a faulted brain is replaced, not restarted");
            Assert.That(_logSink.Entries[^1],
                Is.EqualTo("Warning Sentry brain faulted and stays stopped until it is replaced"));
        });
    }

    /// <summary>A brain over one always-valid option whose action throws every cycle, wired to the fixture's sink.</summary>
    private Brain BuildThrowingBrain(bool throwsOnDeselect)
    {
        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(new StubOption([], [new ThrowingAction(throwsOnDeselect)]));

        return new Brain(reasoner)
        {
            LogSink = _logSink,
            LogChannel = "Sentry",
        };
    }
}

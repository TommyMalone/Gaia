using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Reasoners;

/// <summary>
///     Covers <see cref="DualUtilityReasoner" />'s four selection steps, which discard options with no weight, keep
///     only the highest rank tier, discard survivors that fall too far below the best weight, then choose among what is
///     left at random in proportion to weight.
/// </summary>
/// <remarks>
///     Each option's numbers come from a <see cref="StubConsideration" /> set by hand, and the random source is
///     fixed, so each test whose comment opens with a step number pins down that one step.
///     SelectsInProportionToWeightOverManyCycles instead draws its rolls from a seeded source and checks the share of many
///     cycles in which the heavier option is selected.
/// </remarks>
[TestFixture]
public class DualUtilityReasonerSpec
{
    [SetUp]
    public void BuildReasoner()
    {
        _logSink = new RecordingLogSink();
        _executed = new List<string>();

        _dualUtilityReasoner = new DualUtilityReasoner { LogSink = _logSink };
        _dualUtilityReasoner.Enable();

        // A roll of 0 always lands on the first surviving option. A test whose answer has to change when the step it
        // covers is skipped sets its own roll instead.
        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0f);

        // A neutral TuningConsideration contributes nothing, but its presence stops EnsureTuningConsideration from
        // adding the default one with an addend of 1. That keeps each option's weight exactly what its
        // StubConsideration reports. TuningConsideration reads neither the option it belongs to nor whether it is
        // selected, so one instance can sit in every option.
        TuningConsideration neutralTuning = new(WeightValues.Neutral);

        _firstConsideration = new StubConsideration(WeightValues.Neutral);
        _secondConsideration = new StubConsideration(WeightValues.Neutral);

        RecordAction firstRecordAction = new("First", _executed, false);
        RecordAction secondRecordAction = new("Second", _executed, false);

        _firstOption = new StubOption([_firstConsideration, neutralTuning], [firstRecordAction]) { Name = "First" };
        _secondOption = new StubOption([_secondConsideration, neutralTuning], [secondRecordAction]) { Name = "Second" };

        _dualUtilityReasoner.AddOption(_firstOption);
        _dualUtilityReasoner.AddOption(_secondOption);

        _vetoedWithALargeAddend = new WeightValues(50f, 0f, WeightValues.NoRank);

        _heavyWeight = new WeightValues(3f, 1f, WeightValues.NoRank);
        _lightWeight = new WeightValues(1f, 1f, WeightValues.NoRank);

        _highRankButLight = new WeightValues(1f, 1f, 1f);
        _lowRankButHeavy = new WeightValues(100f, 1f, 0f);
    }

    private RecordingLogSink _logSink;

    private List<string> _executed;

    private DualUtilityReasoner _dualUtilityReasoner;

    private StubConsideration _firstConsideration;
    private StubConsideration _secondConsideration;

    private StubOption _firstOption;
    private StubOption _secondOption;

    private WeightValues _vetoedWithALargeAddend;

    private WeightValues _heavyWeight;
    private WeightValues _lightWeight;

    private WeightValues _highRankButLight;
    private WeightValues _lowRankButHeavy;

    [Test]
    public void ReportsTheOptionsItChoosesAmongInTheOrderTheyWereAdded()
    {
        Assert.That(_dualUtilityReasoner.Options, Is.EqualTo(new OptionBase[] { _firstOption, _secondOption }));
    }

    [Test]
    public void SkipsOptionsThatAreVetoed()
    {
        // Step 1. A multiplier of 0 zeroes the weight no matter how large the addend is.
        _firstConsideration.Set(_vetoedWithALargeAddend);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void SelectsNothingWhenEveryOptionIsVetoed()
    {
        _firstConsideration.Set(_vetoedWithALargeAddend);
        _secondConsideration.Set(_vetoedWithALargeAddend);

        _dualUtilityReasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_executed, Is.Empty);
            Assert.That(_dualUtilityReasoner.SelectedOption, Is.Null);
            Assert.That(_dualUtilityReasoner.IsDone, Is.False,
                "nothing eligible is not completion, since a scoring reasoner never runs out of work");
        });
    }

    [Test]
    public void RollsNothingWhenEveryOptionIsVetoed()
    {
        // A seeded brain replays its run only while every cycle takes the same rolls. A source that took nothing rolls
        // next what an untouched source of the same seed rolls first.
        const int seed = 42;

        _firstConsideration.Set(_vetoedWithALargeAddend);
        _secondConsideration.Set(_vetoedWithALargeAddend);

        Xoshiro128RandomSource randomSource = new(seed);
        Xoshiro128RandomSource untouchedSource = new(seed);
        _dualUtilityReasoner.RandomSource = randomSource;

        _dualUtilityReasoner.Update();

        Assert.Multiple(() =>
        {
            Assert.That(randomSource.NextFloat(), Is.EqualTo(untouchedSource.NextFloat()));
            Assert.That(_dualUtilityReasoner.LastDecisionRoll, Is.EqualTo(0f));
        });
    }

    [Test]
    public void IgnoresLowerRanksNoMatterHowMuchTheyWeigh()
    {
        // Step 2. Rank is a hard tier boundary, so the option weighing a hundred times more never gets considered.
        // The 0.5 roll is chosen so that the answer changes if this step is skipped. With rank elimination the total
        // weight is First's 1 and any roll selects First. Without it the total is 101, and a roll of 0.5 lands at
        // 50.5, inside Second's share.
        _firstConsideration.Set(_highRankButLight);
        _secondConsideration.Set(_lowRankButHeavy);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.5f);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void DiscardsSurvivorsFarBelowTheBestWeight()
    {
        // Step 3. The 0.95 roll is chosen so that the answer changes if this step is skipped. With a
        // WeightScreeningFactor of 0.5, the threshold is 3 * 0.5 = 1.5, Second's weight of 1 is dropped, and the
        // total falls to 3. A roll of 0.95 lands at 2.85, inside First's share. Without
        // screening, Second survives, the total is 4, and the same roll lands at 3.8, which is Second's. A roll of 0
        // would take First either way and so would pass even if screening were deleted outright.
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.95f);
        _dualUtilityReasoner.WeightScreeningFactor = 0.5f;

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void ChoosesOnlyAmongTheHeaviestOptionsWithAScreeningFactorOfOne()
    {
        // A factor of 1 puts the threshold at the best weight itself, so the heaviest option survives by meeting it
        // exactly. Second is the heavier, and without screening a roll of 0 would land on First.
        _firstConsideration.Set(_lightWeight);
        _secondConsideration.Set(_heavyWeight);

        _dualUtilityReasoner.WeightScreeningFactor = 1f;

        _dualUtilityReasoner.Update();

        Assert.That(_dualUtilityReasoner.SelectedOption, Is.SameAs(_secondOption), "a roll of 0");

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.99f);

        _dualUtilityReasoner.Update();

        Assert.That(_dualUtilityReasoner.SelectedOption, Is.SameAs(_secondOption), "a roll of 0.99");
    }

    [Test]
    public void LandsOnTheHeavierOptionForALowRoll()
    {
        // Step 4. Weights of 3 and 1 give a total of 4. A roll of 0 falls inside First's share of [0, 3).
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "First.Select", "First.Update" }));
    }

    [Test]
    public void LandsOnTheLighterOptionForAHighRoll()
    {
        // Step 4 again. The same two options, so First owns the roll range [0, 3) and Second owns [3, 4). A roll of
        // 0.9 lands at 3.6, inside Second's share. A roll landing on either side of the boundary at 3 is what shows
        // selection walks the accumulated weights rather than taking the first candidate every time.
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.9f);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void GivesARollOnABoundaryBetweenTwoOptionsToTheLaterOne()
    {
        // Two weights of 1 give First the roll range [0, 1) and Second [1, 2), and a roll of 0.5 lands at 1 exactly.
        // Which option a roll on a boundary goes to decides what a seeded brain replays.
        _firstConsideration.Set(_lightWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.5f);

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Second.Select", "Second.Update" }));
    }

    [Test]
    public void KeepsTheSameOptionSelectedAcrossCycles()
    {
        // Selection is re-run every cycle. With nothing changing and a fixed roll the same option is selected again, so
        // it is updated a second time rather than torn down and restarted.
        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.Update();

        _dualUtilityReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void StaysWithARunningOptionThatKeepsEnoughOfItsRank()
    {
        // The Inertia pattern, cited on ExecutionHistoryConsideration. First won at rank 10 and
        // keeps six tenths of it while running, so when its own rank falls away it still sits above Second's 5.
        RunInertiaCycles(0.6f);

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Update",
            // @formatter:on
        }));
    }

    [Test]
    public void GivesUpARunningOptionThatKeepsTooLittleOfItsRank()
    {
        // Four tenths of 10 is 4, which no longer holds off Second's 5.
        RunInertiaCycles(0.4f);

        Assert.That(_executed, Is.EqualTo(new[]
        {
            // @formatter:off
            "First.Select", "First.Update",
            "First.Deselect", "Second.Select", "Second.Update",
            // @formatter:on
        }));
    }

    /// <summary>
    ///     Runs two cycles of a reasoner whose First option is selected at rank 10 on the first and loses that rank
    ///     before the second, keeping only the given <see cref="ExecutionHistoryConsideration.RankRetentionFactor" /> of it
    ///     through its <see cref="ExecutionHistoryConsideration" />.
    /// </summary>
    private void RunInertiaCycles(float rankRetentionFactor)
    {
        WeightValues rankTen = new(1f, 1f, 10f);
        WeightValues rankFive = new(1f, 1f, 5f);

        StubConsideration firstRank = new(rankTen);
        StubConsideration secondRank = new(rankFive);

        ManualTimeSource timeSource = new();
        ExecutionHistoryConsideration inertia = new()
        {
            TimeSource = timeSource,
            RankRetentionFactor = rankRetentionFactor,
        };

        RecordAction firstAction = new("First", _executed, false);
        RecordAction secondAction = new("Second", _executed, false);

        StubOption firstOption = new([firstRank, inertia], [firstAction]);
        StubOption secondOption = new([secondRank], [secondAction]);

        DualUtilityReasoner reasoner = new() { RandomSource = new FixedRandomSource(0f) };
        reasoner.AddOption(firstOption);
        reasoner.AddOption(secondOption);
        reasoner.Enable();

        reasoner.Update();

        firstRank.Set(WeightValues.Neutral);

        reasoner.Update();
    }

    [Test]
    public void SelectsInProportionToWeightOverManyCycles()
    {
        // Weights of 3 and 1 make First the selection in three cycles out of four. The seed rolls the same sequence on
        // every run and ten thousand cycles settle the share, which is what allows a band this narrow. A selection that
        // ignored weight would fail here. Always taking the heaviest option gives a share of 1, and choosing evenly
        // among the survivors gives 0.5.
        const int cycleCount = 10000;

        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_lightWeight);

        _dualUtilityReasoner.RandomSource = new Xoshiro128RandomSource(42);

        int firstSelections = 0;

        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            _dualUtilityReasoner.Update();

            if (ReferenceEquals(_dualUtilityReasoner.SelectedOption, _firstOption))
            {
                firstSelections++;
            }
        }

        double firstShare = (double)firstSelections / cycleCount;

        Assert.That(firstShare, Is.InRange(0.73, 0.77));
    }

    [Test]
    public void RecordsEachCyclesDecisionWhenAMonitorIsAttached()
    {
        ReasonerMonitor monitor = new(new MonitorSettings());
        _dualUtilityReasoner.Monitor = monitor;
        DecisionHistory history = monitor.History;

        _firstConsideration.Set(_heavyWeight);
        _secondConsideration.Set(_vetoedWithALargeAddend);
        _dualUtilityReasoner.RandomSource = new FixedRandomSource(0.75f);

        _dualUtilityReasoner.Update();

        DecisionCycle cycle = history.GetCycle(0);
        DecisionRecord first = history.GetRecord(0, 0);
        DecisionRecord second = history.GetRecord(0, 1);

        AiString firstName = "First";
        AiString secondName = "Second";

        Assert.Multiple(() =>
        {
            Assert.That(history.CycleCount, Is.EqualTo(1));
            Assert.That(cycle.SelectedName, Is.EqualTo(firstName));
            Assert.That(cycle.RecordCount, Is.EqualTo(2));
            Assert.That(cycle.Roll, Is.EqualTo(0.75f),
                "the roll that decided the cycle is recorded, not the reset value");
            Assert.That(first.OptionName, Is.EqualTo(firstName));
            Assert.That(first.Weight, Is.EqualTo(3f));
            Assert.That(first.PassedScreening, Is.True);
            Assert.That(second.OptionName, Is.EqualTo(secondName));
            Assert.That(second.Weight, Is.EqualTo(0f), "a veto zeroes the weight before screening ever looks");
        });
    }

    [Test]
    public void WarnsOnceWhenItHoldsMoreOptionsThanItsMonitorRecords()
    {
        // The fixture's two options and seven more put the reasoner one past a history's capacity of eight. A
        // filler option needs no considerations of its own, since StubOption gives it the default tuning.
        for (int filler = 1; filler <= 7; filler++)
        {
            _dualUtilityReasoner.AddOption(new StubOption([], []) { Name = $"Filler{filler}" });
        }

        _dualUtilityReasoner.Monitor = new ReasonerMonitor(new MonitorSettings());

        _dualUtilityReasoner.Update();
        _dualUtilityReasoner.Update();

        Assert.That(_logSink.Entries, Has.One.Contains("the decision history records 8 of 9 options"),
            "a second cycle with the same options does not repeat the warning");
    }

    [Test]
    public void RefusesAScreeningFactorOutsideZeroToOne()
    {
        const string snippet = """
                               <Reasoner Type="DualUtility" WeightScreeningFactor="1.5"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        DualUtilityReasoner loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Reasoner element's WeightScreeningFactor is not between 0 and 1, it reads \"1.5\"",
            }));
        });
    }

    [Test]
    public void RefusesAScreeningFactorThatIsNotANumber()
    {
        const string snippet = """
                               <Reasoner Type="DualUtility" WeightScreeningFactor="most"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        DualUtilityReasoner loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Reasoner element's WeightScreeningFactor is not a number, it reads \"most\"",
            }));
        });
    }

    [Test]
    public void LoadsItsScreeningFactorAndRandomSource()
    {
        const string snippet = """
                               <Reasoner Type="DualUtility" WeightScreeningFactor="0.6"/>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FixedRandomSource randomSource = new(0.5f);
        creationData.RandomSource = randomSource;

        DualUtilityReasoner loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.WeightScreeningFactor, Is.EqualTo(0.6f));
            Assert.That(loaded.RandomSource, Is.SameAs(randomSource));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void LoadsAScreeningFactorOfZeroAndOfOne()
    {
        // The two ends of the range loading accepts. 0 screens out nothing, and 1 keeps the best weight alone.
        const string noScreeningSnippet = """
                                          <Reasoner Type="DualUtility" WeightScreeningFactor="0"/>
                                          """;
        const string bestAloneSnippet = """
                                        <Reasoner Type="DualUtility" WeightScreeningFactor="1"/>
                                        """;
        AiCreationData noScreeningData = LoadingSpecHelpers.ParseForLoad(noScreeningSnippet, _logSink);
        AiCreationData bestAloneData = LoadingSpecHelpers.ParseForLoad(bestAloneSnippet, _logSink);

        DualUtilityReasoner screensNothing = new();
        DualUtilityReasoner keepsTheBestAlone = new();

        Assert.Multiple(() =>
        {
            Assert.That(screensNothing.Init(noScreeningData), Is.True);
            Assert.That(screensNothing.WeightScreeningFactor, Is.EqualTo(0f));
            Assert.That(keepsTheBestAlone.Init(bestAloneData), Is.True);
            Assert.That(keepsTheBestAlone.WeightScreeningFactor, Is.EqualTo(1f));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }
}

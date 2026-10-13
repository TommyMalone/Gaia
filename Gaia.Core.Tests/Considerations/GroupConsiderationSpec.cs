using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.Tests.Options;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>
///     Covers how a <see cref="GroupConsideration" /> combines what it groups under each operator, loads its
///     operators, passes the lifecycle down and reports the considerations it groups.
/// </summary>
/// <remarks>
///     The grouped considerations are stubs, so combining is asserted on values set by hand, and selection is
///     observed through the <see cref="ConsiderationBase.IsSelected" /> every consideration carries. That an option handed
///     to the group reaches the considerations inside it is covered in <see cref="OptionBaseSpec" />.
/// </remarks>
[TestFixture]
public class GroupConsiderationSpec
{
    [SetUp]
    public void BuildGroup()
    {
        _logSink = new RecordingLogSink();
        WeightValues firstReport = new(2f, 0.5f, 1f);
        WeightValues secondReport = new(3f, 0.5f, 4f);

        _firstInner = new StubConsideration(firstReport);
        _secondInner = new StubConsideration(secondReport);

        _group = new GroupConsideration([_firstInner, _secondInner]);
    }

    private RecordingLogSink _logSink;

    private StubConsideration _firstInner;
    private StubConsideration _secondInner;

    private GroupConsideration _group;

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
        GroupConsideration emptyGroup = new([]);

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
                               <Consideration Type="Group">
                                   <Consideration Type="Tuning" Addend="1"/>
                                   <Consideration Type="Tuning" Addend="2" Rank="3"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

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
    public void RefusesAGroupHoldingAConsiderationThatFailsToLoad()
    {
        const string snippet = """
                               <Consideration Type="Group">
                                   <Consideration Type="Unregistered"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Consideration element's type Unregistered is not registered",
            }));
        });
    }

    [Test]
    public void ReportsTheConsiderationsItGroupsInTheOrderTheyWereLoaded()
    {
        // Each child's addend is its place in the document, so the addends read back show the order the considerations
        // are reported in.
        const string snippet = """
                               <Consideration Type="Group">
                                   <Consideration Type="Tuning" Addend="1"/>
                                   <Consideration Type="Tuning" Addend="2"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.That(loaded.Init(creationData), Is.True);

        List<float> addends = new();

        foreach (ConsiderationBase consideration in loaded.Considerations)
        {
            addends.Add(consideration.Weights.Addend);
        }

        Assert.That(addends, Is.EqualTo(new[] { 1f, 2f }));
    }

    [Test]
    public void AllowsTheOptionUnderOrWhenOneConsiderationAllowsIt()
    {
        GroupConsideration threatened = GroupOf(BooleanOperator.Or, RankOperator.Max, Threats(true, false, false));

        threatened.Calculate();

        Assert.That(threatened.Weights.Multiplier, Is.EqualTo(1f), "one threat that holds is enough under Or");
    }

    [Test]
    public void LeavesOutAVetoingConsiderationWholeUnderOr()
    {
        StubConsideration enemyVisible = new(new WeightValues(2f, 1f, 1f));
        StubConsideration heardShot = new(new WeightValues(3f, 0f, 5f));
        StubConsideration tookDamage = new(new WeightValues(4f, 0f, 6f));
        GroupConsideration threatened = GroupOf(BooleanOperator.Or, RankOperator.Max, enemyVisible, heardShot,
            tookDamage);

        threatened.Calculate();

        Assert.That(threatened.Weights, Is.EqualTo(new WeightValues(2f, 1f, 1f)),
            "the two that veto take their addends and ranks out with their multipliers");
    }

    [TestCase(0.5f, 0.5f, 0.5f)]
    [TestCase(2f, 0.5f, 2f)]
    public void TakesTheHighestMultiplierOfTheConsiderationsThatAllowUnderOr(
        float firstMultiplier,
        float secondMultiplier,
        float highest)
    {
        StubConsideration first = new(new WeightValues(2f, firstMultiplier, 1f));
        StubConsideration second = new(new WeightValues(3f, secondMultiplier, 4f));
        StubConsideration vetoing = new(new WeightValues(5f, 0f, 7f));
        GroupConsideration group = GroupOf(BooleanOperator.Or, RankOperator.Max, first, second, vetoing);

        group.Calculate();

        Assert.That(group.Weights.Multiplier, Is.EqualTo(highest));
    }

    [Test]
    public void VetoesUnderOrWhenEveryConsiderationVetoes()
    {
        StubConsideration first = new(new WeightValues(2f, 0f, 1f));
        StubConsideration second = new(new WeightValues(3f, 0f, 4f));
        GroupConsideration group = GroupOf(BooleanOperator.Or, RankOperator.Max, first, second);

        group.Calculate();

        Assert.That(group.Weights, Is.EqualTo(new WeightValues(5f, 0f, 4f)),
            "with nothing left to leave out, the group reports the standard totals");
    }

    [Test]
    public void AllowsTheOptionUnderNotWhenNoConsiderationAllowsIt()
    {
        GroupConsideration anyThreat = GroupOf(BooleanOperator.Or, RankOperator.Max, Threats(false, false, false));
        GroupConsideration calm = GroupOf(BooleanOperator.Not, RankOperator.Max, anyThreat);

        calm.Calculate();

        Assert.That(calm.Weights.Multiplier, Is.EqualTo(1f));
    }

    [TestCase(BooleanOperator.Or, 0f)]
    [TestCase(BooleanOperator.And, 1f)]
    public void InvertsAHeldGroupAsAWholeUnderNot(BooleanOperator inner, float turned)
    {
        GroupConsideration threats = GroupOf(inner, RankOperator.Max, Threats(true, false, false));
        GroupConsideration group = GroupOf(BooleanOperator.Not, RankOperator.Max, threats);

        group.Calculate();

        Assert.That(group.Weights.Multiplier, Is.EqualTo(turned));
    }

    [TestCase(0)]
    [TestCase(2)]
    public void RefusesANotGroupThatHoldsOtherThanOneConsideration(int heldCount)
    {
        string held = string.Concat(Enumerable.Repeat("""<Consideration Type="Tuning" Multiplier="0"/>""", heldCount));
        string snippet = $"""<Consideration Type="Group" BooleanOperator="Not">{held}</Consideration>""";
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                $"Error guard.xml Line 1, the Consideration element's BooleanOperator is Not and it holds {heldCount} considerations, where a Not group holds exactly one consideration and several go inside an Or or And group",
            }));
        });
    }

    [Test]
    public void VetoesUnderNotWhenAConsiderationOnlyWeakensTheOption()
    {
        StubConsideration weakening = new(new WeightValues(0f, 0.5f, float.MinValue));
        GroupConsideration group = GroupOf(BooleanOperator.Not, RankOperator.Max, weakening);

        group.Calculate();

        Assert.That(group.Weights.Multiplier, Is.EqualTo(0f), "a multiplier above 0 allows, so Not turns it to a veto");
    }

    [Test]
    public void ChangesOnlyTheMultiplierUnderNot()
    {
        StubConsideration vetoing = new(new WeightValues(2f, 0f, 1f));
        GroupConsideration group = GroupOf(BooleanOperator.Not, RankOperator.Max, vetoing);

        group.Calculate();

        Assert.That(group.Weights, Is.EqualTo(new WeightValues(2f, 1f, 1f)), "the addend and rank pass through");
    }

    [Test]
    public void CountsAHeldTuningAsAllowingUnderOr()
    {
        StubConsideration vetoing = new(new WeightValues(3f, 0f, float.MinValue));
        GroupConsideration group = GroupOf(BooleanOperator.Or, RankOperator.Max, new TuningConsideration(), vetoing);

        group.Calculate();

        Assert.That(group.Weights, Is.EqualTo(new WeightValues(1f, 1f, float.MinValue)),
            "the Tuning's multiplier of 1 allows, so the vetoing stub is left out");
    }

    [TestCase("Or", """<Consideration Type="Tuning"/><Consideration Type="Tuning" Multiplier="0"/>""", "allows",
        "allows")]
    [TestCase("Or", """<Consideration Type="Tuning"/><Consideration Type="Tuning"/>""", "allows", "allows")]
    [TestCase("Not", """<Consideration Type="Tuning"/>""", "allows", "vetoes")]
    [TestCase("Not", """<Consideration Type="Tuning" Multiplier="0"/>""", "vetoes", "allows")]
    public void WarnsWhenAHeldTuningFixesTheGroupsAnswer(
        string booleanOperator,
        string held,
        string does,
        string outcome)
    {
        string snippet = $"""<Consideration Type="Group" BooleanOperator="{booleanOperator}">{held}</Consideration>""";
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                $"Warning guard.xml Line 1, the Consideration element's BooleanOperator is {booleanOperator} and it holds a Tuning that {does}, so it {outcome} the option on every cycle",
            }));
        });
    }

    [Test]
    public void DrawsNoWarningFromAHeldTuningThatVetoes()
    {
        // A Tuning written with a multiplier of 0 never allows, so under Or it is left out and fixes nothing. Under Not
        // the same Tuning makes the group allow on every cycle, and the test above holds that it warns.
        const string snippet = """
                               <Consideration Type="Group" BooleanOperator="Or">
                                   <Consideration Type="Tuning" Multiplier="0"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase(BooleanOperator.Or, RankOperator.Max)]
    [TestCase(BooleanOperator.And, RankOperator.Min)]
    [TestCase(BooleanOperator.And, RankOperator.Sum)]
    public void ContributesNothingWithAnEmptyGroupUnderEveryOperator(
        BooleanOperator booleanOperator,
        RankOperator rankOperator)
    {
        GroupConsideration emptyGroup = GroupOf(booleanOperator, rankOperator);

        emptyGroup.Calculate();

        Assert.That(emptyGroup.Weights, Is.EqualTo(WeightValues.Neutral));
    }

    [Test]
    public void TakesTheLowestRankUnderMin()
    {
        GroupConsideration group = GroupOf(BooleanOperator.And, RankOperator.Min, _firstInner, _secondInner,
            new StubConsideration(WeightValues.Neutral));

        group.Calculate();

        Assert.That(group.Weights.Rank, Is.EqualTo(1f), "the consideration with no rank is left out");
    }

    [Test]
    public void AddsTheRanksUnderSum()
    {
        GroupConsideration group = GroupOf(BooleanOperator.And, RankOperator.Sum, _firstInner, _secondInner,
            new StubConsideration(WeightValues.Neutral));

        group.Calculate();

        Assert.That(group.Weights.Rank, Is.EqualTo(5f), "the consideration with no rank is left out");
    }

    [Test]
    public void ReportsNoRankUnderSumWhenNoConsiderationHasOne()
    {
        GroupConsideration group = GroupOf(BooleanOperator.And, RankOperator.Sum,
            new StubConsideration(WeightValues.Neutral), new StubConsideration(WeightValues.Neutral));

        group.Calculate();

        Assert.That(group.Weights.Rank, Is.EqualTo(float.MinValue));
    }

    [Test]
    public void AddsOnlyTheRanksOfTheConsiderationsThatAllowUnderOr()
    {
        StubConsideration vetoing = new(new WeightValues(5f, 0f, 7f));
        GroupConsideration group = GroupOf(BooleanOperator.Or, RankOperator.Sum, _firstInner, _secondInner, vetoing);

        group.Calculate();

        Assert.That(group.Weights.Rank, Is.EqualTo(5f), "the vetoing consideration's rank of 7 is left out");
    }

    [Test]
    public void TakesTheDefaultOperatorsWhenTheFileNamesNeither()
    {
        // The held Tuning allows, so a warning given under the default operators would show in the log.
        const string snippet = """
                               <Consideration Type="Group">
                                   <Consideration Type="Tuning"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.BooleanOperator, Is.EqualTo(BooleanOperator.And));
            Assert.That(loaded.RankOperator, Is.EqualTo(RankOperator.Max));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase("and", BooleanOperator.And)]
    [TestCase("OR", BooleanOperator.Or)]
    [TestCase("Not", BooleanOperator.Not)]
    public void LoadsEachBooleanOperatorInAnyCasing(string text, BooleanOperator booleanOperator)
    {
        // The group holds one empty group, so a Not group loads and no Tuning draws a warning.
        string snippet = $"""
                          <Consideration Type="Group" BooleanOperator="{text}">
                              <Consideration Type="Group"/>
                          </Consideration>
                          """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.BooleanOperator, Is.EqualTo(booleanOperator));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase("max", RankOperator.Max)]
    [TestCase("MIN", RankOperator.Min)]
    [TestCase("Sum", RankOperator.Sum)]
    public void LoadsEachRankOperatorInAnyCasing(string text, RankOperator rankOperator)
    {
        string snippet = $"""
                          <Consideration Type="Group" RankOperator="{text}">
                              <Consideration Type="Group"/>
                          </Consideration>
                          """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.RankOperator, Is.EqualTo(rankOperator));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [TestCase("Xor")]
    [TestCase("1")]
    public void RefusesABooleanOperatorThatNamesNoOperator(string text)
    {
        // Enum.TryParse would read "1" as Or, so the second case holds the reader to the names.
        string snippet = $"""<Consideration Type="Group" BooleanOperator="{text}"/>""";
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                $"Error guard.xml Line 1, the Consideration element's BooleanOperator is not And, Or or Not, it reads \"{text}\"",
            }));
        });
    }

    [Test]
    public void RefusesARankOperatorThatNamesNoOperator()
    {
        const string snippet = """<Consideration Type="Group" RankOperator="Average"/>""";
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        GroupConsideration loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 1, the Consideration element's RankOperator is not Max, Min or Sum, it reads \"Average\"",
            }));
        });
    }

    [Test]
    public void LoadsAnOrGroupThroughTheFactory()
    {
        // The factory checks the element against the group's description before it loads, so the attribute has to be
        // described. The first Tuning vetoes, so under Or the group reports the second Tuning's addend alone.
        const string snippet = """
                               <Consideration Type="Group" BooleanOperator="Or">
                                   <Consideration Type="Tuning" Addend="3" Multiplier="0"/>
                                   <Consideration Type="Tuning" Addend="2"/>
                               </Consideration>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        bool created = creationData.Factories.Considerations.TryCreate(creationData, out ConsiderationBase? group);

        Assert.That(created, Is.True, "the factory accepts the BooleanOperator attribute");

        group!.Calculate();

        Assert.That(group.Weights, Is.EqualTo(new WeightValues(2f, 1f, float.MinValue)));
    }

    /// <summary>Builds a group in code over the given considerations, its operators set as loading would set them.</summary>
    private static GroupConsideration GroupOf(
        BooleanOperator booleanOperator,
        RankOperator rankOperator,
        params ConsiderationBase[] considerations)
    {
        return new GroupConsideration(considerations)
        {
            BooleanOperator = booleanOperator,
            RankOperator = rankOperator,
        };
    }

    /// <summary>The three threats of a guard's Threatened option, each true or false as given.</summary>
    /// <remarks>
    ///     Each threat reports Neutral while its variable is true and Vetoed while it is false, as a BooleanVariable with
    ///     a False element that vetoes does.
    /// </remarks>
    private static ConsiderationBase[] Threats(bool enemyVisible, bool heardShot, bool tookDamage)
    {
        return [Threat(enemyVisible), Threat(heardShot), Threat(tookDamage)];
    }

    /// <summary>One threat, reporting Neutral while it holds and Vetoed while it does not.</summary>
    private static StubConsideration Threat(bool holds)
    {
        return new StubConsideration(holds ? WeightValues.Neutral : WeightValues.Vetoed);
    }
}

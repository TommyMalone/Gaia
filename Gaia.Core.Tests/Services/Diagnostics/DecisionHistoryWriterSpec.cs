using System.Globalization;
using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Strings;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>Covers <see cref="DecisionHistoryWriter" />, the lines it writes and the order it writes them in.</summary>
/// <remarks>
///     Histories are driven through the internal recording surface the monitor uses, so a test says what is in one
///     without running a brain. The brain test is the exception and runs one, since the order its reasoners come out in is
///     the walk's rather than this writer's. Each test writes into a StringWriter of its own, which holds a StringBuilder
///     and nothing else, so there is no teardown here and no state carried between tests.
/// </remarks>
[TestFixture]
public class DecisionHistoryWriterSpec
{
    [SetUp]
    public void BuildWriter()
    {
        _writer = new DecisionHistoryWriter();
    }

    private DecisionHistoryWriter _writer;

    [Test]
    public void WritesEachCycleOldestFirstWithTheOptionsItRecorded()
    {
        DecisionHistory history = new(4, 2);
        RecordCycle(history, 7, 0.25f, "Patrol", new DecisionRecord("Patrol", 1f, 3f, true),
            new DecisionRecord("Idle", 0f, 1f, false));
        RecordCycle(history, 8, 0.5f, "Idle", new DecisionRecord("Idle", 0f, 1f, true));
        StringWriter text = new();

        _writer.Write(history, text);

        Assert.That(Lines(text), Is.EqualTo(new[]
        {
            "# gaia decision history 1",
            "# brain\tchannel\tcycles",
            "# reasoner\tpath\tcycles\tcycleCapacity\toptionCapacity",
            "# cycle\tindex\troll\tselected\trecords",
            "# record\toption\trank\tweight\tpassedScreening",
            "reasoner\t\t2\t4\t2",
            "cycle\t7\t0.25\tPatrol\t2",
            "record\tPatrol\t1\t3\ttrue",
            "record\tIdle\t0\t1\tfalse",
            "cycle\t8\t0.5\tIdle\t1",
            "record\tIdle\t0\t1\ttrue",
        }));
    }

    [Test]
    public void WritesNoneForTheRankOfAnOptionWithNoRank()
    {
        DecisionHistory history = new(4, 1);
        RecordCycle(history, 7, 0.25f, "Idle", new DecisionRecord("Idle", WeightValues.NoRank, 1f, false));
        StringWriter text = new();

        _writer.Write(history, text);

        Assert.That(Lines(text)[7], Is.EqualTo("record\tIdle\tnone\t1\tfalse"),
            "an option no consideration ranked has no rank to write");
    }

    [Test]
    public void WritesALegendAndAReasonerLineForAHistoryThatHasRecordedNothing()
    {
        StringWriter text = new();

        _writer.Write(new DecisionHistory(4, 2), text);

        Assert.Multiple(() =>
        {
            Assert.That(Lines(text), Has.Length.EqualTo(6));
            Assert.That(Lines(text)[5], Is.EqualTo("reasoner\t\t0\t4\t2"),
                "the capacities are known before a cycle is");
        });
    }

    [Test]
    public void WritesEveryReasonerOfABrainUnderALineNamingTheBrain()
    {
        RuleBasedReasoner nested = new();
        nested.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)],
            [new RecordAction("Aim", new List<string>(), false)]) { Name = "Aim" });

        RuleBasedReasoner root = new();
        root.AddOption(new StubOption([new StubConsideration(WeightValues.Neutral)], [new SubreasonerAction(nested)])
            { Name = "Snipe" });

        BrainMonitor monitor = new(new MonitorSettings());
        Brain brain = new(root) { LogChannel = "Guard", Monitor = monitor };
        brain.Enable();
        brain.Update();
        StringWriter text = new();

        _writer.Write(monitor, text);

        Assert.Multiple(() =>
        {
            Assert.That(Lines(text)[5], Is.EqualTo("brain\tGuard\t1"));
            Assert.That(Lines(text)[6], Is.EqualTo("reasoner\t\t1\t256\t8"), "the root reasoner sits under no option");
            Assert.That(Lines(text), Has.Some.EqualTo("reasoner\tSnipe\t1\t256\t8"));
        });
    }

    [Test]
    public void WritesOneReasonersHistoryUnderItsPath()
    {
        // The path is the one a brain monitor gives a reasoner nested under the Snipe option, set here by hand.
        ReasonerMonitor monitor = new(new MonitorSettings(4, 2)) { Path = "Snipe" };
        StringWriter text = new();

        _writer.Write(monitor, text);

        Assert.That(Lines(text), Is.EqualTo(new[]
        {
            "# gaia decision history 1",
            "# brain\tchannel\tcycles",
            "# reasoner\tpath\tcycles\tcycleCapacity\toptionCapacity",
            "# cycle\tindex\troll\tselected\trecords",
            "# record\toption\trank\tweight\tpassedScreening",
            "reasoner\tSnipe\t0\t4\t2",
        }));
    }

    [Test]
    public void WritesAFloatThatReadsBackAsTheSameFloat()
    {
        const float roll = 1f / 3f;

        DecisionHistory history = new(1, 1);
        RecordCycle(history, 0, roll, "Patrol", new DecisionRecord("Patrol", roll, roll, true));
        StringWriter text = new();

        _writer.Write(history, text);
        string[] fields = Lines(text)[6].Split('\t');

        Assert.That(float.Parse(fields[2], CultureInfo.InvariantCulture), Is.EqualTo(roll),
            "a dump is compared against another run, so the value has to survive the round trip");
    }

    /// <summary>What was written, as lines, without the empty one the final line feed leaves behind.</summary>
    private static string[] Lines(StringWriter text)
    {
        return text.ToString().TrimEnd('\n').Split('\n');
    }

    /// <summary>Records one cycle through the surface the monitor uses, so a test states what a history holds.</summary>
    private static void RecordCycle(
        DecisionHistory history,
        long cycleIndex,
        float roll,
        AiString selectedName,
        params DecisionRecord[] records)
    {
        history.BeginCycle(cycleIndex);

        foreach (DecisionRecord record in records)
        {
            history.Record(record);
        }

        history.EndCycle(roll, selectedName);
    }
}

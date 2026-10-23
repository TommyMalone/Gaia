using System.Runtime.CompilerServices;
using Gaia.Core.Services.Diagnostics;
using Gaia.Core.Strings;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>Covers <see cref="DecisionHistory" />, the ring it keeps and the cycles it reads back from the newest.</summary>
/// <remarks>
///     Cycles are written through the internal recording surface the monitor uses, with small capacities so the
///     wrap-around cases stay readable. What a recorded cycle means is covered where it is recorded, in
///     <see cref="Reasoners.DualUtilityReasonerSpec" />.
/// </remarks>
[TestFixture]
public class DecisionHistorySpec
{
    [SetUp]
    public void BuildHistory()
    {
        _history = new DecisionHistory(CycleCount, OptionCount);

        _patrolName = "Patrol";
        _standName = "Stand";
    }

    private const int CycleCount = 2;
    private const int OptionCount = 2;

    private DecisionHistory _history;

    private AiString _patrolName;
    private AiString _standName;

    [Test]
    public void ReadsTheNewestCycleAtZeroCyclesBack()
    {
        RecordCycle(0, 0.25f, _patrolName);
        RecordCycle(1, 0.75f, _standName);

        Assert.Multiple(() =>
        {
            Assert.That(_history.CycleCount, Is.EqualTo(2));
            Assert.That(_history.GetCycleFromNewest(0).SelectedName, Is.EqualTo(_standName));
            Assert.That(_history.GetCycleFromNewest(0).Roll, Is.EqualTo(0.75f));
            Assert.That(_history.GetCycleFromNewest(1).SelectedName, Is.EqualTo(_patrolName));
        });
    }

    [Test]
    public void OverwritesItsOldestCycleWhenFull()
    {
        RecordCycle(0, 0f, _patrolName);
        RecordCycle(1, 0f, _patrolName);
        RecordCycle(2, 0f, _standName);

        Assert.Multiple(() =>
        {
            Assert.That(_history.CycleCount, Is.EqualTo(CycleCount), "the ring holds its capacity and no more");
            Assert.That(_history.GetCycleFromNewest(0).CycleIndex, Is.EqualTo(2));
            Assert.That(_history.GetCycleFromNewest(1).CycleIndex, Is.EqualTo(1), "cycle 0 was overwritten");
        });
    }

    [Test]
    public void CapsRecordsAtItsOptionCapacity()
    {
        DecisionRecord patrol = new(_patrolName, 0f, 3f, true);
        DecisionRecord stand = new(_standName, 0f, 1f, true);
        DecisionRecord overflow = new("Overflow", 0f, 1f, true);

        _history.BeginCycle(0);
        _history.Record(patrol);
        _history.Record(stand);
        _history.Record(overflow);
        _history.EndCycle(0f, _patrolName);

        Assert.Multiple(() =>
        {
            Assert.That(_history.GetCycleFromNewest(0).RecordCount, Is.EqualTo(OptionCount));
            Assert.That(_history.GetRecordFromNewest(0, 0).OptionName, Is.EqualTo(_patrolName));
            Assert.That(_history.GetRecordFromNewest(0, 1).OptionName, Is.EqualTo(_standName));
        });
    }

    [Test]
    public void RefusesACapacityBelowOne()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new DecisionHistory(0, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new DecisionHistory(16, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void RefusesToReadACycleOutsideTheCyclesRecorded()
    {
        // One cycle in a ring of two, so -1 and 1 cycles back both land on the slot nothing has written.
        RecordCycle(0, 0f, _patrolName);

        Assert.Multiple(() =>
        {
            Assert.That(() => _history.GetCycleFromNewest(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                    .EqualTo("cyclesBack"));
            Assert.That(() => _history.GetCycleFromNewest(1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                    .EqualTo("cyclesBack"));
        });
    }

    [Test]
    public void RefusesToReadPastTheOldestCycleOnceTheRingHasWrapped()
    {
        // Three cycles in a ring of two, so cycle 0 is overwritten and two cycles back names a cycle the ring no
        // longer holds. Before the guard that call wrapped round the ring and read cycle 2, the newest, without a
        // word. Once the ring is full CycleCount equals CycleCapacity, so no mutant dies to this test alone; the cycle
        // and record tests on a ring not yet full already kill the guard removed and the wrong bound. What this test
        // holds is the wrapped ring itself, against a guard rewritten to check whether a slot was written rather than
        // CycleCount.
        RecordCycle(0, 0f, _patrolName);
        RecordCycle(1, 0f, _patrolName);
        RecordCycle(2, 0f, _standName);

        Assert.That(() => _history.GetCycleFromNewest(2),
            Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                .EqualTo("cyclesBack"));
    }

    [Test]
    public void RefusesToReadARecordFromACycleOutsideTheCyclesRecorded()
    {
        RecordCycle(0, 0f, _patrolName);

        Assert.Multiple(() =>
        {
            Assert.That(() => _history.GetRecordFromNewest(-1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                    .EqualTo("cyclesBack"));
            Assert.That(() => _history.GetRecordFromNewest(1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                    .EqualTo("cyclesBack"));
        });
    }

    [Test]
    public void RefusesToReadARecordAtAnOptionIndexOutsideTheRecordsItsCycleHolds()
    {
        // Each cycle records one option of the two it has room for, so an index of 1 is inside OptionCapacity and
        // outside RecordCount. The newest cycle sits in the second slot, so an unchecked index of -1 stays inside the
        // array, reading the first cycle's unwritten second record slot.
        RecordCycle(0, 0f, _patrolName);
        RecordCycle(1, 0f, _standName);

        Assert.Multiple(() =>
        {
            Assert.That(() => _history.GetRecordFromNewest(0, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                    .EqualTo("optionIndex"));
            Assert.That(() => _history.GetRecordFromNewest(0, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property(nameof(ArgumentException.ParamName))
                    .EqualTo("optionIndex"));
        });
    }

    [Test]
    public void AllocatesFiftySixKibibytesAtItsDefaultCapacities()
    {
        // The type's remarks quote a cost per history, and this is where that number comes from. The two structs are
        // measured rather than summed by hand, since the runtime pads DecisionCycle's float before its AiString. One
        // history is built and dropped first, so that type initialization and the JIT are not charged to the one
        // measured.
        const int cycleCapacity = 256;
        const int optionCapacity = 8;

        int recordSize = Unsafe.SizeOf<DecisionRecord>();
        int cycleSize = Unsafe.SizeOf<DecisionCycle>();
        long arrays = (long)cycleCapacity * cycleSize + (long)cycleCapacity * optionCapacity * recordSize;

        _ = new DecisionHistory();

        long before = GC.GetAllocatedBytesForCurrentThread();
        DecisionHistory measured = new();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        GC.KeepAlive(measured);

        TestContext.Out.WriteLine($"DecisionRecord {recordSize} B, DecisionCycle {cycleSize} B, " +
                                  $"arrays {arrays:N0} B, construction {allocated:N0} B");

        Assert.Multiple(() =>
        {
            Assert.That(recordSize, Is.EqualTo(24));
            Assert.That(cycleSize, Is.EqualTo(32));
            Assert.That(arrays, Is.EqualTo(57344), "the 56 KiB the remarks quote");
            Assert.That(allocated, Is.GreaterThanOrEqualTo(arrays), "all of it is allocated at construction");
        });
    }

    /// <summary>Records one whole cycle holding a single option, selected with the given roll.</summary>
    private void RecordCycle(long cycleIndex, float roll, AiString selectedName)
    {
        DecisionRecord record = new(selectedName, 0f, 1f, true);

        _history.BeginCycle(cycleIndex);
        _history.Record(record);
        _history.EndCycle(roll, selectedName);
    }
}

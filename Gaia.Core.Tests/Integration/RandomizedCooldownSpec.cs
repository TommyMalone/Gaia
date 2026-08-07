using Gaia.Core.Considerations;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Integration;

/// <summary>
///     Covers an option that cannot run again until a cooldown has passed, where the length of that cooldown is
///     rolled fresh each time the option runs.
/// </summary>
/// <remarks>
///     This spec puts the clock, the random source, a weight function and the selection lifecycle together. The
///     cooldown is a <see cref="FloatSequenceWeightFunction" /> on
///     <see cref="ExecutionHistoryConsideration.SinceLastRunWeightFunction" /> with a single
///     <see cref="FloatSequenceEntry" /> that covers the rolled cooldown and vetoes. While the time since the option last
///     ran is at or below that entry's <see cref="FloatSequenceEntry.Threshold" /> the option is vetoed, and once it
///     climbs above, the neutral <see cref="FloatSequenceWeightFunction.DefaultWeights" /> let the option run again.
/// </remarks>
/// <seealso>
///     Kevin Dill, Eugene Ray Pursel, Pat Garrity and Gino Fragomeni, "Design Patterns for the Configuration of
///     Utility-Based AI", I/ITSEC, 2012
/// </seealso>
[TestFixture]
public class RandomizedCooldownSpec
{
    [SetUp]
    public void BuildOption()
    {
        _timeSource = new ManualTimeSource();

        _cooldownWeightFunction = new FloatSequenceWeightFunction(WeightValues.Neutral)
        {
            RandomSource = new FixedRandomSource(0.25f),
        };

        FloatSequenceEntry coolDown = new(10f, 20f, WeightValues.Vetoed);
        _cooldownWeightFunction.AddEntry(coolDown);

        ExecutionHistoryConsideration consideration = new()
        {
            TimeSource = _timeSource,
            SinceLastRunWeightFunction = _cooldownWeightFunction,
        };

        List<string> executed = new();
        RecordAction recordAction = new("Option", executed);

        _option = new StubOption([consideration], [recordAction]);
    }

    private ManualTimeSource _timeSource;

    private FloatSequenceWeightFunction _cooldownWeightFunction;

    private StubOption _option;

    [Test]
    public void IsReadyBeforeItHasEverRun()
    {
        _option.Calculate();

        Assert.That(_option.IsValid, Is.True);
    }

    [Test]
    public void WaitsADifferentLengthOfTimeAfterEachRun()
    {
        // A roll of 0.25 puts the first cooldown at 12.5 seconds.
        RunTheOption();

        Seconds withinTheFirstCooldownTime = new(10f);
        _timeSource.Advance(withinTheFirstCooldownTime);
        _option.Calculate();

        Assert.That(_option.IsValid, Is.False);

        Seconds pastTheFirstCooldownTime = new(5f);
        _timeSource.Advance(pastTheFirstCooldownTime);
        _option.Calculate();

        Assert.That(_option.IsValid, Is.True);

        // Replacing RandomSource is how a spec says the next roll came out differently. A roll of 0.75 puts the
        // second cooldown at 17.5 seconds.
        _cooldownWeightFunction.RandomSource = new FixedRandomSource(0.75f);

        RunTheOption();

        // Fifteen seconds was long enough after the first run and is not long enough after the second.
        Seconds withinTheSecondCooldownTime = new(15f);
        _timeSource.Advance(withinTheSecondCooldownTime);
        _option.Calculate();

        Assert.That(_option.IsValid, Is.False);

        Seconds pastTheSecondCooldownTime = new(5f);
        _timeSource.Advance(pastTheSecondCooldownTime);
        _option.Calculate();

        Assert.That(_option.IsValid, Is.True);
    }

    /// <summary>
    ///     Selects and deselects the option. Selection rolls a fresh cooldown length, and deselection starts the cooldown
    ///     counting.
    /// </summary>
    private void RunTheOption()
    {
        _option.Select();
        _option.Deselect();
    }
}

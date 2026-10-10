using Gaia.Core.Factories;
using Gaia.Core.Services.Random;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.WeightFunctions;

/// <summary>Covers <see cref="FloatSequenceWeightFunction" />.</summary>
[TestFixture]
public class FloatSequenceWeightFunctionSpec
{
    [SetUp]
    public void BuildWeightFunction()
    {
        _logSink = new RecordingLogSink();
        _inRangeWeights = new WeightValues(2f, 1f, float.MinValue);

        _weightFunction = new FloatSequenceWeightFunction(WeightValues.Vetoed);

        FloatSequenceEntry tooCloseEntry = new(50f, WeightValues.Vetoed);
        FloatSequenceEntry inRangeEntry = new(300f, _inRangeWeights);

        _weightFunction.AddEntry(tooCloseEntry);
        _weightFunction.AddEntry(inRangeEntry);

        // Rolls its threshold between 50 and 150 as it joins a function, and again on selection. It is left off the
        // function above, for the tests about rolling to use.
        _randomizedEntry = new FloatSequenceEntry(50f, 150f, _inRangeWeights);
    }

    private RecordingLogSink _logSink;

    private WeightValues _inRangeWeights;

    private FloatSequenceWeightFunction _weightFunction;
    private FloatSequenceEntry _randomizedEntry;

    [Test]
    public void ReportsTheFirstEntryTheInputFallsWithin()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateFloat(25f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_weightFunction.CalculateFloat(200f), Is.EqualTo(_inRangeWeights));
        });
    }

    [Test]
    public void ReportsMinimumThresholdAsExclusiveAndMaximumThresholdAsInclusive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateFloat(50f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_weightFunction.CalculateFloat(50.01f), Is.EqualTo(_inRangeWeights));
            Assert.That(_weightFunction.CalculateFloat(300f), Is.EqualTo(_inRangeWeights));
        });
    }

    [Test]
    public void ReportsTheDefaultAboveEveryThreshold()
    {
        Assert.That(_weightFunction.CalculateFloat(500f), Is.EqualTo(WeightValues.Vetoed));
    }

    [Test]
    public void ReportsTheDefaultWhenThereAreNoEntries()
    {
        FloatSequenceWeightFunction emptyWeightFunction = new(WeightValues.Vetoed);

        Assert.That(emptyWeightFunction.CalculateFloat(100f), Is.EqualTo(WeightValues.Vetoed));
    }

    [Test]
    public void LeavesFixedThresholdsAloneWhenTheOptionIsSelected()
    {
        // The fixture's entries were given a single threshold each, which is the common case, so selection changes
        // nothing about what they match.
        _weightFunction.RandomSource = new FixedRandomSource(0.9f);

        _weightFunction.Select();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.CalculateFloat(50f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(_weightFunction.CalculateFloat(200f), Is.EqualTo(_inRangeWeights));
            Assert.That(_weightFunction.CalculateFloat(500f), Is.EqualTo(WeightValues.Vetoed));
        });
    }

    [Test]
    public void RollsNothingForAnEntryWithAFixedThreshold()
    {
        // A seeded brain replays its run only while every selection takes the same rolls. A source that took nothing
        // rolls next what an untouched source of the same seed rolls first.
        const int seed = 42;

        Xoshiro128RandomSource randomSource = new(seed);
        Xoshiro128RandomSource untouchedSource = new(seed);
        _weightFunction.RandomSource = randomSource;

        _weightFunction.Select();

        Assert.That(randomSource.NextFloat(), Is.EqualTo(untouchedSource.NextFloat()));
    }

    [Test]
    public void RollsARandomizedEntryAsItIsAdded()
    {
        FloatSequenceWeightFunction weightFunction = new(WeightValues.Vetoed);
        weightFunction.RandomSource = new FixedRandomSource(0.9f);

        // Threshold is ThresholdMin, 50, until the entry joins a function, so 100 falls past it.
        Assert.That(_randomizedEntry.Threshold, Is.EqualTo(50f));

        weightFunction.AddEntry(_randomizedEntry);

        // A roll of 0.9 puts the threshold at 140, without waiting for a selection.
        Assert.That(_randomizedEntry.Threshold, Is.EqualTo(140f));
        Assert.That(weightFunction.CalculateFloat(100f), Is.EqualTo(_inRangeWeights));
    }

    [Test]
    public void RollsARandomizedEntryAgainWhenTheOptionIsSelected()
    {
        FloatSequenceWeightFunction weightFunction = new(WeightValues.Vetoed);
        weightFunction.RandomSource = new FixedRandomSource(0.9f);

        weightFunction.AddEntry(_randomizedEntry);

        // The second source answers the selection's roll, so a threshold that moves shows the reroll happened.
        weightFunction.RandomSource = new FixedRandomSource(0.1f);
        weightFunction.Select();

        Assert.That(_randomizedEntry.Threshold, Is.EqualTo(60f));
    }

    [Test]
    public void HoldsTheFirstRollOfAnEntryThatDoesNotRerollOnSelect()
    {
        FloatSequenceWeightFunction weightFunction = new(WeightValues.Vetoed);
        weightFunction.RandomSource = new FixedRandomSource(0.9f);

        _randomizedEntry.RerollOnSelect = false;
        weightFunction.AddEntry(_randomizedEntry);

        weightFunction.RandomSource = new FixedRandomSource(0.1f);
        weightFunction.Select();

        // The entry rolled once as it was added and keeps that value for the life of the function.
        Assert.That(_randomizedEntry.Threshold, Is.EqualTo(140f));
    }

    [Test]
    public void RejectsAnEntryOutOfOrder()
    {
        // The entries are tested first to last, so one added out of order would shadow everything after it.
        FloatSequenceEntry earlierEntry = new(100f, _inRangeWeights);

        Assert.That(() => _weightFunction.AddEntry(earlierEntry), Throws.ArgumentException);
    }

    [Test]
    public void AcceptsAnEntryAtTheSameThresholdAsTheLast()
    {
        // Equal thresholds are harmless. The earlier entry answers, and the later one is never reached.
        FloatSequenceEntry repeatedEntry = new(300f, WeightValues.Vetoed);

        Assert.That(() => _weightFunction.AddEntry(repeatedEntry), Throws.Nothing);
    }

    [Test]
    public void RejectsARolledRangeThatOverlapsTheEntryBefore()
    {
        // An entry rolling between 250 and 350 would sometimes land below the 300 entry and sometimes above it.
        FloatSequenceEntry overlappingEntry = new(250f, 350f, WeightValues.Vetoed);

        Assert.That(() => _weightFunction.AddEntry(overlappingEntry), Throws.ArgumentException);
    }

    [Test]
    public void RejectsAnEntryThatCouldRollBelowTheRangeBefore()
    {
        FloatSequenceWeightFunction weightFunction = new(WeightValues.Vetoed);

        FloatSequenceEntry tooEarlyEntry = new(120f, 200f, WeightValues.Vetoed);
        FloatSequenceEntry lateEnoughEntry = new(150f, 200f, WeightValues.Vetoed);

        weightFunction.AddEntry(_randomizedEntry);

        Assert.Multiple(() =>
        {
            Assert.That(() => weightFunction.AddEntry(tooEarlyEntry), Throws.ArgumentException);
            Assert.That(() => weightFunction.AddEntry(lateEnoughEntry), Throws.Nothing);
        });
    }

    [Test]
    public void RejectsARangeWhoseMaximumIsBelowItsMinimum()
    {
        Assert.That(() => new FloatSequenceEntry(150f, 50f, _inRangeWeights), Throws.ArgumentException);
    }

    [Test]
    public void ReportsWhichEntriesRollTheirThreshold()
    {
        FloatSequenceEntry fixedEntry = new(100f, _inRangeWeights);

        Assert.Multiple(() =>
        {
            Assert.That(fixedEntry.IsRandomized, Is.False);
            Assert.That(_randomizedEntry.IsRandomized, Is.True);
        });
    }

    [Test]
    public void LoadsItsEntriesAndDefaultInOrder()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry Threshold="50" Multiplier="0"/>
                                   <Entry Threshold="300" Addend="1"/>
                                   <Default Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.CalculateFloat(40f), Is.EqualTo(WeightValues.Vetoed), "below the first threshold");
            Assert.That(loaded.CalculateFloat(100f), Is.EqualTo(new WeightValues(1f, 1f, float.MinValue)),
                "between the thresholds");
            Assert.That(loaded.CalculateFloat(400f), Is.EqualTo(WeightValues.Vetoed), "above both, the default");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RollsALoadedEntryAsItLoads()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="10" ThresholdMax="20" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        creationData.RandomSource = new FixedRandomSource(0.5f);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            // A roll of 0.5 lands the threshold at 15, between the loaded 10 and 20, before any selection.
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(loaded.CalculateFloat(14f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(loaded.CalculateFloat(16f), Is.EqualTo(WeightValues.Neutral));
        });
    }

    [Test]
    public void RefusesRerollOnSelectOnAFixedThreshold()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry Threshold="50" RerollOnSelect="false" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Entry element sets RerollOnSelect on a fixed threshold",
            }), "a fixed entry has nothing to reroll");
        });
    }

    [Test]
    public void RefusesARerollOnSelectThatIsNotABoolean()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="10" ThresholdMax="20" RerollOnSelect="sometimes" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Entry element's RerollOnSelect is not true or false, it reads \"sometimes\"",
            }));
        });
    }

    [Test]
    public void LoadsAnEntryThatHoldsItsFirstRoll()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="10" ThresholdMax="20" RerollOnSelect="false" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        creationData.RandomSource = new FixedRandomSource(0.5f);

        FloatSequenceWeightFunction loaded = new();

        Assert.That(loaded.Init(creationData), Is.True);

        // Loading rolled the threshold to 15. A selection under a source that would roll 19 leaves it there.
        loaded.RandomSource = new FixedRandomSource(0.9f);
        loaded.Select();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.CalculateFloat(14f), Is.EqualTo(WeightValues.Vetoed));
            Assert.That(loaded.CalculateFloat(16f), Is.EqualTo(WeightValues.Neutral), "the entry held 15");
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RerollsALoadedRangeOnSelectUnlessToldNotTo()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="10" ThresholdMax="20" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        creationData.RandomSource = new FixedRandomSource(0.5f);

        FloatSequenceWeightFunction loaded = new();

        Assert.That(loaded.Init(creationData), Is.True);

        // Loading rolled the threshold to 15. The entry writes no RerollOnSelect, so a selection under a source of 0.9
        // rolls it again, to 19.
        loaded.RandomSource = new FixedRandomSource(0.9f);
        loaded.Select();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.CalculateFloat(18.5f), Is.EqualTo(WeightValues.Vetoed), "the entry rolled 19");
            Assert.That(loaded.CalculateFloat(19.5f), Is.EqualTo(WeightValues.Neutral));
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesAnEntryMixingItsThresholdForms()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry Threshold="50" ThresholdMin="10" ThresholdMax="20"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Entry element mixes Threshold with ThresholdMin and ThresholdMax",
            }));
        });
    }

    [Test]
    public void RefusesAnEntryWithNoThresholdOrHalfARange()
    {
        (string entry, string missingAttribute)[] cases =
        {
            ("""<Entry Multiplier="0"/>""", "Threshold"),
            ("""<Entry ThresholdMin="10" Multiplier="0"/>""", "ThresholdMax"),
            ("""<Entry ThresholdMax="20" Multiplier="0"/>""", "ThresholdMin"),
        };

        Assert.Multiple(() =>
        {
            foreach ((string entry, string missingAttribute) in cases)
            {
                RecordingLogSink logSink = new();
                string snippet = $"""
                                  <WeightFunction Type="FloatSequence">
                                      {entry}
                                  </WeightFunction>
                                  """;
                AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, logSink);

                FloatSequenceWeightFunction loaded = new();

                Assert.That(loaded.Init(creationData), Is.False, entry);
                Assert.That(logSink.Entries, Is.EqualTo(new[]
                {
                    $"Error guard.xml Line 2, the Entry element has no {missingAttribute} attribute",
                }), entry);
            }
        });
    }

    [Test]
    public void RefusesAnEntryOutOfOrder()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry Threshold="300" Addend="1"/>
                                   <Entry Threshold="50" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Entry element is out of order, it starts at 50 and the entry before it reaches 300",
            }));
        });
    }

    [Test]
    public void RefusesAnEntryThatStartsInsideTheRolledRangeBeforeIt()
    {
        // A source of 0 rolls the range to 10, so 15 starts above the rolled threshold and inside the range. A check
        // against the rolled threshold would pass the entry on to AddEntry, which throws.
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="10" ThresholdMax="20" Multiplier="0"/>
                                   <Entry Threshold="15" Addend="1"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);
        creationData.RandomSource = new FixedRandomSource(0f);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 3, the Entry element is out of order, it starts at 15 and the entry before it reaches 20",
            }));
        });
    }

    [Test]
    public void LoadsAnEntryStartingWhereTheRangeBeforeItEnds()
    {
        // Bands that touch are what a designer writes, each starting where the one before it ends.
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="10" ThresholdMax="20" Multiplier="0"/>
                                   <Entry Threshold="20" Addend="1"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.True);
            Assert.That(_logSink.Entries, Is.Empty);
        });
    }

    [Test]
    public void RefusesARangeWhoseMaxIsBelowItsMin()
    {
        const string snippet = """
                               <WeightFunction Type="FloatSequence">
                                   <Entry ThresholdMin="20" ThresholdMax="10" Multiplier="0"/>
                               </WeightFunction>
                               """;
        AiCreationData creationData = LoadingSpecHelpers.ParseForLoad(snippet, _logSink);

        FloatSequenceWeightFunction loaded = new();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Init(creationData), Is.False);
            Assert.That(_logSink.Entries, Is.EqualTo(new[]
            {
                "Error guard.xml Line 2, the Entry element's ThresholdMax 10 is below its ThresholdMin 20",
            }));
        });
    }
}

using Gaia.Core.Services.Diagnostics;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>Covers <see cref="MonitorSettings" />, the capacities and warning windows it carries and the ones it refuses.</summary>
[TestFixture]
public class MonitorSettingsSpec
{
    [Test]
    public void HoldsTheCapacitiesItWasMadeWith()
    {
        MonitorSettings settings = new(16, 4);

        Assert.Multiple(() =>
        {
            Assert.That(settings.CycleCapacity, Is.EqualTo(16));
            Assert.That(settings.OptionCapacity, Is.EqualTo(4));
        });
    }

    [Test]
    public void AcceptsCapacitiesOfOne()
    {
        // One cycle of one option is the smallest history there is, and the least a capacity may be.
        MonitorSettings settings = new(1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(settings.CycleCapacity, Is.EqualTo(1));
            Assert.That(settings.OptionCapacity, Is.EqualTo(1));
        });
    }

    [Test]
    public void WarnsAboutADoneOptionAndAnEmptySelectionAfterSixtyCyclesByDefault()
    {
        MonitorSettings settings = new();

        Assert.Multiple(() =>
        {
            Assert.That(settings.DoneWarningWindow, Is.EqualTo(60));
            Assert.That(settings.NothingEligibleForSelectionWarningWindow, Is.EqualTo(60));
        });
    }

    [Test]
    public void HoldsTheWarningWindowsItWasMadeWith()
    {
        MonitorSettings settings = new(doneWarningWindow: 30, nothingEligibleForSelectionWarningWindow: 0);

        Assert.Multiple(() =>
        {
            Assert.That(settings.DoneWarningWindow, Is.EqualTo(30));
            Assert.That(settings.NothingEligibleForSelectionWarningWindow, Is.Zero, "0 turns a window off");
        });
    }

    [Test]
    public void RefusesACapacityBelowOne()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new MonitorSettings(0, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MonitorSettings(16, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void RefusesANegativeWarningWindow()
    {
        // 0 already means off, so a negative window is a mistake rather than a stronger off.
        Assert.Multiple(() =>
        {
            Assert.That(() => new MonitorSettings(doneWarningWindow: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MonitorSettings(nothingEligibleForSelectionWarningWindow: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }
}

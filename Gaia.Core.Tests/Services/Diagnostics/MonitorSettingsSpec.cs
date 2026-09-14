using Gaia.Core.Services.Diagnostics;

namespace Gaia.Core.Tests.Services.Diagnostics;

/// <summary>Covers <see cref="MonitorSettings" />, the capacities it carries and the ones it refuses.</summary>
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
    public void RefusesACapacityBelowOne()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new MonitorSettings(0, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MonitorSettings(16, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }
}

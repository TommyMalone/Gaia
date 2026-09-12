using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Factories;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves a host can swap a character's brain without the character losing what it knew, using only the public
///     surface. The document is parsed once and both brains load from the same root.
/// </summary>
/// <remarks>
///     The swap is what a reload does. A designer edits the file, the host builds a new brain, and the character
///     carries on. The swapped-in brain is loaded with the first brain's blackboard. On an empty blackboard every variable
///     the file reads would be missing and report neutral values, so the sniper's rule-based reasoner would select
///     Retreat, its first option. Selecting Hide therefore shows the swapped-in brain decided from values written before
///     it existed.
/// </remarks>
[TestFixture]
public class BrainSwapHostSpec
{
    [Test]
    public void SwapsABrainWithoutLosingWhatTheCharacterKnew()
    {
        ManualTimeSource timeSource = new();
        List<string> selections = new();
        ListLogSink logSink = new();

        Actor sniper = new() { Name = "Sniper", Position = new Vector3(0f, 10f, 0f) };

        FactorySet factories = new();
        factories.Actions.Register("Record", () => new HostRecordAction(selections));

        GaiaLoader loader = new(factories, timeSource, logSink: logSink);

        string configurationText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sniper.xml"));

        Assert.That(loader.TryParse(configurationText, "sniper.xml", out SpecificationNode? root), Is.True);

        // The first brain, and the knowledge the host's sensors write for it.
        Assert.That(loader.TryLoadBrain(root!, out Brain? first, self: sniper), Is.True);

        Brain original = first!;
        original.Blackboard.SetBool("IsUnderFire", false);
        original.Blackboard.SetEntityList("KillZoneContacts", new List<EntityBase>());
        original.Blackboard.SetEntityList("RetreatRouteContacts", new List<EntityBase>());

        // The swap.
        Assert.That(loader.TryLoadBrain(root!, out Brain? second, self: sniper, brainBlackboard: original.Blackboard),
            Is.True);

        Brain swapped = second!;
        sniper.Brain = swapped;
        swapped.Enable();

        timeSource.Advance(new Seconds(1f));
        sniper.Update();

        Assert.Multiple(() =>
        {
            Assert.That(swapped.Blackboard, Is.SameAs(original.Blackboard),
                "the character kept its blackboard across the swap");
            Assert.That(selections, Is.EqualTo(new[] { "Hide" }),
                "the swapped-in brain decides from what was already known");
        });
    }
}

using Gaia.Core.Blackboards;
using Gaia.Core.Strings;
using Gaia.Core.Tests.DataStores;

namespace Gaia.Core.Tests.Blackboards;

/// <summary>Covers what separates the two kinds of blackboard, which is who can see what.</summary>
/// <remarks>
///     Both are data stores and how a store holds values is covered in <see cref="DataStoreBaseSpec" />, so what is
///     asserted here is scope. One character's values stay its own, and the two scopes do not collide when they use the
///     same name.
/// </remarks>
[TestFixture]
public class BlackboardSpec
{
    [SetUp]
    public void BuildBlackboards()
    {
        _worldBlackboard = new WorldBlackboard();

        _firstBrainBlackboard = new BrainBlackboard();
        _secondBrainBlackboard = new BrainBlackboard();
    }

    private WorldBlackboard _worldBlackboard;

    private BrainBlackboard _firstBrainBlackboard;
    private BrainBlackboard _secondBrainBlackboard;

    [Test]
    public void KeepsOneCharactersValuesFromAnother()
    {
        // Two characters can believe different things about the same target under the same name, since each writes to
        // a blackboard of its own.
        _firstBrainBlackboard.SetFloat("TargetDistance", 50f);
        _secondBrainBlackboard.SetFloat("TargetDistance", 300f);

        Assert.Multiple(() =>
        {
            Assert.That(_firstBrainBlackboard.TryGetFloat("TargetDistance", out float firstDistance), Is.True);
            Assert.That(firstDistance, Is.EqualTo(50f));

            Assert.That(_secondBrainBlackboard.TryGetFloat("TargetDistance", out float secondDistance), Is.True);
            Assert.That(secondDistance, Is.EqualTo(300f));
        });
    }

    [Test]
    public void KnowsNothingOfANameOnlyAnotherCharacterWrote()
    {
        _firstBrainBlackboard.SetFloat("TargetDistance", 50f);

        Assert.That(_secondBrainBlackboard.Contains("TargetDistance"), Is.False);
    }

    [Test]
    public void KeepsTheWorldAndACharacterApartWhenTheyUseTheSameName()
    {
        // The two scopes are separate stores, so a name means whatever its own scope says it means.
        _worldBlackboard.SetString("Alert", "None");
        _firstBrainBlackboard.SetString("Alert", "ThreatNearby");

        Assert.Multiple(() =>
        {
            Assert.That(_worldBlackboard.TryGetString("Alert", out AiString worldAlert), Is.True);
            Assert.That(worldAlert.Text, Is.EqualTo("None"));

            Assert.That(_firstBrainBlackboard.TryGetString("Alert", out AiString brainAlert), Is.True);
            Assert.That(brainAlert.Text, Is.EqualTo("ThreatNearby"));
        });
    }

    [Test]
    public void ShowsEveryHolderWhatWasWrittenToTheWorld()
    {
        // Nothing here enforces that there is only one WorldBlackboard. Handing the same instance to everyone is the
        // host's job, and this records what that provides.
        WorldBlackboard sharedByAnotherComponent = _worldBlackboard;

        _worldBlackboard.SetBool("IsRaining", true);

        Assert.Multiple(() =>
        {
            Assert.That(sharedByAnotherComponent.TryGetBool("IsRaining", out bool isRaining), Is.True);
            Assert.That(isRaining, Is.True);
        });
    }
}

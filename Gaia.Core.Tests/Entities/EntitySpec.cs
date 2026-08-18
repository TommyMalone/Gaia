using Gaia.Core.Brains;
using Gaia.Core.Entities;
using Gaia.Core.Reasoners;
using Gaia.Core.Spatial;
using Gaia.Core.Strings;
using Gaia.Core.Tests.DataStores;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Entities;

/// <summary>Covers the three kinds of entity, and what separates each from a bare data store with a position.</summary>
/// <remarks>
///     An actor may or may not have a brain, a contact is one character's belief rather than the truth, and a threat
///     outlives whatever caused it. How an entity holds values is covered in <see cref="DataStoreBaseSpec" />.
/// </remarks>
[TestFixture]
public class EntitySpec
{
    [SetUp]
    public void BuildEntities()
    {
        _executed = new List<string>();

        RecordAction action = new("Idle", _executed, false);
        StubOption option = new([], [action]);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(option);

        _brain = new Brain { RootReasoner = reasoner };

        _guard = new Actor
        {
            Name = "Guard",
            Brain = _brain,
        };

        // A player with no brain, and a shooter whose round has landed somewhere other than where it was fired from.
        _player = new Actor { Name = "Player" };

        _impactPosition = new Vector3(4f, 0f, 9f);
        _shooterPosition = new Vector3(100f, 0f, 100f);
        _shooter = new StubEntity("Shooter") { Position = _shooterPosition };
    }

    private List<string> _executed;

    private Brain _brain;
    private Actor _guard;
    private Actor _player;

    private Vector3 _impactPosition;
    private Vector3 _shooterPosition;
    private StubEntity _shooter;

    [Test]
    public void CarriesAFacingAsAnActor()
    {
        // A quarter turn to the left about the up axis. A quaternion stores half its rotation angle, so the sine and
        // cosine here are taken at 45 degrees rather than 90.
        float halfAngle = MathF.PI / 4f;
        Rotation facingLeft = new(0f, -MathF.Sin(halfAngle), 0f, MathF.Cos(halfAngle));

        _guard.Rotation = facingLeft;

        Assert.That(_guard.Rotation, Is.EqualTo(facingLeft));
    }

    [Test]
    public void FacesForwardAsAnActorUntilTold()
    {
        Actor actor = new();

        Assert.That(actor.Rotation, Is.EqualTo(Rotation.Identity));
    }

    [Test]
    public void RunsItsBrainWhenUpdated()
    {
        _brain.Enable();

        _guard.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Idle.Select", "Idle.Update" }));
    }

    [Test]
    public void DoesNothingWhenUpdatedWithNoBrain()
    {
        // A player's actor may have no brain at all, and the game still updates every actor the same way.
        Assert.That(() => _player.Update(), Throws.Nothing);
    }

    [Test]
    public void DoesNothingWhenUpdatedWhileItsBrainIsDisabled()
    {
        // A character that switches between AI and player control keeps its brain but disables it.
        _brain.Enable();
        _brain.Disable();

        _executed.Clear();

        _guard.Update();

        Assert.That(_executed, Is.Empty);
    }

    [Test]
    public void LetsAContactDisagreeWithTheActorItDescribes()
    {
        // The stolen uniform. The player is a rebel who has put on a garrison uniform. The player's actor records the
        // truth, the guard's contact for the player records what the guard was fooled into believing, and the guard
        // acts on its own contact.
        _player.SetString("Side", "Rebel");

        Contact guardsContactForPlayer = new() { Name = "Player" };
        guardsContactForPlayer.SetString("Side", "Garrison");

        List<EntityBase> contacts = [guardsContactForPlayer];
        _brain.Blackboard.SetEntityList("Contacts", contacts);

        Assert.Multiple(() =>
        {
            Assert.That(_player.TryGetString("Side", out AiString actualSide), Is.True);
            Assert.That(actualSide.Text, Is.EqualTo("Rebel"));

            Assert.That(_brain.Blackboard.TryGetEntityList("Contacts", out List<EntityBase>? believed), Is.True);
            Assert.That(believed![0].TryGetString("Side", out AiString believedSide), Is.True);
            Assert.That(believedSide.Text, Is.EqualTo("Garrison"));
        });
    }

    [Test]
    public void KeepsOneCharactersContactsFromAnother()
    {
        // Two guards each keep their own contact for the same player, and each believes something different. Fooling
        // one does not fool the other, so each guard still acts on its own belief.
        Contact firstGuardsContact = new() { Name = "Player" };
        firstGuardsContact.SetString("Side", "Garrison");

        Contact secondGuardsContact = new() { Name = "Player" };
        secondGuardsContact.SetString("Side", "Rebel");

        Brain secondGuardsBrain = new();

        List<EntityBase> firstGuardsContacts = [firstGuardsContact];
        List<EntityBase> secondGuardsContacts = [secondGuardsContact];

        _brain.Blackboard.SetEntityList("Contacts", firstGuardsContacts);
        secondGuardsBrain.Blackboard.SetEntityList("Contacts", secondGuardsContacts);

        Assert.Multiple(() =>
        {
            Assert.That(_brain.Blackboard.TryGetEntityList("Contacts", out List<EntityBase>? first), Is.True);
            Assert.That(first![0].TryGetString("Side", out AiString firstBelieved), Is.True);
            Assert.That(firstBelieved.Text, Is.EqualTo("Garrison"));

            Assert.That(secondGuardsBrain.Blackboard.TryGetEntityList("Contacts", out List<EntityBase>? second),
                Is.True);
            Assert.That(second![0].TryGetString("Side", out AiString secondBelieved), Is.True);
            Assert.That(secondBelieved.Text, Is.EqualTo("Rebel"));
        });
    }

    [Test]
    public void LetsAThreatHaveNoSource()
    {
        // An explosion is a position the character should react to, with nothing behind it.
        Threat explosion = new()
        {
            Name = "Explosion",
            Position = _impactPosition,
        };

        Assert.Multiple(() =>
        {
            Assert.That(explosion.Source, Is.Null);
            Assert.That(explosion.HasValidSource, Is.False);
            Assert.That(explosion.IsValid, Is.True);
            Assert.That(explosion.Position, Is.EqualTo(_impactPosition));
        });
    }

    [Test]
    public void KeepsAThreatValidAfterItsSourceIsInvalidated()
    {
        // The shooter has left the character's awareness, but the impact is still something to react to.
        Threat bulletImpact = new()
        {
            Name = "Impact",
            Source = _shooter,
            Position = _impactPosition,
        };

        _shooter.IsValid = false;

        Assert.Multiple(() =>
        {
            Assert.That(bulletImpact.HasValidSource, Is.False);
            Assert.That(bulletImpact.IsValid, Is.True);
        });
    }

    [Test]
    public void KeepsAThreatsPositionApartFromItsSources()
    {
        // Where a round landed is not where it was fired from.
        Threat bulletImpact = new()
        {
            Name = "Impact",
            Source = _shooter,
            Position = _impactPosition,
        };

        Assert.Multiple(() =>
        {
            Assert.That(bulletImpact.HasValidSource, Is.True);
            Assert.That(bulletImpact.Position, Is.EqualTo(_impactPosition));
            Assert.That(bulletImpact.Source!.Position, Is.EqualTo(_shooterPosition));
        });
    }

    [Test]
    public void RunsOnlyWhatItsBrainsReasonerSelects()
    {
        // An actor's brain is a real decision tree rather than a flag, so a vetoed option is not run.
        StubConsideration veto = new(WeightValues.Vetoed);
        RecordAction vetoedAction = new("Vetoed", _executed, false);
        StubOption vetoedOption = new([veto], [vetoedAction]);

        RuleBasedReasoner reasoner = new();
        reasoner.AddOption(vetoedOption);

        Brain brain = new() { RootReasoner = reasoner };
        Actor actor = new() { Brain = brain };

        brain.Enable();
        actor.Update();

        Assert.That(_executed, Is.Empty);
    }
}

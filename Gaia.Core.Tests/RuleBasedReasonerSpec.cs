using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Reasoner;
using Gaia.Core.Temp;

namespace Gaia.Core.Tests;

/// <summary>
///     Covers RuleBasedReasoner's selection logic: it walks its options in configuration order and takes the first
///     valid one, ignoring rank. The scenario is the sniper's top-level decision from Game AI Pro 3 ch. 8. Considerations
///     are stubbed rather than queried from a world, so a failure here points at the reasoner rather than at a
///     consideration.
/// </summary>
/// <remarks>
///     Considerations are stubbed rather than queried from a world, so a failure here points at the reasoner rather
///     than at a consideration.
/// </remarks>
[TestFixture]
public class RuleBasedReasonerSpec
{
    [SetUp]
    public void BuildSniper()
    {
        _executed = new List<string>();

        _isUnderFire = new StubConsideration();
        _retreatPathIsClear = new StubConsideration();
        _targetIsInKillZone = new StubConsideration();
        _shotCooldownHasElapsed = new StubConsideration();

        _ruleBasedReasoner = new RuleBasedReasoner();

        // Options are added in priority order. Several may be valid at once; the reasoner takes the first.

        // "If the sniper is under fire and its line of retreat is clear, retreat."
        _ruleBasedReasoner.AddOption(BuildOption(new[] { _isUnderFire, _retreatPathIsClear },
            new ActionBase[] { new RecordAction("Retreat", _executed) }));

        // "If the sniper is under fire, fight back."
        _ruleBasedReasoner.AddOption(BuildOption(new[] { _isUnderFire },
            new ActionBase[] { new RecordAction("FightBack", _executed) }));

        // "If the line of retreat is clear, there is a valid target in the kill zone,
        //  and a minute or two has elapsed since the last shot, snipe."
        _ruleBasedReasoner.AddOption(BuildOption(
            new ConsiderationBase[] { _retreatPathIsClear, _targetIsInKillZone, _shotCooldownHasElapsed },
            new ActionBase[] { new RecordAction("Snipe", _executed) }));

        // "Hide."
        // No considerations of its own. Init supplies a default TuningConsideration with an addend of 1, which is
        // what makes a bare fallback option selectable at all.
        _ruleBasedReasoner.AddOption(BuildOption(new StubConsideration[] { },
            new ActionBase[] { new RecordAction("Hide", _executed) }));
    }

    private List<string> _executed;

    private StubConsideration _isUnderFire;
    private StubConsideration _retreatPathIsClear;
    private StubConsideration _targetIsInKillZone;
    private StubConsideration _shotCooldownHasElapsed;

    private RuleBasedReasoner _ruleBasedReasoner;

    [Test]
    public void RetreatsWhenUnderFireAndThePathIsClear()
    {
        // Retreat and FightBack are both valid here. Retreat wins because it was added first.
        _isUnderFire.Pass();
        _retreatPathIsClear.Pass();
        _targetIsInKillZone.Veto();
        _shotCooldownHasElapsed.Veto();

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Retreat.Select", "Retreat.Update" }));
    }

    [Test]
    public void FightsBackWhenUnderFireAndTheRetreatIsBlocked()
    {
        _isUnderFire.Pass();
        _retreatPathIsClear.Veto();
        _targetIsInKillZone.Veto();
        _shotCooldownHasElapsed.Veto();

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "FightBack.Select", "FightBack.Update" }));
    }

    [Test]
    public void SnipesWhenSafeWithATargetAndTheCooldownElapsed()
    {
        _isUnderFire.Veto();
        _retreatPathIsClear.Pass();
        _targetIsInKillZone.Pass();
        _shotCooldownHasElapsed.Pass();

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Snipe.Select", "Snipe.Update" }));
    }

    [Test]
    public void HidesWhenNothingElseIsValid()
    {
        _isUnderFire.Veto();
        _retreatPathIsClear.Pass();
        _targetIsInKillZone.Veto();
        _shotCooldownHasElapsed.Veto();

        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Hide.Select", "Hide.Update" }));
    }

    [Test]
    public void DoesNotRestartAnOptionThatIsAlreadyRunning()
    {
        // Select and Deselect are transitions; Update is the per-frame call. An option that stays valid across
        // ticks is selected once and updated every tick.
        _isUnderFire.Veto();
        _retreatPathIsClear.Pass();
        _targetIsInKillZone.Pass();
        _shotCooldownHasElapsed.Pass();

        _ruleBasedReasoner.Update();
        _ruleBasedReasoner.Update();

        Assert.That(_executed, Is.EqualTo(new[] { "Snipe.Select", "Snipe.Update", "Snipe.Update" }));
    }

    [Test]
    public void DeselectsTheRunningOptionWhenAHigherPriorityOptionBecomesValid()
    {
        // Sniping until the sniper takes fire, at which point Retreat outranks it by position.
        _isUnderFire.Veto();
        _retreatPathIsClear.Pass();
        _targetIsInKillZone.Pass();
        _shotCooldownHasElapsed.Pass();

        _ruleBasedReasoner.Update();

        _isUnderFire.Pass();

        _ruleBasedReasoner.Update();

        // The outgoing option is deselected before the incoming one is selected, and only the incoming option is
        // updated on the tick that swaps them.
        Assert.That(_executed,
            Is.EqualTo(new[]
            {
                "Snipe.Select", "Snipe.Update", "Snipe.Deselect", "Retreat.Select", "Retreat.Update",
            }));
    }

    /// <summary>
    ///     A consideration whose result is set by hand rather than queried from a world. Stands in for EntityExists,
    ///     ExecutionHistory, and the rest. A multiplier of 0 vetoes the option regardless of what the other considerations
    ///     contribute.
    /// </summary>
    private sealed class StubConsideration : ConsiderationBase
    {
        public void Pass()
        {
            Weights = WeightValues.Neutral;
        }

        public void Veto()
        {
            Weights = WeightValues.Vetoed;
        }

        public override bool Init(AiCreationData creationData)
        {
            return true;
        }

        public override void Calculate()
        {
        }
    }

    /// <summary>
    ///     An action that records every lifecycle call it receives as "Name.Phase". Stands in for Move, FireWeapon, etc.
    ///     Recording Select, Deselect, and Update separately is what lets these tests distinguish an option that kept running
    ///     from one that was torn down and restarted.
    /// </summary>
    private sealed class RecordAction : ActionBase
    {
        private readonly List<string> _log;
        private readonly string _name;

        public RecordAction(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public override bool Init(AiCreationData creationData)
        {
            return true;
        }

        public override void Select()
        {
            Record("Select");
        }

        public override void Deselect()
        {
            Record("Deselect");
        }

        public override void Update()
        {
            Record("Update");
        }

        private void Record(string phase)
        {
            _log.Add($"{_name}.{phase}");
        }
    }

    private sealed class StubOption : OptionBase
    {
        protected override bool OnInit(AiCreationData creationData)
        {
            return true;
        }
    }

    /// <summary>Builds an option imperatively, in absence of configuration data.</summary>
    /// <returns>A StubOption with the provided considerations and actions.</returns>
    private static OptionBase BuildOption(
        IEnumerable<ConsiderationBase> considerations,
        IEnumerable<ActionBase> actions)
    {
        StubOption option = new();

        foreach (ConsiderationBase consideration in considerations)
        {
            option.AddConsideration(consideration);
        }

        foreach (ActionBase action in actions)
        {
            option.AddAction(action);
        }

        // Init adds the default TuningConsideration when the configuration does not supply one, so it has to run
        // after every Add. Every stub consideration here has an addend of 0, and an option needs both a non-zero
        // addend and a non-zero multiplier to be selected, so without that Tuning no option would ever be valid.
        option.Init(new AiCreationData());

        return option;
    }
}

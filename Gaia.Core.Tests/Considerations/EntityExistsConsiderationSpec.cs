using Gaia.Core.Blackboards;
using Gaia.Core.Considerations;
using Gaia.Core.Entities;
using Gaia.Core.Tests.Helpers;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Tests.Considerations;

/// <summary>Covers <see cref="EntityExistsConsideration" />.</summary>
/// <remarks>
///     The weight function is stubbed, so what is asserted is the bool that reached it. The list lives on a brain
///     blackboard, which is where the chapter keeps contacts and threats.
/// </remarks>
/// <seealso>"Modular AI", Section 8.5.1</seealso>
[TestFixture]
public class EntityExistsConsiderationSpec
{
    [SetUp]
    public void BuildConsideration()
    {
        _blackboard = new BrainBlackboard();

        _reportedWeights = new WeightValues(3f, 0.5f, 2f);
        _missingWeights = WeightValues.Vetoed;

        _weightFunction = new RecordingWeightFunction { Result = _reportedWeights };

        _contacts = new List<EntityBase>();
        _blackboard.SetEntityList("Contacts", _contacts);

        // Not in the list until a test puts it there.
        _lookout = new StubEntity("Lookout");

        _consideration = new EntityExistsConsideration
        {
            DataStore = _blackboard,
            VariableName = "Contacts",
            WeightFunction = _weightFunction,
            MissingVariableWeights = _missingWeights,
        };
    }

    private BrainBlackboard _blackboard;

    private WeightValues _reportedWeights;
    private WeightValues _missingWeights;

    private RecordingWeightFunction _weightFunction;

    private List<EntityBase> _contacts;
    private StubEntity _lookout;

    private EntityExistsConsideration _consideration;

    [Test]
    public void ReportsTrueWhenTheListHoldsAValidEntity()
    {
        _contacts.Add(_lookout);

        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastBool, Is.True);
            Assert.That(_consideration.Weights, Is.EqualTo(_reportedWeights));
        });
    }

    [Test]
    public void ReportsFalseWhenTheListIsEmpty()
    {
        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_weightFunction.LastBool, Is.False);
            Assert.That(_weightFunction.CalculationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ReportsFalseWhenEverythingInTheListHasGone()
    {
        // A list nobody has cleaned up must not keep an option alive.
        StubEntity gone = new("Gone") { IsValid = false };
        _contacts.Add(gone);

        _consideration.Calculate();

        Assert.That(_weightFunction.LastBool, Is.False);
    }

    [Test]
    public void ReportsTrueWhenAnyEntryIsStillValid()
    {
        StubEntity gone = new("Gone") { IsValid = false };
        StubEntity present = new("Present");
        _contacts.Add(gone);
        _contacts.Add(present);

        _consideration.Calculate();

        Assert.That(_weightFunction.LastBool, Is.True);
    }

    [Test]
    public void ReportsItsMissingValuesWhenThereIsNoListUnderThatName()
    {
        _consideration.VariableName = "Threats";

        _consideration.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(_consideration.Weights, Is.EqualTo(_missingWeights));
            Assert.That(_weightFunction.CalculationCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void SeesTheListChangeBetweenCycles()
    {
        // The store holds the list itself rather than a copy, so whoever fills it need not write it again.
        _consideration.Calculate();

        Assert.That(_weightFunction.LastBool, Is.False);

        _contacts.Add(_lookout);

        _consideration.Calculate();

        Assert.That(_weightFunction.LastBool, Is.True);
    }

    [Test]
    public void CanBeConfiguredToVetoEitherWay()
    {
        // The sniper's two uses. Veto when nothing is in the kill zone, and veto when something blocks the retreat.
        BooleanWeightFunction vetoWhenEmpty = new(WeightValues.Neutral, WeightValues.Vetoed);
        BooleanWeightFunction vetoWhenFound = new(WeightValues.Vetoed, WeightValues.Neutral);

        EntityExistsConsideration killZone = new()
        {
            DataStore = _blackboard,
            VariableName = "Contacts",
            WeightFunction = vetoWhenEmpty,
        };

        EntityExistsConsideration lineOfRetreat = new()
        {
            DataStore = _blackboard,
            VariableName = "Contacts",
            WeightFunction = vetoWhenFound,
        };

        killZone.Calculate();
        lineOfRetreat.Calculate();

        Assert.Multiple(() =>
        {
            Assert.That(killZone.Weights, Is.EqualTo(WeightValues.Vetoed));
            Assert.That(lineOfRetreat.Weights, Is.EqualTo(WeightValues.Neutral));
        });
    }
}

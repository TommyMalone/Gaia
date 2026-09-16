using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves a game-defined consideration built against the public surface alone can hold a game-defined weight
///     function and score an option with it.
/// </summary>
/// <remarks>
///     The shipped weight functions are made by loading, so a consideration assembled in code brings a function of
///     the game's own. What is covered here is a derived consideration that sets its own WeightFunction and hands its
///     distance to the function's CalculateFloat. The option's weight comes from its default tuning consideration, and the
///     function's neutral values or veto decide whether the option is selected.
/// </remarks>
[TestFixture]
public class GameDefinedConsiderationHostSpec
{
    [SetUp]
    public void AssembleReasoner()
    {
        _selections = new List<string>();
        _distance = new HostDistanceConsideration(new HostRangeWeightFunction(10f));

        ConsiderationAndActionOption approach = new("Approach");
        approach.AddConsideration(_distance);
        approach.AddAction(new HostRecordAction("Close", _selections));

        ConsiderationAndActionOption idle = new("Idle");
        idle.AddAction(new HostRecordAction("Stand", _selections));

        _reasoner = new RuleBasedReasoner();
        _reasoner.AddOption(approach);
        _reasoner.AddOption(idle);
        _reasoner.Enable();
    }

    private List<string> _selections;
    private HostDistanceConsideration _distance;
    private RuleBasedReasoner _reasoner;

    [Test]
    public void SelectsThroughAGameDefinedWeightFunction()
    {
        _distance.Distance = 5f;

        _reasoner.Update();

        Assert.That(_selections, Is.EqualTo(new[] { "Close" }), "inside the range the function reports neutral");
    }

    [Test]
    public void VetoesThroughAGameDefinedWeightFunction()
    {
        _distance.Distance = 50f;

        _reasoner.Update();

        Assert.That(_selections, Is.EqualTo(new[] { "Stand" }), "outside the range the function vetoes");
    }

    /// <summary>A game-defined consideration reading a distance the game writes, scored by whatever function it holds.</summary>
    private sealed class HostDistanceConsideration : WeightedConsiderationBase
    {
        public HostDistanceConsideration(WeightFunctionBase weightFunction)
        {
            WeightFunction = weightFunction;
        }

        /// <summary>The distance the game measured, in meters.</summary>
        public float Distance { get; set; }

        /// <inheritdoc />
        protected override string QueryPhrase => "a float";

        /// <summary>Reports failure, since this consideration is assembled rather than loaded.</summary>
        protected override bool OnInit(AiCreationData creationData)
        {
            return false;
        }

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsFloat;
        }

        /// <summary>Hands the distance to the function as a float.</summary>
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction)
        {
            return weightFunction.CalculateFloat(Distance);
        }
    }

    /// <summary>A game-defined weight function, neutral up to a range and a veto beyond it.</summary>
    private sealed class HostRangeWeightFunction : WeightFunctionBase
    {
        private readonly float _range;

        public HostRangeWeightFunction(float range)
        {
            _range = range;
        }

        /// <inheritdoc />
        public override bool ConvertsFloat => true;

        /// <summary>Reports failure, since this function is assembled rather than loaded.</summary>
        protected override bool OnInit(AiCreationData creationData)
        {
            return false;
        }

        /// <summary>Neutral at or inside the range, a veto beyond it.</summary>
        public override WeightValues CalculateFloat(float value)
        {
            return value <= _range ? WeightValues.Neutral : WeightValues.Vetoed;
        }
    }
}

using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Targets;
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
///     SelectsAnOptionBuiltFromComponentsThatWriteNoOnInit covers the other way a game reaches the bases, a consideration,
///     a weight function, a target and an action the loader builds, each of which overrides only what it answers with.
///     RefusesAChildOfAComponentThatWritesNoOnInit covers what the default does with a child element such a component does
///     not read.
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

    [Test]
    public void SelectsAnOptionBuiltFromComponentsThatWriteNoOnInit()
    {
        List<string> selections = new();

        FactorySet factories = new();
        factories.Considerations.Register("Ready", () => new HostReadyConsideration(),
            HostReadyConsideration.Description);
        factories.WeightFunctions.Register("Steady", () => new HostSteadyWeightFunction(),
            HostSteadyWeightFunction.Description);
        factories.Targets.Register("Origin", () => new HostOriginTarget(), HostOriginTarget.Description);
        factories.Actions.Register("Step", () => new HostStepAction(selections), HostStepAction.Description);

        GaiaLoader loader = new(factories, new ManualTimeSource());

        // Each of the four game types takes no attributes and no children of its own, so none writes an OnInit and the
        // default on each base carries the load. The shipped Distance consideration is what puts the game's weight
        // function and targets under a loaded parent.
        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Advance">
                                                     <Consideration Type="Ready"/>
                                                     <Consideration Type="Distance">
                                                         <From Type="Origin"/>
                                                         <To Type="Origin"/>
                                                         <WeightFunction Type="Steady"/>
                                                     </Consideration>
                                                     <Action Type="Step" Name="Advance"/>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;

        Assert.That(loader.TryLoadBrain(configurationText, "defaults.xml", out Brain? brain), Is.True);

        brain!.Enable();
        brain.Update();

        Assert.That(selections, Is.EqualTo(new[] { "Advance" }),
            "the loaded components leave the option eligible and run its action");
    }

    [Test]
    public void RefusesAChildOfAComponentThatWritesNoOnInit()
    {
        ListLogSink logSink = new();

        FactorySet factories = new();
        factories.Considerations.Register("Ready", () => new HostReadyConsideration(),
            HostReadyConsideration.Description);

        GaiaLoader loader = new(factories, new ManualTimeSource(), logSink: logSink);

        // The consideration writes no OnInit, so what refuses the Extra element is the default on its base.
        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Advance">
                                                     <Consideration Type="Ready">
                                                         <Extra/>
                                                     </Consideration>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;

        bool isLoaded = loader.TryLoadBrain(configurationText, "defaults.xml", out Brain? _);

        Assert.Multiple(() =>
        {
            Assert.That(isLoaded, Is.False);
            Assert.That(logSink.Errors,
                Has.Member("Error defaults.xml Line 5, the Consideration element does not take Extra elements"));
        });
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

    /// <summary>A game-defined consideration with nothing of its own to load, which reports neutral values.</summary>
    private sealed class HostReadyConsideration : ConsiderationBase
    {
        /// <summary>Describes the consideration for the test that registers it.</summary>
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Considerations, "Reports neutral values.").Build();

        /// <inheritdoc />
        public override void Calculate()
        {
            Weights = WeightValues.Neutral;
        }
    }

    /// <summary>A game-defined weight function with nothing of its own to load, which answers a float with neutral values.</summary>
    private sealed class HostSteadyWeightFunction : WeightFunctionBase
    {
        /// <summary>Describes the function for the test that registers it.</summary>
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.WeightFunctions, "Answers any float with neutral values.")
                .Build();

        /// <inheritdoc />
        public override bool ConvertsFloat => true;

        /// <inheritdoc />
        public override WeightValues CalculateFloat(float value)
        {
            return WeightValues.Neutral;
        }
    }

    /// <summary>A game-defined target with nothing of its own to load, which reports the world's origin.</summary>
    private sealed class HostOriginTarget : TargetBase
    {
        /// <summary>Describes the target for the test that registers it.</summary>
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Targets, "The world's origin.").Build();

        /// <inheritdoc />
        public override bool TryGetPosition(out Vector3 position)
        {
            position = Vector3.Zero;
            return true;
        }
    }

    /// <summary>A game-defined action with nothing of its own to load, which records the name it was loaded under.</summary>
    private sealed class HostStepAction : ActionBase
    {
        /// <summary>Describes the action for the test that registers it, the Name every action reads and nothing more.</summary>
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Actions, "Records the selection it receives.")
                .Add(NameAttribute).Build();

        private readonly List<string> _selections;

        /// <summary>Creates an action for the factories to build, appending each selection to the given list.</summary>
        internal HostStepAction(List<string> selections)
        {
            _selections = selections;
        }

        /// <inheritdoc />
        public override bool IsDone => true;

        /// <summary>Records the action's name.</summary>
        protected override void OnSelect()
        {
            _selections.Add(Name.Text);
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

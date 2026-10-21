using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Factories;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Services.Random;
using Gaia.Core.Services.Time;
using Gaia.Core.Spatial;
using Gaia.Core.Specifications;
using Gaia.Core.Targets;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.HostSpecs;

/// <summary>
///     Proves a game-defined consideration built against the public surface alone can hold a game-defined weight
///     function and score an option with it. The same surface lets the loader build the components a game registers,
///     holding each element to the description registered with its type, and lets a game's own group pass its option to
///     the considerations it holds.
/// </summary>
/// <remarks>
///     The shipped weight functions are made by loading, so a consideration assembled in code brings a function of
///     the game's own. The reasoner the setup assembles holds a derived consideration that sets its own WeightFunction and
///     hands its distance to the function's CalculateFloat. The option's weight comes from its default tuning
///     consideration, and the function's neutral values or veto decide whether the option is selected.
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

        // The consideration writes no OnInit and describes no slot, so the factory refuses the Extra element before the
        // base's default OnInit runs.
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

    [Test]
    public void LoadsAGameWeightedConsiderationThroughTheWeightFunctionSlotItsBaseReads()
    {
        ListLogSink logSink = new();

        FactorySet factories = new();
        factories.Considerations.Register("Range", () => new HostRangeConsideration(),
            HostRangeConsideration.Description);

        // A master is supplied so the loader reports no rolled seed into the sink the test reads.
        GaiaLoader loader = new(factories, new ManualTimeSource(), new FixedRandomSource(0f), logSink);

        // The consideration adds the slot its base reads to its own description, so the WeightFunction child is one
        // the factory allows and the base's minimum of one is met.
        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Advance">
                                                     <Consideration Type="Range">
                                                         <WeightFunction Type="Constant" Addend="2"/>
                                                     </Consideration>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;

        bool isLoaded = loader.TryLoadBrain(configurationText, "range.xml", out Brain? brain);

        Assert.Multiple(() =>
        {
            Assert.That(isLoaded, Is.True);
            Assert.That(logSink.Entries, Is.Empty);

            HostRangeConsideration loaded = (HostRangeConsideration)brain!.RootReasoner!.Options[0].Considerations[0];

            Assert.That(loaded.WeightFunction, Is.InstanceOf<ConstantWeightFunction>());
        });
    }

    [Test]
    public void PassesItsOptionToTheConsiderationsAGameGroupHolds()
    {
        HostReadyConsideration firstHeld = new();
        HostReadyConsideration secondHeld = new();
        HostGroupConsideration group = new([firstHeld, secondHeld]);
        ConsiderationAndActionOption patrol = new("Patrol");

        patrol.AddConsideration(group);

        Assert.Multiple(() =>
        {
            Assert.That(firstHeld.Option, Is.SameAs(patrol));
            Assert.That(secondHeld.Option, Is.SameAs(patrol));
        });
    }

    [Test]
    public void PassesItsOptionThroughAGameGroupHeldByAnother()
    {
        HostReadyConsideration held = new();
        HostGroupConsideration inner = new([held]);
        HostGroupConsideration outer = new([inner]);
        ConsiderationAndActionOption patrol = new("Patrol");

        patrol.AddConsideration(outer);

        Assert.That(held.Option, Is.SameAs(patrol));
    }

    [Test]
    public void LoadsAGameGroupThatDerivesTheGroupBase()
    {
        ListLogSink logSink = new();

        FactorySet factories = new();
        factories.Considerations.Register("AnyOf", () => new HostAnyOfConsideration(),
            HostAnyOfConsideration.Description);

        // A master is supplied so the loader reports no rolled seed into the sink the test reads.
        GaiaLoader loader = new(factories, new ManualTimeSource(), new FixedRandomSource(0f), logSink);

        // The group's rule skips the vetoing first Tuning, so the option weighs the second Tuning's addend of 3.
        // The shipped group's rule would weigh 0, and a default Tuning beside the group would make it 4.
        const string configurationText = """
                                         <Brain>
                                             <Reasoner Type="RuleBased">
                                                 <Option Type="ConsiderationAndAction" Name="Threatened">
                                                     <Consideration Type="AnyOf" VetoWhenNoneAllow="false">
                                                         <Consideration Type="Tuning" Addend="2" Multiplier="0"/>
                                                         <Consideration Type="Tuning" Addend="3"/>
                                                     </Consideration>
                                                 </Option>
                                             </Reasoner>
                                         </Brain>
                                         """;

        bool isLoaded = loader.TryLoadBrain(configurationText, "threats.xml", out Brain? brain);

        Assert.That(isLoaded, Is.True);
        Assert.That(logSink.Entries, Is.Empty);

        brain!.Enable();
        brain.Update();

        OptionBase threatened = brain.RootReasoner!.Options[0];
        HostAnyOfConsideration group = (HostAnyOfConsideration)threatened.Considerations[0];

        Assert.Multiple(() =>
        {
            Assert.That(group.VetoWhenNoneAllow, Is.False, "the group read its own attribute");
            Assert.That(group.Considerations[0].Option, Is.SameAs(threatened));
            Assert.That(group.Considerations[1].Option, Is.SameAs(threatened));
            Assert.That(threatened.Considerations, Has.Count.EqualTo(1), "no default Tuning beside the group");
            Assert.That(threatened.Weight, Is.EqualTo(3f),
                "the group allows the option through the Tuning that allows it");
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

    /// <summary>
    ///     A game-defined weighted consideration the loader builds, which measures a fixed distance and hands it to the
    ///     WeightFunction child its description takes from its base.
    /// </summary>
    private sealed class HostRangeConsideration : WeightedConsiderationBase
    {
        /// <summary>
        ///     Describes the consideration for the test that registers it, the WeightFunction slot its base reads and nothing
        ///     more.
        /// </summary>
        internal static readonly ComponentDescription Description =
            new DescriptionBuilder(ConceptualAbstraction.Considerations, "Weighs a fixed distance.")
                .Add(WeightFunctionSlot).Build();

        /// <inheritdoc />
        protected override string QueryPhrase => "a float";

        /// <summary>Builds the WeightFunction child, which the factory has already held to one.</summary>
        protected override bool OnInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            for (int index = 0; index < node.Children.Count; index++)
            {
                if (!TryLoadWeightFunction(node.Children[index], creationData))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        protected override bool CanConvert(WeightFunctionBase weightFunction)
        {
            return weightFunction.ConvertsFloat;
        }

        /// <summary>Hands a fixed distance to the function as a float.</summary>
        protected override WeightValues CalculateWeights(WeightFunctionBase weightFunction)
        {
            return weightFunction.CalculateFloat(5f);
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

    /// <summary>A game-defined group, which passes its option to each consideration it holds.</summary>
    private sealed class HostGroupConsideration : ConsiderationBase
    {
        private readonly ConsiderationBase[] _held;

        /// <summary>Creates a group holding the given considerations.</summary>
        public HostGroupConsideration(ConsiderationBase[] held)
        {
            _held = held;
        }

        /// <inheritdoc />
        public override void Calculate()
        {
            Weights = WeightValues.Neutral;
        }

        /// <summary>Attaches each consideration this group holds to the option this group was attached to.</summary>
        protected override void OnAttach(OptionBase option)
        {
            foreach (ConsiderationBase held in _held)
            {
                AttachChild(held, option);
            }
        }
    }

    /// <summary>
    ///     A game-defined group built on the group base, which allows its option when any consideration it holds does,
    ///     ignoring a held consideration that vetoes.
    /// </summary>
    private sealed class HostAnyOfConsideration : GroupConsiderationBase
    {
        private static readonly DescriptionBuilder Builder =
            new DescriptionBuilder(ConceptualAbstraction.Considerations,
                "Allows its option when any consideration it holds does.").Add(ConsiderationSlot);

        /// <summary>Describes the optional VetoWhenNoneAllow attribute OnGroupInit reads by.</summary>
        private static readonly AttributeDescription VetoWhenNoneAllowAttribute = Builder.Optional("VetoWhenNoneAllow",
            AttributeKind.Bool, "true",
            "Whether the group vetoes its option when no consideration it holds allows it.");

        /// <summary>Describes the group for the test that registers it, the Consideration slot its base reads and one attribute.</summary>
        internal static readonly ComponentDescription Description = Builder.Build();

        /// <summary>Whether the group vetoes its option when no consideration it holds allows it, as loaded.</summary>
        internal bool VetoWhenNoneAllow { get; private set; } = true;

        /// <summary>Takes an optional VetoWhenNoneAllow attribute.</summary>
        protected override bool OnGroupInit(AiCreationData creationData)
        {
            SpecificationNode node = creationData.Node;

            if (node.HasAttribute(VetoWhenNoneAllowAttribute.Name))
            {
                if (!node.ReadBool(VetoWhenNoneAllowAttribute.Name, out bool vetoWhenNoneAllow))
                {
                    return false;
                }

                VetoWhenNoneAllow = vetoWhenNoneAllow;
            }

            return true;
        }

        /// <summary>
        ///     Combines the considerations that allow the option by the library's standard rule. When none allows it, the
        ///     group vetoes or reports neutral values as VetoWhenNoneAllow says.
        /// </summary>
        protected override WeightValues CombineWeights()
        {
            float addend = 0f;
            float multiplier = 1f;
            float rank = WeightValues.NoRank;
            bool anyAllows = false;

            for (int index = 0; index < Considerations.Count; index++)
            {
                WeightValues weights = Considerations[index].Weights;

                if (weights.Multiplier <= 0f)
                {
                    continue;
                }

                anyAllows = true;
                addend += weights.Addend;
                multiplier *= weights.Multiplier;
                rank = MathF.Max(rank, weights.Rank);
            }

            if (anyAllows)
            {
                return new WeightValues(addend, multiplier, rank);
            }

            return VetoWhenNoneAllow ? WeightValues.Vetoed : WeightValues.Neutral;
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

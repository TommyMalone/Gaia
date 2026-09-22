using System;
using Gaia.Core.Actions;
using Gaia.Core.Brains;
using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Targets;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     One factory per kind of component, the single thing creation data carries so any component can build children
    ///     of any kind.
    /// </summary>
    /// <remarks>
    ///     The constructor registers every built-in component with its description, so a registered name always means a
    ///     loadable component that a tool can describe, and a game registers its own components on the same set before loading
    ///     begins.
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class FactorySet
    {
        /// <summary>Creates a set holding every built-in component that can load itself.</summary>
        public FactorySet()
        {
            Actions.Register("Pause", () => new PauseAction(), PauseAction.Description);
            Actions.Register("Subreasoner", () => new SubreasonerAction(), SubreasonerAction.Description);

            Considerations.Register("BooleanVariable", () => new BooleanVariableConsideration(),
                BooleanVariableConsideration.Description);
            Considerations.Register("ConsiderationSet", () => new ConsiderationSetConsideration(),
                Pending(ConceptualAbstraction.Considerations,
                    "Groups considerations and reports their combined values as one."));
            Considerations.Register("Distance", () => new DistanceConsideration(), DistanceConsideration.Description);
            Considerations.Register("EntityExists", () => new EntityExistsConsideration(),
                EntityExistsConsideration.Description);
            Considerations.Register("ExecutionHistory", () => new ExecutionHistoryConsideration(),
                ExecutionHistoryConsideration.Description);
            Considerations.Register("FloatVariable", () => new FloatVariableConsideration(),
                FloatVariableConsideration.Description);
            Considerations.Register("IntVariable", () => new IntVariableConsideration(),
                Pending(ConceptualAbstraction.Considerations, "Reads a whole number from a blackboard."));
            Considerations.Register("StringVariable", () => new StringVariableConsideration(),
                Pending(ConceptualAbstraction.Considerations, "Reads a piece of text from a blackboard."));
            Considerations.Register("Tuning", () => new TuningConsideration(), TuningConsideration.Description);

            Options.Register("ConsiderationAndAction", () => new ConsiderationAndActionOption(),
                ConsiderationAndActionOption.Description);

            Reasoners.Register("DualUtility", () => new DualUtilityReasoner(), DualUtilityReasoner.Description);
            Reasoners.Register("RuleBased", () => new RuleBasedReasoner(), RuleBasedReasoner.Description);
            Reasoners.Register("Sequence", () => new SequenceReasoner(), SequenceReasoner.Description);

            Regions.Register("Box", () => new BoxRegion(),
                Pending(ConceptualAbstraction.Regions, "A box aligned with the world's axes."));
            Regions.Register("Capsule", () => new CapsuleRegion(),
                Pending(ConceptualAbstraction.Regions, "A cylinder with a rounded top and bottom."));
            Regions.Register("Circle", () => new CircleRegion(),
                Pending(ConceptualAbstraction.Regions, "A disc on the ground, which ignores height."));
            Regions.Register("ConvexHull", () => new ConvexHullRegion(),
                Pending(ConceptualAbstraction.Regions, "A volume bounded by planes."));
            Regions.Register("Cylinder", () => new CylinderRegion(),
                Pending(ConceptualAbstraction.Regions, "A cylinder standing on the ground."));
            Regions.Register("ExtrudedPolygon", () => new ExtrudedPolygonRegion(),
                Pending(ConceptualAbstraction.Regions, "A footprint of any shape given a height."));
            Regions.Register("Polygon", () => new PolygonRegion(),
                Pending(ConceptualAbstraction.Regions,
                    "A footprint of any shape on the ground, which ignores height."));
            Regions.Register("Rectangle", () => new RectangleRegion(),
                Pending(ConceptualAbstraction.Regions, "A rectangle on the ground, which ignores height."));
            Regions.Register("Sphere", () => new SphereRegion(), Pending(ConceptualAbstraction.Regions, "A ball."));

            Targets.Register("ByName", () => new ByNameTarget(),
                Pending(ConceptualAbstraction.Targets, "An entity found by name in a list on a blackboard."));
            Targets.Register("Position", () => new PositionTarget(), PositionTarget.Description);
            Targets.Register("Self", () => new SelfTarget(), SelfTarget.Description);

            WeightFunctions.Register("Constant", () => new ConstantWeightFunction(),
                ConstantWeightFunction.Description);
            WeightFunctions.Register("Boolean", () => new BooleanWeightFunction(), BooleanWeightFunction.Description);
            WeightFunctions.Register("String", () => new StringWeightFunction(),
                Pending(ConceptualAbstraction.WeightFunctions,
                    "Matches the input against pieces of text and reports the weight values of the one it matches."));
            WeightFunctions.Register("FloatSequence", () => new FloatSequenceWeightFunction(),
                FloatSequenceWeightFunction.Description);
            WeightFunctions.Register("Curve", () => new CurveWeightFunction(), CurveWeightFunction.Description);
        }

        /// <summary>Builds the actions options run.</summary>
        public ComponentFactory<ActionBase> Actions { get; } = new();

        /// <summary>Builds the considerations that score options.</summary>
        public ComponentFactory<ConsiderationBase> Considerations { get; } = new();

        /// <summary>Builds the options reasoners choose among.</summary>
        public ComponentFactory<OptionBase> Options { get; } = new();

        /// <summary>Builds the reasoners that decide.</summary>
        public ComponentFactory<ReasonerBase> Reasoners { get; } = new();

        /// <summary>Builds the regions that answer containment and sampling.</summary>
        public ComponentFactory<RegionBase> Regions { get; } = new();

        /// <summary>Builds the targets considerations and actions point at.</summary>
        public ComponentFactory<TargetBase> Targets { get; } = new();

        /// <summary>Builds the weight functions considerations convert through.</summary>
        public ComponentFactory<WeightFunctionBase> WeightFunctions { get; } = new();

        /// <summary>
        ///     Describes the Brain element, the document's root, which no factory builds and which holds one Reasoner at
        ///     most.
        /// </summary>
        public ComponentDescription Root => Brain.Description;

        /// <summary>
        ///     The factory of the given conceptual abstraction, the same object as the property named for it, for a caller
        ///     that holds a <see cref="SlotDescription.Abstraction" /> and asks what may fill the slot.
        /// </summary>
        public IComponentFactory Factory(ConceptualAbstraction abstraction)
        {
            switch (abstraction)
            {
                case ConceptualAbstraction.Actions:
                    return Actions;
                case ConceptualAbstraction.Considerations:
                    return Considerations;
                case ConceptualAbstraction.Options:
                    return Options;
                case ConceptualAbstraction.Reasoners:
                    return Reasoners;
                case ConceptualAbstraction.Regions:
                    return Regions;
                case ConceptualAbstraction.Targets:
                    return Targets;
                default:
                    return WeightFunctions;
            }
        }

        /// <summary>
        ///     The description of a component whose attributes and slots are not presently described beside its name fields,
        ///     which carries its conceptual abstraction and summary alone.
        /// </summary>
        private static ComponentDescription Pending(ConceptualAbstraction abstraction, string summary)
        {
            return new ComponentDescription(abstraction, summary, Array.Empty<AttributeDescription>(),
                Array.Empty<SlotDescription>());
        }
    }
}

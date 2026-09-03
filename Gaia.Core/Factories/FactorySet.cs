using Gaia.Core.Actions;
using Gaia.Core.Considerations;
using Gaia.Core.Options;
using Gaia.Core.Reasoners;
using Gaia.Core.Regions;
using Gaia.Core.Targets;
using Gaia.Core.WeightFunctions;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     One factory per conceptual abstraction, the single thing creation data carries so any component can build
    ///     children of any kind.
    /// </summary>
    /// <remarks>
    ///     The constructor registers every built-in component whose loading is implemented, so a registered name always
    ///     means a loadable component, and a game registers its own components on the same set before loading begins. What
    ///     registration records is also the authoring tool's catalog, which is why it lives in one place.
    /// </remarks>
    /// <seealso>Kevin Dill, "Six Factory System Tricks for Extensibility and Library Reuse", Game AI Pro 3, CRC Press, 2017</seealso>
    public sealed class FactorySet
    {
        /// <summary>Creates a set holding every built-in component that can load itself.</summary>
        public FactorySet()
        {
            Considerations.Register("BooleanVariable", () => new BooleanVariableConsideration());
            Considerations.Register("ConsiderationSet", () => new ConsiderationSetConsideration());
            Considerations.Register("Distance", () => new DistanceConsideration());
            Considerations.Register("EntityExists", () => new EntityExistsConsideration());
            Considerations.Register("ExecutionHistory", () => new ExecutionHistoryConsideration());
            Considerations.Register("FloatVariable", () => new FloatVariableConsideration());
            Considerations.Register("IntVariable", () => new IntVariableConsideration());
            Considerations.Register("StringVariable", () => new StringVariableConsideration());
            Considerations.Register("Tuning", () => new TuningConsideration());

            Regions.Register("Box", () => new BoxRegion());
            Regions.Register("Capsule", () => new CapsuleRegion());
            Regions.Register("Circle", () => new CircleRegion());
            Regions.Register("ConvexHull", () => new ConvexHullRegion());
            Regions.Register("Cylinder", () => new CylinderRegion());
            Regions.Register("ExtrudedPolygon", () => new ExtrudedPolygonRegion());
            Regions.Register("Polygon", () => new PolygonRegion());
            Regions.Register("Rectangle", () => new RectangleRegion());
            Regions.Register("Sphere", () => new SphereRegion());

            Targets.Register("ByName", () => new ByNameTarget());
            Targets.Register("Position", () => new PositionTarget());
            Targets.Register("Self", () => new SelfTarget());

            WeightFunctions.Register("Constant", () => new ConstantWeightFunction());
            WeightFunctions.Register("Boolean", () => new BooleanWeightFunction());
            WeightFunctions.Register("String", () => new StringWeightFunction());
            WeightFunctions.Register("FloatSequence", () => new FloatSequenceWeightFunction());
            WeightFunctions.Register("Curve", () => new CurveWeightFunction());
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
    }
}

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     The conceptual abstractions a <see cref="FactorySet" /> builds, one per factory on the set. A conceptual
    ///     abstraction is one of the kinds of component a brain is composed from, as <see cref="IAiComponent" /> defines it.
    /// </summary>
    public enum ConceptualAbstraction
    {
        Actions,
        Considerations,
        Options,
        Reasoners,
        Regions,
        Targets,
        WeightFunctions,
    }
}

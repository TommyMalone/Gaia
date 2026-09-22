using System.Collections.Generic;
using Gaia.Core.Strings;

namespace Gaia.Core.Factories
{
    /// <summary>
    ///     What every <see cref="ComponentFactory{TBase}" /> says about the types it holds, whatever it builds, so a
    ///     caller holding a <see cref="ConceptualAbstraction" /> reaches the factory through <see cref="FactorySet.Factory" />
    ///     without knowing its base type.
    /// </summary>
    public interface IComponentFactory
    {
        /// <summary>The registered type names, in the order they were first registered.</summary>
        IReadOnlyList<AiString> Types { get; }

        /// <summary>Reports the description registered under a type name.</summary>
        /// <returns>True when the name is registered. False, with a null description, when it is not.</returns>
        bool TryGetDescription(AiString type, out ComponentDescription? description);
    }
}

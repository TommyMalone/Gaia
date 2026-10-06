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

        /// <summary>
        ///     The registered type names in the order of <see cref="Types" />, as each was spelled at its latest
        ///     registration.
        /// </summary>
        /// <remarks>
        ///     An <see cref="AiString" /> keeps no spelling of its own. Its Text is whatever casing the string table met
        ///     first, so a tool that writes a Type into a document reads the spelling here.
        /// </remarks>
        IReadOnlyList<string> TypeSpellings { get; }

        /// <summary>Reports the description registered under a type name.</summary>
        /// <returns>True when the name is registered. False, with a null description, when it is not.</returns>
        bool TryGetDescription(AiString type, out ComponentDescription? description);
    }
}

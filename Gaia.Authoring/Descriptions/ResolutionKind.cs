using Gaia.Core.Factories;

namespace Gaia.Authoring.Descriptions
{
    /// <summary>What <see cref="DescriptionResolver" /> found for an element of a document.</summary>
    public enum ResolutionKind
    {
        /// <summary>
        ///     The <see cref="Resolution" /> is a default value, one nothing has assigned, such as a field read before any
        ///     answer is stored in it. <see cref="DescriptionResolver" /> never answers it.
        /// </summary>
        None = 0,

        /// <summary>
        ///     The element resolved to a <see cref="ComponentDescription" />, which <see cref="Resolution.Description" />
        ///     holds.
        /// </summary>
        Described = 1,

        /// <summary>
        ///     The element fills a <see cref="SlotDescription" /> of a <see cref="ConceptualAbstraction" />, whose factory
        ///     picks a component by Type, and the element carries no Type.
        /// </summary>
        NoType = 2,

        /// <summary>
        ///     The element fills a slot of a conceptual abstraction, and the factory of that abstraction has nothing
        ///     registered under the element's Type.
        /// </summary>
        UnknownType = 3,

        /// <summary>
        ///     No slot of the parent's description takes the element's name, or the element is a root whose name is not
        ///     <see cref="FactorySet.RootName" />.
        /// </summary>
        NoSlot = 4,

        /// <summary>An element above this one resolved to no description, so no slot is known for this one to fill.</summary>
        BeneathUndescribed = 5,
    }
}

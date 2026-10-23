using Gaia.Core.Factories;

namespace Gaia.Authoring.Descriptions
{
    /// <summary>What <see cref="DescriptionResolver" /> answers for one element of a document.</summary>
    /// <remarks>
    ///     A default Resolution, which a field holds before anything assigns it, reads <see cref="ResolutionKind.None" />
    ///     with a null <see cref="Description" /> and <see cref="Slot" />.
    /// </remarks>
    public readonly struct Resolution
    {
        /// <summary>Creates the answer for one element.</summary>
        internal Resolution(ResolutionKind kind, ComponentDescription? description, SlotDescription? slot)
        {
            Kind = kind;
            Description = description;
            Slot = slot;
        }

        /// <summary>Whether the element resolved to a description, or why it did not.</summary>
        public ResolutionKind Kind { get; }

        /// <summary>
        ///     The <see cref="ComponentDescription" /> the element resolved to, or null when <see cref="Kind" /> is not
        ///     <see cref="ResolutionKind.Described" />.
        /// </summary>
        public ComponentDescription? Description { get; }

        /// <summary>
        ///     The <see cref="SlotDescription" /> of the parent's description that the element fills, or null for a root and
        ///     when <see cref="Kind" /> is <see cref="ResolutionKind.NoSlot" /> or
        ///     <see cref="ResolutionKind.BeneathUndescribed" />.
        /// </summary>
        public SlotDescription? Slot { get; }
    }
}
